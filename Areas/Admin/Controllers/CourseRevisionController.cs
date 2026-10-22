using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
namespace ECOLearning.Areas.Admin.Controllers {
 public class CourseRevisionController : AdminBaseController {
  private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();
  public ActionResult Index() { return RedirectToAction("Index", "CourseApproval", new { statusFilter = "Revisions" }); }
  public ActionResult Details(string id) {
   var row=new CourseRevisionService(db,Server.MapPath).Get(id);
   if(row==null) return HttpNotFound();
   ViewBag.LiveCourse=db.KhoaHoc.Single(k=>k.maKhoaHoc==id);
   var input=row.Read().Model;
   var live=(KhoaHoc)ViewBag.LiveCourse;
   ViewBag.LiveCategory=db.DanhMuc.Where(c=>c.maDanhMuc==live.maDanhMuc).Select(c=>c.tenDanhMuc).FirstOrDefault();
   ViewBag.DraftCategory=db.DanhMuc.Where(c=>c.maDanhMuc==input.MaDanhMuc).Select(c=>c.tenDanhMuc).FirstOrDefault();
   ViewBag.LiveLessons=db.BaiHoc.Where(l=>(l.ChuongHoc!=null&&l.ChuongHoc.maKhoaHoc==id)||(l.maChuong==null&&l.maBaiHoc.StartsWith(id+"_"))).OrderBy(l=>l.thuTu).ToList();
   return View(row);
  }
  [HttpPost,ValidateAntiForgeryToken]
  public ActionResult Review(string id,string version,string decision,string reason) {
   if(decision!="approve"&&decision!="reject") return new HttpStatusCodeResult(400);
   try {
    if(reason!=null&&reason.Length>2000) throw new InvalidOperationException("Lý do tối đa 2000 ký tự.");
    new CourseRevisionService(db,Server.MapPath).Review(id,version,Convert.ToInt32(Session["UserId"]),decision=="approve",reason);
    TempData["SuccessMessage"] = decision=="approve"?"Đã áp dụng bản sửa được duyệt.":"Đã từ chối bản sửa. Khóa đang hoạt động được giữ nguyên.";
    return RedirectToAction("Index", "CourseApproval", new { statusFilter = "Revisions" });
   } catch(InvalidOperationException error) {TempData["ErrorMessage"]=error.Message;return RedirectToAction("Details",new{id});}
  }
  protected override void Dispose(bool disposing) {if(disposing) db.Dispose();base.Dispose(disposing);}
 }
}
