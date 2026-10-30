using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Areas.Marketing.Models;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Thống kê tương tác khóa học cho Marketing: chỉ đọc TuongTacKhoaHoc, DangKyKhoaHoc, KhuyenMai, không ghi gì.
    /// </summary>
    public class CourseEngagementService
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 _db;
        public const int SoGoiY = 8;

        public CourseEngagementService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            _db = db;
        }

        public CourseEngagementViewModel LapBaoCao(DateTime tuNgay, DateTime denNgay)
        {
            var m = new CourseEngagementViewModel { TuNgay = tuNgay.Date, DenNgay = denNgay.Date };
            if (m.DenNgay < m.TuNgay)
            {
                m.ThongBaoLoi = "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.";
                return m;
            }

            var tu = m.TuNgay;
            var den = m.DenNgay.AddDays(1);
            List<CourseEngagementRow> tatCa;
            try
            {
                tatCa = _db.Database.SqlQuery<CourseEngagementRow>(@"
                    SELECT kh.maKhoaHoc AS MaKhoaHoc, kh.tenKhoaHoc AS TenKhoaHoc, dm.tenDanhMuc AS TenDanhMuc, kh.giaBan AS GiaBan,
                           ISNULL(SUM(t.luotXem), 0) AS LuotXem,
                           ISNULL(SUM(t.luotClick), 0) AS LuotClick,
                           ISNULL(SUM(CASE WHEN t.daThich = 1 THEN 1 ELSE 0 END), 0) AS LuotThich,
                           COUNT(DISTINCT CASE WHEN t.luotXem > 0 THEN t.maHocVien END) AS HocVienXem,
                           COUNT(DISTINCT CASE WHEN t.luotXem > 0 AND dk.maHocVien IS NOT NULL THEN t.maHocVien END) AS HocVienDangKy,
                           (SELECT COUNT(*) FROM dbo.DangKyKhoaHoc d
                             WHERE d.maKhoaHoc = kh.maKhoaHoc AND d.ngayDangKy >= @tu AND d.ngayDangKy < @den) AS DangKyTrongKy,
                           CAST(CASE WHEN EXISTS (
                               SELECT 1 FROM dbo.KhuyenMai km
                               WHERE km.trangThai = N'HoatDong' AND ISNULL(km.adminTamDung, 0) = 0
                                 AND km.ngayBatDau <= GETDATE() AND km.ngayKetThuc >= GETDATE()
                                 AND (km.loaiKhuyenMai = N'GiamTrucTiep' OR km.phamVi <> N'TatCa')
                                 AND (km.phamVi = N'TatCa'
                                      OR (km.phamVi = N'DanhMuc' AND km.maDanhMucApDung = kh.maDanhMuc)
                                      OR (km.phamVi = N'KhoaHoc' AND EXISTS (
                                            SELECT 1 FROM dbo.KhuyenMaiKhoaHoc k
                                            WHERE k.maKhuyenMai = km.maKhuyenMai AND k.maKhoaHoc = kh.maKhoaHoc))))
                               THEN 1 ELSE 0 END AS BIT) AS CoUuDai
                    FROM dbo.KhoaHoc kh
                    INNER JOIN dbo.DanhMuc dm ON dm.maDanhMuc = kh.maDanhMuc
                    LEFT JOIN dbo.TuongTacKhoaHoc t ON t.maKhoaHoc = kh.maKhoaHoc AND t.lanCapNhat >= @tu AND t.lanCapNhat < @den
                    LEFT JOIN (SELECT DISTINCT maHocVien, maKhoaHoc FROM dbo.DangKyKhoaHoc
                               WHERE ngayDangKy >= @tu AND ngayDangKy < @den) dk
                           ON dk.maHocVien = t.maHocVien AND dk.maKhoaHoc = t.maKhoaHoc
                    WHERE kh.trangThai = @daXuatBan
                    GROUP BY kh.maKhoaHoc, kh.tenKhoaHoc, dm.tenDanhMuc, kh.giaBan, kh.maDanhMuc",
                    new SqlParameter("@tu", tu), new SqlParameter("@den", den), new SqlParameter("@daXuatBan", CourseStatus.DaXuatBan)).ToList();
            }
            catch (Exception)
            {
                m.ThongBaoLoi = "Không đọc được dữ liệu tương tác. Kiểm tra đã chạy Migrations 005 (gợi ý khóa học) và 021 (giảm trực tiếp).";
                return m;
            }

            m.TongLuotXem = tatCa.Sum(r => r.LuotXem);
            m.TongLuotClick = tatCa.Sum(r => r.LuotClick);
            m.TongLuotThich = tatCa.Sum(r => r.LuotThich);
            m.TongDangKy = tatCa.Sum(r => r.DangKyTrongKy);

            var coTuongTac = tatCa.Where(r => r.Diem > 0).OrderByDescending(r => r.Diem).ThenBy(r => r.TenKhoaHoc).ToList();
            m.Rows = coTuongTac;
            m.GoiY = coTuongTac.Where(r => !r.CoUuDai).Take(SoGoiY).ToList();
            m.DanhMucs = coTuongTac.Select(r => r.TenDanhMuc).Distinct().OrderBy(x => x).ToList();
            return m;
        }

        /// <summary>Lọc, sắp xếp và cắt trang cho bảng chi tiết.</summary>
        public void ApDungBoLoc(CourseEngagementViewModel m, string tuKhoa, string danhMuc, string uuDai, string sapXep, int trang)
        {
            m.TuKhoa = (tuKhoa ?? "").Trim();
            m.DanhMuc = danhMuc ?? "";
            m.UuDai = uuDai == "co" || uuDai == "chua" ? uuDai : "";
            m.SapXep = sapXep ?? "diem";

            IEnumerable<CourseEngagementRow> q = m.Rows;
            if (m.TuKhoa.Length > 0)
            {
                // Không phân biệt hoa thường và dấu tiếng Việt: gõ "lap trinh" vẫn ra "Lập trình"
                var canTim = SearchText.KhongDau(m.TuKhoa).ToLowerInvariant();
                q = q.Where(r => r.TenKhoaHoc != null && SearchText.KhongDau(r.TenKhoaHoc).ToLowerInvariant().Contains(canTim));
            }
            if (m.DanhMuc.Length > 0) q = q.Where(r => r.TenDanhMuc == m.DanhMuc);
            if (m.UuDai == "co") q = q.Where(r => r.CoUuDai);
            else if (m.UuDai == "chua") q = q.Where(r => !r.CoUuDai);

            IOrderedEnumerable<CourseEngagementRow> sx;
            switch (m.SapXep)
            {
                case "xem": sx = q.OrderByDescending(r => r.LuotXem); break;
                case "nhap": sx = q.OrderByDescending(r => r.LuotClick); break;
                case "thich": sx = q.OrderByDescending(r => r.LuotThich); break;
                case "dangky": sx = q.OrderByDescending(r => r.DangKyTrongKy); break;
                case "chuyendoi": sx = q.OrderByDescending(r => r.TyLeChuyenDoi ?? -1m); break;
                default: m.SapXep = "diem"; sx = q.OrderByDescending(r => r.Diem); break;
            }

            var ds = sx.ThenBy(r => r.TenKhoaHoc).ToList();
            m.TongDong = ds.Count;
            m.SoTrang = Math.Max(1, (int)Math.Ceiling(ds.Count / (double)CourseEngagementViewModel.KichThuocTrang));
            m.Trang = Math.Min(Math.Max(1, trang), m.SoTrang);
            m.TrangRows = ds.Skip((m.Trang - 1) * CourseEngagementViewModel.KichThuocTrang).Take(CourseEngagementViewModel.KichThuocTrang).ToList();
        }
    }
}
