using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ECOLearning.Services;
namespace ECOLearning.Areas.Instructor.Controllers {
 public class SpecialtyController : InstructorBaseController {
  public ActionResult Index() {
   var service=new TeacherSpecialtyService(db);
   ViewBag.Legacy=service.IsLegacy(CurrentTeacherId);
   ViewBag.Installed=service.Installed();ViewBag.Approved=service.Approved(CurrentTeacherId);
   ViewBag.Categories=db.DanhMuc.OrderBy(c=>c.tenDanhMuc).ToList();
   ViewBag.BreadcrumbCurrent="Chuyên môn giảng dạy";
   return View(service.Requests(CurrentTeacherId));
  }
  [HttpPost,ValidateAntiForgeryToken]
  public ActionResult Submit(string[] categoryIds,string description,HttpPostedFileBase evidence) {
   string saved=null;
   try {
    if(evidence==null||evidence.ContentLength==0||evidence.ContentLength>5*1024*1024) throw new InvalidOperationException("Vui lòng chọn minh chứng PDF, JPG hoặc PNG, tối đa 5 MB.");
    var ext=Path.GetExtension(evidence.FileName).ToLowerInvariant();
    if(!new[]{".pdf",".jpg",".jpeg",".png"}.Contains(ext)) throw new InvalidOperationException("Định dạng minh chứng không hợp lệ.");
    var service=new TeacherSpecialtyService(db);service.ValidateSelection(categoryIds);
    var folder=Server.MapPath("~/assets/uploads/certificates/");Directory.CreateDirectory(folder);
    var file="specialty_"+CurrentTeacherId+"_"+Guid.NewGuid().ToString("N")+ext;
    saved=Path.Combine(folder,file);evidence.SaveAs(saved);
    service.Submit(CurrentTeacherId,categoryIds,Url.Content("~/assets/uploads/certificates/"+file),description);
    TempData["SpecialtyMessage"]="Đã gửi yêu cầu bổ sung chuyên môn. Lĩnh vực hiện tại được giữ nguyên trong khi chờ duyệt.";
   } catch(Exception ex) {
    if(saved!=null&&System.IO.File.Exists(saved)) System.IO.File.Delete(saved);
    TempData["SpecialtyMessage"]=ex is InvalidOperationException?ex.Message:"Chưa gửi được yêu cầu. Vui lòng thử lại.";
   }
   return RedirectToAction("Index");
  }
 }
}
