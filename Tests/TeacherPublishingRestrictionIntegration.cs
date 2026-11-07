using System;
using System.Data.SqlClient;
using System.Data.Entity;
using System.Linq;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using ECOLearning.Areas.Instructor.Models;
using ECOLearning.Models;
using ECOLearning.Services;

class TeacherPublishingRestrictionIntegration
{
    class FakeSession : HttpSessionStateBase
    {
        readonly Dictionary<string,object> values=new Dictionary<string,object>();
        public override object this[string name] { get { object value; return values.TryGetValue(name,out value)?value:null; } set { values[name]=value; } }
    }
    class FakeServer : HttpServerUtilityBase { public override string MapPath(string path) { return path; } }
    class FakeContext : HttpContextBase
    {
        readonly FakeSession session=new FakeSession();
        readonly FakeServer server=new FakeServer();
        public FakeContext(int teacher) { session["UserId"]=teacher; session["Role"]="GiaoVien"; }
        public override HttpSessionStateBase Session { get { return session; } }
        public override HttpServerUtilityBase Server { get { return server; } }
    }
    static int checks;
    static void Check(bool ok,string message) { if(!ok) throw new Exception("FAIL: "+message); Console.WriteLine("PASS: "+message); checks++; }
    static int Main()
    {
        string key="TR"+Guid.NewGuid().ToString("N").Substring(0,10), email=key+"@test.invalid";
        int teacherId=0;
        using(var db=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
        {
            try
            {
                int adminId=db.NguoiDung.First(u=>u.vaiTro=="Admin").maNguoiDung;
                var category=db.DanhMuc.First();
                var teacher=new NguoiDung{hoTen="Temporary restriction test",email=email,matKhau="Test-only",vaiTro="GiaoVien",trangThai=true};
                db.NguoiDung.Add(teacher); db.SaveChanges(); teacherId=teacher.maNguoiDung;
                db.KhoaHoc.Add(new KhoaHoc{maKhoaHoc=key,tenKhoaHoc="Temporary restriction test",maGiaoVien=teacherId,maDanhMuc=category.maDanhMuc,giaBan=0,trangThai="Đã xuất bản",capDo="Cơ bản"}); db.SaveChanges();
                db.KhoaHoc.Add(new KhoaHoc{maKhoaHoc=key+"D",tenKhoaHoc="Temporary restriction draft",maGiaoVien=teacherId,maDanhMuc=category.maDanhMuc,giaBan=0,trangThai="Bản nháp",capDo="Cơ bản"}); db.SaveChanges();
                int notifications=0;
                var service=new TeacherPublishingRestrictionService(db,(address,name,subject,body,title,highlight,tone)=>{ notifications++; return true; });
                Check(service.Installed() && service.Active(teacherId)==null,"migration installed and initial state unrestricted");
                Check(!service.Apply(adminId,teacherId,"bad","Lý do hợp lệ").ThanhCong,"invalid duration rejected");
                Check(!service.Apply(adminId,teacherId,"2d","a").ThanhCong,"short reason rejected");
                string[] durations={"2d","5d","7d","20d","1m","3m","6m","indefinite"};
                foreach(string duration in durations)
                {
                    Check(service.Apply(adminId,teacherId,duration,"Lý do kiểm thử").ThanhCong,"apply "+duration);
                    var active=service.Active(teacherId);
                    Check(active!=null && (duration=="indefinite" ? !active.EndsAtUtc.HasValue : active.EndsAtUtc>active.StartedAtUtc),"active period "+duration);
                    if (duration.EndsWith("d")) Check((active.EndsAtUtc.Value-active.StartedAtUtc).TotalDays==int.Parse(duration.Substring(0,duration.Length-1)),"exact duration "+duration);
                    if (duration.EndsWith("m")) Check(active.EndsAtUtc.Value==active.StartedAtUtc.AddHours(7).AddMonths(int.Parse(duration.Substring(0,duration.Length-1))).AddHours(-7),"calendar month duration "+duration);
                    if (duration=="2d")
                    {
                        using(var controller=new ECOLearning.Areas.Instructor.Controllers.CourseController())
                        {
                            controller.ControllerContext=new ControllerContext(new FakeContext(teacherId),new RouteData(),controller);
                            Check(controller.Create() is RedirectToRouteResult,"restricted teacher cannot open create course");
                            Check(controller.Create(new CourseCreateOrEditViewModel(),"draft") is RedirectToRouteResult,"restricted teacher cannot post new draft");
                            Check(controller.Edit(new CourseCreateOrEditViewModel{MaKhoaHoc=key+"D"},"submit") is RedirectToRouteResult,"restricted teacher cannot submit from edit");
                            Check(controller.SubmitForApproval(key+"D") is RedirectToRouteResult,"restricted teacher cannot submit existing draft");
                            Check(db.KhoaHoc.AsNoTracking().Single(k=>k.maKhoaHoc==key+"D").trangThai=="Bản nháp","blocked submissions leave draft unchanged");
                        }
                    }
                    Check(!service.Apply(adminId,teacherId,duration,"Lý do kiểm thử").ThanhCong,"duplicate restriction rejected "+duration);
                    Check(db.KhoaHoc.Single(k=>k.maKhoaHoc==key).trangThai=="Đã xuất bản","published course unchanged "+duration);
                    Check(service.Lift(adminId,teacherId).ThanhCong && service.Active(teacherId)==null,"manual lift "+duration);
                }
                Check(notifications==16,"apply and lift generate notifications");
                Check(service.Apply(adminId,teacherId,"2d","Kiểm thử hết hạn").ThanhCong,"apply expiring restriction");
                db.Database.ExecuteSqlCommand("UPDATE dbo.TeacherPublishingRestriction SET EndsAtUtc=DATEADD(SECOND,-1,SYSUTCDATETIME()) WHERE TeacherId=@p0 AND LiftedAtUtc IS NULL",teacherId);
                Check(service.Active(teacherId)==null,"expired restriction no longer applies");
                Check(service.Apply(adminId,teacherId,"5d","Kiểm thử áp dụng lại").ThanhCong,"new restriction after expiry");
                Check(service.Active(teacherId)!=null,"replacement restriction active");
                Check(service.Lift(adminId,teacherId).ThanhCong,"lift replacement restriction");
                Check(db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.NhatKyQuanTri WHERE loaiDoiTuong='NguoiDung' AND maDoiTuong=@p0 AND hanhDong IN ('HanCheDangKhoaHoc','GoHanCheDangKhoaHoc')",teacherId.ToString()).Single()==19,"audit log records actions");
                var failedEmail=new TeacherPublishingRestrictionService(db,(address,name,subject,body,title,highlight,tone)=>false);
                var emailResult=failedEmail.Apply(adminId,teacherId,"2d","Kiểm thử lỗi email");
                Check(emailResult.ThanhCong && emailResult.ThongBao.Contains("Chưa gửi được email") && failedEmail.Active(teacherId)!=null,"email failure preserves restriction and informs admin");
                Check(failedEmail.Lift(adminId,teacherId).ThanhCong,"lift works after email failure");
                Console.WriteLine("ALL "+checks+" CHECKS PASSED"); return 0;
            }
            catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally
            {
                if(teacherId>0)
                {
                    db.Database.ExecuteSqlCommand("DELETE dbo.TeacherPublishingRestriction WHERE TeacherId=@p0",teacherId);
                    db.Database.ExecuteSqlCommand("DELETE dbo.NhatKyQuanTri WHERE loaiDoiTuong='NguoiDung' AND maDoiTuong=@p0 AND hanhDong IN ('HanCheDangKhoaHoc','GoHanCheDangKhoaHoc')",teacherId.ToString());
                    db.Database.ExecuteSqlCommand("DELETE dbo.KhoaHoc WHERE maKhoaHoc=@p0 AND maGiaoVien=@p1",key,teacherId);
                    db.Database.ExecuteSqlCommand("DELETE dbo.KhoaHoc WHERE maKhoaHoc=@p0 AND maGiaoVien=@p1",key+"D",teacherId);
                    db.Database.ExecuteSqlCommand("DELETE dbo.NguoiDung WHERE maNguoiDung=@p0 AND email=@p1",teacherId,email);
                }
                Console.WriteLine("Temporary restriction fixtures removed.");
            }
        }
    }
}
