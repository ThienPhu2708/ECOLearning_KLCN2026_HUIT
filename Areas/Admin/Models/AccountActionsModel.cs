namespace ECOLearning.Areas.Admin.Models
{
    /// <summary>Dữ liệu cho partial _AccountActions: các nút khóa / mở khóa / cảnh cáo / đặt lại mật khẩu.</summary>
    public class AccountActionsModel
    {
        public int MaNguoiDung { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string VaiTro { get; set; }
        public bool HoatDong { get; set; }
        public bool LaChinhMinh { get; set; }

        /// <summary>Trang quay về sau khi thao tác (trang chi tiết đang mở).</summary>
        public string ReturnUrl { get; set; }
    }
}
