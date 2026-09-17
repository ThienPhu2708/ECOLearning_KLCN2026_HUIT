using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;

namespace ECOLearning.Controllers
{
    public class LearningController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Learning/MyCourses (UC8: Khóa học của tôi)
        public ActionResult MyCourses()
        {
            var currentUser = Session["User"] as NguoiDung;
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var enrolledCourses = db.DangKyKhoaHoc
                .Where(d => d.maHocVien == currentUser.maNguoiDung)
                .Include(d => d.KhoaHoc)
                .Select(d => d.KhoaHoc)
                .ToList();

            return View(enrolledCourses);
        }

        // GET: Learning/Study (UC5: Không gian học tập Video Player & Cây đề cương)
        public ActionResult Study(string courseId, string lessonId = null)
        {
            if (string.IsNullOrEmpty(courseId))
            {
                var defaultCourse = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == "KH_TEST01") ?? db.KhoaHoc.FirstOrDefault();
                if (defaultCourse != null)
                {
                    courseId = defaultCourse.maKhoaHoc;
                }
                else
                {
                    return RedirectToAction("Index", "Course");
                }
            }

            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == courseId);
            if (course == null)
            {
                return HttpNotFound();
            }

            var chapters = db.ChuongHoc
                .Where(c => c.maKhoaHoc == courseId)
                .OrderBy(c => c.thuTu)
                .Include(c => c.BaiHoc)
                .ToList();

            var allLessons = chapters
                .SelectMany(c => c.BaiHoc != null ? c.BaiHoc.OrderBy(b => b.thuTu).AsEnumerable() : Enumerable.Empty<BaiHoc>())
                .ToList();

            BaiHoc activeLesson = null;
            if (!string.IsNullOrEmpty(lessonId))
            {
                activeLesson = allLessons.FirstOrDefault(b => b.maBaiHoc == lessonId);
            }

            if (activeLesson == null)
            {
                activeLesson = allLessons.FirstOrDefault();
            }

            // Tiến độ học tập của người dùng
            var currentUser = Session["User"] as NguoiDung;
            List<string> completedLessonIds = new List<string>();

            if (currentUser != null)
            {
                completedLessonIds = db.TienDoBaiHoc
                    .Where(t => t.maHocVien == currentUser.maNguoiDung && t.daHoanThanh == true)
                    .Select(t => t.maBaiHoc)
                    .ToList();
            }

            ViewBag.Course = course;
            ViewBag.Chapters = chapters;
            ViewBag.AllLessons = allLessons;
            ViewBag.ActiveLesson = activeLesson;
            ViewBag.CompletedLessonIds = completedLessonIds;

            return View();
        }

        // POST: Learning/UpdateProgress (Lưu tiến độ & đánh dấu hoàn thành nếu >= 80%)
        [HttpPost]
        public ActionResult UpdateProgress(string courseId, string lessonId, float durationWatched, float totalDuration)
        {
            var currentUser = Session["User"] as NguoiDung;
            int studentId = currentUser != null ? currentUser.maNguoiDung : 1; // Fallback demo user

            if (string.IsNullOrEmpty(lessonId))
            {
                return Json(new { success = false, message = "Thiếu thông tin bài học" });
            }

            bool isDone = (totalDuration > 0 && (durationWatched / totalDuration) >= 0.80f) || durationWatched >= (totalDuration - 5);

            var existingProgress = db.TienDoBaiHoc
                .FirstOrDefault(t => t.maHocVien == studentId && t.maBaiHoc == lessonId);

            if (existingProgress == null)
            {
                existingProgress = new TienDoBaiHoc
                {
                    maHocVien = studentId,
                    maBaiHoc = lessonId,
                    thoiGianXemDungLai = (int)durationWatched,
                    daHoanThanh = isDone,
                    lanTruyCapGanNhat = DateTime.Now,
                    ngayHoanThanh = isDone ? (DateTime?)DateTime.Now : null
                };
                db.TienDoBaiHoc.Add(existingProgress);
            }
            else
            {
                existingProgress.thoiGianXemDungLai = (int)durationWatched;
                existingProgress.lanTruyCapGanNhat = DateTime.Now;
                if (isDone && (existingProgress.daHoanThanh != true))
                {
                    existingProgress.daHoanThanh = true;
                    existingProgress.ngayHoanThanh = DateTime.Now;
                }
            }

            db.SaveChanges();

            return Json(new { success = true, completed = existingProgress.daHoanThanh });
        }

        // GET: Learning/Certificate (UC8: Xem & Tải chứng chỉ PDF)
        public ActionResult Certificate(string courseId)
        {
            ViewBag.CourseId = courseId;
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && db != null)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
