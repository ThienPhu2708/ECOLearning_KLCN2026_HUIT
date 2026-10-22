using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Chương trình khuyến mãi / coupon (phân hệ Marketing) và luồng áp dụng ưu đãi khi thanh toán, theo Đặc tả Marketing ECOLearning.
    /// Nguyên tắc: giá khóa học = giaBan (không có giá khuyến mãi theo từng khóa); giảm giá chỉ qua mã, mỗi đơn tối đa 1 mã;
    /// mọi số tiền giảm tính ở server và tính lại trong transaction lúc thanh toán; doanh thu chỉ tính đơn đã thanh toán.
    /// Cột cấu hình thuộc migration 008 / 009 / 018 (chưa có trong mô hình EF) nên đọc bằng SQL trực tiếp.
    /// Tên cột giữ theo 008: loaiUuDai (đặc tả: loaiGiam), donHangToiThieu (donToiThieu), MaGiamGia.gioiHanLuotDung (soLuotToiDa),
    /// MaGiamGia.gioiHanMoiHocVien (soLuotMoiNguoi), doiTuong = ThanThiet (laUuDaiThanThiet), chiTieuToiThieu (tongChiToiThieu),
    /// phamVi TatCa (ToanSan) / DanhMuc / KhoaHoc.
    /// </summary>
    public partial class PromotionService
    {
        public const string HoatDong = "HoatDong";
        public const string TamNgung = "TamNgung";
        public const string KetThuc = "KetThuc";

        /// <summary>Trạng thái đơn hàng được tính là giao dịch thành công (đơn mới).</summary>
        public const string DonDaThanhToan = "Đã thanh toán";

        /// <summary>Các trạng thái đơn đã thanh toán (đặc tả mục 1), dùng trong SQL: <c>d.trangThai IN (...)</c>.</summary>
        public const string DonDaTraSql = "N'Đã thanh toán', N'Hoàn tất', N'Success'";

        private static readonly Regex DinhDangMa = new Regex("^[A-Z0-9_-]{3,30}$");

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public PromotionService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        /// <summary>Đã chạy migration 008, 009 và 018 hay chưa.</summary>
        public bool IsInstalled()
        {
            try
            {
                return db.Database.SqlQuery<int>(@"
                    SELECT CASE WHEN OBJECT_ID(N'dbo.SuDungUuDai', N'U') IS NOT NULL
                                 AND COL_LENGTH(N'dbo.KhuyenMai', N'loaiUuDai') IS NOT NULL
                                 AND COL_LENGTH(N'dbo.KhuyenMai', N'congKhai') IS NOT NULL
                                 AND COL_LENGTH(N'dbo.KhuyenMai', N'chinhSach') IS NOT NULL
                                 AND OBJECT_ID(N'dbo.ViMaGiamGia', N'U') IS NOT NULL
                                 AND COL_LENGTH(N'dbo.KhuyenMai', N'maDanhMucApDung') IS NOT NULL
                                 AND COL_LENGTH(N'dbo.ChienDich', N'nganSach') IS NOT NULL
                           THEN 1 ELSE 0 END").FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Hậu tố mã mới: yyMMddHHmmss + 4 chữ số ngẫu nhiên (16 ký tự, thêm tiền tố 2 ký tự vẫn vừa varchar(20)).
        /// Dùng Guid thay cho new Random() vì tạo nhiều Random liên tiếp có thể ra cùng một số.
        /// </summary>
        public static string HauToMaMoi()
        {
            int so = (int)(BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0) % 10000);
            return DateTime.Now.ToString("yyMMddHHmmss") + so.ToString("D4");
        }

        // ================================================================== đọc dữ liệu

        private const string SelectChuongTrinh = @"
            SELECT km.maKhuyenMai AS MaKhuyenMai, km.tenKhuyenMai AS TenKhuyenMai, km.mucUuDai AS MucUuDai,
                   km.loaiUuDai AS LoaiUuDai, km.giamToiDa AS GiamToiDa, km.donHangToiThieu AS DonHangToiThieu,
                   km.phamVi AS PhamVi, km.maDanhMucApDung AS MaDanhMucApDung, dm.tenDanhMuc AS TenDanhMucApDung,
                   km.doiTuong AS DoiTuong, km.soKhoaToiThieu AS SoKhoaToiThieu,
                   km.chiTieuToiThieu AS ChiTieuToiThieu, km.trangThai AS TrangThaiLuu,
                   km.ngayBatDau AS NgayBatDau, km.ngayKetThuc AS NgayKetThuc, km.dieuKienApDung AS DieuKienApDung,
                   km.maChienDich AS MaChienDich, cd.tenChienDich AS TenChienDich, cd.trangThai AS TrangThaiChienDich,
                   km.ngayTao AS NgayTao, km.congKhai AS CongKhai, km.chinhSach AS ChinhSach,
                   mg.maGiamGia AS MaGiamGia, mg.maCode AS MaCode, CAST(ISNULL(mg.trangThai, 1) AS BIT) AS CouponDangBat,
                   CAST(ISNULL(mg.giaTriGiam, 0) AS DECIMAL(18, 2)) AS GiaTriGiam,
                   mg.gioiHanLuotDung AS GioiHanLuotDung, mg.gioiHanMoiHocVien AS GioiHanMoiHocVien,
                   ISNULL(sd.soLuot, 0) AS SoLuotDaDung,
                   CAST(ISNULL(sd.tienGiam, 0) AS DECIMAL(18, 2)) AS TongTienGiam,
                   CAST(ISNULL(sd.doanhThu, 0) AS DECIMAL(18, 2)) AS DoanhThuSauGiam,
                   (SELECT COUNT(*) FROM dbo.KhuyenMaiKhoaHoc k WHERE k.maKhuyenMai = km.maKhuyenMai) AS SoKhoaApDung,
                   (SELECT COUNT(*) FROM dbo.ViMaGiamGia v WHERE v.maGiamGia = mg.maGiamGia) AS SoHocVienDaLuu
            FROM dbo.KhuyenMai km
            OUTER APPLY (SELECT TOP (1) m.* FROM dbo.MaGiamGia m WHERE m.maKhuyenMai = km.maKhuyenMai ORDER BY m.maGiamGia) mg
            LEFT JOIN dbo.ChienDich cd ON cd.maChienDich = km.maChienDich
            LEFT JOIN dbo.DanhMuc dm ON dm.maDanhMuc = km.maDanhMucApDung
            OUTER APPLY (
                SELECT COUNT(*) AS soLuot, SUM(s.tienGiam) AS tienGiam, SUM(d.tongTien) AS doanhThu
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai IN (" + DonDaTraSql + @")
                WHERE s.maKhuyenMai = km.maKhuyenMai
            ) sd";

        /// <summary>
        /// Danh sách chương trình. Lọc theo tên / mã coupon, trạng thái hiển thị, đối tượng, chiến dịch và
        /// khoảng thời gian (chương trình có thời gian áp dụng giao với khoảng lọc).
        /// </summary>
        public List<ChuongTrinhKhuyenMai> LayDanhSach(string tuKhoa = null, string trangThai = null, string doiTuong = null,
            DateTime? tuNgay = null, DateTime? denNgay = null, string maChienDich = null)
        {
            var thamSo = new List<SqlParameter>();
            var dieuKien = new List<string>();
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                dieuKien.Add("(km.tenKhuyenMai LIKE @tuKhoa OR mg.maCode LIKE @tuKhoa)");
                thamSo.Add(new SqlParameter("@tuKhoa", "%" + tuKhoa.Trim() + "%"));
            }
            if (doiTuong == "TatCa" || doiTuong == "ThanThiet")
            {
                dieuKien.Add("km.doiTuong = @doiTuong");
                thamSo.Add(new SqlParameter("@doiTuong", doiTuong));
            }
            if (!string.IsNullOrEmpty(maChienDich))
            {
                dieuKien.Add("km.maChienDich = @maChienDich");
                thamSo.Add(new SqlParameter("@maChienDich", maChienDich));
            }
            if (tuNgay.HasValue)
            {
                dieuKien.Add("km.ngayKetThuc >= @tuNgay");
                thamSo.Add(new SqlParameter("@tuNgay", tuNgay.Value.Date));
            }
            if (denNgay.HasValue)
            {
                dieuKien.Add("km.ngayBatDau < @denNgaySau");
                thamSo.Add(new SqlParameter("@denNgaySau", denNgay.Value.Date.AddDays(1)));
            }

            string sql = SelectChuongTrinh
                + (dieuKien.Count > 0 ? " WHERE " + string.Join(" AND ", dieuKien) : "")
                + " ORDER BY km.ngayTao DESC, km.ngayBatDau DESC";
            var list = db.Database.SqlQuery<ChuongTrinhKhuyenMai>(sql, thamSo.ToArray()).ToList();

            if (!string.IsNullOrEmpty(trangThai) && trangThai != "all")
            {
                var now = DateTime.Now;
                list = list.Where(p => p.TrangThaiHienThi(now) == trangThai).ToList();
            }
            return list;
        }

        public ChuongTrinhKhuyenMai LayTheoMa(string maKhuyenMai)
        {
            if (string.IsNullOrEmpty(maKhuyenMai)) return null;
            return db.Database.SqlQuery<ChuongTrinhKhuyenMai>(SelectChuongTrinh + " WHERE km.maKhuyenMai = @ma",
                new SqlParameter("@ma", maKhuyenMai)).FirstOrDefault();
        }

        /// <summary>Tìm chương trình theo mã code (không phân biệt hoa thường).</summary>
        public ChuongTrinhKhuyenMai LayTheoCode(string maCode)
        {
            if (string.IsNullOrWhiteSpace(maCode)) return null;
            string maKm = db.Database.SqlQuery<string>("SELECT TOP (1) maKhuyenMai FROM dbo.MaGiamGia WHERE UPPER(maCode) = @code",
                new SqlParameter("@code", maCode.Trim().ToUpperInvariant())).FirstOrDefault();
            var p = LayTheoMa(maKm);
            return p == null || p.MaGiamGia == null ? null : p;
        }

        public List<LuaChonKhoaHoc> LayKhoaHocApDung(string maKhuyenMai)
        {
            return db.Database.SqlQuery<LuaChonKhoaHoc>(@"
                SELECT kh.maKhoaHoc AS MaKhoaHoc, kh.tenKhoaHoc AS TenKhoaHoc, ISNULL(dm.tenDanhMuc, N'') AS TenDanhMuc, kh.giaBan AS GiaBan
                FROM dbo.KhuyenMaiKhoaHoc k
                INNER JOIN dbo.KhoaHoc kh ON kh.maKhoaHoc = k.maKhoaHoc
                LEFT JOIN dbo.DanhMuc dm ON dm.maDanhMuc = kh.maDanhMuc
                WHERE k.maKhuyenMai = @ma
                ORDER BY kh.tenKhoaHoc",
                new SqlParameter("@ma", maKhuyenMai)).ToList();
        }

        /// <summary>Khóa học được chọn làm phạm vi áp dụng: chỉ khóa đã xuất bản, có phí.</summary>
        public List<LuaChonKhoaHoc> LayKhoaHocCoTheApDung()
        {
            return db.Database.SqlQuery<LuaChonKhoaHoc>(@"
                SELECT kh.maKhoaHoc AS MaKhoaHoc, kh.tenKhoaHoc AS TenKhoaHoc, ISNULL(dm.tenDanhMuc, N'') AS TenDanhMuc, kh.giaBan AS GiaBan
                FROM dbo.KhoaHoc kh
                LEFT JOIN dbo.DanhMuc dm ON dm.maDanhMuc = kh.maDanhMuc
                WHERE kh.trangThai = N'Đã xuất bản' AND kh.giaBan > 0
                ORDER BY dm.tenDanhMuc, kh.tenKhoaHoc").ToList();
        }

        public List<LuaChonDanhMuc> LayDanhMucLuaChon()
        {
            return db.Database.SqlQuery<LuaChonDanhMuc>(@"
                SELECT maDanhMuc AS MaDanhMuc, tenDanhMuc AS TenDanhMuc
                FROM dbo.DanhMuc
                WHERE trangThai IS NULL OR trangThai = 1
                ORDER BY tenDanhMuc").ToList();
        }

        /// <summary>Chiến dịch còn gắn thêm khuyến mãi được: không Hoàn thành / Đã hủy và chưa qua ngày kết thúc.</summary>
        public List<LuaChonChienDich> LayChienDichDangMo()
        {
            return db.Database.SqlQuery<LuaChonChienDich>(@"
                SELECT maChienDich AS MaChienDich, tenChienDich AS TenChienDich, ngayBatDau AS NgayBatDau, ngayKetThuc AS NgayKetThuc
                FROM dbo.ChienDich
                WHERE trangThai NOT IN (N'HoanThanh', N'DaHuy') AND ngayKetThuc >= CAST(GETDATE() AS DATE)
                ORDER BY ngayBatDau DESC").ToList();
        }

        public List<LuotSuDungUuDai> LayLuotSuDung(string maKhuyenMai)
        {
            return db.Database.SqlQuery<LuotSuDungUuDai>(@"
                SELECT s.maDonHang AS MaDonHang, s.ngaySuDung AS NgaySuDung, nd.hoTen AS HoTen, nd.email AS Email,
                       s.tienTruocGiam AS TienTruocGiam, s.tienGiam AS TienGiam, d.tongTien AS TongDonHang
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai IN (" + DonDaTraSql + @")
                INNER JOIN dbo.NguoiDung nd ON nd.maNguoiDung = s.maHocVien
                WHERE s.maKhuyenMai = @ma
                ORDER BY s.ngaySuDung DESC",
                new SqlParameter("@ma", maKhuyenMai)).ToList();
        }

        // ================================================================== tạo / sửa (đặc tả mục 5)

        /// <summary>Đổ dữ liệu chương trình có sẵn vào form chỉnh sửa.</summary>
        public PromotionFormViewModel TaoFormTu(ChuongTrinhKhuyenMai p)
        {
            return new PromotionFormViewModel
            {
                MaKhuyenMai = p.MaKhuyenMai,
                TenKhuyenMai = p.TenKhuyenMai,
                MaChienDich = p.MaChienDich,
                MaCode = p.MaCode,
                LoaiUuDai = p.LoaiUuDai,
                GiaTriUuDai = p.LaPhanTram ? (decimal)p.MucUuDai : p.SoTienGiam,
                GiamToiDa = p.GiamToiDa,
                DonHangToiThieu = p.DonHangToiThieu,
                NgayBatDau = p.NgayBatDau.Date,
                NgayKetThuc = p.NgayKetThuc.Date,
                GioiHanLuotDung = p.GioiHanLuotDung,
                GioiHanMoiHocVien = p.GioiHanMoiHocVien,
                PhamVi = p.PhamVi,
                MaDanhMucApDung = p.MaDanhMucApDung,
                KhoaHocApDung = LayKhoaHocApDung(p.MaKhuyenMai).Select(k => k.MaKhoaHoc).ToList(),
                DoiTuong = p.DoiTuong,
                SoKhoaToiThieu = p.SoKhoaToiThieu,
                ChiTieuToiThieu = p.ChiTieuToiThieu,
                DieuKienApDung = p.DieuKienApDung,
                CongKhai = p.CongKhai,
                ChinhSach = p.ChinhSach,
                SoLuotDaDung = p.SoLuotDaDung
            };
        }

        /// <summary>
        /// Kiểm tra dữ liệu form theo đặc tả mục 5. Trả về lỗi theo tên trường (rỗng = hợp lệ). Chuẩn hóa dữ liệu trong form
        /// (mã in hoa, bỏ trường không dùng theo loại ưu đãi / đối tượng / phạm vi).
        /// </summary>
        public Dictionary<string, string> KiemTraForm(PromotionFormViewModel f, ChuongTrinhKhuyenMai hienTai)
        {
            var loi = new Dictionary<string, string>();
            bool laSua = hienTai != null;

            f.TenKhuyenMai = (f.TenKhuyenMai ?? "").Trim();
            f.MaCode = (f.MaCode ?? "").Trim().ToUpperInvariant();
            f.DieuKienApDung = string.IsNullOrWhiteSpace(f.DieuKienApDung) ? null : f.DieuKienApDung.Trim();
            f.ChinhSach = string.IsNullOrWhiteSpace(f.ChinhSach) ? null : f.ChinhSach.Trim();
            f.LoaiUuDai = f.LoaiUuDai == "SoTien" ? "SoTien" : "PhanTram";
            f.PhamVi = f.PhamVi == "KhoaHoc" || f.PhamVi == "DanhMuc" ? f.PhamVi : "TatCa";
            f.DoiTuong = f.DoiTuong == "ThanThiet" ? "ThanThiet" : "TatCa";
            f.KhoaHocApDung = (f.KhoaHocApDung ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            f.MaDanhMucApDung = string.IsNullOrWhiteSpace(f.MaDanhMucApDung) ? null : f.MaDanhMucApDung.Trim();

            if (f.ChinhSach != null && f.ChinhSach.Length > 1000) loi["ChinhSach"] = "Mô tả chính sách tối đa 1000 ký tự.";
            if (f.DieuKienApDung != null && f.DieuKienApDung.Length > 500) loi["DieuKienApDung"] = "Mô tả tối đa 500 ký tự.";
            if (f.TenKhuyenMai.Length == 0) loi["TenKhuyenMai"] = "Vui lòng nhập tên chương trình.";
            else if (f.TenKhuyenMai.Length > 150) loi["TenKhuyenMai"] = "Tên chương trình tối đa 150 ký tự.";

            // Mã coupon: không trùng (không phân biệt hoa thường); đã có lượt dùng thì không đổi mã để giữ lịch sử đối soát
            if (f.MaCode.Length == 0) loi["MaCode"] = "Vui lòng nhập mã coupon.";
            else if (!DinhDangMa.IsMatch(f.MaCode)) loi["MaCode"] = "Mã coupon gồm 3–30 ký tự: chữ in hoa không dấu, số, dấu gạch ngang hoặc gạch dưới.";
            else if (laSua && hienTai.SoLuotDaDung > 0 && !string.Equals(f.MaCode, hienTai.MaCode, StringComparison.OrdinalIgnoreCase))
                loi["MaCode"] = "Mã đã có lượt sử dụng nên không thể đổi.";
            else if (db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.MaGiamGia WHERE UPPER(maCode) = @ma AND maKhuyenMai <> @km",
                         new SqlParameter("@ma", f.MaCode), new SqlParameter("@km", laSua ? hienTai.MaKhuyenMai : "")).First() > 0)
                loi["MaCode"] = "Mã coupon \"" + f.MaCode + "\" đã tồn tại.";

            // Giá trị ưu đãi theo loại
            if (f.LoaiUuDai == "SoTien") f.GiamToiDa = null;
            if (f.DonHangToiThieu.HasValue && f.DonHangToiThieu <= 0) f.DonHangToiThieu = null;
            if (!f.GiaTriUuDai.HasValue) loi["GiaTriUuDai"] = "Vui lòng nhập giá trị ưu đãi.";
            else if (f.LoaiUuDai == "PhanTram" && (f.GiaTriUuDai < 1 || f.GiaTriUuDai > 100 || f.GiaTriUuDai != decimal.Truncate(f.GiaTriUuDai.Value)))
                loi["GiaTriUuDai"] = "Phần trăm giảm là số nguyên từ 1 đến 100.";
            else if (f.LoaiUuDai == "SoTien" && (f.GiaTriUuDai < 1000 || f.GiaTriUuDai != decimal.Truncate(f.GiaTriUuDai.Value)))
                loi["GiaTriUuDai"] = "Số tiền giảm phải là số nguyên từ 1.000 đ.";
            else if (f.LoaiUuDai == "SoTien" && f.DonHangToiThieu.HasValue && f.GiaTriUuDai > f.DonHangToiThieu)
                loi["GiaTriUuDai"] = "Số tiền giảm không được lớn hơn giá trị đơn tối thiểu (" + f.DonHangToiThieu.Value.ToString("N0") + " đ).";
            if (f.GiamToiDa.HasValue && f.GiamToiDa <= 0) loi["GiamToiDa"] = "Mức giảm tối đa phải lớn hơn 0.";

            // Chiến dịch: phải tồn tại và còn mở (không Hoàn thành / Đã hủy); giữ được chiến dịch cũ khi sửa
            if (string.IsNullOrEmpty(f.MaChienDich)) f.MaChienDich = null;
            LuaChonChienDich chienDich = null;
            if (f.MaChienDich != null)
            {
                chienDich = LayChienDichDangMo().FirstOrDefault(c => c.MaChienDich == f.MaChienDich);
                if (chienDich == null && laSua && f.MaChienDich == hienTai.MaChienDich)
                    chienDich = db.Database.SqlQuery<LuaChonChienDich>(@"
                        SELECT maChienDich AS MaChienDich, tenChienDich AS TenChienDich, ngayBatDau AS NgayBatDau, ngayKetThuc AS NgayKetThuc
                        FROM dbo.ChienDich WHERE maChienDich = @ma", new SqlParameter("@ma", f.MaChienDich)).FirstOrDefault();
                if (chienDich == null) loi["MaChienDich"] = "Chiến dịch không tồn tại, đã hoàn thành hoặc đã hủy.";
            }

            // Thời gian: kết thúc ≥ bắt đầu, ≥ hôm nay, nằm trong thời gian chiến dịch
            if (!f.NgayBatDau.HasValue) loi["NgayBatDau"] = "Vui lòng chọn ngày bắt đầu.";
            if (!f.NgayKetThuc.HasValue) loi["NgayKetThuc"] = "Vui lòng chọn ngày kết thúc.";
            if (f.NgayBatDau.HasValue && f.NgayKetThuc.HasValue)
            {
                if (f.NgayKetThuc.Value.Date < f.NgayBatDau.Value.Date) loi["NgayKetThuc"] = "Ngày kết thúc không được trước ngày bắt đầu.";
                else if (f.NgayKetThuc.Value.Date < DateTime.Today) loi["NgayKetThuc"] = "Ngày kết thúc đã ở quá khứ.";
                else if (chienDich != null && (f.NgayBatDau.Value.Date < chienDich.NgayBatDau.Date || f.NgayKetThuc.Value.Date > chienDich.NgayKetThuc.Date))
                    loi["NgayKetThuc"] = "Thời gian khuyến mãi phải nằm trong thời gian chiến dịch ("
                        + chienDich.NgayBatDau.ToString("dd/MM/yyyy") + " – " + chienDich.NgayKetThuc.ToString("dd/MM/yyyy") + ").";
            }

            // Giới hạn lượt dùng
            if (f.GioiHanLuotDung.HasValue && f.GioiHanLuotDung < 1) loi["GioiHanLuotDung"] = "Tổng lượt dùng phải từ 1 (bỏ trống nếu không giới hạn).";
            else if (laSua && f.GioiHanLuotDung.HasValue && f.GioiHanLuotDung < hienTai.SoLuotDaDung)
                loi["GioiHanLuotDung"] = "Mã đã được dùng " + hienTai.SoLuotDaDung + " lượt, tổng lượt không được nhỏ hơn số này.";
            if (f.GioiHanMoiHocVien.HasValue && f.GioiHanMoiHocVien < 1) loi["GioiHanMoiHocVien"] = "Số lượt mỗi học viên phải từ 1 (bỏ trống nếu không giới hạn).";
            else if (f.GioiHanMoiHocVien.HasValue && f.GioiHanLuotDung.HasValue && f.GioiHanMoiHocVien > f.GioiHanLuotDung)
                loi["GioiHanMoiHocVien"] = "Số lượt mỗi học viên không được lớn hơn tổng lượt dùng.";

            // Phạm vi
            if (f.PhamVi == "DanhMuc")
            {
                if (f.MaDanhMucApDung == null) loi["MaDanhMucApDung"] = "Chọn danh mục áp dụng.";
                else if (LayDanhMucLuaChon().All(d => d.MaDanhMuc != f.MaDanhMucApDung)) loi["MaDanhMucApDung"] = "Danh mục không tồn tại hoặc đã ẩn.";
            }
            else
            {
                f.MaDanhMucApDung = null;
            }
            if (f.PhamVi == "KhoaHoc")
            {
                if (f.KhoaHocApDung.Count == 0) loi["KhoaHocApDung"] = "Chọn ít nhất một khóa học áp dụng.";
                else
                {
                    var hopLe = new HashSet<string>(LayKhoaHocCoTheApDung().Select(k => k.MaKhoaHoc));
                    // Khi sửa, giữ được các khóa đã gắn trước đó dù nay không còn mở bán
                    if (laSua) foreach (var k in LayKhoaHocApDung(hienTai.MaKhuyenMai)) hopLe.Add(k.MaKhoaHoc);
                    if (f.KhoaHocApDung.Any(k => !hopLe.Contains(k))) loi["KhoaHocApDung"] = "Có khóa học không tồn tại hoặc không còn mở bán.";
                }
            }
            else
            {
                f.KhoaHocApDung.Clear();
            }

            // Ưu đãi thân thiết: ít nhất một tiêu chí (số khóa đã mua ≥ 1 và/hoặc tổng chi tiêu > 0)
            if (f.DoiTuong == "ThanThiet")
            {
                if (!f.SoKhoaToiThieu.HasValue && !f.ChiTieuToiThieu.HasValue)
                    loi["SoKhoaToiThieu"] = "Ưu đãi thân thiết cần ít nhất một tiêu chí: số khóa đã mua hoặc tổng chi tiêu.";
                if (f.SoKhoaToiThieu.HasValue && f.SoKhoaToiThieu < 1) loi["SoKhoaToiThieu"] = "Số khóa đã mua tối thiểu phải từ 1.";
                if (f.ChiTieuToiThieu.HasValue && f.ChiTieuToiThieu <= 0) loi["ChiTieuToiThieu"] = "Tổng chi tiêu tối thiểu phải lớn hơn 0.";
            }
            else
            {
                f.SoKhoaToiThieu = null;
                f.ChiTieuToiThieu = null;
            }

            return loi;
        }

        /// <summary>Tạo chương trình, coupon và liên kết khóa học trong một transaction. Trả về mã chương trình.</summary>
        public string Tao(PromotionFormViewModel f, int maNhanVien)
        {
            string hauTo, maKm, maMgg;
            do
            {
                hauTo = HauToMaMoi();
                maKm = "KM" + hauTo;
                maMgg = "MG" + hauTo;
            }
            while (db.Database.SqlQuery<int>(@"
                SELECT (SELECT COUNT(*) FROM dbo.KhuyenMai WHERE maKhuyenMai = @km) + (SELECT COUNT(*) FROM dbo.MaGiamGia WHERE maGiamGia = @mgg)",
                new SqlParameter("@km", maKm), new SqlParameter("@mgg", maMgg)).First() > 0);

            using (var tx = db.Database.BeginTransaction())
            {
                db.Database.ExecuteSqlCommand(@"
                    INSERT INTO dbo.KhuyenMai (maKhuyenMai, tenKhuyenMai, mucUuDai, ngayBatDau, ngayKetThuc, dieuKienApDung, maNhanVienMarketing,
                                               loaiUuDai, giamToiDa, donHangToiThieu, phamVi, maDanhMucApDung, doiTuong, soKhoaToiThieu, chiTieuToiThieu,
                                               trangThai, maChienDich, congKhai, chinhSach)
                    VALUES (@ma, @ten, @muc, @bd, @kt, @dk, @nv, @loai, @toiDa, @donMin, @phamVi, @danhMuc, @doiTuong, @soKhoa, @chiTieu, N'HoatDong', @cd, @congKhai, @chinhSach)",
                    ThamSoChuongTrinh(f, maKm, maNhanVien).ToArray());

                db.Database.ExecuteSqlCommand(@"
                    INSERT INTO dbo.MaGiamGia (maGiamGia, maCode, giaTriGiam, trangThai, maKhuyenMai, gioiHanLuotDung, gioiHanMoiHocVien)
                    VALUES (@mgg, @code, @giaTri, 1, @km, @gioiHan, @moiHv)",
                    new SqlParameter("@mgg", maMgg),
                    new SqlParameter("@code", f.MaCode),
                    new SqlParameter("@giaTri", f.GiaTriUuDai.Value),
                    new SqlParameter("@km", maKm),
                    new SqlParameter("@gioiHan", (object)f.GioiHanLuotDung ?? DBNull.Value),
                    new SqlParameter("@moiHv", (object)f.GioiHanMoiHocVien ?? DBNull.Value));

                GhiKhoaHocApDung(maKm, f.KhoaHocApDung);
                tx.Commit();
            }
            return maKm;
        }

        /// <summary>
        /// Cập nhật chương trình. Đơn hàng đã hoàn tất không bị ảnh hưởng vì số tiền giảm của từng đơn đã lưu trong SuDungUuDai.
        /// </summary>
        public void CapNhat(ChuongTrinhKhuyenMai hienTai, PromotionFormViewModel f)
        {
            using (var tx = db.Database.BeginTransaction())
            {
                db.Database.ExecuteSqlCommand(@"
                    UPDATE dbo.KhuyenMai
                    SET tenKhuyenMai = @ten, mucUuDai = @muc, ngayBatDau = @bd, ngayKetThuc = @kt, dieuKienApDung = @dk,
                        loaiUuDai = @loai, giamToiDa = @toiDa, donHangToiThieu = @donMin, phamVi = @phamVi, maDanhMucApDung = @danhMuc,
                        doiTuong = @doiTuong, soKhoaToiThieu = @soKhoa, chiTieuToiThieu = @chiTieu, maChienDich = @cd, congKhai = @congKhai,
                        chinhSach = @chinhSach
                    WHERE maKhuyenMai = @ma",
                    ThamSoChuongTrinh(f, hienTai.MaKhuyenMai, 0).Where(p => p.ParameterName != "@nv").ToArray());

                db.Database.ExecuteSqlCommand(@"
                    UPDATE dbo.MaGiamGia
                    SET maCode = @code, giaTriGiam = @giaTri, gioiHanLuotDung = @gioiHan, gioiHanMoiHocVien = @moiHv
                    WHERE maGiamGia = @mgg",
                    new SqlParameter("@code", f.MaCode),
                    new SqlParameter("@giaTri", f.GiaTriUuDai.Value),
                    new SqlParameter("@gioiHan", (object)f.GioiHanLuotDung ?? DBNull.Value),
                    new SqlParameter("@moiHv", (object)f.GioiHanMoiHocVien ?? DBNull.Value),
                    new SqlParameter("@mgg", hienTai.MaGiamGia));

                db.Database.ExecuteSqlCommand("DELETE FROM dbo.KhuyenMaiKhoaHoc WHERE maKhuyenMai = @ma", new SqlParameter("@ma", hienTai.MaKhuyenMai));
                GhiKhoaHocApDung(hienTai.MaKhuyenMai, f.KhoaHocApDung);
                tx.Commit();
            }
        }

        private static List<SqlParameter> ThamSoChuongTrinh(PromotionFormViewModel f, string maKm, int maNhanVien)
        {
            return new List<SqlParameter>
            {
                new SqlParameter("@ma", maKm),
                new SqlParameter("@ten", f.TenKhuyenMai),
                new SqlParameter("@muc", (double)f.GiaTriUuDai.Value),
                new SqlParameter("@bd", f.NgayBatDau.Value.Date),
                // Kết thúc vào cuối ngày đã chọn
                new SqlParameter("@kt", f.NgayKetThuc.Value.Date.AddDays(1).AddSeconds(-1)),
                new SqlParameter("@dk", (object)f.DieuKienApDung ?? DBNull.Value),
                new SqlParameter("@nv", maNhanVien),
                new SqlParameter("@loai", f.LoaiUuDai),
                new SqlParameter("@toiDa", (object)f.GiamToiDa ?? DBNull.Value),
                new SqlParameter("@donMin", (object)f.DonHangToiThieu ?? DBNull.Value),
                new SqlParameter("@phamVi", f.PhamVi),
                new SqlParameter("@danhMuc", (object)f.MaDanhMucApDung ?? DBNull.Value),
                new SqlParameter("@doiTuong", f.DoiTuong),
                new SqlParameter("@soKhoa", (object)f.SoKhoaToiThieu ?? DBNull.Value),
                new SqlParameter("@chiTieu", (object)f.ChiTieuToiThieu ?? DBNull.Value),
                new SqlParameter("@cd", (object)f.MaChienDich ?? DBNull.Value),
                new SqlParameter("@congKhai", f.CongKhai),
                new SqlParameter("@chinhSach", (object)f.ChinhSach ?? DBNull.Value)
            };
        }

        private void GhiKhoaHocApDung(string maKm, IEnumerable<string> khoaHoc)
        {
            foreach (var maKhoaHoc in khoaHoc)
            {
                db.Database.ExecuteSqlCommand("INSERT INTO dbo.KhuyenMaiKhoaHoc (maKhuyenMai, maKhoaHoc) VALUES (@km, @kh)",
                    new SqlParameter("@km", maKm), new SqlParameter("@kh", maKhoaHoc));
            }
        }

        // ================================================================== vận hành mã (đặc tả mục 7)

        /// <summary>
        /// pause: tạm ngưng (tắt MaGiamGia.trangThai). resume: kích hoạt lại. end: kết thúc sớm — ngày kết thúc = hôm qua
        /// (chưa bắt đầu thì dời luôn ngày bắt đầu) và tắt mã; lịch sử và giao dịch cũ giữ nguyên.
        /// Trạng thái coupon (MaGiamGia.trangThai) đồng bộ theo để các hàm SQL cũ (fn_KiemTraCoupon) cùng kết quả.
        /// </summary>
        public string DoiTrangThai(string maKhuyenMai, string hanhDong, out bool thanhCong)
        {
            thanhCong = false;
            var p = LayTheoMa(maKhuyenMai);
            if (p == null) return "Không tìm thấy chương trình khuyến mãi.";
            var now = DateTime.Now;
            bool daKetThuc = p.DaKetThuc(now);

            switch (hanhDong)
            {
                case "pause":
                    if (daKetThuc) return "Chương trình đã kết thúc, không thể tạm ngưng.";
                    if (p.DangTamNgung) return "Chương trình đang tạm ngưng.";
                    db.Database.ExecuteSqlCommand(@"
                        UPDATE dbo.KhuyenMai SET trangThai = @tt WHERE maKhuyenMai = @ma;
                        UPDATE dbo.MaGiamGia SET trangThai = 0 WHERE maKhuyenMai = @ma;",
                        new SqlParameter("@tt", TamNgung), new SqlParameter("@ma", maKhuyenMai));
                    thanhCong = true;
                    return "Đã tạm ngưng chương trình \"" + p.TenKhuyenMai + "\".";

                case "resume":
                    if (daKetThuc) return "Chương trình đã kết thúc, không thể kích hoạt lại.";
                    if (!p.DangTamNgung) return "Chương trình đang hoạt động.";
                    // Admin tạm dừng (migration 016): chỉ Admin cho chạy lại được
                    string lyDoAdmin = AdminMarketingService.LyDoAdminTamDung(db, maKhuyenMai);
                    if (lyDoAdmin != null) return "Chương trình đang bị Quản trị viên tạm dừng (lý do: " + lyDoAdmin + "). Hãy điều chỉnh rồi liên hệ Quản trị viên để được cho chạy lại.";
                    db.Database.ExecuteSqlCommand(@"
                        UPDATE dbo.KhuyenMai SET trangThai = @tt WHERE maKhuyenMai = @ma;
                        UPDATE dbo.MaGiamGia SET trangThai = 1 WHERE maKhuyenMai = @ma;",
                        new SqlParameter("@tt", HoatDong), new SqlParameter("@ma", maKhuyenMai));
                    thanhCong = true;
                    return "Đã kích hoạt lại chương trình \"" + p.TenKhuyenMai + "\".";

                case "end":
                    if (daKetThuc) return "Chương trình đã kết thúc trước đó.";
                    var homQua = DateTime.Today.AddSeconds(-1);
                    db.Database.ExecuteSqlCommand(@"
                        UPDATE dbo.KhuyenMai
                        SET trangThai = @tt, ngayKetThuc = @kt,
                            ngayBatDau = CASE WHEN ngayBatDau > @kt THEN @bd ELSE ngayBatDau END
                        WHERE maKhuyenMai = @ma;
                        UPDATE dbo.MaGiamGia SET trangThai = 0 WHERE maKhuyenMai = @ma;",
                        new SqlParameter("@tt", KetThuc),
                        new SqlParameter("@kt", homQua),
                        new SqlParameter("@bd", homQua.Date),
                        new SqlParameter("@ma", maKhuyenMai));
                    thanhCong = true;
                    return "Đã kết thúc chương trình \"" + p.TenKhuyenMai + "\". Lịch sử sử dụng được giữ nguyên.";

                default:
                    return "Thao tác không hợp lệ.";
            }
        }

        /// <summary>Có đơn hàng nào (kể cả đơn chờ / thất bại) đã gắn mã này chưa.</summary>
        public bool DaCoDonHang(ChuongTrinhKhuyenMai p)
        {
            return db.Database.SqlQuery<int>(@"
                SELECT (SELECT COUNT(*) FROM dbo.DonHang WHERE maGiamGia = @mgg) + (SELECT COUNT(*) FROM dbo.SuDungUuDai WHERE maKhuyenMai = @km)",
                new SqlParameter("@mgg", (object)p.MaGiamGia ?? DBNull.Value), new SqlParameter("@km", p.MaKhuyenMai)).First() > 0;
        }

        /// <summary>Xóa chương trình: chỉ khi chưa có đơn hàng nào dùng mã; nếu đã có thì phải dùng "Kết thúc" để giữ lịch sử.</summary>
        public string Xoa(string maKhuyenMai, out bool thanhCong)
        {
            thanhCong = false;
            var p = LayTheoMa(maKhuyenMai);
            if (p == null) return "Không tìm thấy chương trình khuyến mãi.";
            if (DaCoDonHang(p)) return "Mã " + p.MaCode + " đã có đơn hàng sử dụng nên không thể xóa. Hãy dùng \"Kết thúc\" để giữ lịch sử.";

            using (var tx = db.Database.BeginTransaction())
            {
                db.Database.ExecuteSqlCommand(@"
                    DELETE v FROM dbo.ViMaGiamGia v INNER JOIN dbo.MaGiamGia m ON m.maGiamGia = v.maGiamGia WHERE m.maKhuyenMai = @ma;
                    DELETE FROM dbo.KhuyenMaiKhoaHoc WHERE maKhuyenMai = @ma;
                    DELETE FROM dbo.MaGiamGia WHERE maKhuyenMai = @ma;
                    DELETE FROM dbo.KhuyenMai WHERE maKhuyenMai = @ma;",
                    new SqlParameter("@ma", maKhuyenMai));
                tx.Commit();
            }
            thanhCong = true;
            return "Đã xóa chương trình \"" + p.TenKhuyenMai + "\" (mã " + p.MaCode + ").";
        }

        // ================================================================== học viên thân thiết (đặc tả mục 6)

        private const string SelectHocVien = @"
            SELECT nd.maNguoiDung AS MaNguoiDung, nd.hoTen AS HoTen, nd.email AS Email,
                   (SELECT COUNT(DISTINCT ct.maKhoaHoc) FROM dbo.ChiTietDonHang ct
                    INNER JOIN dbo.DonHang d ON d.maDonHang = ct.maDonHang AND d.trangThai IN (" + DonDaTraSql + @")
                    WHERE d.maHocVien = nd.maNguoiDung) AS SoKhoa,
                   CAST(ISNULL((SELECT SUM(d.tongTien) FROM dbo.DonHang d WHERE d.maHocVien = nd.maNguoiDung AND d.trangThai IN (" + DonDaTraSql + @")), 0) AS DECIMAL(18, 2)) AS ChiTieu,
                   (SELECT COUNT(*) FROM dbo.SuDungUuDai s
                    INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai IN (" + DonDaTraSql + @")
                    WHERE s.maHocVien = nd.maNguoiDung AND s.maKhuyenMai = @km) AS SoLuotDaDung
            FROM dbo.NguoiDung nd";

        /// <summary>
        /// Số khóa học khác nhau đã mua trong các đơn đã thanh toán (khóa miễn phí đăng ký trực tiếp không tính)
        /// và tổng chi tiêu (DonHang.tongTien sau giảm của các đơn đã thanh toán).
        /// </summary>
        public HocVienThanThietItem ThongKeHocVien(int maHocVien, string maKhuyenMai = "")
        {
            if (maHocVien <= 0) return null;
            return db.Database.SqlQuery<HocVienThanThietItem>(SelectHocVien + " WHERE nd.maNguoiDung = @hv",
                new SqlParameter("@km", maKhuyenMai ?? ""), new SqlParameter("@hv", maHocVien)).FirstOrDefault();
        }

        /// <summary>Đặt cả hai tiêu chí thì phải đạt cả hai.</summary>
        public static bool DatTieuChi(ChuongTrinhKhuyenMai p, HocVienThanThietItem hv)
        {
            if (!p.LaThanThiet) return true;
            if (hv == null) return false;
            if (p.SoKhoaToiThieu.HasValue && hv.SoKhoa < p.SoKhoaToiThieu.Value) return false;
            if (p.ChiTieuToiThieu.HasValue && hv.ChiTieu < p.ChiTieuToiThieu.Value) return false;
            return true;
        }

        /// <summary>
        /// Phần còn thiếu để đạt tiêu chí thân thiết, ví dụ "mua thêm 1 khóa học (đã mua 1/2) và chi tiêu thêm 300.000 đ (đã chi 1.200.000 đ)".
        /// Rỗng khi đã đạt.
        /// </summary>
        public static string PhanConThieu(ChuongTrinhKhuyenMai p, HocVienThanThietItem hv)
        {
            int soKhoa = hv?.SoKhoa ?? 0;
            decimal chiTieu = hv?.ChiTieu ?? 0;
            var thieu = new List<string>();
            if (p.SoKhoaToiThieu.HasValue && soKhoa < p.SoKhoaToiThieu.Value)
                thieu.Add("mua thêm " + (p.SoKhoaToiThieu.Value - soKhoa) + " khóa học (đã mua " + soKhoa + "/" + p.SoKhoaToiThieu.Value + ")");
            if (p.ChiTieuToiThieu.HasValue && chiTieu < p.ChiTieuToiThieu.Value)
                thieu.Add("chi tiêu thêm " + (p.ChiTieuToiThieu.Value - chiTieu).ToString("N0") + " đ (đã chi " + chiTieu.ToString("N0") + " đ)");
            return string.Join(" và ", thieu);
        }

        /// <summary>Học viên đang hoạt động kèm số liệu xét tiêu chí của một chương trình (đạt tiêu chí xếp trước).</summary>
        public List<HocVienThanThietItem> LayHocVienTheoTieuChi(ChuongTrinhKhuyenMai p)
        {
            return db.Database.SqlQuery<HocVienThanThietItem>(
                    SelectHocVien + " WHERE nd.vaiTro IN (N'HocVien', N'GiaoVien') AND ISNULL(nd.trangThai, 1) = 1",
                    new SqlParameter("@km", p.MaKhuyenMai)).ToList()
                .OrderByDescending(h => DatTieuChi(p, h))
                .ThenByDescending(h => h.ChiTieu)
                .ThenByDescending(h => h.SoKhoa)
                .ToList();
        }

        // ================================================================== áp dụng coupon (đặc tả mục 9)

        /// <summary>Số lần học viên đã dùng một mã (chỉ đơn đã thanh toán).</summary>
        private int LuotDungCuaHocVien(int maHocVien, string maGiamGia)
        {
            if (maHocVien <= 0) return 0;
            return db.Database.SqlQuery<int>(@"
                SELECT COUNT(*) FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai IN (" + DonDaTraSql + @")
                WHERE s.maGiamGia = @mgg AND s.maHocVien = @hv",
                new SqlParameter("@mgg", maGiamGia), new SqlParameter("@hv", maHocVien)).First();
        }

        public class KhoaDanhMuc
        {
            public string MaKhoaHoc { get; set; }
            public string MaDanhMuc { get; set; }
        }

        /// <summary>
        /// Hàm kiểm tra duy nhất cho web và API (đặc tả: PromotionService.Check(code, userId, lines)). Kiểm tra theo đúng thứ tự
        /// 13 bước của mục 9, dừng ở lỗi đầu tiên, rồi tính số tiền giảm ở server. <paramref name="maHocVien"/> = 0 khi chưa đăng nhập.
        /// Giá mỗi dòng giỏ hàng là giaBan lấy lại từ CSDL.
        /// </summary>
        public KetQuaApDungUuDai KiemTraCoupon(int maHocVien, string maCode, IList<DongGioHang> gioHang)
        {
            // 1–2. Có nhập mã, mã tồn tại
            if (string.IsNullOrWhiteSpace(maCode)) return KetQuaApDungUuDai.Loi("Vui lòng nhập mã khuyến mãi!");
            var p = LayTheoCode(maCode);
            if (p == null) return KetQuaApDungUuDai.Loi("Mã khuyến mãi không tồn tại!");

            var now = DateTime.Now;
            // 3. Chưa hết hạn (kết thúc sớm đặt ngày kết thúc = hôm qua; dữ liệu cũ "KetThuc" mà ngày còn hạn thì báo đã kết thúc)
            if (p.NgayKetThuc < now) return KetQuaApDungUuDai.Loi("Mã khuyến mãi đã hết hạn từ ngày " + p.NgayKetThuc.ToString("dd/MM/yyyy") + "!", p);
            if (p.TrangThaiLuu == KetThuc) return KetQuaApDungUuDai.Loi("Chương trình khuyến mãi đã kết thúc!", p);
            // 4. Mã đang bật
            if (p.DangTamNgung) return KetQuaApDungUuDai.Loi("Mã khuyến mãi đang tạm ngưng áp dụng!", p);
            // 5. Đã tới ngày bắt đầu
            if (p.NgayBatDau > now) return KetQuaApDungUuDai.Loi("Chương trình khuyến mãi chưa bắt đầu (áp dụng từ " + p.NgayBatDau.ToString("dd/MM/yyyy") + ")!", p);
            // 6. Chiến dịch không Tạm dừng / Đã hủy
            if (p.ChienDichChan) return KetQuaApDungUuDai.Loi(p.LyDoChienDichChan, p);
            // 7. Còn lượt tổng
            if (p.HetLuot) return KetQuaApDungUuDai.Loi("Mã khuyến mãi đã hết lượt sử dụng!", p);
            // 8. Mã giới hạn mỗi người hoặc mã thân thiết cần đăng nhập
            if (maHocVien <= 0 && (p.GioiHanMoiHocVien.HasValue || p.LaThanThiet))
                return KetQuaApDungUuDai.Loi(p.LaThanThiet
                    ? "Vui lòng đăng nhập để sử dụng ưu đãi dành cho học viên thân thiết!"
                    : "Vui lòng đăng nhập để sử dụng mã khuyến mãi này!", p, requireLogin: true);
            // 9. Chưa dùng hết lượt của mình
            if (p.GioiHanMoiHocVien.HasValue)
            {
                int daDung = LuotDungCuaHocVien(maHocVien, p.MaGiamGia);
                if (daDung >= p.GioiHanMoiHocVien.Value)
                    return KetQuaApDungUuDai.Loi("Bạn đã dùng mã này " + daDung + " lần (tối đa " + p.GioiHanMoiHocVien.Value + " lần mỗi học viên)!", p);
            }
            // 10. Đạt tiêu chí thân thiết
            if (p.LaThanThiet)
            {
                var hv = ThongKeHocVien(maHocVien, p.MaKhuyenMai);
                if (!DatTieuChi(p, hv))
                    return KetQuaApDungUuDai.Loi("Ưu đãi dành cho học viên thân thiết. Bạn cần " + PhanConThieu(p, hv) + "!", p);
            }
            // 11. Giỏ hàng không trống
            if (gioHang == null || gioHang.Count == 0) return KetQuaApDungUuDai.Loi("Giỏ hàng của bạn đang trống!", p);

            // 12. Có khóa thuộc phạm vi
            var apDung = gioHang.ToList();
            if (p.PhamVi == "KhoaHoc")
            {
                var trongPhamVi = new HashSet<string>(LayKhoaHocApDung(p.MaKhuyenMai).Select(k => k.MaKhoaHoc));
                apDung = gioHang.Where(g => trongPhamVi.Contains(g.MaKhoaHoc)).ToList();
                if (apDung.Count == 0) return KetQuaApDungUuDai.Loi("Mã không áp dụng cho các khóa học trong giỏ hàng của bạn!", p);
            }
            else if (p.PhamVi == "DanhMuc")
            {
                var ids = gioHang.Select(g => g.MaKhoaHoc).ToList();
                var danhMuc = db.KhoaHoc.Where(k => ids.Contains(k.maKhoaHoc))
                    .Select(k => new KhoaDanhMuc { MaKhoaHoc = k.maKhoaHoc, MaDanhMuc = k.maDanhMuc }).ToList()
                    .ToDictionary(k => k.MaKhoaHoc, k => k.MaDanhMuc);
                apDung = gioHang.Where(g => danhMuc.ContainsKey(g.MaKhoaHoc) && danhMuc[g.MaKhoaHoc] == p.MaDanhMucApDung).ToList();
                if (apDung.Count == 0)
                    return KetQuaApDungUuDai.Loi("Mã chỉ áp dụng cho khóa học thuộc danh mục \"" + (p.TenDanhMucApDung ?? p.MaDanhMucApDung) + "\"!", p);
            }

            // 13. Tổng giá khóa thuộc phạm vi ≥ đơn tối thiểu
            decimal tienApDung = apDung.Sum(g => g.DonGia);
            if (p.DonHangToiThieu.HasValue && tienApDung < p.DonHangToiThieu.Value)
                return KetQuaApDungUuDai.Loi("Giá trị khóa học được áp dụng cần từ " + p.DonHangToiThieu.Value.ToString("N0") + " đ (hiện có " + tienApDung.ToString("N0") + " đ)!", p);

            // Tiền giảm: % làm tròn, có trần thì không vượt trần; số tiền cố định; không giảm quá phần được áp dụng
            decimal giam = p.LaPhanTram
                ? Math.Round(tienApDung * (decimal)p.MucUuDai / 100m, 0, MidpointRounding.AwayFromZero)
                : p.SoTienGiam;
            if (p.LaPhanTram && p.GiamToiDa.HasValue) giam = Math.Min(giam, p.GiamToiDa.Value);
            giam = Math.Min(giam, tienApDung);
            if (giam <= 0) return KetQuaApDungUuDai.Loi("Mã khuyến mãi không tạo ra khoản giảm nào cho giỏ hàng này!", p);

            return new KetQuaApDungUuDai
            {
                Success = true,
                ChuongTrinh = p,
                MaKhuyenMai = p.MaKhuyenMai,
                MaGiamGia = p.MaGiamGia,
                MaCode = p.MaCode,
                TienApDung = tienApDung,
                TienGiam = giam,
                SoKhoaApDung = apDung.Count,
                KhoaApDung = apDung.Select(g => g.MaKhoaHoc).ToList(),
                Message = "Đã áp dụng mã " + p.MaCode + ": giảm " + giam.ToString("N0") + " đ (cho " + apDung.Count + "/" + gioHang.Count + " khóa học trong giỏ)!"
            };
        }

        /// <summary>
        /// Ghi nhận lượt dùng ưu đãi cho đơn vừa tạo (gọi trong transaction tạo đơn). Lượt dùng chỉ được tính khi đơn chuyển sang
        /// đã thanh toán (mọi truy vấn lượt dùng đều nối với đơn đã thanh toán); mã vào ví ở ThemMaVaoViKhiDaThanhToan.
        /// </summary>
        public void GhiNhanSuDung(string maDonHang, int maHocVien, KetQuaApDungUuDai kq)
        {
            db.Database.ExecuteSqlCommand(@"
                INSERT INTO dbo.SuDungUuDai (maDonHang, maKhuyenMai, maGiamGia, maHocVien, tienTruocGiam, tienGiam)
                VALUES (@dh, @km, @mgg, @hv, @truoc, @giam)",
                new SqlParameter("@dh", maDonHang),
                new SqlParameter("@km", kq.MaKhuyenMai),
                new SqlParameter("@mgg", kq.MaGiamGia),
                new SqlParameter("@hv", maHocVien),
                new SqlParameter("@truoc", kq.TienApDung),
                new SqlParameter("@giam", kq.TienGiam));
        }

        /// <summary>
        /// Đơn vừa chuyển sang đã thanh toán (đặc tả mục 10, bước 7): mã của đơn chưa có trong ví (mã nhập tay) thì thêm vào ví
        /// để giữ lịch sử. Gọi trong transaction ghi nhận thanh toán; chưa chạy migration 008/009 thì bỏ qua.
        /// </summary>
        public static void ThemMaVaoViKhiDaThanhToan(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, string maDonHang)
        {
            db.Database.ExecuteSqlCommand(@"
                IF OBJECT_ID(N'dbo.ViMaGiamGia', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.SuDungUuDai', N'U') IS NOT NULL
                    INSERT INTO dbo.ViMaGiamGia (maHocVien, maGiamGia)
                    SELECT DISTINCT s.maHocVien, s.maGiamGia FROM dbo.SuDungUuDai s
                    WHERE s.maDonHang = @dh
                      AND NOT EXISTS (SELECT 1 FROM dbo.ViMaGiamGia v WHERE v.maHocVien = s.maHocVien AND v.maGiamGia = s.maGiamGia)",
                new SqlParameter("@dh", maDonHang));
        }
    }
}
