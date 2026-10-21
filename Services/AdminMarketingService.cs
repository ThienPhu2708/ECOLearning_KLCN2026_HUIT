using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Admin giám sát phân hệ Marketing (không tạo khuyến mãi thay Marketing):
    ///  - Tổng quan: chương trình đang chạy / sắp hết hạn, tiền đã giảm so với doanh thu mang lại, chiến dịch đang mở.
    ///  - Cảnh báo chương trình bất thường: giảm từ 50% trở lên, hoặc tổng tiền đã giảm vượt doanh thu sau giảm.
    ///  - Tạm dừng chương trình kèm lý do (cờ adminTamDung, migration 016): Marketing thấy lý do và không tự kích hoạt lại được;
    ///    chỉ Admin "Cho chạy lại". Mọi thao tác ghi NhatKyQuanTri.
    ///  - Nhân viên Marketing: số chương trình / chiến dịch đã tạo (tạo, khóa tài khoản ở trang Quản lý người dùng).
    /// </summary>
    public class AdminMarketingService
    {
        public const double NguongGiamSau = 50;   // % giảm từ mức này trở lên được đánh dấu "giảm sâu"

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public AdminMarketingService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public bool IsInstalled()
        {
            return new PromotionService(db).IsInstalled() && CoCotAdmin(db);
        }

        public static bool CoCotAdmin(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            return db.Database.SqlQuery<int>("SELECT CASE WHEN COL_LENGTH('dbo.KhuyenMai', 'adminTamDung') IS NULL THEN 0 ELSE 1 END").Single() == 1;
        }

        public TongQuanMarketingAdmin TongQuan(string tuKhoa, string loc)
        {
            var now = DateTime.Now;
            var tatCa = new PromotionService(db).LayDanhSach();
            var thongTin = ThongTinAdmin();
            var ds = tatCa.Select(p =>
            {
                ThongTinKhuyenMaiAdmin t;
                thongTin.TryGetValue(p.MaKhuyenMai, out t);
                return TaoDong(p, t ?? new ThongTinKhuyenMaiAdmin(), now);
            }).ToList();

            var kq = new TongQuanMarketingAdmin
            {
                DangChay = ds.Count(d => d.TrangThai == "Đang chạy"),
                SapHetHan = ds.Count(d => d.TrangThai == "Đang chạy" && d.ChuongTrinh.NgayKetThuc <= now.AddDays(7)),
                AdminTamDung = ds.Count(d => d.AdminTamDung),
                CanChuY = ds.Count(d => d.CanhBao.Count > 0 && d.TrangThai != "Đã kết thúc"),
                TongTienGiam = ds.Sum(d => d.ChuongTrinh.TongTienGiam),
                DoanhThuSauGiam = ds.Sum(d => d.ChuongTrinh.DoanhThuSauGiam),
                TongLuotDung = ds.Sum(d => d.ChuongTrinh.SoLuotDaDung),
                ChienDichDangMo = db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM dbo.ChienDich WHERE trangThai = N'HoatDong' AND ngayKetThuc >= CAST(GETDATE() AS DATE)").Single(),
                NhanVien = NhanVienMarketing()
            };

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                string k = tuKhoa.Trim().ToLowerInvariant();
                ds = ds.Where(d => (d.ChuongTrinh.TenKhuyenMai ?? "").ToLowerInvariant().Contains(k)
                                || (d.ChuongTrinh.MaCode ?? "").ToLowerInvariant().Contains(k)
                                || (d.TenNguoiTao ?? "").ToLowerInvariant().Contains(k)).ToList();
            }
            switch (loc)
            {
                case "running": ds = ds.Where(d => d.TrangThai == "Đang chạy").ToList(); break;
                case "warning": ds = ds.Where(d => d.CanhBao.Count > 0 && d.TrangThai != "Đã kết thúc").ToList(); break;
                case "admin-paused": ds = ds.Where(d => d.AdminTamDung).ToList(); break;
                case "expiring": ds = ds.Where(d => d.TrangThai == "Đang chạy" && d.ChuongTrinh.NgayKetThuc <= now.AddDays(7)).ToList(); break;
                case "ended": ds = ds.Where(d => d.TrangThai == "Đã kết thúc").ToList(); break;
            }

            // Cần xử lý trước: Admin đang tạm dừng, có cảnh báo, đang chạy; chương trình đã kết thúc xuống cuối
            kq.ChuongTrinh = ds
                .OrderBy(d => d.TrangThai == "Đã kết thúc" ? 1 : 0)
                .ThenByDescending(d => d.AdminTamDung)
                .ThenByDescending(d => d.CanhBao.Count)
                .ThenByDescending(d => d.ChuongTrinh.NgayTao)
                .ToList();
            return kq;
        }

        public KhuyenMaiAdminItem LayMot(string maKhuyenMai)
        {
            var p = new PromotionService(db).LayTheoMa(maKhuyenMai);
            if (p == null) return null;
            ThongTinKhuyenMaiAdmin t;
            ThongTinAdmin(maKhuyenMai).TryGetValue(maKhuyenMai, out t);
            return TaoDong(p, t ?? new ThongTinKhuyenMaiAdmin(), DateTime.Now);
        }

        public KetQuaQuanTri TamDung(int maAdmin, string maKhuyenMai, string lyDo)
        {
            lyDo = (lyDo ?? "").Trim();
            if (lyDo.Length < 5) return KetQuaQuanTri.Loi("Vui lòng nhập lý do tạm dừng (tối thiểu 5 ký tự) để nhân viên Marketing biết cần điều chỉnh gì.");
            if (lyDo.Length > 500) return KetQuaQuanTri.Loi("Lý do tối đa 500 ký tự.");

            var km = LayMot(maKhuyenMai);
            if (km == null) return KetQuaQuanTri.Loi("Không tìm thấy chương trình khuyến mãi.");
            if (km.TrangThai == "Đã kết thúc") return KetQuaQuanTri.Loi("Chương trình đã kết thúc, không cần tạm dừng.");
            if (km.AdminTamDung) return KetQuaQuanTri.Loi("Chương trình đang bị Admin tạm dừng.");

            using (var tran = db.Database.BeginTransaction())
            {
                db.Database.ExecuteSqlCommand(@"
                    UPDATE dbo.KhuyenMai
                    SET trangThai = @tam, adminTamDung = 1, lyDoAdminTamDung = @ld, ngayAdminTamDung = GETDATE()
                    WHERE maKhuyenMai = @ma;
                    UPDATE dbo.MaGiamGia SET trangThai = 0 WHERE maKhuyenMai = @ma;",
                    new SqlParameter("@tam", PromotionService.TamNgung),
                    new SqlParameter("@ld", lyDo),
                    new SqlParameter("@ma", maKhuyenMai));
                new AccountAdminService(db).GhiNhatKy(maAdmin, AccountAdminService.HdTamDungKhuyenMai, AccountAdminService.DoiTuongKhuyenMai, maKhuyenMai, lyDo);
                tran.Commit();
            }

            string thongBao = "Đã tạm dừng chương trình \"" + km.ChuongTrinh.TenKhuyenMai + "\". Mã " + km.ChuongTrinh.MaCode + " không áp dụng được nữa.";
            if (!string.IsNullOrEmpty(km.EmailNguoiTao))
            {
                var mail = EmailService.SendAdminNoticeEmail(km.EmailNguoiTao, km.TenNguoiTao, "[E-Learning] Chương trình khuyến mãi bị Quản trị viên tạm dừng",
                    "<p>Ban quản trị đã tạm dừng chương trình <strong>" + WebUtility.HtmlEncode(km.ChuongTrinh.TenKhuyenMai) + "</strong> (mã "
                    + WebUtility.HtmlEncode(km.ChuongTrinh.MaCode) + "). Học viên không áp dụng được mã này cho đến khi Quản trị viên cho chạy lại.</p>"
                    + "<p>Vui lòng điều chỉnh chương trình theo lý do bên dưới rồi liên hệ Quản trị viên.</p>",
                    "Lý do tạm dừng", WebUtility.HtmlEncode(lyDo), "warning");
                if (!mail.Success) thongBao += " (Chưa gửi được email cho nhân viên tạo chương trình.)";
            }
            return KetQuaQuanTri.Ok(thongBao);
        }

        public KetQuaQuanTri ChoChayLai(int maAdmin, string maKhuyenMai, string ghiChu)
        {
            ghiChu = (ghiChu ?? "").Trim();
            if (ghiChu.Length > 500) return KetQuaQuanTri.Loi("Ghi chú tối đa 500 ký tự.");
            var km = LayMot(maKhuyenMai);
            if (km == null) return KetQuaQuanTri.Loi("Không tìm thấy chương trình khuyến mãi.");
            if (!km.AdminTamDung) return KetQuaQuanTri.Loi("Chương trình không bị Admin tạm dừng.");

            // Đã kết thúc (hết hạn trong lúc tạm dừng) thì chỉ gỡ cờ, không bật lại mã
            bool batLai = km.ChuongTrinh.TrangThaiLuu != PromotionService.KetThuc && km.ChuongTrinh.NgayKetThuc >= DateTime.Now;
            using (var tran = db.Database.BeginTransaction())
            {
                db.Database.ExecuteSqlCommand(@"
                    UPDATE dbo.KhuyenMai
                    SET adminTamDung = 0, lyDoAdminTamDung = NULL, ngayAdminTamDung = NULL,
                        trangThai = CASE WHEN @bat = 1 THEN @hd ELSE trangThai END
                    WHERE maKhuyenMai = @ma;
                    IF @bat = 1 UPDATE dbo.MaGiamGia SET trangThai = 1 WHERE maKhuyenMai = @ma;",
                    new SqlParameter("@bat", batLai),
                    new SqlParameter("@hd", PromotionService.HoatDong),
                    new SqlParameter("@ma", maKhuyenMai));
                new AccountAdminService(db).GhiNhatKy(maAdmin, AccountAdminService.HdChoChayKhuyenMai, AccountAdminService.DoiTuongKhuyenMai, maKhuyenMai,
                    ghiChu.Length > 0 ? ghiChu : "Admin cho chạy lại");
                tran.Commit();
            }
            return KetQuaQuanTri.Ok(batLai
                ? "Đã cho chạy lại chương trình \"" + km.ChuongTrinh.TenKhuyenMai + "\"."
                : "Đã gỡ tạm dừng. Chương trình đã hết hạn nên mã không được bật lại.");
        }

        /// <summary>Lý do Admin tạm dừng (null nếu không bị tạm dừng / chưa chạy migration 016). Dùng ở phía Marketing.</summary>
        public static string LyDoAdminTamDung(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, string maKhuyenMai)
        {
            if (!CoCotAdmin(db)) return null;
            return db.Database.SqlQuery<string>(
                "SELECT lyDoAdminTamDung FROM dbo.KhuyenMai WHERE maKhuyenMai = @ma AND adminTamDung = 1",
                new SqlParameter("@ma", maKhuyenMai ?? "")).FirstOrDefault();
        }

        /// <summary>Mã các chương trình đang bị Admin tạm dừng (danh sách phía Marketing hiện nhãn, ẩn nút kích hoạt).</summary>
        public static HashSet<string> DanhSachAdminTamDung(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            if (!CoCotAdmin(db)) return new HashSet<string>();
            return new HashSet<string>(db.Database.SqlQuery<string>("SELECT maKhuyenMai FROM dbo.KhuyenMai WHERE adminTamDung = 1").ToList());
        }

        // ------------------------------------------------------------------ hỗ trợ

        private static KhuyenMaiAdminItem TaoDong(ChuongTrinhKhuyenMai p, ThongTinKhuyenMaiAdmin t, DateTime now)
        {
            var d = new KhuyenMaiAdminItem
            {
                ChuongTrinh = p,
                TrangThai = p.TrangThaiHienThi(now),
                AdminTamDung = t.AdminTamDung,
                LyDoAdminTamDung = t.LyDoAdminTamDung,
                NgayAdminTamDung = t.NgayAdminTamDung,
                TenNguoiTao = t.TenNguoiTao,
                EmailNguoiTao = t.EmailNguoiTao
            };
            if (p.LoaiUuDai == "PhanTram" && p.MucUuDai >= NguongGiamSau)
                d.CanhBao.Add("Giảm sâu " + p.MucUuDai.ToString("0.#") + "%" + (p.GiamToiDa.HasValue ? "" : ", không giới hạn số tiền giảm"));
            if (p.TongTienGiam > 0 && p.TongTienGiam > p.DoanhThuSauGiam)
                d.CanhBao.Add("Tiền đã giảm vượt doanh thu sau giảm");
            if (p.LoaiUuDai == "PhanTram" && p.MucUuDai >= 100)
                d.CanhBao.Add("Miễn phí toàn bộ đơn hàng");
            return d;
        }

        private Dictionary<string, ThongTinKhuyenMaiAdmin> ThongTinAdmin(string maKhuyenMai = null)
        {
            return db.Database.SqlQuery<ThongTinKhuyenMaiAdmin>(@"
                SELECT km.maKhuyenMai AS MaKhuyenMai, km.adminTamDung AS AdminTamDung, km.lyDoAdminTamDung AS LyDoAdminTamDung,
                       km.ngayAdminTamDung AS NgayAdminTamDung, nv.hoTen AS TenNguoiTao, nv.email AS EmailNguoiTao
                FROM dbo.KhuyenMai km
                LEFT JOIN dbo.NguoiDung nv ON nv.maNguoiDung = km.maNhanVienMarketing
                WHERE @ma IS NULL OR km.maKhuyenMai = @ma",
                new SqlParameter("@ma", (object)maKhuyenMai ?? DBNull.Value)).ToList()
                .ToDictionary(x => x.MaKhuyenMai, x => x);
        }

        private List<NhanVienMarketingItem> NhanVienMarketing()
        {
            return db.Database.SqlQuery<NhanVienMarketingItem>(@"
                SELECT nd.maNguoiDung AS MaNguoiDung, nd.hoTen AS HoTen, nd.email AS Email, CAST(ISNULL(nd.trangThai, 1) AS BIT) AS HoatDong,
                       (SELECT COUNT(*) FROM dbo.KhuyenMai km WHERE km.maNhanVienMarketing = nd.maNguoiDung) AS SoChuongTrinh,
                       (SELECT COUNT(*) FROM dbo.ChienDich cd WHERE cd.maNhanVienMarketing = nd.maNguoiDung) AS SoChienDich,
                       (SELECT COUNT(*) FROM dbo.KhuyenMai km WHERE km.maNhanVienMarketing = nd.maNguoiDung AND km.adminTamDung = 1) AS SoBiTamDung,
                       (SELECT MAX(km.ngayTao) FROM dbo.KhuyenMai km WHERE km.maNhanVienMarketing = nd.maNguoiDung) AS LanTaoGanNhat
                FROM dbo.NguoiDung nd
                WHERE nd.vaiTro = N'Marketing'
                ORDER BY nd.hoTen").ToList();
        }

        private class ThongTinKhuyenMaiAdmin
        {
            public string MaKhuyenMai { get; set; }
            public bool AdminTamDung { get; set; }
            public string LyDoAdminTamDung { get; set; }
            public DateTime? NgayAdminTamDung { get; set; }
            public string TenNguoiTao { get; set; }
            public string EmailNguoiTao { get; set; }
        }
    }

    public class TongQuanMarketingAdmin
    {
        public int DangChay { get; set; }
        public int SapHetHan { get; set; }
        public int AdminTamDung { get; set; }
        public int CanChuY { get; set; }
        public decimal TongTienGiam { get; set; }
        public decimal DoanhThuSauGiam { get; set; }
        public int TongLuotDung { get; set; }
        public int ChienDichDangMo { get; set; }
        public List<NhanVienMarketingItem> NhanVien { get; set; } = new List<NhanVienMarketingItem>();
        public List<KhuyenMaiAdminItem> ChuongTrinh { get; set; } = new List<KhuyenMaiAdminItem>();
    }

    public class KhuyenMaiAdminItem
    {
        public ChuongTrinhKhuyenMai ChuongTrinh { get; set; }
        public string TrangThai { get; set; }
        public bool AdminTamDung { get; set; }
        public string LyDoAdminTamDung { get; set; }
        public DateTime? NgayAdminTamDung { get; set; }
        public string TenNguoiTao { get; set; }
        public string EmailNguoiTao { get; set; }
        public List<string> CanhBao { get; set; } = new List<string>();
    }

    public class NhanVienMarketingItem
    {
        public int MaNguoiDung { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public bool HoatDong { get; set; }
        public int SoChuongTrinh { get; set; }
        public int SoChienDich { get; set; }
        public int SoBiTamDung { get; set; }
        public DateTime? LanTaoGanNhat { get; set; }
    }
}
