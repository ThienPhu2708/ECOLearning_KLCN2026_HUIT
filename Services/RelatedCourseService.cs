using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Caching;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Hai mục cuối trang chi tiết khóa học, cùng là lọc cộng tác Item-based trên dữ liệu MUA (đơn hàng "Đã thanh toán"):
    ///
    ///   1. "Thường xuyên được mua cùng nhau": vector của khóa i là tập học viên đã mua i; hai khóa chỉ được tính là
    ///      mua chung khi cùng một học viên mua chúng cách nhau không quá <see cref="SoNgayMuaCungNhau"/> ngày.
    ///   2. "Học viên khác cũng mua": như trên nhưng tính trên toàn bộ lịch sử mua, cần ít nhất
    ///      <see cref="SoNguoiMuaChungToiThieu"/> học viên chung (giống min_common_users của job gợi ý). Bỏ các khóa đã có ở mục 1.
    ///
    ///   sim(i, j) = |U(i) ∩ U(j)| / sqrt(|U(i)| · |U(j)|)   (cosine trên vector nhị phân học viên đã mua)
    ///
    /// Danh sách giống nhau với mọi người xem (kể cả khách) — chỉ bỏ khóa người xem đã sở hữu.
    /// Ma trận dựng từ CSDL và cache 10 phút; dữ liệu đơn hàng nhỏ nên tính trực tiếp, không cần job riêng.
    /// </summary>
    public class RelatedCourseService
    {
        public const int SoNgayMuaCungNhau = 30;
        public const int SoNguoiMuaGanNhauToiThieu = 1;
        public const int SoNguoiMuaChungToiThieu = 2;
        public const int SoKhoaMoiMuc = 5;

        /// <summary>Số khóa đi kèm khóa đang xem trong combo "Thường xuyên được mua cùng nhau" (có nút thêm tất cả vào giỏ).</summary>
        public const int SoKhoaMuaCungNhau = 3;

        private const string CacheKey = "ECOLearning.MaTranMuaKhoaHoc";
        private static readonly object KhoaDung = new object();
        private static readonly string[] TrangThaiDangBan = { "Đã xuất bản", "Đã duyệt", "Active" };

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public RelatedCourseService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        private class DongMua
        {
            public int MaHocVien { get; set; }
            public string MaKhoaHoc { get; set; }
            public DateTime NgayMua { get; set; }
        }

        private class MaTranMua
        {
            /// <summary>Khóa → số học viên đã mua (|U(i)|).</summary>
            public Dictionary<string, int> SoNguoiMua = new Dictionary<string, int>();

            /// <summary>Khóa i → (khóa j → số học viên mua cả hai).</summary>
            public Dictionary<string, Dictionary<string, int>> MuaChung = new Dictionary<string, Dictionary<string, int>>();

            /// <summary>Như MuaChung nhưng hai lần mua cách nhau ≤ SoNgayMuaCungNhau ngày.</summary>
            public Dictionary<string, Dictionary<string, int>> MuaGanNhau = new Dictionary<string, Dictionary<string, int>>();
        }

        public static void XoaCache()
        {
            HttpRuntime.Cache.Remove(CacheKey);
        }

        private MaTranMua LayMaTran()
        {
            var mt = HttpRuntime.Cache[CacheKey] as MaTranMua;
            if (mt != null) return mt;
            lock (KhoaDung)
            {
                mt = HttpRuntime.Cache[CacheKey] as MaTranMua;
                if (mt != null) return mt;
                mt = DungMaTran();
                HttpRuntime.Cache.Insert(CacheKey, mt, null, DateTime.UtcNow.AddMinutes(10), Cache.NoSlidingExpiration);
                return mt;
            }
        }

        private static void Cong(Dictionary<string, Dictionary<string, int>> m, string a, string b)
        {
            Dictionary<string, int> dong;
            if (!m.TryGetValue(a, out dong)) { dong = new Dictionary<string, int>(); m[a] = dong; }
            int n;
            dong.TryGetValue(b, out n);
            dong[b] = n + 1;
        }

        private MaTranMua DungMaTran()
        {
            // Mỗi (học viên, khóa) lấy lần mua đầu tiên; một học viên mua lại khóa (nếu có) không tính hai lần
            var mua = db.Database.SqlQuery<DongMua>(@"
                SELECT d.maHocVien AS MaHocVien, c.maKhoaHoc AS MaKhoaHoc, MIN(ISNULL(d.ngayDat, GETDATE())) AS NgayMua
                FROM dbo.DonHang d
                INNER JOIN dbo.ChiTietDonHang c ON c.maDonHang = d.maDonHang
                WHERE d.trangThai = N'Đã thanh toán'
                GROUP BY d.maHocVien, c.maKhoaHoc").ToList();

            var mt = new MaTranMua();
            foreach (var g in mua.GroupBy(x => x.MaKhoaHoc))
            {
                mt.SoNguoiMua[g.Key] = g.Count();
            }
            foreach (var hv in mua.GroupBy(x => x.MaHocVien))
            {
                var ds = hv.ToList();
                for (int i = 0; i < ds.Count; i++)
                {
                    for (int j = i + 1; j < ds.Count; j++)
                    {
                        string a = ds[i].MaKhoaHoc, b = ds[j].MaKhoaHoc;
                        Cong(mt.MuaChung, a, b);
                        Cong(mt.MuaChung, b, a);
                        if (Math.Abs((ds[i].NgayMua - ds[j].NgayMua).TotalDays) <= SoNgayMuaCungNhau)
                        {
                            Cong(mt.MuaGanNhau, a, b);
                            Cong(mt.MuaGanNhau, b, a);
                        }
                    }
                }
            }
            return mt;
        }

        /// <summary>Các khóa j xếp theo cosine với khóa i, chỉ giữ cặp có từ <paramref name="toiThieu"/> học viên chung.</summary>
        private static List<Tuple<string, int, double>> XepHang(MaTranMua mt, Dictionary<string, Dictionary<string, int>> maTran, string i, int toiThieu)
        {
            Dictionary<string, int> dong;
            if (!maTran.TryGetValue(i, out dong)) return new List<Tuple<string, int, double>>();
            int ni = mt.SoNguoiMua.ContainsKey(i) ? mt.SoNguoiMua[i] : 0;
            return dong.Where(x => x.Value >= toiThieu)
                .Select(x =>
                {
                    int nj = mt.SoNguoiMua.ContainsKey(x.Key) ? mt.SoNguoiMua[x.Key] : 0;
                    double sim = ni > 0 && nj > 0 ? x.Value / Math.Sqrt((double)ni * nj) : 0;
                    return Tuple.Create(x.Key, x.Value, sim);
                })
                .OrderByDescending(x => x.Item3).ThenByDescending(x => x.Item2).ThenBy(x => x.Item1)
                .ToList();
        }

        public KhoaHocLienQuanViewModel LayChoKhoaHoc(string maKhoaHoc, int userId)
        {
            var vm = new KhoaHocLienQuanViewModel();
            var mt = LayMaTran();

            // Khóa không gợi ý: khóa đang xem, khóa người xem đã đăng ký, khóa do chính họ dạy, khóa không còn bán
            var daSoHuu = userId > 0
                ? new HashSet<string>(db.DangKyKhoaHoc.Where(d => d.maHocVien == userId).Select(d => d.maKhoaHoc).ToList())
                : new HashSet<string>();
            var ungVien = XepHang(mt, mt.MuaGanNhau, maKhoaHoc, SoNguoiMuaGanNhauToiThieu)
                .Concat(XepHang(mt, mt.MuaChung, maKhoaHoc, SoNguoiMuaChungToiThieu))
                .Select(x => x.Item1).Distinct().ToList();
            if (ungVien.Count == 0) return vm;

            Func<KhoaHoc, bool> muaDuoc = k => !daSoHuu.Contains(k.maKhoaHoc) && (userId <= 0 || k.maGiaoVien != userId);
            var canNap = ungVien.Concat(new[] { maKhoaHoc }).ToList();
            var tatCa = db.KhoaHoc.Include(k => k.NguoiDung).Include(k => k.DanhMuc)
                .Where(k => canNap.Contains(k.maKhoaHoc) && TrangThaiDangBan.Contains(k.trangThai))
                .ToList()
                .ToDictionary(k => k.maKhoaHoc);
            var khoa = tatCa.Values.Where(k => k.maKhoaHoc != maKhoaHoc && muaDuoc(k)).ToDictionary(k => k.maKhoaHoc);
            if (khoa.Count == 0) return vm;

            var ma = tatCa.Keys.ToList();
            var soHocVien = db.DangKyKhoaHoc.Where(d => ma.Contains(d.maKhoaHoc)).GroupBy(d => d.maKhoaHoc)
                .Select(g => new { g.Key, SoLuong = g.Count() }).ToDictionary(x => x.Key, x => x.SoLuong);
            var danhGia = db.DanhGiaKhoaHoc.Where(d => ma.Contains(d.maKhoaHoc)).GroupBy(d => d.maKhoaHoc)
                .Select(g => new { g.Key, SoLuong = g.Count(), TrungBinh = g.Average(x => (double)x.soSao) }).ToDictionary(x => x.Key);

            // Tổng thời lượng bài học (bài trong chương + bài chưa xếp chương, mã "<maKhoaHoc>_...") và lần duyệt gần nhất
            var thoiLuong = (from b in db.BaiHoc
                             join c in db.ChuongHoc on b.maChuong equals c.maChuong
                             where ma.Contains(c.maKhoaHoc)
                             group b by c.maKhoaHoc into g
                             select new { g.Key, Giay = g.Sum(x => x.thoiLuong) ?? 0 })
                .ToDictionary(x => x.Key, x => x.Giay);
            foreach (var m in ma)
            {
                string tienTo = m + "_";
                int ngoaiChuong = db.BaiHoc.Where(b => b.maChuong == null && b.maBaiHoc.StartsWith(tienTo)).Sum(b => b.thoiLuong) ?? 0;
                if (ngoaiChuong > 0) thoiLuong[m] = (thoiLuong.ContainsKey(m) ? thoiLuong[m] : 0) + ngoaiChuong;
            }
            var capNhat = db.KiemDuyetKhoaHoc.Where(k => ma.Contains(k.maKhoaHoc) && k.ngayKiemDuyet != null)
                .GroupBy(k => k.maKhoaHoc)
                .Select(g => new { g.Key, Ngay = g.Max(x => x.ngayKiemDuyet) })
                .ToDictionary(x => x.Key, x => x.Ngay);

            Func<Tuple<string, int, double>, KhoaHocLienQuanItem> taoThe = x => new KhoaHocLienQuanItem
            {
                SoNguoiChung = x.Item2,
                DoTuongDong = x.Item3,
                ThoiLuongGiay = thoiLuong.ContainsKey(x.Item1) ? thoiLuong[x.Item1] : 0,
                NgayCapNhat = capNhat.ContainsKey(x.Item1) ? capNhat[x.Item1] : null,
                The = new CourseCardViewModel
                {
                    Course = tatCa[x.Item1],
                    SoHocVien = soHocVien.ContainsKey(x.Item1) ? soHocVien[x.Item1] : 0,
                    SoDanhGia = danhGia.ContainsKey(x.Item1) ? danhGia[x.Item1].SoLuong : 0,
                    DiemDanhGia = danhGia.ContainsKey(x.Item1) ? danhGia[x.Item1].TrungBinh : 0
                }
            };

            vm.MuaCungNhau = XepHang(mt, mt.MuaGanNhau, maKhoaHoc, SoNguoiMuaGanNhauToiThieu)
                .Where(x => khoa.ContainsKey(x.Item1))
                .Take(SoKhoaMuaCungNhau).Select(taoThe).ToList();
            // Combo bắt đầu bằng khóa đang xem (nếu người xem còn mua được khóa đó)
            KhoaHoc dangXem;
            if (vm.MuaCungNhau.Count > 0 && tatCa.TryGetValue(maKhoaHoc, out dangXem) && muaDuoc(dangXem))
            {
                vm.KhoaDangXem = taoThe(Tuple.Create(maKhoaHoc, 0, 1.0));
            }
            var daHien = new HashSet<string>(vm.MuaCungNhau.Select(x => x.The.Course.maKhoaHoc));
            vm.CungMua = XepHang(mt, mt.MuaChung, maKhoaHoc, SoNguoiMuaChungToiThieu)
                .Where(x => khoa.ContainsKey(x.Item1) && !daHien.Contains(x.Item1))
                .Take(SoKhoaMoiMuc).Select(taoThe).ToList();
            return vm;
        }
    }
}
