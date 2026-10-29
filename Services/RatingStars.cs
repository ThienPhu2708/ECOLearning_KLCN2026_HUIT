using System;
using System.Globalization;
using System.Text;
using System.Web;

namespace ECOLearning.Services
{
    /// <summary>
    /// Vẽ 5 ngôi sao theo đúng điểm trung bình: mỗi sao là nền xám + lớp vàng cắt theo phần trăm,
    /// nên 4,0 / 4,3 / 4,5 hiển thị khác nhau (4,3 = 4 sao đầy + 30% sao thứ năm).
    /// </summary>
    public static class RatingStars
    {
        public static IHtmlString Html(double diem, string lopThem = null)
        {
            if (double.IsNaN(diem) || diem < 0) diem = 0;
            if (diem > 5) diem = 5;

            var sb = new StringBuilder();
            sb.Append("<span class=\"stars star-rating");
            if (!string.IsNullOrWhiteSpace(lopThem)) sb.Append(' ').Append(HttpUtility.HtmlAttributeEncode(lopThem));
            sb.Append("\" role=\"img\" aria-label=\"")
              .Append(diem.ToString("0.0", CultureInfo.InvariantCulture)).Append(" trên 5 sao\">");

            for (int i = 0; i < 5; i++)
            {
                // Làm tròn phần lẻ tới 10% để sao không bị cắt lắt nhắt.
                double phan = Math.Round(Math.Max(0, Math.Min(1, diem - i)) * 10) * 10;
                sb.Append("<span class=\"star-cell\"><i class=\"bi bi-star-fill star-bg\"></i>");
                if (phan > 0)
                    sb.Append("<span class=\"star-fg\" style=\"width:")
                      .Append(phan.ToString(CultureInfo.InvariantCulture))
                      .Append("%\"><i class=\"bi bi-star-fill\"></i></span>");
                sb.Append("</span>");
            }
            sb.Append("</span>");
            return new HtmlString(sb.ToString());
        }
    }
}
