using System;
using System.Collections.Generic;
using System.Linq;

namespace ECOLearning.ViewModels
{
    /// <summary>
    /// Trạng thái chiến dịch (ChienDich.trangThai, migration 018) và các bước chuyển hợp lệ (đặc tả Marketing mục 4):
    /// Kế hoạch → Đang triển khai → (Tạm dừng) → Hoàn thành; hoặc Đã hủy. Hoàn thành / Đã hủy là trạng thái cuối.
    /// </summary>
    public static class TrangThaiChienDich
    {
        public const string KeHoach = "KeHoach";
        public const string DangTrienKhai = "DangTrienKhai";
        public const string TamDung = "TamDung";
        public const string HoanThanh = "HoanThanh";
        public const string DaHuy = "DaHuy";

        public static readonly string[] TatCa = { KeHoach, DangTrienKhai, TamDung, HoanThanh, DaHuy };

        /// <summary>Kênh triển khai gợi ý (lưu dạng chuỗi phân tách bằng dấu phẩy).</summary>
        public static readonly string[] KenhGoiY = { "Email", "Facebook", "Zalo", "TikTok", "Google Ads", "Website / Trang chủ" };

        public static string Ten(string ma)
        {
            switch (ma)
            {
                case KeHoach: return "Kế hoạch";
                case DangTrienKhai: return "Đang triển khai";
                case TamDung: return "Tạm dừng";
                case HoanThanh: return "Hoàn thành";
                case DaHuy: return "Đã hủy";
                default: return ma;
            }
        }

        public static string Mau(string ma)
        {
            switch (ma)
            {
                case DangTrienKhai: return "badge-success";
                case TamDung: return "badge-warning";
                case HoanThanh: return "badge-secondary";
                case DaHuy: return "badge-danger";
                default: return "badge-info";
            }
        }

        public static bool LaKetThuc(string ma) => ma == HoanThanh || ma == DaHuy;

        /// <summary>Trạng thái chuyển được từ trạng thái hiện tại (kể cả giữ nguyên).</summary>
        public static string[] ChuyenDuoc(string hienTai)
        {
            switch (hienTai)
            {
                case null:
                case "": return new[] { KeHoach, DangTrienKhai };
                case KeHoach: return new[] { KeHoach, DangTrienKhai, DaHuy };
                case DangTrienKhai: return new[] { DangTrienKhai, TamDung, HoanThanh, DaHuy };
                case TamDung: return new[] { TamDung, DangTrienKhai, HoanThanh, DaHuy };
                default: return new[] { hienTai };
            }
        }
    }

    /// <summary>Chiến dịch Marketing (bảng ChienDich, migration 008 + 018) kèm số liệu từ các chương trình khuyến mãi gắn với nó.</summary>
    public class ChienDichItem
    {
        public string MaChienDich { get; set; }
        public string TenChienDich { get; set; }
        public string MucTieu { get; set; }
        public string NoiDung { get; set; }
        public string DoiTuong { get; set; }
        public string KenhTrienKhai { get; set; }
        public decimal NganSach { get; set; }
        public decimal ChiPhiThucTe { get; set; }
        public decimal? ChiTieuDoanhThu { get; set; }
        public int? ChiTieuLuotDung { get; set; }
        public string GhiChu { get; set; }
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }

        /// <summary>KeHoach / DangTrienKhai / TamDung / HoanThanh / DaHuy (TrangThaiChienDich).</summary>
        public string TrangThaiLuu { get; set; }
        public DateTime NgayTao { get; set; }
        public string NguoiTao { get; set; }

        /// <summary>Số liệu toàn thời gian từ các đơn đã thanh toán có dùng mã thuộc chiến dịch.</summary>
        public int SoChuongTrinh { get; set; }
        public int SoLuotDung { get; set; }
        public int SoDonHang { get; set; }
        public decimal TienGiam { get; set; }
        public decimal DoanhThuSauGiam { get; set; }

        public string TrangThaiHienThi => TrangThaiChienDich.Ten(TrangThaiLuu);
        public bool DaKetThuc => TrangThaiChienDich.LaKetThuc(TrangThaiLuu);

        /// <summary>Tổng chi phí = chi phí thực tế + tiền đã giảm cho học viên.</summary>
        public decimal TongChiPhi => ChiPhiThucTe + TienGiam;

        /// <summary>% đạt chỉ tiêu doanh thu (null khi chưa đặt chỉ tiêu).</summary>
        public decimal? PhanTramDatDoanhThu => ChiTieuDoanhThu.HasValue && ChiTieuDoanhThu.Value > 0
            ? Math.Round(DoanhThuSauGiam / ChiTieuDoanhThu.Value * 100, 1) : (decimal?)null;

