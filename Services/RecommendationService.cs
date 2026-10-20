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
        /// Có hiển thị khóa phổ biến hay không. Bật: khi lọc cộng tác chưa đủ dữ liệu (học viên mới, khóa đã xem
        /// chưa có khóa tương tự) thì bù khóa phổ biến, thẻ gắn nhãn "Phổ biến" để phân biệt với kết quả lọc cộng tác.
        /// Tắt: chỉ hiển thị kết quả lọc cộng tác thuần (mục gợi ý có thể trống).
        /// </summary>
        public static readonly bool HienThiKhoaPhoBien = true;

        /// <summary>Lọc cộng tác trả về ít hơn số này thì bù khóa phổ biến cho đủ một hàng thẻ.</summary>
        public const int SoGoiYToiThieu = 5;

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
        /// Mục "Khóa học dành riêng cho bạn" ở trang chủ. Chọn nguồn theo lượng dữ liệu đã biết về học viên:
        ///  1. "Vì bạn đã xem X": khóa tương tự khóa xem gần nhất (đọc thẳng ma trận course_similarity, đổi ngay sau mỗi lần xem).
        ///  2. Gợi ý tổng hợp từ toàn bộ lịch sử, đã tính sẵn (job hằng đêm / lần mở trang "Khóa học gợi ý").
        ///  3. "Vì bạn quan tâm …": khóa tương tự các khóa mồi của lĩnh vực học viên đã chọn (cold-start).
        ///  4. Khóa phổ biến (chưa đủ dữ liệu cho lọc cộng tác).
        /// Ở 1–3, lọc cộng tác trả về ít hơn SoGoiYToiThieu khóa thì bù khóa phổ biến cùng lĩnh vực, rồi toàn hệ thống.
        /// </summary>
        public GoiYTrangChuViewModel GetForHome(int userId, int topK, bool dungSoThich)
        {
            var model = new GoiYTrangChuViewModel();
            int toiThieu = Math.Min(SoGoiYToiThieu, topK);

            // 1. Vì bạn đã xem X
            var moc = LayKhoaMoc(userId);
            if (moc != null)
            {
                var rows = LayKhoaTuongTu(userId, new[] { moc.maKhoaHoc }, topK);
                BuPhoBien(userId, rows, toiThieu, new[] { moc.maDanhMuc });
                model.CheDo = CheDoGoiY.TheoKhoaVuaXem;
                model.MaKhoaHocMoc = moc.maKhoaHoc;
                model.TenKhoaHocMoc = moc.tenKhoaHoc;
                model.Items = LocHienThi(AttachCourses(rows));
                model.DaCoTuongTac = true;
                return model;
            }

            // 2. Gợi ý tổng hợp đã tính sẵn (chỉ phần lọc cộng tác; phần bù do BuPhoBien quyết định)
            var saved = LayGoiYDaLuu(userId, topK);
            if (saved.Count > 0)
            {
                BuPhoBien(userId, saved, toiThieu, LayDanhMucDaTuongTac(userId));
                model.CheDo = CheDoGoiY.TongHop;
                model.Items = LocHienThi(AttachCourses(saved));
                model.DaCoTuongTac = true;
                return model;
            }

            // 3. Lĩnh vực quan tâm đã chọn (migration 007)
            if (dungSoThich)
            {
                var linhVuc = new InterestService(db).GetSelected(userId);
                if (linhVuc.Count > 0)
                {
                    var maDanhMuc = linhVuc.Select(l => l.MaDanhMuc).ToList();
                    var rows = LayTheoSoThich(userId, maDanhMuc, topK);
                    BuPhoBien(userId, rows, toiThieu, maDanhMuc);
                    if (rows.Count > 0)
                    {
                        model.CheDo = CheDoGoiY.TheoSoThich;
                        model.LinhVucQuanTam = linhVuc;
                        model.Items = LocHienThi(AttachCourses(rows));
                        return model;
                    }
                }
            }

            // 4. Khóa phổ biến: ưu tiên lĩnh vực học viên đã tương tác (nếu có)
            var daTuongTac = LayDanhMucDaTuongTac(userId);
            model.DaCoTuongTac = daTuongTac.Count > 0 || CountInteractedCourses(userId) > 0;
            var phoBien = new List<GoiYRow>();
            BuPhoBien(userId, phoBien, topK, daTuongTac);
            model.Items = LocHienThi(AttachCourses(phoBien));
            model.CheDo = model.Items.Count > 0 ? CheDoGoiY.PhoBien : CheDoGoiY.Trong;
            return model;
        }

        /// <summary>
        /// Tính lại gợi ý cho một người từ ma trận đã lưu (không tính lại cosine) và lưu kết quả.
        /// Dùng cho trang "Khóa học gợi ý". Chưa có lịch sử nhưng đã chọn lĩnh vực quan tâm thì gợi ý theo khóa mồi.
        /// </summary>
        public RecommendationPageViewModel Compute(int userId, int topK, bool dungSoThich)
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

            if (dungSoThich && !rows.Any(r => r.nguon == NguonLocCongTac))
            {
                var linhVuc = new InterestService(db).GetSelected(userId);
                if (linhVuc.Count > 0)
                {
                    var maDanhMuc = linhVuc.Select(l => l.MaDanhMuc).ToList();
                    var theoSoThich = LayTheoSoThich(userId, maDanhMuc, topK);
                    BuPhoBien(userId, theoSoThich, topK, maDanhMuc);
                    if (theoSoThich.Count > 0)
                    {
                        model.LinhVucQuanTam = linhVuc;
                        model.Items = LocHienThi(AttachCourses(theoSoThich));
                        model.MaTranCapNhatLuc = rows.Select(r => r.maTranCapNhatLuc).FirstOrDefault(d => d.HasValue);
                        model.SoKhoaDaTuongTac = CountInteractedCourses(userId);
                        return model;
                    }
                }
            }

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
                GhiLuotXem(userId, courseId, false);
                return;
            }
            daXem.Add(courseId);
            if (GhiLuotXem(userId, courseId, true))
            {
                session[SessionKhoaDaXem] = daXem;
            }
        }

        /// <summary>
        /// Ghi lượt xem không cần Session (API ứng dụng di động): [tangLuotXem] = false chỉ cập nhật thời điểm xem gần nhất.
        /// Người gọi tự chống cộng lặp (API dùng bộ nhớ đệm theo người dùng + khóa học).
        /// </summary>
        public bool GhiLuotXem(int userId, string courseId, bool tangLuotXem)
        {
            if (userId <= 0 || string.IsNullOrEmpty(courseId)) return false;
            try
            {
                if (!tangLuotXem)
                {
                    db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction,
                        "UPDATE dbo.TuongTacKhoaHoc SET lanCapNhat = GETDATE() WHERE maHocVien = @maHocVien AND maKhoaHoc = @maKhoaHoc",
                        new SqlParameter("@maHocVien", userId),
                        new SqlParameter("@maKhoaHoc", courseId));
                    return true;
                }
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
                return true;
            }
            catch (Exception ex)
            {
                // Chưa chạy migration 005 hoặc lỗi kết nối: không ảnh hưởng trang chi tiết
                System.Diagnostics.Debug.WriteLine("Record course view error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Ghi một lượt click thẻ khóa học (tín hiệu "clicks" của lọc cộng tác, trọng số trong GoiY_TrongSo).
        /// Không đổi lanCapNhat: thời điểm xem do lượt mở trang chi tiết quyết định.
        /// </summary>
        public bool GhiLuotClick(int userId, string courseId)
        {
            if (userId <= 0 || string.IsNullOrEmpty(courseId)) return false;
            try
            {
                db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, @"
                    MERGE dbo.TuongTacKhoaHoc WITH (HOLDLOCK) AS t
                    USING (SELECT @maHocVien AS maHocVien, @maKhoaHoc AS maKhoaHoc) AS s
                        ON t.maHocVien = s.maHocVien AND t.maKhoaHoc = s.maKhoaHoc
                    WHEN MATCHED THEN
                        UPDATE SET luotClick = t.luotClick + 1
                    WHEN NOT MATCHED THEN
                        INSERT (maHocVien, maKhoaHoc, luotClick) VALUES (s.maHocVien, s.maKhoaHoc, 1);",
                    new SqlParameter("@maHocVien", userId),
                    new SqlParameter("@maKhoaHoc", courseId));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Record course click error: " + ex.Message);
                return false;
            }
        }

        // ------------------------------------------------------------------ các bước lấy dữ liệu

        /// <summary>
        /// Khóa mốc của "Vì bạn đã xem X": khóa xem gần nhất (lanCapNhat) còn ít nhất một khóa tương tự được phép gợi ý.
        /// Khóa xem gần nhất chưa có khóa tương tự (chưa đủ người học chung) thì lùi sang khóa xem trước đó.
        /// </summary>
        private KhoaMocRow LayKhoaMoc(int userId)
        {
            return db.Database.SqlQuery<KhoaMocRow>(@"
                SELECT TOP (1) t.maKhoaHoc, kh.tenKhoaHoc, kh.maDanhMuc
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
        }

        /// <summary>
        /// Khóa tương tự một hoặc nhiều khóa mốc trong ma trận course_similarity đã tính sẵn:
        /// điểm = Σ sim(mốc, j) (công thức dự đoán của Item-based CF với điểm tương tác của mỗi khóa mốc = 1).
        /// Lọc bằng fn_GoiY_KhoaHocDuocGoiY: bỏ khóa đã sở hữu / đã học / không còn mở bán.
        /// </summary>
        private List<GoiYRow> LayKhoaTuongTu(int userId, IList<string> khoaMoc, int topK)
        {
            if (khoaMoc.Count == 0)
            {
                return new List<GoiYRow>();
            }
            var thamSo = new List<SqlParameter>
            {
                new SqlParameter("@topK", topK),
                new SqlParameter("@maHocVien", userId)
            };
            string danhSachMoc = ThamSoDanhSach("@moc", khoaMoc, thamSo);

            return db.Database.SqlQuery<DiemRow>(@"
                SELECT TOP (@topK) s.course_id_b AS maKhoaHoc, SUM(s.similarity) AS diem
                FROM dbo.course_similarity s
                INNER JOIN dbo.fn_GoiY_KhoaHocDuocGoiY(@maHocVien) f ON f.maKhoaHoc = s.course_id_b
                WHERE s.course_id_a IN (" + danhSachMoc + @")
                GROUP BY s.course_id_b
                ORDER BY SUM(s.similarity) DESC, s.course_id_b",
                thamSo.ToArray()).ToList()
                .Select((r, i) => new GoiYRow { thuTu = i + 1, maKhoaHoc = r.maKhoaHoc, diemDuDoan = r.diem, nguon = NguonLocCongTac })
                .ToList();
        }

        /// <summary>
        /// Cold-start theo lĩnh vực quan tâm: mỗi lĩnh vực lấy khóa nổi bật nhất (nhiều học viên quan tâm nhất,
        /// hòa thì đánh giá cao hơn) có mặt trong ma trận làm khóa mồi, rồi lấy khóa tương tự các khóa mồi.
        /// </summary>
        private List<GoiYRow> LayTheoSoThich(int userId, IList<string> maDanhMuc, int topK)
        {
            if (maDanhMuc.Count == 0)
            {
                return new List<GoiYRow>();
            }
            var thamSo = new List<SqlParameter> { new SqlParameter("@maHocVien", userId) };
            string danhSachDanhMuc = ThamSoDanhSach("@dm", maDanhMuc, thamSo);

            var khoaMoi = db.Database.SqlQuery<string>(@"
                SELECT x.maKhoaHoc
                FROM dbo.DanhMuc dm
                CROSS APPLY (
                    SELECT TOP (1) f.maKhoaHoc
                    FROM dbo.fn_GoiY_KhoaHocDuocGoiY(@maHocVien) f
                    INNER JOIN dbo.KhoaHoc kh ON kh.maKhoaHoc = f.maKhoaHoc
                    WHERE kh.maDanhMuc = dm.maDanhMuc
                      AND EXISTS (SELECT 1 FROM dbo.course_similarity s WHERE s.course_id_a = f.maKhoaHoc)
                    ORDER BY f.soNguoi DESC, f.ratingTB DESC, f.maKhoaHoc
                ) x
                WHERE dm.maDanhMuc IN (" + danhSachDanhMuc + ")",
                thamSo.ToArray()).ToList();

            return LayKhoaTuongTu(userId, khoaMoi, topK);
        }

        /// <summary>Phần lọc cộng tác của gợi ý đã lưu trong KhoaHocKhuyenNghi (không lấy phần bù phổ biến của SP).</summary>
        private List<GoiYRow> LayGoiYDaLuu(int userId, int topK)
        {
            return db.Database.SqlQuery<GoiYRow>(
                "EXEC dbo.sp_GoiY_KhoaHocChoNguoiDung @maHocVien, @topK",
                new SqlParameter("@maHocVien", userId),
                new SqlParameter("@topK", topK)).ToList()
                .Where(r => r.nguon == NguonLocCongTac)
                .OrderBy(r => r.thuTu)
                .ToList();
        }

        /// <summary>Danh mục của các khóa học viên đã tương tác (xem, học, đánh giá...), nhiều tương tác trước.</summary>
        private List<string> LayDanhMucDaTuongTac(int userId)
        {
            return db.Database.SqlQuery<string>(@"
                SELECT kh.maDanhMuc
                FROM dbo.vw_GoiY_TuongTac v
                INNER JOIN dbo.KhoaHoc kh ON kh.maKhoaHoc = v.course_id
                WHERE v.user_id = @maHocVien AND kh.maDanhMuc IS NOT NULL
                GROUP BY kh.maDanhMuc
                ORDER BY COUNT(*) DESC, kh.maDanhMuc",
                new SqlParameter("@maHocVien", userId)).ToList();
        }

        /// <summary>
        /// Bù khóa phổ biến vào danh sách cho đủ <paramref name="soLuong"/> khóa: trước hết trong các danh mục
        /// ưu tiên, sau đó toàn hệ thống. Khóa phổ biến = nhiều học viên tương tác nhất, hòa thì đánh giá trung bình
        /// cao hơn (cùng thứ tự với phần bù của stored procedure). Không lấy lại khóa đã có trong danh sách.
        /// </summary>
        private void BuPhoBien(int userId, List<GoiYRow> rows, int soLuong, IEnumerable<string> danhMucUuTien)
        {
            if (!HienThiKhoaPhoBien || rows.Count >= soLuong)
            {
                return;
            }
            var danhMuc = (danhMucUuTien ?? Enumerable.Empty<string>()).Where(d => !string.IsNullOrEmpty(d)).Distinct().ToList();
            if (danhMuc.Count > 0)
            {
                ThemPhoBien(userId, rows, soLuong - rows.Count, danhMuc);
            }
            if (rows.Count < soLuong)
            {
                ThemPhoBien(userId, rows, soLuong - rows.Count, null);
            }
        }

        private void ThemPhoBien(int userId, List<GoiYRow> rows, int soLuong, IList<string> danhMuc)
        {
            var thamSo = new List<SqlParameter>
            {
                new SqlParameter("@soLuong", soLuong),
                new SqlParameter("@maHocVien", userId)
            };
            string dieuKien = "";
            if (danhMuc != null && danhMuc.Count > 0)
            {
                dieuKien += " AND kh.maDanhMuc IN (" + ThamSoDanhSach("@dm", danhMuc, thamSo) + ")";
            }
            if (rows.Count > 0)
            {
                dieuKien += " AND f.maKhoaHoc NOT IN (" + ThamSoDanhSach("@co", rows.Select(r => r.maKhoaHoc).ToList(), thamSo) + ")";
            }

            var them = db.Database.SqlQuery<DiemRow>(@"
                SELECT TOP (@soLuong) f.maKhoaHoc, CAST(f.soNguoi AS FLOAT) AS diem
                FROM dbo.fn_GoiY_KhoaHocDuocGoiY(@maHocVien) f
                INNER JOIN dbo.KhoaHoc kh ON kh.maKhoaHoc = f.maKhoaHoc
                WHERE 1 = 1" + dieuKien + @"
                ORDER BY f.soNguoi DESC, f.ratingTB DESC, f.maKhoaHoc",
                thamSo.ToArray()).ToList();

            foreach (var r in them)
            {
                rows.Add(new GoiYRow { thuTu = rows.Count + 1, maKhoaHoc = r.maKhoaHoc, diemDuDoan = r.diem, nguon = NguonPhoBien });
            }
        }

        /// <summary>Sinh "@p0, @p1, ..." cho mệnh đề IN và thêm tham số tương ứng.</summary>
        private static string ThamSoDanhSach(string tienTo, IList<string> giaTri, List<SqlParameter> thamSo)
        {
            var ten = new List<string>();
            for (int i = 0; i < giaTri.Count; i++)
            {
                string name = tienTo + i;
                ten.Add(name);
                thamSo.Add(new SqlParameter(name, giaTri[i]));
            }
            return string.Join(", ", ten);
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
            public string maDanhMuc { get; set; }
        }

        public class GoiYTinhLaiRow : GoiYRow
        {
            public string maKhoaHocLyDo { get; set; }
            public string tenKhoaHocLyDo { get; set; }
            public DateTime? maTranCapNhatLuc { get; set; }
        }

        public class DiemRow
        {
            public string maKhoaHoc { get; set; }
            public double diem { get; set; }
        }
    }
}
