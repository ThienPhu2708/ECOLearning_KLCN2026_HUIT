using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>Giá bán hiệu lực của một khóa học tại một thời điểm (đã tính khuyến mãi "Giảm trực tiếp").</summary>
    public class GiaHieuLuc
    {
        /// <summary>Giá gốc trong CSDL (KhoaHoc.giaBan), không đổi.</summary>
        public decimal GiaGoc { get; set; }

        /// <summary>Giá bán sau giảm; bằng GiaGoc khi khóa không được giảm.</summary>
        public decimal Gia { get; set; }

        public decimal TienGiam => GiaGoc - Gia;
        public bool DangGiam => Gia < GiaGoc;

        /// <summary>% giảm thực tế sau khi áp trần giảm tối đa (làm tròn), không phải % nhập ở form.</summary>
        public int PhanTram => DangGiam && GiaGoc > 0 ? (int)Math.Round(TienGiam * 100m / GiaGoc, 0, MidpointRounding.AwayFromZero) : 0;

        public string MaKhuyenMai { get; set; }
        public string TenChuongTrinh { get; set; }
        public DateTime? NgayKetThuc { get; set; }
    }

    /// <summary>
    /// Khuyến mãi "Giảm trực tiếp" (KhuyenMai.loaiKhuyenMai = GiamTrucTiep, migration 021): không cần mã, tự áp dụng cho các khóa
    /// thuộc phạm vi. Đây là nơi duy nhất tính giá hiệu lực; web (thẻ khóa học, chi tiết, giỏ hàng), API ứng dụng, báo giá và tạo đơn
    /// đều gọi <see cref="Tinh(KhoaHoc)"/>, không nơi nào tự tính riêng. Chương trình hết hạn thì tự trả về giá gốc.
    /// Danh sách chương trình đang chạy chỉ đọc một lần cho mỗi request (HttpContext.Items) nên gọi cho từng thẻ khóa học không tốn truy vấn.
    /// Chưa chạy migration 021 thì không có chương trình nào: mọi khóa giữ giá gốc.
    /// </summary>
    public static class CourseDiscountService
    {
        public const string LoaiGiamTrucTiep = "GiamTrucTiep";
        public const string LoaiCoupon = "Coupon";

        private const string KhoaCache = "CourseDiscountService.ChuongTrinh";

        public class ChuongTrinhGiam
        {
            public string MaKhuyenMai { get; set; }
            public string TenKhuyenMai { get; set; }

            /// <summary>% (1–100) khi LoaiUuDai = PhanTram, số tiền VNĐ khi = SoTien.</summary>
            public double MucUuDai { get; set; }
            public string LoaiUuDai { get; set; }
            public decimal? GiamToiDa { get; set; }
            public string PhamVi { get; set; }
            public string MaDanhMucApDung { get; set; }
            public DateTime NgayBatDau { get; set; }
            public DateTime NgayKetThuc { get; set; }
            public HashSet<string> KhoaHoc { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private class DongChuongTrinh
        {
            public string MaKhuyenMai { get; set; }
            public string TenKhuyenMai { get; set; }
            public double MucUuDai { get; set; }
            public string LoaiUuDai { get; set; }
            public decimal? GiamToiDa { get; set; }
            public string PhamVi { get; set; }
            public string MaDanhMucApDung { get; set; }
            public DateTime NgayBatDau { get; set; }
            public DateTime NgayKetThuc { get; set; }
        }

        private class DongKhoa
        {
            public string MaKhuyenMai { get; set; }
            public string MaKhoaHoc { get; set; }
        }

        /// <summary>
        /// Chương trình giảm trực tiếp đang chạy: đang bật, trong thời gian áp dụng, không bị Admin tạm dừng và chiến dịch
        /// (nếu có) không Tạm dừng / Đã hủy.
        /// </summary>
        public static IReadOnlyList<ChuongTrinhGiam> DangChay()
        {
            var items = HttpContext.Current?.Items;
            if (items != null && items[KhoaCache] is List<ChuongTrinhGiam> daCo) return daCo;

            var ds = Doc();
            if (items != null) items[KhoaCache] = ds;
            return ds;
        }

        private static List<ChuongTrinhGiam> Doc()
        {
            try
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    var dong = db.Database.SqlQuery<DongChuongTrinh>(@"
                        SELECT km.maKhuyenMai AS MaKhuyenMai, km.tenKhuyenMai AS TenKhuyenMai, km.mucUuDai AS MucUuDai,
                               km.loaiUuDai AS LoaiUuDai, km.giamToiDa AS GiamToiDa, km.phamVi AS PhamVi,
                               km.maDanhMucApDung AS MaDanhMucApDung, km.ngayBatDau AS NgayBatDau, km.ngayKetThuc AS NgayKetThuc
                        FROM dbo.KhuyenMai km
                        LEFT JOIN dbo.ChienDich cd ON cd.maChienDich = km.maChienDich
                        WHERE km.loaiKhuyenMai = N'GiamTrucTiep' AND km.trangThai = N'HoatDong' AND ISNULL(km.adminTamDung, 0) = 0
                          AND km.ngayBatDau <= GETDATE() AND km.ngayKetThuc >= GETDATE()
                          AND (cd.maChienDich IS NULL OR cd.trangThai NOT IN (N'TamDung', N'DaHuy'))").ToList();
                    var ds = dong.Select(d => new ChuongTrinhGiam
                    {
                        MaKhuyenMai = d.MaKhuyenMai,
                        TenKhuyenMai = d.TenKhuyenMai,
                        MucUuDai = d.MucUuDai,
                        LoaiUuDai = d.LoaiUuDai,
                        GiamToiDa = d.GiamToiDa,
                        PhamVi = d.PhamVi,
                        MaDanhMucApDung = d.MaDanhMucApDung,
                        NgayBatDau = d.NgayBatDau,
                        NgayKetThuc = d.NgayKetThuc
                    }).ToList();

                    var canKhoa = ds.Where(p => p.PhamVi == "KhoaHoc").ToDictionary(p => p.MaKhuyenMai);
                    if (canKhoa.Count > 0)
                    {
                        foreach (var k in db.Database.SqlQuery<DongKhoa>(
                            "SELECT maKhuyenMai AS MaKhuyenMai, maKhoaHoc AS MaKhoaHoc FROM dbo.KhuyenMaiKhoaHoc").ToList())
                        {
                            ChuongTrinhGiam p;
                            if (canKhoa.TryGetValue(k.MaKhuyenMai, out p)) p.KhoaHoc.Add(k.MaKhoaHoc);
                        }
                    }
                    return ds;
                }
            }
            catch
            {
                // Chưa chạy migration 021 hoặc CSDL không đọc được: không giảm giá, giữ giá gốc
                return new List<ChuongTrinhGiam>();
            }
        }

        /// <summary>
        /// Lưu giá gốc và chương trình giảm trực tiếp vào từng dòng đơn (ChiTietDonHang.giaGoc / maKhuyenMaiTrucTiep) để đơn cũ không
        /// đổi khi chương trình bị sửa hay hết hạn. Gọi trong transaction tạo đơn; chưa chạy migration 021 thì bỏ qua.
        /// </summary>
        public static void GhiGiaGocDong(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, string maDonHang, IEnumerable<OrderService.DongBaoGia> dong)
        {
            bool coCot = db.Database.SqlQuery<int>(
                "SELECT CASE WHEN COL_LENGTH(N'dbo.ChiTietDonHang', N'maKhuyenMaiTrucTiep') IS NULL THEN 0 ELSE 1 END").First() == 1;
            if (!coCot) return;
            foreach (var d in dong)
            {
                db.Database.ExecuteSqlCommand(
                    "UPDATE dbo.ChiTietDonHang SET giaGoc = @goc, maKhuyenMaiTrucTiep = @km WHERE maDonHang = @dh AND maKhoaHoc = @kh",
                    new System.Data.SqlClient.SqlParameter("@goc", d.GiaGoc),
                    new System.Data.SqlClient.SqlParameter("@km", (object)d.MaKhuyenMaiTrucTiep ?? DBNull.Value),
                    new System.Data.SqlClient.SqlParameter("@dh", maDonHang),
                    new System.Data.SqlClient.SqlParameter("@kh", d.MaKhoaHoc));
            }
        }

        public static GiaHieuLuc Tinh(KhoaHoc k)
        {
            return Tinh(k.giaBan, k.maKhoaHoc, k.maDanhMuc);
        }

        public static GiaHieuLuc Tinh(decimal giaGoc, string maKhoaHoc, string maDanhMuc)
        {
            return Tinh(DangChay(), giaGoc, maKhoaHoc, maDanhMuc);
        }

        /// <summary>
        /// Giá hiệu lực của một khóa. Khóa miễn phí không áp dụng. Nhiều chương trình cùng hợp lệ thì lấy chương trình cho giá
        /// thấp nhất (bằng giá thì lấy chương trình kết thúc sớm hơn).
        /// </summary>
        public static GiaHieuLuc Tinh(IEnumerable<ChuongTrinhGiam> chuongTrinh, decimal giaGoc, string maKhoaHoc, string maDanhMuc)
        {
            var kq = new GiaHieuLuc { GiaGoc = giaGoc, Gia = giaGoc };
            if (giaGoc <= 0 || chuongTrinh == null) return kq;

            ChuongTrinhGiam tot = null;
            decimal giaTot = giaGoc;
            foreach (var p in chuongTrinh)
            {
                if (!ApDung(p, maKhoaHoc, maDanhMuc)) continue;
                decimal gia = TinhGia(giaGoc, p);
                if (gia < giaTot || (gia == giaTot && tot != null && p.NgayKetThuc < tot.NgayKetThuc))
                {
                    tot = p;
                    giaTot = gia;
                }
            }
            if (tot == null || giaTot >= giaGoc) return kq;

            kq.Gia = giaTot;
            kq.MaKhuyenMai = tot.MaKhuyenMai;
            kq.TenChuongTrinh = tot.TenKhuyenMai;
            kq.NgayKetThuc = tot.NgayKetThuc;
            return kq;
        }

        /// <summary>% giảm làm tròn từ giá gốc và giá bán (nhãn "-X%" trên web, cùng cách tính với app); 0 khi không giảm.</summary>
        public static int PhanTram(decimal giaGoc, decimal gia)
        {
            if (giaGoc <= 0 || gia >= giaGoc) return 0;
            return (int)Math.Round((giaGoc - gia) * 100m / giaGoc, 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Khóa nằm trong phạm vi chương trình: toàn sàn, danh mục hoặc danh sách khóa.</summary>
        public static bool ApDung(ChuongTrinhGiam p, string maKhoaHoc, string maDanhMuc)
        {
            switch (p.PhamVi)
            {
                case "KhoaHoc": return maKhoaHoc != null && p.KhoaHoc.Contains(maKhoaHoc);
                case "DanhMuc": return maDanhMuc != null && string.Equals(p.MaDanhMucApDung, maDanhMuc, StringComparison.OrdinalIgnoreCase);
                default: return true;
            }
        }

        /// <summary>
        /// Giảm %: min(giá gốc × % / 100, giảm tối đa) tính theo từng khóa. Giảm tiền: min(giá trị, giá gốc).
        /// Giá bán = max(0, giá gốc − giảm), làm tròn xuống đến đồng.
        /// </summary>
        public static decimal TinhGia(decimal giaGoc, ChuongTrinhGiam p)
        {
            decimal giam;
            if (p.LoaiUuDai == "SoTien")
            {
                giam = (decimal)p.MucUuDai;
            }
            else
            {
                giam = giaGoc * (decimal)p.MucUuDai / 100m;
                if (p.GiamToiDa.HasValue) giam = Math.Min(giam, p.GiamToiDa.Value);
            }
            giam = Math.Min(giam, giaGoc);
            return Math.Max(0m, Math.Floor(giaGoc - giam));
        }
    }
}
