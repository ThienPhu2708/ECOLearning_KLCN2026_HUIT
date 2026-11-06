using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Instructor.Controllers;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class MessageController : InstructorBaseController
    {
        [HttpGet]
        public JsonResult Conversations()
        {
            var service = new ChatService(db);
            var conversations = service.LayDanhSachHoiThoaiGiaoVien(CurrentTeacherId);
            return Json(new {
                count = service.DemTongTinChuaDoc(CurrentTeacherId, true),
                items = conversations.Where(c => c.TinCuoiLuc.HasValue).Take(20).Select(c => new {
                    name = c.TenHocVien, course = c.TenKhoaHoc,
                    preview = (c.MaNguoiGuiCuoi == CurrentTeacherId ? "Bạn: " : "") + c.TinCuoiTomTat, unread = c.SoTinChuaDoc,
                    time = c.TinCuoiLuc.HasValue ? c.TinCuoiLuc.Value.ToString(c.TinCuoiLuc.Value.Date == DateTime.Today ? "HH:mm" : "dd/MM") : "",
                    url = Url.Action("Index", "Message", new { area = "Instructor", id = c.MaHoiThoai })
                })
            }, JsonRequestBehavior.AllowGet);
        }
        // GET: /Instructor/Message
        public ActionResult Index(int? id, string courseId = "", string status = "all", bool list = false, string lessonId = null)
        {
            int teacherId = CurrentTeacherId;
            var courses = db.KhoaHoc.Where(k => k.maGiaoVien == teacherId)
                .OrderBy(k => k.tenKhoaHoc).ToList();
            // Các liên kết cũ có mã hội thoại 1–1: chuyển đến khóa tương ứng, không xóa dữ liệu.
            if (string.IsNullOrWhiteSpace(courseId) && id.HasValue && id.Value > 0)
            {
                var oldChat = new ChatService(db).LayChiTietHoiThoai(id.Value, teacherId);
                if (oldChat != null && oldChat.MaGiaoVien == teacherId) courseId = oldChat.MaKhoaHoc;
            }
            var selected = courses.FirstOrDefault(c => c.maKhoaHoc == courseId);
            if (selected == null && !list) selected = courses.FirstOrDefault();
            var lessons = selected == null ? new System.Collections.Generic.List<ECOLearning.Models.BaiHoc>()
                : new LearningService(db).LayBaiHoc(selected.maKhoaHoc);
            var lesson = lessons.FirstOrDefault(b => b.maBaiHoc == lessonId) ?? lessons.FirstOrDefault();
            var counts = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (lesson != null && new LessonDiscussionService(db).Installed())
            {
                var lessonIds = lessons.Select(b => b.maBaiHoc).ToList();
                // Chỉ lấy bài của khóa do giáo viên đang quản lý.
                var rows = db.Database.SqlQuery<DiscussionCount>(@"SELECT t.maBaiHoc AS LessonId, COUNT(*) AS Total
                    FROM dbo.ThaoLuanBaiHoc t
                    JOIN dbo.BaiHoc b ON b.maBaiHoc=t.maBaiHoc
                    LEFT JOIN dbo.ChuongHoc c ON c.maChuong=b.maChuong
                    WHERE t.trangThai=N'HienThi' AND (c.maKhoaHoc=@courseId
                        OR (b.maChuong IS NULL AND LEFT(b.maBaiHoc, LEN(@courseId)+1)=@courseId+'_'))
                    GROUP BY t.maBaiHoc", new System.Data.SqlClient.SqlParameter("@courseId", selected.maKhoaHoc)).ToList();
                counts = rows.Where(r => lessonIds.Contains(r.LessonId)).ToDictionary(r => r.LessonId, r => r.Total, StringComparer.OrdinalIgnoreCase);
            }
            ViewBag.Courses = courses;
            ViewBag.SelectedCourse = selected;
            ViewBag.Lessons = lessons;
            ViewBag.SelectedLesson = lesson;
            ViewBag.DiscussionCounts = counts;
            ViewBag.BreadcrumbCurrent = "Thảo luận bài học";
            return View();
        }

        private class DiscussionCount
        {
            public string LessonId { get; set; }
            public int Total { get; set; }
        }

        // GET: /Instructor/Message/StudentProfile?courseId=...&studentId=...
        [HttpGet]
        public JsonResult StudentProfile(string courseId, int studentId)
        {
            if (!db.KhoaHoc.Any(k => k.maKhoaHoc == courseId && k.maGiaoVien == CurrentTeacherId))
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            var service = new ChatService(db);
            var info = service.LayThongTinHocVien(courseId, studentId);
            return Json(new { success = info != null, data = info }, JsonRequestBehavior.AllowGet);
        }
    }
}

