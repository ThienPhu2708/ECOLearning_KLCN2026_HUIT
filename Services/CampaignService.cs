using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Chiến dịch Marketing (bảng ChienDich, migration 008). Một chiến dịch gắn nhiều chương trình khuyến mãi
    /// (KhuyenMai.maChienDich); số liệu chiến dịch = tổng số liệu sử dụng ưu đãi của các chương trình đó.
    /// Kết thúc chiến dịch không xóa hay kết thúc chương trình khuyến mãi, không đụng tới giao dịch cũ.
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
                   cd.doiTuong AS DoiTuong, cd.ngayBatDau AS NgayBatDau, cd.ngayKetThuc AS NgayKetThuc, cd.trangThai AS TrangThaiLuu,
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
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai = N'Đã thanh toán'
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
            var list = db.Database.SqlQuery<ChienDichItem>(
                SelectChienDich + (dieuKien.Count > 0 ? " WHERE " + string.Join(" AND ", dieuKien) : "") + " ORDER BY cd.ngayTao DESC",
                thamSo.ToArray()).ToList();

            if (!string.IsNullOrEmpty(trangThai) && trangThai != "all")
            {
                var now = DateTime.Now;
                list = list.Where(c => c.TrangThaiHienThi(now) == trangThai).ToList();
            }
            return list;
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
            return new CampaignFormViewModel
            {
                MaChienDich = c.MaChienDich,
                TenChienDich = c.TenChienDich,
                MucTieu = c.MucTieu,
                NoiDung = c.NoiDung,
                DoiTuong = c.DoiTuong,
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
            f.ChuongTrinh = (f.ChuongTrinh ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

            if (f.TenChienDich.Length == 0) loi["TenChienDich"] = "Vui lòng nhập tên chiến dịch.";
            else if (f.TenChienDich.Length > 150) loi["TenChienDich"] = "Tên chiến dịch tối đa 150 ký tự.";
            if (f.MucTieu != null && f.MucTieu.Length > 500) loi["MucTieu"] = "Mục tiêu tối đa 500 ký tự.";
            if (f.NoiDung != null && f.NoiDung.Length > 2000) loi["NoiDung"] = "Nội dung tối đa 2000 ký tự.";
            if (f.DoiTuong != null && f.DoiTuong.Length > 255) loi["DoiTuong"] = "Đối tượng tối đa 255 ký tự.";

            if (!f.NgayBatDau.HasValue) loi["NgayBatDau"] = "Vui lòng chọn ngày bắt đầu.";
            if (!f.NgayKetThuc.HasValue) loi["NgayKetThuc"] = "Vui lòng chọn ngày kết thúc.";
            if (f.NgayBatDau.HasValue && f.NgayKetThuc.HasValue && f.NgayKetThuc.Value.Date < f.NgayBatDau.Value.Date)
                loi["NgayKetThuc"] = "Ngày kết thúc không được trước ngày bắt đầu.";

            var hopLe = new HashSet<string>(LayChuongTrinhCoTheGan(hienTai?.MaChienDich).Select(p => p.MaKhuyenMai));
            if (f.ChuongTrinh.Any(id => !hopLe.Contains(id)))
                loi["ChuongTrinh"] = "Có chương trình không tồn tại, đã kết thúc hoặc thuộc chiến dịch khác.";
            return loi;
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
                    INSERT INTO dbo.ChienDich (maChienDich, tenChienDich, mucTieu, noiDung, doiTuong, ngayBatDau, ngayKetThuc, maNhanVienMarketing)
                    VALUES (@ma, @ten, @mt, @nd, @dt, @bd, @kt, @nv)",
                    new SqlParameter("@ma", ma),
                    new SqlParameter("@ten", f.TenChienDich),
                    new SqlParameter("@mt", (object)f.MucTieu ?? DBNull.Value),
                    new SqlParameter("@nd", (object)f.NoiDung ?? DBNull.Value),
                    new SqlParameter("@dt", (object)f.DoiTuong ?? DBNull.Value),
                    new SqlParameter("@bd", f.NgayBatDau.Value.Date),
                    new SqlParameter("@kt", f.NgayKetThuc.Value.Date.AddDays(1).AddSeconds(-1)),
                    new SqlParameter("@nv", maNhanVien));
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
                    SET tenChienDich = @ten, mucTieu = @mt, noiDung = @nd, doiTuong = @dt, ngayBatDau = @bd, ngayKetThuc = @kt
                    WHERE maChienDich = @ma",
                    new SqlParameter("@ma", ma),
                    new SqlParameter("@ten", f.TenChienDich),
                    new SqlParameter("@mt", (object)f.MucTieu ?? DBNull.Value),
                    new SqlParameter("@nd", (object)f.NoiDung ?? DBNull.Value),
                    new SqlParameter("@dt", (object)f.DoiTuong ?? DBNull.Value),
                    new SqlParameter("@bd", f.NgayBatDau.Value.Date),
                    new SqlParameter("@kt", f.NgayKetThuc.Value.Date.AddDays(1).AddSeconds(-1)));
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

        public void KetThuc(string ma)
        {
            db.Database.ExecuteSqlCommand("UPDATE dbo.ChienDich SET trangThai = N'KetThuc' WHERE maChienDich = @ma", new SqlParameter("@ma", ma));
        }
    }
}
