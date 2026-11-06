using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Hosting;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Tài liệu đính kèm bài học (bảng TaiNguyen) cho API ứng dụng: đọc thông tin tệp (tên, loại, dung lượng, ngày tải lên)
    /// và tìm đường dẫn thật trên máy chủ để tải về.
    /// Giáo viên tải tài liệu lên ở Instructor/Course (UploadLessonFile, lưu ~/Uploads/CourseResources/) và Instructor/Curriculum
    /// (AddResource, lưu ~/Uploads/Resources/). Cột loaiTaiNguyen ghi không thống nhất (".pdf" / "PDF") và tenTaiNguyen có khi thiếu
    /// đuôi tệp, nên loại tệp và tên tệp được suy ra từ đuôi của đường dẫn.
    /// </summary>
    public class LessonDocumentService
    {
        /// <summary>Đuôi tệp giáo viên được tải lên (CourseController.UploadLessonFile); tệp khác không được tải qua API.</summary>
        public static readonly string[] DuoiChoPhep = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".txt", ".csv" };

        /// <summary>Thư mục được phép đọc tệp (tránh đường dẫn lạ trong CSDL trỏ tới Web.config...).</summary>
        private static readonly string[] ThuMucChoPhep = { "/Uploads/", "/Content/Uploads/" };

        public class TaiLieu
        {
            public string MaTaiLieu { get; set; }
            public string MaBaiHoc { get; set; }
            /// <summary>Tên hiển thị (không có đuôi tệp).</summary>
            public string TenHienThi { get; set; }
            /// <summary>Tên tệp khi lưu về máy (có đuôi).</summary>
            public string TenTep { get; set; }
            /// <summary>pdf, docx, xlsx, pptx, zip, txt, csv, ... (chữ thường, không dấu chấm).</summary>
            public string LoaiTep { get; set; }
            public string Mime { get; set; }
            /// <summary>null khi không đọc được tệp (liên kết ngoài hoặc tệp đã mất).</summary>
            public long? KichThuoc { get; set; }
            /// <summary>Ngày ghi tệp trên máy chủ (bảng TaiNguyen không có cột ngày).</summary>
            public DateTime? NgayTaiLen { get; set; }
            /// <summary>Đường dẫn lưu trong CSDL.</summary>
            public string DuongDan { get; set; }
            /// <summary>Đường dẫn thật trên máy chủ, null nếu là liên kết ngoài hoặc không hợp lệ.</summary>
            public string DuongDanVatLy { get; set; }
            /// <summary>Tài liệu là đường dẫn http(s) ra ngoài: tải về thì chuyển hướng tới đó.</summary>
            public bool LaLienKetNgoai { get; set; }
            /// <summary>Có thể tải: tệp còn trên máy chủ, hoặc là liên kết ngoài.</summary>
            public bool CoSan => LaLienKetNgoai || DuongDanVatLy != null;
            /// <summary>Mở xem trực tiếp được trong ứng dụng / trình duyệt (pdf, txt, csv).</summary>
            public bool XemTrucTiep => LoaiTep == "pdf" || LoaiTep == "txt" || LoaiTep == "csv";
        }

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public LessonDocumentService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        /// <summary>Tài liệu của các bài, giữ thứ tự bài truyền vào; trong một bài sắp theo tên.</summary>
        public List<TaiLieu> LayTheoCacBai(IList<string> maBai)
        {
            if (maBai == null || maBai.Count == 0) return new List<TaiLieu>();
            var ds = maBai.ToList();
            var viTri = ds.Select((m, i) => new { m, i }).GroupBy(x => x.m).ToDictionary(g => g.Key, g => g.First().i);
            return db.TaiNguyen.Where(t => ds.Contains(t.maBaiHoc)).ToList()
                .Select(Doc)
                .OrderBy(t => viTri[t.MaBaiHoc])
                .ThenBy(t => t.TenHienThi, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(t => t.MaTaiLieu)
                .ToList();
        }

        /// <summary>Số tài liệu của mỗi bài (bài không có tài liệu không có trong kết quả).</summary>
        public Dictionary<string, int> DemTheoBai(IList<string> maBai)
        {
            if (maBai == null || maBai.Count == 0) return new Dictionary<string, int>();
            var ds = maBai.ToList();
            return db.TaiNguyen.Where(t => ds.Contains(t.maBaiHoc) && t.duongDan != null && t.duongDan.Trim() != "")
                .GroupBy(t => t.maBaiHoc)
                .Select(g => new { g.Key, SoLuong = g.Count() })
                .ToDictionary(x => x.Key, x => x.SoLuong);
        }

        public TaiLieu Lay(string maTaiLieu)
        {
            if (string.IsNullOrWhiteSpace(maTaiLieu)) return null;
            var t = db.TaiNguyen.FirstOrDefault(x => x.maTaiNguyen == maTaiLieu);
            return t == null ? null : Doc(t);
        }

        private static TaiLieu Doc(TaiNguyen t)
        {
            string duongDan = (t.duongDan ?? "").Trim();
            bool ngoai = duongDan.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || duongDan.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            string duoi = DuoiTep(ngoai ? DuongDanCuaUrl(duongDan) : duongDan);
            if (duoi.Length == 0) duoi = DuoiTuLoai(t.loaiTaiNguyen);

            string ten = (t.tenTaiNguyen ?? "").Trim();
            if (ten.Length == 0) ten = Path.GetFileNameWithoutExtension(duongDan);
            if (ten.Length == 0) ten = "Tài liệu";
            string tenHienThi = DuoiTep(ten).Equals(duoi, StringComparison.OrdinalIgnoreCase) ? ten.Substring(0, ten.Length - duoi.Length) : ten;
            if (tenHienThi.Length == 0) tenHienThi = ten;

            var tl = new TaiLieu
            {
                MaTaiLieu = t.maTaiNguyen,
                MaBaiHoc = t.maBaiHoc,
                TenHienThi = tenHienThi,
                TenTep = LamSachTenTep(tenHienThi) + duoi,
                LoaiTep = duoi.TrimStart('.').ToLowerInvariant(),
                Mime = MimeMapping.GetMimeMapping("a" + (duoi.Length > 0 ? duoi : ".bin")),
                DuongDan = duongDan,
                LaLienKetNgoai = ngoai
            };
            if (tl.LoaiTep.Length == 0) tl.LoaiTep = "khac";

            if (!ngoai)
            {
                string vatLy = DuongDanVatLy(duongDan, duoi);
                if (vatLy != null && File.Exists(vatLy))
                {
                    var fi = new FileInfo(vatLy);
                    tl.DuongDanVatLy = vatLy;
                    tl.KichThuoc = fi.Length;
                    tl.NgayTaiLen = fi.LastWriteTime;
                }
            }
            return tl;
        }

        /// <summary>"/Uploads/CourseResources/x.pdf" → đường dẫn trên ổ đĩa; null nếu nằm ngoài thư mục tải lên hoặc đuôi không được phép.</summary>
        private static string DuongDanVatLy(string duongDan, string duoi)
        {
            if (string.IsNullOrEmpty(duongDan) || !DuoiChoPhep.Contains(duoi.ToLowerInvariant())) return null;
            string ao = duongDan.StartsWith("~") ? duongDan.Substring(1) : duongDan;
            if (!ao.StartsWith("/")) ao = "/" + ao;
            int hoi = ao.IndexOfAny(new[] { '?', '#' });
            if (hoi >= 0) ao = ao.Substring(0, hoi);
            ao = HttpUtility.UrlDecode(ao);
            if (!ThuMucChoPhep.Any(d => ao.StartsWith(d, StringComparison.OrdinalIgnoreCase)) || ao.Contains("..")) return null;

            string goc = HostingEnvironment.MapPath("~/");
            string vatLy = HostingEnvironment.MapPath("~" + ao);
            if (goc == null || vatLy == null) return null;
            goc = Path.GetFullPath(goc).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            vatLy = Path.GetFullPath(vatLy);
            return vatLy.StartsWith(goc, StringComparison.OrdinalIgnoreCase) ? vatLy : null;
        }

        private static string DuongDanCuaUrl(string url)
        {
            Uri u;
            return Uri.TryCreate(url, UriKind.Absolute, out u) ? u.AbsolutePath : url;
        }

        private static string DuoiTep(string ten)
        {
            try { return (Path.GetExtension(ten ?? "") ?? "").ToLowerInvariant(); }
            catch (ArgumentException) { return ""; }
        }

        /// <summary>loaiTaiNguyen cũ: ".pdf" hoặc "PDF" / "DOCX" / "PPTX" / "ZIP".</summary>
        private static string DuoiTuLoai(string loai)
        {
            string l = (loai ?? "").Trim().TrimStart('.').ToLowerInvariant();
            return l.Length > 0 && l.Length <= 5 && l.All(char.IsLetterOrDigit) ? "." + l : "";
        }

        /// <summary>Bỏ ký tự không được dùng trong tên tệp (giữ dấu tiếng Việt).</summary>
        private static string LamSachTenTep(string ten)
        {
            var sb = new StringBuilder();
            foreach (char c in ten) sb.Append(Path.GetInvalidFileNameChars().Contains(c) ? '_' : c);
            string s = sb.ToString().Trim().TrimEnd('.');
            return s.Length == 0 ? "tai-lieu" : s;
        }

        /// <summary>Tên tệp chỉ gồm ký tự ASCII (cho filename= của Content-Disposition; tên đầy đủ đi trong filename*).</summary>
        public static string TenTepAscii(string ten)
        {
            string d = (ten ?? "").Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(c < 128 && c >= 32 && c != '"' && c != '\\' && c != ';' ? c : '_');
            }
            return sb.ToString();
        }
    }
}
