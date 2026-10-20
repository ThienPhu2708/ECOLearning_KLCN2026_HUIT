using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ECOLearning.Services
{
    /// <summary>
    /// Chuyển tên (tiếng Việt) thành đoạn đường dẫn: "Tài chính & Kế toán" -> "tai-chinh-ke-toan".
    /// Dùng cho trang danh mục /danh-muc/{slug}: một trang duy nhất hiển thị theo danh mục người dùng chọn,
    /// thêm danh mục mới không phải tạo thêm trang hay sửa route.
    /// </summary>
    public static class SlugHelper
    {
        public static string ToSlug(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            // Bỏ dấu: tách chữ và dấu (FormD) rồi bỏ phần dấu; "đ" không có dạng tách nên đổi riêng
            string normalized = text.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            // Ký tự không phải chữ / số -> "-", gộp nhiều "-" liên tiếp, bỏ "-" ở hai đầu
            string slug = Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), "[^a-z0-9]+", "-");
            return slug.Trim('-');
        }
    }
}
