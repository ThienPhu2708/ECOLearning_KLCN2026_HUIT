using System;
using System.Collections.Generic;

namespace ECOLearning.Areas.Admin.Models
{
    public class DonutSegmentItem
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public string Color { get; set; }
    }

    public class PendingCourseItem
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string TenGiaoVien { get; set; }
        public string TenDanhMuc { get; set; }
        public DateTime? NgayGui { get; set; }
        public decimal HocPhi { get; set; }
        public decimal? GiaKhuyenMai { get; set; }
        public int SoVideo { get; set; }
        public int SoTaiLieu { get; set; }
        public string TrangThai { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public decimal TotalRevenue { get; set; }
        public int TotalUsers { get; set; }
        public int TotalStudents { get; set; }
        public int TotalInstructors { get; set; }
        public int ActiveCoursesCount { get; set; }
        public int PendingCoursesCount { get; set; }
        public List<PendingCourseItem> PendingCourses { get; set; }
        public List<int> MonthlyGrowthData { get; set; }
        public List<string> MonthlyGrowthLabels { get; set; }
        public List<DonutSegmentItem> CategoryDonutSegments { get; set; }

        public AdminDashboardViewModel()
        {
            PendingCourses = new List<PendingCourseItem>();
            MonthlyGrowthData = new List<int>();
            MonthlyGrowthLabels = new List<string>();
            CategoryDonutSegments = new List<DonutSegmentItem>();
        }
    }

    public class CategoryViewModel
    {
        public string MaDanhMuc { get; set; }
        public string TenDanhMuc { get; set; }
        public string MoTa { get; set; }
        public int SoKhoaHoc { get; set; }
        public bool TrangThai { get; set; }
    }

    public class TaiNguyenDetail
    {
        public string MaTaiNguyen { get; set; }
        public string TenTaiNguyen { get; set; }
        public string FileUrl { get; set; }
        public string LoaiFile { get; set; }
    }

    public class BaiHocDetail
    {
        public string MaBaiHoc { get; set; }
        public string TenBaiHoc { get; set; }
        public string VideoUrl { get; set; }
        public int? ThoiLuong { get; set; }
        public bool? CoXemThu { get; set; }
        public List<TaiNguyenDetail> TaiNguyens { get; set; }

        public BaiHocDetail()
        {
            TaiNguyens = new List<TaiNguyenDetail>();
        }
    }

    public class ChuongHocDetail
    {
        public string MaChuong { get; set; }
        public string TenChuong { get; set; }
        public List<BaiHocDetail> BaiHocs { get; set; }

        public ChuongHocDetail()
        {
            BaiHocs = new List<BaiHocDetail>();
        }
    }

    public class CourseApprovalDetailViewModel
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string TenGiaoVien { get; set; }
        public string EmailGiaoVien { get; set; }
        public string TenDanhMuc { get; set; }
        public decimal HocPhi { get; set; }
        public decimal? GiaKhuyenMai { get; set; }
        public string MoTa { get; set; }
        public string MoTaNgan { get; set; }
        public string MucTieuDauRa { get; set; }
        public string AnhBia { get; set; }
        public string TrangThai { get; set; }
        public DateTime? NgayGui { get; set; }
        public string LyDoTuChoi { get; set; }
        public List<ChuongHocDetail> ChuongHocs { get; set; }

        public CourseApprovalDetailViewModel()
        {
            ChuongHocs = new List<ChuongHocDetail>();
        }
    }

    public class PeriodReportRow
    {
        public string GiaiDoan { get; set; }
        public decimal DoanhThu { get; set; }
        public int SoDonHang { get; set; }
        public decimal GiaTriDonTrungBinh { get; set; }
        public int HocVienMoi { get; set; }
        public string TangTruong { get; set; }
    }

    public class SystemReportViewModel
    {
        public string Period { get; set; }
        public decimal TotalNetRevenue { get; set; }
        public int TotalOrders { get; set; }
        public decimal AvgOrderValue { get; set; }
        public int NewStudentsCount { get; set; }
        public double GrowthPercentage { get; set; }
        public List<decimal> RevenueChartData { get; set; }
        public List<int> OrderChartData { get; set; }
        public List<string> ChartLabels { get; set; }
        public List<DonutSegmentItem> CategoryRevenueSegments { get; set; }
        public List<PeriodReportRow> PeriodReports { get; set; }

        public SystemReportViewModel()
        {
            RevenueChartData = new List<decimal>();
            OrderChartData = new List<int>();
            ChartLabels = new List<string>();
            CategoryRevenueSegments = new List<DonutSegmentItem>();
            PeriodReports = new List<PeriodReportRow>();
        }
    }
}
