using System;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using ECOLearning.Models;

namespace ECOLearning.Services.Api
{
    /// <summary>
    /// Mã OTP qua email cho app (bảng dbo.ApiOtp, migration 015). Cùng quy tắc với website (AccountController):
    /// mã 6 số, hết hạn sau 5 phút, sai quá 5 lần thì vô hiệu, phải xác thực (bước 2) rồi mới dùng ở bước 3.
    /// </summary>
    public class ApiOtpService
    {
        public const string DangKy = "register";
        public const string QuenMatKhau = "forgot";
        public const int PhutHetHan = 5;
        public const int SoLanSaiToiDa = 5;
        public const int GiayChoGuiLai = 60;
        /// <summary>Sau khi xác thực đúng, có 15 phút để hoàn tất bước đặt mật khẩu.</summary>
        public const int PhutDungSauXacThuc = 15;

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public ApiOtpService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        private class OtpRow
        {
            public int maOtp { get; set; }
            public string hoTen { get; set; }
            public string maHash { get; set; }
            public DateTime ngayTao { get; set; }
            public DateTime ngayHetHan { get; set; }
            public int soLanSai { get; set; }
            public DateTime? ngayXacThuc { get; set; }
        }

        private OtpRow MoiNhat(string email, string mucDich)
        {
            return db.Database.SqlQuery<OtpRow>(@"
                SELECT TOP (1) maOtp, hoTen, maHash, ngayTao, ngayHetHan, soLanSai, ngayXacThuc
                FROM dbo.ApiOtp WHERE email = @e AND mucDich = @m AND daDung = 0
                ORDER BY ngayTao DESC, maOtp DESC",
                new SqlParameter("@e", email), new SqlParameter("@m", mucDich)).FirstOrDefault();
        }

        /// <summary>Số giây phải chờ trước khi được gửi lại mã (0 = gửi được ngay).</summary>
        public int GiayPhaiCho(string email, string mucDich)
        {
            var row = MoiNhat(email, mucDich);
            if (row == null) return 0;
            int daQua = (int)(DateTime.Now - row.ngayTao).TotalSeconds;
            return Math.Max(0, GiayChoGuiLai - daQua);
        }

        /// <summary>Tạo mã mới (mã cũ cùng mục đích bị vô hiệu). Trả về mã 6 số để gửi email.</summary>
        public string TaoMa(string email, string mucDich, string hoTen)
        {
            string ma = SoNgauNhien6();
            db.Database.ExecuteSqlCommand(@"
                UPDATE dbo.ApiOtp SET daDung = 1 WHERE email = @e AND mucDich = @m AND daDung = 0;
                INSERT INTO dbo.ApiOtp (email, mucDich, hoTen, maHash, ngayHetHan) VALUES (@e, @m, @ten, @h, DATEADD(MINUTE, @phut, GETDATE()));",
                new SqlParameter("@e", email),
                new SqlParameter("@m", mucDich),
                new SqlParameter("@ten", (object)hoTen ?? DBNull.Value),
                new SqlParameter("@h", Bam(email, mucDich, ma)),
                new SqlParameter("@phut", PhutHetHan));
            return ma;
        }

        /// <summary>Kiểm tra mã (bước 2). Đúng thì đánh dấu đã xác thực. Trả null nếu đúng, ngược lại (code, message).</summary>
        public Tuple<string, string> XacThuc(string email, string mucDich, string ma)
        {
            var row = MoiNhat(email, mucDich);
            if (row == null)
                return Tuple.Create("OTP_INVALID", "Chưa có mã OTP cho email này. Vui lòng bấm gửi mã.");
            if (row.soLanSai >= SoLanSaiToiDa)
                return Tuple.Create("OTP_LOCKED", "Bạn đã nhập sai mã OTP quá 5 lần. Vui lòng yêu cầu mã OTP mới!");
            if (row.ngayHetHan < DateTime.Now)
                return Tuple.Create("OTP_EXPIRED", "Mã OTP đã hết hạn (sau 5 phút). Vui lòng gửi lại mã mới!");
            if (!BangNhau(row.maHash, Bam(email, mucDich, (ma ?? "").Trim())))
            {
                db.Database.ExecuteSqlCommand("UPDATE dbo.ApiOtp SET soLanSai = soLanSai + 1 WHERE maOtp = @id", new SqlParameter("@id", row.maOtp));
                int conLai = SoLanSaiToiDa - row.soLanSai - 1;
                return Tuple.Create("OTP_INVALID", conLai > 0
                    ? string.Format("Mã xác thực OTP không chính xác! Còn lại {0} lần thử.", conLai)
                    : "Bạn đã nhập sai mã OTP quá 5 lần. Vui lòng yêu cầu mã OTP mới!");
            }
            if (!row.ngayXacThuc.HasValue)
            {
                db.Database.ExecuteSqlCommand("UPDATE dbo.ApiOtp SET ngayXacThuc = GETDATE() WHERE maOtp = @id", new SqlParameter("@id", row.maOtp));
            }
            return null;
        }

        /// <summary>
        /// Bước 3: mã phải đúng, đã (hoặc vừa) xác thực và còn trong thời gian cho phép. Thành công thì đánh dấu đã dùng
        /// và trả họ tên đã gửi ở bước 1 (có thể null). Lỗi trả về qua [loi].
        /// </summary>
        public bool DungMa(string email, string mucDich, string ma, out string hoTen, out Tuple<string, string> loi)
        {
            hoTen = null;
            var row = MoiNhat(email, mucDich);
            // Chưa xác thực ở bước 2 (app gộp bước): kiểm tra như bước 2. Đã xác thực thì không xét hạn 5 phút của mã,
            // chỉ cần đúng mã và còn trong PhutDungSauXacThuc kể từ lúc xác thực.
            if (row == null || !row.ngayXacThuc.HasValue)
            {
                loi = XacThuc(email, mucDich, ma);
                if (loi != null) return false;
                row = MoiNhat(email, mucDich);
            }
            else if (!BangNhau(row.maHash, Bam(email, mucDich, (ma ?? "").Trim())))
            {
                loi = Tuple.Create("OTP_INVALID", "Mã xác thực OTP không chính xác!");
                return false;
            }
            loi = null;
            if (row == null || !row.ngayXacThuc.HasValue || row.ngayXacThuc.Value.AddMinutes(PhutDungSauXacThuc) < DateTime.Now)
            {
                loi = Tuple.Create("OTP_EXPIRED", "Phiên xác thực OTP đã hết hạn. Vui lòng gửi lại mã mới!");
                return false;
            }
            int doi = db.Database.ExecuteSqlCommand("UPDATE dbo.ApiOtp SET daDung = 1 WHERE maOtp = @id AND daDung = 0", new SqlParameter("@id", row.maOtp));
            if (doi == 0)
            {
                loi = Tuple.Create("OTP_INVALID", "Mã OTP đã được sử dụng. Vui lòng gửi lại mã mới!");
                return false;
            }
            hoTen = row.hoTen;
            return true;
        }

        private static string SoNgauNhien6()
        {
            var b = new byte[4];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(b);
            }
            return (100000 + BitConverter.ToUInt32(b, 0) % 900000).ToString();
        }

        private static string Bam(string email, string mucDich, string ma) => ApiTokenService.Bam(email + "|" + mucDich + "|" + ma);

        private static bool BangNhau(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int khac = 0;
            for (int i = 0; i < a.Length; i++) khac |= a[i] ^ b[i];
            return khac == 0;
        }
    }
}
