using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Chiến dịch Marketing (bảng ChienDich, migration 008 + 018; đặc tả Marketing mục 4). Một chiến dịch gắn nhiều chương trình
    /// khuyến mãi (KhuyenMai.maChienDich); số liệu chiến dịch = tổng số liệu sử dụng ưu đãi (đơn đã thanh toán) của các chương trình đó.
    /// Ràng buộc: kết thúc ≥ bắt đầu; sửa thời gian mà có khuyến mãi nằm ngoài khoảng mới thì báo lỗi; chiến dịch Tạm dừng / Đã hủy
    /// làm mọi mã thuộc chiến dịch không dùng được (PromotionService.KiemTraCoupon); không gắn thêm khuyến mãi vào chiến dịch
    /// Hoàn thành / Đã hủy; xóa chỉ khi chưa gắn khuyến mãi nào (có rồi thì chuyển Đã hủy).
    /// </summary>
    public class CampaignService
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public CampaignService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        private const string SelectChienDich = @"
            SELECT cd.maChienDich AS MaChienDich, cd.tenChienDich AS TenChienDich, cd.mucTieu AS MucTieu, cd.noiDung AS NoiDung,
                   cd.doiTuong AS DoiTuong, cd.kenhTrienKhai AS KenhTrienKhai, cd.nganSach AS NganSach, cd.chiPhiThucTe AS ChiPhiThucTe,
                   cd.chiTieuDoanhThu AS ChiTieuDoanhThu, cd.chiTieuLuotDung AS ChiTieuLuotDung, cd.ghiChu AS GhiChu,
                   cd.ngayBatDau AS NgayBatDau, cd.ngayKetThuc AS NgayKetThuc, cd.trangThai AS TrangThaiLuu,
                   cd.ngayTao AS NgayTao, nd.hoTen AS NguoiTao,
                   (SELECT COUNT(*) FROM dbo.KhuyenMai km WHERE km.maChienDich = cd.maChienDich) AS SoChuongTrinh,
                   ISNULL(sd.soLuot, 0) AS SoLuotDung, ISNULL(sd.soDon, 0) AS SoDonHang,
                   CAST(ISNULL(sd.tienGiam, 0) AS DECIMAL(18, 2)) AS TienGiam,
                   CAST(ISNULL(sd.doanhThu, 0) AS DECIMAL(18, 2)) AS DoanhThuSauGiam
            FROM dbo.ChienDich cd
            LEFT JOIN dbo.NguoiDung nd ON nd.maNguoiDung = cd.maNhanVienMarketing
            OUTER APPLY (
                SELECT COUNT(*) AS soLuot, COUNT(DISTINCT s.maDonHang) AS soDon, SUM(s.tienGiam) AS tienGiam, SUM(d.tongTien) AS doanhThu
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.KhuyenMai km ON km.maKhuyenMai = s.maKhuyenMai
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai IN (" + PromotionService.DonDaTraSql + @")
                WHERE km.maChienDich = cd.maChienDich
            ) sd";

        public List<ChienDichItem> LayDanhSach(string tuKhoa = null, string trangThai = null, DateTime? tuNgay = null, DateTime? denNgay = null)
        {
            var thamSo = new List<SqlParameter>();
            var dieuKien = new List<string>();
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                dieuKien.Add("(cd.tenChienDich LIKE @tuKhoa OR cd.mucTieu LIKE @tuKhoa)");
                thamSo.Add(new SqlParameter("@tuKhoa", "%" + tuKhoa.Trim() + "%"));
            }
            if (!string.IsNullOrEmpty(trangThai) && TrangThaiChienDich.TatCa.Contains(trangThai))
            {
                dieuKien.Add("cd.trangThai = @trangThai");
                thamSo.Add(new SqlParameter("@trangThai", trangThai));
            }
            if (tuNgay.HasValue)
            {
                dieuKien.Add("cd.ngayKetThuc >= @tuNgay");
                thamSo.Add(new SqlParameter("@tuNgay", tuNgay.Value.Date));
            }
            if (denNgay.HasValue)
            {
                dieuKien.Add("cd.ngayBatDau < @denNgaySau");
                thamSo.Add(new SqlParameter("@denNgaySau", denNgay.Value.Date.AddDays(1)));
            }
            return db.Database.SqlQuery<ChienDichItem>(
                SelectChienDich + (dieuKien.Count > 0 ? " WHERE " + string.Join(" AND ", dieuKien) : "") + " ORDER BY cd.ngayTao DESC",
                thamSo.ToArray()).ToList();
        }

        public ChienDichItem LayTheoMa(string maChienDich)
        {
            if (string.IsNullOrEmpty(maChienDich)) return null;
            return db.Database.SqlQuery<ChienDichItem>(SelectChienDich + " WHERE cd.maChienDich = @ma",
                new SqlParameter("@ma", maChienDich)).FirstOrDefault();
        }

        /// <summary>Chương trình có thể gắn vào chiến dịch: chưa kết thúc và chưa thuộc chiến dịch khác.</summary>
        public List<ChuongTrinhKhuyenMai> LayChuongTrinhCoTheGan(string maChienDich)
        {
            var now = DateTime.Now;
            return new PromotionService(db).LayDanhSach()
                .Where(p => (p.MaChienDich == null && p.TrangThaiHienThi(now) != "Đã kết thúc")
                         || (!string.IsNullOrEmpty(maChienDich) && p.MaChienDich == maChienDich))
                .ToList();
        }

        public CampaignFormViewModel TaoFormTu(ChienDichItem c)
        {
            var kenh = c.DanhSachKenh;
            return new CampaignFormViewModel
            {
                MaChienDich = c.MaChienDich,
                TenChienDich = c.TenChienDich,
                MucTieu = c.MucTieu,
                NoiDung = c.NoiDung,
                DoiTuong = c.DoiTuong,
                Kenh = kenh.Where(k => TrangThaiChienDich.KenhGoiY.Contains(k)).ToList(),
                KenhKhac = string.Join(", ", kenh.Where(k => !TrangThaiChienDich.KenhGoiY.Contains(k))),
                NganSach = c.NganSach,
                ChiPhiThucTe = c.ChiPhiThucTe,
                ChiTieuDoanhThu = c.ChiTieuDoanhThu,
                ChiTieuLuotDung = c.ChiTieuLuotDung,
                GhiChu = c.GhiChu,
                TrangThai = c.TrangThaiLuu,
                TrangThaiHienTai = c.TrangThaiLuu,
                NgayBatDau = c.NgayBatDau.Date,
                NgayKetThuc = c.NgayKetThuc.Date,
                ChuongTrinh = new PromotionService(db).LayDanhSach(maChienDich: c.MaChienDich).Select(p => p.MaKhuyenMai).ToList()
            };
        }

        public Dictionary<string, string> KiemTraForm(CampaignFormViewModel f, ChienDichItem hienTai)
        {
            var loi = new Dictionary<string, string>();
            f.TenChienDich = (f.TenChienDich ?? "").Trim();
            f.MucTieu = string.IsNullOrWhiteSpace(f.MucTieu) ? null : f.MucTieu.Trim();
            f.NoiDung = string.IsNullOrWhiteSpace(f.NoiDung) ? null : f.NoiDung.Trim();
            f.DoiTuong = string.IsNullOrWhiteSpace(f.DoiTuong) ? null : f.DoiTuong.Trim();
            f.GhiChu = string.IsNullOrWhiteSpace(f.GhiChu) ? null : f.GhiChu.Trim();
            f.Kenh = (f.Kenh ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            f.ChuongTrinh = (f.ChuongTrinh ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            f.NganSach = f.NganSach ?? 0;
            f.ChiPhiThucTe = f.ChiPhiThucTe ?? 0;
            f.TrangThaiHienTai = hienTai?.TrangThaiLuu;

            if (f.TenChienDich.Length == 0) loi["TenChienDich"] = "Vui lòng nhập tên chiến dịch.";
            else if (f.TenChienDich.Length > 200) loi["TenChienDich"] = "Tên chiến dịch tối đa 200 ký tự.";
            if (f.MucTieu != null && f.MucTieu.Length > 1000) loi["MucTieu"] = "Mục tiêu tối đa 1000 ký tự.";
            if (f.NoiDung != null && f.NoiDung.Length > 2000) loi["NoiDung"] = "Nội dung tối đa 2000 ký tự.";
            if (f.DoiTuong != null && f.DoiTuong.Length > 300) loi["DoiTuong"] = "Đối tượng tối đa 300 ký tự.";
            if (f.GhiChu != null && f.GhiChu.Length > 1000) loi["GhiChu"] = "Ghi chú tối đa 1000 ký tự.";

            string kenh = f.KenhGhep;
            if (kenh.Length == 0) loi["Kenh"] = "Chọn ít nhất 1 kênh triển khai.";
            else if (kenh.Length > 300) loi["Kenh"] = "Danh sách kênh tối đa 300 ký tự.";

            if (f.NganSach < 0) loi["NganSach"] = "Ngân sách không được âm.";
            if (f.ChiPhiThucTe < 0) loi["ChiPhiThucTe"] = "Chi phí thực tế không được âm.";
            if (f.ChiTieuDoanhThu.HasValue && f.ChiTieuDoanhThu < 0) loi["ChiTieuDoanhThu"] = "Chỉ tiêu doanh thu không được âm.";
            if (f.ChiTieuLuotDung.HasValue && f.ChiTieuLuotDung < 0) loi["ChiTieuLuotDung"] = "Chỉ tiêu lượt dùng không được âm.";

            if (!TrangThaiChienDich.ChuyenDuoc(hienTai?.TrangThaiLuu).Contains(f.TrangThai))
                loi["TrangThai"] = "Không chuyển được từ \"" + TrangThaiChienDich.Ten(hienTai?.TrangThaiLuu ?? TrangThaiChienDich.KeHoach)
                    + "\" sang \"" + TrangThaiChienDich.Ten(f.TrangThai) + "\".";

            if (!f.NgayBatDau.HasValue) loi["NgayBatDau"] = "Vui lòng chọn ngày bắt đầu.";
            if (!f.NgayKetThuc.HasValue) loi["NgayKetThuc"] = "Vui lòng chọn ngày kết thúc.";
            if (f.NgayBatDau.HasValue && f.NgayKetThuc.HasValue)
            {
                if (f.NgayKetThuc.Value.Date < f.NgayBatDau.Value.Date) loi["NgayKetThuc"] = "Ngày kết thúc không được trước ngày bắt đầu.";
                else
                {
                    // Khuyến mãi (đang gắn hoặc sẽ gắn) phải nằm trong thời gian chiến dịch
                    var promotions = new PromotionService(db);
                    var ngoaiKhoang = f.ChuongTrinh.Select(promotions.LayTheoMa).Where(p => p != null)
                        .Where(p => p.NgayBatDau.Date < f.NgayBatDau.Value.Date || p.NgayKetThuc.Date > f.NgayKetThuc.Value.Date)
                        .Select(p => p.TenKhuyenMai + " (" + p.NgayBatDau.ToString("dd/MM/yyyy") + " – " + p.NgayKetThuc.ToString("dd/MM/yyyy") + ")")
                        .ToList();
                    if (ngoaiKhoang.Count > 0)
                        loi["NgayKetThuc"] = "Có khuyến mãi nằm ngoài thời gian chiến dịch, hãy sửa khuyến mãi trước: " + string.Join("; ", ngoaiKhoang) + ".";
                }
            }

            var hopLe = new HashSet<string>(LayChuongTrinhCoTheGan(hienTai?.MaChienDich).Select(p => p.MaKhuyenMai));
            if (f.ChuongTrinh.Any(id => !hopLe.Contains(id)))
                loi["ChuongTrinh"] = "Có chương trình không tồn tại, đã kết thúc hoặc thuộc chiến dịch khác.";
            var daGan = hienTai == null ? new HashSet<string>()
                : new HashSet<string>(new PromotionService(db).LayDanhSach(maChienDich: hienTai.MaChienDich).Select(p => p.MaKhuyenMai));
            if (TrangThaiChienDich.LaKetThuc(f.TrangThai) && f.ChuongTrinh.Any(id => !daGan.Contains(id)))
                loi["ChuongTrinh"] = "Chiến dịch hoàn thành / đã hủy không gắn thêm được khuyến mãi.";
            return loi;
        }

        private static SqlParameter[] ThamSo(string ma, CampaignFormViewModel f, int maNhanVien = 0)
        {
            return new[]
            {
                new SqlParameter("@ma", ma),
                new SqlParameter("@ten", f.TenChienDich),
                new SqlParameter("@mt", (object)f.MucTieu ?? DBNull.Value),
                new SqlParameter("@nd", (object)f.NoiDung ?? DBNull.Value),
                new SqlParameter("@dt", (object)f.DoiTuong ?? DBNull.Value),
                new SqlParameter("@kenh", f.KenhGhep),
                new SqlParameter("@ns", f.NganSach ?? 0),
                new SqlParameter("@cp", f.ChiPhiThucTe ?? 0),
                new SqlParameter("@ctDt", (object)f.ChiTieuDoanhThu ?? DBNull.Value),
                new SqlParameter("@ctLd", (object)f.ChiTieuLuotDung ?? DBNull.Value),
                new SqlParameter("@gc", (object)f.GhiChu ?? DBNull.Value),
                new SqlParameter("@tt", f.TrangThai),
                new SqlParameter("@bd", f.NgayBatDau.Value.Date),
                new SqlParameter("@kt", f.NgayKetThuc.Value.Date.AddDays(1).AddSeconds(-1)),
                new SqlParameter("@nv", maNhanVien)
            };
        }

        public string Tao(CampaignFormViewModel f, int maNhanVien)
        {
            string ma;
            do
            {
                ma = "CD" + PromotionService.HauToMaMoi();
            }
            while (db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.ChienDich WHERE maChienDich = @ma", new SqlParameter("@ma", ma)).First() > 0);
            using (var tx = db.Database.BeginTransaction())
            {
                db.Database.ExecuteSqlCommand(@"
                    INSERT INTO dbo.ChienDich (maChienDich, tenChienDich, mucTieu, noiDung, doiTuong, kenhTrienKhai, nganSach, chiPhiThucTe,
                                               chiTieuDoanhThu, chiTieuLuotDung, ghiChu, trangThai, ngayBatDau, ngayKetThuc, maNhanVienMarketing)
                    VALUES (@ma, @ten, @mt, @nd, @dt, @kenh, @ns, @cp, @ctDt, @ctLd, @gc, @tt, @bd, @kt, @nv)",
                    ThamSo(ma, f, maNhanVien));
                GanChuongTrinh(ma, f.ChuongTrinh);
                tx.Commit();
            }
            return ma;
        }

        public void CapNhat(string ma, CampaignFormViewModel f)
        {
            using (var tx = db.Database.BeginTransaction())
            {
                db.Database.ExecuteSqlCommand(@"
                    UPDATE dbo.ChienDich
                    SET tenChienDich = @ten, mucTieu = @mt, noiDung = @nd, doiTuong = @dt, kenhTrienKhai = @kenh, nganSach = @ns,
                        chiPhiThucTe = @cp, chiTieuDoanhThu = @ctDt, chiTieuLuotDung = @ctLd, ghiChu = @gc, trangThai = @tt,
                        ngayBatDau = @bd, ngayKetThuc = @kt
                    WHERE maChienDich = @ma",
                    ThamSo(ma, f).Where(p => p.ParameterName != "@nv").ToArray());
                // Gỡ liên kết các chương trình bị bỏ chọn (chương trình vẫn giữ nguyên), rồi gắn danh sách mới
                db.Database.ExecuteSqlCommand("UPDATE dbo.KhuyenMai SET maChienDich = NULL WHERE maChienDich = @ma", new SqlParameter("@ma", ma));
                GanChuongTrinh(ma, f.ChuongTrinh);
                tx.Commit();
            }
        }

        private void GanChuongTrinh(string ma, IEnumerable<string> chuongTrinh)
        {
            foreach (var km in chuongTrinh)
            {
                db.Database.ExecuteSqlCommand("UPDATE dbo.KhuyenMai SET maChienDich = @ma WHERE maKhuyenMai = @km",
                    new SqlParameter("@ma", ma), new SqlParameter("@km", km));
            }
        }

        /// <summary>Đổi trạng thái theo các bước chuyển hợp lệ (nút nhanh ở trang chi tiết).</summary>
        public string DoiTrangThai(string ma, string trangThaiMoi, out bool thanhCong)
        {
            thanhCong = false;
            var c = LayTheoMa(ma);
            if (c == null) return "Không tìm thấy chiến dịch.";
            if (!TrangThaiChienDich.TatCa.Contains(trangThaiMoi) || trangThaiMoi == c.TrangThaiLuu
                || !TrangThaiChienDich.ChuyenDuoc(c.TrangThaiLuu).Contains(trangThaiMoi))
                return "Không chuyển được từ \"" + c.TrangThaiHienThi + "\" sang \"" + TrangThaiChienDich.Ten(trangThaiMoi) + "\".";
            db.Database.ExecuteSqlCommand("UPDATE dbo.ChienDich SET trangThai = @tt WHERE maChienDich = @ma",
                new SqlParameter("@tt", trangThaiMoi), new SqlParameter("@ma", ma));
            thanhCong = true;
            string them = trangThaiMoi == TrangThaiChienDich.TamDung || trangThaiMoi == TrangThaiChienDich.DaHuy
                ? " Mọi mã thuộc chiến dịch tạm thời không lưu, không dùng được."
                : trangThaiMoi == TrangThaiChienDich.DangTrienKhai && c.TrangThaiLuu == TrangThaiChienDich.TamDung
                    ? " Các mã thuộc chiến dịch dùng lại được (nếu chương trình còn hiệu lực)." : "";
            return "Đã chuyển chiến dịch \"" + c.TenChienDich + "\" sang " + TrangThaiChienDich.Ten(trangThaiMoi) + "." + them;
        }

        /// <summary>Xóa chỉ khi chưa gắn khuyến mãi nào; có rồi thì phải chuyển "Đã hủy".</summary>
        public string Xoa(string ma, out bool thanhCong)
        {
            thanhCong = false;
            var c = LayTheoMa(ma);
            if (c == null) return "Không tìm thấy chiến dịch.";
            if (c.SoChuongTrinh > 0) return "Chiến dịch đã gắn " + c.SoChuongTrinh + " chương trình khuyến mãi nên không xóa được. Hãy chuyển sang \"Đã hủy\".";
            db.Database.ExecuteSqlCommand("DELETE FROM dbo.ChienDich WHERE maChienDich = @ma", new SqlParameter("@ma", ma));
            thanhCong = true;
            return "Đã xóa chiến dịch \"" + c.TenChienDich + "\".";
        }
    }
}
