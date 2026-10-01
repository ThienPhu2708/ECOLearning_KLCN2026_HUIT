using System.Linq;
using System.Web.Mvc;
using ECOLearning.Services;

namespace ECOLearning.Areas.Instructor.Controllers
{
    // Giáo viên xem và xử lý khóa học bị khóa do báo cáo vi phạm
    public class ViolationController : InstructorBaseController
    {
        // GET: Instructor/Violation/Index/KH...
        public ActionResult Index(string id)
        {
            var service = new ViolationReportService(db);
            if (!service.IsInstalled())
            {
                TempData["ErrorMessage"] = ViolationReportService.NotInstalledMessage;
                return RedirectToAction("Index", "Course");
            }

            int teacherId = CurrentTeacherId;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == id && k.maGiaoVien == teacherId);
            if (course == null) return HttpNotFound();

            var actions = service.GetActions(id);
            var lastLock = actions.FirstOrDefault(a => a.hanhDong == ViolationReportService.ActionLock);
            var lastPermanent = actions.FirstOrDefault(a => a.hanhDong == ViolationReportService.ActionPermanentLock);
            if (lastLock == null && lastPermanent == null)
            {
                TempData["ErrorMessage"] = "Khóa học này chưa từng bị xử lý vi phạm.";
                return RedirectToAction("Index", "Course");
            }

            ViewBag.Course = course;
            ViewBag.LastLock = lastLock;
            ViewBag.LastPermanent = lastPermanent;
            ViewBag.HasDiscussion = service.HasDiscussion();
            ViewBag.Messages = service.GetMessages(lastLock != null ? lastLock.maXuLy : null);
            // Báo cáo liên quan (không hiển thị danh tính người báo cáo ở view)
            ViewBag.Reports = service.GetReports(id)
                .Where(r => r.trangThai != ViolationReportService.ReportDismissed)
                .ToList();
            ViewBag.BreadcrumbCourseName = course.tenKhoaHoc;
            ViewBag.BreadcrumbCourseId = course.maKhoaHoc;
            ViewBag.ShowCourseBreadcrumb = true;
            ViewBag.BreadcrumbCurrent = "Xử lý vi phạm";
            return View();
        }

        // POST: Instructor/Violation/Respond (Đã chỉnh sửa / Khiếu nại)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Respond(string id, string loai, string noiDung)
        {
            var service = new ViolationReportService(db);
            if (!service.IsInstalled())
            {
                TempData["ErrorMessage"] = ViolationReportService.NotInstalledMessage;
                return RedirectToAction("Index", "Course");
            }

            string error = service.SubmitTeacherResponse(id, CurrentTeacherId, loai, noiDung);
            if (error != null)
            {
                TempData["ErrorMessage"] = error;
                TempData["DraftLoai"] = loai;
                TempData["DraftNoiDung"] = noiDung;
            }
            else
            {
                TempData["SuccessMessage"] = loai == ViolationReportService.MessageAppeal
                    ? "Đã gửi khiếu nại. Quản trị viên sẽ xem xét và phản hồi."
                    : "Đã gửi yêu cầu xem xét lại. Quản trị viên sẽ kiểm tra nội dung đã chỉnh sửa.";
            }
            return RedirectToAction("Index", new { id = id });
        }
    }
}
