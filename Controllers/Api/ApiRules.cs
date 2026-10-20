using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ECOLearning.Controllers.Api
{
    /// <summary>Quy tắc kiểm tra dữ liệu, cùng câu thông báo với website (AccountController, ProfileController).</summary>
    public static class ApiRules
    {
        private static readonly Regex EmailRegex = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);

        public static string ChuanHoaEmail(string email) => (email ?? "").Trim().ToLowerInvariant();

        /// <summary>null nếu hợp lệ, ngược lại câu báo lỗi</summary>
        public static string KiemTraEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "Email không được để trống!";
            if (!EmailRegex.IsMatch(ChuanHoaEmail(email))) return "Định dạng Email không hợp lệ (ví dụ: tenban@gmail.com)!";
            return null;
        }

        public static string KiemTraHoTen(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "Vui lòng nhập Họ và tên!";
            string ten = fullName.Trim();
            bool coSo = ten.Any(char.IsDigit);
            bool coKyTu = ten.Any(ch => !char.IsLetter(ch) && !char.IsWhiteSpace(ch));
            if (coSo && coKyTu) return "Họ và tên chỉ được chứa chữ cái, không được chứa chữ số và ký tự đặc biệt!";
            if (coSo) return "Họ và tên không được chứa chữ số (0-9)!";
            if (coKyTu) return "Họ và tên không được chứa ký tự đặc biệt!";
            if (ten.Length < 2) return "Họ và tên phải có tối thiểu 2 ký tự!";
            if (ten.Length > 100) return "Họ và tên tối đa 100 ký tự!";
            return null;
        }

        /// <summary>Mật khẩu tối thiểu 8 ký tự, có chữ in hoa, chữ số và ký tự đặc biệt</summary>
        public static string KiemTraMatKhau(string password, string tienTo = "Mật khẩu")
        {
            var thieu = new List<string>();
            if (string.IsNullOrEmpty(password) || password.Length < 8) thieu.Add("tối thiểu 8 ký tự");
            if (string.IsNullOrEmpty(password) || !password.Any(char.IsUpper)) thieu.Add("chữ in hoa (A-Z)");
            if (string.IsNullOrEmpty(password) || !password.Any(char.IsDigit)) thieu.Add("chữ số (0-9)");
            if (string.IsNullOrEmpty(password) || !password.Any(ch => !char.IsLetterOrDigit(ch))) thieu.Add("ký tự đặc biệt (!@#$%^&*...)");
            return thieu.Any() ? tienTo + " còn thiếu: " + string.Join(", ", thieu) + "." : null;
        }

        public static string KiemTraSoDienThoai(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            string so = phone.Replace(" ", "").Replace(".", "").Replace("-", "");
            return Regex.IsMatch(so, @"^(\+84|0)\d{9,10}$") ? null : "Số điện thoại không hợp lệ!";
        }
    }
}
