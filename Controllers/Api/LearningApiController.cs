using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.Services.Api;

namespace ECOLearning.Controllers.Api
{
    /// <summary>
    /// Nhóm G – Học tập và tiến độ (Task_Tuan10.docx mục 6.7 và luồng 6.10).
    /// Tiến độ dùng chung LearningService với website: học trên app hay web đều ra cùng % và tích xanh.
    /// </summary>
    [RoutePrefix("api/v1"), ApiAuthorize]
    public class LearningApiController : ApiControllerBase
    {
        public class ProgressRequest
        {
            public double PositionSeconds { get; set; }
            public double DurationSeconds { get; set; }
        }

        // G1. GET /api/v1/my-courses?status=in-progress|completed&page=
        [HttpGet, Route("my-courses")]
        public ActionResult MyCourses(string status = "in-progress", int page = 1, int pageSize = 12)
        {
            page = ChuanHoaTrang(page);
            pageSize = ChuanHoaKichThuoc(pageSize);
            int userId = CurrentUserId;

            var dangKy = Db.DangKyKhoaHoc.Where(d => d.maHocVien == userId && d.KhoaHoc != null)
                .OrderByDescending(d => d.ngayDangKy)
                .Select(d => new { d.maKhoaHoc, d.ngayDangKy, d.KhoaHoc.trangThai })
                .ToList()
                .GroupBy(d => d.maKhoaHoc).Select(g => g.First()).ToList();
            var tienDo = new LearningService(Db).TienDoCacKhoa(userId, dangKy.Select(d => d.maKhoaHoc).ToList());

            bool xemHoanThanh = status == "completed";
            var loc = dangKy.Where(d => tienDo[d.maKhoaHoc].DaHoanThanh == xemHoanThanh).ToList();
            var trang = loc.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var the = new ApiCourseMapper(Db, AbsoluteUrl).Map(trang.Select(d => d.maKhoaHoc)).ToDictionary(c => c.Id);

            var quiz = new QuizService(Db);
            bool coQuiz = quiz.Installed();
            var items = trang.Where(d => the.ContainsKey(d.maKhoaHoc)).Select(d =>
            {
                var c = the[d.maKhoaHoc];
                var td = tienDo[d.maKhoaHoc];
                var cauHinh = coQuiz ? quiz.Config(d.maKhoaHoc) : null;
                var trangThaiQuiz = coQuiz && td.DaHoanThanh ? quiz.Student(d.maKhoaHoc, userId) : null;
                return new
                {
                    c.Id, c.Title, c.CategoryId, c.Category, c.Instructor, c.InstructorAvatar, c.ImageUrl, c.Level,
                    c.Rating, c.ReviewCount, c.Price, c.OriginalPrice, c.IsFree, c.LessonsCount, c.DurationSeconds, c.StudentsCount, c.Tag,
                    progress = td.PhanTram,
                    completedLessons = td.SoBaiXong,
                    isCompleted = td.DaHoanThanh,
                    lastLessonId = td.MaBaiTiepTheo,
                    nextLessonTitle = td.TenBaiTiepTheo,
                    lastAccessAt = ApiFormat.Date(td.LanHocGanNhat),
                    completedAt = ApiFormat.Date(td.NgayHoanThanh),
                    enrolledAt = ApiFormat.Date(d.ngayDangKy),
                    isLocked = d.trangThai == CourseStatus.KhoaVinhVien,
                    hasQuiz = cauHinh != null && cauHinh.Published,
                    quizPassed = trangThaiQuiz != null && trangThaiQuiz.Passed,
                    hasCertificate = trangThaiQuiz != null && trangThaiQuiz.IssuedAt.HasValue
                };
            }).ToList();

            int soHoanThanh = dangKy.Count(d => tienDo[d.maKhoaHoc].DaHoanThanh);
            return Ok(new
            {
                items,
                page,
                pageSize,
                total = loc.Count,
                totalPages = (int)Math.Ceiling(loc.Count / (double)pageSize),
                counts = new { inProgress = dangKy.Count - soHoanThanh, completed = soHoanThanh }
            });
        }

