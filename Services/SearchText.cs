using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ECOLearning.Services
{
    /// <summary>
    /// Chuẩn hóa chữ cho tìm kiếm: bỏ dấu tiếng Việt, tách từ, so khớp gần đúng (khoảng cách Damerau-Levenshtein,
    /// cách phát âm tiếng Việt, độ giống cả cụm cho họ tên).
    /// "Lập trình  WEB" → "lap trinh web"; "C#", "C++" giữ nguyên ký hiệu; "ASP.NET" → "asp net".
    /// </summary>
    public static class SearchText
    {
        /// <summary>
        /// Hư từ bị bỏ qua khi so khớp. Chỉ những từ không dấu không lẫn nghĩa: không bỏ "học" (học máy),
        /// "tu" (tư duy, tự động), "den" (đến / đèn).
        /// </summary>
        public static readonly HashSet<string> HuTu = new HashSet<string>
        {
            "va", "voi", "cua", "cac", "nhung",
            "the", "a", "an", "of", "for", "to", "in", "on", "and", "with", "by"
        };

        /// <summary>
        /// Từ ghép hai âm tiết hay gặp trong tên khóa học (dạng không dấu). Dùng khi cắt cụm từ gợi ý để không cắt
        /// giữa từ ghép ("figma cơ" thay vì "figma cơ bản"). Danh sách mở rộng dần theo dữ liệu thực tế.
        /// </summary>
        public static readonly HashSet<string> TuGhep = new HashSet<string>
        {
            "lap trinh", "co ban", "nang cao", "trung cap", "can ban", "hien dai", "khoa hoc", "du lieu", "thiet ke", "do hoa",
            "giao tiep", "thuc chien", "thuc hanh", "thuc te", "nguoi moi", "bat dau", "dien thoai", "san pham", "hoc may",
            "tri tue", "nhan tao", "ung dung", "kinh doanh", "van phong", "tieng anh", "tieng nhat", "tieng han", "tieng trung",
            "ngoai ngu", "chup anh", "nhiep anh", "hinh anh", "chinh sua", "dung phim", "phat trien", "quan ly", "co so",
            "may tinh", "may chu", "an toan", "bao mat", "phan tich", "truc quan", "kiem thu", "di dong", "giao dien",
            "thiet bi", "he thong", "du an", "toan tap", "chuyen sau", "tin hoc", "ky nang", "tiep thi", "noi dung",
            "thuong mai", "dien tu", "tai chinh", "ke toan", "ngon ngu", "ban hang", "kiem tien", "cap toc", "sang tao",
            "cong nghe", "thong tin", "tu dong", "tu duy", "giai thuat", "thuat toan", "cau truc", "doi tuong", "trien khai",
            "van ban", "trinh bay", "thuyet trinh", "lanh dao", "nhan su", "nguoi dung", "trai nghiem", "toi uu", "tim kiem",
            "quang cao", "chien luoc", "thuong hieu", "kinh te", "dau tu", "chung khoan", "mang xa", "xa hoi", "luyen thi",
            "on thi", "hoc tap", "giao duc", "huong dan", "lam chu", "chuyen nghiep", "cuoc song", "suc khoe", "am nhac"
        };

        /// <summary>Không dấu, chữ thường, chỉ giữ chữ / số / + / #, mỗi từ cách một khoảng trắng.</summary>
        public static string KhongDau(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            string d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            bool khoangTrang = true;
            foreach (char c0 in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c0) == UnicodeCategory.NonSpacingMark) continue;
                char c = char.ToLowerInvariant(c0);
                if (c == 'đ') c = 'd';
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '#')
                {
                    sb.Append(c);
                    khoangTrang = false;
                }
                else if (!khoangTrang)
                {
                    sb.Append(' ');
                    khoangTrang = true;
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>Dạng hiển thị / lưu của từ khóa: chữ thường giữ dấu, bỏ ký tự lạ, gộp khoảng trắng, tối đa 100 ký tự.</summary>
        public static string HienThi(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var sb = new StringBuilder();
            bool khoangTrang = true;
            foreach (char c0 in s.Normalize(NormalizationForm.FormC))
            {
                char c = char.ToLowerInvariant(c0);
                if (char.IsLetterOrDigit(c) || c == '+' || c == '#' || c == '.' || c == '-' || c == '/' || c == '&')
                {
                    sb.Append(c);
                    khoangTrang = false;
                }
                else if (!khoangTrang)
                {
                    sb.Append(' ');
                    khoangTrang = true;
                }
            }
            string kq = sb.ToString().Trim(' ', '.', '-', '/', '&');
            return kq.Length > 100 ? kq.Substring(0, 100).Trim() : kq;
        }

        public static string[] Tach(string khongDau)
        {
            return string.IsNullOrEmpty(khongDau) ? new string[0] : khongDau.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>Từ của câu tìm kiếm, bỏ hư từ (nếu bỏ hết thì giữ nguyên).</summary>
        public static string[] TuTimKiem(string khongDau)
        {
            var tu = Tach(khongDau);
            var loc = tu.Where(t => !HuTu.Contains(t)).ToArray();
            return loc.Length > 0 ? loc : tu;
        }

        /// <summary>Số ký tự được gõ sai: từ 4–7 ký tự sai 1, từ 8 ký tự sai 2, ngắn hơn phải đúng.</summary>
        public static int SaiSoChoPhep(int doDai)
        {
            return doDai >= 8 ? 2 : doDai >= 4 ? 1 : 0;
        }

        /// <summary>
        /// Khoảng cách Damerau-Levenshtein (dạng OSA): thêm / bớt / thay một ký tự, hoặc đảo hai ký tự liền nhau ("khao" ↔ "khoa"),
        /// mỗi thao tác tính 1. Dừng sớm khi chắc chắn vượt <paramref name="toiDa"/> (trả về toiDa + 1).
        /// </summary>
        public static int KhoangCach(string a, string b, int toiDa)
        {
            if (Math.Abs(a.Length - b.Length) > toiDa) return toiDa + 1;
            var truoc2 = new int[b.Length + 1];
            var truoc = new int[b.Length + 1];
            var hienTai = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) truoc[j] = j;
            int nhoNhatTruoc = 0;
            for (int i = 1; i <= a.Length; i++)
            {
                hienTai[0] = i;
                int nhoNhat = hienTai[0];
                for (int j = 1; j <= b.Length; j++)
                {
                    int chiPhi = a[i - 1] == b[j - 1] ? 0 : 1;
                    int d = Math.Min(Math.Min(hienTai[j - 1] + 1, truoc[j] + 1), truoc[j - 1] + chiPhi);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) d = Math.Min(d, truoc2[j - 2] + 1);
                    hienTai[j] = d;
                    if (d < nhoNhat) nhoNhat = d;
                }
                // Phép đảo chữ nhìn lại hai hàng nên chỉ dừng khi cả hai hàng gần nhất đều vượt ngưỡng
                if (nhoNhat > toiDa && nhoNhatTruoc > toiDa) return toiDa + 1;
                nhoNhatTruoc = nhoNhat;
                var tam = truoc2; truoc2 = truoc; truoc = hienTai; hienTai = tam;
            }
            return truoc[b.Length] > toiDa ? toiDa + 1 : truoc[b.Length];
        }

        /// <summary>
        /// Khóa phát âm của một âm tiết tiếng Việt đã bỏ dấu, gộp các cặp hay gõ / nghe nhầm giữa các vùng miền:
        /// phụ âm đầu d / gi / r, tr / ch, s / x, ph / f, k / c, gh / g, ngh / ng; vần cuối nh / n, ng / n, ch / t, c / t; i / y.
        /// "Trần Minh Huy" và "Chan Min Hui" cùng khóa. Từ có số hoặc ký hiệu (C#, C++) giữ nguyên.
        /// </summary>
        public static string AmViet(string w)
        {
            if (string.IsNullOrEmpty(w) || w.Length < 2) return w;
            foreach (char c in w) if (c < 'a' || c > 'z') return w;
            string s = w;
            if (s.StartsWith("ngh", StringComparison.Ordinal)) s = "ng" + s.Substring(3);
            else if (s.StartsWith("gh", StringComparison.Ordinal)) s = "g" + s.Substring(2);
            else if (s.StartsWith("gi", StringComparison.Ordinal) && s.Length > 2) s = "z" + s.Substring(2);
            else if (s.StartsWith("tr", StringComparison.Ordinal)) s = "ch" + s.Substring(2);
            else if (s.StartsWith("ph", StringComparison.Ordinal)) s = "f" + s.Substring(2);
            else if (s[0] == 'd' || s[0] == 'r') s = "z" + s.Substring(1);
            else if (s[0] == 's') s = "x" + s.Substring(1);
            else if (s[0] == 'k' && !s.StartsWith("kh", StringComparison.Ordinal)) s = "c" + s.Substring(1);

            if (s.Length > 2)
            {
                if (s.EndsWith("nh", StringComparison.Ordinal) || s.EndsWith("ng", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 2) + "n";
                else if (s.EndsWith("ch", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 2) + "t";
                else if (s.EndsWith("c", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 1) + "t";
            }
            return s.Replace('y', 'i');
        }

        /// <summary>
        /// Độ giống của hai cụm (đã bỏ dấu) theo hệ số Dice trên bộ 3 ký tự liền nhau, 0–1. Dùng cho họ tên: gõ thiếu / sai một âm
        /// tiết ngắn ("tran mih khoa") vẫn giống tên thật nhiều hơn tên khác.
        /// </summary>
        public static double DoGiongCum(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;
            if (a == b) return 1;
            Func<string, List<string>> bo3 = s =>
            {
                string p = "  " + s + " ";
                var ds = new List<string>();
                for (int i = 0; i + 3 <= p.Length; i++) ds.Add(p.Substring(i, 3));
                return ds;
            };
            var x = bo3(a);
            var y = bo3(b).GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
            int chung = 0;
            foreach (var t in x)
            {
                int n;
                if (y.TryGetValue(t, out n) && n > 0) { chung++; y[t] = n - 1; }
            }
            return 2.0 * chung / (x.Count + bo3(b).Count);
        }

        public const double KhopDung = 1, KhopTienTo = 0.85, KhopPhatAm = 0.7, KhopGoSai = 0.6, KhopTienToGoSai = 0.5;

        /// <summary>
        /// Độ khớp của từ tìm kiếm <paramref name="q"/> với một từ <paramref name="w"/>: 1 = trùng, 0.85 = tiền tố
        /// (chỉ khi <paramref name="choTienTo"/>, ví dụ từ cuối đang gõ dở), 0.7 = cùng cách phát âm (AmViet, "min" ↔ "minh"),
        /// 0.6 = gõ sai trong giới hạn, 0.5 = tiền tố gõ sai, 0 = không khớp.
        /// Khớp theo phát âm chỉ bật cho họ tên (<paramref name="choPhatAm"/>): với chữ thường của tên khóa học nó gộp nhầm
        /// các từ khác nghĩa ("trình" ↔ "chỉnh", "tin" ↔ "tính").
        /// </summary>
        public static double DoKhop(string w, string q, bool choTienTo, bool choSai = true, bool choPhatAm = false)
        {
            if (w == q) return KhopDung;
            if (choTienTo && q.Length >= 1 && w.StartsWith(q, StringComparison.Ordinal)) return KhopTienTo;
            if (!choSai) return 0;
            if (choPhatAm && q.Length >= 2 && AmViet(w) == AmViet(q)) return KhopPhatAm;
            int sai = SaiSoChoPhep(q.Length);
            if (sai == 0) return 0;
            if (KhoangCach(w, q, sai) <= sai) return KhopGoSai;
            if (choTienTo && w.Length > q.Length && KhoangCach(w.Substring(0, q.Length), q, sai) <= sai) return KhopTienToGoSai;
            return 0;
        }

        /// <summary>Độ khớp tốt nhất của <paramref name="q"/> với một trong các từ.</summary>
        public static double KhopTotNhat(IEnumerable<string> tu, string q, bool choTienTo, bool choSai = true, bool choPhatAm = false)
        {
            double tot = 0;
            foreach (var w in tu)
            {
                double d = DoKhop(w, q, choTienTo, choSai, choPhatAm);
                if (d > tot) { tot = d; if (tot >= 1) break; }
            }
            return tot;
        }
    }
}
