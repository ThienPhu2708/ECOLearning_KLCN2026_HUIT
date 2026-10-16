using System;
using System.Collections.Generic;

namespace ECOLearning.Areas.Marketing.Models
{
    /// <summary>Báo cáo hiệu quả Marketing theo khoảng ngày (số liệu từ SuDungUuDai + đơn hàng đã thanh toán).</summary>
    public class MarketingReportViewModel
    {
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
        public string MaKhuyenMai { get; set; }
        public string MaChienDich { get; set; }
        public string ThongBaoLoi { get; set; }

        public int SoChuongTrinh { get; set; }
        public int SoLuotDung { get; set; }
        public int SoDonHang { get; set; }

        /// <summary>Tổng tiền các đơn liên quan trước khi trừ ưu đãi.</summary>
        public decimal DoanhThuTruocGiam { get; set; }
        public decimal TienGiam { get; set; }
        public decimal DoanhThuSauGiam { get; set; }

        public List<MarketingReportRow> Rows { get; set; } = new List<MarketingReportRow>();

        /// <summary>Lượt dùng ưu đãi theo ngày (hoặc theo tháng khi khoảng dài hơn 62 ngày).</summary>
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<int> ChartData { get; set; } = new List<int>();
        public bool ChartTheoThang { get; set; }
    }

    public class MarketingReportRow
    {
        public string MaKhuyenMai { get; set; }
        public string TenKhuyenMai { get; set; }
        public string MaCode { get; set; }
        public string TenChienDich { get; set; }
        public string MoTaUuDai { get; set; }
        public string TrangThai { get; set; }
        public int SoLuotDung { get; set; }
        public decimal DoanhThuTruocGiam { get; set; }
        public decimal TienGiam { get; set; }
        public decimal DoanhThuSauGiam { get; set; }
    }

    /// <summary>Một lần dùng ưu đãi trong khoảng ngày (đọc bằng SQL trực tiếp).</summary>
    public class MarketingUsageRow
    {
        public string MaKhuyenMai { get; set; }
        public DateTime NgaySuDung { get; set; }
        public decimal TienGiam { get; set; }
        public decimal TongDonHang { get; set; }
    }
}
