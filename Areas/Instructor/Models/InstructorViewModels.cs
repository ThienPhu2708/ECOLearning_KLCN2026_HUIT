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
        public int TotalReviews { get; set; }
        public int NewStudentsThisMonth { get; set; }
        public List<string> ChartLabels { get; set; }
        public List<decimal> RevenueChartData { get; set; }
        public List<TopCourseItem> TopCourses { get; set; }
        // Phân bổ đánh giá: index 0 = 5 sao, index 4 = 1 sao
        public int[] RatingDistribution { get; set; }
        public List<InstructorCourseItem> RecentCourses { get; set; }

        public InstructorDashboardViewModel()
        {
            ChartLabels = new List<string>();
            RevenueChartData = new List<decimal>();
            TopCourses = new List<TopCourseItem>();
            RatingDistribution = new int[5];
            RecentCourses = new List<InstructorCourseItem>();
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
        public bool CoVideo { get; set; }
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
        public List<CourseChapterInput> Chapters { get; set; } = new List<CourseChapterInput>();
        public List<CourseLessonInput> Lessons { get; set; } = new List<CourseLessonInput>();
        public System.Web.HttpPostedFileBase CoverImageFile { get; set; }
        public IEnumerable<SelectListItem> Categories { get; set; }
    }

    public class CourseChapterInput
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }
    public class CourseLessonInput
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public int? ChapterIndex { get; set; }
        public bool Preview { get; set; }
        public string VideoToken { get; set; }
        public int Duration { get; set; }
        public List<string> AttachmentTokens { get; set; } = new List<string>();
        public System.Web.HttpPostedFileBase VideoFile { get; set; }
        public System.Web.HttpPostedFileBase AttachmentFile { get; set; }
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
        public string TrangThai { get; set; }
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public List<ChuongHocItem> ChuongHocs { get; set; }
        public List<BaiHocItem> BaiHocKhongChuong { get; set; }

        public CurriculumViewModel()
        {
            ChuongHocs = new List<ChuongHocItem>();
            BaiHocKhongChuong = new List<BaiHocItem>();
        }
    }

    public class InstructorStudentItem
    {
        public string QuizStatus { get; set; }
        public decimal? QuizScore { get; set; }
        public string CertificateStatus { get; set; }
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

    public class CourseAnalyticsItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public decimal Revenue { get; set; }
        public int Registrations { get; set; }
        public int Reviews { get; set; }
        public double? AverageProgress { get; set; }
    }

    public class InstructorAnalyticsViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalStudents { get; set; }
        public List<int> RegistrationData { get; set; } = new List<int>();
        public List<CourseAnalyticsItem> Courses { get; set; } = new List<CourseAnalyticsItem>();
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

        /// <summary>Admin xử lý (migration 014): thời điểm, mã giao dịch chi thử nghiệm, lý do từ chối.</summary>
        public DateTime? NgayXuLy { get; set; }
        public string MaGiaoDichChi { get; set; }
        public string LyDoTuChoi { get; set; }
    }

    public class InstructorPayoutViewModel
    {
        /// <summary>Doanh thu thực thu các khóa của giáo viên (đơn đã thanh toán, sau khi trừ mã giảm giá).</summary>
        public decimal TongDoanhThuThucThu { get; set; }
        /// <summary>Phần giáo viên được hưởng (80% doanh thu thực thu).</summary>
        public decimal PhanGiaoVien { get; set; }
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
