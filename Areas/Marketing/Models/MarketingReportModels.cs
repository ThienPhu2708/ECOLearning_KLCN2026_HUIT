using System;
using System.Collections.Generic;

namespace ECOLearning.Areas.Marketing.Models
{
    /// <summary>
    /// Báo cáo hiệu quả Marketing theo khoảng ngày (đặc tả mục 11). Chỉ tính đơn đã thanh toán có dùng mã, theo ngày thanh toán.
    /// Doanh thu toàn sàn thuộc báo cáo của Admin.
    /// </summary>
    public class MarketingReportViewModel
    {
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
        public string MaKhuyenMai { get; set; }
        public string MaChienDich { get; set; }
        public string ThongBaoLoi { get; set; }

        public int SoChuongTrinh { get; set; }

        /// <summary>Lượt dùng mã = số đơn đã thanh toán có mã trong kỳ (mỗi đơn tối đa 1 mã).</summary>
        public int SoLuotDung { get; set; }
        public int SoHocVien { get; set; }
        public int SoLuotThanThiet { get; set; }

        /// <summary>Doanh thu từ đơn có mã = tổng tongTien (sau giảm).</summary>
        public decimal DoanhThuSauGiam { get; set; }
        public decimal TienGiam { get; set; }
        public decimal DoanhThuTruocGiam => DoanhThuSauGiam + TienGiam;
        public decimal GiaTriDonTrungBinh => SoLuotDung > 0 ? Math.Round(DoanhThuSauGiam / SoLuotDung) : 0;

        /// <summary>Tổng đơn đã thanh toán trong kỳ (có hoặc không có mã) để tính tỷ lệ đơn dùng mã.</summary>
        public int TongDonDaThanhToan { get; set; }
        public decimal TyLeDonCoMa => TongDonDaThanhToan > 0 ? Math.Round((decimal)SoLuotDung / TongDonDaThanhToan * 100, 1) : 0;

        public List<MarketingReportRow> Rows { get; set; } = new List<MarketingReportRow>();
        public List<MarketingCampaignRow> ChienDichRows { get; set; } = new List<MarketingCampaignRow>();
        public List<MarketingUsageRow> ChiTiet { get; set; } = new List<MarketingUsageRow>();

        /// <summary>Lượt dùng và doanh thu theo ngày (khoảng dài thì theo tháng / năm).</summary>
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<int> ChartData { get; set; } = new List<int>();
        public List<decimal> ChartDoanhThu { get; set; } = new List<decimal>();

        /// <summary>ngày / tháng / năm.</summary>
        public string ChartDonVi { get; set; } = "ngày";
    }

    public class MarketingReportRow
    {
        public string MaKhuyenMai { get; set; }
        public string TenKhuyenMai { get; set; }
        public string MaCode { get; set; }
        public string TenChienDich { get; set; }
        public string MoTaUuDai { get; set; }
        public string TrangThai { get; set; }
        public bool LaThanThiet { get; set; }
        public int SoLuotDung { get; set; }
        public decimal DoanhThuTruocGiam { get; set; }
        public decimal TienGiam { get; set; }
        public decimal DoanhThuSauGiam { get; set; }
    }

    /// <summary>Một chiến dịch trong kỳ báo cáo: % đạt chỉ tiêu và ROI tính theo số liệu trong kỳ.</summary>
    public class MarketingCampaignRow
    {
        public string MaChienDich { get; set; }
        public string TenChienDich { get; set; }
        public string TrangThai { get; set; }
        public int SoLuotDung { get; set; }
        public decimal TienGiam { get; set; }
        public decimal DoanhThu { get; set; }
        public decimal ChiPhiThucTe { get; set; }
        public decimal? ChiTieuDoanhThu { get; set; }

        public decimal TongChiPhi => ChiPhiThucTe + TienGiam;
        public decimal? PhanTramDatChiTieu => ChiTieuDoanhThu.HasValue && ChiTieuDoanhThu.Value > 0 ? Math.Round(DoanhThu / ChiTieuDoanhThu.Value * 100, 1) : (decimal?)null;
        public decimal? Roi => TongChiPhi > 0 ? Math.Round((DoanhThu - TongChiPhi) / TongChiPhi * 100, 1) : (decimal?)null;
    }

    /// <summary>Một lượt dùng mã (đơn đã thanh toán) trong kỳ, đọc bằng SQL trực tiếp.</summary>
    public class MarketingUsageRow
    {
        public string MaDonHang { get; set; }
        public string MaKhuyenMai { get; set; }
        public string MaChienDich { get; set; }
        public string MaCode { get; set; }
        public int MaHocVien { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public bool LaThanThiet { get; set; }
        public DateTime NgaySuDung { get; set; }
        public decimal TienGiam { get; set; }
        public decimal TongDonHang { get; set; }
    }
}
