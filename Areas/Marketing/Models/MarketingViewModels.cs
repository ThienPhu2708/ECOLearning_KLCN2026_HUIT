using System;
using System.Collections.Generic;

namespace ECOLearning.Areas.Marketing.Models
{
    public class CouponPerformanceItem
    {
        public string MaCode { get; set; }
        public string TenKhuyenMai { get; set; }
        public double MucUuDai { get; set; }
        public int SoLuongDung { get; set; }
        public decimal DoanhThu { get; set; }
    }

    public class MarketingDashboardViewModel
    {
        public int ActiveCampaignsCount { get; set; }
        public int TotalCouponsUsed { get; set; }
        public decimal DrivenRevenue { get; set; }
        public List<string> ChartLabels { get; set; }
        public List<int> CouponUsageChartData { get; set; }
        public List<CouponPerformanceItem> TopCoupons { get; set; }

        public MarketingDashboardViewModel()
        {
            ChartLabels = new List<string>();
            CouponUsageChartData = new List<int>();
            TopCoupons = new List<CouponPerformanceItem>();
        }
    }

    public class PromotionItem
    {
        public string MaKhuyenMai { get; set; }
        public string TenKhuyenMai { get; set; }
        public string MaCode { get; set; }
        public double MucUuDai { get; set; }
        public decimal GiaTriGiam { get; set; }
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public string DieuKienApDung { get; set; }
        public string TrangThai { get; set; } // Đang chạy, Tạm ngưng, Đã kết thúc
        public int SoLuongDaDung { get; set; }
    }

    public class PromotionCreateViewModel
    {
        public string TenKhuyenMai { get; set; }
        public string MaCode { get; set; }
        public double MucUuDai { get; set; }
        public decimal GiaTriGiam { get; set; }
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public string DieuKienApDung { get; set; }
    }

    public class CampaignReportRowItem
    {
        public string TenChienDich { get; set; }
        public string MaCode { get; set; }
        public double MucGiam { get; set; }
        public int SoLuotDung { get; set; }
        public decimal TienGiamTru { get; set; }
        public decimal DoanhThuRong { get; set; }
        public double TyLeChuyenDoi { get; set; }
        public string TrangThai { get; set; }
    }

    public class CampaignReportViewModel
    {
        public string SelectedCampaignId { get; set; }
        public string Period { get; set; }
        public int TotalUsed { get; set; }
        public decimal TotalDiscountGiven { get; set; }
        public decimal TotalDrivenRevenue { get; set; }
        public double AvgConversionRate { get; set; }
        public List<string> ChartLabels { get; set; }
        public List<int> ChartData { get; set; }
        public List<CampaignReportRowItem> ReportRows { get; set; }

        public CampaignReportViewModel()
        {
            ChartLabels = new List<string>();
            ChartData = new List<int>();
            ReportRows = new List<CampaignReportRowItem>();
        }
    }
}
