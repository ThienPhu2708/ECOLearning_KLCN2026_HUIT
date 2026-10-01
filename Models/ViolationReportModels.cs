using System;

namespace ECOLearning.Models
{
    // Các lớp ánh xạ kết quả truy vấn SQL cho chức năng Báo cáo vi phạm khóa học.
    // Hai bảng BaoCaoViPham / XuLyViPham không nằm trong file .edmx nên được đọc/ghi bằng SQL thuần
    // (xem Services/ViolationReportService.cs), giống cách làm của bảng DangKyGiaoVien.

    public class ViolationReport
    {
        public string maBaoCao { get; set; }
        public string maKhoaHoc { get; set; }
        public int maNguoiBaoCao { get; set; }
        public string tenNguoiBaoCao { get; set; }
        public string emailNguoiBaoCao { get; set; }
        public string loaiViPham { get; set; }
        public string moTa { get; set; }
        public string maBaiHoc { get; set; }
        public string tenBaiHoc { get; set; }
        public string trangThai { get; set; }
        public DateTime ngayTao { get; set; }
        public DateTime? ngayXuLy { get; set; }
        public string ghiChuXuLy { get; set; }
    }

    public class ViolationAction
    {
        public string maXuLy { get; set; }
        public string maKhoaHoc { get; set; }
        public string hanhDong { get; set; }
        public string lyDo { get; set; }
        public DateTime? hanPhanHoi { get; set; }
        public string trangThaiTruoc { get; set; }
        public int maAdmin { get; set; }
        public string tenAdmin { get; set; }
        public DateTime ngayXuLy { get; set; }
        // Chỉ có ở lần Tạm khóa (script 004): Chờ GV xử lý | Chờ Admin xem xét | Đã kết thúc
        public string trangThaiXuLy { get; set; }
    }

    // Một tin trong lịch sử trao đổi giữa giáo viên và Admin của một lần Tạm khóa (script 004)
    public class ViolationMessage
    {
        public string maPhanHoi { get; set; }
        public string maXuLy { get; set; }
        public int maNguoiGui { get; set; }
        public string tenNguoiGui { get; set; }
        public string vaiTroGui { get; set; }
        public string loai { get; set; }
        public string noiDung { get; set; }
        public DateTime? hanPhanHoiMoi { get; set; }
        public DateTime ngayGui { get; set; }
    }

    public class ViolationCourseSummary
    {
        public string maKhoaHoc { get; set; }
        public string tenKhoaHoc { get; set; }
        public string trangThaiKhoaHoc { get; set; }
        public string tenGiaoVien { get; set; }
        public int soChoXuLy { get; set; }
        public int tongBaoCao { get; set; }
        public DateTime? baoCaoGanNhat { get; set; }
        public DateTime? hanPhanHoi { get; set; }
        public string trangThaiXuLy { get; set; }
    }
}
