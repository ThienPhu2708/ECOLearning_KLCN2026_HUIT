using System;
using System.Collections.Generic;

namespace ECOLearning.Areas.Marketing.Models
{
    public class CouponPerformanceItem
    {
        public string MaCode { get; set; }
        public string TenKhuyenMai { get; set; }
        public double MucUuDai { get; set; }

        /// <summary>VD: "Giảm 25% (tối đa 100.000 đ)" hoặc "Giảm 50.000 đ".</summary>
        public string MoTaUuDai { get; set; }
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
}
