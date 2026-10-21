using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>Đọc dữ liệu cho trang Admin / Quản lý người dùng (cần migration 016 cho cột khóa tài khoản).</summary>
    public class AdminUserQueryService
    {
        public const int SoDongMoiTrang = 20;

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public AdminUserQueryService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public ThongKeNguoiDung ThongKe()
        {
            return db.Database.SqlQuery<ThongKeNguoiDung>(@"
                SELECT COUNT(*) AS TongSo,
                       SUM(CASE WHEN vaiTro = N'HocVien' THEN 1 ELSE 0 END) AS HocVien,
                       SUM(CASE WHEN vaiTro = N'GiaoVien' THEN 1 ELSE 0 END) AS GiaoVien,
                       SUM(CASE WHEN vaiTro IN (N'Marketing', N'Admin') THEN 1 ELSE 0 END) AS NhanVien,
                       SUM(CASE WHEN trangThai = 0 THEN 1 ELSE 0 END) AS DangKhoa
                FROM dbo.NguoiDung").Single();
        }

        /// <param name="trangThai">"active" / "locked" / null (tất cả)</param>
        public List<NguoiDungAdminItem> DanhSach(string tuKhoa, string vaiTro, string trangThai, int trang, out PhanTrangViewModel phanTrang)
        {
            var dieuKien = new List<string>();
            var thamSo = new List<SqlParameter>();
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                dieuKien.Add("(nd.hoTen LIKE @q ESCAPE '\\' OR nd.email LIKE @q ESCAPE '\\' OR nd.soDienThoai LIKE @q ESCAPE '\\' OR CAST(nd.maNguoiDung AS VARCHAR(12)) = @ma)");
                thamSo.Add(new SqlParameter("@q", "%" + ThoatLike(tuKhoa.Trim()) + "%"));
                thamSo.Add(new SqlParameter("@ma", tuKhoa.Trim()));
            }
            if (!string.IsNullOrEmpty(vaiTro))
            {
                dieuKien.Add("nd.vaiTro = @vt");
                thamSo.Add(new SqlParameter("@vt", vaiTro));
            }
            if (trangThai == "active") dieuKien.Add("ISNULL(nd.trangThai, 1) = 1");
            else if (trangThai == "locked") dieuKien.Add("nd.trangThai = 0");
            string where = dieuKien.Count > 0 ? "WHERE " + string.Join(" AND ", dieuKien) : "";

            int tong = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.NguoiDung nd " + where, NhanBan(thamSo)).Single();
            phanTrang = new PhanTrangViewModel(trang, tong, SoDongMoiTrang) { DonVi = "người dùng" };

            var p = NhanBan(thamSo).ToList();
            p.Add(new SqlParameter("@boQua", phanTrang.BoQua));
            p.Add(new SqlParameter("@lay", phanTrang.KichThuoc));
            return db.Database.SqlQuery<NguoiDungAdminItem>(@"
                SELECT nd.maNguoiDung AS MaNguoiDung, nd.hoTen AS HoTen, nd.email AS Email, nd.soDienThoai AS SoDienThoai,
                       nd.vaiTro AS VaiTro, nd.anhDaiDien AS AnhDaiDien, CAST(ISNULL(nd.trangThai, 1) AS BIT) AS HoatDong,
                       nd.khoaDenNgay AS KhoaDenNgay, nd.ngayTao AS NgayTao, nd.canDoiMatKhau AS CanDoiMatKhau,
                       (SELECT COUNT(*) FROM dbo.DangKyKhoaHoc dk WHERE dk.maHocVien = nd.maNguoiDung) AS SoKhoaDaDangKy,
                       (SELECT COUNT(*) FROM dbo.KhoaHoc k WHERE k.maGiaoVien = nd.maNguoiDung) AS SoKhoaGiangDay
                FROM dbo.NguoiDung nd " + where + @"
                ORDER BY CASE WHEN nd.trangThai = 0 THEN 0 ELSE 1 END, nd.maNguoiDung DESC
                OFFSET @boQua ROWS FETCH NEXT @lay ROWS ONLY", p.ToArray()).ToList();
        }

        public NguoiDungChiTiet ChiTiet(int maNguoiDung)
        {
            var nd = db.Database.SqlQuery<NguoiDungChiTiet>(@"
                SELECT nd.maNguoiDung AS MaNguoiDung, nd.hoTen AS HoTen, nd.email AS Email, nd.soDienThoai AS SoDienThoai,
                       nd.gioiTinh AS GioiTinh, nd.ngaySinh AS NgaySinh, nd.vaiTro AS VaiTro, nd.anhDaiDien AS AnhDaiDien,
                       CAST(ISNULL(nd.trangThai, 1) AS BIT) AS HoatDong, nd.lyDoKhoa AS LyDoKhoa, nd.ngayKhoa AS NgayKhoa,
                       nd.khoaDenNgay AS KhoaDenNgay, nd.ngayTao AS NgayTao, nd.canDoiMatKhau AS CanDoiMatKhau,
                       ad.hoTen AS TenAdminKhoa
                FROM dbo.NguoiDung nd
                LEFT JOIN dbo.NguoiDung ad ON ad.maNguoiDung = nd.maAdminKhoa
                WHERE nd.maNguoiDung = @id", new SqlParameter("@id", maNguoiDung)).FirstOrDefault();
            if (nd == null) return null;

            nd.KhoaHoc = db.Database.SqlQuery<KhoaHocCuaHocVien>(@"
                SELECT dk.maKhoaHoc AS MaKhoaHoc, k.tenKhoaHoc AS TenKhoaHoc, dk.ngayDangKy AS NgayDangKy, k.trangThai AS TrangThaiKhoaHoc
                FROM dbo.DangKyKhoaHoc dk INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = dk.maKhoaHoc
                WHERE dk.maHocVien = @id
                ORDER BY dk.ngayDangKy DESC", new SqlParameter("@id", maNguoiDung)).ToList();
            if (nd.KhoaHoc.Count > 0)
            {
                var tienDo = new LearningService(db).TienDoCacKhoa(maNguoiDung, nd.KhoaHoc.Select(k => k.MaKhoaHoc).ToList());
                foreach (var k in nd.KhoaHoc)
                {
                    TienDoKhoaHoc td;
                    if (tienDo.TryGetValue(k.MaKhoaHoc, out td))
                    {
                        k.PhanTram = td.PhanTram;
                        k.LanHocGanNhat = td.LanHocGanNhat;
                    }
                }
            }

            nd.DonHang = db.Database.SqlQuery<DonHangCuaHocVien>(@"
                SELECT d.maDonHang AS MaDonHang, d.ngayDat AS NgayDat, d.tongTien AS TongTien, d.trangThai AS TrangThai,
                       (SELECT COUNT(*) FROM dbo.ChiTietDonHang ct WHERE ct.maDonHang = d.maDonHang) AS SoKhoa
                FROM dbo.DonHang d WHERE d.maHocVien = @id
                ORDER BY d.ngayDat DESC", new SqlParameter("@id", maNguoiDung)).ToList();

            nd.DanhGia = db.Database.SqlQuery<DanhGiaCuaHocVien>(@"
                SELECT dg.maKhoaHoc AS MaKhoaHoc, k.tenKhoaHoc AS TenKhoaHoc, dg.soSao AS SoSao, dg.noiDung AS NoiDung, dg.ngayDanhGia AS NgayDanhGia
                FROM dbo.DanhGiaKhoaHoc dg INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = dg.maKhoaHoc
                WHERE dg.maHocVien = @id
                ORDER BY dg.ngayDanhGia DESC", new SqlParameter("@id", maNguoiDung)).ToList();

            nd.SoChungChi = db.Database.SqlQuery<int>(@"
                SELECT CASE WHEN OBJECT_ID('dbo.QuizCertificate') IS NULL THEN 0
                            ELSE (SELECT COUNT(*) FROM dbo.QuizCertificate WHERE StudentId = @id AND IssuedAt IS NOT NULL) END",
                new SqlParameter("@id", maNguoiDung)).Single();

            nd.NhatKy = new AccountAdminService(db).LayNhatKy(AccountAdminService.DoiTuongNguoiDung, maNguoiDung.ToString());
            return nd;
        }

        private static string ThoatLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

        // Mỗi lần gọi SqlQuery cần đối tượng SqlParameter mới (một tham số không dùng lại được cho hai câu lệnh)
        private static SqlParameter[] NhanBan(IEnumerable<SqlParameter> ds) => ds.Select(x => new SqlParameter(x.ParameterName, x.Value)).ToArray();

        public static string TenVaiTro(string vaiTro)
        {
            switch (vaiTro)
            {
                case AccountAdminService.HocVien: return "Học viên";
                case AccountAdminService.GiaoVien: return "Giáo viên";
                case AccountAdminService.Marketing: return "Marketing";
                case AccountAdminService.Admin: return "Quản trị viên";
                default: return vaiTro;
            }
        }

        public static string MauVaiTro(string vaiTro)
        {
            switch (vaiTro)
            {
                case AccountAdminService.GiaoVien: return "badge-primary";
                case AccountAdminService.Marketing: return "badge-warning";
                case AccountAdminService.Admin: return "badge-danger";
                default: return "badge-secondary";
            }
        }
    }

    public class ThongKeNguoiDung
    {
        public int TongSo { get; set; }
        public int HocVien { get; set; }
        public int GiaoVien { get; set; }
        public int NhanVien { get; set; }
        public int DangKhoa { get; set; }
    }

    public class NguoiDungAdminItem
    {
        public int MaNguoiDung { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string SoDienThoai { get; set; }
        public string VaiTro { get; set; }
        public string AnhDaiDien { get; set; }
        public bool HoatDong { get; set; }
        public DateTime? KhoaDenNgay { get; set; }
        public DateTime? NgayTao { get; set; }
        public bool CanDoiMatKhau { get; set; }
        public int SoKhoaDaDangKy { get; set; }
        public int SoKhoaGiangDay { get; set; }
    }

    public class NguoiDungChiTiet
    {
        public int MaNguoiDung { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string SoDienThoai { get; set; }
        public string GioiTinh { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string VaiTro { get; set; }
        public string AnhDaiDien { get; set; }
        public bool HoatDong { get; set; }
        public string LyDoKhoa { get; set; }
        public DateTime? NgayKhoa { get; set; }
        public DateTime? KhoaDenNgay { get; set; }
        public DateTime? NgayTao { get; set; }
        public bool CanDoiMatKhau { get; set; }
        public string TenAdminKhoa { get; set; }

        public List<KhoaHocCuaHocVien> KhoaHoc { get; set; } = new List<KhoaHocCuaHocVien>();
        public List<DonHangCuaHocVien> DonHang { get; set; } = new List<DonHangCuaHocVien>();
        public List<DanhGiaCuaHocVien> DanhGia { get; set; } = new List<DanhGiaCuaHocVien>();
        public int SoChungChi { get; set; }
        public List<NhatKyQuanTriItem> NhatKy { get; set; } = new List<NhatKyQuanTriItem>();
    }

    public class KhoaHocCuaHocVien
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public DateTime? NgayDangKy { get; set; }
        public string TrangThaiKhoaHoc { get; set; }
        public int PhanTram { get; set; }
        public DateTime? LanHocGanNhat { get; set; }
    }

    public class DonHangCuaHocVien
    {
        public string MaDonHang { get; set; }
        public DateTime? NgayDat { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; }
        public int SoKhoa { get; set; }
    }

    public class DanhGiaCuaHocVien
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public int SoSao { get; set; }
        public string NoiDung { get; set; }
        public DateTime? NgayDanhGia { get; set; }
    }
}
