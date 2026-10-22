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

        /// <summary>TatCa (toàn sàn) / DanhMuc (khóa thuộc MaDanhMucApDung) / KhoaHoc (các khóa trong KhuyenMaiKhoaHoc).</summary>
        public string PhamVi { get; set; }
        public string MaDanhMucApDung { get; set; }
        public string TenDanhMucApDung { get; set; }

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

        /// <summary>ChienDich.trangThai (migration 018): KeHoach / DangTrienKhai / TamDung / HoanThanh / DaHuy.</summary>
        public string TrangThaiChienDich { get; set; }
        public DateTime NgayTao { get; set; }

        /// <summary>true: học viên thấy mã ở trang chủ / trang khóa học để lưu vào ví; false: mã kín, Marketing gửi riêng.</summary>
        public bool CongKhai { get; set; }

        /// <summary>Mô tả chính sách Marketing viết cho học viên (migration 009).</summary>
        public string ChinhSach { get; set; }

        public string MaGiamGia { get; set; }
        public string MaCode { get; set; }
        public bool CouponDangBat { get; set; }

        /// <summary>MaGiamGia.giaTriGiam: số tiền giảm khi LoaiUuDai = SoTien.</summary>
        public decimal GiaTriGiam { get; set; }
        public int? GioiHanLuotDung { get; set; }

        /// <summary>Số lần một học viên được dùng; null = không giới hạn (migration 018).</summary>
        public int? GioiHanMoiHocVien { get; set; }

        /// <summary>Số liệu từ SuDungUuDai (chỉ đơn đã thanh toán).</summary>
        public int SoLuotDaDung { get; set; }
        public decimal TongTienGiam { get; set; }
        public decimal DoanhThuSauGiam { get; set; }
        public int SoKhoaApDung { get; set; }

        /// <summary>Số học viên đã lưu mã vào ví.</summary>
        public int SoHocVienDaLuu { get; set; }

        public bool LaPhanTram => LoaiUuDai != "SoTien";
        public bool LaThanThiet => DoiTuong == "ThanThiet";
        public bool HetLuot => GioiHanLuotDung.HasValue && SoLuotDaDung >= GioiHanLuotDung.Value;

        /// <summary>Số tiền giảm của mã giảm cố định (MaGiamGia.giaTriGiam; dữ liệu cũ thiếu thì lấy mucUuDai).</summary>
        public decimal SoTienGiam => GiaTriGiam > 0 ? GiaTriGiam : (decimal)MucUuDai;

        /// <summary>Chiến dịch Tạm dừng / Đã hủy: mọi mã thuộc chiến dịch không lưu, không dùng được.</summary>
        public bool ChienDichChan => TrangThaiChienDich == "TamDung" || TrangThaiChienDich == "DaHuy";

        public string LyDoChienDichChan => TrangThaiChienDich == "DaHuy"
            ? "Chiến dịch \"" + TenChienDich + "\" của mã này đã bị hủy!"
            : "Chiến dịch \"" + TenChienDich + "\" của mã này đang tạm dừng!";

        public bool DaKetThuc(DateTime now) => TrangThaiLuu == "KetThuc" || NgayKetThuc < now;
        public bool DangTamNgung => TrangThaiLuu == "TamNgung" || !CouponDangBat;

        /// <summary>
        /// Trạng thái hiển thị, tính theo đặc tả mục 7 (xét theo thứ tự, gặp điều kiện nào trước thì lấy):
        /// Đã kết thúc → Tạm ngưng → Hết lượt → Chưa bắt đầu → Đang chạy.
        /// </summary>
        public string TrangThaiHienThi(DateTime now)
        {
            if (DaKetThuc(now)) return "Đã kết thúc";
            if (DangTamNgung) return "Tạm ngưng";
            if (HetLuot) return "Hết lượt";
            if (NgayBatDau > now) return "Chưa bắt đầu";
            return "Đang chạy";
        }

        public string MoTaUuDai
        {
            get
            {
                if (!LaPhanTram) return "Giảm " + SoTienGiam.ToString("N0") + " đ";
                string s = "Giảm " + MucUuDai.ToString("0.##") + "%";
                if (GiamToiDa.HasValue) s += " (tối đa " + GiamToiDa.Value.ToString("N0") + " đ)";
                return s;
            }
        }

        public string MoTaPhamVi => PhamVi == "KhoaHoc" ? SoKhoaApDung + " khóa học"
            : PhamVi == "DanhMuc" ? "Danh mục " + (TenDanhMucApDung ?? MaDanhMucApDung)
            : "Toàn sàn";

        public string MoTaTieuChiThanThiet
        {
            get
            {
                var dk = new List<string>();
                if (SoKhoaToiThieu.HasValue) dk.Add("đã mua từ " + SoKhoaToiThieu.Value + " khóa học");
                if (ChiTieuToiThieu.HasValue) dk.Add("chi tiêu từ " + ChiTieuToiThieu.Value.ToString("N0") + " đ");
                return string.Join(", ", dk);
            }
        }

        public string MoTaDoiTuong => !LaThanThiet ? "Tất cả học viên"
            : "Học viên thân thiết" + (MoTaTieuChiThanThiet.Length > 0 ? " (" + MoTaTieuChiThanThiet + ")" : "");
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

        /// <summary>Mã các khóa trong giỏ thuộc phạm vi mã.</summary>
        public List<string> KhoaApDung { get; set; } = new List<string>();

        /// <summary>Mã có giới hạn mỗi người / mã thân thiết mà chưa đăng nhập (đặc tả mục 9, bước 8).</summary>
        public bool RequireLogin { get; set; }

        /// <summary>Thông tin chương trình khi tìm thấy mã (kể cả khi không áp dụng được), dùng cho thẻ mã của API.</summary>
        public ChuongTrinhKhuyenMai ChuongTrinh { get; set; }

        public static KetQuaApDungUuDai Loi(string message, ChuongTrinhKhuyenMai p = null, bool requireLogin = false)
        {
            return new KetQuaApDungUuDai
            {
                Success = false,
                Message = message,
                ChuongTrinh = p,
                MaCode = p?.MaCode,
                RequireLogin = requireLogin
            };
        }
    }

    /// <summary>Số liệu học viên để xét tiêu chí thân thiết.</summary>
    public class HocVienThanThietItem
    {
        public int MaNguoiDung { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }

        /// <summary>Số khóa học khác nhau đã mua trong các đơn đã thanh toán (khóa miễn phí đăng ký trực tiếp không tính).</summary>
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

        /// <summary>Bỏ trống = không giới hạn số lần mỗi học viên.</summary>
        public int? GioiHanMoiHocVien { get; set; } = 1;
        public string PhamVi { get; set; } = "TatCa";
        public string MaDanhMucApDung { get; set; }
        public List<string> KhoaHocApDung { get; set; } = new List<string>();
        public string DoiTuong { get; set; } = "TatCa";
        public int? SoKhoaToiThieu { get; set; }
        public decimal? ChiTieuToiThieu { get; set; }
        public string DieuKienApDung { get; set; }
        public bool CongKhai { get; set; } = true;
        public string ChinhSach { get; set; }

        // Chỉ dùng để hiển thị form
        public bool LaChinhSua => !string.IsNullOrEmpty(MaKhuyenMai);
        public int SoLuotDaDung { get; set; }
        public List<LuaChonKhoaHoc> KhoaHocLuaChon { get; set; } = new List<LuaChonKhoaHoc>();
        public List<LuaChonChienDich> ChienDichLuaChon { get; set; } = new List<LuaChonChienDich>();
        public List<LuaChonDanhMuc> DanhMucLuaChon { get; set; } = new List<LuaChonDanhMuc>();
    }

    public class LuaChonDanhMuc
    {
        public string MaDanhMuc { get; set; }
        public string TenDanhMuc { get; set; }
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
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }
    }

    /// <summary>
    /// Mã giảm giá công khai hiển thị cho học viên (giỏ hàng, trang chi tiết khóa học).
    /// Chỉ để gợi ý; số tiền giảm thật vẫn tính lại ở server khi áp mã và khi thanh toán.
    /// </summary>
    public class MaGiamGiaHienThi
    {
        public string MaKhuyenMai { get; set; }
        public string MaGiamGia { get; set; }
        public string MaCode { get; set; }
        public string TenKhuyenMai { get; set; }
        public string MoTaUuDai { get; set; }

        /// <summary>Giá trị ngắn cho cuống vé: "25%", "50K".</summary>
        public string GiaTriNgan { get; set; }

        /// <summary>Dòng phụ dưới giá trị ngắn: "Tối đa 50K" (chỉ giảm %).</summary>
        public string GhiChuGiaTri { get; set; }

        /// <summary>Đã nằm trong ví của học viên đang xem.</summary>
        public bool DaLuu { get; set; }

        /// <summary>Điều kiện ngắn gọn: phạm vi, đơn tối thiểu, thân thiết, số lượt mỗi tài khoản.</summary>
        public List<string> DieuKien { get; set; } = new List<string>();
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }

        /// <summary>Số lượt còn lại của cả chương trình (null = không giới hạn).</summary>
        public int? ConLai { get; set; }
        public bool LaThanThiet { get; set; }

        /// <summary>Nhãn "Mã công khai" / "Mã riêng" (KhuyenMai.congKhai).</summary>
        public bool CongKhai { get; set; }

        /// <summary>Đang chạy và còn ≤ 3 ngày là hết hạn (nhãn "Sắp hết hạn").</summary>
        public bool SapHetHan { get; set; }

        /// <summary>Chưa tới ngày bắt đầu (lưu được nhưng chưa dùng được).</summary>
        public bool ChuaBatDau { get; set; }

        /// <summary>Dùng được với giỏ hàng / khóa học đang xem hay chưa, kèm số tiền giảm hoặc lý do.</summary>
        public bool CoTheApDung { get; set; }
        public decimal TienGiam { get; set; }
        public string LyDo { get; set; }
    }

    /// <summary>Một mã trong ví "Mã giảm giá của tôi".</summary>
    public class MaTrongVi
    {
        public MaGiamGiaHienThi Ma { get; set; }
        public DateTime NgayLuu { get; set; }
        public int LuotCuaToi { get; set; }

        /// <summary>null = không giới hạn số lần mỗi học viên.</summary>
        public int? GioiHanMoiHocVien { get; set; }

        /// <summary>CoTheDung / SapDienRa / DaDung / HetHieuLuc.</summary>
        public string TrangThai { get; set; }

        /// <summary>
        /// Nhãn trạng thái / lý do hết hiệu lực: "Có thể dùng", "Chưa bắt đầu", "Đã dùng", "Hết hạn", "Đã kết thúc", "Hết lượt",
        /// "Tạm ngưng", "Chiến dịch tạm dừng", "Chiến dịch đã hủy".
        /// </summary>
        public string NhanTrangThai { get; set; }

        /// <summary>Đơn đã thanh toán có dùng mã này của học viên (tab "Đã dùng").</summary>
        public List<LuotSuDungUuDai> LichSu { get; set; } = new List<LuotSuDungUuDai>();
    }

    /// <summary>Ưu đãi thân thiết học viên chưa đạt tiêu chí: hiện tiến độ để khuyến khích mua thêm, không lộ mã.</summary>
    public class UuDaiThanThietSapMo
    {
        public string TenKhuyenMai { get; set; }
        public string GiaTriNgan { get; set; }
        public string MoTaUuDai { get; set; }

        /// <summary>"Cần mua thêm 1 khóa học (đã mua 1/2)".</summary>
        public string TienDo { get; set; }
    }

    /// <summary>Trang "Mã giảm giá của tôi".</summary>
    public class ViMaGiamGiaViewModel
    {
        /// <summary>codung / dadung / hethan / thuthap.</summary>
        public string Tab { get; set; }
        public List<MaTrongVi> CoTheDung { get; set; } = new List<MaTrongVi>();
        public List<MaTrongVi> DaDung { get; set; } = new List<MaTrongVi>();
        public List<MaTrongVi> HetHieuLuc { get; set; } = new List<MaTrongVi>();

        /// <summary>Mã công khai lưu được (đang chạy / chưa bắt đầu) mà học viên chưa lưu.</summary>
        public List<MaGiamGiaHienThi> CoTheThuThap { get; set; } = new List<MaGiamGiaHienThi>();

        /// <summary>Khối "Ưu đãi thân thiết sắp mở".</summary>
        public List<UuDaiThanThietSapMo> ThanThietSapMo { get; set; } = new List<UuDaiThanThietSapMo>();
        public bool GioHangCoKhoaHoc { get; set; }
    }

    /// <summary>Trang chi tiết mã giảm giá kèm chính sách.</summary>
    public class ChiTietMaGiamGiaViewModel
    {
        public ChuongTrinhKhuyenMai ChuongTrinh { get; set; }
        public MaGiamGiaHienThi The { get; set; }
        public MaTrongVi TrongVi { get; set; }

        /// <summary>Còn lưu được (công khai, đang chạy, đủ điều kiện, chưa lưu).</summary>
        public bool CoTheLuu { get; set; }

        /// <summary>Các điều khoản sinh từ cấu hình chương trình.</summary>
        public List<KeyValuePair<string, string>> DieuKhoan { get; set; } = new List<KeyValuePair<string, string>>();
        public List<LuaChonKhoaHoc> KhoaHocApDung { get; set; } = new List<LuaChonKhoaHoc>();
        public List<LuotSuDungUuDai> LichSuCuaToi { get; set; } = new List<LuotSuDungUuDai>();
        public bool GioHangCoKhoaHoc { get; set; }
    }

    /// <summary>Tham số cho partial thẻ mã dùng chung (Views/Shared/_CouponTicket.cshtml).</summary>
    public class TheMaGiamGiaModel
    {
        public MaGiamGiaHienThi Ma { get; set; }

        /// <summary>Nhãn trạng thái thay cho hạn dùng (trang ví): "Đã dùng", "Hết hạn"...</summary>
        public string NhanTrangThai { get; set; }

        /// <summary>Làm mờ thẻ (mã đã dùng / hết hiệu lực).</summary>
        public bool LamMo { get; set; }

        /// <summary>Dòng phụ nhấn mạnh, ví dụ "Còn 517.500 đ khi dùng mã".</summary>
        public string DongPhu { get; set; }

        /// <summary>Có thì hiện nút "Dùng ngay" tới link này.</summary>
        public string LinkDungNgay { get; set; }

        /// <summary>Hiện nút "Lưu mã" / nhãn "Đã lưu".</summary>
        public bool HienNutLuu { get; set; } = true;

        /// <summary>Thẻ hẹp (cột bên phải trang khóa học).</summary>
        public bool NhoGon { get; set; }

        /// <summary>Hiện link "Chi tiết" (ẩn ở chính trang chi tiết mã).</summary>
        public bool HienChiTiet { get; set; } = true;
    }

    /// <summary>Kết quả lưu mã vào ví.</summary>
    public class KetQuaLuuMa
    {
        public bool Success { get; set; }
        public bool DaCoTrongVi { get; set; }
        public string Message { get; set; }
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
