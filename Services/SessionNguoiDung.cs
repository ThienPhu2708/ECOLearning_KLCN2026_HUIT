using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Bản sao nhẹ của NguoiDung để lưu vào Session["User"].
    /// Thực thể lấy từ db.NguoiDung.Find(...) là proxy (EDMX bật LazyLoadingEnabled) giữ tham chiếu tới DbContext và mọi thực thể
    /// context đó đã nạp trong request (khóa học, bài học, đăng ký...). Session lưu trong bộ nhớ nên các đối tượng này ở lại đến khi phiên
    /// hết hạn (20 phút) cho từng người dùng, làm app pool vượt giới hạn bộ nhớ của hosting và bị tái chế (mọi người bị đăng xuất).
    /// Bản sao chỉ có cột thường, không có danh sách liên quan, không có mã băm mật khẩu. Nơi đọc Session["User"] chỉ cần maNguoiDung,
    /// trangThai và thông tin hiển thị (chứng chỉ).
    /// </summary>
    public static class SessionNguoiDung
    {
        /// <summary>Session["Avatar"] khi người dùng chưa có ảnh (đăng nhập và khôi phục phiên dùng chung).</summary>
        public const string AnhDaiDienMacDinh = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=100&auto=format&fit=crop&q=80";

        public static NguoiDung BanSao(NguoiDung u)
        {
            if (u == null) return null;
            return new NguoiDung
            {
                maNguoiDung = u.maNguoiDung,
                hoTen = u.hoTen,
                email = u.email,
                vaiTro = u.vaiTro,
                trangThai = u.trangThai,
                ngaySinh = u.ngaySinh,
                soDienThoai = u.soDienThoai,
                gioiTinh = u.gioiTinh,
                anhDaiDien = u.anhDaiDien
            };
        }
    }
}
