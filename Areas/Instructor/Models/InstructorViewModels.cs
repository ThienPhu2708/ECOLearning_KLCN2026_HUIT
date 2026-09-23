using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace ECOLearning.Areas.Instructor.Models
{
    public class TopCourseItem
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string AnhBia { get; set; }
        public int SoHocVien { get; set; }
        public decimal DoanhThu { get; set; }
        public double DiemDanhGia { get; set; }
    }

    public class InstructorDashboardViewModel
    {
        public decimal TotalRevenue { get; set; }
        public decimal MonthRevenue { get; set; }
        public int TotalStudents { get; set; }
        public double AverageRating { get; set; }
        public List<string> ChartLabels { get; set; }
        public List<decimal> RevenueChartData { get; set; }
        public List<TopCourseItem> TopCourses { get; set; }

        public InstructorDashboardViewModel()
        {
            ChartLabels = new List<string>();
            RevenueChartData = new List<decimal>();
            TopCourses = new List<TopCourseItem>();
        }
    }

    public class InstructorCourseItem
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string AnhBia { get; set; }
        public string TenDanhMuc { get; set; }
        public decimal GiaBan { get; set; }
        public decimal? GiaKhuyenMai { get; set; }
        public int SoHocVien { get; set; }
        public string TrangThai { get; set; }
        public DateTime? NgayTao { get; set; }
        public int SoBaiHoc { get; set; }
        public string LyDoTuChoi { get; set; }
    }

    public class CourseCreateOrEditViewModel
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string MoTa { get; set; }
        public string MoTaNgan { get; set; }
        public string MucTieuDauRa { get; set; }
        public decimal GiaBan { get; set; }
        public decimal? GiaKhuyenMai { get; set; }
        public string CapDo { get; set; }
        public string MaDanhMuc { get; set; }
        public string AnhBia { get; set; }
        public string TrangThai { get; set; }
        public IEnumerable<SelectListItem> Categories { get; set; }
    }

    public class TaiNguyenItem
    {
        public string MaTaiNguyen { get; set; }
        public string TenTaiNguyen { get; set; }
        public string DuongDan { get; set; }
        public string LoaiTaiNguyen { get; set; }
    }

    public class BaiHocItem
    {
        public string MaBaiHoc { get; set; }
        public string TenBaiHoc { get; set; }
        public string DuongDanVideo { get; set; }
        public int ThoiLuong { get; set; }
        public bool ChoXemThu { get; set; }
        public int ThuTu { get; set; }
        public List<TaiNguyenItem> TaiNguyens { get; set; }

        public BaiHocItem()
        {
            TaiNguyens = new List<TaiNguyenItem>();
        }
    }

    public class ChuongHocItem
    {
        public string MaChuong { get; set; }
        public string TenChuong { get; set; }
        public int ThuTu { get; set; }
        public List<BaiHocItem> BaiHocs { get; set; }

        public ChuongHocItem()
        {
            BaiHocs = new List<BaiHocItem>();
        }
    }

    public class CurriculumViewModel
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public List<ChuongHocItem> ChuongHocs { get; set; }

        public CurriculumViewModel()
        {
            ChuongHocs = new List<ChuongHocItem>();
        }
    }

    public class InstructorStudentItem
    {
        public int MaHocVien { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string AnhDaiDien { get; set; }
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public DateTime? NgayThamGia { get; set; }
        public double TienDoHoanThanh { get; set; }
        public int? SoSao { get; set; }
    }

    public class InstructorAnalyticsViewModel
    {
        public string Period { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalRegistrations { get; set; }
        public List<string> ChartLabels { get; set; }
        public List<decimal> RevenueData { get; set; }
        public int[] RatingStarCounts { get; set; } // Index 0=1 star .. 4=5 star
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }

        public InstructorAnalyticsViewModel()
        {
            ChartLabels = new List<string>();
            RevenueData = new List<decimal>();
            RatingStarCounts = new int[5];
        }
    }

    public class PayoutItem
    {
        public string MaPhieuChi { get; set; }
        public decimal SoTien { get; set; }
        public decimal ThucNhan { get; set; }
        public DateTime? NgayYeuCau { get; set; }
        public string TrangThai { get; set; }
        public string NganHang { get; set; }
        public string SoTaiKhoan { get; set; }
        public string TenChuTaiKhoan { get; set; }
    }

    public class InstructorPayoutViewModel
    {
        public decimal AvailableBalance { get; set; }
        public decimal PendingBalance { get; set; }
        public decimal TotalPaid { get; set; }
        public List<PayoutItem> PayoutRequests { get; set; }

        public InstructorPayoutViewModel()
        {
            PayoutRequests = new List<PayoutItem>();
        }
    }
}
