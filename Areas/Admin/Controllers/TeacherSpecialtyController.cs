using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
namespace ECOLearning.Areas.Admin.Controllers {
 public class TeacherSpecialtyController : AdminBaseController {
  readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();
  // Danh sách yêu cầu bổ sung chuyên môn của giáo viên (tách khỏi "Duyệt hồ sơ giáo viên"); mặc định hiện yêu cầu chờ duyệt
  public ActionResult Index(string status="Pending") {
   if(status!="Pending"&&status!="Approved"&&status!="Rejected"&&status!="All") status="Pending";
   var service=new TeacherSpecialtyService(db);
   var all=service.AllRequests();
   ViewBag.Installed=service.Installed();
   ViewBag.Status=status;
   ViewBag.PendingCount=all.Count(r=>r.Status=="Pending");
   ViewBag.ApprovedCount=all.Count(r=>r.Status=="Approved");
   ViewBag.RejectedCount=all.Count(r=>r.Status=="Rejected");
   var ids=all.Select(r=>r.TeacherId).Distinct().ToList();
   ViewBag.Teachers=db.NguoiDung.Where(u=>ids.Contains(u.maNguoiDung)).ToDictionary(u=>u.maNguoiDung,u=>u);
   ViewBag.Categories=db.DanhMuc.ToDictionary(c=>c.maDanhMuc,c=>c.tenDanhMuc);
   ViewBag.BreadcrumbCurrent="Yêu cầu bổ sung chuyên môn";
   // Chờ duyệt: cũ nhất trước để xử lý theo thứ tự gửi; các trạng thái khác: mới nhất trước
   var list=status=="All"?all:all.Where(r=>r.Status==status).ToList();
   if(status=="Pending") list=list.OrderBy(r=>r.CreatedAt).ToList();
   return View(list);
  }
  public ActionResult Manage(int id) {
   var user=db.NguoiDung.Find(id);if(user==null||(user.vaiTro!="GiaoVien"&&user.vaiTro!="Giáo viên")) return HttpNotFound();
   var service=new TeacherSpecialtyService(db);
   ViewBag.Installed=service.Installed();ViewBag.Approved=service.Approved(id);ViewBag.Legacy=service.IsLegacy(id);
   ViewBag.Categories=db.DanhMuc.OrderBy(c=>c.tenDanhMuc).ToList();
   ViewBag.BreadcrumbParent="Yêu cầu bổ sung chuyên môn";
   ViewBag.BreadcrumbParentUrl=Url.Action("Index");
   ViewBag.BreadcrumbCurrent="Chuyên môn giảng dạy";
   ViewBag.Teacher=user;ViewBag.Profile=TeacherApplicationService.GetByUserId(id);
   return View(service.Requests(id));
  }
  [HttpPost,ValidateAntiForgeryToken]
  public ActionResult Review(int id,int teacherId,string[] categoryIds,bool approve,string reason) {
   // Kết quả hiện bằng thông báo nổi tự tắt (showToast) ở trang Manage
   try {new TeacherSpecialtyService(db).Review(id,teacherId,categoryIds,Convert.ToInt32(Session["UserId"]),approve,reason);TempData["SpecialtyMessage"]=approve?"Đã duyệt chuyên môn.":"Đã từ chối yêu cầu.";TempData["SpecialtyMessageType"]=approve?"success":"info";}
   catch(InvalidOperationException ex) {TempData["SpecialtyMessage"]=ex.Message;TempData["SpecialtyMessageType"]="error";}
   return RedirectToAction("Manage",new{id=teacherId});
  }
  protected override void Dispose(bool disposing) {if(disposing) db.Dispose();base.Dispose(disposing);}
 }
}
