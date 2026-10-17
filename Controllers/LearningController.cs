using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    public class LearningController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Learning/MyCourses (UC8: Khóa học của tôi)
        public ActionResult MyCourses()
        {
            int currentUserId = (Session["UserId"] as int?) ?? 0;
            var currentUser = Session["User"] as NguoiDung;
            if (currentUser == null && currentUserId > 0)
            {
                currentUser = db.NguoiDung.Find(currentUserId);
                Session["User"] = currentUser;
            }

            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("MyCourses", "Learning") });
            }

            int userId = currentUser.maNguoiDung;

            var enrollments = db.DangKyKhoaHoc
                .Where(d => d.maHocVien == userId)
                .Include(d => d.KhoaHoc)
                .Include(d => d.KhoaHoc.DanhMuc)
                .Include(d => d.KhoaHoc.NguoiDung)
                // Không Include ChuongHoc / ChuongHoc.BaiHoc: nạp lồng nhiều danh sách con sinh truy vấn cần nhiều
                // bộ nhớ, máy thiếu RAM thì SQL Server bắt chờ tới 25 giây. Chương / bài học được nạp khi dùng tới
                // bằng các truy vấn nhỏ trong vòng lặp bên dưới.
                .OrderByDescending(d => d.ngayDangKy)
                .ToList();

            var viewModel = new MyCoursesViewModel();

            foreach (var dk in enrollments)
            {
                var course = dk.KhoaHoc;
                if (course == null) continue;

                // Lấy tất cả bài học của khóa học (cả có chương và không phân chương)
                var chapterLessons = (course.ChuongHoc != null)
                    ? course.ChuongHoc.SelectMany(c => c.BaiHoc != null ? c.BaiHoc.OrderBy(b => b.thuTu) : Enumerable.Empty<BaiHoc>()).ToList()
                    : new List<BaiHoc>();

                var unchaptered = db.BaiHoc
                    .Where(b => b.maChuong == null && b.maBaiHoc.StartsWith(course.maKhoaHoc + "_"))
                    .OrderBy(b => b.thuTu)
                    .ToList();

                var allLessons = chapterLessons.Concat(unchaptered).ToList();
                int totalLessons = allLessons.Count;

                // Lấy các bài học đã hoàn thành của học viên
                var lessonIds = allLessons.Select(b => b.maBaiHoc).ToList();
                var completedRecords = db.TienDoBaiHoc
                    .Where(t => t.maHocVien == userId && lessonIds.Contains(t.maBaiHoc) && t.daHoanThanh == true)
                    .ToList();

                int completedCount = completedRecords.Select(r => r.maBaiHoc).Distinct().Count();
                int progressPercent = (totalLessons > 0) ? (int)Math.Floor((double)completedCount / totalLessons * 100) : 0;
                if (progressPercent > 100) progressPercent = 100;

                // Xác định bài học tiếp theo (bài đầu tiên chưa hoàn thành)
                var completedIdSet = new HashSet<string>(completedRecords.Select(c => c.maBaiHoc));
                var nextLesson = allLessons.FirstOrDefault(b => !completedIdSet.Contains(b.maBaiHoc)) ?? allLessons.FirstOrDefault();

                // Ngày hoàn thành (lấy ngày hoàn thành bài học cuối cùng nếu đã đạt 100%)
                DateTime? completionDate = null;
                if (progressPercent >= 100 && completedRecords.Any())
                {
                    completionDate = completedRecords.Max(c => c.ngayHoanThanh ?? c.lanTruyCapGanNhat);
                }

                var item = new MyCourseItemViewModel
                {
                    MaKhoaHoc = course.maKhoaHoc,
                    TenKhoaHoc = course.tenKhoaHoc,
                    AnhBia = string.IsNullOrEmpty(course.anhBia) ? "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&auto=format&fit=crop&q=80" : course.anhBia,
                    TenDanhMuc = course.DanhMuc != null ? course.DanhMuc.tenDanhMuc : "Lập trình Web & CNTT",
                    TenGiangVien = course.NguoiDung != null ? course.NguoiDung.hoTen : "Giảng viên E-Learning",
                    NgayDangKy = dk.ngayDangKy,
                    NgayHoanThanh = completionDate,
                    TongSoBaiHoc = totalLessons,
                    SoBaiDaHoanThanh = completedCount,
                    PhanTramTienDo = progressPercent,
                    TenBaiHocTiepTheo = nextLesson != null ? nextLesson.tenBaiHoc : "Bắt đầu học bài đầu tiên",
                    MaBaiHocTiepTheo = nextLesson != null ? nextLesson.maBaiHoc : null
                };

                if (item.DaHoanThanh)
                {
                    viewModel.CompletedCourses.Add(item);
                }
                else
                {
                    viewModel.OngoingCourses.Add(item);
                }
            }

            var quizStates = new Dictionary<string, QuizStudentRow>();
            var quizService = new QuizService(db);
            if (quizService.Installed())
            {
                foreach (var completedCourse in viewModel.CompletedCourses)
                {
                    quizService.CloseExpired(completedCourse.MaKhoaHoc, userId);
                    var state = quizService.Student(completedCourse.MaKhoaHoc, userId);
                    if (state != null) quizStates[completedCourse.MaKhoaHoc] = state;
                }
            }
            ViewBag.CompletedQuizStates = quizStates;
            return View(viewModel);
        }

        // GET: Learning/Favorites — trang quản lý "Khóa học đã tim" (trang riêng, tách khỏi Khóa học đã đăng ký)
        public ActionResult Favorites()
        {
            int userId = (Session["UserId"] as int?) ?? 0;
            if (userId <= 0)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Favorites", "Learning") });
            }

            var model = new FavoritesPageViewModel();
            try
            {
                model.Items = new FavoriteService(db).GetFavorites(userId);
            }
            catch (Exception ex)
            {
                // Chưa chạy migration 005 (chưa có bảng TuongTacKhoaHoc)
                System.Diagnostics.Debug.WriteLine("Load favorites page error: " + ex.Message);
                model.DaKhoiTao = false;
            }

            return View(model);
        }

        private NguoiDung GetCurrentStudent(string courseId = null)
        {
            int currentUserId = (Session["UserId"] as int?) ?? 0;
            var currentUser = currentUserId > 0 ? db.NguoiDung.Find(currentUserId) : null;
            if (currentUser == null && User.Identity.IsAuthenticated)
                currentUser = db.NguoiDung.FirstOrDefault(u => u.email == User.Identity.Name);
            if (currentUser == null || currentUser.trangThai == false) return null;
            Session["UserId"] = currentUser.maNguoiDung;
            Session["User"] = currentUser;
            return currentUser;
        }
        // GET: Learning/Study (UC5: Không gian học tập Video Player & Cây đề cương)
        public ActionResult Study(string courseId, string lessonId = null)
        {
            if (string.IsNullOrEmpty(courseId))
            {
                var defaultCourse = db.KhoaHoc.FirstOrDefault();
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

            // Khóa học bị khóa vĩnh viễn do vi phạm: học viên không học được nữa (giáo viên chủ khóa học và Admin vẫn xem được).
            var viewer = GetCurrentStudent();
            if (viewer == null)
                return RedirectToAction("Login", "Account", new { returnUrl = Request.RawUrl });
            bool isPreview = viewer.maNguoiDung == course.maGiaoVien || viewer.vaiTro == "Admin";
            if (!isPreview && !db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == courseId && d.maHocVien == viewer.maNguoiDung))
                return new HttpStatusCodeResult(403, "Bạn chưa đăng ký khóa học này.");
            ViewBag.IsPreview = isPreview;

            // Khóa học tạm khóa thì học viên đã mua vẫn học bình thường.
            if (course.trangThai == ECOLearning.Services.CourseStatus.KhoaVinhVien)
            {
                int viewerId = (Session["UserId"] as int?) ?? 0;
                bool isOwnerOrAdmin = string.Equals(Session["Role"]?.ToString(), "Admin", StringComparison.OrdinalIgnoreCase)
                    || (viewerId > 0 && course.maGiaoVien == viewerId);
                if (!isOwnerOrAdmin)
                {
                    TempData["ErrorMessage"] = "Khóa học \"" + course.tenKhoaHoc + "\" đã bị khóa vĩnh viễn do vi phạm quy định nên không thể tiếp tục học.";
                    return RedirectToAction("MyCourses");
                }
            }

            var chapters = db.ChuongHoc
                .Where(c => c.maKhoaHoc == courseId)
                .OrderBy(c => c.thuTu)
                .Include(c => c.BaiHoc)
                .ToList();

            var allLessons = chapters
                .SelectMany(c => c.BaiHoc != null ? c.BaiHoc.OrderBy(b => b.thuTu).AsEnumerable() : Enumerable.Empty<BaiHoc>())
                .ToList();

            // Bài học không phân chương (maChuong == null)
            var unchapteredLessons = db.BaiHoc
                .Where(b => b.maChuong == null && b.maBaiHoc.StartsWith(courseId + "_"))
                .OrderBy(b => b.thuTu)
                .ToList();

            allLessons.AddRange(unchapteredLessons);

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
            var currentUser = GetCurrentStudent(courseId);
            List<string> completedLessonIds = new List<string>();

            if (currentUser != null)
            {
                var courseLessonIds = allLessons.Select(b => b.maBaiHoc).ToList();
                completedLessonIds = db.TienDoBaiHoc
                    .Where(t => t.maHocVien == currentUser.maNguoiDung && t.daHoanThanh == true && courseLessonIds.Contains(t.maBaiHoc))
                    .Select(t => t.maBaiHoc)
                    .Distinct()
                    .ToList();
            }

            bool hasSubmittedReview = false;
            if (currentUser != null)
            {
                hasSubmittedReview = db.DanhGiaKhoaHoc.Any(d => d.maHocVien == currentUser.maNguoiDung && d.maKhoaHoc == courseId);
            }

            // Tài liệu đính kèm bài học hiện tại (bảng TaiNguyen)
            List<TaiNguyen> activeLessonDocuments = new List<TaiNguyen>();
            if (activeLesson != null)
            {
                activeLessonDocuments = db.TaiNguyen
                    .Where(t => t.maBaiHoc == activeLesson.maBaiHoc)
                    .ToList();
            }

            ViewBag.Course = course;
            ViewBag.Chapters = chapters;
            ViewBag.AllLessons = allLessons;
            ViewBag.ActiveLesson = activeLesson;
            ViewBag.CompletedLessonIds = completedLessonIds;
            ViewBag.UnchapteredLessons = unchapteredLessons;
            ViewBag.HasSubmittedReview = hasSubmittedReview;
            var studyQuiz = new QuizService(db);
            var studyQuizState = currentUser != null && studyQuiz.Installed() ? studyQuiz.Student(courseId, currentUser.maNguoiDung) : null;
            ViewBag.CanReview = studyQuizState != null && studyQuizState.LessonsDone && studyQuizState.Passed;

            // Nút báo cáo vi phạm: chỉ cho học viên đã đăng ký và không phải giáo viên của khóa học
            ViewBag.CanReportViolation = currentUser != null
                && course.maGiaoVien != currentUser.maNguoiDung
                && db.DangKyKhoaHoc.Any(d => d.maHocVien == currentUser.maNguoiDung && d.maKhoaHoc == courseId);
            ViewBag.ActiveLessonDocuments = activeLessonDocuments;

            return View();
        }

        // POST: Learning/UpdateProgress (Lưu tiến độ & đánh dấu hoàn thành nếu >= 80%)
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UpdateProgress(string courseId, string lessonId, float? durationWatched, float? totalDuration)
        {
            var currentUser = GetCurrentStudent(courseId);
            if (currentUser == null)
            {
                return new HttpStatusCodeResult(403, "Vui lòng đăng nhập để lưu tiến độ.");
            }
            int studentId = currentUser.maNguoiDung;
            if (!db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == courseId && d.maHocVien == studentId))
                return new HttpStatusCodeResult(403, "Bạn chưa đăng ký khóa học này.");

            if (string.IsNullOrEmpty(lessonId))
            {
                return Json(new { success = false, message = "Thiếu thông tin bài học" });
            }

            float watched = durationWatched ?? 0f;
            float total = totalDuration ?? 0f;
            bool lessonBelongsToCourse = db.BaiHoc.Any(b => b.maBaiHoc == lessonId &&
                ((b.ChuongHoc != null && b.ChuongHoc.maKhoaHoc == courseId) ||
                 (b.maChuong == null && b.maBaiHoc.StartsWith(courseId + "_"))));
            if (!lessonBelongsToCourse || watched < 0 || total <= 0 || float.IsNaN(watched) || float.IsInfinity(watched) || float.IsNaN(total) || float.IsInfinity(total))
                return new HttpStatusCodeResult(400, "Thông tin tiến độ hoặc bài học không hợp lệ.");

            // Hoàn thành nếu đã xem >= 80% thời lượng bài học (hoặc video chạy đến cuối / còn <= 3 giây)
            bool isDone = (total > 0 && (watched / total) >= 0.795f) || (total > 0 && watched >= (total - 3));

            var existingProgress = db.TienDoBaiHoc
                .FirstOrDefault(t => t.maHocVien == studentId && t.maBaiHoc == lessonId);

            if (existingProgress == null)
            {
                existingProgress = new TienDoBaiHoc
                {
                    maHocVien = studentId,
                    maBaiHoc = lessonId,
                    thoiGianXemDungLai = (int)Math.Round(watched),
                    daHoanThanh = isDone,
                    lanTruyCapGanNhat = DateTime.Now,
                    ngayHoanThanh = isDone ? (DateTime?)DateTime.Now : null
                };
                db.TienDoBaiHoc.Add(existingProgress);
            }
            else
            {
                existingProgress.thoiGianXemDungLai = (int)Math.Round(watched);
                existingProgress.lanTruyCapGanNhat = DateTime.Now;
                if (isDone && (existingProgress.daHoanThanh != true))
                {
                    existingProgress.daHoanThanh = true;
                    existingProgress.ngayHoanThanh = DateTime.Now;
                }
            }

            db.SaveChanges();

            return Json(new { success = true, completed = existingProgress.daHoanThanh, studentId = studentId, isDone = isDone });
        }

        // POST: Learning/ResetProgress (Đặt lại toàn bộ tiến độ của khóa học để kiểm thử lại)
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ResetProgress(string courseId)
        {
            var currentUser = GetCurrentStudent(courseId);
            if (currentUser == null || !db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == courseId && d.maHocVien == currentUser.maNguoiDung))
                return new HttpStatusCodeResult(403);
            if (currentUser != null && !string.IsNullOrEmpty(courseId))
            {
                var lessonIds = db.BaiHoc.Where(b => (b.maChuong == null && b.maBaiHoc.StartsWith(courseId + "_")) || (b.ChuongHoc != null && b.ChuongHoc.maKhoaHoc == courseId)).Select(b => b.maBaiHoc).ToList();
                var records = db.TienDoBaiHoc.Where(t => t.maHocVien == currentUser.maNguoiDung && lessonIds.Contains(t.maBaiHoc)).ToList();
                if (records.Any())
                {
                    db.TienDoBaiHoc.RemoveRange(records);
                    db.SaveChanges();
                }
            }
            return Json(new { success = true });
        }

        // GET: Learning/Certificate (UC8: Xem & Tải chứng chỉ PDF)
        public ActionResult Certificate(string courseId)
        {
            var currentUser = Session["User"] as NguoiDung;
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrEmpty(courseId))
            {
                return RedirectToAction("MyCourses");
            }

            var course = db.KhoaHoc
                .Include(k => k.DanhMuc)
                .Include(k => k.NguoiDung)
                .FirstOrDefault(k => k.maKhoaHoc == courseId);

            if (course == null)
            {
                return HttpNotFound();
            }

            bool isEnrolled = db.DangKyKhoaHoc.Any(d => d.maHocVien == currentUser.maNguoiDung && d.maKhoaHoc == courseId);
            if (!isEnrolled)
            {
                TempData["ErrorMessage"] = "Bạn chưa đăng ký khóa học này nên chưa thể nhận chứng chỉ.";
                return RedirectToAction("MyCourses");
            }

            var quizService = new QuizService(db);
            if (!quizService.Installed())
            {
                TempData["ErrorMessage"] = "Chưa khởi tạo chức năng quiz và chứng chỉ.";
                return RedirectToAction("MyCourses");
            }
            var certificateState = quizService.Student(courseId, currentUser.maNguoiDung);
            if (certificateState == null || !certificateState.Passed || !certificateState.IssuedAt.HasValue)
            {
                TempData["QuizMessage"] = "Học đủ bài, đạt ngưỡng quiz rồi bấm Nhận chứng chỉ để được cấp tự động.";
                return RedirectToAction("Index", "Quiz", new { courseId });
            }
            ViewBag.Course = course;
            ViewBag.Student = currentUser;
            ViewBag.CourseId = courseId;
            ViewBag.CertificateCode = certificateState.Code;
            ViewBag.CertificateIssuedAt = certificateState.IssuedAt;

            return View();
        }

        // GET: Learning/StreamVideo (Phát video nội bộ có hỗ trợ HTTP Range Request để tua/streaming mượt,
        // vì IIS Express không phục vụ đúng Range cho static file, gây hiện tượng có tiếng nhưng hình đứng)
        public ActionResult StreamVideo(string file)
        {
            if (string.IsNullOrEmpty(file) || file.Contains("..") || file.Contains("/") || file.Contains("\\"))
            {
                return new HttpStatusCodeResult(400, "Tên file không hợp lệ");
            }

            string videoDir = Server.MapPath("~/assets/uploads/videos/");
            string fullPath = Path.Combine(videoDir, file);

            if (!fullPath.StartsWith(videoDir, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
            {
                return HttpNotFound();
            }

            string ext = Path.GetExtension(fullPath).ToLowerInvariant();
            string contentType;
            switch (ext)
            {
                case ".webm": contentType = "video/webm"; break;
                case ".mov": contentType = "video/quicktime"; break;
                case ".mkv": contentType = "video/x-matroska"; break;
                case ".ogv": contentType = "video/ogg"; break;
                default: contentType = "video/mp4"; break;
            }

            long totalLength = new FileInfo(fullPath).Length;
            long start = 0;
            long end = totalLength - 1;

            Response.Clear();
            Response.BufferOutput = false;
            Response.AddHeader("Accept-Ranges", "bytes");
            Response.ContentType = contentType;

            string rangeHeader = Request.Headers["Range"];
            if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
            {
                string[] parts = rangeHeader.Substring("bytes=".Length).Split('-');
                if (!long.TryParse(parts[0], out start)) start = 0;
                if (parts.Length > 1 && parts[1].Length > 0 && long.TryParse(parts[1], out long parsedEnd))
                {
                    end = parsedEnd;
                }
                if (end >= totalLength) end = totalLength - 1;

                if (start < 0 || start > end)
                {
                    Response.StatusCode = 416;
                    Response.AddHeader("Content-Range", "bytes */" + totalLength);
                    return new EmptyResult();
                }

                Response.StatusCode = 206;
                Response.StatusDescription = "Partial Content";
                Response.AddHeader("Content-Range", string.Format("bytes {0}-{1}/{2}", start, end, totalLength));
            }

            long contentLength = end - start + 1;
            Response.AddHeader("Content-Length", contentLength.ToString());

            const int bufferSize = 65536;
            byte[] buffer = new byte[bufferSize];

            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                stream.Seek(start, SeekOrigin.Begin);
                long bytesRemaining = contentLength;

                while (bytesRemaining > 0 && Response.IsClientConnected)
                {
                    int bytesToRead = (int)Math.Min(bufferSize, bytesRemaining);
                    int bytesRead = stream.Read(buffer, 0, bytesToRead);
                    if (bytesRead == 0) break;

                    try
                    {
                        Response.OutputStream.Write(buffer, 0, bytesRead);
                    }
                    catch (System.Web.HttpException ex) when (
                        ex.ErrorCode == unchecked((int)0x800703E3) || !Response.IsClientConnected)
                    {
                        // The browser can cancel a video request after the connection check
                        // (for example when seeking or leaving the page).
                        break;
                    }
                    bytesRemaining -= bytesRead;
                }
            }

            return new EmptyResult();
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
