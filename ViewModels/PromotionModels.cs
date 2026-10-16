using System;
using System.Collections.Generic;

namespace ECOLearning.ViewModels
{
    /// <summary>
    /// Một chương trình khuyến mãi kèm coupon và số liệu sử dụng (đọc bằng SQL trực tiếp vì các cột cấu hình
    /// của migration 008 chưa có trong mô hình EF). Tên thuộc tính trùng bí danh cột trong PromotionService.
    /// </summary>
    public class ChuongTrinhKhuyenMai
    {
        public string MaKhuyenMai { get; set; }
        public string TenKhuyenMai { get; set; }

        /// <summary>Giá trị ưu đãi: % (1–100) khi LoaiUuDai = PhanTram, số tiền VNĐ khi = SoTien.</summary>
        public double MucUuDai { get; set; }
        public string LoaiUuDai { get; set; }
        public decimal? GiamToiDa { get; set; }
        public decimal? DonHangToiThieu { get; set; }

        /// <summary>TatCa: mọi khóa học; KhoaHoc: chỉ các khóa trong KhuyenMaiKhoaHoc.</summary>
        public string PhamVi { get; set; }

        /// <summary>TatCa hoặc ThanThiet (kèm tiêu chí số khóa / chi tiêu tối thiểu).</summary>
        public string DoiTuong { get; set; }
        public int? SoKhoaToiThieu { get; set; }
        public decimal? ChiTieuToiThieu { get; set; }

        /// <summary>Trạng thái lưu trong CSDL: HoatDong / TamNgung / KetThuc.</summary>
        public string TrangThaiLuu { get; set; }
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public string DieuKienApDung { get; set; }
        public string MaChienDich { get; set; }
        public string TenChienDich { get; set; }
        public DateTime NgayTao { get; set; }

        /// <summary>true: học viên thấy mã ở giỏ hàng / trang khóa học; false: mã kín, Marketing gửi riêng.</summary>
        public bool CongKhai { get; set; }

        public string MaGiamGia { get; set; }
        public string MaCode { get; set; }
        public bool CouponDangBat { get; set; }
        public int? GioiHanLuotDung { get; set; }
        public int GioiHanMoiHocVien { get; set; }

        /// <summary>Số liệu từ SuDungUuDai (chỉ đơn đã thanh toán).</summary>
        public int SoLuotDaDung { get; set; }
        public decimal TongTienGiam { get; set; }
        public decimal DoanhThuSauGiam { get; set; }
        public int SoKhoaApDung { get; set; }

        public bool LaPhanTram => LoaiUuDai != "SoTien";
        public bool LaThanThiet => DoiTuong == "ThanThiet";
        public bool HetLuot => GioiHanLuotDung.HasValue && SoLuotDaDung >= GioiHanLuotDung.Value;

        /// <summary>Đang chạy / Chưa bắt đầu / Tạm ngưng / Đã kết thúc (tính theo trạng thái lưu và thời gian).</summary>
        public string TrangThaiHienThi(DateTime now)
        {
            if (TrangThaiLuu == "KetThuc" || NgayKetThuc < now) return "Đã kết thúc";
            if (TrangThaiLuu == "TamNgung" || !CouponDangBat) return "Tạm ngưng";
            if (NgayBatDau > now) return "Chưa bắt đầu";
            return "Đang chạy";
        }

        public string MoTaUuDai
        {
            get
            {
                if (!LaPhanTram) return "Giảm " + ((decimal)MucUuDai).ToString("N0") + " đ";
                string s = "Giảm " + MucUuDai.ToString("0.##") + "%";
                if (GiamToiDa.HasValue) s += " (tối đa " + GiamToiDa.Value.ToString("N0") + " đ)";
                return s;
            }
        }

        public string MoTaPhamVi => PhamVi == "KhoaHoc" ? SoKhoaApDung + " khóa học" : "Toàn bộ khóa học";

        public string MoTaDoiTuong
        {
            get
            {
                if (!LaThanThiet) return "Tất cả học viên";
                var dk = new List<string>();
                if (SoKhoaToiThieu.HasValue) dk.Add("từ " + SoKhoaToiThieu.Value + " khóa đã đăng ký");
                if (ChiTieuToiThieu.HasValue) dk.Add("chi tiêu từ " + ChiTieuToiThieu.Value.ToString("N0") + " đ");
                return "Học viên thân thiết" + (dk.Count > 0 ? " (" + string.Join(", ", dk) + ")" : "");
            }
        }
    }

    /// <summary>Một khóa học trong giỏ, giá lấy lại từ CSDL ở server.</summary>
    public class DongGioHang
    {
        public string MaKhoaHoc { get; set; }
        public decimal DonGia { get; set; }
    }

