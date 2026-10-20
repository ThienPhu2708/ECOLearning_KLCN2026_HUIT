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
        public int PendingViolationReports { get; set; }
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

        /// <summary>Đường dẫn ảnh minh họa (DanhMuc.anhMinhHoa, migration 012), null nếu chưa có.</summary>
        public string AnhMinhHoa { get; set; }
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

    public class CourseStatusStatItem
    {
        public string Label { get; set; }
        public int Count { get; set; }
        public double Percent { get; set; }
        public string Color { get; set; }
    }

    public class TeacherRevenueRow
    {
        public int TeacherId { get; set; }
        public string TeacherName { get; set; }
        public int CourseCount { get; set; }
        public int PublishedCourseCount { get; set; }
        public int Orders { get; set; }
        public decimal Revenue { get; set; }
    }

    public class SystemReportViewModel
    {
        // Bộ lọc "Phân tích dữ liệu": theo danh mục / giáo viên (rỗng = toàn hệ thống)
        public string FilterCategory { get; set; } = "";
        public int? FilterTeacherId { get; set; }
        public string FilterLabel { get; set; } = "Toàn bộ hệ thống";
        public List<System.Web.Mvc.SelectListItem> CategoryOptions { get; set; } = new List<System.Web.Mvc.SelectListItem>();
        public List<System.Web.Mvc.SelectListItem> TeacherOptions { get; set; } = new List<System.Web.Mvc.SelectListItem>();

        // Doanh thu theo giáo viên
        public List<TeacherRevenueRow> TeacherRevenueRows { get; set; } = new List<TeacherRevenueRow>();

        // Cơ cấu doanh thu: theo khóa học (khi lọc 1 danh mục) / tính theo doanh thu hay theo số khóa học
        public bool CategorySegmentsByCourse { get; set; }
        public bool CategorySegmentsByRevenue { get; set; }

        // Thống kê giáo viên
        public int TotalInstructors { get; set; }
        public int InstructorsWithPublishedCourse { get; set; }
        public int PendingTeacherApplications { get; set; }

        // Thống kê khóa học theo trạng thái
        public int TotalCourses { get; set; }
        public List<CourseStatusStatItem> CourseStatusStats { get; set; } = new List<CourseStatusStatItem>();

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
