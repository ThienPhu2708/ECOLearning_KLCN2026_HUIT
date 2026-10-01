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
    }
}
