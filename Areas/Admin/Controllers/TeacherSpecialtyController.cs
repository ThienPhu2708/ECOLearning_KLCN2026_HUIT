using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
namespace ECOLearning.Areas.Admin.Controllers {
 public class TeacherSpecialtyController : AdminBaseController {
  readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();
  public ActionResult Index() {
   ViewBag.BreadcrumbCurrent="Duyệt chuyên môn giảng dạy";
   ViewBag.TeacherNames=db.NguoiDung.Where(u=>u.vaiTro=="GiaoVien"||u.vaiTro=="Giáo viên").ToDictionary(u=>u.maNguoiDung,u=>u.hoTen);
   ViewBag.Categories=db.DanhMuc.ToDictionary(c=>c.maDanhMuc,c=>c.tenDanhMuc);
   return View(new TeacherSpecialtyService(db).Requests());
  }
  public ActionResult Manage(int id) {
   var user=db.NguoiDung.Find(id);if(user==null||(user.vaiTro!="GiaoVien"&&user.vaiTro!="Giáo viên")) return HttpNotFound();
   var service=new TeacherSpecialtyService(db);
   ViewBag.Installed=service.Installed();ViewBag.Approved=service.Approved(id);ViewBag.Legacy=service.IsLegacy(id);
   ViewBag.Categories=db.DanhMuc.OrderBy(c=>c.tenDanhMuc).ToList();
   ViewBag.BreadcrumbParent="Quản lý giáo viên";
   ViewBag.BreadcrumbParentUrl=Url.Action("Index","Teacher");
   ViewBag.BreadcrumbCurrent="Chuyên môn giảng dạy";
   ViewBag.Teacher=user;ViewBag.Profile=TeacherApplicationService.GetByUserId(id);
   return View(service.Requests(id));
  }
  [HttpPost,ValidateAntiForgeryToken]
  public ActionResult Review(int id,int teacherId,string[] categoryIds,bool approve,string reason) {
   try {new TeacherSpecialtyService(db).Review(id,teacherId,categoryIds,Convert.ToInt32(Session["UserId"]),approve,reason);TempData["SpecialtyMessage"]=approve?"Đã duyệt chuyên môn.":"Đã từ chối yêu cầu.";}
   catch(InvalidOperationException ex) {TempData["SpecialtyMessage"]=ex.Message;}
   return RedirectToAction("Manage",new{id=teacherId});
  }
  protected override void Dispose(bool disposing) {if(disposing) db.Dispose();base.Dispose(disposing);}
 }
}
