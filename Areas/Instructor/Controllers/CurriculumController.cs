using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Instructor.Models;
using ECOLearning.Models;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class CurriculumController : InstructorBaseController
    {
        protected override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            if (context.Result != null || !string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) return;
            string courseId = Request["courseId"];
            string chapterId = Request["chapterId"];
            string lessonId = Request["lessonId"];
            string resourceId = Request["resourceId"];
            if (!string.IsNullOrEmpty(resourceId)) {
                var resource = db.TaiNguyen.FirstOrDefault(r => r.maTaiNguyen == resourceId);
                lessonId = resource?.maBaiHoc;
            }
            if (!string.IsNullOrEmpty(lessonId)) {
                var lesson = db.BaiHoc.FirstOrDefault(l => l.maBaiHoc == lessonId);
                chapterId = lesson?.maChuong;
                if (lesson != null && chapterId == null) {
                    int separator = lesson.maBaiHoc.LastIndexOf('_');
                    courseId = separator > 0 ? lesson.maBaiHoc.Substring(0, separator) : null;
                }
            }
            if (!string.IsNullOrEmpty(chapterId) && chapterId != "none")
                courseId = db.ChuongHoc.FirstOrDefault(c => c.maChuong == chapterId)?.maKhoaHoc;
            var course = db.KhoaHoc.FirstOrDefault(c => c.maKhoaHoc == courseId && c.maGiaoVien == CurrentTeacherId);
            if (course == null) {
                context.Result = Json(new { success = false, message = "Không tìm thấy khóa học hoặc bạn không có quyền chỉnh sửa." }); return;
            }
            // Tạo phụ đề là tác vụ dẫn xuất từ video có sẵn, không sửa đề cương đã xuất bản.
            bool laTaoPhuDe = string.Equals(context.ActionDescriptor.ActionName, "GenerateSubtitle", StringComparison.OrdinalIgnoreCase);
            if (course.trangThai == ECOLearning.Services.CourseStatus.TamNgung || course.trangThai == ECOLearning.Services.CourseStatus.DaXoa) {
                context.Result = Json(new { success = false, message = "Khóa học đang tạm ngưng bán hoặc đã xóa nên không thể chỉnh sửa. Hãy mở bán lại trước." }); return;
            }
            if (!laTaoPhuDe && (ECOLearning.Services.CourseRevisionService.Published(course.trangThai) || course.DangKyKhoaHoc.Any())) {
                context.Result = Json(new { success = false, message = "Khóa đã xuất bản cần duyệt bản sửa. Vào Quản lý khóa học → Chỉnh sửa thông tin để lưu bản nháp và gửi Admin duyệt." }); return;
            }
            if (!laTaoPhuDe && (course.trangThai == "Chờ duyệt" || course.trangThai == "ChoDuyet"))
                context.Result = Json(new { success = false, message = "Khóa học đang chờ duyệt. Vui lòng hủy yêu cầu duyệt trước khi chỉnh sửa." });
        }

        private string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string[] signs = new string[] {
                "aAeEoOuUiIdDyY",
                "áàạảãâấầậẩẫăắằặẳẵ",
                "ÁÀẠẢÃÂẤẦẬẨẪĂẮẰẶẲẴ",
                "éèẹẻẽêếềệểễ",
                "ÉÈẸẺẼÊẾỀỆỂỄ",
                "óòọỏõôốồộổỗơớờợởỡ",
                "ÓÒỌỎÕÔỐỒỘỔỖƠỚỜỢỞỠ",
                "úùụủũưứừựửữ",
                "ÚÙỤỦŨƯỨỪỰỬỮ",
                "íìịỉĩ",
                "ÍÌỊỈĨ",
                "đ",
                "Đ",
                "ýỳỵỷỹ",
                "ÝỲỴỶỸ"
            };
            for (int i = 1; i < signs.Length; i++)
            {
                for (int j = 0; j < signs[i].Length; j++)
                {
                    text = text.Replace(signs[i][j], signs[0][i - 1]);
                }
            }
            return System.Text.RegularExpressions.Regex.Replace(text.Replace(" ", "_"), @"[^a-zA-Z0-9_]", "");
        }
        // GET: Instructor/Curriculum (UC10: Đề cương & Tài nguyên khóa học)
        public ActionResult Index(string courseId = "")
        {
            int teacherId = CurrentTeacherId;
            var teacherCourses = db.KhoaHoc
                .Where(k => k.maGiaoVien == teacherId && k.trangThai != ECOLearning.Services.CourseStatus.DaXoa)
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
                TrangThai = currentCourse.trangThai,
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

            model.ChuongHocs.Add(new ChuongHocItem {
                MaChuong = "none", TenChuong = "Bài học không theo chương", ThuTu = 0,
                BaiHocs = model.BaiHocKhongChuong
            });
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

        // POST: Instructor/Curriculum/AddLesson
        [HttpPost]
        public JsonResult AddLesson(string courseId, string chapterId, string tenBaiHoc, string videoUrl, int? thoiLuong, bool choXemThu)
        {
            if (string.IsNullOrWhiteSpace(tenBaiHoc))
            {
                return Json(new { success = false, message = "Tên bài học không được để trống!" });
            }

            bool noChapter = string.IsNullOrEmpty(chapterId) || chapterId == "none";
            if (!db.KhoaHoc.Any(k => k.maKhoaHoc == courseId && k.maGiaoVien == CurrentTeacherId))
                return Json(new { success = false, message = "Không tìm thấy khóa học." });
            string lessonId;
            int order;
            if (noChapter) {
                var existing = db.BaiHoc.Where(b => b.maChuong == null && b.maBaiHoc.StartsWith(courseId + "_")).ToList();
                order = existing.Count + 1;
                int sequence = 1;
                while (db.BaiHoc.Any(b => b.maBaiHoc == courseId + "_" + sequence)) sequence++;
                lessonId = courseId + "_" + sequence;
            } else {
                if (!db.ChuongHoc.Any(c => c.maChuong == chapterId && c.maKhoaHoc == courseId))
                    return Json(new { success = false, message = "Không tìm thấy chương." });
                lessonId = "BH" + Guid.NewGuid().ToString("N").Substring(0,18);
                order = db.BaiHoc.Count(b => b.maChuong == chapterId) + 1;
            }

            var lesson = new BaiHoc
            {
                maBaiHoc = lessonId,
                maChuong = noChapter ? null : chapterId,
                tenBaiHoc = tenBaiHoc.Trim(),
                duongDanVideo = !string.IsNullOrWhiteSpace(videoUrl) ? videoUrl.Trim() : null,
                thoiLuong = thoiLuong ?? 15,
                choXemThu = choXemThu,
                thuTu = order
            };

            db.BaiHoc.Add(lesson);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã thêm bài học thành công!", lessonId = lessonId });
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
        public JsonResult AddResource(string lessonId, System.Web.HttpPostedFileBase resourceFile)
        {
            if (string.IsNullOrWhiteSpace(lessonId))
                return Json(new { success = false, message = "Thiếu ID bài học!" });

            if (resourceFile == null || resourceFile.ContentLength == 0)
                return Json(new { success = false, message = "Vui lòng chọn một tệp tài liệu!" });

            string ext = System.IO.Path.GetExtension(resourceFile.FileName).ToLower();
            string loai = "PDF";
            if (ext == ".zip") loai = "ZIP";
            else if (ext == ".docx" || ext == ".doc") loai = "DOCX";
            else if (ext == ".pptx" || ext == ".ppt") loai = "PPTX";

            string uploadFolder = Server.MapPath("~/Uploads/Resources/");
            if (!System.IO.Directory.Exists(uploadFolder))
                System.IO.Directory.CreateDirectory(uploadFolder);

            string safeFileName = RemoveDiacritics(System.IO.Path.GetFileNameWithoutExtension(resourceFile.FileName));
            string fileName = safeFileName + "_" + DateTime.Now.ToString("yyMMddHHmmss") + ext;
            string filePath = System.IO.Path.Combine(uploadFolder, fileName);
            resourceFile.SaveAs(filePath);

            string relativeUrl = "/Uploads/Resources/" + fileName;
            string displayName = System.IO.Path.GetFileNameWithoutExtension(resourceFile.FileName);

            string resId = "TN" + DateTime.Now.ToString("yyMMddHHmmss");
            var res = new TaiNguyen
            {
                maTaiNguyen = resId,
                maBaiHoc = lessonId,
                tenTaiNguyen = displayName,
                duongDan = relativeUrl,
                loaiTaiNguyen = loai
            };

            db.TaiNguyen.Add(res);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã tải lên tài liệu thành công!", resourceId = resId });
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

                        // POST: Instructor/Curriculum/UploadLessonVideo
        [HttpPost]
        public JsonResult UploadLessonVideo(string lessonId, System.Web.HttpPostedFileBase videoFile, int duration = 0)
        {
            if (string.IsNullOrWhiteSpace(lessonId))
                return Json(new { success = false, message = "Thiếu ID bài học!" });

            if (videoFile == null || videoFile.ContentLength == 0)
                return Json(new { success = false, message = "Vui lòng chọn một tệp video!" });

            var lesson = db.BaiHoc.FirstOrDefault(b => b.maBaiHoc == lessonId);
            if (lesson == null) return Json(new { success = false, message = "Không tìm thấy bài học!" });

            string ext = System.IO.Path.GetExtension(videoFile.FileName).ToLower();
            if (ext != ".mp4" && ext != ".avi" && ext != ".webm")
            {
                return Json(new { success = false, message = "Chỉ chấp nhận tệp video MP4/AVI/WEBM." });
            }

            // Cloudflare R2 qua hàng đợi nén (chưa cấu hình R2 thì lưu trong assets/uploads/videos)
            string relativeUrl = ECOLearning.Services.VideoStorageService.Luu(videoFile);

            // Update lesson
            lesson.duongDanVideo = relativeUrl;
            lesson.thoiLuong = duration > 0 ? duration : 0;
            // Video mới thì phụ đề cũ không còn khớp timestamp, phải tạo lại.
            ECOLearning.Services.VideoSubtitleService.Xoa(lesson.maBaiHoc);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã tải lên video thành công!", videoUrl = ECOLearning.Services.VideoStorageService.LinkPhat(relativeUrl) });
        }

        // POST: Instructor/Curriculum/RemoveVideo
        [HttpPost]
        public JsonResult RemoveVideo(string lessonId)
        {
            var lesson = db.BaiHoc.FirstOrDefault(b => b.maBaiHoc == lessonId);
            if (lesson == null) return Json(new { success = false, message = "Không tìm thấy bài học!" });
            
            lesson.duongDanVideo = null;
            lesson.thoiLuong = 0;
            ECOLearning.Services.VideoSubtitleService.Xoa(lesson.maBaiHoc);
            db.SaveChanges();
            
            return Json(new { success = true, message = "Đã xóa video khỏi bài học!" });
        }

        // POST: Instructor/Curriculum/GenerateSubtitle
        // Hàng đợi chạy nền: request trả ngay để không bị timeout với video dài.
        [HttpPost, ValidateAntiForgeryToken]
        public JsonResult GenerateSubtitle(string lessonId)
        {
            if (string.IsNullOrWhiteSpace(lessonId))
                return Json(new { success = false, message = "Thiếu mã bài học." });

            var lesson = db.BaiHoc.FirstOrDefault(b => b.maBaiHoc == lessonId);
            if (lesson == null) return Json(new { success = false, message = "Không tìm thấy bài học." });
            if (string.IsNullOrWhiteSpace(lesson.duongDanVideo))
                return Json(new { success = false, message = "Bài học chưa có video để tạo phụ đề." });

            var ketQua = ECOLearning.Services.VideoSubtitleService.XepHang(lesson.maBaiHoc, lesson.duongDanVideo);
            return Json(new
            {
                success = ketQua.ThanhCong,
                message = ketQua.ThongBao,
                status = ketQua.TrangThai == null ? null : ketQua.TrangThai.TrangThai,
                hasSubtitle = ketQua.TrangThai != null && ketQua.TrangThai.CoPhuDe
            });
        }

                // POST: Instructor/Curriculum/EditChapter
        [HttpPost]
        public JsonResult EditChapter(string chapterId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return Json(new { success = false, message = "Tên không hợp lệ!" });
            var chapter = db.ChuongHoc.FirstOrDefault(c => c.maChuong == chapterId);
            if (chapter == null) return Json(new { success = false, message = "Không tìm thấy chương học!" });
            
            chapter.tenChuong = newName.Trim();
            db.SaveChanges();
            
            return Json(new { success = true, message = "Đã đổi tên chương!" });
        }

        // POST: Instructor/Curriculum/EditLesson
        [HttpPost]
        public JsonResult EditLesson(string lessonId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return Json(new { success = false, message = "Tên không hợp lệ!" });
            var lesson = db.BaiHoc.FirstOrDefault(b => b.maBaiHoc == lessonId);
            if (lesson == null) return Json(new { success = false, message = "Không tìm thấy bài học!" });
            
            lesson.tenBaiHoc = newName.Trim();
            db.SaveChanges();
            
            return Json(new { success = true, message = "Đã đổi tên bài học!" });
        }

        // POST: Instructor/Curriculum/RenameResource
        [HttpPost]
        public JsonResult RenameResource(string resourceId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return Json(new { success = false, message = "Tên không hợp lệ!" });
            var res = db.TaiNguyen.FirstOrDefault(t => t.maTaiNguyen == resourceId);
            if (res == null) return Json(new { success = false, message = "Không tìm thấy tài nguyên!" });
            
            res.tenTaiNguyen = newName;
            db.SaveChanges();
            
            return Json(new { success = true, message = "Đã đổi tên tài nguyên!" });
        }

        // GET: Instructor/Curriculum/ViewResource/TN...
        [HttpGet]
        public ActionResult ViewResource(string id)
        {
            var res = db.TaiNguyen.FirstOrDefault(t => t.maTaiNguyen == id);
            if (res == null) return HttpNotFound();

            string physicalPath = Server.MapPath(res.duongDan);
            if (!System.IO.File.Exists(physicalPath))
                return HttpNotFound("Không tìm thấy tệp trên máy chủ.");

            string ext = System.IO.Path.GetExtension(physicalPath).ToLower();
            string mimeType = "application/octet-stream";
            if (ext == ".pdf") mimeType = "application/pdf";
            else if (ext == ".png") mimeType = "image/png";
            else if (ext == ".jpg" || ext == ".jpeg") mimeType = "image/jpeg";
            else if (ext == ".gif") mimeType = "image/gif";
            else if (ext == ".txt") mimeType = "text/plain";
            
            // Forces inline display
            Response.Headers["Content-Disposition"] = "inline; filename=\"" + System.IO.Path.GetFileName(physicalPath) + "\"";
            return File(physicalPath, mimeType);
        }
    }
}