    /// <summary>Kết quả kiểm tra và tính coupon (PromotionService.KiemTraCoupon).</summary>
    public class KetQuaApDungUuDai
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string MaKhuyenMai { get; set; }
        public string MaGiamGia { get; set; }
        public string MaCode { get; set; }

        /// <summary>Tổng giá các khóa được áp dụng ưu đãi (trước giảm).</summary>
        public decimal TienApDung { get; set; }
        public decimal TienGiam { get; set; }
        public int SoKhoaApDung { get; set; }

        public static KetQuaApDungUuDai Loi(string message)
        {
            return new KetQuaApDungUuDai { Success = false, Message = message };
        }
    }

    /// <summary>Số liệu học viên để xét tiêu chí thân thiết.</summary>
    public class HocVienThanThietItem
    {
        public int MaNguoiDung { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }

        /// <summary>Số khóa đã đăng ký thành công (DangKyKhoaHoc).</summary>
        public int SoKhoa { get; set; }

        /// <summary>Tổng tiền các đơn hàng đã thanh toán.</summary>
        public decimal ChiTieu { get; set; }

        /// <summary>Số lần đã dùng coupon của chương trình đang xét.</summary>
        public int SoLuotDaDung { get; set; }
    }

    /// <summary>Form tạo / sửa chương trình khuyến mãi (Marketing). Mọi giá trị được kiểm tra lại ở server.</summary>
    public class PromotionFormViewModel
    {
        public string MaKhuyenMai { get; set; }
        public string TenKhuyenMai { get; set; }
        public string MaChienDich { get; set; }
        public string MaCode { get; set; }
        public string LoaiUuDai { get; set; } = "PhanTram";
        public decimal? GiaTriUuDai { get; set; }
        public decimal? GiamToiDa { get; set; }
        public decimal? DonHangToiThieu { get; set; }
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }
        public int? GioiHanLuotDung { get; set; }
        public int GioiHanMoiHocVien { get; set; } = 1;
        public string PhamVi { get; set; } = "TatCa";
        public List<string> KhoaHocApDung { get; set; } = new List<string>();
        public string DoiTuong { get; set; } = "TatCa";
        public int? SoKhoaToiThieu { get; set; }
        public decimal? ChiTieuToiThieu { get; set; }
        public string DieuKienApDung { get; set; }
        public bool CongKhai { get; set; } = true;

        // Chỉ dùng để hiển thị form
        public bool LaChinhSua => !string.IsNullOrEmpty(MaKhuyenMai);
        public int SoLuotDaDung { get; set; }
        public List<LuaChonKhoaHoc> KhoaHocLuaChon { get; set; } = new List<LuaChonKhoaHoc>();
        public List<LuaChonChienDich> ChienDichLuaChon { get; set; } = new List<LuaChonChienDich>();
    }

    public class LuaChonKhoaHoc
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string TenDanhMuc { get; set; }
        public decimal GiaBan { get; set; }
    }

    public class LuaChonChienDich
    {
        public string MaChienDich { get; set; }
        public string TenChienDich { get; set; }
    }

    /// <summary>
    /// Mã giảm giá công khai hiển thị cho học viên (giỏ hàng, trang chi tiết khóa học).
    /// Chỉ để gợi ý; số tiền giảm thật vẫn tính lại ở server khi áp mã và khi thanh toán.
    /// </summary>
    public class MaGiamGiaHienThi
    {
        public string MaCode { get; set; }
        public string TenKhuyenMai { get; set; }
        public string MoTaUuDai { get; set; }

        /// <summary>Điều kiện ngắn gọn: phạm vi, đơn tối thiểu, thân thiết, ghi chú của Marketing.</summary>
        public List<string> DieuKien { get; set; } = new List<string>();
        public DateTime NgayKetThuc { get; set; }

        /// <summary>Số lượt còn lại của cả chương trình (null = không giới hạn).</summary>
        public int? ConLai { get; set; }
        public bool LaThanThiet { get; set; }

        /// <summary>Dùng được với giỏ hàng / khóa học đang xem hay chưa, kèm số tiền giảm hoặc lý do.</summary>
        public bool CoTheApDung { get; set; }
        public decimal TienGiam { get; set; }
        public string LyDo { get; set; }
    }

    /// <summary>Một lần dùng ưu đãi (chi tiết chương trình / báo cáo).</summary>
    public class LuotSuDungUuDai
    {
        public string MaDonHang { get; set; }
        public DateTime NgaySuDung { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public decimal TienTruocGiam { get; set; }
        public decimal TienGiam { get; set; }
        public decimal TongDonHang { get; set; }
    }
}
