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

        // GET: Learning/MyCourses?tab=hoan-thanh&trang=2 (UC8: Khóa học của tôi), 12 khóa mỗi trang trong từng tab
        public ActionResult MyCourses(string tab, int trang = 1)
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
                return RedirectToAction("Login", "Account", new { returnUrl = Request.RawUrl });
            }

            int userId = currentUser.maNguoiDung;

            var enrollments = db.DangKyKhoaHoc
                .Where(d => d.maHocVien == userId)
                .Include(d => d.KhoaHoc)
                .Include(d => d.KhoaHoc.DanhMuc)
                .Include(d => d.KhoaHoc.NguoiDung)
                .OrderByDescending(d => d.ngayDangKy)
                .ToList();
            var courseIds = enrollments.Where(d => d.KhoaHoc != null).Select(d => d.maKhoaHoc).Distinct().ToList();

            // Phải biết tiến độ của MỌI khóa mới chia được hai tab và đếm số khóa mỗi tab, nên tiến độ được tính bằng
            // 3 truy vấn chỉ lấy cột cần dùng cho cả trang, thay vì 3 truy vấn cho từng khóa như trước.
            // Không Include ChuongHoc.BaiHoc: nạp lồng nhiều danh sách con tốn bộ nhớ, máy ít RAM bị SQL Server bắt chờ.
            var baiHoc = db.BaiHoc
                .Where(b => b.maChuong != null && courseIds.Contains(b.ChuongHoc.maKhoaHoc))
                .Select(b => new { MaKhoaHoc = b.ChuongHoc.maKhoaHoc, b.maBaiHoc, b.tenBaiHoc, ThuTuChuong = b.ChuongHoc.thuTu, b.maChuong, b.thuTu })
                .ToList()
                .OrderBy(b => b.ThuTuChuong ?? int.MaxValue).ThenBy(b => b.maChuong).ThenBy(b => b.thuTu ?? int.MaxValue)
                .Select(b => new BaiHocTomTat { MaKhoaHoc = b.MaKhoaHoc, MaBaiHoc = b.maBaiHoc, TenBaiHoc = b.tenBaiHoc })
                .ToList();

            // Bài không phân chương có mã dạng "{maKhoaHoc}_..." (cùng quy ước với trang Study), xếp sau bài trong chương
            var baiKhongChuong = db.BaiHoc
                .Where(b => b.maChuong == null)
                .Select(b => new { b.maBaiHoc, b.tenBaiHoc, b.thuTu })
                .ToList()
                .OrderBy(b => b.thuTu ?? int.MaxValue);
            foreach (var b in baiKhongChuong)
            {
                string maKhoa = courseIds.FirstOrDefault(id => b.maBaiHoc.StartsWith(id + "_"));
                if (maKhoa != null)
                {
                    baiHoc.Add(new BaiHocTomTat { MaKhoaHoc = maKhoa, MaBaiHoc = b.maBaiHoc, TenBaiHoc = b.tenBaiHoc });
                }
            }
            var baiTheoKhoa = baiHoc.GroupBy(b => b.MaKhoaHoc).ToDictionary(g => g.Key, g => g.ToList());

            // Ngày hoàn thành của từng bài học viên đã học xong
            var ngayXongTheoBai = db.TienDoBaiHoc
                .Where(t => t.maHocVien == userId && t.daHoanThanh == true)
                .Select(t => new { t.maBaiHoc, t.ngayHoanThanh, t.lanTruyCapGanNhat })
                .ToList()
                .GroupBy(t => t.maBaiHoc)
                .ToDictionary(g => g.Key, g => g.Max(t => t.ngayHoanThanh ?? t.lanTruyCapGanNhat));

            var ongoing = new List<MyCourseItemViewModel>();
            var completed = new List<MyCourseItemViewModel>();

            foreach (var dk in enrollments)
            {
                var course = dk.KhoaHoc;
                if (course == null) continue;

                List<BaiHocTomTat> allLessons;
                if (!baiTheoKhoa.TryGetValue(course.maKhoaHoc, out allLessons))
                {
                    allLessons = new List<BaiHocTomTat>();
                }
                int totalLessons = allLessons.Count;

                // Các bài học đã hoàn thành của học viên trong khóa này (mỗi bài đếm một lần: ngayXongTheoBai gom theo mã bài).
                // Làm tròn xuống để chỉ khi học đủ mọi bài mới thành 100% (vd. 199/200 bài là 99%, không phải 100%).
                var completedLessons = allLessons.Where(b => ngayXongTheoBai.ContainsKey(b.MaBaiHoc)).ToList();
                int completedCount = completedLessons.Count;
                int progressPercent = (totalLessons > 0) ? (int)Math.Floor((double)completedCount / totalLessons * 100) : 0;
                if (progressPercent > 100) progressPercent = 100;

                // Xác định bài học tiếp theo (bài đầu tiên chưa hoàn thành)
                var nextLesson = allLessons.FirstOrDefault(b => !ngayXongTheoBai.ContainsKey(b.MaBaiHoc)) ?? allLessons.FirstOrDefault();

                // Ngày hoàn thành (lấy ngày hoàn thành bài học cuối cùng nếu đã đạt 100%)
                DateTime? completionDate = null;
                if (progressPercent >= 100 && completedCount > 0)
                {
                    completionDate = completedLessons.Max(b => ngayXongTheoBai[b.MaBaiHoc]);
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
                    TenBaiHocTiepTheo = nextLesson != null ? nextLesson.TenBaiHoc : "Bắt đầu học bài đầu tiên",
                    MaBaiHocTiepTheo = nextLesson != null ? nextLesson.MaBaiHoc : null
                };

                if (item.DaHoanThanh)
                {
                    completed.Add(item);
                }
                else
                {
                    ongoing.Add(item);
                }
            }

            // Mỗi tab phân trang riêng (link tab không mang số trang nên đổi tab luôn về trang 1)
            var viewModel = new MyCoursesViewModel
            {
                Tab = tab == MyCoursesViewModel.TabHoanThanh ? MyCoursesViewModel.TabHoanThanh : MyCoursesViewModel.TabDangHoc,
                SoDangHoc = ongoing.Count,
                SoHoanThanh = completed.Count
            };
            var dangXem = viewModel.LaTabHoanThanh ? completed : ongoing;
            viewModel.PhanTrang = new PhanTrangViewModel(trang, dangXem.Count) { Neo = "ds-khoa-hoc" };
            viewModel.Items = viewModel.PhanTrang.CatTrang(dangXem);

            // Trạng thái quiz / chứng chỉ của các khóa đã học đủ bài: chỉ lấy cho các khóa của trang đang xem
            var quizStates = new Dictionary<string, QuizStudentRow>();
            var quizService = new QuizService(db);
            if (viewModel.LaTabHoanThanh && quizService.Installed())
            {
                foreach (var completedCourse in viewModel.Items)
                {
                    quizService.CloseExpired(completedCourse.MaKhoaHoc, userId);
                    var state = quizService.Student(completedCourse.MaKhoaHoc, userId);
                    if (state != null) quizStates[completedCourse.MaKhoaHoc] = state;
                }
            }
            ViewBag.CompletedQuizStates = quizStates;

            return View(viewModel);
        }

        /// <summary>Bài học rút gọn để tính tiến độ ở trang Khóa học của tôi.</summary>
        private class BaiHocTomTat
        {
            public string MaKhoaHoc { get; set; }
            public string MaBaiHoc { get; set; }
            public string TenBaiHoc { get; set; }
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

            // Thời lượng bài học theo độ dài thật của video (dữ liệu cũ nhập tay có thể lệch nhiều so với video đã tải lên)
            DongBoThoiLuong(allLessons);

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

            // Vị trí xem dở của bài hiện tại: trang Study tua tới đó khi mở lại bài (giáo viên / Admin xem thử thì không lưu)
            int resumeSeconds = 0;
            if (currentUser != null && activeLesson != null && !isPreview)
            {
                resumeSeconds = db.TienDoBaiHoc
                    .Where(t => t.maHocVien == currentUser.maNguoiDung && t.maBaiHoc == activeLesson.maBaiHoc)
                    .OrderByDescending(t => t.lanTruyCapGanNhat)
                    .Select(t => t.thoiGianXemDungLai)
                    .FirstOrDefault() ?? 0;
            }
            ViewBag.ResumeSeconds = Math.Max(0, resumeSeconds);

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

        // POST: Learning/UpdateProgress: lưu vị trí đang xem (gọi định kỳ khi xem, khi tạm dừng và khi rời trang)
        // và đánh dấu hoàn thành khi đã xem >= 80% thời lượng video.
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
            var lesson = db.BaiHoc.FirstOrDefault(b => b.maBaiHoc == lessonId &&
                ((b.ChuongHoc != null && b.ChuongHoc.maKhoaHoc == courseId) ||
                 (b.maChuong == null && b.maBaiHoc.StartsWith(courseId + "_"))));
            if (lesson == null || watched < 0 || float.IsNaN(watched) || float.IsInfinity(watched) || float.IsNaN(total) || float.IsInfinity(total))
                return new HttpStatusCodeResult(400, "Thông tin tiến độ hoặc bài học không hợp lệ.");

            // Độ dài video lấy từ chính file trên máy chủ, không tin số trình duyệt gửi lên. Cập nhật BaiHoc.thoiLuong TRƯỚC khi
            // ghi tiến độ: trigger trg_TienDoBaiHoc_CapNhatHoanThanh tự đánh dấu hoàn thành khi thoiGianXemDungLai >= 80% thoiLuong,
            // nên thoiLuong sai (nhỏ hơn video thật) sẽ làm bài được tính xong khi mới xem một phần.
            int? thoiLuongThat = DongBoThoiLuong(new[] { lesson }, total);
            if (thoiLuongThat.HasValue) total = thoiLuongThat.Value;
            else if (total <= 0) total = lesson.thoiLuong ?? 0;
            if (total <= 0)
                return new HttpStatusCodeResult(400, "Thông tin tiến độ hoặc bài học không hợp lệ.");
            if (watched > total) watched = total;

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
            CapNhatTienDoKhoaHoc(studentId, courseId);

            return Json(new { success = true, completed = existingProgress.daHoanThanh == true, position = existingProgress.thoiGianXemDungLai, isDone = isDone });
        }

        /// <summary>
        /// Đặt BaiHoc.thoiLuong theo độ dài thật của file video (đọc từ file, Services/VideoDuration).
        /// Video không đọc được độ dài (YouTube, WebM, MKV...) thì dùng độ dài trình phát báo về nếu có.
        /// Trả về độ dài thật của bài đầu tiên (null nếu không xác định được).
        /// </summary>
        private int? DongBoThoiLuong(IEnumerable<BaiHoc> lessons, float doDaiTrinhPhat = 0f)
        {
            int? dauTien = null;
            bool coThayDoi = false, laBaiDau = true;
            foreach (var b in lessons)
            {
                int? thuc = VideoDuration.LaySoGiay(b.duongDanVideo);
                if (!thuc.HasValue && doDaiTrinhPhat > 0 && laBaiDau
                    && !string.IsNullOrEmpty(b.duongDanVideo) && b.duongDanVideo.StartsWith(VideoDuration.ThuMucVideo, StringComparison.OrdinalIgnoreCase))
                {
                    thuc = (int)Math.Round(doDaiTrinhPhat);
                }
                if (laBaiDau) dauTien = thuc;
                laBaiDau = false;
                if (thuc.HasValue && thuc.Value > 0 && Math.Abs((b.thoiLuong ?? 0) - thuc.Value) > 1)
                {
                    b.thoiLuong = thuc.Value;
                    coThayDoi = true;
                }
            }
            if (coThayDoi) db.SaveChanges();
            return dauTien;
        }

        /// <summary>
        /// Tính lại % hoàn thành của khóa (TienDoHocTap) và lần truy cập gần nhất. Trigger trg_TienDoBaiHoc_CapNhatTienDoKhoaHoc
        /// chỉ tính bài nằm trong chương, nên khóa có bài không phân chương trước đây luôn hiện 0% ở trang giáo viên.
        /// </summary>
        private void CapNhatTienDoKhoaHoc(int studentId, string courseId)
        {
            var lessonIds = db.BaiHoc
                .Where(b => (b.ChuongHoc != null && b.ChuongHoc.maKhoaHoc == courseId) || (b.maChuong == null && b.maBaiHoc.StartsWith(courseId + "_")))
                .Select(b => b.maBaiHoc)
                .ToList();
            int done = lessonIds.Count == 0 ? 0 : db.TienDoBaiHoc
                .Where(t => t.maHocVien == studentId && t.daHoanThanh == true && lessonIds.Contains(t.maBaiHoc))
                .Select(t => t.maBaiHoc)
                .Distinct()
                .Count();
            double percent = lessonIds.Count == 0 ? 0 : Math.Round(100.0 * done / lessonIds.Count, 2);

            var row = db.TienDoHocTap.FirstOrDefault(t => t.maHocVien == studentId && t.maKhoaHoc == courseId);
            if (row == null)
            {
                row = new TienDoHocTap
                {
                    maTienDo = "TD_" + Guid.NewGuid().ToString("N").Substring(0, 17),
                    maHocVien = studentId,
                    maKhoaHoc = courseId
                };
                db.TienDoHocTap.Add(row);
            }
            row.phanTramHoanThanh = percent;
            row.lanTruyCapGanNhat = DateTime.Now;
            db.SaveChanges();
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
