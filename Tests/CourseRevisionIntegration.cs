using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.Areas.Instructor.Models;
class CourseRevisionIntegration {
 static int checks;
 static void Check(bool value,string text){if(!value)throw new Exception("FAIL: "+text);Console.WriteLine("PASS: "+text);checks++;}
 static void Reject(Action action,string text){try{action();}catch(InvalidOperationException){Check(true,text);return;}throw new Exception("Expected rejection: "+text);}
 static int Main(){
  string id="RV"+Guid.NewGuid().ToString("N").Substring(0,10), temp=Path.GetTempFileName();int student=0,other=0;
  using(var db=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1()) try {
   var teacher=db.NguoiDung.First(u=>u.vaiTro=="GiaoVien"&&u.trangThai!=false);var category=db.DanhMuc.First(c=>c.trangThai!=false);
   var user=new NguoiDung {hoTen="Revision test",email=id+"@revision.test",matKhau="test-only",vaiTro="HocVien",trangThai=true};
   var second=new NguoiDung {hoTen="Revision test unfinished",email="2"+id+"@revision.test",matKhau="test-only",vaiTro="HocVien",trangThai=true};
   db.NguoiDung.Add(user);db.NguoiDung.Add(second);db.SaveChanges();student=user.maNguoiDung;other=second.maNguoiDung;
   var course=new KhoaHoc {maKhoaHoc=id,tenKhoaHoc="Live title",maGiaoVien=teacher.maNguoiDung,maDanhMuc=category.maDanhMuc,giaBan=100,trangThai="Đã xuất bản",capDo="Cơ bản"};db.KhoaHoc.Add(course);db.SaveChanges();
   db.ChuongHoc.Add(new ChuongHoc {maChuong=id+"ch",maKhoaHoc=id,tenChuong="Original chapter",thuTu=1});db.SaveChanges();
   db.BaiHoc.Add(new BaiHoc {maBaiHoc=id+"_1",maChuong=id+"ch",tenBaiHoc="Original",thuTu=1,thoiLuong=100,duongDanVideo="~/revision-test.mp4"});
   db.DangKyKhoaHoc.Add(new DangKyKhoaHoc {maDangKy="D"+id,maKhoaHoc=id,maHocVien=student,ngayDangKy=DateTime.Now});
   db.DangKyKhoaHoc.Add(new DangKyKhoaHoc {maDangKy="E"+id,maKhoaHoc=id,maHocVien=other,ngayDangKy=DateTime.Now});db.SaveChanges();
   db.TienDoBaiHoc.Add(new TienDoBaiHoc {maHocVien=student,maBaiHoc=id+"_1",daHoanThanh=true,thoiGianXemDungLai=100,ngayHoanThanh=DateTime.Now});db.SaveChanges();
   db.Database.ExecuteSqlCommand("INSERT dbo.QuizCertificate(CourseId,StudentId,RequestedAt,IssuedAt,Code) VALUES(@p0,@p1,GETDATE(),GETDATE(),@p2)",id,student,"CERT-"+id);
   var service=new CourseRevisionService(db,_=>temp);
   var model=new CourseCreateOrEditViewModel {MaKhoaHoc=id,TenKhoaHoc="Revised title",GiaBan=200,MaDanhMuc=category.maDanhMuc,CapDo="Cơ bản",Lessons=new List<CourseLessonInput>{new CourseLessonInput {Id=id+"_1",Title="Changed original",VideoToken="video",Duration=100},new CourseLessonInput {Title="Added lesson",VideoToken="video",Duration=100}}};
   var uploads=new Dictionary<string,string[]> {{"video",new[]{"video","~/revision-test.mp4","test.mp4"}}};
   service.Save(course,model,uploads,false);
   Check(service.ForTeacher(teacher.maNguoiDung)[id].Status=="Draft","teacher draft list includes saved revision");
   Check(!service.ForTeacher(student).ContainsKey(id),"revision list isolated to owner");
   using(var fresh=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1()) {Check(fresh.KhoaHoc.Find(id).tenKhoaHoc=="Live title","draft leaves public metadata unchanged");Check(fresh.BaiHoc.Count(l=>l.maBaiHoc.StartsWith(id+"_"))==1,"draft leaves live lessons unchanged");}
   Reject(()=>service.Save(course,model,uploads,false),"stale editor cannot overwrite newer draft");
   model.RevisionVersion=service.Get(id).Version;service.Save(course,model,uploads,true);var pending=service.Get(id);
   Check(service.ForTeacher(teacher.maNguoiDung)[id].Status=="Pending","teacher pending list includes submitted revision");
   Check(pending.Status=="Pending","submit creates pending revision without hiding live course");
   Reject(()=>service.Save(course,model,uploads,false),"pending revision cannot be edited");
   Reject(()=>service.Cancel(id,student,pending.Version),"another account cannot cancel submitted revision");
   service.Cancel(id,teacher.maNguoiDung,pending.Version);
   Check(service.Get(id).Status=="Draft","cancel returns revision to editable draft");
   db.Entry(course).Reload();Check(course.trangThai=="Đã xuất bản"&&course.tenKhoaHoc=="Live title","cancel leaves published course unchanged");
   model.RevisionVersion=service.Get(id).Version;service.Save(course,model,uploads,true);pending=service.Get(id);
   Reject(()=>service.Review(id,"old",teacher.maNguoiDung,true,null),"approval must match reviewed version");
   service.Review(id,pending.Version,teacher.maNguoiDung,false,"Please revise");db.Entry(course).Reload();Check(course.tenKhoaHoc=="Live title"&&course.trangThai=="Đã xuất bản","rejection preserves live course");
   model.RevisionVersion=service.Get(id).Version;service.Save(course,model,uploads,true);pending=service.Get(id);
   File.Delete(temp);Reject(()=>service.Review(id,pending.Version,teacher.maNguoiDung,true,null),"missing media blocks approval atomically");Check(service.Get(id).Status=="Pending","failed approval leaves revision pending");
   File.WriteAllText(temp,"test media");service.Review(id,pending.Version,teacher.maNguoiDung,true,null);
   using(var fresh=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1()) {
    Check(fresh.KhoaHoc.Find(id).tenKhoaHoc=="Revised title"&&fresh.KhoaHoc.Find(id).giaBan==200,"approved metadata applied");
    Check(fresh.BaiHoc.Find(id+"_1").tenBaiHoc=="Changed original","original lesson identity retained");
    Check(fresh.BaiHoc.Count(l=>l.maBaiHoc.StartsWith(id+"_"))==2,"new lesson published only after approval");
    var state=new LearningService(fresh).TienDoCacKhoa(student,new List<string>{id})[id];Check(state.DaHoanThanh&&state.PhanTram==100&&state.SoBaiXong==1,"previous completion preserved without faking watched lessons");
    Check(!new LearningService(fresh).TienDoCacKhoa(other,new List<string>{id})[id].DaHoanThanh,"unfinished learner must still learn required lessons");
    Check(new QuizService(fresh).Student(id,student).LessonsDone,"quiz eligibility retained for completed learner");
    Check(!new QuizService(fresh).Student(id,other).LessonsDone,"quiz remains locked for unfinished learner");
    var certificate=new CourseRevisionService(fresh,_=>temp).Certificates(student)[id];
    Check(certificate.CourseTitle=="Live title"&&certificate.DurationSeconds==100,"issued certificate keeps original course title and duration");
    Check(fresh.TienDoBaiHoc.Any(t=>t.maHocVien==student&&t.maBaiHoc==id+"_1"&&t.daHoanThanh==true),"existing watched progress retained");
   }
   Check(!service.ForTeacher(teacher.maNguoiDung).ContainsKey(id),"approved revision disappears from draft and pending lists");
   Reject(()=>service.Review(id,pending.Version,teacher.maNguoiDung,true,null),"approval replay rejected");
   Console.WriteLine(checks+" revision checks passed.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
  finally {
   foreach(var sql in new[]{"DELETE dbo.CourseCertificateSnapshot WHERE CourseId=@p0","DELETE dbo.QuizCertificate WHERE CourseId=@p0","DELETE dbo.CourseCompletionProtection WHERE CourseId=@p0","DELETE dbo.CourseRevision WHERE CourseId=@p0","DELETE dbo.TienDoBaiHoc WHERE maBaiHoc LIKE @p0+'[_]%'","DELETE dbo.TienDoHocTap WHERE maKhoaHoc=@p0","DELETE dbo.DangKyKhoaHoc WHERE maKhoaHoc=@p0","DELETE dbo.TaiNguyen WHERE maBaiHoc LIKE @p0+'[_]%'","DELETE dbo.BaiHoc WHERE maBaiHoc LIKE @p0+'[_]%'","DELETE dbo.ChuongHoc WHERE maKhoaHoc=@p0","DELETE dbo.KhoaHoc WHERE maKhoaHoc=@p0"}) db.Database.ExecuteSqlCommand(sql,id);
   if(student>0)db.Database.ExecuteSqlCommand("DELETE dbo.NguoiDung WHERE maNguoiDung=@p0",student);if(other>0)db.Database.ExecuteSqlCommand("DELETE dbo.NguoiDung WHERE maNguoiDung=@p0",other);if(File.Exists(temp)) File.Delete(temp);
  }
 }
}
