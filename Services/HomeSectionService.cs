using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Các mục trang chủ của học viên đã đăng nhập (HomeController.Index):
    ///   - "Tiếp tục học": khóa đang học dở, mở đúng bài đang xem (trang Study tự tua tới vị trí đã lưu).
    ///   - "Bán chạy tuần này": xếp theo lượt đăng ký trong 7 ngày; tuần ít dữ liệu thì 30 ngày, rồi toàn thời gian.
    ///   - "Khóa học yêu thích của bạn": khóa đã thả tim (tối đa vài khóa, còn lại ở trang "Khóa học yêu thích").
    /// </summary>
    public class HomeSectionService
    {
        private static readonly string[] TrangThaiDangBan = { "Đã xuất bản", "Đã duyệt", "Active" };
        /// <summary>Kỳ "bán chạy" cần ít nhất chừng này khóa có lượt đăng ký, ít hơn thì mở rộng kỳ.</summary>
        private const int SoKhoaToiThieuMoiKy = 3;

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public HomeSectionService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        // ================================================================== Tiếp tục học

        /// <summary>
        /// Khóa đã đăng ký chưa học xong: khóa học gần nhất trước, rồi khóa đăng ký gần nhất chưa bắt đầu.
        /// Bài mở ra là bài chưa xong vừa xem gần nhất, chưa xem bài nào thì là bài đầu tiên chưa xong.
        /// </summary>
        public List<TiepTucHocItem> TiepTucHoc(int userId, int soLuong)
        {
            var dangKy = db.DangKyKhoaHoc
                .Where(d => d.maHocVien == userId && d.KhoaHoc != null && d.KhoaHoc.trangThai != CourseStatus.KhoaVinhVien)
                .Select(d => new
                {
                    d.maKhoaHoc,
                    d.ngayDangKy,
                    d.KhoaHoc.tenKhoaHoc,
                    d.KhoaHoc.anhBia,
                    TenGiaoVien = d.KhoaHoc.NguoiDung != null ? d.KhoaHoc.NguoiDung.hoTen : null
                })
                .ToList()
                .GroupBy(d => d.maKhoaHoc).Select(g => g.OrderByDescending(x => x.ngayDangKy).First())
                .ToList();
            if (dangKy.Count == 0) return new List<TiepTucHocItem>();

            var learning = new LearningService(db);
            var tienDo = learning.TienDoCacKhoa(userId, dangKy.Select(d => d.maKhoaHoc).ToList());
            var chon = dangKy
                .Where(d => tienDo.ContainsKey(d.maKhoaHoc) && tienDo[d.maKhoaHoc].TongSoBai > 0 && !tienDo[d.maKhoaHoc].DaHoanThanh)
                .OrderByDescending(d => tienDo[d.maKhoaHoc].LanHocGanNhat.HasValue)
                .ThenByDescending(d => tienDo[d.maKhoaHoc].LanHocGanNhat)
                .ThenByDescending(d => d.ngayDangKy)
                .Take(soLuong)
                .ToList();

            var ketQua = new List<TiepTucHocItem>();
            foreach (var d in chon)
            {
                var td = tienDo[d.maKhoaHoc];
                var bai = learning.LayBaiHoc(d.maKhoaHoc);
                var maBai = bai.Select(b => b.maBaiHoc).ToList();
                var dangXem = db.TienDoBaiHoc
                    .Where(t => t.maHocVien == userId && maBai.Contains(t.maBaiHoc) && t.daHoanThanh != true && t.lanTruyCapGanNhat != null)
                    .OrderByDescending(t => t.lanTruyCapGanNhat)
                    .Select(t => new { t.maBaiHoc, t.thoiGianXemDungLai })
                    .FirstOrDefault();
                string maMo = dangXem != null ? dangXem.maBaiHoc : td.MaBaiTiepTheo;
                int viTri = bai.FindIndex(b => b.maBaiHoc == maMo);

                ketQua.Add(new TiepTucHocItem
                {
                    MaKhoaHoc = d.maKhoaHoc,
                    TenKhoaHoc = d.tenKhoaHoc,
                    AnhBia = d.anhBia,
                    TenGiaoVien = d.TenGiaoVien,
                    PhanTram = td.PhanTram,
                    SoBaiXong = td.SoBaiXong,
                    TongSoBai = td.TongSoBai,
                    MaBaiHoc = maMo,
                    TenBaiHoc = viTri >= 0 ? bai[viTri].tenBaiHoc : td.TenBaiTiepTheo,
                    SoThuTuBai = viTri + 1,
                    ViTriGiay = dangXem != null ? (dangXem.thoiGianXemDungLai ?? 0) : 0,
                    LanHocGanNhat = td.LanHocGanNhat
                });
            }
            return ketQua;
        }

        // ================================================================== Bán chạy

        /// <summary>
        /// Khóa đang bán, chưa sở hữu, xếp theo lượt đăng ký trong kỳ (7 ngày → 30 ngày → toàn thời gian, kỳ đầu tiên có ít nhất
        /// 3 khóa có lượt đăng ký). Cùng số lượt thì đánh giá cao, nhiều học viên hơn đứng trước.
        /// </summary>
        public BanChayResult BanChay(int userId, int soLuong)
        {
            var now = DateTime.Now;
            var tatCaDangKy = db.DangKyKhoaHoc.Select(d => new { d.maKhoaHoc, d.ngayDangKy }).ToList();
            var kyXet = new[]
            {
                new { Ten = "tuần này", Tu = (DateTime?)now.AddDays(-7) },
                new { Ten = "30 ngày qua", Tu = (DateTime?)now.AddDays(-30) },
                new { Ten = "toàn thời gian", Tu = (DateTime?)null }
            };

            var ketQua = new BanChayResult();
            Dictionary<string, int> luot = null;
            foreach (var ky in kyXet)
            {
                luot = tatCaDangKy.Where(d => ky.Tu == null || (d.ngayDangKy.HasValue && d.ngayDangKy.Value >= ky.Tu.Value))
                    .GroupBy(d => d.maKhoaHoc).ToDictionary(g => g.Key, g => g.Count());
                ketQua.TenKy = ky.Ten;
                if (luot.Count >= SoKhoaToiThieuMoiKy) break;
            }

            var tongHocVien = tatCaDangKy.GroupBy(d => d.maKhoaHoc).ToDictionary(g => g.Key, g => g.Count());
            var khoa = db.KhoaHoc
                .Include(k => k.NguoiDung)
                .Include(k => k.DanhMuc)
                .Include(k => k.DanhGiaKhoaHoc)
                .Where(k => TrangThaiDangBan.Contains(k.trangThai))
                .Where(k => !db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == k.maKhoaHoc && d.maHocVien == userId))
                .ToList();

            Func<KhoaHoc, int> luotKy = k => luot.ContainsKey(k.maKhoaHoc) ? luot[k.maKhoaHoc] : 0;
            ketQua.KhoaHoc = khoa
                .Where(k => luotKy(k) > 0)
                .OrderByDescending(luotKy)
                .ThenByDescending(k => k.DanhGiaKhoaHoc.Any() ? k.DanhGiaKhoaHoc.Average(x => (double)x.soSao) : 0)
                .ThenByDescending(k => tongHocVien.ContainsKey(k.maKhoaHoc) ? tongHocVien[k.maKhoaHoc] : 0)
                .Take(soLuong)
                .ToList();
            ketQua.LuotTrongKy = ketQua.KhoaHoc.ToDictionary(k => k.maKhoaHoc, luotKy);
            ketQua.TongHocVien = tongHocVien;
            // Nhãn "Bán chạy": 3 khóa đứng đầu có lượt đăng ký trong kỳ
            ketQua.TopBanChay = new HashSet<string>(ketQua.KhoaHoc.Select(k => k.maKhoaHoc));
            return ketQua;
        }

        // ================================================================== thẻ khóa học dùng chung

        /// <summary>
        /// Thẻ khóa học theo đúng thứ tự mã truyền vào, bỏ khóa không còn bán, khóa đã đăng ký và khóa do chính người xem dạy.
        /// Số học viên / đánh giá đếm một lần cho cả danh sách.
        /// </summary>
        private List<CourseCardViewModel> TheKhoaHoc(IEnumerable<string> maTheoThuTu, int userId, int soLuong)
        {
            var ma = maTheoThuTu.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            if (ma.Count == 0) return new List<CourseCardViewModel>();
            var khoa = db.KhoaHoc.Include(k => k.NguoiDung).Include(k => k.DanhMuc)
                .Where(k => ma.Contains(k.maKhoaHoc) && TrangThaiDangBan.Contains(k.trangThai) && k.maGiaoVien != userId
                         && !db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == k.maKhoaHoc && d.maHocVien == userId))
                .ToDictionary(k => k.maKhoaHoc);
            var chon = ma.Where(khoa.ContainsKey).Take(soLuong).ToList();
            var hocVien = db.DangKyKhoaHoc.Where(d => chon.Contains(d.maKhoaHoc)).GroupBy(d => d.maKhoaHoc)
                .Select(g => new { g.Key, n = g.Count() }).ToDictionary(x => x.Key, x => x.n);
            var danhGia = db.DanhGiaKhoaHoc.Where(d => chon.Contains(d.maKhoaHoc)).GroupBy(d => d.maKhoaHoc)
                .Select(g => new { g.Key, n = g.Count(), tb = g.Average(x => (double)x.soSao) }).ToDictionary(x => x.Key);
            return chon.Select(id => new CourseCardViewModel
            {
                Course = khoa[id],
                SoHocVien = hocVien.ContainsKey(id) ? hocVien[id] : 0,
                SoDanhGia = danhGia.ContainsKey(id) ? danhGia[id].n : 0,
                DiemDanhGia = danhGia.ContainsKey(id) ? danhGia[id].tb : 0,
                ChiNhanChiDinh = true
            }).ToList();
        }

        // ================================================================== Vì bạn đã học "X"

        /// <summary>
        /// Khóa liên quan tới khóa học viên vừa học gần nhất: "mua cùng nhau" / "học viên khác cũng mua" (RelatedCourseService,
        /// giống trang chi tiết khóa), thiếu thì bù khóa cùng lĩnh vực nhiều học viên nhất. Chưa học khóa nào thì trả về null.
        /// </summary>
        public GoiYTheoKhoaHoc ViBanDaHoc(int userId, int soLuong)
        {
            var baiGanNhat = db.TienDoBaiHoc
                .Where(t => t.maHocVien == userId && t.lanTruyCapGanNhat != null)
                .OrderByDescending(t => t.lanTruyCapGanNhat)
                .Select(t => new { t.maBaiHoc, khoa = t.BaiHoc.ChuongHoc != null ? t.BaiHoc.ChuongHoc.maKhoaHoc : null })
                .FirstOrDefault();
            if (baiGanNhat == null) return null;
            string moc = baiGanNhat.khoa;
            if (moc == null && baiGanNhat.maBaiHoc.Contains("_")) moc = baiGanNhat.maBaiHoc.Substring(0, baiGanNhat.maBaiHoc.IndexOf('_'));
            var khoaMoc = db.KhoaHoc.Where(k => k.maKhoaHoc == moc).Select(k => new { k.maKhoaHoc, k.tenKhoaHoc, k.maDanhMuc }).FirstOrDefault();
            if (khoaMoc == null || string.IsNullOrEmpty(khoaMoc.maDanhMuc)) return null;

            // Mục này chỉ gợi ý khóa trong đúng lĩnh vực của khóa vừa học.
            // Không dùng dữ liệu mua chung khác danh mục vì dữ liệu demo ít có thể
            // tạo các liên hệ không đúng về mặt nội dung.
            var cungLinhVuc = db.KhoaHoc
                .Where(k => k.maKhoaHoc != moc
                         && k.maDanhMuc == khoaMoc.maDanhMuc
                         && k.maGiaoVien != userId
                         && TrangThaiDangBan.Contains(k.trangThai)
                         && !db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == k.maKhoaHoc && d.maHocVien == userId))
                .OrderByDescending(k => db.DangKyKhoaHoc.Count(d => d.maKhoaHoc == k.maKhoaHoc))
                .ThenByDescending(k => k.DanhGiaKhoaHoc.Any() ? k.DanhGiaKhoaHoc.Average(d => (double)d.soSao) : 0)
                .ThenBy(k => k.maKhoaHoc)
                .Select(k => k.maKhoaHoc).Take(soLuong).ToList();

            var the = TheKhoaHoc(cungLinhVuc, userId, soLuong);
            return the.Count == 0 ? null : new GoiYTheoKhoaHoc { MaKhoaHocMoc = moc, TenKhoaHocMoc = khoaMoc.tenKhoaHoc, KhoaHoc = the };
        }

        // ================================================================== Đánh giá cao / Mới ra mắt

        /// <summary>
        /// Khóa được đánh giá cao: điểm trung bình từ 4,0 sao, xếp theo điểm có tính số lượt (trung bình Bayes, m = 3) để khóa
        /// 1 lượt 5 sao không vượt khóa 20 lượt 4,8 sao.
        /// </summary>
        public List<CourseCardViewModel> DanhGiaCao(int userId, int soLuong)
        {
            var dg = db.DanhGiaKhoaHoc.GroupBy(d => d.maKhoaHoc)
                .Select(g => new { g.Key, n = g.Count(), tong = g.Sum(x => (double)x.soSao) }).ToList();
            if (dg.Count == 0) return new List<CourseCardViewModel>();
            const double m = 3;
            double c = dg.Sum(x => x.tong) / dg.Sum(x => x.n);
            var ma = dg.Where(x => x.tong / x.n >= 4.0)
                .OrderByDescending(x => (x.tong + m * c) / (x.n + m))
                .ThenByDescending(x => x.n)
                .Select(x => x.Key);
            var the = TheKhoaHoc(ma, userId, soLuong);
            foreach (var t in the) { t.Nhan = "Đánh giá cao"; t.NhanKieu = "danhgia"; }
            return the;
        }

        /// <summary>Khóa mới ra mắt: theo ngày được duyệt lần đầu (bảng khóa học không có ngày tạo); trong 30 ngày gắn nhãn "Mới".</summary>
        public List<CourseCardViewModel> MoiRaMat(int userId, int soLuong)
        {
            var ngay = db.KiemDuyetKhoaHoc
                .Where(k => k.trangThai == "Đã duyệt" && k.ngayKiemDuyet != null)
                .GroupBy(k => k.maKhoaHoc)
                .Select(g => new { g.Key, ngay = g.Min(x => x.ngayKiemDuyet) })
                .ToList()
                .OrderByDescending(x => x.ngay)
                .ThenBy(x => x.Key)
                .ToList();
            var ngayTheoMa = ngay.ToDictionary(x => x.Key, x => x.ngay.Value);
            var the = TheKhoaHoc(ngay.Select(x => x.Key), userId, soLuong);
            foreach (var t in the)
            {
                var d = ngayTheoMa[t.Course.maKhoaHoc];
                if (d >= DateTime.Now.AddDays(-30)) { t.Nhan = "Mới"; t.NhanKieu = "moi"; }
                t.GhiChu = "Ra mắt " + d.ToString("dd/MM/yyyy");
            }
            return the;
        }

        // ================================================================== Giáo viên nổi bật

        /// <summary>Giáo viên có khóa đang bán, nhiều học viên nhất rồi đánh giá cao nhất (chỉ mục tìm kiếm, cache 2 phút).</summary>
        public List<SearchService.GiaoVienChiMuc> GiaoVienNoiBat(int userId, int soLuong)
        {
            return new SearchService(db).LayChiMuc().GiaoVien
                .Where(g => g.MaGiaoVien != userId)
                .OrderByDescending(g => g.SoHocVien)
                .ThenByDescending(g => g.DiemDanhGia)
                .ThenByDescending(g => g.SoKhoaHoc)
                .Take(soLuong)
                .ToList();
        }

        // ================================================================== Đã xem gần đây

        private class LuotXem
        {
            public string MaKhoaHoc { get; set; }
            public DateTime LanCapNhat { get; set; }
        }

        /// <summary>Khóa đã mở trang chi tiết gần đây (TuongTacKhoaHoc.luotXem, migration 005), chưa sở hữu, mới xem trước.</summary>
        public List<CourseCardViewModel> DaXemGanDay(int userId, int soLuong)
        {
            var xem = db.Database.SqlQuery<LuotXem>(
                "SELECT TOP (60) maKhoaHoc AS MaKhoaHoc, lanCapNhat AS LanCapNhat FROM dbo.TuongTacKhoaHoc WHERE maHocVien = @p0 AND luotXem > 0 ORDER BY lanCapNhat DESC",
                userId).ToList();
            var luc = xem.GroupBy(x => x.MaKhoaHoc).ToDictionary(g => g.Key, g => g.Max(x => x.LanCapNhat));
            var the = TheKhoaHoc(xem.Select(x => x.MaKhoaHoc), userId, soLuong);
            foreach (var t in the)
            {
                int ngay = (DateTime.Now.Date - luc[t.Course.maKhoaHoc].Date).Days;
                t.GhiChu = ngay <= 0 ? "Đã xem hôm nay" : ngay == 1 ? "Đã xem hôm qua" : "Đã xem " + ngay + " ngày trước";
            }
            return the;
        }

        // ================================================================== Yêu thích

        /// <summary>Khóa đã thả tim (đang bán, chưa sở hữu), mới tương tác trước; <paramref name="tongSo"/> = tổng số để ghi "Xem tất cả (n)".</summary>
        public List<YeuThichItem> YeuThich(int userId, int soLuong, out int tongSo)
        {
            var favorites = new FavoriteService(db).GetFavorites(userId);
            tongSo = favorites.Count;
            return favorites.Take(soLuong)
                .Select(f => new YeuThichItem { Course = f.Course, SoDanhGia = f.SoDanhGia, DiemDanhGia = f.DiemDanhGia })
                .ToList();
        }
    }
}