        /// <summary>Kiểm tra quyền học: đã ghi danh và khóa chưa bị khóa vĩnh viễn. Trả null nếu được học.</summary>
        private ActionResult KiemTraQuyenHoc(string courseId, out KhoaHoc khoa)
        {
            khoa = Db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == courseId);
            if (khoa == null) return NotFoundJson("Không tìm thấy khóa học.", "COURSE_NOT_FOUND");
            if (!new LearningService(Db).DaGhiDanh(CurrentUserId, courseId))
                return Forbidden("Bạn chưa đăng ký khóa học này.", "NOT_ENROLLED");
            if (khoa.trangThai == CourseStatus.KhoaVinhVien)
                return Forbidden("Khóa học \"" + khoa.tenKhoaHoc + "\" đã bị khóa vĩnh viễn do vi phạm quy định nên không thể tiếp tục học.", "COURSE_LOCKED");
            return null;
        }

        private class TienDoBai
        {
            public bool Xong { get; set; }
            public int ViTri { get; set; }
            public DateTime? LanHoc { get; set; }
        }

        private Dictionary<string, TienDoBai> TienDoCacBai(IEnumerable<string> maBai)
        {
            var ds = maBai.ToList();
            return Db.TienDoBaiHoc
                .Where(t => t.maHocVien == CurrentUserId && ds.Contains(t.maBaiHoc))
                .Select(t => new { t.maBaiHoc, t.daHoanThanh, t.thoiGianXemDungLai, t.lanTruyCapGanNhat })
                .ToList()
                .GroupBy(t => t.maBaiHoc)
                .ToDictionary(g => g.Key, g =>
                {
                    var moiNhat = g.OrderByDescending(t => t.lanTruyCapGanNhat).First();
                    return new TienDoBai
                    {
                        Xong = g.Any(t => t.daHoanThanh == true),
                        ViTri = moiNhat.thoiGianXemDungLai ?? 0,
                        LanHoc = moiNhat.lanTruyCapGanNhat
                    };
                });
        }

        // G2. GET /api/v1/learning/{courseId}
        [HttpGet, Route("learning/{courseId}")]
        public ActionResult Overview(string courseId)
        {
            KhoaHoc khoa;
            var loi = KiemTraQuyenHoc(courseId, out khoa);
            if (loi != null) return loi;

            var learning = new LearningService(Db);
            List<BaiHoc> baiKhongChuong;
            var chuong = learning.LayChuong(courseId, out baiKhongChuong);
            var tatCa = chuong.SelectMany(c => (c.BaiHoc ?? new List<BaiHoc>()).OrderBy(b => b.thuTu)).Concat(baiKhongChuong).ToList();
            // Thời lượng theo độ dài thật của file video (giống trang Study)
            learning.DongBoThoiLuong(tatCa);
            var tienDo = TienDoCacBai(tatCa.Select(b => b.maBaiHoc));

            Func<BaiHoc, object> bai = b =>
            {
                TienDoBai td;
                tienDo.TryGetValue(b.maBaiHoc, out td);
                return new
                {
                    id = b.maBaiHoc,
                    title = b.tenBaiHoc,
                    durationSeconds = b.thoiLuong ?? 0,
                    isPreview = b.choXemThu == true,
                    isCompleted = td != null && td.Xong,
                    lastPositionSeconds = td?.ViTri ?? 0
                };
            };
            var dsChuong = chuong.Select(c => new
            {
                id = c.maChuong,
                title = c.tenChuong,
                lessons = (c.BaiHoc ?? new List<BaiHoc>()).OrderBy(b => b.thuTu).Select(bai).ToList()
            }).ToList();
            if (baiKhongChuong.Count > 0) dsChuong.Add(new { id = "_", title = "Bài học khác", lessons = baiKhongChuong.Select(bai).ToList() });

            // Bài nên mở: bài học gần nhất chưa xong → bài đầu tiên chưa xong → bài đầu tiên
            var ganNhat = tatCa.Where(b => tienDo.ContainsKey(b.maBaiHoc) && !tienDo[b.maBaiHoc].Xong)
                .OrderByDescending(b => tienDo[b.maBaiHoc].LanHoc).FirstOrDefault();
            var baiMo = ganNhat ?? tatCa.FirstOrDefault(b => !(tienDo.ContainsKey(b.maBaiHoc) && tienDo[b.maBaiHoc].Xong)) ?? tatCa.FirstOrDefault();
            int soXong = tatCa.Count(b => tienDo.ContainsKey(b.maBaiHoc) && tienDo[b.maBaiHoc].Xong);

            var quiz = new QuizService(Db);
            var cauHinh = quiz.Installed() ? quiz.Config(courseId) : null;
            var trangThai = quiz.Installed() ? quiz.Student(courseId, CurrentUserId) : null;
            return Ok(new
            {
                courseId = khoa.maKhoaHoc,
                title = khoa.tenKhoaHoc,
                progress = learning.ProtectedCompletions(CurrentUserId).Contains(courseId) ? 100 : tatCa.Count == 0 ? 0 : Math.Min(100, (int)Math.Floor(100.0 * soXong / tatCa.Count)),
                completedLessons = soXong,
                totalLessons = tatCa.Count,
                currentLessonId = baiMo?.maBaiHoc,
                chapters = dsChuong,
                quiz = new
                {
                    available = cauHinh != null && cauHinh.Published,
                    passed = trangThai != null && trangThai.Passed
                },
                certificate = new { issued = trangThai != null && trangThai.IssuedAt.HasValue },
                canReview = trangThai != null && trangThai.LessonsDone && trangThai.Passed
            });
        }