        public decimal? PhanTramDatLuotDung => ChiTieuLuotDung.HasValue && ChiTieuLuotDung.Value > 0
            ? Math.Round((decimal)SoLuotDung / ChiTieuLuotDung.Value * 100, 1) : (decimal?)null;

        /// <summary>ROI = (Doanh thu − Tổng chi phí) / Tổng chi phí × 100 (null khi tổng chi phí = 0).</summary>
        public decimal? Roi => TongChiPhi > 0 ? Math.Round((DoanhThuSauGiam - TongChiPhi) / TongChiPhi * 100, 1) : (decimal?)null;

        public List<string> DanhSachKenh => (KenhTrienKhai ?? "").Split(',').Select(k => k.Trim()).Where(k => k.Length > 0).ToList();
    }

    public class CampaignFormViewModel
    {
        public string MaChienDich { get; set; }
        public string TenChienDich { get; set; }
        public string MucTieu { get; set; }
        public string NoiDung { get; set; }
        public string DoiTuong { get; set; }

        /// <summary>Kênh đã chọn (checkbox) + kênh khác nhập tay, ghép thành ChienDich.kenhTrienKhai.</summary>
        public List<string> Kenh { get; set; } = new List<string>();
        public string KenhKhac { get; set; }
        public decimal? NganSach { get; set; }
        public decimal? ChiPhiThucTe { get; set; }
        public decimal? ChiTieuDoanhThu { get; set; }
        public int? ChiTieuLuotDung { get; set; }
        public string GhiChu { get; set; }
        public string TrangThai { get; set; } = TrangThaiChienDich.KeHoach;
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }

        /// <summary>Mã các chương trình khuyến mãi liên kết.</summary>
        public List<string> ChuongTrinh { get; set; } = new List<string>();

        public bool LaChinhSua => !string.IsNullOrEmpty(MaChienDich);

        /// <summary>Trạng thái đang lưu (khi sửa) để giới hạn các bước chuyển.</summary>
        public string TrangThaiHienTai { get; set; }
        public List<ChuongTrinhKhuyenMai> ChuongTrinhLuaChon { get; set; } = new List<ChuongTrinhKhuyenMai>();

        public string KenhGhep => string.Join(", ", Kenh.Concat((KenhKhac ?? "").Split(',')).Select(k => k.Trim()).Where(k => k.Length > 0).Distinct());
    }

    /// <summary>Một đơn hàng đã thanh toán trong báo cáo doanh thu theo khoảng ngày.</summary>
    public class DoanhThuDonHang
    {
        public string MaDonHang { get; set; }
        public DateTime NgayDat { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public int SoKhoaHoc { get; set; }

        /// <summary>Tổng giá gốc các khóa (trước giảm trực tiếp và coupon).</summary>
        public decimal TienTruocGiam { get; set; }

        /// <summary>Giảm trực tiếp (khuyến mãi tự áp dụng): giá gốc - đơn giá.</summary>
        public decimal TienGiamTrucTiep { get; set; }

        /// <summary>Giảm bởi mã coupon.</summary>
        public decimal TienGiam { get; set; }

        /// <summary>Số tiền thực thu = trước giảm - giảm trực tiếp - giảm coupon (khớp DonHang.tongTien).</summary>
        public decimal ThucThu { get; set; }

        /// <summary>Phần của giáo viên (80% thực thu) và doanh thu nền tảng (phần còn lại).</summary>
        public decimal PhanGiaoVien { get; set; }
        public decimal NenTang { get; set; }
        public string MaCode { get; set; }
    }

    public class DoanhThuTheoNgay
    {
        public DateTime Ngay { get; set; }
        public int SoDon { get; set; }
        public decimal ThucThu { get; set; }
        public decimal NenTang { get; set; }
    }

    /// <summary>Báo cáo doanh thu theo khoảng ngày (Admin).</summary>
    public class RevenueReportViewModel
    {
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
        public string ThongBaoLoi { get; set; }
        public int SoDonHang { get; set; }
        public decimal TienTruocGiam { get; set; }
        public decimal TienGiamTrucTiep { get; set; }
        public decimal TienGiam { get; set; }
        public decimal ThucThu { get; set; }
        public decimal PhanGiaoVien { get; set; }
        public decimal NenTang { get; set; }
        public List<DoanhThuTheoNgay> TheoNgay { get; set; } = new List<DoanhThuTheoNgay>();
        public List<DoanhThuDonHang> DonHang { get; set; } = new List<DoanhThuDonHang>();
    }
}
