using System;
using System.Web;

namespace ECOLearning.Services
{
    /// <summary>
    /// Chế độ chỉ xem cho tài khoản vận hành (Admin, Marketing) ở trang người học: thấy đúng giao diện học viên nhưng
    /// không mua, không thả tim, không lưu mã, không đánh giá... để không làm sai doanh thu, thống kê khuyến mãi và dữ liệu gợi ý
    /// (phân tách vai trò). Giao diện: nút có data-staff-lock bị làm mờ (assets/js/staff-readonly.js);
    /// server: action ghi dữ liệu gắn [StaffReadOnly] (App_Start/StaffReadOnlyAttribute.cs).
    /// Giáo viên không thuộc chế độ này (vẫn mua khóa học của người khác).
    /// </summary>
    public static class StaffViewMode
    {
        public const string ThongBao = "Tài khoản Quản trị viên / Marketing chỉ có quyền xem trang học viên, không thể mua khóa học hay tương tác. Hãy dùng tài khoản học viên để thử.";

        public static bool LaChiXem(string role)
        {
            return AccountAdminService.LaNhanVienQuanTri(role)
                || string.Equals(role, AccountAdminService.Marketing, StringComparison.OrdinalIgnoreCase);
        }

        public static bool LaChiXem(HttpSessionStateBase session)
        {
            return session != null && session["UserId"] != null && LaChiXem(session["Role"] as string);
        }

        public static string TenVaiTro(string role)
        {
            if (AccountAdminService.LaAdminGoc(role)) return "Quản trị viên";
            if (AccountAdminService.LaAdminKiemDuyetNoiDung(role)) return "Quản trị viên kiểm duyệt nội dung";
            return "Marketing";
        }
    }
}
