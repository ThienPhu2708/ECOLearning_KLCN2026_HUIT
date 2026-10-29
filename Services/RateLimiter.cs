using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ECOLearning.Services
{
    /// <summary>
    /// Giới hạn số lần gọi trong một khoảng thời gian (lưu trong bộ nhớ, khởi động lại IIS thì đếm lại).
    /// Website và API dùng chung khóa (vd. "chat:u12") nên hỏi trợ lý trên web và app tính chung một hạn mức.
    /// </summary>
    public static class RateLimiter
    {
        private static readonly ConcurrentDictionary<string, List<DateTime>> LuotGoi = new ConcurrentDictionary<string, List<DateTime>>();

        /// <summary>true nếu đã đủ <paramref name="toiDa"/> lần trong <paramref name="cuaSo"/>; chưa đủ thì ghi nhận lần này và trả false.</summary>
        public static bool VuotGioiHan(string khoa, int toiDa, TimeSpan cuaSo)
        {
            var ds = LuotGoi.GetOrAdd(khoa, _ => new List<DateTime>());
            lock (ds)
            {
                var moc = DateTime.UtcNow - cuaSo;
                ds.RemoveAll(t => t < moc);
                if (ds.Count >= toiDa) return true;
                ds.Add(DateTime.UtcNow);
                return false;
            }
        }
    }
}
