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
    /// Chương trình khuyến mãi / coupon (phân hệ Marketing) và luồng áp dụng ưu đãi khi thanh toán.
    /// Mọi giá và số tiền giảm được tính ở server từ CSDL, không tin giá trị trình duyệt gửi lên.
    /// Các cột cấu hình và bảng SuDungUuDai thuộc Database/Migrations/008_Marketing_KhuyenMai.sql (chưa có trong mô hình EF).
    /// </summary>
    public partial class PromotionService
    {
        public const string HoatDong = "HoatDong";
        public const string TamNgung = "TamNgung";
        public const string KetThuc = "KetThuc";

        /// <summary>Trạng thái đơn hàng được tính là giao dịch thành công.</summary>
        public const string DonDaThanhToan = "Đã thanh toán";

        private static readonly Regex DinhDangMa = new Regex("^[A-Z0-9_-]{3,30}$");

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public PromotionService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        /// <summary>Đã chạy migration 008 và 009 hay chưa.</summary>
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

        /// <summary>
        /// Giá bán hiện tại của khóa học: giá giảm do Marketing đặt (nếu hợp lệ: 0 &lt; giá giảm &lt; giá gốc), ngược lại giá gốc.
        /// </summary>
        public static decimal GiaBanHienTai(KhoaHoc k)
        {
            if (k.giaKhuyenMai.HasValue && k.giaKhuyenMai.Value > 0 && k.giaKhuyenMai.Value < k.giaBan)
            {
                return k.giaKhuyenMai.Value;
            }
            return k.giaBan;
        }

        // ================================================================== đọc dữ liệu

        private const string SelectChuongTrinh = @"
            SELECT km.maKhuyenMai AS MaKhuyenMai, km.tenKhuyenMai AS TenKhuyenMai, km.mucUuDai AS MucUuDai,
                   km.loaiUuDai AS LoaiUuDai, km.giamToiDa AS GiamToiDa, km.donHangToiThieu AS DonHangToiThieu,
                   km.phamVi AS PhamVi, km.doiTuong AS DoiTuong, km.soKhoaToiThieu AS SoKhoaToiThieu,
                   km.chiTieuToiThieu AS ChiTieuToiThieu, km.trangThai AS TrangThaiLuu,
                   km.ngayBatDau AS NgayBatDau, km.ngayKetThuc AS NgayKetThuc, km.dieuKienApDung AS DieuKienApDung,
                   km.maChienDich AS MaChienDich, cd.tenChienDich AS TenChienDich, km.ngayTao AS NgayTao, km.congKhai AS CongKhai, km.chinhSach AS ChinhSach,
                   mg.maGiamGia AS MaGiamGia, mg.maCode AS MaCode, CAST(ISNULL(mg.trangThai, 1) AS BIT) AS CouponDangBat,
                   mg.gioiHanLuotDung AS GioiHanLuotDung, ISNULL(mg.gioiHanMoiHocVien, 1) AS GioiHanMoiHocVien,
                   ISNULL(sd.soLuot, 0) AS SoLuotDaDung,
                   CAST(ISNULL(sd.tienGiam, 0) AS DECIMAL(18, 2)) AS TongTienGiam,
                   CAST(ISNULL(sd.doanhThu, 0) AS DECIMAL(18, 2)) AS DoanhThuSauGiam,
                   (SELECT COUNT(*) FROM dbo.KhuyenMaiKhoaHoc k WHERE k.maKhuyenMai = km.maKhuyenMai) AS SoKhoaApDung
            FROM dbo.KhuyenMai km
            OUTER APPLY (SELECT TOP (1) m.* FROM dbo.MaGiamGia m WHERE m.maKhuyenMai = km.maKhuyenMai ORDER BY m.maGiamGia) mg
            LEFT JOIN dbo.ChienDich cd ON cd.maChienDich = km.maChienDich
            OUTER APPLY (
                SELECT COUNT(*) AS soLuot, SUM(s.tienGiam) AS tienGiam, SUM(d.tongTien) AS doanhThu
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai = N'Đã thanh toán'
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

        /// <summary>Khóa học được chọn làm phạm vi áp dụng: chỉ khóa đã xuất bản.</summary>
        public List<LuaChonKhoaHoc> LayKhoaHocCoTheApDung()
        {
            return db.Database.SqlQuery<LuaChonKhoaHoc>(@"
                SELECT kh.maKhoaHoc AS MaKhoaHoc, kh.tenKhoaHoc AS TenKhoaHoc, ISNULL(dm.tenDanhMuc, N'') AS TenDanhMuc, kh.giaBan AS GiaBan
                FROM dbo.KhoaHoc kh
                LEFT JOIN dbo.DanhMuc dm ON dm.maDanhMuc = kh.maDanhMuc
                WHERE kh.trangThai = N'Đã xuất bản' AND kh.giaBan > 0
                ORDER BY dm.tenDanhMuc, kh.tenKhoaHoc").ToList();
        }

        public List<LuaChonChienDich> LayChienDichDangMo()
        {
            return db.Database.SqlQuery<LuaChonChienDich>(@"
                SELECT maChienDich AS MaChienDich, tenChienDich AS TenChienDich
                FROM dbo.ChienDich
                WHERE trangThai = N'HoatDong' AND ngayKetThuc >= CAST(GETDATE() AS DATE)
                ORDER BY ngayBatDau DESC").ToList();
        }

        public List<LuotSuDungUuDai> LayLuotSuDung(string maKhuyenMai)
        {
            return db.Database.SqlQuery<LuotSuDungUuDai>(@"
                SELECT s.maDonHang AS MaDonHang, s.ngaySuDung AS NgaySuDung, nd.hoTen AS HoTen, nd.email AS Email,
                       s.tienTruocGiam AS TienTruocGiam, s.tienGiam AS TienGiam, d.tongTien AS TongDonHang
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai = N'Đã thanh toán'
                INNER JOIN dbo.NguoiDung nd ON nd.maNguoiDung = s.maHocVien
                WHERE s.maKhuyenMai = @ma
                ORDER BY s.ngaySuDung DESC",
                new SqlParameter("@ma", maKhuyenMai)).ToList();
        }

        // ================================================================== tạo / sửa

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
                GiaTriUuDai = (decimal)p.MucUuDai,
                GiamToiDa = p.GiamToiDa,
                DonHangToiThieu = p.DonHangToiThieu,
                NgayBatDau = p.NgayBatDau.Date,
                NgayKetThuc = p.NgayKetThuc.Date,
                GioiHanLuotDung = p.GioiHanLuotDung,
                GioiHanMoiHocVien = p.GioiHanMoiHocVien,
                PhamVi = p.PhamVi,
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
        /// Kiểm tra dữ liệu form. Trả về lỗi theo tên trường (rỗng = hợp lệ). Chuẩn hóa dữ liệu trong form (mã in hoa,
        /// bỏ trường không dùng theo loại ưu đãi / đối tượng / phạm vi).
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
            f.PhamVi = f.PhamVi == "KhoaHoc" ? "KhoaHoc" : "TatCa";
            f.DoiTuong = f.DoiTuong == "ThanThiet" ? "ThanThiet" : "TatCa";
            f.KhoaHocApDung = (f.KhoaHocApDung ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

            if (f.ChinhSach != null && f.ChinhSach.Length > 1000) loi["ChinhSach"] = "Mô tả chính sách tối đa 1000 ký tự.";
            if (f.TenKhuyenMai.Length == 0) loi["TenKhuyenMai"] = "Vui lòng nhập tên chương trình.";
            else if (f.TenKhuyenMai.Length > 150) loi["TenKhuyenMai"] = "Tên chương trình tối đa 150 ký tự.";

            // Mã coupon: không trùng; đã có lượt dùng thì không đổi mã để giữ lịch sử đối soát
            if (f.MaCode.Length == 0) loi["MaCode"] = "Vui lòng nhập mã coupon.";
            else if (!DinhDangMa.IsMatch(f.MaCode)) loi["MaCode"] = "Mã coupon gồm 3–30 ký tự: chữ in hoa, số, dấu gạch ngang hoặc gạch dưới.";
            else if (laSua && hienTai.SoLuotDaDung > 0 && !string.Equals(f.MaCode, hienTai.MaCode, StringComparison.OrdinalIgnoreCase))
                loi["MaCode"] = "Mã đã có lượt sử dụng nên không thể đổi.";
            else if (db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.MaGiamGia WHERE UPPER(maCode) = @ma AND maKhuyenMai <> @km",
                         new SqlParameter("@ma", f.MaCode), new SqlParameter("@km", laSua ? hienTai.MaKhuyenMai : "")).First() > 0)
                loi["MaCode"] = "Mã coupon \"" + f.MaCode + "\" đã tồn tại.";

            // Giá trị ưu đãi theo loại
            if (!f.GiaTriUuDai.HasValue) loi["GiaTriUuDai"] = "Vui lòng nhập giá trị ưu đãi.";
            else if (f.LoaiUuDai == "PhanTram" && (f.GiaTriUuDai < 1 || f.GiaTriUuDai > 100 || f.GiaTriUuDai != decimal.Truncate(f.GiaTriUuDai.Value)))
                loi["GiaTriUuDai"] = "Phần trăm giảm là số nguyên từ 1 đến 100.";
            else if (f.LoaiUuDai == "SoTien" && (f.GiaTriUuDai < 1000 || f.GiaTriUuDai != decimal.Truncate(f.GiaTriUuDai.Value)))
                loi["GiaTriUuDai"] = "Số tiền giảm phải là số nguyên từ 1.000 đ.";
            if (f.LoaiUuDai == "SoTien") f.GiamToiDa = null;
            if (f.GiamToiDa.HasValue && f.GiamToiDa <= 0) loi["GiamToiDa"] = "Mức giảm tối đa phải lớn hơn 0.";
            if (f.DonHangToiThieu.HasValue && f.DonHangToiThieu < 0) loi["DonHangToiThieu"] = "Giá trị đơn tối thiểu không được âm.";

            // Thời gian
            if (!f.NgayBatDau.HasValue) loi["NgayBatDau"] = "Vui lòng chọn ngày bắt đầu.";
            if (!f.NgayKetThuc.HasValue) loi["NgayKetThuc"] = "Vui lòng chọn ngày kết thúc.";
            if (f.NgayBatDau.HasValue && f.NgayKetThuc.HasValue)
            {
                if (f.NgayKetThuc.Value.Date < f.NgayBatDau.Value.Date) loi["NgayKetThuc"] = "Ngày kết thúc không được trước ngày bắt đầu.";
                else if (f.NgayKetThuc.Value.Date < DateTime.Today) loi["NgayKetThuc"] = "Ngày kết thúc đã ở quá khứ.";
            }

            // Giới hạn lượt dùng
            if (f.GioiHanLuotDung.HasValue && f.GioiHanLuotDung <= 0) loi["GioiHanLuotDung"] = "Giới hạn lượt dùng phải lớn hơn 0 (bỏ trống nếu không giới hạn).";
            else if (laSua && f.GioiHanLuotDung.HasValue && f.GioiHanLuotDung < hienTai.SoLuotDaDung)
                loi["GioiHanLuotDung"] = "Mã đã được dùng " + hienTai.SoLuotDaDung + " lượt, giới hạn không được nhỏ hơn số này.";
            if (f.GioiHanMoiHocVien <= 0) loi["GioiHanMoiHocVien"] = "Số lượt mỗi học viên phải lớn hơn 0.";

            // Phạm vi khóa học: phải tồn tại và đang mở bán
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

            // Đối tượng thân thiết: cần ít nhất một tiêu chí
            if (f.DoiTuong == "ThanThiet")
            {
                if (!f.SoKhoaToiThieu.HasValue && !f.ChiTieuToiThieu.HasValue)
                    loi["SoKhoaToiThieu"] = "Ưu đãi thân thiết cần ít nhất một tiêu chí: số khóa đã đăng ký hoặc tổng chi tiêu.";
                if (f.SoKhoaToiThieu.HasValue && f.SoKhoaToiThieu <= 0) loi["SoKhoaToiThieu"] = "Số khóa tối thiểu phải lớn hơn 0.";
                if (f.ChiTieuToiThieu.HasValue && f.ChiTieuToiThieu <= 0) loi["ChiTieuToiThieu"] = "Chi tiêu tối thiểu phải lớn hơn 0.";
            }
            else
            {
                f.SoKhoaToiThieu = null;
                f.ChiTieuToiThieu = null;
            }

            // Chiến dịch phải còn hoạt động (giữ được chiến dịch cũ khi sửa)
            if (!string.IsNullOrEmpty(f.MaChienDich)
                && !LayChienDichDangMo().Any(c => c.MaChienDich == f.MaChienDich)
                && !(laSua && f.MaChienDich == hienTai.MaChienDich))
                loi["MaChienDich"] = "Chiến dịch không tồn tại hoặc đã kết thúc.";
            if (string.IsNullOrEmpty(f.MaChienDich)) f.MaChienDich = null;

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
                                               loaiUuDai, giamToiDa, donHangToiThieu, phamVi, doiTuong, soKhoaToiThieu, chiTieuToiThieu,
                                               trangThai, maChienDich, congKhai, chinhSach)
                    VALUES (@ma, @ten, @muc, @bd, @kt, @dk, @nv, @loai, @toiDa, @donMin, @phamVi, @doiTuong, @soKhoa, @chiTieu, N'HoatDong', @cd, @congKhai, @chinhSach)",
                    ThamSoChuongTrinh(f, maKm, maNhanVien).ToArray());

                db.Database.ExecuteSqlCommand(@"
                    INSERT INTO dbo.MaGiamGia (maGiamGia, maCode, giaTriGiam, trangThai, maKhuyenMai, gioiHanLuotDung, gioiHanMoiHocVien)
                    VALUES (@mgg, @code, @giaTri, 1, @km, @gioiHan, @moiHv)",
                    new SqlParameter("@mgg", maMgg),
                    new SqlParameter("@code", f.MaCode),
                    new SqlParameter("@giaTri", f.GiaTriUuDai.Value),
                    new SqlParameter("@km", maKm),
                    new SqlParameter("@gioiHan", (object)f.GioiHanLuotDung ?? DBNull.Value),
                    new SqlParameter("@moiHv", f.GioiHanMoiHocVien));

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
                        loaiUuDai = @loai, giamToiDa = @toiDa, donHangToiThieu = @donMin, phamVi = @phamVi, doiTuong = @doiTuong,
                        soKhoaToiThieu = @soKhoa, chiTieuToiThieu = @chiTieu, maChienDich = @cd, congKhai = @congKhai, chinhSach = @chinhSach
                    WHERE maKhuyenMai = @ma",
                    ThamSoChuongTrinh(f, hienTai.MaKhuyenMai, 0).Where(p => p.ParameterName != "@nv").ToArray());

                db.Database.ExecuteSqlCommand(@"
                    UPDATE dbo.MaGiamGia
                    SET maCode = @code, giaTriGiam = @giaTri, gioiHanLuotDung = @gioiHan, gioiHanMoiHocVien = @moiHv
                    WHERE maGiamGia = @mgg",
                    new SqlParameter("@code", f.MaCode),
                    new SqlParameter("@giaTri", f.GiaTriUuDai.Value),
                    new SqlParameter("@gioiHan", (object)f.GioiHanLuotDung ?? DBNull.Value),
                    new SqlParameter("@moiHv", f.GioiHanMoiHocVien),
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

        /// <summary>
        /// Kích hoạt / tạm ngừng / kết thúc. Kết thúc là trạng thái cuối, giữ nguyên lịch sử và giao dịch cũ.
        /// Trạng thái coupon (MaGiamGia.trangThai) đồng bộ theo để các hàm SQL cũ (fn_KiemTraCoupon) cùng kết quả.
        /// </summary>
        public string DoiTrangThai(string maKhuyenMai, string hanhDong, out bool thanhCong)
        {
            thanhCong = false;
            var p = LayTheoMa(maKhuyenMai);
            if (p == null) return "Không tìm thấy chương trình khuyến mãi.";
            var now = DateTime.Now;
            string hienThi = p.TrangThaiHienThi(now);

            string moi;
            switch (hanhDong)
            {
                case "pause":
                    if (hienThi == "Đã kết thúc") return "Chương trình đã kết thúc, không thể tạm ngừng.";
                    if (p.TrangThaiLuu == TamNgung) return "Chương trình đang tạm ngừng.";
                    moi = TamNgung;
                    break;
                case "resume":
                    if (hienThi == "Đã kết thúc") return "Chương trình đã kết thúc, không thể kích hoạt lại.";
                    moi = HoatDong;
                    break;
                case "end":
                    if (p.TrangThaiLuu == KetThuc) return "Chương trình đã kết thúc trước đó.";
                    moi = KetThuc;
                    break;
                default:
                    return "Thao tác không hợp lệ.";
            }

            db.Database.ExecuteSqlCommand(@"
                UPDATE dbo.KhuyenMai SET trangThai = @tt WHERE maKhuyenMai = @ma;
                UPDATE dbo.MaGiamGia SET trangThai = @bat WHERE maKhuyenMai = @ma;",
                new SqlParameter("@tt", moi),
                new SqlParameter("@bat", moi == HoatDong),
                new SqlParameter("@ma", maKhuyenMai));
            thanhCong = true;
            return moi == TamNgung ? "Đã tạm ngừng chương trình \"" + p.TenKhuyenMai + "\"."
                 : moi == HoatDong ? "Đã kích hoạt lại chương trình \"" + p.TenKhuyenMai + "\"."
                 : "Đã kết thúc chương trình \"" + p.TenKhuyenMai + "\". Lịch sử sử dụng được giữ nguyên.";
        }

        // ================================================================== học viên thân thiết

        private const string SelectHocVien = @"
            SELECT nd.maNguoiDung AS MaNguoiDung, nd.hoTen AS HoTen, nd.email AS Email,
                   (SELECT COUNT(DISTINCT dk.maKhoaHoc) FROM dbo.DangKyKhoaHoc dk WHERE dk.maHocVien = nd.maNguoiDung) AS SoKhoa,
                   CAST(ISNULL((SELECT SUM(d.tongTien) FROM dbo.DonHang d WHERE d.maHocVien = nd.maNguoiDung AND d.trangThai = N'Đã thanh toán'), 0) AS DECIMAL(18, 2)) AS ChiTieu,
                   (SELECT COUNT(*) FROM dbo.SuDungUuDai s WHERE s.maHocVien = nd.maNguoiDung AND s.maKhuyenMai = @km) AS SoLuotDaDung
            FROM dbo.NguoiDung nd";

        /// <summary>
        /// Số khóa đã đăng ký thành công (mỗi bản ghi DangKyKhoaHoc là một đăng ký đã hoàn tất, gồm cả khóa miễn phí)
        /// và tổng chi tiêu (chỉ đơn "Đã thanh toán", không tính đơn chờ / thất bại).
        /// </summary>
        public HocVienThanThietItem ThongKeHocVien(int maHocVien, string maKhuyenMai = "")
        {
            return db.Database.SqlQuery<HocVienThanThietItem>(SelectHocVien + " WHERE nd.maNguoiDung = @hv",
                new SqlParameter("@km", maKhuyenMai ?? ""), new SqlParameter("@hv", maHocVien)).FirstOrDefault();
        }

        public static bool DatTieuChi(ChuongTrinhKhuyenMai p, HocVienThanThietItem hv)
        {
            if (!p.LaThanThiet) return true;
            if (hv == null) return false;
            if (p.SoKhoaToiThieu.HasValue && hv.SoKhoa < p.SoKhoaToiThieu.Value) return false;
            if (p.ChiTieuToiThieu.HasValue && hv.ChiTieu < p.ChiTieuToiThieu.Value) return false;
            return true;
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

        // ================================================================== áp dụng coupon

        /// <summary>
        /// Kiểm tra mã coupon cho giỏ hàng và tính số tiền giảm ở server: trạng thái, thời gian, giới hạn tổng lượt,
        /// giới hạn mỗi học viên, tiêu chí thân thiết, phạm vi khóa học, giá trị đơn tối thiểu, mức giảm tối đa.
        /// Mỗi đơn chỉ áp dụng một coupon. <paramref name="maHocVien"/> = 0 khi chưa đăng nhập (kiểm tra lại lúc thanh toán).
        /// </summary>
        public KetQuaApDungUuDai KiemTraCoupon(int maHocVien, string maCode, IList<DongGioHang> gioHang)
        {
            if (string.IsNullOrWhiteSpace(maCode)) return KetQuaApDungUuDai.Loi("Vui lòng nhập mã khuyến mãi.");
            if (gioHang == null || gioHang.Count == 0) return KetQuaApDungUuDai.Loi("Giỏ hàng đang trống.");

            string code = maCode.Trim().ToUpperInvariant();
            string maKm = db.Database.SqlQuery<string>("SELECT TOP (1) maKhuyenMai FROM dbo.MaGiamGia WHERE UPPER(maCode) = @code",
                new SqlParameter("@code", code)).FirstOrDefault();
            var p = LayTheoMa(maKm);
            if (p == null || p.MaGiamGia == null) return KetQuaApDungUuDai.Loi("Mã khuyến mãi không tồn tại.");

            var now = DateTime.Now;
            if (p.TrangThaiLuu == KetThuc) return KetQuaApDungUuDai.Loi("Chương trình khuyến mãi đã kết thúc.");
            if (p.TrangThaiLuu == TamNgung || !p.CouponDangBat) return KetQuaApDungUuDai.Loi("Mã khuyến mãi đang tạm ngừng áp dụng.");
            if (p.NgayBatDau > now) return KetQuaApDungUuDai.Loi("Chương trình khuyến mãi chưa bắt đầu (từ " + p.NgayBatDau.ToString("dd/MM/yyyy") + ").");
            if (p.NgayKetThuc < now) return KetQuaApDungUuDai.Loi("Mã khuyến mãi đã hết hạn.");
            if (p.HetLuot) return KetQuaApDungUuDai.Loi("Mã khuyến mãi đã hết lượt sử dụng.");

            if (maHocVien > 0)
            {
                int daDung = db.Database.SqlQuery<int>(@"
                    SELECT COUNT(*) FROM dbo.SuDungUuDai s
                    INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai = N'Đã thanh toán'
                    WHERE s.maGiamGia = @mgg AND s.maHocVien = @hv",
                    new SqlParameter("@mgg", p.MaGiamGia), new SqlParameter("@hv", maHocVien)).First();
                if (daDung >= p.GioiHanMoiHocVien)
                    return KetQuaApDungUuDai.Loi("Bạn đã dùng hết số lượt cho phép của mã này (" + p.GioiHanMoiHocVien + " lượt).");
            }

            if (p.LaThanThiet)
            {
                if (maHocVien <= 0) return KetQuaApDungUuDai.Loi("Mã dành cho học viên thân thiết. Vui lòng đăng nhập để sử dụng.");
                var hv = ThongKeHocVien(maHocVien, p.MaKhuyenMai);
                if (!DatTieuChi(p, hv))
                {
                    var thieu = new List<string>();
                    if (p.SoKhoaToiThieu.HasValue && (hv == null || hv.SoKhoa < p.SoKhoaToiThieu.Value))
                        thieu.Add("đã đăng ký từ " + p.SoKhoaToiThieu.Value + " khóa (bạn có " + (hv?.SoKhoa ?? 0) + ")");
                    if (p.ChiTieuToiThieu.HasValue && (hv == null || hv.ChiTieu < p.ChiTieuToiThieu.Value))
                        thieu.Add("tổng chi tiêu từ " + p.ChiTieuToiThieu.Value.ToString("N0") + " đ");
                    return KetQuaApDungUuDai.Loi("Mã dành cho học viên thân thiết: cần " + string.Join(" và ", thieu) + ".");
                }
            }

            // Phạm vi khóa học
            var apDung = gioHang.ToList();
            if (p.PhamVi == "KhoaHoc")
            {
                var trongPhamVi = new HashSet<string>(LayKhoaHocApDung(p.MaKhuyenMai).Select(k => k.MaKhoaHoc));
                apDung = gioHang.Where(g => trongPhamVi.Contains(g.MaKhoaHoc)).ToList();
                if (apDung.Count == 0) return KetQuaApDungUuDai.Loi("Mã không áp dụng cho khóa học nào trong giỏ hàng.");
            }
            decimal tienApDung = apDung.Sum(g => g.DonGia);
            if (tienApDung <= 0) return KetQuaApDungUuDai.Loi("Khóa học trong giỏ đã miễn phí, không cần mã giảm giá.");
            if (p.DonHangToiThieu.HasValue && tienApDung < p.DonHangToiThieu.Value)
                return KetQuaApDungUuDai.Loi("Mã áp dụng cho đơn từ " + p.DonHangToiThieu.Value.ToString("N0") + " đ (các khóa áp dụng hiện có " + tienApDung.ToString("N0") + " đ).");

            decimal giam = p.LaPhanTram
                ? Math.Floor(tienApDung * (decimal)p.MucUuDai / 100m)
                : (decimal)p.MucUuDai;
            if (p.GiamToiDa.HasValue) giam = Math.Min(giam, p.GiamToiDa.Value);
            giam = Math.Min(giam, tienApDung);

            return new KetQuaApDungUuDai
            {
                Success = true,
                MaKhuyenMai = p.MaKhuyenMai,
                MaGiamGia = p.MaGiamGia,
                MaCode = p.MaCode,
                TienApDung = tienApDung,
                TienGiam = giam,
                SoKhoaApDung = apDung.Count,
                Message = "Đã áp dụng mã " + p.MaCode + ": " + p.MoTaUuDai.ToLower()
                    + (p.PhamVi == "KhoaHoc" ? " cho " + apDung.Count + " khóa học trong giỏ" : "")
                    // Giảm số tiền cố định thì mô tả đã nêu số tiền, không lặp lại
                    + (p.LaPhanTram || giam != (decimal)p.MucUuDai ? " (giảm " + giam.ToString("N0") + " đ)." : ".")
            };
        }

        /// <summary>Ghi nhận lượt dùng ưu đãi cho đơn hàng vừa thanh toán (gọi trong transaction của thanh toán).</summary>
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

            // Mã nhập tay (kể cả mã kín) cũng vào ví để trang "Mã giảm giá của tôi" có lịch sử
            db.Database.ExecuteSqlCommand(@"
                IF NOT EXISTS (SELECT 1 FROM dbo.ViMaGiamGia WHERE maHocVien = @hv AND maGiamGia = @mgg)
                    INSERT INTO dbo.ViMaGiamGia (maHocVien, maGiamGia) VALUES (@hv, @mgg)",
                new SqlParameter("@hv", maHocVien),
                new SqlParameter("@mgg", kq.MaGiamGia));
        }
    }
}