        // G3. GET /api/v1/learning/{courseId}/lessons/{lessonId}
        [HttpGet, Route("learning/{courseId}/lessons/{lessonId}")]
        public ActionResult Lesson(string courseId, string lessonId)
        {
            KhoaHoc khoa;
            var loi = KiemTraQuyenHoc(courseId, out khoa);
            if (loi != null) return loi;

            var learning = new LearningService(Db);
            var tatCa = learning.LayBaiHoc(courseId);
            int viTri = tatCa.FindIndex(b => b.maBaiHoc == lessonId);
            if (viTri < 0) return NotFoundJson("Không tìm thấy bài học.", "LESSON_NOT_FOUND");
            var bai = tatCa[viTri];
            learning.DongBoThoiLuong(new[] { bai });

            TienDoBai td;
            TienDoCacBai(new[] { bai.maBaiHoc }).TryGetValue(bai.maBaiHoc, out td);
            string url, loai;
            VideoCuaBai(bai.duongDanVideo, out url, out loai);
            return Ok(new
            {
                id = bai.maBaiHoc,
                title = bai.tenBaiHoc,
                videoUrl = url,
                videoType = loai,
                durationSeconds = bai.thoiLuong ?? 0,
                lastPositionSeconds = td?.ViTri ?? 0,
                isCompleted = td != null && td.Xong,
                previousLessonId = viTri > 0 ? tatCa[viTri - 1].maBaiHoc : null,
                nextLessonId = viTri < tatCa.Count - 1 ? tatCa[viTri + 1].maBaiHoc : null
            });
        }

        // G4. POST /api/v1/learning/{courseId}/lessons/{lessonId}/progress
        [HttpPost, Route("learning/{courseId}/lessons/{lessonId}/progress")]
        public ActionResult Progress(string courseId, string lessonId)
        {
            KhoaHoc khoa;
            var loi = KiemTraQuyenHoc(courseId, out khoa);
            if (loi != null) return loi;

            var req = Body<ProgressRequest>();
            var kq = new LearningService(Db).LuuTienDo(CurrentUserId, courseId, lessonId, (float)req.PositionSeconds, (float)req.DurationSeconds);
            if (!kq.Success) return BadRequest(kq.Message, "INVALID_PROGRESS");
            return Ok(new
            {
                lessonCompleted = kq.Completed,
                positionSeconds = kq.Position,
                courseProgress = kq.PhanTramKhoaHoc
            });
        }
    }
}
