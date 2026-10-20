using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// "Thả tim" khóa học. Lưu ở cột daThich của dbo.TuongTacKhoaHoc (migration 005), không cần bảng mới.
    /// daThich cũng là tín hiệu "likes" (trọng số 0.1) của lọc cộng tác: thả tim xong thì job hằng đêm và
    /// nút tính lại gợi ý tự dùng, không phải sửa thuật toán.
    /// Thả tim KHÔNG cập nhật lanCapNhat: cột này là thời điểm xem gần nhất của mục "Vì bạn đã xem X".
    /// </summary>
    public class FavoriteService
    {
        private const string CacheKey = "FavoriteService.LikedIds";

        private static readonly string[] TrangThaiDangBan = { "Đã xuất bản", "Đã duyệt", "Active" };

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public FavoriteService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        /// <summary>Bật / tắt tim. Trả về trạng thái mới (Liked) hoặc thông báo lỗi.</summary>
        public FavoriteToggleResult Toggle(int userId, string courseId)
        {
            var course = db.KhoaHoc.Where(k => k.maKhoaHoc == courseId)
                .Select(k => new { k.maGiaoVien, k.trangThai }).FirstOrDefault();
            if (course == null || !TrangThaiDangBan.Contains(course.trangThai))
            {
                return FavoriteToggleResult.Fail("Không tìm thấy khóa học hoặc khóa học đã ngừng bán.");
            }
            if (course.maGiaoVien == userId)
            {
                return FavoriteToggleResult.Fail("Bạn không thể thả tim khóa học của chính mình.");
            }
            if (db.DangKyKhoaHoc.Any(d => d.maHocVien == userId && d.maKhoaHoc == courseId))
            {
                return FavoriteToggleResult.Fail("Bạn đã sở hữu khóa học này, xem ở mục Khóa học của tôi.");
            }

            // Chưa có dòng tương tác: thêm mới với daThich = 1 (luotXem = 0 nên không thành khóa "đã xem").
            // Đã có: đảo daThich, giữ nguyên lượt xem / click / lanCapNhat.
            bool liked = db.Database.SqlQuery<bool>(@"
                MERGE dbo.TuongTacKhoaHoc WITH (HOLDLOCK) AS t
                USING (SELECT @maHocVien AS maHocVien, @maKhoaHoc AS maKhoaHoc) AS s
                    ON t.maHocVien = s.maHocVien AND t.maKhoaHoc = s.maKhoaHoc
                WHEN MATCHED THEN
                    UPDATE SET daThich = CASE WHEN t.daThich = 1 THEN 0 ELSE 1 END
                WHEN NOT MATCHED THEN
                    INSERT (maHocVien, maKhoaHoc, daThich) VALUES (s.maHocVien, s.maKhoaHoc, 1)
                OUTPUT inserted.daThich;",
                new SqlParameter("@maHocVien", userId),
                new SqlParameter("@maKhoaHoc", courseId)).Single();

            return new FavoriteToggleResult { Success = true, Liked = liked, Count = CountFavorites(userId) };
        }

        /// <summary>Mã các khóa người dùng đang thả tim.</summary>
        public HashSet<string> GetLikedIds(int userId)
        {
            return new HashSet<string>(db.Database.SqlQuery<string>(
                "SELECT maKhoaHoc FROM dbo.TuongTacKhoaHoc WHERE maHocVien = @maHocVien AND daThich = 1",
                new SqlParameter("@maHocVien", userId)));
        }

        /// <summary>
        /// Như GetLikedIds nhưng chỉ truy vấn một lần cho mỗi request (nút tim xuất hiện trên nhiều thẻ của một trang).
        /// Chưa đăng nhập / chưa chạy migration 005: trả về tập rỗng.
        /// </summary>
        public static HashSet<string> GetLikedIdsCached(HttpContextBase context)
        {
            var cached = context.Items[CacheKey] as HashSet<string>;
            if (cached != null)
            {
                return cached;
            }

            var ids = new HashSet<string>();
            int userId = (context.Session?["UserId"] as int?) ?? 0;
            if (userId > 0)
            {
                try
                {
                    using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                    {
                        ids = new FavoriteService(db).GetLikedIds(userId);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Load favorites error: " + ex.Message);
                }
            }
            context.Items[CacheKey] = ids;
            return ids;
        }

        // Khóa đang thả tim còn hiển thị được: đang bán, chưa sở hữu (TuongTacKhoaHoc không có trong model EF)
        private const string SqlKhoaDaTim = @"
                FROM dbo.TuongTacKhoaHoc t
                INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = t.maKhoaHoc
                WHERE t.maHocVien = @maHocVien
                  AND t.daThich = 1
                  AND k.trangThai IN (N'Đã xuất bản', N'Đã duyệt', N'Active')
                  AND NOT EXISTS (SELECT 1 FROM dbo.DangKyKhoaHoc d WHERE d.maHocVien = t.maHocVien AND d.maKhoaHoc = t.maKhoaHoc)";

        /// <summary>Số khóa đang thả tim còn hiển thị được (đang bán, chưa sở hữu).</summary>
        public int CountFavorites(int userId)
        {
            return db.Database.SqlQuery<int>("SELECT COUNT(*)" + SqlKhoaDaTim,
                new SqlParameter("@maHocVien", userId)).Single();
        }

        /// <summary>Tối đa top khóa đã tim gần đây (mã, tên) cho menu "Khóa học của tôi".</summary>
        public List<ExploreCourseViewModel> GetFavoriteMenuItems(int userId, int top)
        {
            return db.Database.SqlQuery<ExploreCourseViewModel>(
                "SELECT TOP (@top) k.maKhoaHoc AS MaKhoaHoc, k.tenKhoaHoc AS TenKhoaHoc" + SqlKhoaDaTim + " ORDER BY t.lanCapNhat DESC, k.maKhoaHoc",
                new SqlParameter("@top", top),
                new SqlParameter("@maHocVien", userId)).ToList();
        }

        /// <summary>
        /// Danh sách "Khóa học đã tim": khóa đang bán, chưa sở hữu. Khóa đã mua / đăng ký tự rời danh sách
        /// (vào "Khóa học đã đăng ký"), nhưng daThich vẫn giữ làm tín hiệu cho lọc cộng tác.
        /// Không có ngày thả tim riêng nên xếp theo lần tương tác gần nhất.
        /// </summary>
        public List<FavoriteCourseItem> GetFavorites(int userId)
        {
            var ids = db.Database.SqlQuery<string>("SELECT k.maKhoaHoc" + SqlKhoaDaTim + " ORDER BY t.lanCapNhat DESC, k.maKhoaHoc",
                new SqlParameter("@maHocVien", userId)).ToList();
            if (ids.Count == 0)
            {
                return new List<FavoriteCourseItem>();
            }

            var courses = db.KhoaHoc
                .Include(k => k.NguoiDung)
                .Include(k => k.DanhMuc)
                .Where(k => ids.Contains(k.maKhoaHoc))
                .ToDictionary(k => k.maKhoaHoc);

            var ratings = db.DanhGiaKhoaHoc
                .Where(d => ids.Contains(d.maKhoaHoc))
                .GroupBy(d => d.maKhoaHoc)
                .Select(g => new { MaKhoaHoc = g.Key, SoDanhGia = g.Count(), Diem = g.Average(d => (double)d.soSao) })
                .ToDictionary(x => x.MaKhoaHoc);

            return ids.Where(courses.ContainsKey).Select(id => new FavoriteCourseItem
            {
                Course = courses[id],
                SoDanhGia = ratings.ContainsKey(id) ? ratings[id].SoDanhGia : 0,
                DiemDanhGia = ratings.ContainsKey(id) ? ratings[id].Diem : 0
            }).ToList();
        }
    }

    public class FavoriteToggleResult
    {
        public bool Success { get; set; }
        public bool Liked { get; set; }
        public int Count { get; set; }
        public string Message { get; set; }

        public static FavoriteToggleResult Fail(string message)
        {
            return new FavoriteToggleResult { Success = false, Message = message };
        }
    }
}
