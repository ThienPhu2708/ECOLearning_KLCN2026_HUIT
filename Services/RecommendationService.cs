using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Gợi ý khóa học bằng lọc cộng tác dựa trên khóa học (Item-based CF).
    /// Ma trận độ tương đồng do job Python (Recommender/) tính mỗi đêm; ở đây chỉ gọi stored procedure
    /// của Database/Migrations/005_GoiY_LocCongTac.sql để lấy top-K, không tính cosine khi có request.
    /// </summary>
    public class RecommendationService
    {
        public const string NguonLocCongTac = "LocCongTac";
        public const string NguonPhoBien = "PhoBien";

        /// <summary>
        /// Có hiển thị khóa phổ biến (bù cho đủ K / người dùng mới) hay không. Tắt: chỉ hiển thị kết quả lọc cộng tác thuần.
        /// Logic khóa phổ biến vẫn nằm trong stored procedure, chỉ lọc ở bước hiển thị; bật lại bằng cách đổi thành true.
        /// </summary>
        public static readonly bool HienThiKhoaPhoBien = false;

        private const string SessionKhoaDaXem = "GoiY_KhoaDaXem";

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public RecommendationService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        /// <summary>Đã chạy migration 005 (có đủ stored procedure gợi ý) hay chưa.</summary>
        public bool IsInstalled()
        {
            try
            {
                return db.Database.SqlQuery<int>(@"
                    SELECT CASE WHEN OBJECT_ID(N'dbo.sp_GoiY_KhoaHocChoNguoiDung', N'P') IS NOT NULL
                                 AND OBJECT_ID(N'dbo.sp_GoiY_TinhGoiYChoNguoiDung', N'P') IS NOT NULL
                           THEN 1 ELSE 0 END").FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gợi ý đã tính sẵn (job hằng đêm hoặc lần mở trang "Khóa học gợi ý" gần nhất), chỉ đọc.
        /// Dùng cho mục "Khóa học dành riêng cho bạn" ở trang chủ.
        /// </summary>
        public List<RecommendedCourseViewModel> GetSaved(int userId, int topK)
        {
            var rows = db.Database.SqlQuery<GoiYRow>(
                "EXEC dbo.sp_GoiY_KhoaHocChoNguoiDung @maHocVien, @topK",
                new SqlParameter("@maHocVien", userId),
                new SqlParameter("@topK", topK)).ToList();
            return LocHienThi(AttachCourses(rows));
        }

        /// <summary>
        /// "Vì bạn đã xem X": lấy khóa người dùng mở trang chi tiết gần nhất làm mốc, trả về top-K khóa
        /// có độ tương đồng cao nhất với khóa mốc trong ma trận course_similarity đã tính sẵn (Item-based,
        /// hồ sơ người dùng thu lại còn một khóa). Lọc bằng fn_GoiY_KhoaHocDuocGoiY như các stored procedure:
        /// bỏ khóa đã sở hữu / đã học / không còn mở bán; khóa chỉ mới xem qua vẫn được gợi ý (migration 006).
        /// Khóa xem gần nhất chưa có khóa tương tự (chưa đủ người học chung) thì lùi sang khóa xem trước đó.
        /// Trả về null khi không có khóa mốc nào dùng được.
        /// </summary>
        public GoiYTheoKhoaVuaXemViewModel GetBecauseYouViewed(int userId, int topK)
        {
            var moc = db.Database.SqlQuery<KhoaMocRow>(@"
                SELECT TOP (1) t.maKhoaHoc, kh.tenKhoaHoc
                FROM dbo.TuongTacKhoaHoc t
                INNER JOIN dbo.KhoaHoc kh ON kh.maKhoaHoc = t.maKhoaHoc
                WHERE t.maHocVien = @maHocVien
                  AND t.luotXem > 0
                  AND EXISTS (SELECT 1
                              FROM dbo.course_similarity s
                              INNER JOIN dbo.fn_GoiY_KhoaHocDuocGoiY(@maHocVien) f ON f.maKhoaHoc = s.course_id_b
                              WHERE s.course_id_a = t.maKhoaHoc)
                ORDER BY t.lanCapNhat DESC",
                new SqlParameter("@maHocVien", userId)).FirstOrDefault();
            if (moc == null)
            {
                return null;
            }

            var rows = db.Database.SqlQuery<GoiYRow>(@"
                SELECT TOP (@topK)
                    CAST(ROW_NUMBER() OVER (ORDER BY s.similarity DESC, s.course_id_b) AS INT) AS thuTu,
                    s.course_id_b AS maKhoaHoc,
                    s.similarity  AS diemDuDoan,
                    @nguon        AS nguon
                FROM dbo.course_similarity s
                INNER JOIN dbo.fn_GoiY_KhoaHocDuocGoiY(@maHocVien) f ON f.maKhoaHoc = s.course_id_b
                WHERE s.course_id_a = @maKhoaHocMoc
                ORDER BY s.similarity DESC, s.course_id_b",
                new SqlParameter("@topK", topK),
                new SqlParameter("@nguon", NguonLocCongTac),
                new SqlParameter("@maHocVien", userId),
                new SqlParameter("@maKhoaHocMoc", moc.maKhoaHoc)).ToList();

            return new GoiYTheoKhoaVuaXemViewModel
            {
                MaKhoaHocMoc = moc.maKhoaHoc,
                TenKhoaHocMoc = moc.tenKhoaHoc,
                Items = AttachCourses(rows)
            };
        }

        /// <summary>
        /// Tính lại gợi ý cho một người từ ma trận đã lưu (không tính lại cosine) và lưu kết quả.
        /// Dùng cho trang "Khóa học gợi ý".
        /// </summary>
        public RecommendationPageViewModel Compute(int userId, int topK)
        {
            var model = new RecommendationPageViewModel { DaKhoiTao = IsInstalled() };
            if (!model.DaKhoiTao)
            {
                return model;
            }

            var rows = db.Database.SqlQuery<GoiYTinhLaiRow>(
                "EXEC dbo.sp_GoiY_TinhGoiYChoNguoiDung @maHocVien, @topK",
                new SqlParameter("@maHocVien", userId),
                new SqlParameter("@topK", topK)).ToList();

            model.Items = LocHienThi(AttachCourses(rows));
            foreach (var item in model.Items)
            {
                var row = rows.First(r => r.maKhoaHoc == item.Course.maKhoaHoc);
                item.MaKhoaHocLyDo = row.maKhoaHocLyDo;
                item.TenKhoaHocLyDo = row.tenKhoaHocLyDo;
            }
            model.MaTranCapNhatLuc = rows.Select(r => r.maTranCapNhatLuc).FirstOrDefault(d => d.HasValue);
            model.SoKhoaDaTuongTac = CountInteractedCourses(userId);
            return model;
        }

        /// <summary>Số khóa học người dùng đã tương tác (đăng ký, học, đánh giá, xem...).</summary>
        public int CountInteractedCourses(int userId)
        {
            return db.Database.SqlQuery<int>(
                "SELECT COUNT(*) FROM dbo.vw_GoiY_TuongTac WHERE user_id = @maHocVien",
                new SqlParameter("@maHocVien", userId)).FirstOrDefault();
        }

        /// <summary>
        /// Ghi nhận một lượt xem trang chi tiết khóa học (tín hiệu "views" của lọc cộng tác).
        /// Mỗi khóa chỉ cộng lượt xem một lần trong một phiên đăng nhập để tải lại trang không làm tăng số liệu,
        /// nhưng lần mở nào cũng cập nhật thời điểm xem để "Vì bạn đã xem X" luôn lấy đúng khóa mở gần nhất.
        /// </summary>
        public void RecordView(System.Web.HttpSessionStateBase session, int userId, string courseId)
        {
            if (userId <= 0 || string.IsNullOrEmpty(courseId))
            {
                return;
            }

            var daXem = session[SessionKhoaDaXem] as HashSet<string> ?? new HashSet<string>();
            if (daXem.Contains(courseId))
            {
                try
                {
                    db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction,
                        "UPDATE dbo.TuongTacKhoaHoc SET lanCapNhat = GETDATE() WHERE maHocVien = @maHocVien AND maKhoaHoc = @maKhoaHoc",
                        new SqlParameter("@maHocVien", userId),
                        new SqlParameter("@maKhoaHoc", courseId));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Touch course view error: " + ex.Message);
                }
                return;
            }
            daXem.Add(courseId);

            try
            {
                db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, @"
                    MERGE dbo.TuongTacKhoaHoc WITH (HOLDLOCK) AS t
                    USING (SELECT @maHocVien AS maHocVien, @maKhoaHoc AS maKhoaHoc) AS s
                        ON t.maHocVien = s.maHocVien AND t.maKhoaHoc = s.maKhoaHoc
                    WHEN MATCHED THEN
                        UPDATE SET luotXem = t.luotXem + 1, lanCapNhat = GETDATE()
                    WHEN NOT MATCHED THEN
                        INSERT (maHocVien, maKhoaHoc, luotXem) VALUES (s.maHocVien, s.maKhoaHoc, 1);",
                    new SqlParameter("@maHocVien", userId),
                    new SqlParameter("@maKhoaHoc", courseId));
                session[SessionKhoaDaXem] = daXem;
            }
            catch (Exception ex)
            {
                // Chưa chạy migration 005 hoặc lỗi kết nối: không ảnh hưởng trang chi tiết
                System.Diagnostics.Debug.WriteLine("Record course view error: " + ex.Message);
            }
        }

        /// <summary>Bỏ khóa phổ biến khỏi danh sách hiển thị khi HienThiKhoaPhoBien = false.</summary>
        private static List<RecommendedCourseViewModel> LocHienThi(List<RecommendedCourseViewModel> items)
        {
            return HienThiKhoaPhoBien ? items : items.Where(i => i.LaLocCongTac).ToList();
        }

        /// <summary>Nạp thông tin khóa học cho các dòng gợi ý, giữ nguyên thứ tự thuật toán trả về.</summary>
        private List<RecommendedCourseViewModel> AttachCourses(IEnumerable<GoiYRow> rows)
        {
            var ordered = rows.OrderBy(r => r.thuTu).ToList();
            var ids = ordered.Select(r => r.maKhoaHoc).ToList();
            var courses = db.KhoaHoc
                .Include(k => k.NguoiDung)
                .Include(k => k.DanhMuc)
                .Where(k => ids.Contains(k.maKhoaHoc))
                .ToDictionary(k => k.maKhoaHoc);

            // Thẻ chỉ cần số lượt và điểm đánh giá trung bình: lấy bằng GROUP BY thay vì Include cả danh sách
            // đánh giá. Include danh sách con sinh truy vấn có ORDER BY trên các cột nvarchar(max), cần nhiều
            // bộ nhớ; khi máy thiếu RAM, SQL Server bắt truy vấn đó chờ cấp bộ nhớ tới 25 giây.
            var ratings = db.DanhGiaKhoaHoc
                .Where(d => ids.Contains(d.maKhoaHoc))
                .GroupBy(d => d.maKhoaHoc)
                .Select(g => new { MaKhoaHoc = g.Key, SoDanhGia = g.Count(), DiemTrungBinh = g.Average(d => (double)d.soSao) })
                .ToDictionary(x => x.MaKhoaHoc);

            return ordered
                .Where(r => courses.ContainsKey(r.maKhoaHoc))
                .Select(r => new RecommendedCourseViewModel
                {
                    Course = courses[r.maKhoaHoc],
                    SoDanhGia = ratings.ContainsKey(r.maKhoaHoc) ? ratings[r.maKhoaHoc].SoDanhGia : 0,
                    DiemDanhGia = ratings.ContainsKey(r.maKhoaHoc) ? ratings[r.maKhoaHoc].DiemTrungBinh : 0,
                    ThuTu = r.thuTu,
                    DiemDuDoan = r.diemDuDoan,
                    Nguon = r.nguon
                })
                .ToList();
        }

        /// <summary>Dòng kết quả của stored procedure (tên thuộc tính trùng tên cột).</summary>
        public class GoiYRow
        {
            public int thuTu { get; set; }
            public string maKhoaHoc { get; set; }
            public double diemDuDoan { get; set; }
            public string nguon { get; set; }
        }

        public class KhoaMocRow
        {
            public string maKhoaHoc { get; set; }
            public string tenKhoaHoc { get; set; }
        }

        public class GoiYTinhLaiRow : GoiYRow
        {
            public string maKhoaHocLyDo { get; set; }
            public string tenKhoaHocLyDo { get; set; }
            public DateTime? maTranCapNhatLuc { get; set; }
        }
    }
}
