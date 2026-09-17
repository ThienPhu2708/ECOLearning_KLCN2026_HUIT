using System;
using System.Web;
using System.Web.Mvc;

namespace ECOLearning.Areas.Instructor.Controllers
{
    [Authorize(Roles = "GiaoVien")]
    public class CurriculumController : Controller
    {
        // GET: Instructor/Curriculum/Index?courseId=xxx (UC10: Quản lý đề cương bài giảng)
        public ActionResult Index(string courseId)
        {
            ViewBag.CourseId = courseId;
            return View();
        }

        // POST: Thêm chương học mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateChapter(string courseId, string tenChuong, int thuTu)
        {
            return RedirectToAction("Index", new { courseId });
        }

        // POST: Thêm bài học mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateLesson(string chapterId, string tenBaiHoc, int thoiLuong, bool choXemThu)
        {
            return Json(new { success = true });
        }

        // POST: Tải lên video bài giảng
        [HttpPost]
        public ActionResult UploadVideo(string lessonId, HttpPostedFileBase videoFile)
        {
            // Validate: MP4, kích thước tối đa 2GB
            return Json(new { success = true, videoUrl = "/uploads/videos/sample.mp4" });
        }

        // POST: Tải lên tài nguyên đính kèm (PDF, ZIP, DOC)
        [HttpPost]
        public ActionResult UploadResource(string lessonId, HttpPostedFileBase resourceFile)
        {
            return Json(new { success = true });
        }
    }
}
