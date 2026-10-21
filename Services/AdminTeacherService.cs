using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Đọc dữ liệu cho Admin / Quản lý giáo viên (giáo viên đã được duyệt, vai trò GiaoVien):
    /// khóa học, học viên, đánh giá, doanh thu thực thu (cùng cách tính với trang Rút tiền của giáo viên: 80% doanh thu
    /// sau mã giảm giá), phiếu chi, báo cáo vi phạm. Thao tác khóa / mở khóa / cảnh cáo đi qua AccountAdminService.
    /// </summary>
    public class AdminTeacherService
    {
        public const decimal TyLeGiaoVien = 0.8m;

        private static readonly string[] DangBan = { CourseStatus.DaXuatBan, "Đã duyệt", "Active", "Đang hoạt động" };
        private static readonly string[] ChoDuyet = { CourseStatus.ChoDuyet, "Chờ kiểm duyệt", "Chờ xét duyệt", "Pending" };

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public AdminTeacherService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        /// <param name="trangThai">"active" / "locked" / "violation" (có báo cáo vi phạm chờ xử lý) / null</param>
        /// <param name="sapXep">"priority" (mặc định) / "revenue" / "rating" / "students" / "newest"</param>
        public List<GiaoVienAdminItem> DanhSach(string tuKhoa, string trangThai, string sapXep)
        {
            var thamSo = new List<SqlParameter> { new SqlParameter("@gv", AccountAdminService.GiaoVien) };
            string dieuKien = "";
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                dieuKien = " AND (nd.hoTen LIKE @q OR nd.email LIKE @q)";
                thamSo.Add(new SqlParameter("@q", "%" + tuKhoa.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%"));
            }
            thamSo.AddRange(DangBan.Select((v, i) => new SqlParameter("@b" + i, v)));
            thamSo.AddRange(ChoDuyet.Select((v, i) => new SqlParameter("@c" + i, v)));
            string inBan = string.Join(", ", DangBan.Select((v, i) => "@b" + i));
            string inCho = string.Join(", ", ChoDuyet.Select((v, i) => "@c" + i));

            var ds = db.Database.SqlQuery<GiaoVienAdminItem>(@"
                SELECT nd.maNguoiDung AS MaGiaoVien, nd.hoTen AS HoTen, nd.email AS Email, nd.anhDaiDien AS AnhDaiDien,
                       CAST(ISNULL(nd.trangThai, 1) AS BIT) AS HoatDong, nd.khoaDenNgay AS KhoaDenNgay, nd.ngayTao AS NgayTao,
                       (SELECT COUNT(*) FROM dbo.KhoaHoc k WHERE k.maGiaoVien = nd.maNguoiDung) AS TongKhoaHoc,
                       (SELECT COUNT(*) FROM dbo.KhoaHoc k WHERE k.maGiaoVien = nd.maNguoiDung AND k.trangThai IN (" + inBan + @")) AS SoKhoaDangBan,
                       (SELECT COUNT(*) FROM dbo.KhoaHoc k WHERE k.maGiaoVien = nd.maNguoiDung AND k.trangThai IN (" + inCho + @")) AS SoKhoaChoDuyet,
                       (SELECT COUNT(DISTINCT dk.maHocVien) FROM dbo.DangKyKhoaHoc dk INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = dk.maKhoaHoc
                         WHERE k.maGiaoVien = nd.maNguoiDung) AS SoHocVien,
                       (SELECT CAST(AVG(CAST(dg.soSao AS FLOAT)) AS FLOAT) FROM dbo.DanhGiaKhoaHoc dg INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = dg.maKhoaHoc
                         WHERE k.maGiaoVien = nd.maNguoiDung) AS DiemTrungBinh,
                       (SELECT COUNT(*) FROM dbo.DanhGiaKhoaHoc dg INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = dg.maKhoaHoc
                         WHERE k.maGiaoVien = nd.maNguoiDung) AS SoDanhGia,
                       CAST(ISNULL((SELECT SUM(p.soTienYeuCau) FROM dbo.NhanThanhToan p
                         WHERE p.maGiaoVien = nd.maNguoiDung AND ISNULL(p.trangThai, N'Chờ duyệt') = N'Chờ duyệt'), 0) AS DECIMAL(18, 2)) AS ChoChiTra
                FROM dbo.NguoiDung nd
                WHERE nd.vaiTro = @gv" + dieuKien, thamSo.ToArray()).ToList();

            var doanhThu = DoanhThuThucThuTheoGiaoVien();
            var viPham = ViPhamTheoGiaoVien();
            var hoSo = TeacherApplicationService.GetAll()
                .Where(a => a.Status == "Approved")
                .GroupBy(a => a.UserId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAt).First());
            foreach (var g in ds)
            {
                decimal dt;
                g.DoanhThuThucThu = doanhThu.TryGetValue(g.MaGiaoVien, out dt) ? dt : 0;
                SoViPham vp;
                if (viPham.TryGetValue(g.MaGiaoVien, out vp))
                {
                    g.SoBaoCaoViPham = vp.TongSo;
                    g.SoViPhamChoXuLy = vp.ChoXuLy;
                }
                TeacherApplication app;
                if (hoSo.TryGetValue(g.MaGiaoVien, out app)) g.ChuyenMon = app.Specialization;
            }

            if (trangThai == "active") ds = ds.Where(g => g.HoatDong).ToList();
            else if (trangThai == "locked") ds = ds.Where(g => !g.HoatDong).ToList();
            else if (trangThai == "violation") ds = ds.Where(g => g.SoViPhamChoXuLy > 0).ToList();

            switch (sapXep)
            {
                case "revenue": return ds.OrderByDescending(g => g.DoanhThuThucThu).ToList();
                case "rating": return ds.OrderByDescending(g => g.DiemTrungBinh ?? 0).ThenByDescending(g => g.SoDanhGia).ToList();
                case "students": return ds.OrderByDescending(g => g.SoHocVien).ToList();
                case "newest": return ds.OrderByDescending(g => g.MaGiaoVien).ToList();
                default:
                    // Cần Admin chú ý trước: có vi phạm chờ xử lý, đang bị khóa, có phiếu chi chờ duyệt
                    return ds.OrderByDescending(g => g.SoViPhamChoXuLy).ThenBy(g => g.HoatDong).ThenByDescending(g => g.ChoChiTra).ThenBy(g => g.HoTen).ToList();
            }
        }

        public GiaoVienChiTiet ChiTiet(int maGiaoVien)
        {
            var co = new AdminUserQueryService(db).ChiTiet(maGiaoVien);
            if (co == null || co.VaiTro != AccountAdminService.GiaoVien) return null;
            var kq = new GiaoVienChiTiet { CoBan = co };

            kq.HoSo = TeacherApplicationService.GetAll()
                .Where(a => a.UserId == maGiaoVien && a.Status == "Approved")
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();

            kq.KhoaHoc = db.Database.SqlQuery<KhoaHocCuaGiaoVien>(@"
                SELECT k.maKhoaHoc AS MaKhoaHoc, k.tenKhoaHoc AS TenKhoaHoc, k.trangThai AS TrangThai, k.giaBan AS GiaBan, k.giaKhuyenMai AS GiaKhuyenMai,
                       (SELECT COUNT(*) FROM dbo.DangKyKhoaHoc dk WHERE dk.maKhoaHoc = k.maKhoaHoc) AS SoHocVien,
                       (SELECT CAST(AVG(CAST(dg.soSao AS FLOAT)) AS FLOAT) FROM dbo.DanhGiaKhoaHoc dg WHERE dg.maKhoaHoc = k.maKhoaHoc) AS DiemTrungBinh,
                       (SELECT COUNT(*) FROM dbo.DanhGiaKhoaHoc dg WHERE dg.maKhoaHoc = k.maKhoaHoc) AS SoDanhGia
                FROM dbo.KhoaHoc k WHERE k.maGiaoVien = @id
                ORDER BY k.tenKhoaHoc", new SqlParameter("@id", maGiaoVien)).ToList();

            var dtKhoa = DoanhThuThucThuTheoKhoa(maGiaoVien);
            foreach (var k in kq.KhoaHoc)
            {
                decimal dt;
                k.DoanhThuThucThu = dtKhoa.TryGetValue(k.MaKhoaHoc, out dt) ? dt : 0;
            }

            kq.PhieuChi = db.Database.SqlQuery<PhieuChiCuaGiaoVien>(@"
                SELECT p.maPhieuChi AS MaPhieuChi, p.soTienYeuCau AS SoTien, p.ngayYeuCau AS NgayYeuCau, ISNULL(p.trangThai, N'Chờ duyệt') AS TrangThai
                FROM dbo.NhanThanhToan p WHERE p.maGiaoVien = @id
                ORDER BY p.ngayYeuCau DESC", new SqlParameter("@id", maGiaoVien)).ToList();

            kq.TongThucThu = kq.KhoaHoc.Sum(k => k.DoanhThuThucThu);
            kq.PhanGiaoVien = Math.Round(kq.TongThucThu * TyLeGiaoVien, 0);
            kq.DaChiTra = kq.PhieuChi.Where(p => p.TrangThai == "Đã thanh toán").Sum(p => p.SoTien);
            kq.ChoChiTra = kq.PhieuChi.Where(p => p.TrangThai == "Chờ duyệt").Sum(p => p.SoTien);
            kq.KhaDung = Math.Max(0, kq.PhanGiaoVien - kq.PhieuChi.Where(p => p.TrangThai != "Từ chối").Sum(p => p.SoTien));

            kq.DanhGia = db.Database.SqlQuery<DanhGiaKhoaHocCuaGiaoVien>(@"
                SELECT TOP (30) k.tenKhoaHoc AS TenKhoaHoc, hv.hoTen AS TenHocVien, dg.soSao AS SoSao, dg.noiDung AS NoiDung, dg.ngayDanhGia AS NgayDanhGia
                FROM dbo.DanhGiaKhoaHoc dg
                INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = dg.maKhoaHoc
                INNER JOIN dbo.NguoiDung hv ON hv.maNguoiDung = dg.maHocVien
                WHERE k.maGiaoVien = @id
                ORDER BY dg.ngayDanhGia DESC", new SqlParameter("@id", maGiaoVien)).ToList();

            if (CoBangViPham())
            {
                kq.ViPham = db.Database.SqlQuery<ViPhamCuaGiaoVien>(@"
                    SELECT b.maBaoCao AS MaBaoCao, b.maKhoaHoc AS MaKhoaHoc, k.tenKhoaHoc AS TenKhoaHoc, b.loaiViPham AS LoaiViPham,
                           b.moTa AS MoTa, b.trangThai AS TrangThai, b.ngayTao AS NgayTao
                    FROM dbo.BaoCaoViPham b INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = b.maKhoaHoc
                    WHERE k.maGiaoVien = @id
                    ORDER BY CASE WHEN b.trangThai = N'Chờ xử lý' THEN 0 ELSE 1 END, b.ngayTao DESC", new SqlParameter("@id", maGiaoVien)).ToList();
            }
            return kq;
        }

        public ThongKeGiaoVien ThongKe(List<GiaoVienAdminItem> tatCa)
        {
            return new ThongKeGiaoVien
            {
                TongSo = tatCa.Count,
                DangKhoa = tatCa.Count(g => !g.HoatDong),
                CoViPham = tatCa.Count(g => g.SoViPhamChoXuLy > 0),
                ChoChiTra = tatCa.Sum(g => g.ChoChiTra)
            };
        }

        // ------------------------------------------------------------------ doanh thu / vi phạm

        /// <summary>Doanh thu thực thu (sau mã giảm giá) theo giáo viên, chỉ đơn đã thanh toán. Cùng cách tính với Instructor/Payout.</summary>
        private Dictionary<int, decimal> DoanhThuThucThuTheoGiaoVien()
        {
            var dong = DongDaThanhToan(null);
            var tyLe = new RevenueService(db).TyLeThucThuTheoDon();
            return dong.GroupBy(d => d.MaGiaoVien)
                .ToDictionary(g => g.Key, g => g.Sum(d => ThucThu(d, tyLe)));
        }

        private Dictionary<string, decimal> DoanhThuThucThuTheoKhoa(int maGiaoVien)
        {
            var dong = DongDaThanhToan(maGiaoVien);
            var tyLe = new RevenueService(db).TyLeThucThuTheoDon();
            return dong.GroupBy(d => d.MaKhoaHoc)
                .ToDictionary(g => g.Key, g => g.Sum(d => ThucThu(d, tyLe)));
        }

        private static decimal ThucThu(DongDoanhThu d, Dictionary<string, decimal> tyLe)
        {
            decimal r;
            return tyLe.TryGetValue(d.MaDonHang, out r) ? Math.Round(d.DonGia * r, 0) : d.DonGia;
        }

        private List<DongDoanhThu> DongDaThanhToan(int? maGiaoVien)
        {
            var thanhCong = RevenueService.TrangThaiThanhCong;
            return db.ChiTietDonHang
                .Where(ct => ct.DonHang != null && thanhCong.Contains(ct.DonHang.trangThai) && (maGiaoVien == null || ct.KhoaHoc.maGiaoVien == maGiaoVien))
                .Select(ct => new DongDoanhThu { MaDonHang = ct.maDonHang, MaKhoaHoc = ct.maKhoaHoc, MaGiaoVien = ct.KhoaHoc.maGiaoVien, DonGia = ct.donGia })
                .ToList();
        }

        private bool CoBangViPham()
        {
            return db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.BaoCaoViPham') IS NULL THEN 0 ELSE 1 END").Single() == 1;
        }

        private Dictionary<int, SoViPham> ViPhamTheoGiaoVien()
        {
            if (!CoBangViPham()) return new Dictionary<int, SoViPham>();
            return db.Database.SqlQuery<SoViPham>(@"
                SELECT k.maGiaoVien AS MaGiaoVien, COUNT(*) AS TongSo,
                       SUM(CASE WHEN b.trangThai = N'Chờ xử lý' THEN 1 ELSE 0 END) AS ChoXuLy
                FROM dbo.BaoCaoViPham b INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = b.maKhoaHoc
                GROUP BY k.maGiaoVien").ToList().ToDictionary(v => v.MaGiaoVien, v => v);
        }

        private class DongDoanhThu
        {
            public string MaDonHang { get; set; }
            public string MaKhoaHoc { get; set; }
            public int MaGiaoVien { get; set; }
            public decimal DonGia { get; set; }
        }

        private class SoViPham
        {
            public int MaGiaoVien { get; set; }
            public int TongSo { get; set; }
            public int ChoXuLy { get; set; }
        }
    }

    public class ThongKeGiaoVien
    {
        public int TongSo { get; set; }
        public int DangKhoa { get; set; }
        public int CoViPham { get; set; }
        public decimal ChoChiTra { get; set; }
    }

    public class GiaoVienAdminItem
    {
        public int MaGiaoVien { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string AnhDaiDien { get; set; }
        public bool HoatDong { get; set; }
        public DateTime? KhoaDenNgay { get; set; }
        public DateTime? NgayTao { get; set; }
        public int TongKhoaHoc { get; set; }
        public int SoKhoaDangBan { get; set; }
        public int SoKhoaChoDuyet { get; set; }
        public int SoHocVien { get; set; }
        public double? DiemTrungBinh { get; set; }
        public int SoDanhGia { get; set; }
        public decimal ChoChiTra { get; set; }
        public decimal DoanhThuThucThu { get; set; }
        public int SoBaoCaoViPham { get; set; }
        public int SoViPhamChoXuLy { get; set; }
        public string ChuyenMon { get; set; }
    }

    public class GiaoVienChiTiet
    {
        public NguoiDungChiTiet CoBan { get; set; }
        public TeacherApplication HoSo { get; set; }
        public List<KhoaHocCuaGiaoVien> KhoaHoc { get; set; } = new List<KhoaHocCuaGiaoVien>();
        public List<PhieuChiCuaGiaoVien> PhieuChi { get; set; } = new List<PhieuChiCuaGiaoVien>();
        public List<DanhGiaKhoaHocCuaGiaoVien> DanhGia { get; set; } = new List<DanhGiaKhoaHocCuaGiaoVien>();
        public List<ViPhamCuaGiaoVien> ViPham { get; set; } = new List<ViPhamCuaGiaoVien>();
        public decimal TongThucThu { get; set; }
        public decimal PhanGiaoVien { get; set; }
        public decimal DaChiTra { get; set; }
        public decimal ChoChiTra { get; set; }
        public decimal KhaDung { get; set; }
    }

    public class KhoaHocCuaGiaoVien
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string TrangThai { get; set; }
        public decimal GiaBan { get; set; }
        public decimal? GiaKhuyenMai { get; set; }
        public int SoHocVien { get; set; }
        public double? DiemTrungBinh { get; set; }
        public int SoDanhGia { get; set; }
        public decimal DoanhThuThucThu { get; set; }
    }

    public class PhieuChiCuaGiaoVien
    {
        public string MaPhieuChi { get; set; }
        public decimal SoTien { get; set; }
        public DateTime? NgayYeuCau { get; set; }
        public string TrangThai { get; set; }
    }

    public class DanhGiaKhoaHocCuaGiaoVien
    {
        public string TenKhoaHoc { get; set; }
        public string TenHocVien { get; set; }
        public int SoSao { get; set; }
        public string NoiDung { get; set; }
        public DateTime? NgayDanhGia { get; set; }
    }

    public class ViPhamCuaGiaoVien
    {
        public string MaBaoCao { get; set; }
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string LoaiViPham { get; set; }
        public string MoTa { get; set; }
        public string TrangThai { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
