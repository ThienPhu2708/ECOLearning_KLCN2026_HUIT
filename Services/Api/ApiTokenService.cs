using System;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ECOLearning.Models;

namespace ECOLearning.Services.Api
{
    /// <summary>
    /// Token đăng nhập của ứng dụng di động (bảng dbo.ApiToken, migration 015).
    /// Token là 32 byte ngẫu nhiên (base64url) chỉ trả cho app một lần lúc đăng nhập; CSDL chỉ lưu SHA-256 của token.
    /// </summary>
    public class ApiTokenService
    {
        public const int SoNgayHieuLuc = 30;

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public ApiTokenService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public bool IsInstalled()
        {
            try
            {
                return db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.ApiToken', N'U') IS NULL THEN 0 ELSE 1 END").Single() == 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Tạo token mới cho người dùng. Trả về token gốc (chỉ lần này) và thời điểm hết hạn.</summary>
        public string TaoToken(int maNguoiDung, string thietBi, out DateTime hetHan)
        {
            var bytes = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(bytes);
            }
            string token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            hetHan = DateTime.Now.AddDays(SoNgayHieuLuc);
            db.Database.ExecuteSqlCommand(
                "INSERT INTO dbo.ApiToken (tokenHash, maNguoiDung, ngayHetHan, thietBi) VALUES (@h, @u, @het, @tb)",
                new SqlParameter("@h", Bam(token)),
                new SqlParameter("@u", maNguoiDung),
                new SqlParameter("@het", hetHan),
                new SqlParameter("@tb", (object)Cat(thietBi, 200) ?? DBNull.Value));
            return token;
        }

        /// <summary>Mã người dùng của token còn hiệu lực (chưa hết hạn, chưa thu hồi), 0 nếu không hợp lệ.</summary>
        public int XacThuc(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return 0;
            // Cập nhật lần dùng gần nhất tối đa 5 phút / lần để không ghi CSDL ở mọi request
            return db.Database.SqlQuery<int>(@"
                DECLARE @u INT, @ma INT, @lan DATETIME;
                SELECT @ma = maToken, @u = maNguoiDung, @lan = lanDungGanNhat
                FROM dbo.ApiToken
                WHERE tokenHash = @h AND ngayThuHoi IS NULL AND ngayHetHan > GETDATE();
                IF @ma IS NOT NULL AND (@lan IS NULL OR @lan < DATEADD(MINUTE, -5, GETDATE()))
                    UPDATE dbo.ApiToken SET lanDungGanNhat = GETDATE() WHERE maToken = @ma;
                SELECT ISNULL(@u, 0);",
                new SqlParameter("@h", Bam(token))).Single();
        }

        /// <summary>Đăng xuất: thu hồi token đang dùng.</summary>
        public void ThuHoi(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;
            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.ApiToken SET ngayThuHoi = GETDATE() WHERE tokenHash = @h AND ngayThuHoi IS NULL",
                new SqlParameter("@h", Bam(token)));
        }

        /// <summary>Đổi / đặt lại mật khẩu: thu hồi mọi token khác của người dùng (giữ token đang dùng nếu có).</summary>
        public void ThuHoiTatCa(int maNguoiDung, string truToken = null)
        {
            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.ApiToken SET ngayThuHoi = GETDATE() WHERE maNguoiDung = @u AND ngayThuHoi IS NULL AND (@giu IS NULL OR tokenHash <> @giu)",
                new SqlParameter("@u", maNguoiDung),
                new SqlParameter("@giu", string.IsNullOrEmpty(truToken) ? (object)DBNull.Value : Bam(truToken)));
        }

        internal static string Bam(string giaTri)
        {
            using (var sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(giaTri)).Select(b => b.ToString("x2")));
            }
        }

        private static string Cat(string s, int max) => string.IsNullOrEmpty(s) ? null : (s.Length <= max ? s : s.Substring(0, max));
    }
}
