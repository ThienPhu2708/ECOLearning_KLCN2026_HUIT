using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ECOLearning.Services;
namespace ECOLearning.Areas.Instructor.Controllers {
 public class SpecialtyController : InstructorBaseController {
  // Giáo viên đã đồng ý Điều khoản giáo viên và tắt popup "chưa có chuyên môn" ở Dashboard: không tự mở lại trong phiên đăng nhập này
  public const string NoticeDismissedKey="SpecialtyNoticeDismissed";
  [HttpPost,ValidateAntiForgeryToken]
  public ActionResult DismissNotice(bool agreed=false) {
   if(!agreed) return new HttpStatusCodeResult(400,"Cần đồng ý Điều khoản giáo viên.");
   Session[NoticeDismissedKey]=true;return new HttpStatusCodeResult(204);
  }
  // Thông tin giáo viên đã kê khai gần nhất (đề xuất chuyên môn trước đó, nếu chưa có thì hồ sơ đăng ký giáo viên):
  // điền sẵn vào form đề xuất để giáo viên không phải nhập lại; minh chứng chỉ dùng lại khi tệp còn trên máy chủ.
  public class SavedProfile {public string Description {get;set;} public string EvidenceUrl {get;set;} public string EvidenceName {get;set;}}
  SavedProfile Saved(TeacherSpecialtyService service) {
   var p=new SavedProfile();
   var last=service.Requests(CurrentTeacherId).FirstOrDefault();
   var app=TeacherApplicationService.GetByUserId(CurrentTeacherId);
   if(last!=null) {p.Description=last.Description;p.EvidenceUrl=last.EvidenceUrl;p.EvidenceName=Path.GetFileName(last.EvidenceUrl??"");}
   else if(app!=null) {p.Description=app.BioExperience;p.EvidenceUrl=app.CertificateFilePath;p.EvidenceName=string.IsNullOrWhiteSpace(app.CertificateFileName)?Path.GetFileName(app.CertificateFilePath??""):app.CertificateFileName;}
   if(string.IsNullOrWhiteSpace(p.Description)) p.Description=null;
   if(!string.IsNullOrWhiteSpace(p.EvidenceUrl)) {
    string disk=null;
    try {disk=Server.MapPath(p.EvidenceUrl.StartsWith("~")||p.EvidenceUrl.StartsWith("/")?p.EvidenceUrl:"~/"+p.EvidenceUrl);} catch(HttpException) {}
    if(disk==null||!System.IO.File.Exists(disk)) p.EvidenceUrl=null;
   } else p.EvidenceUrl=null;
   if(p.EvidenceUrl==null) p.EvidenceName=null;
   return p;
  }
  public ActionResult Index() {
   var service=new TeacherSpecialtyService(db);
   ViewBag.Saved=Saved(service);
   ViewBag.Legacy=service.IsLegacy(CurrentTeacherId);
   ViewBag.Installed=service.Installed();ViewBag.Approved=service.Approved(CurrentTeacherId);
   ViewBag.Suggested=service.SuggestedFromCourses(CurrentTeacherId);
   ViewBag.Categories=db.DanhMuc.OrderBy(c=>c.tenDanhMuc).ToList();
   ViewBag.BreadcrumbCurrent="Chuyên môn giảng dạy";
   return View(service.Requests(CurrentTeacherId));
  }
  [HttpPost,ValidateAntiForgeryToken]
  public ActionResult Submit(string[] categoryIds,string description,HttpPostedFileBase evidence) {
   string saved=null;
   try {
    var service=new TeacherSpecialtyService(db);
    bool hasNew=evidence!=null&&evidence.ContentLength>0;
    // Không tải tệp mới: dùng lại minh chứng đã kê khai (lấy từ CSDL phía máy chủ, không nhận đường dẫn từ form)
    string evidenceUrl=hasNew?null:Saved(service).EvidenceUrl;
    if(!hasNew&&evidenceUrl==null) throw new InvalidOperationException("Vui lòng chọn minh chứng PDF, JPG hoặc PNG, tối đa 5 MB.");
    string ext=null;
    if(hasNew) {
     if(evidence.ContentLength>5*1024*1024) throw new InvalidOperationException("Vui lòng chọn minh chứng PDF, JPG hoặc PNG, tối đa 5 MB.");
     ext=Path.GetExtension(evidence.FileName).ToLowerInvariant();
     if(!new[]{".pdf",".jpg",".jpeg",".png"}.Contains(ext)) throw new InvalidOperationException("Định dạng minh chứng không hợp lệ.");
    }
    service.ValidateSelection(categoryIds);
    if(hasNew) {
     var folder=Server.MapPath("~/assets/uploads/certificates/");Directory.CreateDirectory(folder);
     var file="specialty_"+CurrentTeacherId+"_"+Guid.NewGuid().ToString("N")+ext;
     saved=Path.Combine(folder,file);evidence.SaveAs(saved);
     evidenceUrl=Url.Content("~/assets/uploads/certificates/"+file);
    }
    service.Submit(CurrentTeacherId,categoryIds,evidenceUrl,description);
    Session[NoticeDismissedKey]=true;
    TempData["SpecialtyMessage"]="Đã gửi đề xuất chuyên môn. Các khóa học và lĩnh vực hiện tại được giữ nguyên trong khi chờ duyệt.";
   } catch(Exception ex) {
    if(saved!=null&&System.IO.File.Exists(saved)) System.IO.File.Delete(saved);
    TempData["SpecialtyMessage"]=ex is InvalidOperationException?ex.Message:"Chưa gửi được yêu cầu. Vui lòng thử lại.";
   }
   return RedirectToAction("Index");
  }
 }
}
