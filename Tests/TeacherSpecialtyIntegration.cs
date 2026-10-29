using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.Areas.Instructor.Models;
using CourseController=ECOLearning.Areas.Instructor.Controllers.CourseController;
using ApprovalController=ECOLearning.Areas.Admin.Controllers.CourseApprovalController;
using AdminSpecialtyController=ECOLearning.Areas.Admin.Controllers.TeacherSpecialtyController;
class SpecialtySession:HttpSessionStateBase {
 readonly Dictionary<string,object> values=new Dictionary<string,object>();
 public override object this[string name] {get {object v;return values.TryGetValue(name,out v)?v:null;}set{values[name]=value;}}
}
class SpecialtyRequest:HttpRequestBase {public override NameValueCollection Form {get{return new NameValueCollection();}}}
class SpecialtyServer:HttpServerUtilityBase {public override string MapPath(string path){return System.IO.Path.GetTempPath();}}
class SpecialtyContext:HttpContextBase {
 readonly SpecialtySession session=new SpecialtySession();
 public SpecialtyContext(int user){session["UserId"]=user;}
 public override HttpSessionStateBase Session {get{return session;}}
 public override HttpRequestBase Request {get{return new SpecialtyRequest();}}
 public override HttpServerUtilityBase Server {get{return new SpecialtyServer();}}
}
class TeacherSpecialtyIntegration {
 static int count=0;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+(++count)+": "+message);}
 static void Denied(Action action,string message){try{action();}catch(InvalidOperationException){Check(true,message);return;}throw new Exception("Expected denial: "+message);}
 static NguoiDung User(string email,string role){return new NguoiDung{hoTen="Specialty fixture",email=email,matKhau="test-only",vaiTro=role,trangThai=true};}
 static int Main(){
  var key="SP"+Guid.NewGuid().ToString("N").Substring(0,10);
  int learner=0,teacher=0,admin=0;string course=key;
  using(var db=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1()) try {
   var service=new TeacherSpecialtyService(db);Check(service.Installed(),"migration installed");
   var cats=db.DanhMuc.Where(c=>c.trangThai!=false).Select(c=>c.maDanhMuc).Take(3).ToList();if(cats.Count!=3)throw new Exception("Need three active categories");
   var u=User(key+"@specialty.test","HocVien");var t=User("t"+key+"@specialty.test","GiaoVien");var a=User("a"+key+"@specialty.test","Admin");
   db.NguoiDung.Add(u);db.NguoiDung.Add(t);db.NguoiDung.Add(a);db.SaveChanges();learner=u.maNguoiDung;teacher=t.maNguoiDung;admin=a.maNguoiDung;
   db.Database.ExecuteSqlCommand("INSERT dbo.DangKyGiaoVien(maDon,maNguoiDung,chuyenMon,kinhNghiem,tenNganHang,soTaiKhoan,tenChuTaiKhoan,ngayGui,trangThai) VALUES(@p0,@p1,N'Legacy arbitrary text',N'Fixture experience',N'ACB','123456','FIXTURE',GETDATE(),'Pending')",key,learner);
   Check(!service.Approved(teacher).Any(),"new account receives no inferred permissions");
   Denied(()=>service.Require(teacher,cats[0]),"new account without approved categories cannot create course");
   db.Database.ExecuteSqlCommand("INSERT dbo.TeacherSpecialtyPolicy(TeacherId,LegacyAccess) VALUES(@p0,1)",teacher);
   Denied(()=>service.Require(teacher,cats[0]),"legacy teacher without approved specialty cannot pick a category for a new course");
   Check(service.Allowed(teacher).Count==0&&service.NeedsSpecialty(teacher),"legacy access no longer grants every category");
   service.Require(teacher,cats[1],cats[1]);Check(true,"existing course keeps its current category without approval");
   Denied(()=>service.Require(teacher,cats[0],cats[1]),"existing course cannot move to an unapproved category");
   Check(!service.Approved(teacher).Any(),"legacy access does not invent specialty approvals");
   using(var controller=new CourseController()){
    controller.ControllerContext=new ControllerContext{Controller=controller,HttpContext=new SpecialtyContext(teacher)};
    var r=controller.Create() as RedirectToRouteResult;
    Check(r!=null&&(string)r.RouteValues["controller"]=="Specialty","create page redirects teacher without specialty to the proposal page");
   }
   Denied(()=>service.ValidateSelection(new[]{"NONEXISTENT"}),"forged category rejected");
   service.SaveRequested(key,learner,new[]{cats[0]});
   Denied(()=>service.SaveRequested(key,teacher,new[]{cats[0]}),"another account cannot alter application categories");
   Denied(()=>service.ApproveApplication(key,new[]{cats[0]},teacher),"non-admin cannot approve a teacher");
   Denied(()=>service.ApproveApplication(key,new[]{cats[1]},admin),"admin cannot approve unrequested initial category");
   Check(db.Database.SqlQuery<string>("SELECT vaiTro FROM dbo.NguoiDung WHERE maNguoiDung=@p0",learner).Single()=="HocVien","failed approval does not grant role");
   service.ApproveApplication(key,new[]{cats[0]},admin);
   Check(service.Approved(learner).SequenceEqual(new[]{cats[0]}),"initial approval grants exactly selected category");
   Check(db.Database.SqlQuery<string>("SELECT vaiTro FROM dbo.NguoiDung WHERE maNguoiDung=@p0",learner).Single()=="GiaoVien","initial role and category granted together");
   Denied(()=>service.ApproveApplication(key,new[]{cats[0]},admin),"initial approval replay blocked");
   Check(!service.IsLegacy(learner),"newly approved teacher always follows selected specialties");
   db.Database.ExecuteSqlCommand("INSERT dbo.TeacherSpecialty(TeacherId,CategoryId,ApprovedBy) VALUES(@p0,@p1,@p2)",teacher,cats[0],admin);
   service.Require(teacher,cats[0]);Check(true,"approved category permits course creation");
   Denied(()=>service.Require(teacher,cats[1]),"unapproved category denied after assignment");
   using(var controller=new CourseController()){
    controller.ControllerContext=new ControllerContext{Controller=controller,HttpContext=new SpecialtyContext(teacher)};
    var get=(ViewResult)controller.Create();var model=(CourseCreateOrEditViewModel)get.Model;
    Check(model.Categories.Count()==1&&model.Categories.Single().Value==cats[0],"create dropdown only lists approved categories");
    var forged=new CourseCreateOrEditViewModel{TenKhoaHoc="Forged",MaDanhMuc=cats[1]};
    Check(controller.Create(forged,"draft") is ViewResult&&controller.ModelState["MaDanhMuc"].Errors.Any(),"forged create POST blocked even when saving draft");
   }
   db.KhoaHoc.Add(new KhoaHoc{maKhoaHoc=course,tenKhoaHoc="Preserved legacy course",maGiaoVien=teacher,maDanhMuc=cats[1],giaBan=0,trangThai="Bản nháp",capDo="Cơ bản"});db.SaveChanges();
   Check(service.SuggestedFromCourses(teacher).SequenceEqual(new[]{cats[1]}),"proposal form pre-selects categories of the teacher's existing courses");
   using(var controller=new CourseController()){
    controller.ControllerContext=new ControllerContext{Controller=controller,HttpContext=new SpecialtyContext(teacher)};
    controller.SubmitForApproval(course);
    var msg=(controller.TempData["ErrorMessage"]??"").ToString();
    Check(!msg.Contains("lĩnh vực được duyệt"),"existing course in its original category is not blocked at submit");
   }
   using(var controller=new ApprovalController()){
    controller.ControllerContext=new ControllerContext{Controller=controller,HttpContext=new SpecialtyContext(admin)};
    var result=(JsonResult)controller.Approve(course);
    var msg=(string)result.Data.GetType().GetProperty("message").GetValue(result.Data)??"";
    Check(!msg.Contains("lĩnh vực được duyệt"),"admin publication does not block an existing course by specialty");
   }
   var live=db.KhoaHoc.Single(k=>k.maKhoaHoc==course);
   Denied(()=>new CourseRevisionService(db,_=>"").Save(live,new CourseCreateOrEditViewModel{MaDanhMuc=cats[2]},new Dictionary<string,string[]>(),false),"revision draft cannot move course to an unapproved category");
   Check(db.KhoaHoc.Single(k=>k.maKhoaHoc==course).tenKhoaHoc=="Preserved legacy course","existing courses remain intact");
   Denied(()=>service.Submit(teacher,new[]{cats[1]},"~/test.pdf",""),"supplement request needs description");
   service.Submit(teacher,new[]{cats[1]},"~/test.pdf","Fixture evidence");var request=service.Requests(teacher).First();
   Check(service.Requests(learner).Count==0,"requests isolated by teacher");
   Denied(()=>service.Submit(teacher,new[]{cats[1]},"~/test.pdf","Second"),"duplicate pending request blocked");
   Check(new AdminPendingService(db).Dem().ChuyenMon>=1,"admin sidebar badge counts pending specialty request");
   using(var admin1=new AdminSpecialtyController()){
    var list=(List<TeacherSpecialtyRequest>)((ViewResult)admin1.Index()).Model;
    Check(list.Any(r=>r.Id==request.Id)&&list.All(r=>r.Status=="Pending"),"admin request tab defaults to pending requests");
    var bad=(List<TeacherSpecialtyRequest>)((ViewResult)admin1.Index("<script>")).Model;
    Check(bad.All(r=>r.Status=="Pending"),"admin request tab ignores unknown status filter");
   }
   Denied(()=>service.Review(request.Id,learner,new[]{cats[1]},admin,true,null),"review cannot swap request owner");
   Denied(()=>service.Review(request.Id,teacher,new[]{cats[0]},admin,true,null),"supplement approval cannot add unrequested category");
   Denied(()=>service.Review(request.Id,teacher,null,admin,false,""),"rejection requires reason");
   service.Review(request.Id,teacher,null,admin,false,"More evidence needed");
   Check(service.Approved(teacher).SequenceEqual(new[]{cats[0]}),"rejected supplement keeps existing permissions");
   Check(service.IsLegacy(teacher),"rejected request keeps legacy marker");
   service.Submit(teacher,new[]{cats[1]},"~/test.pdf","Resubmitted evidence");request=service.Requests(teacher).First();
   service.Review(request.Id,teacher,new[]{cats[1]},admin,true,null);
   Check(service.Approved(teacher).Count==2,"approved supplement adds category without dropping old one");
   Check(!service.IsLegacy(teacher),"first approved request turns off legacy marker");
   Check(service.Requests(teacher).First().GrantedCategoryIds.SequenceEqual(new[]{cats[1]}),"review records exact approved subset");
   using(var admin2=new AdminSpecialtyController()){
    Check(!((List<TeacherSpecialtyRequest>)((ViewResult)admin2.Index()).Model).Any(r=>r.TeacherId==teacher),"approved request leaves pending tab");
    var approvedTab=(List<TeacherSpecialtyRequest>)((ViewResult)admin2.Index("Approved")).Model;
    Check(approvedTab.Any(r=>r.Id==request.Id)&&approvedTab.All(r=>r.Status=="Approved"),"approved tab lists approved request");
    Check(((List<TeacherSpecialtyRequest>)((ViewResult)admin2.Index("Rejected")).Model).Any(r=>r.TeacherId==teacher&&r.Status=="Rejected"),"rejected tab lists rejected request");
   }
   Denied(()=>service.Review(request.Id,teacher,new[]{cats[1]},admin,true,null),"supplement approval replay blocked");
   Console.WriteLine("All "+count+" specialty checks passed.");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
  finally{
   db.Database.ExecuteSqlCommand("UPDATE dbo.KhoaHoc SET trangThai=N'Bản nháp' WHERE maKhoaHoc=@p0; DELETE dbo.KhoaHoc WHERE maKhoaHoc=@p0",course);
   db.Database.ExecuteSqlCommand("DELETE dbo.TeacherApplicationSpecialty WHERE ApplicationId=@p0",key);
   db.Database.ExecuteSqlCommand("DELETE dbo.DangKyGiaoVien WHERE maDon=@p0",key);
   foreach(int id in new[]{learner,teacher,admin}.Where(i=>i>0)){
    db.Database.ExecuteSqlCommand("DELETE dbo.TeacherSpecialtyRequest WHERE TeacherId=@p0",id);
    db.Database.ExecuteSqlCommand("DELETE dbo.TeacherSpecialty WHERE TeacherId=@p0",id);
   }
   foreach(int id in new[]{learner,teacher,admin}.Where(i=>i>0)) {db.Database.ExecuteSqlCommand("DELETE dbo.TeacherSpecialtyPolicy WHERE TeacherId=@p0",id);db.Database.ExecuteSqlCommand("DELETE dbo.NguoiDung WHERE maNguoiDung=@p0",id); }
  }
 }
}
