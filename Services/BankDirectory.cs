using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECOLearning.Services
{
    /// <summary>
    /// Danh mục ngân hàng Việt Nam cho thông tin nhận thanh toán của giáo viên: người dùng chọn từ danh sách (tìm được
    /// theo tên viết tắt / tên đầy đủ, không dấu) thay vì gõ tự do, để tên ngân hàng lưu thống nhất và không sai chính tả.
    /// Mã (Code) theo mã viết tắt thông dụng trên VietQR / Napas, giữ để sau này tích hợp chuyển khoản tự động.
    /// Không cho gõ tên ngân hàng tự do (dễ sai sót); cần thêm ngân hàng thì bổ sung vào danh sách này.
    /// Dùng ở trang đăng ký giáo viên và trang hồ sơ giáo viên (Views/Shared/_BankPicker.cshtml, assets/js/bank-picker.js).
    /// </summary>
    public static class BankDirectory
    {
        public class Bank
        {
            public string Code { get; set; }
            public string ShortName { get; set; }
            public string FullName { get; set; }
        }

        /// <summary>Ngân hàng phổ biến trước, còn lại theo bảng chữ cái.</summary>
        public static readonly IReadOnlyList<Bank> Banks = new List<Bank>
        {
            new Bank { Code = "VCB", ShortName = "Vietcombank", FullName = "Ngân hàng TMCP Ngoại thương Việt Nam" },
            new Bank { Code = "ICB", ShortName = "VietinBank", FullName = "Ngân hàng TMCP Công thương Việt Nam" },
            new Bank { Code = "BIDV", ShortName = "BIDV", FullName = "Ngân hàng TMCP Đầu tư và Phát triển Việt Nam" },
            new Bank { Code = "VBA", ShortName = "Agribank", FullName = "Ngân hàng Nông nghiệp và Phát triển Nông thôn Việt Nam" },
            new Bank { Code = "TCB", ShortName = "Techcombank", FullName = "Ngân hàng TMCP Kỹ thương Việt Nam" },
            new Bank { Code = "MB", ShortName = "MB Bank", FullName = "Ngân hàng TMCP Quân đội" },
            new Bank { Code = "ACB", ShortName = "ACB", FullName = "Ngân hàng TMCP Á Châu" },
            new Bank { Code = "VPB", ShortName = "VPBank", FullName = "Ngân hàng TMCP Việt Nam Thịnh Vượng" },
            new Bank { Code = "TPB", ShortName = "TPBank", FullName = "Ngân hàng TMCP Tiên Phong" },
            new Bank { Code = "STB", ShortName = "Sacombank", FullName = "Ngân hàng TMCP Sài Gòn Thương Tín" },
            new Bank { Code = "HDB", ShortName = "HDBank", FullName = "Ngân hàng TMCP Phát triển Thành phố Hồ Chí Minh" },
            new Bank { Code = "VIB", ShortName = "VIB", FullName = "Ngân hàng TMCP Quốc tế Việt Nam" },
            new Bank { Code = "SHB", ShortName = "SHB", FullName = "Ngân hàng TMCP Sài Gòn - Hà Nội" },
            new Bank { Code = "SEAB", ShortName = "SeABank", FullName = "Ngân hàng TMCP Đông Nam Á" },
            new Bank { Code = "OCB", ShortName = "OCB", FullName = "Ngân hàng TMCP Phương Đông" },
            new Bank { Code = "MSB", ShortName = "MSB", FullName = "Ngân hàng TMCP Hàng Hải Việt Nam" },
            new Bank { Code = "EIB", ShortName = "Eximbank", FullName = "Ngân hàng TMCP Xuất Nhập khẩu Việt Nam" },
            new Bank { Code = "LPB", ShortName = "LPBank", FullName = "Ngân hàng TMCP Lộc Phát Việt Nam" },
            new Bank { Code = "ABB", ShortName = "ABBANK", FullName = "Ngân hàng TMCP An Bình" },
            new Bank { Code = "BAB", ShortName = "Bac A Bank", FullName = "Ngân hàng TMCP Bắc Á" },
            new Bank { Code = "BVB", ShortName = "BaoViet Bank", FullName = "Ngân hàng TMCP Bảo Việt" },
            new Bank { Code = "VCCB", ShortName = "BVBank", FullName = "Ngân hàng TMCP Bản Việt" },
            new Bank { Code = "COOPBANK", ShortName = "Co-opBank", FullName = "Ngân hàng Hợp tác xã Việt Nam" },
            new Bank { Code = "GPB", ShortName = "GPBank", FullName = "Ngân hàng TM TNHH MTV Dầu Khí Toàn Cầu" },
            new Bank { Code = "KLB", ShortName = "KienlongBank", FullName = "Ngân hàng TMCP Kiên Long" },
            new Bank { Code = "NAB", ShortName = "Nam A Bank", FullName = "Ngân hàng TMCP Nam Á" },
            new Bank { Code = "NCB", ShortName = "NCB", FullName = "Ngân hàng TMCP Quốc Dân" },
            new Bank { Code = "PGB", ShortName = "PGBank", FullName = "Ngân hàng TMCP Thịnh vượng và Phát triển" },
            new Bank { Code = "PVCB", ShortName = "PVcomBank", FullName = "Ngân hàng TMCP Đại Chúng Việt Nam" },
            new Bank { Code = "SCB", ShortName = "SCB", FullName = "Ngân hàng TMCP Sài Gòn" },
            new Bank { Code = "SGICB", ShortName = "SaigonBank", FullName = "Ngân hàng TMCP Sài Gòn Công Thương" },
            new Bank { Code = "VAB", ShortName = "VietABank", FullName = "Ngân hàng TMCP Việt Á" },
            new Bank { Code = "VIETBANK", ShortName = "VietBank", FullName = "Ngân hàng TMCP Việt Nam Thương Tín" },
            new Bank { Code = "CAKE", ShortName = "Cake by VPBank", FullName = "Ngân hàng số Cake by VPBank" },
            new Bank { Code = "UBANK", ShortName = "Ubank by VPBank", FullName = "Ngân hàng số Ubank by VPBank" },
            new Bank { Code = "TIMO", ShortName = "Timo", FullName = "Ngân hàng số Timo by Bản Việt" },
            new Bank { Code = "SHBVN", ShortName = "Shinhan Bank", FullName = "Ngân hàng TNHH MTV Shinhan Việt Nam" },
            new Bank { Code = "WVN", ShortName = "Woori Bank", FullName = "Ngân hàng TNHH MTV Woori Việt Nam" },
            new Bank { Code = "HSBC", ShortName = "HSBC", FullName = "Ngân hàng TNHH MTV HSBC (Việt Nam)" },
            new Bank { Code = "SCVN", ShortName = "Standard Chartered", FullName = "Ngân hàng TNHH MTV Standard Chartered Việt Nam" },
            new Bank { Code = "UOB", ShortName = "UOB", FullName = "Ngân hàng United Overseas Bank Việt Nam" },
            new Bank { Code = "CIMB", ShortName = "CIMB", FullName = "Ngân hàng TNHH MTV CIMB Việt Nam" },
            new Bank { Code = "PBVN", ShortName = "Public Bank", FullName = "Ngân hàng TNHH MTV Public Việt Nam" },
            new Bank { Code = "HLBVN", ShortName = "Hong Leong Bank", FullName = "Ngân hàng TNHH MTV Hong Leong Việt Nam" },
            new Bank { Code = "IVB", ShortName = "Indovina Bank", FullName = "Ngân hàng TNHH Indovina" },
            new Bank { Code = "VRB", ShortName = "VRB", FullName = "Ngân hàng Liên doanh Việt - Nga" }
        };

        /// <summary>Tìm ngân hàng theo mã, tên viết tắt hoặc tên đầy đủ (không phân biệt hoa thường / dấu).</summary>
        public static Bank Find(string value)
        {
            string k = SearchText.KhongDau(value);
            if (k.Length == 0) return null;
            return Banks.FirstOrDefault(b => SearchText.KhongDau(b.Code) == k || SearchText.KhongDau(b.ShortName) == k || SearchText.KhongDau(b.FullName) == k);
        }

        /// <summary>
        /// Nhận ra ngân hàng từ tên cũ đã lưu kiểu gõ tự do: trùng tên / mã ("VietComBank"), hoặc tên viết tắt / mã đứng đầu
        /// ("ACB Chi nhánh Gò Mây" → ACB, "Techcombank Chi nhánh TP.HCM" → Techcombank). Không nhận ra thì null.
        /// </summary>
        public static Bank DoanNganHang(string tenCu)
        {
            var bank = Find(tenCu);
            if (bank != null) return bank;
            string k = SearchText.KhongDau(tenCu);
            if (k.Length == 0) return null;
            return Banks
                .SelectMany(b => new[] { SearchText.KhongDau(b.ShortName), SearchText.KhongDau(b.Code) }.Select(key => new { b, key }))
                .Where(x => x.key.Length > 0 && k.StartsWith(x.key + " "))
                .OrderByDescending(x => x.key.Length)
                .Select(x => x.b)
                .FirstOrDefault();
        }

        /// <summary>
        /// Ghi chú khi điền sẵn ngân hàng từ tên cũ gõ tự do (trang đăng ký khi nộp lại, trang hồ sơ giáo viên).
        /// null khi tên cũ đã đúng tên chuẩn.
        /// </summary>
        public static string GhiChuTenCu(string tenCu, Bank nhanRa)
        {
            if (string.IsNullOrWhiteSpace(tenCu)) return null;
            if (nhanRa == null) return "Tên ngân hàng đã lưu \"" + tenCu.Trim() + "\" không có trong danh sách — vui lòng chọn lại ngân hàng.";
            if (string.Equals(tenCu.Trim(), nhanRa.ShortName)) return null;
            return "Đã nhận ra \"" + nhanRa.ShortName + "\" từ thông tin cũ \"" + tenCu.Trim() + "\" — vui lòng kiểm tra lại rồi lưu.";
        }

        /// <summary>Số tài khoản chỉ gồm chữ số; bỏ khoảng trắng, dấu chấm, gạch ngang người dùng gõ / dán vào.</summary>
        public static string ChuanHoaSoTaiKhoan(string s)
        {
            return new string((s ?? "").Where(char.IsDigit).ToArray());
        }

        public static bool SoTaiKhoanHopLe(string digits)
        {
            return digits != null && digits.Length >= 6 && digits.Length <= 19;
        }

        /// <summary>
        /// Tên chủ tài khoản theo cách ngân hàng ghi: IN HOA, KHÔNG DẤU, một khoảng trắng giữa các từ ("Nguyễn Văn Ánh" → "NGUYEN VAN ANH").
        /// Ký tự không phải chữ cái bị bỏ.
        /// </summary>
        public static string ChuanHoaTenChuTaiKhoan(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            string d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            bool space = true;
            foreach (char c0 in d)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c0) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                char c = char.ToUpperInvariant(c0 == 'đ' || c0 == 'Đ' ? 'D' : c0);
                if (c >= 'A' && c <= 'Z') { sb.Append(c); space = false; }
                else if (!space) { sb.Append(' '); space = true; }
            }
            return sb.ToString().Trim();
        }
    }
}
