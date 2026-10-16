using System;
using System.Collections.Generic;

namespace ECOLearning.ViewModels
{
    /// <summary>Chiến dịch Marketing (bảng ChienDich, migration 008) kèm số liệu từ các chương trình khuyến mãi gắn với nó.</summary>
    public class ChienDichItem
    {
        public string MaChienDich { get; set; }
        public string TenChienDich { get; set; }
        public string MucTieu { get; set; }
        public string NoiDung { get; set; }
        public string DoiTuong { get; set; }
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }

        /// <summary>HoatDong / KetThuc.</summary>
        public string TrangThaiLuu { get; set; }
        public DateTime NgayTao { get; set; }
        public string NguoiTao { get; set; }

        public int SoChuongTrinh { get; set; }
        public int SoLuotDung { get; set; }
        public int SoDonHang { get; set; }
        public decimal TienGiam { get; set; }
        public decimal DoanhThuSauGiam { get; set; }

        public string TrangThaiHienThi(DateTime now)
        {
            if (TrangThaiLuu == "KetThuc" || NgayKetThuc < now) return "Đã kết thúc";
            if (NgayBatDau > now) return "Chưa bắt đầu";
            return "Đang chạy";
        }
    }

    public class CampaignFormViewModel
    {
        public string MaChienDich { get; set; }
        public string TenChienDich { get; set; }
        public string MucTieu { get; set; }
        public string NoiDung { get; set; }
        public string DoiTuong { get; set; }
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }

        /// <summary>Mã các chương trình khuyến mãi liên kết.</summary>
        public List<string> ChuongTrinh { get; set; } = new List<string>();

        public bool LaChinhSua => !string.IsNullOrEmpty(MaChienDich);
        public List<ChuongTrinhKhuyenMai> ChuongTrinhLuaChon { get; set; } = new List<ChuongTrinhKhuyenMai>();
    }

    /// <summary>Một đơn hàng đã thanh toán trong báo cáo doanh thu theo khoảng ngày.</summary>
    public class DoanhThuDonHang
    {
        public string MaDonHang { get; set; }
        public DateTime NgayDat { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public int SoKhoaHoc { get; set; }

        /// <summary>Tổng đơn giá các khóa (trước coupon).</summary>
        public decimal TienTruocGiam { get; set; }
        public decimal TienGiam { get; set; }

        /// <summary>Số tiền thực thu = trước giảm - giảm (khớp DonHang.tongTien).</summary>
        public decimal ThucThu { get; set; }
        public string MaCode { get; set; }
    }

    public class DoanhThuTheoNgay
    {
        public DateTime Ngay { get; set; }
        public int SoDon { get; set; }
        public decimal ThucThu { get; set; }
    }

    /// <summary>Báo cáo doanh thu theo khoảng ngày (Admin).</summary>
    public class RevenueReportViewModel
    {
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
        public string ThongBaoLoi { get; set; }
        public int SoDonHang { get; set; }
        public decimal TienTruocGiam { get; set; }
        public decimal TienGiam { get; set; }
        public decimal ThucThu { get; set; }
        public List<DoanhThuTheoNgay> TheoNgay { get; set; } = new List<DoanhThuTheoNgay>();
        public List<DoanhThuDonHang> DonHang { get; set; } = new List<DoanhThuDonHang>();
    }
}
