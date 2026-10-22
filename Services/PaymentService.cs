using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Thanh toán ở chế độ THỬ NGHIỆM (chưa có pháp nhân nên chưa ký hợp đồng với cổng thật), nhưng đi đúng luồng của cổng thật:
    ///   Giỏ hàng tạo đơn "Chờ thanh toán" (hạn 15 phút) → trang cổng thử nghiệm (/Payment/Gateway)
    ///   → cổng gửi kết quả có chữ ký HMAC-SHA512 (IPN, cùng kiểu VNPay) → XuLyKetQua kiểm tra chữ ký, số tiền, trạng thái đơn
    ///   → ghi ThanhToan "Đã thanh toán" (trigger trg_ThanhToan_TuDongDangKy tự ghi danh học viên) → đơn "Đã thanh toán".
    /// Trang quay về (Return) chỉ để hiển thị; ghi danh chỉ xảy ra khi nhận IPN hợp lệ. Mọi lần gọi đều ghi NhatKyThanhToan.
    /// Khi có hợp đồng với cổng thật: thay trang /Payment/Gateway bằng URL của cổng, đổi khóa bí mật; phần xử lý IPN giữ nguyên.
    /// Cột / bảng mới thuộc Database/Migrations/014_ThanhToan_ThuNghiem.sql (đọc / ghi bằng SQL, ngoài mô hình EF).
    /// </summary>
    public class PaymentService
    {
        public const string ChoThanhToan = "Chờ thanh toán";
        public const string DaThanhToan = PromotionService.DonDaThanhToan;
        public const string ThatBai = "Thanh toán thất bại";
        public const string HetHan = "Hết hạn";
        public const string DaHuy = "Đã hủy";

        public const string CongThuNghiem = "Cổng thử nghiệm";
        public const string KhongQuaCong = "Không qua cổng";
        public const int PhutHetHan = 15;

        // Mã kết quả giao dịch (theo bảng mã của VNPay) và mã phản hồi IPN cho cổng
        public const string GdThanhCong = "00";
        public const string GdKhachHuy = "24";
        public const string GdKhongDuSoDu = "51";

        public static readonly string[] PhuongThucHopLe = { "MoMo/VNPay", "Napas", "Visa/Mastercard" };

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public PaymentService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public bool IsInstalled()
        {
            return db.Database.SqlQuery<int>(
                "SELECT CASE WHEN COL_LENGTH('dbo.DonHang', 'ngayHetHan') IS NULL OR OBJECT_ID('dbo.NhatKyThanhToan') IS NULL THEN 0 ELSE 1 END").Single() == 1;
        }

        // ------------------------------------------------------------------ chữ ký

        /// <summary>Khóa bí mật của cổng thử nghiệm: appSettings "PaymentSandbox:HashSecret"; không cấu hình thì dùng khóa mặc định (chỉ để thử nghiệm).</summary>
        private static string KhoaBiMat
        {
            get
            {
                string k = ConfigurationManager.AppSettings["PaymentSandbox:HashSecret"];
                return string.IsNullOrWhiteSpace(k) ? "ECOLEARNING-SANDBOX-HASH-SECRET-2026" : k;
            }
        }

        /// <summary>Chuỗi ký: các tham số (trừ signature) sắp theo tên, nối dạng tên=giá trị đã URL-encode bằng dấu &amp; (cách VNPay).</summary>
        private static string ChuoiKy(IDictionary<string, string> thamSo)
        {
            return string.Join("&", thamSo
                .Where(kv => kv.Key != "signature" && !string.IsNullOrEmpty(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Key + "=" + HttpUtility.UrlEncode(kv.Value)));
        }

        public static string Ky(IDictionary<string, string> thamSo)
        {
            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(KhoaBiMat)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(ChuoiKy(thamSo)));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static bool ChuKyHopLe(IDictionary<string, string> thamSo)
        {
            string chuKy;
            if (!thamSo.TryGetValue("signature", out chuKy) || string.IsNullOrEmpty(chuKy)) return false;
            string dung = Ky(thamSo);
            // So sánh thời gian cố định để không lộ chữ ký đúng qua thời gian phản hồi
            if (dung.Length != chuKy.Length) return false;
            int khac = 0;
            for (int i = 0; i < dung.Length; i++) khac |= dung[i] ^ char.ToLowerInvariant(chuKy[i]);
            return khac == 0;
        }

        // ------------------------------------------------------------------ đơn hàng

        public DonThanhToan LayDon(string maDonHang)
        {
            if (string.IsNullOrWhiteSpace(maDonHang)) return null;
            var don = db.Database.SqlQuery<DonThanhToan>(@"
                SELECT d.maDonHang AS MaDonHang, d.maHocVien AS MaHocVien, d.tongTien AS TongTien, d.trangThai AS TrangThai,
                       d.ngayDat AS NgayDat, d.ngayHetHan AS NgayHetHan, d.ngayThanhToan AS NgayThanhToan, d.phuongThuc AS PhuongThuc,
                       d.maGiamGia AS MaGiamGia
                FROM dbo.DonHang d WHERE d.maDonHang = @dh", new SqlParameter("@dh", maDonHang)).FirstOrDefault();
            if (don != null) NapKhoaHoc(new List<DonThanhToan> { don });
            return don;
        }

        /// <summary>Lịch sử mua hàng của học viên, đơn mới nhất trước.</summary>
        public List<DonThanhToan> LichSu(int maHocVien)
        {
            var list = db.Database.SqlQuery<DonThanhToan>(@"
                SELECT d.maDonHang AS MaDonHang, d.maHocVien AS MaHocVien, d.tongTien AS TongTien, d.trangThai AS TrangThai,
                       d.ngayDat AS NgayDat, d.ngayHetHan AS NgayHetHan, d.ngayThanhToan AS NgayThanhToan, d.phuongThuc AS PhuongThuc,
                       d.maGiamGia AS MaGiamGia,
                       (SELECT TOP 1 t.maGiaoDichCong FROM dbo.ThanhToan t WHERE t.maDonHang = d.maDonHang AND t.trangThaiThanhToan = @paid ORDER BY t.ngayThanhToan DESC) AS MaGiaoDich
                FROM dbo.DonHang d WHERE d.maHocVien = @hv
                ORDER BY d.ngayDat DESC", new SqlParameter("@hv", maHocVien), new SqlParameter("@paid", DaThanhToan)).ToList();
            NapKhoaHoc(list);
            return list;
        }

        private void NapKhoaHoc(List<DonThanhToan> dons)
        {
            if (dons.Count == 0) return;
            var ma = dons.Select(d => d.MaDonHang).ToList();
            var dong = db.ChiTietDonHang
                .Where(c => ma.Contains(c.maDonHang))
                .Select(c => new { c.maDonHang, c.maKhoaHoc, c.donGia, Ten = c.KhoaHoc.tenKhoaHoc, Anh = c.KhoaHoc.anhBia })
                .ToList();
            foreach (var d in dons)
            {
                d.KhoaHoc = dong.Where(x => x.maDonHang == d.MaDonHang)
                    .Select(x => new DongDonThanhToan { MaKhoaHoc = x.maKhoaHoc, TenKhoaHoc = x.Ten, AnhBia = x.Anh, DonGia = x.donGia })
                    .ToList();
            }
        }

        /// <summary>Đơn "Chờ thanh toán" quá hạn chuyển sang "Hết hạn" (gọi khi mở cổng, giỏ hàng, lịch sử; không cần job chạy nền).</summary>
        public void XuLyDonHetHan()
        {
            var het = db.Database.SqlQuery<string>(@"
                UPDATE dbo.DonHang SET trangThai = @het
                OUTPUT inserted.maDonHang
                WHERE trangThai = @cho AND ngayHetHan IS NOT NULL AND ngayHetHan < GETDATE()",
                new SqlParameter("@het", HetHan), new SqlParameter("@cho", ChoThanhToan)).ToList();
            foreach (var ma in het) GhiNhatKy(ma, "HET_HAN", null, null, null, "Quá " + PhutHetHan + " phút chưa thanh toán");
        }

        /// <summary>Mỗi học viên chỉ một đơn chờ: tạo đơn mới thì hủy các đơn chờ cũ (tránh dùng một mã giảm giá cho nhiều đơn).</summary>
        public void HuyDonChoCu(int maHocVien)
        {
            var huy = db.Database.SqlQuery<string>(@"
                UPDATE dbo.DonHang SET trangThai = @huy
                OUTPUT inserted.maDonHang
                WHERE maHocVien = @hv AND trangThai = @cho",
                new SqlParameter("@huy", DaHuy), new SqlParameter("@hv", maHocVien), new SqlParameter("@cho", ChoThanhToan)).ToList();
            foreach (var ma in huy) GhiNhatKy(ma, "HUY", null, null, null, "Học viên tạo đơn mới, đơn chờ cũ bị hủy");
        }

        /// <summary>Gắn thông tin thanh toán cho đơn vừa tạo (gọi trong transaction tạo đơn của giỏ hàng).</summary>
        public void DatThongTinDonCho(string maDonHang, string phuongThuc)
        {
            db.Database.ExecuteSqlCommand(@"
                UPDATE dbo.DonHang SET phuongThuc = @pt, ngayHetHan = DATEADD(MINUTE, @phut, GETDATE()) WHERE maDonHang = @dh",
                new SqlParameter("@pt", phuongThuc), new SqlParameter("@phut", PhutHetHan), new SqlParameter("@dh", maDonHang));
        }

        // ------------------------------------------------------------------ cổng thử nghiệm

        /// <summary>
        /// Tham số kết quả cổng thử nghiệm gửi về (đã ký): mã đơn, số tiền, mã giao dịch phía cổng, mã kết quả, phương thức, thời điểm.
        /// </summary>
        public Dictionary<string, string> TaoKetQuaCong(DonThanhToan don, string maKetQua)
        {
            var p = new Dictionary<string, string>
            {
                { "orderId", don.MaDonHang },
                { "amount", ((long)decimal.Round(don.TongTien, 0)).ToString() },
                { "transactionNo", "SBX" + DateTime.Now.ToString("yyMMddHHmmss") + SoNgauNhien.Trong(1000, 9999) },
                { "responseCode", maKetQua },
                { "method", don.PhuongThuc ?? PhuongThucHopLe[0] },
                { "payDate", DateTime.Now.ToString("yyyyMMddHHmmss") }
            };
            p["signature"] = Ky(p);
            return p;
        }

        /// <summary>
        /// Xử lý kết quả cổng gửi về (IPN). Chạy được nhiều lần cho cùng một giao dịch (cổng thật có thể gọi lặp), chỉ ghi nhận một lần.
        /// Trả về mã phản hồi cho cổng: 00 đã xử lý, 01 không thấy đơn, 02 đơn đã xử lý trước đó, 04 sai số tiền, 97 sai chữ ký.
        /// </summary>
        public KetQuaIpn XuLyKetQua(IDictionary<string, string> p, string loai = "IPN")
        {
            string maDon = Lay(p, "orderId");
            string duLieu = ChuoiKy(p);
            if (!ChuKyHopLe(p))
            {
                GhiNhatKy(maDon, loai, duLieu, false, "97", "Sai chữ ký");
                return KetQuaIpn.Tra("97", "Sai chữ ký");
            }
            long soTien;
            long.TryParse(Lay(p, "amount"), out soTien);
            return GhiNhanKetQua(maDon, soTien, Lay(p, "transactionNo"), Lay(p, "responseCode") == GdThanhCong,
                Lay(p, "responseCode"), Lay(p, "method"), CongThuNghiem, duLieu, loai);
        }

        /// <summary>
        /// Lõi ghi nhận kết quả giao dịch, dùng chung cho cổng thử nghiệm và VNPay (gọi SAU KHI đã xác thực chữ ký của cổng):
        /// kiểm tra đơn tồn tại, số tiền khớp, trạng thái đơn; khóa theo đơn để kết quả gửi lặp chỉ ghi nhận một lần.
        /// </summary>
        public KetQuaIpn GhiNhanKetQua(string maDon, long soTien, string maGd, bool thanhCong, string maKq,
            string phuongThucCong, string congThanhToan, string duLieu, string loai)
        {
            var don = LayDon(maDon);
            if (don == null)
            {
                GhiNhatKy(null, loai, duLieu, true, "01", "Không tìm thấy đơn " + maDon);
                return KetQuaIpn.Tra("01", "Không tìm thấy đơn hàng");
            }
            if (soTien != (long)decimal.Round(don.TongTien, 0))
            {
                GhiNhatKy(maDon, loai, duLieu, true, "04", "Số tiền không khớp đơn (" + don.TongTien.ToString("0") + ")");
                return KetQuaIpn.Tra("04", "Số tiền không hợp lệ");
            }

            string phuongThuc = phuongThucCong ?? don.PhuongThuc;

            using (var tx = db.Database.BeginTransaction())
            {
                // Khóa theo đơn: IPN gọi lặp / đồng thời không ghi nhận hai lần
                db.Database.ExecuteSqlCommand("EXEC sp_getapplock @Resource = @r, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000",
                    new SqlParameter("@r", "Order_" + maDon));
                string trangThai = db.Database.SqlQuery<string>("SELECT trangThai FROM dbo.DonHang WHERE maDonHang = @dh",
                    new SqlParameter("@dh", maDon)).First();

                if (trangThai == DaThanhToan)
                {
                    tx.Commit();
                    GhiNhatKy(maDon, loai, duLieu, true, "02", "Đơn đã thanh toán trước đó (gọi lặp)");
                    return KetQuaIpn.Tra("02", "Đơn hàng đã được xác nhận", true);
                }
                if (trangThai != ChoThanhToan)
                {
                    tx.Commit();
                    GhiNhatKy(maDon, loai, duLieu, true, "02", "Đơn ở trạng thái \"" + trangThai + "\", không ghi nhận");
                    return KetQuaIpn.Tra("02", "Đơn hàng không còn chờ thanh toán (" + trangThai + ")");
                }

                // Ghi giao dịch: dòng "Đã thanh toán" kích hoạt trigger trg_ThanhToan_TuDongDangKy ghi danh các khóa trong đơn
                db.Database.ExecuteSqlCommand(@"
                    INSERT INTO dbo.ThanhToan (maThanhToan, soTien, phuongThuc, trangThaiThanhToan, maDonHang, congThanhToan, maGiaoDichCong, ngayThanhToan)
                    VALUES (@ma, @tien, @pt, @tt, @dh, @cong, @gd, GETDATE())",
                    new SqlParameter("@ma", MaThanhToanMoi()),
                    new SqlParameter("@tien", don.TongTien),
                    new SqlParameter("@pt", phuongThuc ?? (object)DBNull.Value),
                    new SqlParameter("@tt", thanhCong ? DaThanhToan : ThatBai),
                    new SqlParameter("@dh", maDon),
                    new SqlParameter("@cong", congThanhToan),
                    new SqlParameter("@gd", (object)maGd ?? DBNull.Value));
                db.Database.ExecuteSqlCommand(@"
                    UPDATE dbo.DonHang SET trangThai = @tt, ngayThanhToan = CASE WHEN @ok = 1 THEN GETDATE() ELSE ngayThanhToan END,
                           phuongThuc = ISNULL(@pt, phuongThuc)
                    WHERE maDonHang = @dh",
                    new SqlParameter("@tt", thanhCong ? DaThanhToan : ThatBai),
                    new SqlParameter("@ok", thanhCong),
                    new SqlParameter("@pt", (object)phuongThuc ?? DBNull.Value),
                    new SqlParameter("@dh", maDon));
                // Mã nhập tay ở giỏ hàng vào ví khi đơn đã thanh toán (đặc tả Marketing mục 10)
                if (thanhCong) PromotionService.ThemMaVaoViKhiDaThanhToan(db, maDon);
                tx.Commit();
            }

            string ghiChu = thanhCong ? "Thanh toán thành công qua " + congThanhToan + ", đã ghi danh khóa học"
                : maKq == GdKhachHuy ? "Khách hủy giao dịch" : "Giao dịch thất bại (mã " + maKq + ")";
            GhiNhatKy(maDon, loai, duLieu, true, "00", ghiChu);
            return KetQuaIpn.Tra("00", "Đã ghi nhận", thanhCong);
        }

        /// <summary>Đơn 0đ (mã giảm giá trừ hết): không qua cổng, ghi nhận và ghi danh ngay.</summary>
        public void XacNhanDonMienPhi(string maDonHang)
        {
            using (var tx = db.Database.BeginTransaction())
            {
                db.Database.ExecuteSqlCommand(@"
                    INSERT INTO dbo.ThanhToan (maThanhToan, soTien, phuongThuc, trangThaiThanhToan, maDonHang, congThanhToan, ngayThanhToan)
                    VALUES (@ma, 0, N'Miễn phí', @tt, @dh, @cong, GETDATE())",
                    new SqlParameter("@ma", MaThanhToanMoi()), new SqlParameter("@tt", DaThanhToan),
                    new SqlParameter("@dh", maDonHang), new SqlParameter("@cong", KhongQuaCong));
                db.Database.ExecuteSqlCommand(
                    "UPDATE dbo.DonHang SET trangThai = @tt, ngayThanhToan = GETDATE(), phuongThuc = N'Miễn phí' WHERE maDonHang = @dh",
                    new SqlParameter("@tt", DaThanhToan), new SqlParameter("@dh", maDonHang));
                PromotionService.ThemMaVaoViKhiDaThanhToan(db, maDonHang);
                tx.Commit();
            }
            GhiNhatKy(maDonHang, "TAO_DON", null, null, null, "Đơn 0đ, ghi danh không qua cổng");
        }

        public void GhiNhatKy(string maDonHang, string loai, string duLieu, bool? chuKyHopLe, string maPhanHoi, string ghiChu)
        {
            db.Database.ExecuteSqlCommand(@"
                INSERT INTO dbo.NhatKyThanhToan (maDonHang, loai, duLieu, chuKyHopLe, maPhanHoi, ghiChu)
                VALUES (@dh, @loai, @dl, @ck, @ph, @gc)",
                new SqlParameter("@dh", (object)maDonHang ?? DBNull.Value),
                new SqlParameter("@loai", loai),
                new SqlParameter("@dl", (object)duLieu ?? DBNull.Value),
                new SqlParameter("@ck", (object)chuKyHopLe ?? DBNull.Value),
                new SqlParameter("@ph", (object)maPhanHoi ?? DBNull.Value),
                new SqlParameter("@gc", (object)ghiChu ?? DBNull.Value));
        }

        private static string Lay(IDictionary<string, string> p, string key)
        {
            string v;
            return p.TryGetValue(key, out v) ? v : null;
        }

        private static string MaThanhToanMoi()
        {
            return "TT" + DateTime.Now.ToString("yyMMddHHmmss") + SoNgauNhien.Trong(1000, 9999);
        }
    }

    internal static class SoNgauNhien
    {
        /// <summary>Số ngẫu nhiên an toàn trong [min, max) (System.Security.Cryptography, .NET Framework chưa có GetInt32).</summary>
        public static int Trong(int min, int max)
        {
            var b = new byte[4];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(b);
            return min + (int)(BitConverter.ToUInt32(b, 0) % (uint)(max - min));
        }
    }

    public class DonThanhToan
    {
        public string MaDonHang { get; set; }
        public int MaHocVien { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; }
        public DateTime? NgayDat { get; set; }
        public DateTime? NgayHetHan { get; set; }
        public DateTime? NgayThanhToan { get; set; }
        public string PhuongThuc { get; set; }
        public string MaGiamGia { get; set; }
        public string MaGiaoDich { get; set; }
        public List<DongDonThanhToan> KhoaHoc { get; set; } = new List<DongDonThanhToan>();

        public bool DangCho => TrangThai == PaymentService.ChoThanhToan && (!NgayHetHan.HasValue || NgayHetHan.Value > DateTime.Now);
        public decimal TongTruocGiam => KhoaHoc.Sum(k => k.DonGia);
    }

    public class DongDonThanhToan
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string AnhBia { get; set; }
        public decimal DonGia { get; set; }
    }

    public class KetQuaIpn
    {
        public string MaPhanHoi { get; set; }
        public string ThongDiep { get; set; }
        public bool DaThanhToan { get; set; }

        public static KetQuaIpn Tra(string ma, string thongDiep, bool daThanhToan = false)
        {
            return new KetQuaIpn { MaPhanHoi = ma, ThongDiep = thongDiep, DaThanhToan = daThanhToan };
        }
    }
}
