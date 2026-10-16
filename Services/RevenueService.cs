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
    ///  - Trước giảm = tổng đơn giá các khóa (ChiTietDonHang.donGia); giảm = SuDungUuDai.tienGiam;
    ///    thực thu = trước giảm - giảm. Không cộng thêm ThanhToan.soTien để tránh đếm trùng.
    ///  - Khoảng ngày lấy trọn ngày kết thúc: ngayDat >= tuNgay AND ngayDat &lt; denNgay + 1 ngày.
    /// CSDL chưa có trạng thái hủy / hoàn tiền nên chưa có bước trừ hoàn tiền.
    /// </summary>
    public class RevenueService
    {
        public static readonly string[] TrangThaiThanhCong = { "Đã thanh toán", "Hoàn tất", "Success" };

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public RevenueService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        private bool CoBangSuDungUuDai()
        {
            return db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.SuDungUuDai', N'U') IS NULL THEN 0 ELSE 1 END").First() == 1;
        }

        public List<DoanhThuDonHang> LayDonHang(DateTime tuNgay, DateTime denNgay)
        {
            string giam = CoBangSuDungUuDai()
                ? "ISNULL((SELECT SUM(s.tienGiam) FROM dbo.SuDungUuDai s WHERE s.maDonHang = d.maDonHang), 0)"
                : "0";
            string sql = @"
                SELECT d.maDonHang AS MaDonHang, d.ngayDat AS NgayDat, nd.hoTen AS HoTen, nd.email AS Email,
                       ISNULL(ct.soKhoa, 0) AS SoKhoaHoc,
                       CAST(ISNULL(ct.tien, 0) AS DECIMAL(18, 2)) AS TienTruocGiam,
                       CAST(" + giam + @" AS DECIMAL(18, 2)) AS TienGiam,
                       CAST(0 AS DECIMAL(18, 2)) AS ThucThu,
                       mg.maCode AS MaCode
                FROM dbo.DonHang d
                INNER JOIN dbo.NguoiDung nd ON nd.maNguoiDung = d.maHocVien
                LEFT JOIN dbo.MaGiamGia mg ON mg.maGiamGia = d.maGiamGia
                OUTER APPLY (SELECT COUNT(*) AS soKhoa, SUM(c.donGia) AS tien FROM dbo.ChiTietDonHang c WHERE c.maDonHang = d.maDonHang) ct
                WHERE d.trangThai IN (N'Đã thanh toán', N'Hoàn tất', N'Success')
                  AND d.ngayDat >= @tu AND d.ngayDat < @denSau
                ORDER BY d.ngayDat DESC";

            var rows = db.Database.SqlQuery<DoanhThuDonHang>(sql,
                new SqlParameter("@tu", tuNgay.Date),
                new SqlParameter("@denSau", denNgay.Date.AddDays(1))).ToList();
            foreach (var r in rows)
            {
                r.ThucThu = Math.Max(0, r.TienTruocGiam - r.TienGiam);
            }
            return rows;
        }

        public RevenueReportViewModel LapBaoCao(DateTime tuNgay, DateTime denNgay)
        {
            var model = new RevenueReportViewModel { TuNgay = tuNgay.Date, DenNgay = denNgay.Date };
            model.DonHang = LayDonHang(model.TuNgay, model.DenNgay);
            model.SoDonHang = model.DonHang.Count;
            model.TienTruocGiam = model.DonHang.Sum(d => d.TienTruocGiam);
            model.TienGiam = model.DonHang.Sum(d => d.TienGiam);
            model.ThucThu = model.DonHang.Sum(d => d.ThucThu);

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
                    ThucThu = ds?.Sum(d => d.ThucThu) ?? 0
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
