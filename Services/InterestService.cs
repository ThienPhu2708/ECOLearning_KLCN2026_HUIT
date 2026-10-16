using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Lĩnh vực quan tâm của học viên (cold-start). Bảng SoThichNguoiDung / KhaoSatSoThich ở
    /// Database/Migrations/007_GoiY_SoThichNguoiDung.sql, chưa có trong mô hình EF nên dùng SQL trực tiếp.
    /// </summary>
    public class InterestService
    {
        public const int SoLinhVucToiDa = 3;

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public InterestService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        /// <summary>Đã chạy migration 007 hay chưa.</summary>
        public bool IsInstalled()
        {
            try
            {
                return db.Database.SqlQuery<int>(@"
                    SELECT CASE WHEN OBJECT_ID(N'dbo.SoThichNguoiDung', N'U') IS NOT NULL
                                 AND OBJECT_ID(N'dbo.KhaoSatSoThich', N'U') IS NOT NULL
                           THEN 1 ELSE 0 END").FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Tự mở bước chọn lĩnh vực khi: học viên chưa trả lời / chưa bỏ qua lần nào và chưa có tương tác nào
        /// (người đã xem hoặc học khóa nào đó thì lọc cộng tác đã có dữ liệu, không cần hỏi).
        /// </summary>
        public bool ShouldAsk(int userId)
        {
            return db.Database.SqlQuery<int>(@"
                SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.KhaoSatSoThich WHERE maNguoiDung = @maNguoiDung)
                             AND NOT EXISTS (SELECT 1 FROM dbo.vw_GoiY_TuongTac WHERE user_id = @maNguoiDung)
                       THEN 1 ELSE 0 END",
                new SqlParameter("@maNguoiDung", userId)).FirstOrDefault() == 1;
        }

        /// <summary>Danh mục đang hoạt động và có ít nhất một khóa đã xuất bản, kèm số khóa.</summary>
        public List<LinhVucQuanTamItem> GetChoices()
        {
            return db.Database.SqlQuery<LinhVucQuanTamItem>(@"
                SELECT dm.maDanhMuc AS MaDanhMuc, dm.tenDanhMuc AS TenDanhMuc, COUNT(*) AS SoKhoaHoc
                FROM dbo.DanhMuc dm
                INNER JOIN dbo.KhoaHoc kh ON kh.maDanhMuc = dm.maDanhMuc AND kh.trangThai = N'Đã xuất bản'
                WHERE ISNULL(dm.trangThai, 1) = 1
                GROUP BY dm.maDanhMuc, dm.tenDanhMuc
                ORDER BY COUNT(*) DESC, dm.tenDanhMuc").ToList();
        }

        /// <summary>Lĩnh vực học viên đã chọn (theo thứ tự chọn), chỉ gồm danh mục còn hoạt động.</summary>
        public List<LinhVucQuanTamItem> GetSelected(int userId)
        {
            return db.Database.SqlQuery<LinhVucQuanTamItem>(@"
                SELECT dm.maDanhMuc AS MaDanhMuc, dm.tenDanhMuc AS TenDanhMuc, 0 AS SoKhoaHoc
                FROM dbo.SoThichNguoiDung s
                INNER JOIN dbo.DanhMuc dm ON dm.maDanhMuc = s.maDanhMuc
                WHERE s.maNguoiDung = @maNguoiDung AND ISNULL(dm.trangThai, 1) = 1
                ORDER BY s.ngayChon, dm.tenDanhMuc",
                new SqlParameter("@maNguoiDung", userId)).ToList();
        }

        /// <summary>
        /// Dữ liệu cho hộp thoại chọn lĩnh vực, hoặc null nếu người dùng không thuộc nhóm được chọn
        /// (chỉ học viên và giáo viên, vì giáo viên cũng mua / học khóa của người khác) hoặc chưa chạy migration 007.
        /// Chỉ tự mở với học viên chưa trả lời lần nào và chưa có tương tác.
        /// </summary>
        public InterestPickerViewModel BuildPicker(int userId, string role)
        {
            bool laHocVien = string.IsNullOrEmpty(role) || role == "HocVien";
            if (userId <= 0 || !(laHocVien || role == "GiaoVien") || !IsInstalled())
            {
                return null;
            }
            var choices = GetChoices();
            if (choices.Count == 0)
            {
                return null;
            }
            return new InterestPickerViewModel
            {
                LuaChon = choices,
                DaChon = new HashSet<string>(GetSelected(userId).Select(s => s.MaDanhMuc), StringComparer.OrdinalIgnoreCase),
                TuDongMo = laHocVien && ShouldAsk(userId),
                SoLuaChonToiDa = SoLinhVucToiDa
            };
        }

        /// <summary>Lưu 1–3 lĩnh vực (thay toàn bộ lựa chọn cũ) và đánh dấu đã trả lời.</summary>
        public InterestSaveResult Save(int userId, IEnumerable<string> categoryIds)
        {
            var ids = (categoryIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ids.Count == 0)
            {
                return InterestSaveResult.Fail("Vui lòng chọn ít nhất một lĩnh vực.");
            }
            if (ids.Count > SoLinhVucToiDa)
            {
                return InterestSaveResult.Fail("Chỉ chọn tối đa " + SoLinhVucToiDa + " lĩnh vực.");
            }

            var hopLe = new HashSet<string>(GetChoices().Select(c => c.MaDanhMuc), StringComparer.OrdinalIgnoreCase);
            if (ids.Any(id => !hopLe.Contains(id)))
            {
                return InterestSaveResult.Fail("Lĩnh vực đã chọn không còn hợp lệ. Vui lòng tải lại trang.");
            }

            using (var tx = db.Database.BeginTransaction())
            {
                db.Database.ExecuteSqlCommand("DELETE FROM dbo.SoThichNguoiDung WHERE maNguoiDung = @maNguoiDung",
                    new SqlParameter("@maNguoiDung", userId));

                // Cách nhau 1 ms để giữ đúng thứ tự chọn khi đọc lại (ORDER BY ngayChon)
                var now = DateTime.Now;
                for (int i = 0; i < ids.Count; i++)
                {
                    db.Database.ExecuteSqlCommand(
                        "INSERT INTO dbo.SoThichNguoiDung (maNguoiDung, maDanhMuc, ngayChon) VALUES (@maNguoiDung, @maDanhMuc, @ngayChon)",
                        new SqlParameter("@maNguoiDung", userId),
                        new SqlParameter("@maDanhMuc", ids[i]),
                        new SqlParameter("@ngayChon", now.AddMilliseconds(i * 10)));
                }
                MarkAnswered(userId, false);
                tx.Commit();
            }
            return new InterestSaveResult { Success = true };
        }

        /// <summary>Học viên bỏ qua bước chọn: chỉ đánh dấu để không tự mở lại, giữ nguyên lựa chọn cũ (nếu có).</summary>
        public void Skip(int userId)
        {
            MarkAnswered(userId, true);
        }

        private void MarkAnswered(int userId, bool daBoQua)
        {
            db.Database.ExecuteSqlCommand(@"
                MERGE dbo.KhaoSatSoThich WITH (HOLDLOCK) AS t
                USING (SELECT @maNguoiDung AS maNguoiDung) AS s ON t.maNguoiDung = s.maNguoiDung
                WHEN MATCHED THEN UPDATE SET daBoQua = @daBoQua, ngayCapNhat = GETDATE()
                WHEN NOT MATCHED THEN INSERT (maNguoiDung, daBoQua) VALUES (s.maNguoiDung, @daBoQua);",
                new SqlParameter("@maNguoiDung", userId),
                new SqlParameter("@daBoQua", daBoQua));
        }
    }

    public class InterestSaveResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public static InterestSaveResult Fail(string message)
        {
            return new InterestSaveResult { Success = false, Message = message };
        }
    }
}
