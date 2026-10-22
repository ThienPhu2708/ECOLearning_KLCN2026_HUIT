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

    /// <summary>Dashboard Marketing (đặc tả mục 11). Chỉ tính đơn đã thanh toán có dùng mã.</summary>
    public class MarketingDashboardViewModel
    {
        /// <summary>Số chương trình khuyến mãi đang chạy.</summary>
        public int ActiveCampaignsCount { get; set; }

        /// <summary>Số chiến dịch Đang triển khai.</summary>
        public int ChienDichDangTrienKhai { get; set; }

        /// <summary>Lượt dùng / doanh thu / tiền giảm trong tháng này.</summary>
        public int TotalCouponsUsed { get; set; }
        public decimal DrivenRevenue { get; set; }
        public decimal TienGiamThangNay { get; set; }

        /// <summary>12 tháng năm nay: lượt dùng và doanh thu.</summary>
        public List<string> ChartLabels { get; set; }
        public List<int> CouponUsageChartData { get; set; }
        public List<decimal> RevenueChartData { get; set; }
        public List<CouponPerformanceItem> TopCoupons { get; set; }

        /// <summary>5 mã đang chạy sắp hết hạn (≤ 7 ngày) hoặc sắp hết lượt (≥ 80%).</summary>
        public List<CanhBaoMa> MaCanChuY { get; set; }

        /// <summary>5 chiến dịch đang triển khai sắp kết thúc.</summary>
        public List<ECOLearning.ViewModels.ChienDichItem> ChienDichSapKetThuc { get; set; }

        public MarketingDashboardViewModel()
        {
            ChartLabels = new List<string>();
            CouponUsageChartData = new List<int>();
            RevenueChartData = new List<decimal>();
            TopCoupons = new List<CouponPerformanceItem>();
            MaCanChuY = new List<CanhBaoMa>();
            ChienDichSapKetThuc = new List<ECOLearning.ViewModels.ChienDichItem>();
        }
    }

    public class CanhBaoMa
    {
        public string MaKhuyenMai { get; set; }
        public string MaCode { get; set; }
        public string TenKhuyenMai { get; set; }

        /// <summary>"Hết hạn sau 3 ngày" / "Đã dùng 45/50 lượt (90%)".</summary>
        public string LyDo { get; set; }
    }
}
