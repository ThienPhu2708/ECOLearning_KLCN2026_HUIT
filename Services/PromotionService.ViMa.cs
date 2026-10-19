using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Mã giảm giá phía học viên: mã công khai để thu thập, ví "Mã giảm giá của tôi", trang chi tiết mã (migration 009).
    /// Mã đã lưu vào ví không xóa được. Giỏ hàng chỉ gợi ý mã trong ví; số tiền giảm luôn tính lại bằng KiemTraCoupon.
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

        private const string DieuKienDangChay = @"
            km.trangThai = N'HoatDong' AND mg.maCode IS NOT NULL AND ISNULL(mg.trangThai, 1) = 1
            AND km.ngayBatDau <= @now AND km.ngayKetThuc >= @now";

        /// <summary>Số lần học viên đã dùng từng mã (chỉ đơn đã thanh toán).</summary>
        private Dictionary<string, int> LuotDungCuaHocVien(int maHocVien)
        {
            if (maHocVien <= 0) return new Dictionary<string, int>();
            return db.Database.SqlQuery<LuotDungTheoMa>(@"
                SELECT s.maGiamGia AS MaGiamGia, COUNT(*) AS SoLuot
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai = N'Đã thanh toán'
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
        private static string SoTienNgan(decimal v)
        {
            if (v >= 1000000 && v % 100000 == 0) return (v / 1000000m).ToString("0.#") + "TR";
            if (v >= 1000 && v % 1000 == 0) return (v / 1000m).ToString("0") + "K";
            return v.ToString("N0") + "đ";
        }

        /// <summary>Thẻ hiển thị một chương trình (chưa có số tiền giảm theo giỏ).</summary>
        private static MaGiamGiaHienThi TaoThe(ChuongTrinhKhuyenMai p)
        {
            var dieuKien = new List<string>();
            if (p.LaThanThiet) dieuKien.Add("Ưu đãi học viên thân thiết");
            dieuKien.Add(p.PhamVi == "KhoaHoc" ? "Áp dụng cho " + p.SoKhoaApDung + " khóa học" : "Áp dụng mọi khóa học");
            if (p.DonHangToiThieu.HasValue && p.DonHangToiThieu.Value > 0)
                dieuKien.Add("Đơn từ " + p.DonHangToiThieu.Value.ToString("N0") + " đ");
            dieuKien.Add("Mỗi tài khoản dùng " + p.GioiHanMoiHocVien + " lần");

            string ghiChu = null;
            if (p.LaPhanTram && p.GiamToiDa.HasValue) ghiChu = "Tối đa " + SoTienNgan(p.GiamToiDa.Value);
            else if (p.DonHangToiThieu.HasValue && p.DonHangToiThieu.Value > 0) ghiChu = "Đơn từ " + SoTienNgan(p.DonHangToiThieu.Value);

            return new MaGiamGiaHienThi
            {
                MaKhuyenMai = p.MaKhuyenMai,
                MaGiamGia = p.MaGiamGia,
                MaCode = p.MaCode,
                TenKhuyenMai = p.TenKhuyenMai,
                MoTaUuDai = p.MoTaUuDai,
                GiaTriNgan = p.LaPhanTram ? p.MucUuDai.ToString("0.##") + "%" : SoTienNgan((decimal)p.MucUuDai),
                GhiChuGiaTri = ghiChu,
                DieuKien = dieuKien,
                NgayBatDau = p.NgayBatDau,
                NgayKetThuc = p.NgayKetThuc,
                ConLai = p.GioiHanLuotDung.HasValue ? p.GioiHanLuotDung.Value - p.SoLuotDaDung : (int?)null,
                LaThanThiet = p.LaThanThiet,
                CoTheApDung = true
            };
        }

        /// <summary>
        /// Mã đang chạy, còn lượt, học viên chưa dùng hết lượt của mình; mã thân thiết chỉ khi đã đạt tiêu chí.
        /// <paramref name="chiTrongVi"/> = false: mã công khai (trang chủ, trang khóa học, tab "Thu thập thêm"), có cờ DaLuu.
        /// <paramref name="chiTrongVi"/> = true: chỉ mã trong ví (giỏ hàng), kể cả mã sau đó bị Marketing chuyển thành mã kín.
        /// Có <paramref name="gioHang"/> thì tính số tiền giảm / lý do chưa dùng được bằng KiemTraCoupon.
        /// <paramref name="maKhoaHoc"/> chỉ lấy mã áp dụng cho khóa đó.
        /// </summary>
        public List<MaGiamGiaHienThi> LayMaChoHocVien(int maHocVien, IList<DongGioHang> gioHang = null, string maKhoaHoc = null, bool chiTrongVi = false)
        {
            var ketQua = new List<MaGiamGiaHienThi>();
            if (chiTrongVi && maHocVien <= 0) return ketQua;

            string nguon = chiTrongVi
                ? "EXISTS (SELECT 1 FROM dbo.ViMaGiamGia v WHERE v.maGiamGia = mg.maGiamGia AND v.maHocVien = @hv)"
                : "km.congKhai = 1";
            var chuongTrinh = db.Database.SqlQuery<ChuongTrinhKhuyenMai>(SelectChuongTrinh + @"
                WHERE " + nguon + " AND " + DieuKienDangChay + @"
                  AND (@kh IS NULL OR km.phamVi = N'TatCa'
                       OR EXISTS (SELECT 1 FROM dbo.KhuyenMaiKhoaHoc k WHERE k.maKhuyenMai = km.maKhuyenMai AND k.maKhoaHoc = @kh))
                ORDER BY km.ngayKetThuc",
                new SqlParameter("@now", DateTime.Now),
                new SqlParameter("@hv", maHocVien),
                new SqlParameter("@kh", SqlDbType.VarChar, 20) { Value = (object)maKhoaHoc ?? DBNull.Value })
                .Where(p => !p.HetLuot)
                .ToList();
            if (chuongTrinh.Count == 0) return ketQua;

            HocVienThanThietItem hv = maHocVien > 0 ? ThongKeHocVien(maHocVien) : null;
            var daDung = LuotDungCuaHocVien(maHocVien);
            var daLuu = new HashSet<string>(LayDongVi(maHocVien).Select(v => v.MaGiamGia));

            foreach (var p in chuongTrinh)
            {
                // Chưa đăng nhập (hv = null) thì không thấy mã thân thiết
                if (p.LaThanThiet && !DatTieuChi(p, hv)) continue;
                int luotCuaToi;
                daDung.TryGetValue(p.MaGiamGia, out luotCuaToi);
                if (luotCuaToi >= p.GioiHanMoiHocVien) continue;

                var ma = TaoThe(p);
                ma.DaLuu = chiTrongVi || daLuu.Contains(p.MaGiamGia);
                if (gioHang != null && gioHang.Count > 0)
                {
                    var kq = KiemTraCoupon(maHocVien, p.MaCode, gioHang);
                    ma.CoTheApDung = kq.Success;
                    ma.TienGiam = kq.TienGiam;
                    ma.LyDo = kq.Success ? null : kq.Message;
                }
                ketQua.Add(ma);
            }

            return ketQua
                .OrderByDescending(m => m.CoTheApDung)
                .ThenByDescending(m => m.TienGiam)
                .ThenBy(m => m.NgayKetThuc)
                .ToList();
        }

        /// <summary>Trạng thái của một mã trong ví.</summary>
        private static void GanTrangThai(MaTrongVi v, ChuongTrinhKhuyenMai p, DateTime now)
        {
            if (v.LuotCuaToi >= p.GioiHanMoiHocVien) { v.TrangThai = "DaDung"; v.NhanTrangThai = "Đã dùng"; }
            else if (p.TrangThaiLuu == KetThuc) { v.TrangThai = "HetHieuLuc"; v.NhanTrangThai = "Đã kết thúc"; }
            else if (p.NgayKetThuc < now) { v.TrangThai = "HetHieuLuc"; v.NhanTrangThai = "Hết hạn"; }
            else if (p.HetLuot) { v.TrangThai = "HetHieuLuc"; v.NhanTrangThai = "Hết lượt"; }
            else if (p.TrangThaiLuu == TamNgung || !p.CouponDangBat) { v.TrangThai = "HetHieuLuc"; v.NhanTrangThai = "Tạm ngưng"; }
            else if (p.NgayBatDau > now) { v.TrangThai = "SapDienRa"; v.NhanTrangThai = "Sắp diễn ra"; }
            else { v.TrangThai = "CoTheDung"; v.NhanTrangThai = "Có thể dùng"; }
        }

        /// <summary>Ví "Mã giảm giá của tôi": mã đã lưu chia theo trạng thái, kèm mã công khai chưa lưu.</summary>
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
                var now = DateTime.Now;
                foreach (var p in chuongTrinh)
                {
                    int luot;
                    daDung.TryGetValue(p.MaGiamGia, out luot);
                    var v = new MaTrongVi
                    {
                        Ma = TaoThe(p),
                        NgayLuu = dongVi.ContainsKey(p.MaGiamGia) ? dongVi[p.MaGiamGia] : now,
                        LuotCuaToi = luot,
                        GioiHanMoiHocVien = p.GioiHanMoiHocVien
                    };
                    v.Ma.DaLuu = true;
                    GanTrangThai(v, p, now);
                    if (v.TrangThai == "DaDung") vm.DaDung.Add(v);
                    else if (v.TrangThai == "HetHieuLuc") vm.HetHieuLuc.Add(v);
                    else vm.CoTheDung.Add(v);
                }
                vm.CoTheDung = vm.CoTheDung.OrderBy(v => v.TrangThai == "SapDienRa").ThenBy(v => v.Ma.NgayKetThuc).ToList();
                vm.DaDung = vm.DaDung.OrderByDescending(v => v.NgayLuu).ToList();
                vm.HetHieuLuc = vm.HetHieuLuc.OrderByDescending(v => v.Ma.NgayKetThuc).ToList();
            }
            vm.CoTheThuThap = LayMaChoHocVien(maHocVien).Where(m => !m.DaLuu).ToList();
            return vm;
        }

        /// <summary>Lưu mã công khai vào ví. Không có thao tác ngược lại (xóa / hoàn tác).</summary>
        public KetQuaLuuMa LuuMa(int maHocVien, string maCode)
        {
            Func<string, KetQuaLuuMa> loi = m => new KetQuaLuuMa { Success = false, Message = m };
            if (maHocVien <= 0) return loi("Vui lòng đăng nhập để lưu mã giảm giá.");
            if (string.IsNullOrWhiteSpace(maCode)) return loi("Mã giảm giá không hợp lệ.");

            string code = maCode.Trim().ToUpperInvariant();
            string maKm = db.Database.SqlQuery<string>("SELECT TOP (1) maKhuyenMai FROM dbo.MaGiamGia WHERE UPPER(maCode) = @code",
                new SqlParameter("@code", code)).FirstOrDefault();
            var p = LayTheoMa(maKm);
            if (p == null || p.MaGiamGia == null) return loi("Mã giảm giá không tồn tại.");

            if (LayDongVi(maHocVien).Any(v => v.MaGiamGia == p.MaGiamGia))
                return new KetQuaLuuMa { Success = true, DaCoTrongVi = true, Message = "Mã " + p.MaCode + " đã có trong ví của bạn." };

            var now = DateTime.Now;
            if (!p.CongKhai) return loi("Mã này không mở để lưu.");
            if (p.TrangThaiLuu == KetThuc || p.NgayKetThuc < now) return loi("Chương trình khuyến mãi đã kết thúc.");
            if (p.TrangThaiLuu == TamNgung || !p.CouponDangBat) return loi("Chương trình khuyến mãi đang tạm ngưng.");
            if (p.NgayBatDau > now) return loi("Chương trình khuyến mãi chưa bắt đầu.");
            if (p.HetLuot) return loi("Mã đã hết lượt sử dụng.");
            if (p.LaThanThiet && !DatTieuChi(p, ThongKeHocVien(maHocVien))) return loi("Mã chỉ dành cho học viên thân thiết đạt tiêu chí của chương trình.");
            int luot;
            LuotDungCuaHocVien(maHocVien).TryGetValue(p.MaGiamGia, out luot);
            if (luot >= p.GioiHanMoiHocVien) return loi("Bạn đã dùng hết lượt của mã này.");

            db.Database.ExecuteSqlCommand(@"
                IF NOT EXISTS (SELECT 1 FROM dbo.ViMaGiamGia WHERE maHocVien = @hv AND maGiamGia = @mgg)
                    INSERT INTO dbo.ViMaGiamGia (maHocVien, maGiamGia) VALUES (@hv, @mgg)",
                new SqlParameter("@hv", maHocVien), new SqlParameter("@mgg", p.MaGiamGia));
            return new KetQuaLuuMa { Success = true, Message = "Đã lưu mã " + p.MaCode + " vào Mã giảm giá của tôi." };
        }

        /// <summary>
        /// Chi tiết mã kèm chính sách. Xem được khi mã có trong ví, hoặc là mã công khai đang chạy học viên đủ điều kiện lưu.
        /// Trả về null nếu không được xem (mã kín chưa lưu, mã thân thiết chưa đạt tiêu chí...).
        /// </summary>
        public ChiTietMaGiamGiaViewModel LayChiTiet(int maHocVien, string maCode)
        {
            if (string.IsNullOrWhiteSpace(maCode)) return null;
            string maKm = db.Database.SqlQuery<string>("SELECT TOP (1) maKhuyenMai FROM dbo.MaGiamGia WHERE UPPER(maCode) = @code",
                new SqlParameter("@code", maCode.Trim().ToUpperInvariant())).FirstOrDefault();
            var p = LayTheoMa(maKm);
            if (p == null || p.MaGiamGia == null) return null;

            var now = DateTime.Now;
            var dong = LayDongVi(maHocVien).FirstOrDefault(v => v.MaGiamGia == p.MaGiamGia);
            int luot;
            LuotDungCuaHocVien(maHocVien).TryGetValue(p.MaGiamGia, out luot);
            bool dangChay = p.TrangThaiLuu == HoatDong && p.CouponDangBat && p.NgayBatDau <= now && p.NgayKetThuc >= now && !p.HetLuot;
            bool duDieuKien = !p.LaThanThiet || DatTieuChi(p, ThongKeHocVien(maHocVien));
            if (dong == null && !(p.CongKhai && dangChay && duDieuKien)) return null;

            var vm = new ChiTietMaGiamGiaViewModel
            {
                ChuongTrinh = p,
                The = TaoThe(p),
                CoTheLuu = dong == null && p.CongKhai && dangChay && duDieuKien && luot < p.GioiHanMoiHocVien,
                KhoaHocApDung = p.PhamVi == "KhoaHoc" ? LayKhoaHocApDung(p.MaKhuyenMai) : new List<LuaChonKhoaHoc>()
            };
            vm.The.DaLuu = dong != null;
            if (dong != null)
            {
                vm.TrongVi = new MaTrongVi { Ma = vm.The, NgayLuu = dong.NgayLuu, LuotCuaToi = luot, GioiHanMoiHocVien = p.GioiHanMoiHocVien };
                GanTrangThai(vm.TrongVi, p, now);
            }
            vm.LichSuCuaToi = db.Database.SqlQuery<LuotSuDungUuDai>(@"
                SELECT s.maDonHang AS MaDonHang, s.ngaySuDung AS NgaySuDung, N'' AS HoTen, N'' AS Email,
                       s.tienTruocGiam AS TienTruocGiam, s.tienGiam AS TienGiam, d.tongTien AS TongDonHang
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai = N'Đã thanh toán'
                WHERE s.maGiamGia = @mgg AND s.maHocVien = @hv
                ORDER BY s.ngaySuDung DESC",
                new SqlParameter("@mgg", p.MaGiamGia), new SqlParameter("@hv", maHocVien)).ToList();

            var dk = vm.DieuKhoan;
            dk.Add(new KeyValuePair<string, string>("Mức ưu đãi", p.LaPhanTram
                ? "Giảm " + p.MucUuDai.ToString("0.##") + "% trên tổng giá các khóa học được áp dụng trong đơn"
                  + (p.GiamToiDa.HasValue ? ", giảm tối đa " + p.GiamToiDa.Value.ToString("N0") + " đ" : "")
                : "Giảm " + ((decimal)p.MucUuDai).ToString("N0") + " đ cho đơn hàng (không vượt quá tổng giá các khóa được áp dụng)"));
            dk.Add(new KeyValuePair<string, string>("Thời gian áp dụng",
                "Từ " + p.NgayBatDau.ToString("HH:mm 'ngày' dd/MM/yyyy") + " đến " + p.NgayKetThuc.ToString("HH:mm 'ngày' dd/MM/yyyy")));
            dk.Add(new KeyValuePair<string, string>("Khóa học áp dụng", p.PhamVi == "KhoaHoc"
                ? vm.KhoaHocApDung.Count + " khóa học được chỉ định (xem danh sách bên dưới)"
                : "Tất cả khóa học có phí trên E-Learning"));
            dk.Add(new KeyValuePair<string, string>("Giá trị đơn tối thiểu", p.DonHangToiThieu.HasValue && p.DonHangToiThieu.Value > 0
                ? "Tổng giá các khóa được áp dụng từ " + p.DonHangToiThieu.Value.ToString("N0") + " đ"
                : "Không yêu cầu"));
            dk.Add(new KeyValuePair<string, string>("Đối tượng", p.MoTaDoiTuong));
            dk.Add(new KeyValuePair<string, string>("Số lượt dùng", "Mỗi tài khoản dùng tối đa " + p.GioiHanMoiHocVien + " lần"
                + (p.GioiHanLuotDung.HasValue ? "; toàn chương trình còn " + Math.Max(0, p.GioiHanLuotDung.Value - p.SoLuotDaDung) + "/" + p.GioiHanLuotDung.Value + " lượt" : "")));
            dk.Add(new KeyValuePair<string, string>("Cách dùng",
                "Lưu mã vào ví, thêm khóa học vào giỏ hàng, mở mục \"Mã khuyến mãi\" ở bước thanh toán và chọn Áp dụng."));
            return vm;
        }
    }
}
