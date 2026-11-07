using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Nghiệp vụ học tập dùng chung cho website (LearningController) và API ứng dụng di động (Controllers/Api):
    /// danh sách bài của khóa, đồng bộ thời lượng video, lưu tiến độ xem, tính % hoàn thành khóa.
    /// </summary>
    public class LearningService
    {
        /// <summary>Bài được tính hoàn thành khi xem ≥ 80% (làm tròn 79,5%) hoặc còn ≤ 3 giây là hết video.</summary>
        public const float TyLeHoanThanh = 0.795f;

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public LearningService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        /// <summary>Đã ghi danh (mua / đăng ký miễn phí) khóa học chưa.</summary>
        public bool DaGhiDanh(int studentId, string courseId)
            => db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == courseId && d.maHocVien == studentId);

        /// <summary>
        /// Các chương của khóa (theo thứ tự) kèm bài; bài không phân chương có mã dạng "{maKhoaHoc}_..." trả riêng.
        /// Cùng quy ước với trang Study.
        /// </summary>
        public List<ChuongHoc> LayChuong(string courseId, out List<BaiHoc> baiKhongChuong)
        {
            var chuong = db.ChuongHoc
                .Where(c => c.maKhoaHoc == courseId)
                .OrderBy(c => c.thuTu)
                .Include(c => c.BaiHoc)
                .ToList();
            baiKhongChuong = db.BaiHoc
                .Where(b => b.maChuong == null && b.maBaiHoc.StartsWith(courseId + "_"))
                .OrderBy(b => b.thuTu)
                .ToList();
            return chuong;
        }

        /// <summary>Mọi bài của khóa theo thứ tự học: bài trong chương trước, bài không phân chương sau.</summary>
        public List<BaiHoc> LayBaiHoc(string courseId)
        {
            List<BaiHoc> baiKhongChuong;
            var chuong = LayChuong(courseId, out baiKhongChuong);
            var bai = chuong
                .SelectMany(c => c.BaiHoc != null ? c.BaiHoc.OrderBy(b => b.thuTu).AsEnumerable() : Enumerable.Empty<BaiHoc>())
                .ToList();
            bai.AddRange(baiKhongChuong);
            return bai;
        }

        /// <summary>
        /// Đặt BaiHoc.thoiLuong theo độ dài thật của file video (đọc từ file, Services/VideoDuration).
        /// Video không đọc được độ dài (YouTube, WebM, MKV...) thì dùng độ dài trình phát báo về nếu có.
        /// Trả về độ dài thật của bài đầu tiên (null nếu không xác định được).
        /// </summary>
        public int? DongBoThoiLuong(IEnumerable<BaiHoc> lessons, float doDaiTrinhPhat = 0f)
        {
            int? dauTien = null;
            bool coThayDoi = false, laBaiDau = true;
            foreach (var b in lessons)
            {
                int? thuc = VideoDuration.LaySoGiay(b.duongDanVideo);
                if (!thuc.HasValue && doDaiTrinhPhat > 0 && laBaiDau
                    && VideoStorageService.LaVideoTaiLen(b.duongDanVideo))
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
        /// Lưu vị trí đang xem của một bài và đánh dấu hoàn thành khi đã xem đủ (người gọi đã kiểm tra ghi danh).
        /// Độ dài video lấy từ chính file trên máy chủ, không tin số trình phát gửi lên. Cập nhật BaiHoc.thoiLuong TRƯỚC khi
        /// ghi tiến độ: trigger trg_TienDoBaiHoc_CapNhatHoanThanh tự đánh dấu hoàn thành khi thoiGianXemDungLai >= 80% thoiLuong,
        /// nên thoiLuong sai (nhỏ hơn video thật) sẽ làm bài được tính xong khi mới xem một phần.
        /// </summary>
        public KetQuaLuuTienDo LuuTienDo(int studentId, string courseId, string lessonId, float watched, float total)
        {
            var lesson = db.BaiHoc.FirstOrDefault(b => b.maBaiHoc == lessonId &&
                ((b.ChuongHoc != null && b.ChuongHoc.maKhoaHoc == courseId) ||
                 (b.maChuong == null && b.maBaiHoc.StartsWith(courseId + "_"))));
            if (lesson == null || watched < 0 || float.IsNaN(watched) || float.IsInfinity(watched) || float.IsNaN(total) || float.IsInfinity(total))
                return KetQuaLuuTienDo.Loi("Thông tin tiến độ hoặc bài học không hợp lệ.");

            int? thoiLuongThat = DongBoThoiLuong(new[] { lesson }, total);
            if (thoiLuongThat.HasValue) total = thoiLuongThat.Value;
            else if (total <= 0) total = lesson.thoiLuong ?? 0;
            if (total <= 0)
                return KetQuaLuuTienDo.Loi("Thông tin tiến độ hoặc bài học không hợp lệ.");
            if (watched > total) watched = total;

            bool isDone = (watched / total) >= TyLeHoanThanh || watched >= (total - 3);

            var progress = db.TienDoBaiHoc.FirstOrDefault(t => t.maHocVien == studentId && t.maBaiHoc == lessonId);
            bool wasCompleted = progress != null && progress.daHoanThanh == true;
            if (progress == null)
            {
                progress = new TienDoBaiHoc
                {
                    maHocVien = studentId,
                    maBaiHoc = lessonId,
                    thoiGianXemDungLai = (int)Math.Round(watched),
                    daHoanThanh = isDone,
                    lanTruyCapGanNhat = DateTime.Now,
                    ngayHoanThanh = isDone ? (DateTime?)DateTime.Now : null
                };
                db.TienDoBaiHoc.Add(progress);
            }
            else
            {
                progress.thoiGianXemDungLai = (int)Math.Round(watched);
                progress.lanTruyCapGanNhat = DateTime.Now;
                if (isDone && progress.daHoanThanh != true)
                {
                    progress.daHoanThanh = true;
                    progress.ngayHoanThanh = DateTime.Now;
                }
            }

            double phanTram;
            using (var transaction = db.Database.BeginTransaction())
            {
                var quiz = new QuizService(db);
                quiz.LockCourseConfiguration(courseId);
                db.SaveChanges();
                phanTram = CapNhatTienDoKhoaHoc(studentId, courseId);
                if (!wasCompleted && progress.daHoanThanh == true && DaGhiDanh(studentId, courseId))
                    quiz.IssueWithoutQuizAfterCompletion(courseId, studentId);
                transaction.Commit();
            }

            return new KetQuaLuuTienDo
            {
                Success = true,
                Completed = progress.daHoanThanh == true,
                Position = progress.thoiGianXemDungLai ?? 0,
                IsDone = isDone,
                PhanTramKhoaHoc = phanTram
            };
        }

        /// <summary>
        /// Tính lại % hoàn thành của khóa (TienDoHocTap) và lần truy cập gần nhất. Trigger trg_TienDoBaiHoc_CapNhatTienDoKhoaHoc
        /// chỉ tính bài nằm trong chương, nên khóa có bài không phân chương trước đây luôn hiện 0% ở trang giáo viên.
        /// Trả về % mới.
        /// </summary>
        public double CapNhatTienDoKhoaHoc(int studentId, string courseId)
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
            if (ProtectedCompletions(studentId).Contains(courseId)) percent = 100;

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
            return percent;
        }

        /// <summary>
        /// Tiến độ của nhiều khóa cùng lúc (trang "Khóa học của tôi"): số bài, số bài xong, % (làm tròn xuống: chỉ học đủ
        /// mọi bài mới là 100%), bài học tiếp theo (bài đầu tiên chưa xong), ngày hoàn thành, lần học gần nhất.
        /// </summary>
        public Dictionary<string, TienDoKhoaHoc> TienDoCacKhoa(int studentId, IList<string> courseIds)
        {
            var ketQua = courseIds.Distinct().ToDictionary(id => id, id => new TienDoKhoaHoc { MaKhoaHoc = id });
            if (ketQua.Count == 0) return ketQua;
            var ids = ketQua.Keys.ToList();

            var bai = db.BaiHoc
                .Where(b => b.maChuong != null && ids.Contains(b.ChuongHoc.maKhoaHoc))
                .Select(b => new { MaKhoaHoc = b.ChuongHoc.maKhoaHoc, b.maBaiHoc, b.tenBaiHoc, ThuTuChuong = b.ChuongHoc.thuTu, b.maChuong, b.thuTu })
                .ToList()
                .OrderBy(b => b.ThuTuChuong ?? int.MaxValue).ThenBy(b => b.maChuong).ThenBy(b => b.thuTu ?? int.MaxValue)
                .Select(b => new { b.MaKhoaHoc, b.maBaiHoc, b.tenBaiHoc })
                .ToList();
            var baiKhongChuong = db.BaiHoc
                .Where(b => b.maChuong == null)
                .Select(b => new { b.maBaiHoc, b.tenBaiHoc, b.thuTu })
                .ToList()
                .OrderBy(b => b.thuTu ?? int.MaxValue);
            foreach (var b in baiKhongChuong)
            {
                string maKhoa = ids.FirstOrDefault(id => b.maBaiHoc.StartsWith(id + "_"));
                if (maKhoa != null) bai.Add(new { MaKhoaHoc = maKhoa, b.maBaiHoc, b.tenBaiHoc });
            }

            var maBai = bai.Select(b => b.maBaiHoc).ToList();
            var tienDo = db.TienDoBaiHoc
                .Where(t => t.maHocVien == studentId && maBai.Contains(t.maBaiHoc))
                .Select(t => new { t.maBaiHoc, t.daHoanThanh, t.ngayHoanThanh, t.lanTruyCapGanNhat })
                .ToList();
            var ngayXong = tienDo.Where(t => t.daHoanThanh == true)
                .GroupBy(t => t.maBaiHoc)
                .ToDictionary(g => g.Key, g => g.Max(t => t.ngayHoanThanh ?? t.lanTruyCapGanNhat));
            var lanHoc = tienDo.GroupBy(t => t.maBaiHoc).ToDictionary(g => g.Key, g => g.Max(t => t.lanTruyCapGanNhat));

            foreach (var nhom in bai.GroupBy(b => b.MaKhoaHoc))
            {
                var td = ketQua[nhom.Key];
                var ds = nhom.ToList();
                td.TongSoBai = ds.Count;
                td.SoBaiXong = ds.Count(b => ngayXong.ContainsKey(b.maBaiHoc));
                td.PhanTram = td.TongSoBai > 0 ? Math.Min(100, (int)Math.Floor(100.0 * td.SoBaiXong / td.TongSoBai)) : 0;
                var tiepTheo = ds.FirstOrDefault(b => !ngayXong.ContainsKey(b.maBaiHoc)) ?? ds.FirstOrDefault();
                td.MaBaiTiepTheo = tiepTheo?.maBaiHoc;
                td.TenBaiTiepTheo = tiepTheo?.tenBaiHoc;
                if (td.PhanTram >= 100 && td.SoBaiXong > 0)
                    td.NgayHoanThanh = ds.Where(b => ngayXong.ContainsKey(b.maBaiHoc)).Max(b => ngayXong[b.maBaiHoc]);
                td.LanHocGanNhat = ds.Where(b => lanHoc.ContainsKey(b.maBaiHoc)).Select(b => lanHoc[b.maBaiHoc]).DefaultIfEmpty(null).Max();
            }
            foreach (string completedCourse in ProtectedCompletions(studentId)) {
                TienDoKhoaHoc frozen;
                if (ketQua.TryGetValue(completedCourse, out frozen)) {
                    frozen.DaHoanThanhTruoc = true;
                    frozen.PhanTram = 100;
                }
            }
            return ketQua;
        }
        public HashSet<string> ProtectedCompletions(int studentId) {
            if (db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.CourseCompletionProtection') IS NULL THEN 0 ELSE 1 END").Single() == 0) return new HashSet<string>();
            return new HashSet<string>(db.Database.SqlQuery<string>("SELECT CourseId FROM dbo.CourseCompletionProtection WHERE StudentId=@p0", studentId).ToList());
        }
    }

    public class KetQuaLuuTienDo
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public bool Completed { get; set; }
        public int Position { get; set; }
        public bool IsDone { get; set; }
        public double PhanTramKhoaHoc { get; set; }

        public static KetQuaLuuTienDo Loi(string message) => new KetQuaLuuTienDo { Success = false, Message = message };
    }

    public class TienDoKhoaHoc
    {
        public string MaKhoaHoc { get; set; }
        public int TongSoBai { get; set; }
        public int SoBaiXong { get; set; }
        public int PhanTram { get; set; }
        public string MaBaiTiepTheo { get; set; }
        public string TenBaiTiepTheo { get; set; }
        public DateTime? NgayHoanThanh { get; set; }
        public DateTime? LanHocGanNhat { get; set; }
        public bool DaHoanThanhTruoc { get; set; }
        public bool DaHoanThanh => DaHoanThanhTruoc || (TongSoBai > 0 && SoBaiXong >= TongSoBai);
    }
}
