using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Instructor.Models;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class CourseController : InstructorBaseController
    {
        // GET: Instructor/Course (UC9: Danh sách khóa học của giảng viên)
        public ActionResult Index(string search = "", string status = "")
        {
            int teacherId = CurrentTeacherId;

            var query = db.KhoaHoc
                .Where(k => k.maGiaoVien == teacherId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                string s = search.Trim().ToLower();
                query = query.Where(k => k.tenKhoaHoc.ToLower().Contains(s) || k.maKhoaHoc.ToLower().Contains(s));
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(k => k.trangThai == status);
            }

            var courses = query.OrderByDescending(k => k.maKhoaHoc).ToList();

            var model = courses.Select(k => {
                int studentCount = k.DangKyKhoaHoc != null ? k.DangKyKhoaHoc.Count : 0;
                int lessonCount = 0;
                if (k.ChuongHoc != null)
                {
                    foreach (var c in k.ChuongHoc)
                    {
                        if (c.BaiHoc != null) lessonCount += c.BaiHoc.Count;
                    }
                }
                lessonCount += db.BaiHoc.Count(b => b.maChuong == null && b.maBaiHoc.StartsWith(k.maKhoaHoc + "_"));

                return new InstructorCourseItem
                {
                    MaKhoaHoc = k.maKhoaHoc,
                    TenKhoaHoc = k.tenKhoaHoc,
                    AnhBia = k.anhBia ?? "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?w=300&auto=format&fit=crop&q=80",
                    TenDanhMuc = k.DanhMuc != null ? k.DanhMuc.tenDanhMuc : "Chung",
                    GiaBan = k.giaBan,
                    SoHocVien = studentCount,
                    TrangThai = k.trangThai ?? "Bản nháp",
                    NgayTao = DateTime.Now,
                    SoBaiHoc = lessonCount,
                    CoVideo = db.BaiHoc.Any(b => b.duongDanVideo != null && b.duongDanVideo.Trim() != "" &&
                        ((b.ChuongHoc != null && b.ChuongHoc.maKhoaHoc == k.maKhoaHoc) ||
                         (b.maChuong == null && b.maBaiHoc.StartsWith(k.maKhoaHoc + "_")))),
                    LyDoTuChoi = k.KiemDuyetKhoaHoc != null ? k.KiemDuyetKhoaHoc.OrderByDescending(kd => kd.ngayGui).Select(kd => kd.lyDoTuChoi).FirstOrDefault() : null
                };
            }).ToList();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.BreadcrumbCurrent = "Quản lý khóa học";

            return View(model);
        }

        private void CreateOrUpdateKiemDuyetRecord(string courseId)
        {
            var kd = db.KiemDuyetKhoaHoc.FirstOrDefault(x => x.maKhoaHoc == courseId);
            if (kd == null)
            {
                kd = new KiemDuyetKhoaHoc
                {
                    maKiemDuyet = "KD-" + Guid.NewGuid().ToString().Substring(0, 8),
                    maKhoaHoc = courseId,
                    ngayGui = DateTime.Now,
                    trangThai = "Chờ duyệt",
                    lyDoTuChoi = null
                };
                db.KiemDuyetKhoaHoc.Add(kd);
            }
            else
            {
                kd.ngayGui = DateTime.Now;
                kd.trangThai = "Chờ duyệt";
                kd.lyDoTuChoi = null;
            }
        }

        // Danh mục cho dropdown: chỉ danh mục đang hoạt động (Admin có thể tạm ngưng danh mục).
        // Khi sửa khóa học thuộc danh mục đã tạm ngưng, vẫn giữ danh mục hiện tại để không mất lựa chọn.
        private List<SelectListItem> BuildCategoryOptions(string currentCategoryId = null)
        {
            return db.DanhMuc
                .Where(c => c.trangThai == null || c.trangThai == true || c.maDanhMuc == currentCategoryId)
                .OrderBy(c => c.tenDanhMuc)
                .ToList()
                .Select(c => new SelectListItem
                {
                    Value = c.maDanhMuc,
                    Text = c.trangThai == false ? c.tenDanhMuc + " (tạm ngưng)" : c.tenDanhMuc
                })
                .ToList();
        }

        // Danh mục được chọn phải tồn tại và đang hoạt động (trừ khi giữ nguyên danh mục cũ của khóa học)
        private void ValidateCategory(string maDanhMuc, string currentCategoryId = null)
        {
            if (string.IsNullOrEmpty(maDanhMuc))
            {
                ModelState.AddModelError("MaDanhMuc", "Vui lòng chọn danh mục đào tạo!");
                return;
            }
            if (maDanhMuc == currentCategoryId) return;

            var cat = db.DanhMuc.FirstOrDefault(c => c.maDanhMuc == maDanhMuc);
            if (cat == null || cat.trangThai == false)
            {
                ModelState.AddModelError("MaDanhMuc", "Danh mục đã chọn không tồn tại hoặc đang tạm ngưng!");
            }
        }

        // Khóa học bị khóa do báo cáo vi phạm: trả về thông báo lỗi nếu thao tác không được phép, ngược lại null.
        // Tạm khóa: được sửa nội dung nhưng không tự gửi duyệt / xóa. Khóa vĩnh viễn: không sửa / xóa.
        private string GetLockedCourseError(KhoaHoc course, bool isSubmitOrDelete)
        {
            if (course.trangThai == CourseStatus.KhoaVinhVien)
                return "Khóa học đã bị khóa vĩnh viễn do vi phạm nên không thể chỉnh sửa hoặc xóa!";
            if (course.trangThai == CourseStatus.TamKhoa && isSubmitOrDelete)
                return "Khóa học đang bị tạm khóa do vi phạm. Hãy chỉnh sửa nội dung rồi gửi phản hồi cho quản trị viên ở trang Xử lý vi phạm.";
            return null;
        }

        private Dictionary<string, string[]> CourseUploads
        {
            get {
                var files = Session["CourseWizardUploads"] as Dictionary<string, string[]>;
                if (files == null) { files = new Dictionary<string, string[]>(); Session["CourseWizardUploads"] = files; }
                return files;
            }
        }

        /// <summary>Bài học có video đã tải lên (token trong phiên làm việc) và file video còn trên máy chủ.</summary>
        private bool CoVideoTaiLen(CourseLessonInput lesson)
        {
            string[] upload;
            return !string.IsNullOrEmpty(lesson.VideoToken)
                && CourseUploads.TryGetValue(lesson.VideoToken, out upload)
                && upload[0] == "video"
                && new CourseContentService(db, Server.MapPath).VideoHopLe(upload[1]);
        }

        /// <summary>
        /// Điều kiện gửi duyệt (CourseContentService): có chương, có bài học, mỗi chương có bài và MỖI bài học có video.
        /// Liệt kê bài còn thiếu để giáo viên bổ sung.
        /// </summary>
        private void KiemTraNoiDungGuiDuyet(List<CourseChapterInput> chapters, List<CourseLessonInput> lessons, Func<CourseLessonInput, bool> coVideo)
        {
            if (chapters.Count == 0) ModelState.AddModelError("", "Khóa học cần ít nhất một chương học trước khi gửi duyệt.");
            if (lessons.Count == 0) ModelState.AddModelError("", "Khóa học cần ít nhất một bài học trước khi gửi duyệt.");
            for (int i = 0; i < chapters.Count; i++)
            {
                if (!lessons.Any(l => l.ChapterIndex == i))
                    ModelState.AddModelError("", "Chương \"" + (chapters[i].Title ?? "").Trim() + "\" chưa có bài học.");
            }
            var thieuVideo = lessons.Where(l => !coVideo(l))
                .Select(l => string.IsNullOrWhiteSpace(l.Title) ? "(chưa đặt tên)" : l.Title.Trim())
                .ToList();
            if (thieuVideo.Any())
                ModelState.AddModelError("", "Mỗi bài học phải có video bài giảng trước khi gửi duyệt. Bài chưa có video: " + string.Join(", ", thieuVideo) + ".");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UploadLessonFile(System.Web.HttpPostedFileBase file, string kind)
        {
            if (file == null || file.ContentLength == 0) return Json(new { success = false, message = "Vui lòng chọn tệp." });
            string extension = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            var videos = new[] { ".mp4", ".webm", ".mov", ".mkv", ".m4v", ".ogv" };
            var documents = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".txt", ".csv" };
            if ((kind != "video" && kind != "document") || !(kind == "video" ? videos : documents).Contains(extension))
                return Json(new { success = false, message = "Định dạng tệp không được hỗ trợ." });
            if (kind == "document" && file.ContentLength > 50 * 1024 * 1024)
                return Json(new { success = false, message = "Tài liệu không được lớn hơn 50MB." });
            // Video: lên Cloudflare R2 qua hàng đợi nén (VideoStorageService), chưa cấu hình R2 thì lưu trên máy chủ như trước
            string url = kind == "video" ? VideoStorageService.Luu(file) : SaveCourseFile(file, "~/Uploads/CourseResources/");
            string token = Guid.NewGuid().ToString("N");
            string name = System.IO.Path.GetFileName(file.FileName);
            ResourceController.Register(Server, CurrentTeacherId, kind, name, url, file.ContentLength);
            CourseUploads[token] = new[] { kind, url, name };
            return Json(new { success = true, token, url, name });
        }

        private string SaveCourseFile(System.Web.HttpPostedFileBase file, string folder)
        {
            if (file == null || file.ContentLength == 0) return null;
            string directory = Server.MapPath(folder);
            System.IO.Directory.CreateDirectory(directory);
            string name = Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            file.SaveAs(System.IO.Path.Combine(directory, name));
            return Url.Content(folder + name);
        }

        // GET: Instructor/Course/Create
        public ActionResult Create()
        {
            var model = new CourseCreateOrEditViewModel();
            model.Categories = BuildCategoryOptions();

            model.CapDo = "Cơ bản";
            ViewBag.BreadcrumbCurrent = "Tạo khóa học mới";
            return View(model);
        }

        // POST: Instructor/Course/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CourseCreateOrEditViewModel model, string actionType = "draft")
        {
            // Normalize formatted VND price from Request.Form if needed
            string rawGiaBan = Request.Form["GiaBan"];
            if (!string.IsNullOrWhiteSpace(rawGiaBan))
            {
                string cleanGiaBan = rawGiaBan.Replace(".", "").Replace(",", "").Replace(" ", "").Replace("đ", "").Replace("₫", "").Replace("VND", "").Replace("VNĐ", "").Trim();
                if (decimal.TryParse(cleanGiaBan, out decimal parsedGiaBan))
                {
                    model.GiaBan = parsedGiaBan;
                    ModelState.Remove("GiaBan");
                }
            }

            if (string.IsNullOrWhiteSpace(model.TenKhoaHoc))
            {
                ModelState.AddModelError("TenKhoaHoc", "Vui lòng nhập tên khóa học!");
            }

            if (model.GiaBan < 0)
            {
                ModelState.AddModelError("GiaBan", "Giá bán không được là số âm!");
            }

            ValidateCategory(model.MaDanhMuc);

            var chapters = model.Chapters ?? new List<CourseChapterInput>();
            var lessons = model.Lessons ?? new List<CourseLessonInput>();
            var allowedVideos = new[] { ".mp4", ".webm", ".mov", ".mkv", ".m4v", ".ogv" };
            foreach (var chapter in chapters)
                if (string.IsNullOrWhiteSpace(chapter.Title)) ModelState.AddModelError("", "Vui lòng nhập tên chương.");
            foreach (var lesson in lessons)
            {
                if (!string.IsNullOrEmpty(lesson.VideoToken) && (!CourseUploads.ContainsKey(lesson.VideoToken) || CourseUploads[lesson.VideoToken][0] != "video"))
                    ModelState.AddModelError("", "Video tải lên không hợp lệ. Vui lòng tải lại.");
                foreach (string token in lesson.AttachmentTokens ?? new List<string>())
                    if (!CourseUploads.ContainsKey(token) || CourseUploads[token][0] != "document") ModelState.AddModelError("", "Tài liệu tải lên không hợp lệ.");
                if (string.IsNullOrWhiteSpace(lesson.Title)) ModelState.AddModelError("", "Vui lòng nhập tên bài học.");
                if (lesson.ChapterIndex.HasValue && (lesson.ChapterIndex.Value < 0 || lesson.ChapterIndex.Value >= chapters.Count))
                    ModelState.AddModelError("", "Chương của bài học không hợp lệ.");
                if (lesson.VideoFile != null && lesson.VideoFile.ContentLength > 0 && !allowedVideos.Contains(System.IO.Path.GetExtension(lesson.VideoFile.FileName).ToLowerInvariant()))
                    ModelState.AddModelError("", "Video phải có định dạng MP4, WebM, MOV, MKV, M4V hoặc OGV.");
            }
            if (actionType == "submit")
                KiemTraNoiDungGuiDuyet(chapters, lessons, l => (l.VideoFile != null && l.VideoFile.ContentLength > 0) || CoVideoTaiLen(l));

            if (!ModelState.IsValid)
            {
                model.Categories = BuildCategoryOptions();
                return View(model);
            }

            // Xử lý upload ảnh bìa từ máy tính
            if (model.CoverImageFile != null && model.CoverImageFile.ContentLength > 0)
            {
                var fileName = System.IO.Path.GetFileName(model.CoverImageFile.FileName);
                var path = System.IO.Path.Combine(Server.MapPath("~/Content/Uploads/CourseImages/"), fileName);

                // Đảm bảo thư mục tồn tại
                System.IO.Directory.CreateDirectory(Server.MapPath("~/Content/Uploads/CourseImages/"));

                model.CoverImageFile.SaveAs(path);
                model.AnhBia = "/Content/Uploads/CourseImages/" + fileName;
            }

            // Sinh mã khóa học tự động KH + timestamp
            string courseId = "KH" + DateTime.Now.ToString("yyMMddHHmmss");

            var course = new KhoaHoc
            {
                maKhoaHoc = courseId,
                tenKhoaHoc = model.TenKhoaHoc.Trim(),
                moTa = model.MoTa,
                moTaNgan = model.MoTaNgan,
                mucTieuDauRa = model.MucTieuDauRa,
                giaBan = model.GiaBan,
                giaKhuyenMai = null,
                capDo = model.CapDo ?? "Cơ bản",
                maDanhMuc = model.MaDanhMuc,
                maGiaoVien = CurrentTeacherId,
                anhBia = !string.IsNullOrWhiteSpace(model.AnhBia) ? model.AnhBia.Trim() : "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?w=500&auto=format&fit=crop&q=80",
                trangThai = (actionType == "submit") ? "Chờ duyệt" : "Bản nháp"
            };

            db.KhoaHoc.Add(course);

            var chapterIds = new List<string>();
            for (int i = 0; i < chapters.Count; i++)
            {
                string chapterId = "CH" + Guid.NewGuid().ToString("N").Substring(0, 18);
                chapterIds.Add(chapterId);
                db.ChuongHoc.Add(new ChuongHoc { maChuong = chapterId, maKhoaHoc = courseId, tenChuong = chapters[i].Title.Trim(), thuTu = i + 1 });
            }
            for (int i = 0; i < lessons.Count; i++)
            {
                var input = lessons[i];
                string lessonId = courseId + "_" + (i + 1);
                string videoUrl = !string.IsNullOrEmpty(input.VideoToken) ? CourseUploads[input.VideoToken][1] : (input.VideoFile != null && input.VideoFile.ContentLength > 0 ? VideoStorageService.Luu(input.VideoFile) : null);
                db.BaiHoc.Add(new BaiHoc {
                    maBaiHoc = lessonId,
                    maChuong = input.ChapterIndex.HasValue ? chapterIds[input.ChapterIndex.Value] : null,
                    tenBaiHoc = input.Title.Trim(), thuTu = i + 1, choXemThu = input.Preview,
                    duongDanVideo = videoUrl, thoiLuong = Math.Max(0, input.Duration)
                });
                foreach (string token in input.AttachmentTokens ?? new List<string>())
                {
                    var uploaded = CourseUploads[token];
                    db.TaiNguyen.Add(new TaiNguyen {
                        maTaiNguyen = "TN" + Guid.NewGuid().ToString("N").Substring(0, 18), maBaiHoc = lessonId,
                        tenTaiNguyen = uploaded[2], duongDan = uploaded[1], loaiTaiNguyen = System.IO.Path.GetExtension(uploaded[2])
                    });
                }
                string attachmentUrl = SaveCourseFile(input.AttachmentFile, "~/Uploads/CourseResources/");
                if (attachmentUrl != null)
                    db.TaiNguyen.Add(new TaiNguyen {
                        maTaiNguyen = "TN" + Guid.NewGuid().ToString("N").Substring(0, 18),
                        maBaiHoc = lessonId, tenTaiNguyen = System.IO.Path.GetFileName(input.AttachmentFile.FileName),
                        duongDan = attachmentUrl, loaiTaiNguyen = System.IO.Path.GetExtension(input.AttachmentFile.FileName)
                    });
            }


            if (actionType == "submit")
            {
                CreateOrUpdateKiemDuyetRecord(courseId);
            }

            db.SaveChanges();

            TempData["SuccessMessage"] = (actionType == "submit")
                ? "Khóa học đã được gửi yêu cầu kiểm duyệt thành công!"
                : "Đã lưu bản nháp khóa học thành công!";

            return RedirectToAction("Index");
        }

        public ActionResult Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return HttpNotFound();

            int teacherId = CurrentTeacherId;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == id && k.maGiaoVien == teacherId);
            if (course == null) return HttpNotFound();

            ViewBag.BreadcrumbCurrent = "Chi tiết khóa học";
            ViewBag.UnchapteredLessons = db.BaiHoc
                .Where(b => b.maChuong == null && b.maBaiHoc.StartsWith(course.maKhoaHoc + "_"))
                .OrderBy(b => b.thuTu).ToList();
            return View(course);
        }

        // GET: Instructor/Course/Edit/5
        public ActionResult Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return HttpNotFound();

            int teacherId = CurrentTeacherId;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == id && k.maGiaoVien == teacherId);
            if (course == null) return HttpNotFound();
            if (course.trangThai == "Chờ duyệt" || course.trangThai == "ChoDuyet")
            {
                TempData["ErrorMessage"] = "Khóa học đang chờ duyệt. Vui lòng hủy yêu cầu duyệt trước khi chỉnh sửa.";
                return RedirectToAction("Details", new { id = course.maKhoaHoc });
            }
            string lockError = GetLockedCourseError(course, false);
            if (lockError != null)
            {
                TempData["ErrorMessage"] = lockError;
                return RedirectToAction("Details", new { id = course.maKhoaHoc });
            }


            var model = new CourseCreateOrEditViewModel
            {
                MaKhoaHoc = course.maKhoaHoc,
                TenKhoaHoc = course.tenKhoaHoc,
                MoTa = course.moTa,
                MoTaNgan = course.moTaNgan,
                MucTieuDauRa = course.mucTieuDauRa,
                GiaBan = course.giaBan,
                CapDo = course.capDo,
                MaDanhMuc = course.maDanhMuc,
                AnhBia = course.anhBia,
                TrangThai = course.trangThai,
                Categories = BuildCategoryOptions(course.maDanhMuc)
            };

            var storedChapters = db.ChuongHoc.Where(c => c.maKhoaHoc == id).OrderBy(c => c.thuTu).ToList();
            model.Chapters = storedChapters.Select(c => new CourseChapterInput { Id = c.maChuong, Title = c.tenChuong }).ToList();
            var storedLessons = db.BaiHoc.Where(l => (l.ChuongHoc != null && l.ChuongHoc.maKhoaHoc == id) || (l.maChuong == null && l.maBaiHoc.StartsWith(id + "_"))).OrderBy(l => l.thuTu).ToList();
            foreach (var lesson in storedLessons) {
                var input = new CourseLessonInput { Id = lesson.maBaiHoc, Title = lesson.tenBaiHoc, Preview = lesson.choXemThu ?? false, Duration = lesson.thoiLuong ?? 0,
                    ChapterIndex = lesson.maChuong == null ? (int?)null : storedChapters.FindIndex(c => c.maChuong == lesson.maChuong) };
                if (!string.IsNullOrWhiteSpace(lesson.duongDanVideo)) {
                    input.VideoToken = Guid.NewGuid().ToString("N");
                    CourseUploads[input.VideoToken] = new[] { "video", lesson.duongDanVideo, System.IO.Path.GetFileName(lesson.duongDanVideo) };
                }
                foreach (var resource in lesson.TaiNguyen) {
                    string token = Guid.NewGuid().ToString("N");
                    CourseUploads[token] = new[] { "document", resource.duongDan, resource.tenTaiNguyen };
                    input.AttachmentTokens.Add(token);
                }
                model.Lessons.Add(input);
            }
            ViewBag.BreadcrumbCurrent = "Chỉnh sửa khóa học";
            return View("Create", model);
        }

        // POST: Instructor/Course/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(CourseCreateOrEditViewModel model, string actionType = "save")
        {
            int teacherId = CurrentTeacherId;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == model.MaKhoaHoc && k.maGiaoVien == teacherId);
            if (course == null) return HttpNotFound();
            if (course.trangThai == "Chờ duyệt" || course.trangThai == "ChoDuyet")
            {
                TempData["ErrorMessage"] = "Khóa học đang chờ duyệt. Vui lòng hủy yêu cầu duyệt trước khi chỉnh sửa.";
                return RedirectToAction("Details", new { id = course.maKhoaHoc });
            }
            // Khóa học đang tạm khóa do vi phạm: "gửi" nghĩa là lưu nội dung đã sửa rồi chuyển sang trang
            // Xử lý vi phạm để gửi phản hồi cho Admin (không đưa vào hàng đợi kiểm duyệt)
            if (course.trangThai == CourseStatus.TamKhoa && actionType == "submit") actionType = "violation";
            string lockError = GetLockedCourseError(course, false);
            if (lockError != null)
            {
                TempData["ErrorMessage"] = lockError;
                return RedirectToAction("Details", new { id = course.maKhoaHoc });
            }


            // Normalize formatted VND price from Request.Form if needed
            string rawGiaBan = Request.Form["GiaBan"];
            if (!string.IsNullOrWhiteSpace(rawGiaBan))
            {
                string cleanGiaBan = rawGiaBan.Replace(".", "").Replace(",", "").Replace(" ", "").Replace("đ", "").Replace("₫", "").Replace("VND", "").Replace("VNĐ", "").Trim();
                if (decimal.TryParse(cleanGiaBan, out decimal parsedGiaBan))
                {
                    model.GiaBan = parsedGiaBan;
                    ModelState.Remove("GiaBan");
                }
            }

            if (string.IsNullOrWhiteSpace(model.TenKhoaHoc))
            {
                ModelState.AddModelError("TenKhoaHoc", "Vui lòng nhập tên khóa học!");
            }

            if (model.GiaBan < 0)
            {
                ModelState.AddModelError("GiaBan", "Giá bán không được là số âm!");
            }

            var chapters = model.Chapters ?? new List<CourseChapterInput>();
            var lessons = model.Lessons ?? new List<CourseLessonInput>();
            var existingChapters = db.ChuongHoc.Where(c => c.maKhoaHoc == course.maKhoaHoc).ToList();
            var existingLessons = db.BaiHoc.Where(l => (l.ChuongHoc != null && l.ChuongHoc.maKhoaHoc == course.maKhoaHoc) || (l.maChuong == null && l.maBaiHoc.StartsWith(course.maKhoaHoc + "_"))).ToList();
            foreach (var chapter in chapters)
                if (string.IsNullOrWhiteSpace(chapter.Title) || (!string.IsNullOrEmpty(chapter.Id) && !existingChapters.Any(c => c.maChuong == chapter.Id))) ModelState.AddModelError("", "Chương không hợp lệ.");
            foreach (var lesson in lessons) {
                if (string.IsNullOrWhiteSpace(lesson.Title) || (!string.IsNullOrEmpty(lesson.Id) && !existingLessons.Any(l => l.maBaiHoc == lesson.Id))) ModelState.AddModelError("", "Bài học không hợp lệ.");
                if (lesson.ChapterIndex.HasValue && (lesson.ChapterIndex.Value < 0 || lesson.ChapterIndex.Value >= chapters.Count)) ModelState.AddModelError("", "Chương của bài học không hợp lệ.");
                if (!string.IsNullOrEmpty(lesson.VideoToken) && (!CourseUploads.ContainsKey(lesson.VideoToken) || CourseUploads[lesson.VideoToken][0] != "video")) ModelState.AddModelError("", "Video không hợp lệ. Vui lòng tải lại.");
                foreach (string token in lesson.AttachmentTokens ?? new List<string>())
                    if (!CourseUploads.ContainsKey(token) || CourseUploads[token][0] != "document") ModelState.AddModelError("", "Tài liệu không hợp lệ.");
            }
            var deletedLessons = existingLessons.Where(l => !lessons.Any(input => input.Id == l.maBaiHoc)).ToList();
            if (deletedLessons.Any(l => l.TienDoBaiHoc.Any())) ModelState.AddModelError("", "Không thể xóa bài học đã có tiến độ của học viên.");
            if (actionType == "submit") KiemTraNoiDungGuiDuyet(chapters, lessons, CoVideoTaiLen);
            ValidateCategory(model.MaDanhMuc, course.maDanhMuc);
            if (!ModelState.IsValid)
            {
                model.TrangThai = course.trangThai;
                model.Categories = BuildCategoryOptions(course.maDanhMuc);
                return View("Create", model);
            }

            if (model.CoverImageFile != null && model.CoverImageFile.ContentLength > 0)
                model.AnhBia = SaveCourseFile(model.CoverImageFile, "~/Content/Uploads/CourseImages/");
            var chapterIds = new List<string>();
            for (int i = 0; i < chapters.Count; i++) {
                var input = chapters[i];
                var chapter = existingChapters.FirstOrDefault(c => c.maChuong == input.Id);
                if (chapter == null) { chapter = new ChuongHoc { maChuong = "CH" + Guid.NewGuid().ToString("N").Substring(0,18), maKhoaHoc = course.maKhoaHoc }; db.ChuongHoc.Add(chapter); }
                chapter.tenChuong = input.Title.Trim(); chapter.thuTu = i + 1; chapterIds.Add(chapter.maChuong);
            }
            int nextSequence = 1;
            for (int i = 0; i < lessons.Count; i++) {
                var input = lessons[i]; var lesson = existingLessons.FirstOrDefault(l => l.maBaiHoc == input.Id);
                if (lesson == null) {
                    while (existingLessons.Any(l => l.maBaiHoc == course.maKhoaHoc + "_" + nextSequence)) nextSequence++;
                    lesson = new BaiHoc { maBaiHoc = course.maKhoaHoc + "_" + nextSequence++ }; db.BaiHoc.Add(lesson);
                }
                lesson.maChuong = input.ChapterIndex.HasValue ? chapterIds[input.ChapterIndex.Value] : null;
                lesson.tenBaiHoc = input.Title.Trim(); lesson.thuTu = i + 1; lesson.choXemThu = input.Preview; lesson.thoiLuong = Math.Max(0,input.Duration);
                lesson.duongDanVideo = string.IsNullOrEmpty(input.VideoToken) ? null : CourseUploads[input.VideoToken][1];
                var wantedResources = (input.AttachmentTokens ?? new List<string>()).Select(token => CourseUploads[token]).ToList();
                foreach (var resource in lesson.TaiNguyen.ToList())
                    if (!wantedResources.Any(r => r[1] == resource.duongDan && r[2] == resource.tenTaiNguyen)) db.TaiNguyen.Remove(resource);
                foreach (var resource in wantedResources)
                    if (!lesson.TaiNguyen.Any(r => r.duongDan == resource[1] && r.tenTaiNguyen == resource[2]))
                        db.TaiNguyen.Add(new TaiNguyen { maTaiNguyen = "TN" + Guid.NewGuid().ToString("N").Substring(0,18), maBaiHoc = lesson.maBaiHoc, tenTaiNguyen = resource[2], duongDan = resource[1], loaiTaiNguyen = System.IO.Path.GetExtension(resource[2]) });
            }
            foreach (var lesson in deletedLessons) { db.TaiNguyen.RemoveRange(lesson.TaiNguyen.ToList()); db.BaiHoc.Remove(lesson); }
            foreach (var chapter in existingChapters.Where(c => !chapters.Any(input => input.Id == c.maChuong))) db.ChuongHoc.Remove(chapter);

            course.tenKhoaHoc = model.TenKhoaHoc.Trim();
            course.moTa = model.MoTa;
            course.moTaNgan = model.MoTaNgan;
            course.mucTieuDauRa = model.MucTieuDauRa;
            course.giaBan = model.GiaBan;
            course.capDo = model.CapDo ?? "Cơ bản";
            course.maDanhMuc = model.MaDanhMuc;
            if (!string.IsNullOrWhiteSpace(model.AnhBia))
            {
                course.anhBia = model.AnhBia.Trim();
            }

            if (actionType == "submit")
            {
                course.trangThai = "Chờ duyệt";
                CreateOrUpdateKiemDuyetRecord(course.maKhoaHoc);
            }

            db.SaveChanges();

            if (actionType == "violation")
            {
                TempData["SuccessMessage"] = "Đã lưu nội dung chỉnh sửa. Hãy mô tả những gì đã sửa và gửi cho quản trị viên xem xét.";
                TempData["DraftLoai"] = ViolationReportService.MessageFixed;
                return RedirectToAction("Index", "Violation", new { id = course.maKhoaHoc });
            }

            TempData["SuccessMessage"] = "Cập nhật thông tin khóa học thành công!";

            return RedirectToAction("Index");
        }

        // POST: Instructor/Course/SubmitForApproval/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitForApproval(string id)
        {
            int teacherId = CurrentTeacherId;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == id && k.maGiaoVien == teacherId);
            if (course == null) return HttpNotFound();
            if (course.trangThai == "Chờ duyệt" || course.trangThai == "ChoDuyet")
            {
                TempData["ErrorMessage"] = "Khóa học đang chờ duyệt. Vui lòng hủy yêu cầu duyệt trước khi chỉnh sửa.";
                return RedirectToAction("Details", new { id = course.maKhoaHoc });
            }
            string lockError = GetLockedCourseError(course, true);
            if (lockError != null)
            {
                TempData["ErrorMessage"] = lockError;
                return RedirectToAction("Details", new { id = course.maKhoaHoc });
            }


            // Kiểm tra nội dung đã lưu: có chương, có bài, mỗi bài có video còn tồn tại (CourseContentService)
            var thieu = new CourseContentService(db, Server.MapPath).KiemTraKhoaHoc(course.maKhoaHoc);
            if (thieu.Any())
            {
                TempData["ErrorMessage"] = "Chưa thể gửi duyệt khóa học '" + course.tenKhoaHoc + "': " + CourseContentService.TomTat(thieu);
                return RedirectToAction("Index");
            }

            course.trangThai = "Chờ duyệt";
            CreateOrUpdateKiemDuyetRecord(course.maKhoaHoc);
            db.SaveChanges();

            TempData["SuccessMessage"] = string.Format("Đã gửi khóa học '{0}' vào hàng đợi kiểm duyệt thành công!", course.tenKhoaHoc);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelApproval(string id)
        {
            int teacherId = CurrentTeacherId;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == id && k.maGiaoVien == teacherId);
            if (course == null) return HttpNotFound();

            if (course.trangThai != "Chờ duyệt" && course.trangThai != "ChoDuyet")
            {
                TempData["ErrorMessage"] = "Không thể hủy vì khóa học không ở trạng thái chờ duyệt.";
                return RedirectToAction("Index");
            }

            course.trangThai = "Bản nháp";

            var kd = db.KiemDuyetKhoaHoc.FirstOrDefault(x => x.maKhoaHoc == course.maKhoaHoc && x.trangThai == "Chờ duyệt");
            if (kd != null)
            {
                db.KiemDuyetKhoaHoc.Remove(kd);
            }

            db.SaveChanges();

            TempData["SuccessMessage"] = string.Format("Đã hủy yêu cầu duyệt khóa học '{0}' thành công. Bạn có thể tiếp tục chỉnh sửa.", course.tenKhoaHoc);
            return RedirectToAction("Details", new { id = id });
        }

        // POST: Instructor/Course/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id)
        {
            int teacherId = CurrentTeacherId;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == id && k.maGiaoVien == teacherId);
            if (course == null) return HttpNotFound();
            if (course.trangThai == "Chờ duyệt" || course.trangThai == "ChoDuyet")
            {
                TempData["ErrorMessage"] = "Khóa học đang chờ duyệt. Vui lòng hủy yêu cầu duyệt trước khi chỉnh sửa.";
                return RedirectToAction("Details", new { id = course.maKhoaHoc });
            }
            string lockError = GetLockedCourseError(course, true);
            if (lockError != null)
            {
                TempData["ErrorMessage"] = lockError;
                return RedirectToAction("Details", new { id = course.maKhoaHoc });
            }


            // Kiểm tra ràng buộc học viên
            int studentCount = course.DangKyKhoaHoc != null ? course.DangKyKhoaHoc.Count : 0;
            if (studentCount > 0)
            {
                TempData["ErrorMessage"] = "Không thể xóa khóa học đã có học viên đăng ký tham gia!";
                return RedirectToAction("Index");
            }

            // Xóa các bài học không phân chương
            var unchaptered = db.BaiHoc.Where(b => b.maChuong == null && b.maBaiHoc.StartsWith(course.maKhoaHoc + "_")).ToList();
            foreach (var bh in unchaptered)
            {
                if (bh.TaiNguyen != null)
                {
                    db.TaiNguyen.RemoveRange(bh.TaiNguyen);
                }
                db.BaiHoc.Remove(bh);
            }

            // Xóa các liên kết bài học & chương học
            if (course.ChuongHoc != null)
            {
                foreach (var ch in course.ChuongHoc.ToList())
                {
                    if (ch.BaiHoc != null)
                    {
                        foreach (var bh in ch.BaiHoc.ToList())
                        {
                            if (bh.TaiNguyen != null)
                            {
                                db.TaiNguyen.RemoveRange(bh.TaiNguyen);
                            }
                            db.BaiHoc.Remove(bh);
                        }
                    }
                    db.ChuongHoc.Remove(ch);
                }
            }

            db.KhoaHoc.Remove(course);
            db.SaveChanges();

            TempData["SuccessMessage"] = string.Format("Đã xóa thành công khóa học '{0}'!", course.tenKhoaHoc);
            return RedirectToAction("Index");
        }
    }
}
