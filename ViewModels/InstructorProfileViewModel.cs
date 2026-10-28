using System;
using System.Collections.Generic;
using ECOLearning.Models;

namespace ECOLearning.ViewModels
{
    public class InstructorProfileViewModel
    {
        /// <summary>Trang hồ sơ chỉ hiện tối đa bấy nhiêu khóa; nhiều hơn thì thu gọn và có nút "Xem tất cả".</summary>
        public const int SoKhoaHocTrenTrangHoSo = 5;

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

        // Danh sách khóa học của giáo viên (kèm số học viên / đánh giá đã đếm sẵn)
        public List<KhoaHocCuaGiaoVienItem> DanhSachKhoaHoc { get; set; }

        public InstructorProfileViewModel()
        {
            DanhSachKhoaHoc = new List<KhoaHocCuaGiaoVienItem>();
        }
    }

    /// <summary>
    /// Một khóa học trên trang giáo viên. Số học viên / lượt đánh giá đếm bằng GROUP BY, không nạp cả
    /// danh sách DangKyKhoaHoc / DanhGiaKhoaHoc (truy vấn đó cần nhiều bộ nhớ, máy thiếu RAM phải chờ lâu).
    /// </summary>
    public class KhoaHocCuaGiaoVienItem
    {
        public KhoaHoc Course { get; set; }
        public int SoHocVien { get; set; }
        public int SoDanhGia { get; set; }
        public double DiemDanhGia { get; set; }

        /// <summary>Giá bán hiệu lực: giá gốc đã trừ giảm trực tiếp (CourseDiscountService); giá gốc là Course.giaBan.</summary>
        public decimal GiaHienTai => ECOLearning.Services.CourseDiscountService.Tinh(Course).Gia;
    }
}
