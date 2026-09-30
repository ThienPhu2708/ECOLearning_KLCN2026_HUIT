using System;
using System.Collections.Generic;
using ECOLearning.Models;

namespace ECOLearning.ViewModels
{
    public class InstructorProfileViewModel
    {
        // Thông tin cá nhân từ bảng NguoiDung
        public int MaNguoiDung { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string SoDienThoai { get; set; }
        public string AnhDaiDien { get; set; }
        public string VaiTro { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string GioiTinh { get; set; }

        // Thông tin chuyên môn từ bảng DangKyGiaoVien (chỉ lấy các trường trong CSDL)
        public string MaDon { get; set; }
        public string ChuyenMon { get; set; }
        public string KinhNghiem { get; set; }
        public string TenNganHang { get; set; }
        public string SoTaiKhoan { get; set; }
        public string TenChuTaiKhoan { get; set; }
        public string TepChungChi { get; set; }
        public string TenTepChungChi { get; set; }
        public DateTime? NgayGui { get; set; }
        public DateTime? NgayDuyet { get; set; }
        public string TrangThaiGiaoVien { get; set; }

        // Thống kê thực tế từ CSDL
        public int TongSoHocVien { get; set; }
        public int TongLuotDanhGia { get; set; }
        public double DiemDanhGiaTrungBinh { get; set; }
        public int TongSoKhoaHoc { get; set; }

        // Danh sách khóa học của giáo viên
        public List<KhoaHoc> DanhSachKhoaHoc { get; set; }

        public InstructorProfileViewModel()
        {
            DanhSachKhoaHoc = new List<KhoaHoc>();
        }
    }
}
