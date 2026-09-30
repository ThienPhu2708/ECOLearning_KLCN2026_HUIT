using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ECOLearning.Areas.Instructor.Models;
using ECOLearning.Models;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class CurriculumController : InstructorBaseController
    {
        // GET: Instructor/Curriculum (UC10: Đề cương & Tài nguyên khóa học)
        public ActionResult Index(string courseId = "")
        {
            int teacherId = CurrentTeacherId;
            var teacherCourses = db.KhoaHoc
                .Where(k => k.maGiaoVien == teacherId)
                .ToList();

            if (!teacherCourses.Any())
            {
                ViewBag.NoCourses = true;
                ViewBag.BreadcrumbCurrent = "Đề cương & Tài nguyên";
                return View(new CurriculumViewModel());
            }

            var currentCourse = !string.IsNullOrEmpty(courseId)
                ? teacherCourses.FirstOrDefault(k => k.maKhoaHoc == courseId)
                : teacherCourses.FirstOrDefault();

            if (currentCourse == null)
            {
                currentCourse = teacherCourses.First();
            }

            var model = new CurriculumViewModel
            {
                MaKhoaHoc = currentCourse.maKhoaHoc,
                TenKhoaHoc = currentCourse.tenKhoaHoc
            };

            var chapters = db.ChuongHoc
                .Where(c => c.maKhoaHoc == currentCourse.maKhoaHoc)
                .OrderBy(c => c.thuTu)
                .ToList();

            foreach (var ch in chapters)
            {
                var chItem = new ChuongHocItem
                {
                    MaChuong = ch.maChuong,
                    TenChuong = ch.tenChuong,
                    ThuTu = ch.thuTu ?? 1
                };

                var lessons = db.BaiHoc
                    .Where(b => b.maChuong == ch.maChuong)
                    .OrderBy(b => b.thuTu)
                    .ToList();

                foreach (var les in lessons)
                {
                    var lesItem = new BaiHocItem
                    {
                        MaBaiHoc = les.maBaiHoc,
                        TenBaiHoc = les.tenBaiHoc,
                        DuongDanVideo = les.duongDanVideo,
                        ThoiLuong = les.thoiLuong ?? 0,
                        ChoXemThu = les.choXemThu ?? false,
                        ThuTu = les.thuTu ?? 1
                    };

                    var resources = db.TaiNguyen
                        .Where(t => t.maBaiHoc == les.maBaiHoc)
                        .ToList();

                    foreach (var res in resources)
                    {
                        lesItem.TaiNguyens.Add(new TaiNguyenItem
                        {
                            MaTaiNguyen = res.maTaiNguyen,
                            TenTaiNguyen = res.tenTaiNguyen,
                            DuongDan = res.duongDan,
                            LoaiTaiNguyen = res.loaiTaiNguyen
                        });
                    }

                    chItem.BaiHocs.Add(lesItem);
                }

                model.ChuongHocs.Add(chItem);
            }

            // Bài học không phân chương (maChuong == null)
            var unchapteredLessons = db.BaiHoc
                .Where(b => b.maChuong == null && b.maBaiHoc.StartsWith(currentCourse.maKhoaHoc + "_"))
                .OrderBy(b => b.thuTu)
                .ToList();

            foreach (var les in unchapteredLessons)
            {
                var lesItem = new BaiHocItem
                {
                    MaBaiHoc = les.maBaiHoc,
                    TenBaiHoc = les.tenBaiHoc,
                    DuongDanVideo = les.duongDanVideo,
                    ThoiLuong = les.thoiLuong ?? 0,
                    ChoXemThu = les.choXemThu ?? false,
                    ThuTu = les.thuTu ?? 1
                };

                var resources = db.TaiNguyen
                    .Where(t => t.maBaiHoc == les.maBaiHoc)
                    .ToList();

                foreach (var res in resources)
                {
                    lesItem.TaiNguyens.Add(new TaiNguyenItem
                    {
                        MaTaiNguyen = res.maTaiNguyen,
                        TenTaiNguyen = res.tenTaiNguyen,
                        DuongDan = res.duongDan,
                        LoaiTaiNguyen = res.loaiTaiNguyen
                    });
                }

                model.BaiHocKhongChuong.Add(lesItem);
            }

            ViewBag.TeacherCourses = teacherCourses;
            ViewBag.SelectedCourseId = currentCourse.maKhoaHoc;
            ViewBag.BreadcrumbCurrent = "Đề cương & Tài nguyên";

            return View(model);
        }

        // POST: Instructor/Curriculum/AddChapter
        [HttpPost]
        public JsonResult AddChapter(string courseId, string tenChuong, int? thuTu)
        {
            if (string.IsNullOrWhiteSpace(tenChuong))
            {
                return Json(new { success = false, message = "Tên chương học không được để trống!" });
            }

            string chapterId = "CH" + DateTime.Now.ToString("yyMMddHHmmss");
            var chapter = new ChuongHoc
            {
                maChuong = chapterId,
                maKhoaHoc = courseId,
                tenChuong = tenChuong.Trim(),
                thuTu = thuTu ?? (db.ChuongHoc.Count(c => c.maKhoaHoc == courseId) + 1)
            };

            db.ChuongHoc.Add(chapter);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã thêm chương học mới thành công!", chapterId = chapterId });
        }

        // POST: Instructor/Curriculum/DeleteChapter
        [HttpPost]
        public JsonResult DeleteChapter(string chapterId)
        {
            var chapter = db.ChuongHoc.FirstOrDefault(c => c.maChuong == chapterId);
            if (chapter == null)
            {
                return Json(new { success = false, message = "Không tìm thấy chương học!" });
            }

            // Xóa các bài học con và tài nguyên
            if (chapter.BaiHoc != null)
            {
                foreach (var b in chapter.BaiHoc.ToList())
                {
                    if (b.TaiNguyen != null)
                    {
                        db.TaiNguyen.RemoveRange(b.TaiNguyen);
                    }
                    db.BaiHoc.Remove(b);
                }
            }

            db.ChuongHoc.Remove(chapter);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã xóa chương học thành công!" });
        }

        // POST: Instructor/Curriculum/AddLesson (Tải lên file video trực tiếp, không hỗ trợ YouTube)
        [HttpPost]
        public JsonResult AddLesson(string courseId, string chapterId, string tenBaiHoc, int? thoiLuong, bool choXemThu, HttpPostedFileBase videoFile)
        {
            if (string.IsNullOrWhiteSpace(tenBaiHoc))
            {
                return Json(new { success = false, message = "Tên bài học không được để trống!" });
            }

            // Kiểm tra file video upload từ model binding hoặc Request.Files
            if (videoFile == null || videoFile.ContentLength == 0)
            {
                if (Request.Files.Count > 0 && Request.Files["videoFile"] != null && Request.Files["videoFile"].ContentLength > 0)
                {
                    videoFile = Request.Files["videoFile"];
                }
                else if (Request.Files.Count > 0 && Request.Files[0].ContentLength > 0)
                {
                    videoFile = Request.Files[0];
                }
            }

            if (videoFile == null || videoFile.ContentLength == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn hoặc kéo thả file video bài giảng (MP4, WebM, MOV, MKV) để tải lên! Hệ thống không còn hỗ trợ liên kết YouTube." });
            }

            // Kiểm tra định dạng video hợp lệ
            string ext = Path.GetExtension(videoFile.FileName)?.ToLowerInvariant();
            string[] allowedExts = { ".mp4", ".webm", ".mov", ".mkv", ".m4v", ".ogv" };
            if (string.IsNullOrEmpty(ext) || !allowedExts.Contains(ext))
            {
                return Json(new { success = false, message = "Định dạng file không hợp lệ! Vui lòng chỉ tải lên file video định dạng MP4, WebM, MOV hoặc MKV." });
            }

            // Lưu file video vào thư mục /assets/uploads/videos/
            string uploadDir = Server.MapPath("~/assets/uploads/videos/");
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            string cleanCourseId = !string.IsNullOrWhiteSpace(courseId) ? courseId.Trim() : "course";
            string uniqueFileName = $"vid_{cleanCourseId}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}{ext}";
            string fullPath = Path.Combine(uploadDir, uniqueFileName);
            videoFile.SaveAs(fullPath);
            string savedVideoUrl = "/assets/uploads/videos/" + uniqueFileName;

            bool isNoChapter = string.IsNullOrWhiteSpace(chapterId) || chapterId == "none" || chapterId == "null";

            string lessonId;
            int order;

            if (isNoChapter)
            {
                if (string.IsNullOrWhiteSpace(courseId))
                {
                    return Json(new { success = false, message = "Không xác định được mã khóa học!" });
                }

                string prefix = courseId + "_";
                var existingLessons = db.BaiHoc
                    .Where(b => b.maChuong == null && b.maBaiHoc.StartsWith(prefix))
                    .ToList();

                order = existingLessons.Count + 1;

                int nextSeq = 1;
                foreach (var b in existingLessons)
                {
                    string suffix = b.maBaiHoc.Substring(prefix.Length);
                    if (int.TryParse(suffix, out int parsedSeq) && parsedSeq >= nextSeq)
                    {
                        nextSeq = parsedSeq + 1;
                    }
                }

                lessonId = prefix + nextSeq;
                while (db.BaiHoc.Any(b => b.maBaiHoc == lessonId))
                {
                    nextSeq++;
                    lessonId = prefix + nextSeq;
                }
            }
            else
            {
                order = db.BaiHoc.Count(b => b.maChuong == chapterId) + 1;
                lessonId = "BH" + DateTime.Now.ToString("yyMMddHHmmss");
            }

            var lesson = new BaiHoc
            {
                maBaiHoc = lessonId,
                maChuong = isNoChapter ? null : chapterId,
                tenBaiHoc = tenBaiHoc.Trim(),
                duongDanVideo = savedVideoUrl,
                thoiLuong = thoiLuong ?? 900,
                choXemThu = choXemThu,
                thuTu = order
            };

            db.BaiHoc.Add(lesson);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã tải lên video và thêm bài học mới thành công!", lessonId = lessonId, isNoChapter = isNoChapter, videoUrl = savedVideoUrl });
        }

        // POST: Instructor/Curriculum/DeleteLesson
        [HttpPost]
        public JsonResult DeleteLesson(string lessonId)
        {
            var lesson = db.BaiHoc.FirstOrDefault(b => b.maBaiHoc == lessonId);
            if (lesson == null)
            {
                return Json(new { success = false, message = "Không tìm thấy bài học!" });
            }

            // Xóa file video vật lý trên máy chủ nếu là file upload
            if (!string.IsNullOrEmpty(lesson.duongDanVideo) && lesson.duongDanVideo.StartsWith("/assets/uploads/videos/"))
            {
                try
                {
                    string localPath = Server.MapPath("~" + lesson.duongDanVideo);
                    if (System.IO.File.Exists(localPath))
                    {
                        System.IO.File.Delete(localPath);
                    }
                }
                catch { }
            }

            if (lesson.TaiNguyen != null)
            {
                db.TaiNguyen.RemoveRange(lesson.TaiNguyen);
            }

            db.BaiHoc.Remove(lesson);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã xóa bài học thành công!" });
        }

        // POST: Instructor/Curriculum/AddResource
        [HttpPost]
        public JsonResult AddResource(string lessonId, string tenTaiNguyen, string fileUrl, string loaiTaiNguyen)
        {
            if (string.IsNullOrWhiteSpace(tenTaiNguyen))
            {
                return Json(new { success = false, message = "Tên tài nguyên không được để trống!" });
            }

            string resId = "TN" + DateTime.Now.ToString("yyMMddHHmmss");
            var res = new TaiNguyen
            {
                maTaiNguyen = resId,
                maBaiHoc = lessonId,
                tenTaiNguyen = tenTaiNguyen.Trim(),
                duongDan = !string.IsNullOrWhiteSpace(fileUrl) ? fileUrl.Trim() : "https://example.com/tai-lieu.pdf",
                loaiTaiNguyen = !string.IsNullOrWhiteSpace(loaiTaiNguyen) ? loaiTaiNguyen.Trim() : "PDF"
            };

            db.TaiNguyen.Add(res);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã thêm tài nguyên thành công!", resourceId = resId });
        }

        // POST: Instructor/Curriculum/DeleteResource
        [HttpPost]
        public JsonResult DeleteResource(string resourceId)
        {
            var res = db.TaiNguyen.FirstOrDefault(t => t.maTaiNguyen == resourceId);
            if (res == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tài nguyên!" });
            }

            db.TaiNguyen.Remove(res);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã xóa tài nguyên thành công!" });
        }
    }
}
