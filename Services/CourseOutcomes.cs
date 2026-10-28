using System;
using System.Collections.Generic;
using System.Linq;

namespace ECOLearning.Services
{
    /// <summary>
    /// Tách cột KhoaHoc.mucTieuDauRa thành từng mục tiêu. Form giáo viên lưu mỗi mục tiêu một dòng;
    /// dữ liệu cũ (seed) ngăn cách bằng ";". Không tách theo dấu phẩy vì một mục tiêu thường có dấu phẩy bên trong.
    /// </summary>
    public static class CourseOutcomes
    {
        public static List<string> Tach(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new List<string>();
            var dau = s.IndexOfAny(new[] { '\r', '\n' }) >= 0 ? new[] { '\r', '\n' } : new[] { ';' };
            return s.Split(dau, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().TrimStart('-', '•', '*').Trim())
                .Where(x => x.Length > 0)
                .ToList();
        }
    }
}
