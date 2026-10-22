using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Ví mã phía học viên (đặc tả mục 8): mã công khai để thu thập, ví "Mã giảm giá của tôi", chọn mã ở giỏ hàng, trang chi tiết mã.
    /// Ví chỉ là nơi giữ mã, không thay luật: mã trong ví vẫn phải qua KiemTraCoupon khi áp dụng và khi thanh toán.
    /// Lưu mã không giữ chỗ (mã giới hạn tổng lượt tính theo ai thanh toán trước); mã đã lưu không xóa được khỏi ví.
    /// </summary>
    public partial class PromotionService
    {
        public class LuotDungTheoMa
        {
            public string MaGiamGia { get; set; }
            public int SoLuot { get; set; }
        }

        public class DongVi
        {
            public string MaGiamGia { get; set; }
            public DateTime NgayLuu { get; set; }
        }

        /// <summary>Số ngày còn lại để gắn nhãn "Sắp hết hạn".</summary>
        private const int SoNgaySapHetHan = 3;

        /// <summary>
        /// Mã còn lưu được về trạng thái: Đang chạy hoặc Chưa bắt đầu (chưa kết thúc, đang bật) và chiến dịch không Tạm dừng / Đã hủy.
        /// Hết lượt toàn chương trình lọc sau (số lượt tính trong SelectChuongTrinh).
        /// </summary>
        private const string DieuKienLuuDuoc = @"
            km.trangThai = N'HoatDong' AND mg.maCode IS NOT NULL AND ISNULL(mg.trangThai, 1) = 1
            AND km.ngayKetThuc >= @now
            AND (cd.trangThai IS NULL OR cd.trangThai NOT IN (N'TamDung', N'DaHuy'))";

        /// <summary>Số lần học viên đã dùng từng mã (chỉ đơn đã thanh toán).</summary>
        private Dictionary<string, int> LuotDungCuaHocVien(int maHocVien)
        {
            if (maHocVien <= 0) return new Dictionary<string, int>();
            return db.Database.SqlQuery<LuotDungTheoMa>(@"
                SELECT s.maGiamGia AS MaGiamGia, COUNT(*) AS SoLuot
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai IN (" + DonDaTraSql + @")
                WHERE s.maHocVien = @hv
                GROUP BY s.maGiamGia",
                new SqlParameter("@hv", maHocVien)).ToDictionary(x => x.MaGiamGia, x => x.SoLuot);
        }

        private List<DongVi> LayDongVi(int maHocVien)
        {
            if (maHocVien <= 0) return new List<DongVi>();
            return db.Database.SqlQuery<DongVi>(
                "SELECT maGiamGia AS MaGiamGia, ngayLuu AS NgayLuu FROM dbo.ViMaGiamGia WHERE maHocVien = @hv",
                new SqlParameter("@hv", maHocVien)).ToList();
        }

        /// <summary>"50K", "1,5TR" cho cuống vé.</summary>
        public static string SoTienNgan(decimal v)
        {
            if (v >= 1000000 && v % 100000 == 0) return (v / 1000000m).ToString("0.#") + "TR";
            if (v >= 1000 && v % 1000 == 0) return (v / 1000m).ToString("0") + "K";
            return v.ToString("N0") + "đ";
        }

        public static string GiaTriNgan(ChuongTrinhKhuyenMai p)
        {
            return p.LaPhanTram ? p.MucUuDai.ToString("0.##") + "%" : SoTienNgan(p.SoTienGiam);
        }

        /// <summary>Điều kiện ngắn trên thẻ mã: phạm vi, đơn tối thiểu, số lượt mỗi người, tiêu chí thân thiết.</summary>
        public static List<string> DieuKienNgan(ChuongTrinhKhuyenMai p)
        {
            var dieuKien = new List<string> { p.MoTaPhamVi };
            if (p.DonHangToiThieu.HasValue && p.DonHangToiThieu.Value > 0)
                dieuKien.Add("Đơn từ " + p.DonHangToiThieu.Value.ToString("N0") + " đ");
            if (p.GioiHanMoiHocVien.HasValue) dieuKien.Add(p.GioiHanMoiHocVien.Value + " lần / học viên");
            if (p.LaThanThiet && p.MoTaTieuChiThanThiet.Length > 0) dieuKien.Add("Học viên thân thiết: " + p.MoTaTieuChiThanThiet);
            return dieuKien;
        }

        /// <summary>Thẻ hiển thị một chương trình (chưa có số tiền giảm theo giỏ).</summary>
        public static MaGiamGiaHienThi TaoThe(ChuongTrinhKhuyenMai p, DateTime now)
        {
            bool dangChay = p.TrangThaiHienThi(now) == "Đang chạy";
            return new MaGiamGiaHienThi
            {
                MaKhuyenMai = p.MaKhuyenMai,
                MaGiamGia = p.MaGiamGia,
                MaCode = p.MaCode,
                TenKhuyenMai = p.TenKhuyenMai,
                MoTaUuDai = p.MoTaUuDai,
                GiaTriNgan = GiaTriNgan(p),
                GhiChuGiaTri = p.LaPhanTram && p.GiamToiDa.HasValue ? "Tối đa " + SoTienNgan(p.GiamToiDa.Value) : null,
                DieuKien = DieuKienNgan(p),
                NgayBatDau = p.NgayBatDau,
                NgayKetThuc = p.NgayKetThuc,
                ConLai = p.GioiHanLuotDung.HasValue ? Math.Max(0, p.GioiHanLuotDung.Value - p.SoLuotDaDung) : (int?)null,
                LaThanThiet = p.LaThanThiet,
                CongKhai = p.CongKhai,
                ChuaBatDau = !p.DaKetThuc(now) && p.NgayBatDau > now,
                SapHetHan = dangChay && p.NgayKetThuc <= now.AddDays(SoNgaySapHetHan),
                CoTheApDung = true
            };
        }

        /// <summary>
        /// Lý do mã không lưu vào ví được (đặc tả mục 8, bước 3–5); null = lưu được. Không xét mã công khai / riêng và đã có trong ví.
        /// </summary>
        private static string LyDoKhongLuuDuoc(ChuongTrinhKhuyenMai p, HocVienThanThietItem hv, int luotCuaToi, DateTime now)
        {
            if (p.DaKetThuc(now)) return "Mã khuyến mãi đã kết thúc!";
            if (p.DangTamNgung) return "Mã khuyến mãi đang tạm ngưng áp dụng!";
            if (p.HetLuot) return "Mã khuyến mãi đã hết lượt sử dụng!";
            if (p.ChienDichChan) return p.LyDoChienDichChan;
            if (p.LaThanThiet && !DatTieuChi(p, hv)) return "Ưu đãi dành cho học viên thân thiết. Bạn cần " + PhanConThieu(p, hv) + "!";
            if (p.GioiHanMoiHocVien.HasValue && luotCuaToi >= p.GioiHanMoiHocVien.Value)
                return "Bạn đã dùng mã này " + luotCuaToi + " lần (tối đa " + p.GioiHanMoiHocVien.Value + " lần mỗi học viên)!";
            return null;
        }

        /// <summary>
        /// <paramref name="chiTrongVi"/> = false: mã công khai lưu được (trang chủ, trang khóa học, tab "Thu thập mã"), có cờ DaLuu.
        /// Chưa đăng nhập thì không có mã thân thiết.
        /// <paramref name="chiTrongVi"/> = true: mã trong ví thuộc tab "Có thể dùng" (giỏ hàng), kể cả mã sau đó bị chuyển thành mã riêng.
        /// Có <paramref name="gioHang"/> thì chạy KiemTraCoupon cho từng mã: mã dùng được có số tiền giảm, xếp giảm nhiều nhất lên đầu;
        /// mã chưa dùng được kèm lý do. <paramref name="maKhoaHoc"/> chỉ lấy mã áp dụng được cho khóa đó.
        /// </summary>
        public List<MaGiamGiaHienThi> LayMaChoHocVien(int maHocVien, IList<DongGioHang> gioHang = null, string maKhoaHoc = null, bool chiTrongVi = false)
        {
            var ketQua = new List<MaGiamGiaHienThi>();
            if (chiTrongVi && maHocVien <= 0) return ketQua;

            string nguon = chiTrongVi
                ? "EXISTS (SELECT 1 FROM dbo.ViMaGiamGia v WHERE v.maGiamGia = mg.maGiamGia AND v.maHocVien = @hv)"
                : "km.congKhai = 1";
            var now = DateTime.Now;
            var chuongTrinh = db.Database.SqlQuery<ChuongTrinhKhuyenMai>(SelectChuongTrinh + @"
                WHERE " + nguon + " AND " + DieuKienLuuDuoc + @"
                  AND (@kh IS NULL OR km.phamVi = N'TatCa'
                       OR (km.phamVi = N'DanhMuc' AND km.maDanhMucApDung = (SELECT kh.maDanhMuc FROM dbo.KhoaHoc kh WHERE kh.maKhoaHoc = @kh))
                       OR (km.phamVi = N'KhoaHoc' AND EXISTS (SELECT 1 FROM dbo.KhuyenMaiKhoaHoc k WHERE k.maKhuyenMai = km.maKhuyenMai AND k.maKhoaHoc = @kh)))
                ORDER BY km.ngayKetThuc",
                new SqlParameter("@now", now),
                new SqlParameter("@hv", maHocVien),
                new SqlParameter("@kh", SqlDbType.VarChar, 20) { Value = (object)maKhoaHoc ?? DBNull.Value })
                .Where(p => !p.HetLuot)
                .ToList();
            if (chuongTrinh.Count == 0) return ketQua;

            HocVienThanThietItem hv = ThongKeHocVien(maHocVien);
            var daDung = LuotDungCuaHocVien(maHocVien);
            var daLuu = new HashSet<string>(LayDongVi(maHocVien).Select(v => v.MaGiamGia));

            foreach (var p in chuongTrinh)
            {
                int luotCuaToi;
                daDung.TryGetValue(p.MaGiamGia, out luotCuaToi);
                if (LyDoKhongLuuDuoc(p, hv, luotCuaToi, now) != null) continue;

                var ma = TaoThe(p, now);
                ma.DaLuu = chiTrongVi || daLuu.Contains(p.MaGiamGia);
                if (gioHang != null && gioHang.Count > 0)
                {
                    var kq = KiemTraCoupon(maHocVien, p.MaCode, gioHang);
                    ma.CoTheApDung = kq.Success;
                    ma.TienGiam = kq.Success ? kq.TienGiam : 0;
                    ma.LyDo = kq.Success ? null : kq.Message;
                }
                ketQua.Add(ma);
            }

            return ketQua
                .OrderByDescending(m => m.CoTheApDung)
                .ThenByDescending(m => m.TienGiam)
                .ThenBy(m => m.ChuaBatDau)
                .ThenBy(m => m.NgayKetThuc)
                .ToList();
        }

        /// <summary>
        /// Khối "Ưu đãi thân thiết sắp mở": mã thân thiết công khai còn lưu được mà học viên chưa đạt tiêu chí, kèm tiến độ.
        /// Không trả mã code. Chưa đăng nhập thì rỗng.
        /// </summary>
        public List<UuDaiThanThietSapMo> LayThanThietSapMo(int maHocVien)
        {
            var ketQua = new List<UuDaiThanThietSapMo>();
            if (maHocVien <= 0) return ketQua;
            var now = DateTime.Now;
            var chuongTrinh = db.Database.SqlQuery<ChuongTrinhKhuyenMai>(SelectChuongTrinh + @"
                WHERE km.congKhai = 1 AND km.doiTuong = N'ThanThiet' AND " + DieuKienLuuDuoc + @"
                ORDER BY km.ngayKetThuc",
                new SqlParameter("@now", now)).Where(p => !p.HetLuot).ToList();
            if (chuongTrinh.Count == 0) return ketQua;

            var hv = ThongKeHocVien(maHocVien);
            foreach (var p in chuongTrinh.Where(p => !DatTieuChi(p, hv)))
            {
                string thieu = PhanConThieu(p, hv);
                ketQua.Add(new UuDaiThanThietSapMo
                {
                    TenKhuyenMai = p.TenKhuyenMai,
                    GiaTriNgan = GiaTriNgan(p),
                    MoTaUuDai = p.MoTaUuDai,
                    TienDo = "Cần " + thieu
                });
            }
            return ketQua;
        }

        /// <summary>Trạng thái của một mã trong ví (đặc tả mục 8, các tab trong ví).</summary>
        private static void GanTrangThai(MaTrongVi v, ChuongTrinhKhuyenMai p, DateTime now)
        {
            if (v.GioiHanMoiHocVien.HasValue && v.LuotCuaToi >= v.GioiHanMoiHocVien.Value) { v.TrangThai = "DaDung"; v.NhanTrangThai = "Đã dùng"; }
            else if (p.TrangThaiLuu == KetThuc) { v.TrangThai = "HetHieuLuc"; v.NhanTrangThai = "Đã kết thúc"; }
            else if (p.NgayKetThuc < now) { v.TrangThai = "HetHieuLuc"; v.NhanTrangThai = "Hết hạn"; }
            else if (p.DangTamNgung) { v.TrangThai = "HetHieuLuc"; v.NhanTrangThai = "Tạm ngưng"; }
            else if (p.HetLuot) { v.TrangThai = "HetHieuLuc"; v.NhanTrangThai = "Hết lượt"; }
            else if (p.TrangThaiChienDich == "TamDung") { v.TrangThai = "HetHieuLuc"; v.NhanTrangThai = "Chiến dịch tạm dừng"; }
            else if (p.TrangThaiChienDich == "DaHuy") { v.TrangThai = "HetHieuLuc"; v.NhanTrangThai = "Chiến dịch đã hủy"; }
            else if (p.NgayBatDau > now) { v.TrangThai = "SapDienRa"; v.NhanTrangThai = "Chưa bắt đầu"; }
            else { v.TrangThai = "CoTheDung"; v.NhanTrangThai = v.Ma.SapHetHan ? "Sắp hết hạn" : "Có thể dùng"; }
        }

        /// <summary>Đơn đã thanh toán của học viên có dùng các mã trong ví (lịch sử ở tab "Đã dùng").</summary>
        private Dictionary<string, List<LuotSuDungUuDai>> LichSuDungMa(int maHocVien)
        {
            return db.Database.SqlQuery<LichSuMa>(@"
                SELECT s.maGiamGia AS MaGiamGia, s.maDonHang AS MaDonHang, ISNULL(d.ngayThanhToan, s.ngaySuDung) AS NgaySuDung,
                       s.tienTruocGiam AS TienTruocGiam, s.tienGiam AS TienGiam, d.tongTien AS TongDonHang
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai IN (" + DonDaTraSql + @")
                WHERE s.maHocVien = @hv
                ORDER BY NgaySuDung DESC",
                new SqlParameter("@hv", maHocVien)).ToList()
                .GroupBy(x => x.MaGiamGia)
                .ToDictionary(g => g.Key, g => g.Select(x => new LuotSuDungUuDai
                {
                    MaDonHang = x.MaDonHang,
                    NgaySuDung = x.NgaySuDung,
                    TienTruocGiam = x.TienTruocGiam,
                    TienGiam = x.TienGiam,
                    TongDonHang = x.TongDonHang
                }).ToList());
        }

        public class LichSuMa
        {
            public string MaGiamGia { get; set; }
            public string MaDonHang { get; set; }
            public DateTime NgaySuDung { get; set; }
            public decimal TienTruocGiam { get; set; }
            public decimal TienGiam { get; set; }
            public decimal TongDonHang { get; set; }
        }

        /// <summary>
        /// Ví "Mã giảm giá của tôi": Có thể dùng (đang chạy trước, hạn gần trước) / Đã dùng (kèm lịch sử đơn) / Hết hiệu lực (kèm lý do)
        /// / Thu thập mã (mã công khai lưu được chưa có trong ví + Ưu đãi thân thiết sắp mở).
        /// </summary>
        public ViMaGiamGiaViewModel LayViCuaToi(int maHocVien, string tab)
        {
            var vm = new ViMaGiamGiaViewModel
            {
                Tab = new[] { "codung", "dadung", "hethan", "thuthap" }.Contains(tab) ? tab : "codung"
            };
            var dongVi = LayDongVi(maHocVien).ToDictionary(v => v.MaGiamGia, v => v.NgayLuu);
            if (dongVi.Count > 0)
            {
                var chuongTrinh = db.Database.SqlQuery<ChuongTrinhKhuyenMai>(SelectChuongTrinh + @"
                    WHERE EXISTS (SELECT 1 FROM dbo.ViMaGiamGia v WHERE v.maGiamGia = mg.maGiamGia AND v.maHocVien = @hv)",
                    new SqlParameter("@hv", maHocVien)).ToList();
                var daDung = LuotDungCuaHocVien(maHocVien);
                var lichSu = LichSuDungMa(maHocVien);
                var now = DateTime.Now;
                foreach (var p in chuongTrinh)
                {
                    int luot;
                    daDung.TryGetValue(p.MaGiamGia, out luot);
                    var v = new MaTrongVi
                    {
                        Ma = TaoThe(p, now),
                        NgayLuu = dongVi.ContainsKey(p.MaGiamGia) ? dongVi[p.MaGiamGia] : now,
                        LuotCuaToi = luot,
                        GioiHanMoiHocVien = p.GioiHanMoiHocVien
                    };
                    v.Ma.DaLuu = true;
                    List<LuotSuDungUuDai> ls;
                    if (lichSu.TryGetValue(p.MaGiamGia, out ls)) v.LichSu = ls;
                    GanTrangThai(v, p, now);
                    if (v.TrangThai == "DaDung") vm.DaDung.Add(v);
                    else if (v.TrangThai == "HetHieuLuc") vm.HetHieuLuc.Add(v);
                    else vm.CoTheDung.Add(v);
                }
                vm.CoTheDung = vm.CoTheDung.OrderBy(v => v.TrangThai == "SapDienRa").ThenBy(v => v.Ma.NgayKetThuc).ToList();
                vm.DaDung = vm.DaDung.OrderByDescending(v => v.LichSu.Select(l => l.NgaySuDung).DefaultIfEmpty(v.NgayLuu).Max()).ToList();
                vm.HetHieuLuc = vm.HetHieuLuc.OrderByDescending(v => v.Ma.NgayKetThuc).ToList();
            }
            vm.CoTheThuThap = LayMaChoHocVien(maHocVien).Where(m => !m.DaLuu).ToList();
            vm.ThanThietSapMo = LayThanThietSapMo(maHocVien);
            return vm;
        }

        /// <summary>
        /// Lưu mã vào ví (đặc tả mục 8). <paramref name="tuDanhSach"/> = true: bấm "Lưu mã" trong danh sách, chỉ lưu được mã công khai;
        /// false: ô "Nhập mã" trong ví / API, lưu được cả mã riêng (học viên đã biết mã). Không có thao tác xóa / hoàn tác.
        /// </summary>
        public KetQuaLuuMa LuuMa(int maHocVien, string maCode, bool tuDanhSach = true)
        {
            Func<string, KetQuaLuuMa> loi = m => new KetQuaLuuMa { Success = false, Message = m };
            if (maHocVien <= 0) return loi("Vui lòng đăng nhập để lưu mã giảm giá.");
            if (string.IsNullOrWhiteSpace(maCode)) return loi("Vui lòng nhập mã khuyến mãi!");

            var p = LayTheoCode(maCode);
            if (p == null) return loi("Mã khuyến mãi không tồn tại!");
            if (tuDanhSach && !p.CongKhai) return loi("Mã này không mở để lưu từ danh sách. Hãy nhập mã ở ô \"Nhập mã\" trong ví.");

            if (LayDongVi(maHocVien).Any(v => v.MaGiamGia == p.MaGiamGia))
                return new KetQuaLuuMa { Success = true, DaCoTrongVi = true, Message = "Mã " + p.MaCode + " đã có trong ví của bạn." };

            string lyDo = LyDoKhongLuuDuoc(p, p.LaThanThiet ? ThongKeHocVien(maHocVien) : null,
                p.GioiHanMoiHocVien.HasValue ? LuotDungCuaHocVien(maHocVien, p.MaGiamGia) : 0, DateTime.Now);
            if (lyDo != null) return loi(lyDo);

            db.Database.ExecuteSqlCommand(@"
                IF NOT EXISTS (SELECT 1 FROM dbo.ViMaGiamGia WHERE maHocVien = @hv AND maGiamGia = @mgg)
                    INSERT INTO dbo.ViMaGiamGia (maHocVien, maGiamGia) VALUES (@hv, @mgg)",
                new SqlParameter("@hv", maHocVien), new SqlParameter("@mgg", p.MaGiamGia));
            return new KetQuaLuuMa { Success = true, Message = "Đã lưu mã " + p.MaCode + " vào ví." };
        }

        /// <summary>Thẻ mã của một mã trong ví hoặc vừa lưu (dùng cho phản hồi API).</summary>
        public MaTrongVi LayMaTrongVi(int maHocVien, string maCode)
        {
            var p = LayTheoCode(maCode);
            if (p == null) return null;
            var now = DateTime.Now;
            var dong = LayDongVi(maHocVien).FirstOrDefault(v => v.MaGiamGia == p.MaGiamGia);
            var v2 = new MaTrongVi
            {
                Ma = TaoThe(p, now),
                NgayLuu = dong?.NgayLuu ?? now,
                LuotCuaToi = p.GioiHanMoiHocVien.HasValue ? LuotDungCuaHocVien(maHocVien, p.MaGiamGia) : 0,
                GioiHanMoiHocVien = p.GioiHanMoiHocVien
            };
            v2.Ma.DaLuu = dong != null;
            GanTrangThai(v2, p, now);
            return v2;
        }

        /// <summary>
        /// Chi tiết mã kèm điều khoản. Xem được khi mã có trong ví, hoặc là mã công khai học viên đang lưu được;
        /// ngược lại trả null (không lộ mã riêng, mã thân thiết chưa đạt tiêu chí).
        /// </summary>
        public ChiTietMaGiamGiaViewModel LayChiTiet(int maHocVien, string maCode)
        {
            var p = LayTheoCode(maCode);
            if (p == null) return null;

            var now = DateTime.Now;
            var dong = LayDongVi(maHocVien).FirstOrDefault(v => v.MaGiamGia == p.MaGiamGia);
            int luot = p.GioiHanMoiHocVien.HasValue ? LuotDungCuaHocVien(maHocVien, p.MaGiamGia) : 0;
            bool luuDuoc = p.CongKhai && LyDoKhongLuuDuoc(p, p.LaThanThiet ? ThongKeHocVien(maHocVien) : null, luot, now) == null;
            if (dong == null && !luuDuoc) return null;

            var vm = new ChiTietMaGiamGiaViewModel
            {
                ChuongTrinh = p,
                The = TaoThe(p, now),
                CoTheLuu = dong == null && luuDuoc,
                KhoaHocApDung = p.PhamVi == "KhoaHoc" ? LayKhoaHocApDung(p.MaKhuyenMai) : new List<LuaChonKhoaHoc>()
            };
            vm.The.DaLuu = dong != null;
            if (dong != null)
            {
                vm.TrongVi = new MaTrongVi { Ma = vm.The, NgayLuu = dong.NgayLuu, LuotCuaToi = luot, GioiHanMoiHocVien = p.GioiHanMoiHocVien };
                GanTrangThai(vm.TrongVi, p, now);
            }
            vm.LichSuCuaToi = db.Database.SqlQuery<LuotSuDungUuDai>(@"
                SELECT s.maDonHang AS MaDonHang, ISNULL(d.ngayThanhToan, s.ngaySuDung) AS NgaySuDung, N'' AS HoTen, N'' AS Email,
                       s.tienTruocGiam AS TienTruocGiam, s.tienGiam AS TienGiam, d.tongTien AS TongDonHang
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai IN (" + DonDaTraSql + @")
                WHERE s.maGiamGia = @mgg AND s.maHocVien = @hv
                ORDER BY NgaySuDung DESC",
                new SqlParameter("@mgg", p.MaGiamGia), new SqlParameter("@hv", maHocVien)).ToList();

            var dk = vm.DieuKhoan;
            dk.Add(new KeyValuePair<string, string>("Mức ưu đãi", p.LaPhanTram
                ? "Giảm " + p.MucUuDai.ToString("0.##") + "% trên tổng giá các khóa học được áp dụng trong đơn"
                  + (p.GiamToiDa.HasValue ? ", giảm tối đa " + p.GiamToiDa.Value.ToString("N0") + " đ" : "")
                : "Giảm " + p.SoTienGiam.ToString("N0") + " đ (không vượt quá tổng giá các khóa được áp dụng)"));
            dk.Add(new KeyValuePair<string, string>("Thời gian áp dụng",
                "Từ " + p.NgayBatDau.ToString("HH:mm 'ngày' dd/MM/yyyy") + " đến " + p.NgayKetThuc.ToString("HH:mm 'ngày' dd/MM/yyyy")));
            dk.Add(new KeyValuePair<string, string>("Phạm vi áp dụng", p.PhamVi == "KhoaHoc"
                ? vm.KhoaHocApDung.Count + " khóa học được chỉ định (xem danh sách bên dưới)"
                : p.PhamVi == "DanhMuc" ? "Các khóa học thuộc danh mục \"" + (p.TenDanhMucApDung ?? p.MaDanhMucApDung) + "\""
                : "Tất cả khóa học có phí trên E-Learning (toàn sàn)"));
            dk.Add(new KeyValuePair<string, string>("Giá trị đơn tối thiểu", p.DonHangToiThieu.HasValue && p.DonHangToiThieu.Value > 0
                ? "Tổng giá các khóa được áp dụng từ " + p.DonHangToiThieu.Value.ToString("N0") + " đ"
                : "Không yêu cầu"));
            dk.Add(new KeyValuePair<string, string>("Đối tượng", p.MoTaDoiTuong));
            dk.Add(new KeyValuePair<string, string>("Số lượt dùng",
                (p.GioiHanMoiHocVien.HasValue ? "Mỗi học viên dùng tối đa " + p.GioiHanMoiHocVien.Value + " lần" : "Không giới hạn số lần mỗi học viên")
                + (p.GioiHanLuotDung.HasValue ? "; toàn chương trình còn " + Math.Max(0, p.GioiHanLuotDung.Value - p.SoLuotDaDung) + "/" + p.GioiHanLuotDung.Value + " lượt" : "")));
            dk.Add(new KeyValuePair<string, string>("Cách dùng",
                "Lưu mã vào ví, thêm khóa học vào giỏ hàng, mở mục \"Mã giảm giá\" ở giỏ hàng và chọn mã. Mỗi đơn hàng dùng tối đa 1 mã."));
            return vm;
        }
    }
}
