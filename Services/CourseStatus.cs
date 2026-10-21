using System;
using System.Linq;

namespace ECOLearning.Services
{
    /// <summary>
    /// Các giá trị trạng thái khóa học (cột KhoaHoc.trangThai) và cách gom nhóm để thống kê.
    /// Dữ liệu cũ có nhiều cách viết cho cùng một ý nghĩa nên thống kê gom theo nhóm.
    /// </summary>
    public static class CourseStatus
    {
        public const string BanNhap = "Bản nháp";
        public const string ChoDuyet = "Chờ duyệt";
        public const string DaXuatBan = "Đã xuất bản";
        public const string CanSuaDoi = "Cần sửa đổi";

        // Trạng thái khi Admin xử lý báo cáo vi phạm
        public const string TamKhoa = "Tạm khóa";
        public const string KhoaVinhVien = "Khóa vĩnh viễn";

        // Khóa đang bán bị ẩn vì giáo viên bị Admin khóa tài khoản (trạng thái cũ lưu ở bảng KhoaHocTamAn).
        // Không nằm trong PublishedValues nên mọi trang bán hàng tự loại ra; học viên đã mua vẫn học bình thường.
        public const string TamAn = "Tạm ẩn";

        private static readonly string[] PublishedValues = { DaXuatBan, "Đã duyệt", "Active", "Đang hoạt động" };
        private static readonly string[] PendingValues = { ChoDuyet, "Chờ kiểm duyệt", "Chờ xét duyệt", "Pending" };
        private static readonly string[] NeedsFixValues = { CanSuaDoi, "Từ chối" };

        public static bool IsPublished(string status)
        {
            return PublishedValues.Contains(status);
        }

        /// <summary>Tên nhóm hiển thị trong thống kê.</summary>
        public static string GroupLabel(string status)
        {
            if (string.IsNullOrWhiteSpace(status) || status == BanNhap) return "Bản nháp";
            if (PublishedValues.Contains(status)) return "Đang bán (đã xuất bản)";
            if (PendingValues.Contains(status)) return "Chờ duyệt";
            if (NeedsFixValues.Contains(status)) return "Cần sửa đổi";
            if (status == TamKhoa) return "Tạm khóa (vi phạm)";
            if (status == KhoaVinhVien) return "Khóa vĩnh viễn";
            if (status == TamAn) return "Tạm ẩn (GV bị khóa)";
            return status;
        }

        /// <summary>Thứ tự hiển thị các nhóm trong bảng thống kê.</summary>
        public static int GroupOrder(string groupLabel)
        {
            switch (groupLabel)
            {
                case "Đang bán (đã xuất bản)": return 1;
                case "Chờ duyệt": return 2;
                case "Cần sửa đổi": return 3;
                case "Bản nháp": return 4;
                case "Tạm khóa (vi phạm)": return 5;
                case "Khóa vĩnh viễn": return 6;
                case "Tạm ẩn (GV bị khóa)": return 7;
                default: return 99;
            }
        }

        public static string GroupColor(string groupLabel)
        {
            switch (groupLabel)
            {
                case "Đang bán (đã xuất bản)": return "#10B981";
                case "Chờ duyệt": return "#F59E0B";
                case "Cần sửa đổi": return "#EF4444";
                case "Bản nháp": return "#94A3B8";
                case "Tạm khóa (vi phạm)": return "#F97316";
                case "Khóa vĩnh viễn": return "#7F1D1D";
                case "Tạm ẩn (GV bị khóa)": return "#6B7280";
                default: return "#64748B";
            }
        }
    }
}
