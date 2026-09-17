using System;
using System.Web.Mvc;

namespace ECOLearning.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CourseApprovalController : Controller
    {
        // GET: Admin/CourseApproval (UC15: Kiểm duyệt khóa học)
        public ActionResult Index()
        {
            return View();
        }

        // GET: Admin/CourseApproval/Review/5
        // Xem chi tiết nội dung, video bài giảng, tài liệu đính kèm
        public ActionResult Review(string id)
        {
            return View();
        }

        // POST: Admin/CourseApproval/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(string id)
        {
            // Nghiệp vụ: Cập nhật trạng thái thành 'Đã xuất bản'
            return RedirectToAction("Index");
        }

        // POST: Admin/CourseApproval/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reject(string id, string reason)
        {
            // Nghiệp vụ: Gửi lý do từ chối về giảng viên
            return RedirectToAction("Index");
        }
    }
}
