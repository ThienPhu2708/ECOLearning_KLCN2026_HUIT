using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Tìm kiếm khóa học cho thanh tìm kiếm (gợi ý khi gõ) và trang /tim-kiem.
    ///
    /// Cách làm: chỉ mục trong bộ nhớ (khóa đang bán, danh mục, giáo viên, cụm từ trong tên khóa) dựng từ CSDL và
    /// cache 2 phút. Mọi chữ được bỏ dấu ("lap trinh" khớp "Lập trình"), từ cuối được khớp tiền tố (đang gõ dở),
    /// từ dài cho phép gõ sai 1–2 ký tự (Levenshtein). Điểm liên quan = trọng số trường (tên 10, danh mục 6,
    /// giáo viên 5, mô tả ngắn 2.5, mô tả 1) × độ khớp, cộng điểm cả cụm nằm liền trong tên, cộng nhẹ độ phổ biến.
    /// Danh mục khóa học cỡ vài trăm nên chỉ mục trong bộ nhớ đủ nhanh; khi lớn hơn thay bằng Full-Text Search / Elasticsearch.
    ///
    /// Gợi ý từ khóa = từ khóa nhiều người đã tìm (bảng TuKhoaTimKiem, migration 010) + cụm 1–3 từ trong tên khóa học
    /// và tên danh mục. Khóa học đã đăng ký (và khóa do chính giáo viên dạy) không xuất hiện trong kết quả.
    /// </summary>
    public class SearchService
    {
        public const int SoTuKhoaGoiY = 7;
        public const int SoKhoaHocGoiY = 5;
        public const int SoGiaoVienGoiY = 3;
        public const int SoGiaoVienKetQua = 20;
        public const int SoKetQuaMoiTrang = 12;
        public const int DoDaiToiDa = 100;

        private const string CacheKey = "ECOLearning.SearchIndex";
        private const string SessionDaGhi = "Search_DaGhiTuKhoa";
        private static readonly object KhoaDung = new object();
        private static readonly string[] TrangThaiDangBan = { "Đã xuất bản", "Đã duyệt", "Active" };

        /// <summary>Từ không được đứng đầu / cuối cụm gợi ý ("lập trình web với" → bỏ).</summary>
        private static readonly HashSet<string> TuBienCumTu = new HashSet<string>(SearchText.HuTu)
        {
            "cho", "tu", "den", "trong", "mot", "la", "ve", "qua", "theo", "tai"
        };

        private static readonly Regex TheHtml = new Regex("<[^>]+>", RegexOptions.Compiled);

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public SearchService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        // ================================================================== chỉ mục

        public class KhoaHocChiMuc
        {
            public string MaKhoaHoc { get; set; }
            public string TenKhoaHoc { get; set; }
            public string TenKhongDau { get; set; }
            public string AnhBia { get; set; }
            public string CapDo { get; set; }
            public string MaDanhMuc { get; set; }
            public string TenDanhMuc { get; set; }
            public int MaGiaoVien { get; set; }
            public string TenGiaoVien { get; set; }
            public decimal GiaHienTai { get; set; }
            public int SoHocVien { get; set; }
            public int SoDanhGia { get; set; }
            public double DiemDanhGia { get; set; }
            public string[] TuTen { get; set; }
            public string[] TuDanhMuc { get; set; }
            public string[] TuGiaoVien { get; set; }
            public string[] TuMoTaNgan { get; set; }
            public string[] TuMoTa { get; set; }
        }

        public class GiaoVienChiMuc
        {
            public int MaGiaoVien { get; set; }
            public string HoTen { get; set; }
            public string AnhDaiDien { get; set; }
            public string[] Tu { get; set; }
            /// <summary>Các chữ của họ tên, chữ thường còn dấu (để nhận ra người gõ đúng cả dấu).</summary>
            public string[] TuCoDau { get; set; }
            public string HoTenKhongDau { get; set; }
            /// <summary>Danh mục có nhiều khóa nhất của giáo viên, giúp phân biệt người trùng tên.</summary>
            public string ChuyenMon { get; set; }
            public int SoKhoaHoc { get; set; }
            public int SoHocVien { get; set; }
            public int SoDanhGia { get; set; }
            public double DiemDanhGia { get; set; }
        }

        /// <summary>Mức khớp tên giáo viên, số nhỏ khớp tốt hơn. Từ PhatAm trở đi là khớp gần đúng.</summary>
        public enum MucKhopTen
        {
            DungCoDau = 0,
            DungKhongDau = 1,
            GoDo = 2,
            PhatAm = 3,
            GoSai = 4,
            GiongCaCum = 5
        }

        public class KetQuaGiaoVien
        {
            public GiaoVienChiMuc GiaoVien { get; set; }
            public MucKhopTen Muc { get; set; }
            public double Diem { get; set; }
            public bool GanDung => Muc >= MucKhopTen.PhatAm;
        }

        public class DanhMucChiMuc
        {
            public string MaDanhMuc { get; set; }
            public string TenDanhMuc { get; set; }
            public string Slug { get; set; }
            public string KhongDau { get; set; }
            public string[] Tu { get; set; }
            public int SoKhoaHoc { get; set; }
        }

        /// <summary>Cụm 1–3 từ trong tên khóa học / tên danh mục, dùng làm từ khóa gợi ý.</summary>
        public class CumTu
        {
            public string HienThi { get; set; }
            public string KhongDau { get; set; }
            public string[] Tu { get; set; }
            public int SoKhoa { get; set; }
            public double PhoBien { get; set; }
            public bool LaDanhMuc { get; set; }
        }

        public class ChiMuc
        {
            public List<KhoaHocChiMuc> KhoaHoc { get; set; }
            public List<GiaoVienChiMuc> GiaoVien { get; set; }
            public List<DanhMucChiMuc> DanhMuc { get; set; }
            public List<CumTu> CumTu { get; set; }

            /// <summary>Từ (không dấu) → số lần xuất hiện, để gợi ý sửa lỗi gõ.</summary>
            public Dictionary<string, int> TuVung { get; set; }
        }

        /// <summary>Xóa cache chỉ mục (gọi khi khóa học / danh mục thay đổi nếu cần cập nhật ngay).</summary>
        public static void XoaCache()
        {
            HttpRuntime.Cache.Remove(CacheKey);
        }

        public ChiMuc LayChiMuc()
        {
            var cm = HttpRuntime.Cache[CacheKey] as ChiMuc;
            if (cm != null) return cm;
            lock (KhoaDung)
            {
                cm = HttpRuntime.Cache[CacheKey] as ChiMuc;
                if (cm != null) return cm;
                cm = DungChiMuc();
                HttpRuntime.Cache.Insert(CacheKey, cm, null, DateTime.UtcNow.AddMinutes(2), Cache.NoSlidingExpiration);
                return cm;
            }
        }

        private static string[] TuDuyNhat(string s)
        {
            return SearchText.Tach(SearchText.KhongDau(s)).Distinct().ToArray();
        }

        private ChiMuc DungChiMuc()
        {
            var khoa = db.KhoaHoc
                .Where(k => TrangThaiDangBan.Contains(k.trangThai))
                .Select(k => new
                {
                    k.maKhoaHoc, k.tenKhoaHoc, k.moTa, k.moTaNgan, k.anhBia, k.capDo, k.giaBan, k.maDanhMuc,
                    TenDanhMuc = k.DanhMuc != null ? k.DanhMuc.tenDanhMuc : null,
                    k.maGiaoVien,
                    TenGiaoVien = k.NguoiDung.hoTen,
                    AnhGiaoVien = k.NguoiDung.anhDaiDien,
                    GiaoVienHoatDong = k.NguoiDung.trangThai != false
                })
                .ToList();
            var soHocVien = db.DangKyKhoaHoc.GroupBy(d => d.maKhoaHoc)
                .Select(g => new { g.Key, SoLuong = g.Count() })
                .ToDictionary(x => x.Key, x => x.SoLuong);
            var danhGia = db.DanhGiaKhoaHoc.GroupBy(d => d.maKhoaHoc)
                .Select(g => new { g.Key, SoLuong = g.Count(), TrungBinh = g.Average(x => (double)x.soSao) })
                .ToDictionary(x => x.Key);

            var cm = new ChiMuc
            {
                KhoaHoc = new List<KhoaHocChiMuc>(),
                GiaoVien = new List<GiaoVienChiMuc>(),
                DanhMuc = new List<DanhMucChiMuc>(),
                CumTu = new List<CumTu>(),
                TuVung = new Dictionary<string, int>()
            };
            var cumTu = new Dictionary<string, CumTu>();

            foreach (var k in khoa)
            {
                int hv;
                soHocVien.TryGetValue(k.maKhoaHoc, out hv);
                var dg = danhGia.ContainsKey(k.maKhoaHoc) ? danhGia[k.maKhoaHoc] : null;
                string moTa = k.moTa == null ? "" : TheHtml.Replace(k.moTa, " ");
                if (moTa.Length > 3000) moTa = moTa.Substring(0, 3000);

                var item = new KhoaHocChiMuc
                {
                    MaKhoaHoc = k.maKhoaHoc,
                    TenKhoaHoc = k.tenKhoaHoc,
                    TenKhongDau = SearchText.KhongDau(k.tenKhoaHoc),
                    AnhBia = k.anhBia,
                    CapDo = k.capDo,
                    MaDanhMuc = k.maDanhMuc,
                    TenDanhMuc = k.TenDanhMuc,
                    MaGiaoVien = k.maGiaoVien,
                    TenGiaoVien = k.TenGiaoVien,
                    // Giá khóa học = giaBan (giảm giá chỉ qua mã khuyến mãi), cùng giá thanh toán
                    GiaHienTai = k.giaBan,
                    SoHocVien = hv,
                    SoDanhGia = dg != null ? dg.SoLuong : 0,
                    DiemDanhGia = dg != null ? dg.TrungBinh : 0,
                    TuTen = TuDuyNhat(k.tenKhoaHoc),
                    TuDanhMuc = TuDuyNhat(k.TenDanhMuc),
                    TuGiaoVien = TuDuyNhat(k.TenGiaoVien),
                    TuMoTaNgan = TuDuyNhat(k.moTaNgan),
                    TuMoTa = TuDuyNhat(moTa)
                };
                cm.KhoaHoc.Add(item);

                foreach (var t in item.TuTen.Concat(item.TuDanhMuc).Concat(item.TuGiaoVien).Concat(item.TuMoTaNgan).Concat(item.TuMoTa))
                {
                    int n;
                    cm.TuVung.TryGetValue(t, out n);
                    cm.TuVung[t] = n + 1;
                }
                ThemCumTuTuTen(cumTu, k.tenKhoaHoc, hv);
            }

            // Giáo viên có khóa đang bán (kèm chuyên môn, học viên, đánh giá để phân biệt người trùng tên)
            cm.GiaoVien = khoa.Where(k => k.GiaoVienHoatDong && !string.IsNullOrWhiteSpace(k.TenGiaoVien))
                .GroupBy(k => k.maGiaoVien)
                .Select(g =>
                {
                    int soDanhGia = g.Sum(x => danhGia.ContainsKey(x.maKhoaHoc) ? danhGia[x.maKhoaHoc].SoLuong : 0);
                    double tongSao = g.Sum(x => danhGia.ContainsKey(x.maKhoaHoc) ? danhGia[x.maKhoaHoc].TrungBinh * danhGia[x.maKhoaHoc].SoLuong : 0);
                    string ten = g.First().TenGiaoVien;
                    return new GiaoVienChiMuc
                    {
                        MaGiaoVien = g.Key,
                        HoTen = ten,
                        AnhDaiDien = g.First().AnhGiaoVien,
                        Tu = TuDuyNhat(ten),
                        TuCoDau = SearchText.HienThi(ten).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries),
                        HoTenKhongDau = SearchText.KhongDau(ten),
                        ChuyenMon = g.Where(x => !string.IsNullOrEmpty(x.TenDanhMuc)).GroupBy(x => x.TenDanhMuc)
                            .OrderByDescending(d => d.Count()).Select(d => d.Key).FirstOrDefault(),
                        SoKhoaHoc = g.Count(),
                        SoHocVien = g.Sum(x => soHocVien.ContainsKey(x.maKhoaHoc) ? soHocVien[x.maKhoaHoc] : 0),
                        SoDanhGia = soDanhGia,
                        DiemDanhGia = soDanhGia > 0 ? Math.Round(tongSao / soDanhGia, 1) : 0
                    };
                })
                .ToList();

            // Danh mục đang hoạt động (cùng điều kiện với trang danh mục)
            var soKhoaTheoDanhMuc = cm.KhoaHoc.Where(k => k.MaDanhMuc != null).GroupBy(k => k.MaDanhMuc).ToDictionary(g => g.Key, g => g.Count());
            foreach (var d in db.DanhMuc.Where(d => d.trangThai == null || d.trangThai == true).Select(d => new { d.maDanhMuc, d.tenDanhMuc }).ToList())
            {
                int n;
                soKhoaTheoDanhMuc.TryGetValue(d.maDanhMuc, out n);
                string kd = SearchText.KhongDau(d.tenDanhMuc);
                cm.DanhMuc.Add(new DanhMucChiMuc
                {
                    MaDanhMuc = d.maDanhMuc,
                    TenDanhMuc = d.tenDanhMuc,
                    Slug = SlugHelper.ToSlug(d.tenDanhMuc),
                    KhongDau = kd,
                    Tu = SearchText.Tach(kd),
                    SoKhoaHoc = n
                });
                if (n > 0 && kd.Length > 0)
                {
                    CumTu c;
                    if (!cumTu.TryGetValue(kd, out c))
                    {
                        c = new CumTu { HienThi = SearchText.HienThi(d.tenDanhMuc), KhongDau = kd, Tu = SearchText.Tach(kd) };
                        cumTu[kd] = c;
                    }
                    c.LaDanhMuc = true;
                    c.SoKhoa = Math.Max(c.SoKhoa, n);
                }
            }

            cm.CumTu = cumTu.Values.ToList();
            return cm;
        }

        private class TuTrongTen
        {
            public string Goc { get; set; }
            public string KhongDau { get; set; }
        }

        /// <summary>
        /// Cụm 1–3 từ liền nhau trong tên khóa: "Lập trình C# từ cơ bản" → "lập trình", "lập trình c#", "c# từ cơ bản"...
        /// Âm tiết của từ ghép (SearchText.TuGhep) được gộp trước để không cắt giữa từ ("c# từ cơ").
        /// </summary>
        private static void ThemCumTuTuTen(Dictionary<string, CumTu> cumTu, string ten, int soHocVien)
        {
            var amTiet = (ten ?? "").Split(new[] { ' ', '\t', '|', ',', ':', ';', '(', ')', '[', ']', '"', '“', '”', '–', '—' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => new TuTrongTen { Goc = SearchText.HienThi(w), KhongDau = SearchText.KhongDau(w) })
                .Where(w => w.KhongDau.Length > 0 && w.Goc.Length > 0)
                .ToList();
            var tu = new List<TuTrongTen>();
            for (int i = 0; i < amTiet.Count; i++)
            {
                if (i + 1 < amTiet.Count && SearchText.TuGhep.Contains(amTiet[i].KhongDau + " " + amTiet[i + 1].KhongDau))
                {
                    tu.Add(new TuTrongTen { Goc = amTiet[i].Goc + " " + amTiet[i + 1].Goc, KhongDau = amTiet[i].KhongDau + " " + amTiet[i + 1].KhongDau });
                    i++;
                }
                else
                {
                    tu.Add(amTiet[i]);
                }
            }
            var daThem = new HashSet<string>();
            for (int i = 0; i < tu.Count; i++)
            {
                for (int n = 1; n <= 3 && i + n <= tu.Count; n++)
                {
                    var phan = tu.Skip(i).Take(n).ToList();
                    string dau = phan[0].KhongDau, cuoi = phan[n - 1].KhongDau;
                    if (TuBienCumTu.Contains(dau) || TuBienCumTu.Contains(cuoi)) continue;
                    string kd = string.Join(" ", phan.Select(p => p.KhongDau));
                    if (n == 1 && (kd.Length < 2 || kd.All(char.IsDigit))) continue;
                    if (!daThem.Add(kd)) continue;

                    CumTu c;
                    if (!cumTu.TryGetValue(kd, out c))
                    {
                        c = new CumTu { HienThi = string.Join(" ", phan.Select(p => p.Goc)), KhongDau = kd, Tu = SearchText.Tach(kd) };
                        cumTu[kd] = c;
                    }
                    c.SoKhoa++;
                    c.PhoBien += 1 + soHocVien;
                }
            }
        }

        // ================================================================== xếp hạng khóa học

        /// <summary>Điểm liên quan của một khóa với câu tìm (đã bỏ dấu, tách từ). Trả về 0 nếu không khớp từ nào.</summary>
        private static double ChamDiem(KhoaHocChiMuc k, string[] q, string cau, out int soTuKhop)
        {
            double tong = 0;
            soTuKhop = 0;
            for (int i = 0; i < q.Length; i++)
            {
                string t = q[i];
                bool cuoi = i == q.Length - 1;
                bool tienTo = cuoi || t.Length >= 3;
                double tot = Math.Max(
                    Math.Max(10 * SearchText.KhopTotNhat(k.TuTen, t, tienTo), 6 * SearchText.KhopTotNhat(k.TuDanhMuc, t, tienTo)),
                    Math.Max(5 * SearchText.KhopTotNhat(k.TuGiaoVien, t, tienTo),
                        Math.Max(2.5 * SearchText.KhopTotNhat(k.TuMoTaNgan, t, cuoi), SearchText.KhopTotNhat(k.TuMoTa, t, cuoi, choSai: false))));
                if (tot > 0)
                {
                    soTuKhop++;
                    tong += tot;
                }
            }
            if (soTuKhop == 0) return 0;

            // Cả cụm nằm liền trong tên khóa
            if (cau.Length >= 3 && k.TenKhongDau.Contains(cau))
            {
                tong += 12;
                if (k.TenKhongDau.StartsWith(cau, StringComparison.Ordinal)) tong += 4;
            }
            // Phổ biến chỉ để phân định khóa có độ khớp gần nhau
            return tong + Math.Log(1 + k.SoHocVien) * 0.8 + k.DiemDanhGia * 0.2;
        }

        /// <summary>
        /// Khóa khớp câu tìm, điểm giảm dần. Mặc định phải khớp mọi từ; không có khóa nào thì nới thành khớp từ nửa số từ
        /// (<paramref name="motPhan"/> = true).
        /// </summary>
        public List<KeyValuePair<KhoaHocChiMuc, double>> XepHang(ChiMuc cm, string cauTim, Func<KhoaHocChiMuc, bool> boQua, out bool motPhan)
        {
            motPhan = false;
            string cau = SearchText.KhongDau(cauTim);
            var q = SearchText.TuTimKiem(cau);
            var ketQua = new List<KeyValuePair<KhoaHocChiMuc, double>>();
            if (q.Length == 0) return ketQua;

            var chamDiem = cm.KhoaHoc.Where(k => !boQua(k))
                .Select(k => { int khop; double d = ChamDiem(k, q, cau, out khop); return new { k, d, khop }; })
                .Where(x => x.d > 0)
                .ToList();

            ketQua = chamDiem.Where(x => x.khop == q.Length).Select(x => new KeyValuePair<KhoaHocChiMuc, double>(x.k, x.d)).ToList();
            if (ketQua.Count == 0 && q.Length >= 2)
            {
                int toiThieu = (q.Length + 1) / 2;
                motPhan = true;
                ketQua = chamDiem.Where(x => x.khop >= toiThieu)
                    .Select(x => new KeyValuePair<KhoaHocChiMuc, double>(x.k, x.d * x.khop / q.Length)).ToList();
            }
            return ketQua.OrderByDescending(x => x.Value).ThenByDescending(x => x.Key.SoHocVien).ToList();
        }

        /// <summary>Khóa không hiện cho người dùng: đã đăng ký, hoặc do chính họ dạy.</summary>
        public Func<KhoaHocChiMuc, bool> BoQuaKhoaDaSoHuu(int userId)
        {
            if (userId <= 0) return k => false;
            var daDangKy = new HashSet<string>(db.DangKyKhoaHoc.Where(d => d.maHocVien == userId).Select(d => d.maKhoaHoc).ToList());
            return k => k.MaGiaoVien == userId || daDangKy.Contains(k.MaKhoaHoc);
        }

        // ================================================================== gợi ý khi gõ

        public class DongTuKhoa
        {
            public string TuKhoa { get; set; }
            public string KhongDau { get; set; }
            public int SoLuot { get; set; }
        }

        public class KetQuaGoiY
        {
            public List<string> TuKhoa { get; set; } = new List<string>();
            public List<KhoaHocChiMuc> KhoaHoc { get; set; } = new List<KhoaHocChiMuc>();
            public List<KetQuaGiaoVien> GiaoVien { get; set; } = new List<KetQuaGiaoVien>();
        }

        /// <summary>Mọi từ của câu tìm đều khớp một từ của cụm (từ cuối khớp tiền tố, từ dài cho phép gõ sai).</summary>
        private static bool KhopCum(string[] tuCum, string[] q)
        {
            for (int i = 0; i < q.Length; i++)
            {
                bool cuoi = i == q.Length - 1;
                if (SearchText.KhopTotNhat(tuCum, q[i], cuoi, q[i].Length >= 4) <= 0) return false;
            }
            return true;
        }

        /// <summary>Tối đa 7 từ khóa, 5 khóa học chưa sở hữu, 3 giáo viên khớp câu đang gõ.</summary>
        public KetQuaGoiY GoiY(string cauTim, int userId)
        {
            var kq = new KetQuaGoiY();
            string cau = SearchText.KhongDau(cauTim);
            if (cau.Length == 0) return kq;
            if (cau.Length > DoDaiToiDa) cau = cau.Substring(0, DoDaiToiDa).Trim();
            var q = SearchText.Tach(cau);
            var cm = LayChiMuc();

            // --- Từ khóa: lịch sử tìm kiếm của mọi người + cụm từ trong tên khóa / danh mục
            var ung = new Dictionary<string, KeyValuePair<string, double>>();
            Action<string, string, double> them = (kd, hienThi, diem) =>
            {
                KeyValuePair<string, double> cu;
                if (!ung.TryGetValue(kd, out cu) || cu.Value < diem) ung[kd] = new KeyValuePair<string, double>(hienThi, diem);
            };

            foreach (var d in LayTuKhoaDaTim(q[0]))
            {
                var tu = SearchText.Tach(d.KhongDau);
                // Chỉ gợi ý lại câu mà mọi từ đều có thật trong dữ liệu khóa học: câu gõ sai ("pythn") hay câu rác
                // dù từng có kết quả nhờ khớp gần đúng cũng không được đưa cho người khác
                if (!tu.All(t => cm.TuVung.ContainsKey(t) || SearchText.HuTu.Contains(t))) continue;
                if (!KhopCum(tu, q)) continue;
                double diem = 4 + Math.Log(1 + d.SoLuot) * 2 + (d.KhongDau.StartsWith(cau, StringComparison.Ordinal) ? 2 : 0);
                them(d.KhongDau, d.TuKhoa, diem);
            }
            foreach (var c in cm.CumTu)
            {
                if (!KhopCum(c.Tu, q)) continue;
                double diem = 2 + Math.Log(1 + c.PhoBien) + c.SoKhoa * 0.8
                    + (c.KhongDau.StartsWith(cau, StringComparison.Ordinal) ? 1.5 : 0)
                    + (c.KhongDau == cau ? 1 : 0)
                    + (c.LaDanhMuc ? 1.5 : 0)
                    - 0.4 * c.Tu.Length;
                them(c.KhongDau, c.HienThi, diem);
            }
            kq.TuKhoa = ung.OrderByDescending(x => x.Value.Value).ThenBy(x => x.Key.Length)
                .Take(SoTuKhoaGoiY).Select(x => x.Value.Key).ToList();

            // --- Khóa học chưa mua / đăng ký
            bool motPhan;
            kq.KhoaHoc = XepHang(cm, cau, BoQuaKhoaDaSoHuu(userId), out motPhan)
                .Take(SoKhoaHocGoiY).Select(x => x.Key).ToList();

            // --- Giáo viên: tên khớp đúng hoặc gần đúng (TimGiaoVien)
            kq.GiaoVien = TimGiaoVien(cm, cauTim, SoGiaoVienGoiY);
            return kq;
        }

        // ================================================================== tìm giáo viên theo tên

        /// <summary>
        /// Giáo viên có tên khớp câu tìm, xếp theo mức khớp: đúng cả dấu → đúng khi bỏ dấu → gõ dở → cùng cách phát âm
        /// ("Tran Min" ↔ "Trần Minh") → gõ sai 1–2 ký tự / đảo chữ → giống cả cụm họ tên. Mọi chữ gõ vào phải khớp một chữ
        /// của tên (trừ mức giống cả cụm). Có người khớp đúng / gõ dở thì bỏ các kết quả gần đúng để không lẫn người khác.
        /// Cùng mức thì người nhiều học viên, đánh giá cao đứng trước.
        /// </summary>
        public List<KetQuaGiaoVien> TimGiaoVien(ChiMuc cm, string cauTim, int toiDa)
        {
            string cau = SearchText.KhongDau(cauTim);
            if (cau.Length > DoDaiToiDa) cau = cau.Substring(0, DoDaiToiDa).Trim();
            var q = SearchText.Tach(cau);
            if (q.Length == 0) return new List<KetQuaGiaoVien>();
            var qCoDau = SearchText.HienThi(cauTim).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool goCoDau = cau != string.Join(" ", qCoDau);

            var ketQua = new List<KetQuaGiaoVien>();
            foreach (var g in cm.GiaoVien)
            {
                MucKhopTen muc = MucKhopTen.DungKhongDau;
                double diem = 0;
                bool khopHet = true;
                for (int i = 0; i < q.Length; i++)
                {
                    bool tienTo = i == q.Length - 1 || q[i].Length >= 2;
                    double d = SearchText.KhopTotNhat(g.Tu, q[i], tienTo, choPhatAm: true);
                    if (d <= 0) { khopHet = false; break; }
                    diem += d;
                    var mucTu = d >= SearchText.KhopDung ? MucKhopTen.DungKhongDau
                        : d >= SearchText.KhopTienTo ? MucKhopTen.GoDo
                        : d >= SearchText.KhopPhatAm ? MucKhopTen.PhatAm : MucKhopTen.GoSai;
                    if (mucTu > muc) muc = mucTu;
                }

                if (khopHet)
                {
                    if (muc == MucKhopTen.DungKhongDau && goCoDau && qCoDau.All(t => g.TuCoDau.Contains(t)))
                        muc = MucKhopTen.DungCoDau;
                }
                else
                {
                    // Gõ thiếu / sai một âm tiết ngắn: chấp nhận khi cả cụm giống tên thật (từ 2 chữ trở lên)
                    double giong = q.Length >= 2 ? SearchText.DoGiongCum(cau, g.HoTenKhongDau) : 0;
                    if (giong < 0.6) continue;
                    muc = MucKhopTen.GiongCaCum;
                    diem = giong * q.Length;
                }
                ketQua.Add(new KetQuaGiaoVien { GiaoVien = g, Muc = muc, Diem = diem });
            }

            if (ketQua.Any(x => !x.GanDung)) ketQua = ketQua.Where(x => !x.GanDung).ToList();
            return ketQua
                .OrderBy(x => x.Muc)
                .ThenByDescending(x => x.Diem)
                .ThenByDescending(x => x.GiaoVien.SoHocVien)
                .ThenByDescending(x => x.GiaoVien.DiemDanhGia)
                .ThenBy(x => x.GiaoVien.HoTen)
                .Take(toiDa)
                .ToList();
        }

        /// <summary>Từ khóa đã có người tìm và có kết quả, chứa từ đầu tiên của câu đang gõ.</summary>
        private List<DongTuKhoa> LayTuKhoaDaTim(string tuDau)
        {
            if (!BangTuKhoaSanSang()) return new List<DongTuKhoa>();
            // tuDau đã bỏ dấu, chỉ còn a-z 0-9 + # nên an toàn trong mẫu LIKE
            return db.Database.SqlQuery<DongTuKhoa>(@"
                SELECT TOP (40) tuKhoa AS TuKhoa, tuKhoaKhongDau AS KhongDau, soLuot AS SoLuot
                FROM dbo.TuKhoaTimKiem
                WHERE soKetQua > 0 AND (tuKhoaKhongDau LIKE @dau OR tuKhoaKhongDau LIKE @giua)
                ORDER BY soLuot DESC, lanCuoiTim DESC",
                new SqlParameter("@dau", tuDau + "%"),
                new SqlParameter("@giua", "% " + tuDau + "%")).ToList();
        }

        private static bool? bangSanSang;

        private bool BangTuKhoaSanSang()
        {
            if (bangSanSang == true) return true;
            try
            {
                bangSanSang = db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.TuKhoaTimKiem', N'U') IS NULL THEN 0 ELSE 1 END").First() == 1;
            }
            catch
            {
                bangSanSang = false;
            }
            return bangSanSang == true;
        }

        /// <summary>
        /// Ghi nhận một lần tìm (khi mở trang kết quả, không ghi mỗi lần gõ phím). Mỗi phiên chỉ cộng một lượt cho một từ khóa
        /// để không bị thổi phồng khi tải lại trang. Không lưu người tìm.
        /// </summary>
        public void GhiNhanTimKiem(string cauTim, int soKetQua, HttpSessionStateBase session)
        {
            string kd = SearchText.KhongDau(cauTim);
            string hienThi = SearchText.HienThi(cauTim);
            if (kd.Length < 2 || kd.Length > DoDaiToiDa || hienThi.Length == 0 || !BangTuKhoaSanSang()) return;

            var daGhi = session?[SessionDaGhi] as HashSet<string>;
            if (daGhi == null)
            {
                daGhi = new HashSet<string>();
                if (session != null) session[SessionDaGhi] = daGhi;
            }
            if (!daGhi.Add(kd)) return;

            try
            {
                db.Database.ExecuteSqlCommand(@"
                    MERGE dbo.TuKhoaTimKiem WITH (HOLDLOCK) AS t
                    USING (SELECT @kd AS kd) AS s ON t.tuKhoaKhongDau = s.kd
                    WHEN MATCHED THEN
                        UPDATE SET soLuot = t.soLuot + 1, soKetQua = @kq, tuKhoa = @hienThi, lanCuoiTim = GETDATE()
                    WHEN NOT MATCHED THEN
                        INSERT (tuKhoaKhongDau, tuKhoa, soKetQua) VALUES (@kd, @hienThi, @kq);",
                    new SqlParameter("@kd", kd),
                    new SqlParameter("@hienThi", hienThi),
                    new SqlParameter("@kq", soKetQua));
            }
            catch (Exception ex)
            {
                // Không để lỗi thống kê làm hỏng trang tìm kiếm
                System.Diagnostics.Debug.WriteLine("Log search error: " + ex.Message);
            }
        }

        public List<string> TuKhoaPhoBien(int soLuong)
        {
            if (!BangTuKhoaSanSang()) return new List<string>();
            return db.Database.SqlQuery<string>(
                "SELECT TOP (@n) tuKhoa FROM dbo.TuKhoaTimKiem WHERE soKetQua > 0 ORDER BY soLuot DESC, lanCuoiTim DESC",
                new SqlParameter("@n", soLuong)).ToList();
        }

        // ================================================================== trang kết quả

        /// <summary>"Có phải bạn muốn tìm": thay từ không có trong dữ liệu bằng từ gần nhất (gõ sai trong giới hạn).</summary>
        private static string GoiYSuaLoi(ChiMuc cm, string cau)
        {
            var q = SearchText.Tach(cau);
            bool doi = false;
            var moi = new List<string>();
            foreach (var t in q)
            {
                if (cm.TuVung.ContainsKey(t) || SearchText.HuTu.Contains(t)) { moi.Add(t); continue; }
                int sai = Math.Max(1, SearchText.SaiSoChoPhep(t.Length));
                if (t.Length < 3) { moi.Add(t); continue; }
                var gan = cm.TuVung
                    .Select(x => new { x.Key, x.Value, kc = SearchText.KhoangCach(x.Key, t, sai) })
                    .Where(x => x.kc <= sai)
                    .OrderBy(x => x.kc).ThenByDescending(x => x.Value)
                    .FirstOrDefault();
                if (gan != null) { moi.Add(gan.Key); doi = true; }
                else moi.Add(t);
            }
            return doi ? string.Join(" ", moi) : null;
        }

        /// <summary>Kết quả trang /tim-kiem: lọc, đếm theo bộ lọc, sắp xếp, phân trang.</summary>
        public TimKiemViewModel TimKiem(TimKiemViewModel f, int userId)
        {
            f.Q = (f.Q ?? "").Trim();
            if (f.Q.Length > DoDaiToiDa) f.Q = f.Q.Substring(0, DoDaiToiDa);
            f.SapXep = new[] { "lienquan", "phobien", "danhgia", "giatang", "giagiam" }.Contains(f.SapXep) ? f.SapXep : "lienquan";
            f.Gia = f.Gia == "mienphi" || f.Gia == "cophi" ? f.Gia : null;
            if (f.Diem.HasValue && (f.Diem.Value <= 0 || f.Diem.Value > 5)) f.Diem = null;
            if (f.Trang < 1) f.Trang = 1;

            var cm = LayChiMuc();
            var boQua = BoQuaKhoaDaSoHuu(userId);

            if (SearchText.KhongDau(f.Q).Length == 0)
            {
                f.TuKhoaPhoBien = TuKhoaPhoBien(10);
                f.KhoaPhoBien = TheKhoaHoc(cm.KhoaHoc.Where(k => !boQua(k)).OrderByDescending(k => k.SoHocVien).ThenByDescending(k => k.DiemDanhGia).Take(8).ToList());
                return f;
            }

            bool motPhan;
            var xepHang = XepHang(cm, f.Q, boQua, out motPhan);
            f.KhopMotPhan = motPhan;

            // Giáo viên có tên khớp (chỉ ở trang 1): khối "Giáo viên" đầu trang, hoặc "Có phải bạn muốn tìm giáo viên…?" khi chỉ khớp gần đúng
            if (f.Trang <= 1)
            {
                var giaoVien = TimGiaoVien(cm, f.Q, SoGiaoVienKetQua);
                f.GiaoVienGanDung = giaoVien.Count > 0 && giaoVien.All(x => x.GanDung);
                f.GiaoVien = giaoVien.Select(x => new GiaoVienTimKiem
                {
                    MaGiaoVien = x.GiaoVien.MaGiaoVien,
                    HoTen = x.GiaoVien.HoTen,
                    AnhDaiDien = x.GiaoVien.AnhDaiDien,
                    ChuyenMon = x.GiaoVien.ChuyenMon,
                    SoKhoaHoc = x.GiaoVien.SoKhoaHoc,
                    SoHocVien = x.GiaoVien.SoHocVien,
                    SoDanhGia = x.GiaoVien.SoDanhGia,
                    DiemDanhGia = x.GiaoVien.DiemDanhGia
                }).ToList();
            }

            // Danh mục có tên khớp câu tìm
            string cau = SearchText.KhongDau(f.Q);
            var q = SearchText.TuTimKiem(cau);
            f.DanhMucLienQuan = cm.DanhMuc
                .Where(d => d.SoKhoaHoc > 0 && (d.KhongDau.Contains(cau) || KhopCum(d.Tu, q)))
                .OrderByDescending(d => d.SoKhoaHoc).Take(4)
                .Select(d => new CategoryLinkViewModel { Ten = d.TenDanhMuc, Slug = d.Slug, SoKhoaHoc = d.SoKhoaHoc })
                .ToList();

            // Mỗi nhóm bộ lọc đếm trên tập đã áp các bộ lọc còn lại
            Func<KhoaHocChiMuc, bool> locGia = k => f.Gia == null || (f.Gia == "mienphi" ? k.GiaHienTai == 0 : k.GiaHienTai > 0);
            Func<KhoaHocChiMuc, bool> locDiem = k => !f.Diem.HasValue || (k.SoDanhGia > 0 && k.DiemDanhGia >= f.Diem.Value);
            Func<KhoaHocChiMuc, bool> locDanhMuc = k => string.IsNullOrEmpty(f.DanhMuc) || k.MaDanhMuc == f.DanhMuc;
            Func<KhoaHocChiMuc, bool> locCapDo = k => string.IsNullOrEmpty(f.CapDo) || k.CapDo == f.CapDo;

            var tatCa = xepHang.Select(x => x.Key).ToList();
            f.BoLocDanhMuc = tatCa.Where(k => locGia(k) && locDiem(k) && locCapDo(k) && k.MaDanhMuc != null)
                .GroupBy(k => k.MaDanhMuc)
                .Select(g => new BoLocMuc { Ma = g.Key, Ten = g.First().TenDanhMuc ?? g.Key, SoLuong = g.Count() })
                .OrderByDescending(x => x.SoLuong).ToList();
            f.BoLocCapDo = tatCa.Where(k => locGia(k) && locDiem(k) && locDanhMuc(k) && !string.IsNullOrEmpty(k.CapDo))
                .GroupBy(k => k.CapDo)
                .Select(g => new BoLocMuc { Ma = g.Key, Ten = g.Key, SoLuong = g.Count() })
                .OrderBy(x => x.Ma == "Cơ bản" ? 1 : x.Ma == "Trung cấp" ? 2 : x.Ma == "Nâng cao" ? 3 : 4).ToList();

            var loc = xepHang.Where(x => locGia(x.Key) && locDiem(x.Key) && locDanhMuc(x.Key) && locCapDo(x.Key));
            switch (f.SapXep)
            {
                case "phobien": loc = loc.OrderByDescending(x => x.Key.SoHocVien).ThenByDescending(x => x.Value); break;
                case "danhgia": loc = loc.OrderByDescending(x => x.Key.DiemDanhGia).ThenByDescending(x => x.Key.SoDanhGia).ThenByDescending(x => x.Value); break;
                case "giatang": loc = loc.OrderBy(x => x.Key.GiaHienTai).ThenByDescending(x => x.Value); break;
                case "giagiam": loc = loc.OrderByDescending(x => x.Key.GiaHienTai).ThenByDescending(x => x.Value); break;
            }
            var danhSach = loc.Select(x => x.Key).ToList();

            f.TongSo = danhSach.Count;
            f.SoTrang = Math.Max(1, (int)Math.Ceiling(f.TongSo / (double)SoKetQuaMoiTrang));
            if (f.Trang > f.SoTrang) f.Trang = f.SoTrang;
            f.KetQua = TheKhoaHoc(danhSach.Skip((f.Trang - 1) * SoKetQuaMoiTrang).Take(SoKetQuaMoiTrang).ToList());

            if (xepHang.Count == 0)
            {
                f.GoiYSuaLoi = GoiYSuaLoi(cm, cau);
                f.TuKhoaPhoBien = TuKhoaPhoBien(8);
                f.KhoaPhoBien = TheKhoaHoc(cm.KhoaHoc.Where(k => !boQua(k)).OrderByDescending(k => k.SoHocVien).ThenByDescending(k => k.DiemDanhGia).Take(4).ToList());
            }
            return f;
        }

        /// <summary>Nạp thực thể cho các khóa trên trang (giữ thứ tự) để dùng thẻ khóa học dùng chung.</summary>
        private List<CourseCardViewModel> TheKhoaHoc(List<KhoaHocChiMuc> ds)
        {
            if (ds.Count == 0) return new List<CourseCardViewModel>();
            var ma = ds.Select(k => k.MaKhoaHoc).ToList();
            var entity = db.KhoaHoc.Include(k => k.NguoiDung).Include(k => k.DanhMuc)
                .Where(k => ma.Contains(k.maKhoaHoc))
                .ToDictionary(k => k.maKhoaHoc);
            return ds.Where(k => entity.ContainsKey(k.MaKhoaHoc))
                .Select(k => new CourseCardViewModel
                {
                    Course = entity[k.MaKhoaHoc],
                    SoHocVien = k.SoHocVien,
                    SoDanhGia = k.SoDanhGia,
                    DiemDanhGia = k.DiemDanhGia
                })
                .ToList();
        }
    }
}
