using System;

namespace ECOLearning.Services
{
    /// <summary>Thời gian tương đối cho đánh giá / phản hồi: "Vừa xong", "5 phút trước", "2 tháng trước", "1 năm trước".</summary>
    public static class ReviewTime
    {
        public static string ThoiGianTruoc(DateTime? thoiDiem)
        {
            if (!thoiDiem.HasValue) return "";
            var t = DateTime.Now - thoiDiem.Value;
            if (t.TotalMinutes < 1) return "Vừa xong";
            if (t.TotalHours < 1) return (int)t.TotalMinutes + " phút trước";
            if (t.TotalDays < 1) return (int)t.TotalHours + " giờ trước";
            if (t.TotalDays < 7) return (int)t.TotalDays + " ngày trước";
            if (t.TotalDays < 30) return (int)(t.TotalDays / 7) + " tuần trước";
            if (t.TotalDays < 365) return (int)(t.TotalDays / 30) + " tháng trước";
            return (int)(t.TotalDays / 365) + " năm trước";
        }
    }
}
