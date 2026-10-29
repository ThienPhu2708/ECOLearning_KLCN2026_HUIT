using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Doanh thu theo khoảng ngày, một nguồn số tiền thống nhất:
    ///  - Chỉ tính đơn hàng thành công (DonHang.trangThai = "Đã thanh toán"; đơn chờ thanh toán / thất bại không tính).
    ///  - Lọc theo ngày đặt đơn (DonHang.ngayDat): CSDL không lưu thời điểm thanh toán riêng, đơn được ghi nhận
    ///    "Đã thanh toán" ngay khi thanh toán thành công.
    ///  - Trước giảm = tổng giá gốc các khóa (ChiTietDonHang.giaGoc, migration 021; đơn cũ không có thì lấy donGia);
    ///    giảm trực tiếp = giá gốc - donGia (khuyến mãi tự áp dụng, không cần mã); giảm coupon = SuDungUuDai.tienGiam;
    ///    thực thu = tổng donGia - giảm coupon. Không cộng thêm ThanhToan.soTien để tránh đếm trùng.
    ///  - Chia doanh thu: giáo viên TyLeGiaoVien (80%) thực thu, nền tảng giữ phần còn lại (20%). Tiền giảm do hai bên
    ///    cùng chịu theo tỷ lệ, giống trang Nhận thanh toán của giáo viên. Nền tảng = thực thu - phần giáo viên (tổng luôn khớp).
    ///  - Khoảng ngày lấy trọn ngày kết thúc: ngayDat >= tuNgay AND ngayDat &lt; denNgay + 1 ngày.
    /// CSDL chưa có trạng thái hủy / hoàn tiền nên chưa có bước trừ hoàn tiền.
    /// </summary>
    public class RevenueService
    {
        public static readonly string[] TrangThaiThanhCong = { "Đã thanh toán", "Hoàn tất", "Success" };

        /// <summary>Giáo viên nhận 80% doanh thu thực thu, nền tảng giữ 20%. Dùng chung cho Nhận thanh toán, Quản lý giáo viên, báo cáo doanh thu.</summary>
        public const decimal TyLeGiaoVien = 0.8m;

        /// <summary>Phần giáo viên của một khoản thực thu (làm tròn đến đồng).</summary>
        public static decimal PhanGiaoVien(decimal thucThu)
        {
            return Math.Round(thucThu * TyLeGiaoVien, 0);
        }

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public RevenueService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        private bool CoBangSuDungUuDai()
        {
            return db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.SuDungUuDai', N'U') IS NULL THEN 0 ELSE 1 END").First() == 1;
        }

        private bool CoCotGiaGoc()
        {
            return db.Database.SqlQuery<int>("SELECT CASE WHEN COL_LENGTH(N'dbo.ChiTietDonHang', N'giaGoc') IS NULL THEN 0 ELSE 1 END").First() == 1;
        }

        public List<DoanhThuDonHang> LayDonHang(DateTime tuNgay, DateTime denNgay)
        {
            string giam = CoBangSuDungUuDai()
                ? "ISNULL((SELECT SUM(s.tienGiam) FROM dbo.SuDungUuDai s WHERE s.maDonHang = d.maDonHang), 0)"
                : "0";
            // Giá gốc lưu từ migration 021; đơn cũ (giaGoc NULL) hoặc chưa chạy migration thì giá gốc = donGia
            string giaGoc = CoCotGiaGoc() ? "CASE WHEN c.giaGoc > c.donGia THEN c.giaGoc ELSE c.donGia END" : "c.donGia";
            string sql = @"
                SELECT d.maDonHang AS MaDonHang, d.ngayDat AS NgayDat, nd.hoTen AS HoTen, nd.email AS Email,
                       ISNULL(ct.soKhoa, 0) AS SoKhoaHoc,
                       CAST(ISNULL(ct.goc, 0) AS DECIMAL(18, 2)) AS TienTruocGiam,
                       CAST(ISNULL(ct.goc, 0) - ISNULL(ct.tien, 0) AS DECIMAL(18, 2)) AS TienGiamTrucTiep,
                       CAST(" + giam + @" AS DECIMAL(18, 2)) AS TienGiam,
                       CAST(0 AS DECIMAL(18, 2)) AS ThucThu,
                       CAST(0 AS DECIMAL(18, 2)) AS PhanGiaoVien, CAST(0 AS DECIMAL(18, 2)) AS NenTang,
                       mg.maCode AS MaCode
                FROM dbo.DonHang d
                INNER JOIN dbo.NguoiDung nd ON nd.maNguoiDung = d.maHocVien
                LEFT JOIN dbo.MaGiamGia mg ON mg.maGiamGia = d.maGiamGia
                OUTER APPLY (SELECT COUNT(*) AS soKhoa, SUM(c.donGia) AS tien, SUM(" + giaGoc + @") AS goc
                             FROM dbo.ChiTietDonHang c WHERE c.maDonHang = d.maDonHang) ct
                WHERE d.trangThai IN (N'Đã thanh toán', N'Hoàn tất', N'Success')
                  AND d.ngayDat >= @tu AND d.ngayDat < @denSau
                ORDER BY d.ngayDat DESC";

            var rows = db.Database.SqlQuery<DoanhThuDonHang>(sql,
                new SqlParameter("@tu", tuNgay.Date),
                new SqlParameter("@denSau", denNgay.Date.AddDays(1))).ToList();
            foreach (var r in rows)
            {
                r.ThucThu = Math.Max(0, r.TienTruocGiam - r.TienGiamTrucTiep - r.TienGiam);
                r.PhanGiaoVien = PhanGiaoVien(r.ThucThu);
                r.NenTang = r.ThucThu - r.PhanGiaoVien;
            }
            return rows;
        }

        public RevenueReportViewModel LapBaoCao(DateTime tuNgay, DateTime denNgay)
        {
            var model = new RevenueReportViewModel { TuNgay = tuNgay.Date, DenNgay = denNgay.Date };
            model.DonHang = LayDonHang(model.TuNgay, model.DenNgay);
            model.SoDonHang = model.DonHang.Count;
            model.TienTruocGiam = model.DonHang.Sum(d => d.TienTruocGiam);
            model.TienGiamTrucTiep = model.DonHang.Sum(d => d.TienGiamTrucTiep);
            model.TienGiam = model.DonHang.Sum(d => d.TienGiam);
            model.ThucThu = model.DonHang.Sum(d => d.ThucThu);
            model.PhanGiaoVien = model.DonHang.Sum(d => d.PhanGiaoVien);
            model.NenTang = model.DonHang.Sum(d => d.NenTang);

            // Mỗi ngày trong khoảng (kể cả ngày không có đơn = 0)
            var theoNgay = model.DonHang.GroupBy(d => d.NgayDat.Date).ToDictionary(g => g.Key, g => g.ToList());
            for (var ngay = model.TuNgay; ngay <= model.DenNgay; ngay = ngay.AddDays(1))
            {
                List<DoanhThuDonHang> ds;
                theoNgay.TryGetValue(ngay, out ds);
                model.TheoNgay.Add(new DoanhThuTheoNgay
                {
                    Ngay = ngay,
                    SoDon = ds?.Count ?? 0,
                    ThucThu = ds?.Sum(d => d.ThucThu) ?? 0,
                    NenTang = ds?.Sum(d => d.NenTang) ?? 0
                });
            }
            return model;
        }

        /// <summary>Tỷ lệ thực thu / trước giảm của từng đơn có coupon, để phân bổ tiền giảm về từng khóa trong đơn.</summary>
        public Dictionary<string, decimal> TyLeThucThuTheoDon()
        {
            if (!CoBangSuDungUuDai()) return new Dictionary<string, decimal>();
            return db.Database.SqlQuery<TyLeRow>(@"
                SELECT s.maDonHang AS MaDonHang,
                       CAST(CASE WHEN t.tien > 0 THEN (t.tien - s.tienGiam) / t.tien ELSE 1 END AS DECIMAL(18, 6)) AS TyLe
                FROM dbo.SuDungUuDai s
                CROSS APPLY (SELECT SUM(c.donGia) AS tien FROM dbo.ChiTietDonHang c WHERE c.maDonHang = s.maDonHang) t").ToList()
                .ToDictionary(r => r.MaDonHang, r => r.TyLe);
        }

        public class TyLeRow
        {
            public string MaDonHang { get; set; }
            public decimal TyLe { get; set; }
        }
    }
}
