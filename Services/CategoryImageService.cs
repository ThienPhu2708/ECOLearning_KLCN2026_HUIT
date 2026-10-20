using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Ảnh minh họa của danh mục (cột DanhMuc.anhMinhHoa, migration 012). Đọc / ghi bằng SQL vì cột thêm sau khi
    /// sinh model EF; chưa chạy migration thì IsInstalled() = false và các trang hiện biểu tượng như cũ.
    /// Ảnh tải lên được kiểm tra là ảnh thật, thu nhỏ còn tối đa 1200 px chiều ngang rồi lưu lại (bỏ mọi dữ liệu
    /// lạ đi kèm file), tên file sinh mới nên không trùng / không đoán được tên gốc của người tải.
    /// </summary>
    public class CategoryImageService
    {
        public const int DungLuongToiDaMB = 2;
        public const string ThuMucAo = "~/assets/images/categories/";
        public const string DinhDangChoPhep = ".jpg, .jpeg, .png, .webp";
        private const string DuongDanGoc = "/assets/images/categories/";
        private const int RongToiDa = 1200;

        private static readonly string[] DuoiHopLe = { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public CategoryImageService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public bool IsInstalled()
        {
            return db.Database.SqlQuery<int>(
                "SELECT CASE WHEN COL_LENGTH('dbo.DanhMuc', 'anhMinhHoa') IS NULL THEN 0 ELSE 1 END").Single() == 1;
        }

        /// <summary>Mã danh mục → đường dẫn ảnh (chỉ danh mục đã có ảnh). Chưa chạy migration thì trả về rỗng.</summary>
        public Dictionary<string, string> LayTatCa()
        {
            if (!IsInstalled())
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            return db.Database.SqlQuery<AnhDanhMuc>(
                    "SELECT maDanhMuc AS MaDanhMuc, anhMinhHoa AS DuongDan FROM dbo.DanhMuc WHERE anhMinhHoa IS NOT NULL AND anhMinhHoa <> ''")
                .ToList()
                .ToDictionary(x => x.MaDanhMuc, x => x.DuongDan, StringComparer.OrdinalIgnoreCase);
        }

        public string Lay(string maDanhMuc)
        {
            if (!IsInstalled()) return null;
            return db.Database.SqlQuery<string>(
                "SELECT anhMinhHoa FROM dbo.DanhMuc WHERE maDanhMuc = @ma",
                new SqlParameter("@ma", maDanhMuc)).FirstOrDefault();
        }

        public void CapNhat(string maDanhMuc, string duongDan)
        {
            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.DanhMuc SET anhMinhHoa = @anh WHERE maDanhMuc = @ma",
                new SqlParameter("@anh", (object)duongDan ?? DBNull.Value),
                new SqlParameter("@ma", maDanhMuc));
        }

        /// <summary>
        /// Kiểm tra và chuẩn bị ảnh tải lên (chưa ghi ra đĩa), để controller kiểm tra hết rồi mới ghi CSDL.
        /// Trả về null kèm thông báo lỗi nếu file không hợp lệ.
        /// </summary>
        public static AnhDaXuLy ChuanBi(HttpPostedFileBase file, out string loi)
        {
            loi = null;
            if (file == null || file.ContentLength == 0)
            {
                loi = "Chưa chọn ảnh.";
                return null;
            }
            if (file.ContentLength > DungLuongToiDaMB * 1024 * 1024)
            {
                loi = "Ảnh tối đa " + DungLuongToiDaMB + " MB.";
                return null;
            }
            string duoi = (Path.GetExtension(file.FileName) ?? "").ToLowerInvariant();
            if (!DuoiHopLe.Contains(duoi))
            {
                loi = "Chỉ nhận ảnh JPG, PNG hoặc WEBP.";
                return null;
            }

            byte[] du;
            using (var ms = new MemoryStream())
            {
                file.InputStream.CopyTo(ms);
                du = ms.ToArray();
            }

            // WEBP: GDI+ trên .NET Framework không đọc được, chỉ kiểm tra chữ ký file (RIFF....WEBP) rồi giữ nguyên
            if (duoi == ".webp")
            {
                bool laWebp = du.Length > 12
                    && du[0] == 'R' && du[1] == 'I' && du[2] == 'F' && du[3] == 'F'
                    && du[8] == 'W' && du[9] == 'E' && du[10] == 'B' && du[11] == 'P';
                if (!laWebp)
                {
                    loi = "File không phải ảnh WEBP hợp lệ.";
                    return null;
                }
                return new AnhDaXuLy { DuLieu = du, Duoi = ".webp" };
            }

            try
            {
                using (var ms = new MemoryStream(du))
                using (var anh = Image.FromStream(ms, true, true))
                {
                    if (anh.Width < 200 || anh.Height < 120)
                    {
                        loi = "Ảnh quá nhỏ (tối thiểu 200 × 120 px).";
                        return null;
                    }
                    bool trongSuot = (anh.Flags & (int)ImageFlags.HasAlpha) != 0;
                    int rong = Math.Min(anh.Width, RongToiDa);
                    int cao = (int)Math.Round(anh.Height * (rong / (double)anh.Width));

                    using (var moi = new Bitmap(rong, cao))
                    using (var g = Graphics.FromImage(moi))
                    using (var ra = new MemoryStream())
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        if (!trongSuot) g.Clear(Color.White);
                        g.DrawImage(anh, 0, 0, rong, cao);

                        if (trongSuot)
                        {
                            moi.Save(ra, ImageFormat.Png);
                            return new AnhDaXuLy { DuLieu = ra.ToArray(), Duoi = ".png" };
                        }
                        var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                        using (var thamSo = new EncoderParameters(1))
                        {
                            thamSo.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
                            moi.Save(ra, jpeg, thamSo);
                        }
                        return new AnhDaXuLy { DuLieu = ra.ToArray(), Duoi = ".jpg" };
                    }
                }
            }
            catch (ArgumentException)
            {
                loi = "File không phải ảnh hợp lệ.";
                return null;
            }
            catch (OutOfMemoryException)
            {
                // GDI+ báo OutOfMemory khi gặp file ảnh hỏng / sai định dạng
                loi = "File không phải ảnh hợp lệ.";
                return null;
            }
        }

        /// <summary>Ghi ảnh đã chuẩn bị ra thư mục danh mục, trả về đường dẫn để lưu CSDL.</summary>
        public static string GhiFile(AnhDaXuLy anh, string maDanhMuc, HttpServerUtilityBase server)
        {
            string thuMuc = server.MapPath(ThuMucAo);
            Directory.CreateDirectory(thuMuc);
            string ten = maDanhMuc + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + anh.Duoi;
            File.WriteAllBytes(Path.Combine(thuMuc, ten), anh.DuLieu);
            return DuongDanGoc + ten;
        }

        /// <summary>Xóa file ảnh cũ; chỉ xóa file nằm đúng trong thư mục ảnh danh mục.</summary>
        public static void XoaFile(string duongDan, HttpServerUtilityBase server)
        {
            if (string.IsNullOrWhiteSpace(duongDan) || !duongDan.StartsWith(DuongDanGoc, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            string ten = Path.GetFileName(duongDan);
            if (string.IsNullOrEmpty(ten)) return;
            try
            {
                string full = Path.Combine(server.MapPath(ThuMucAo), ten);
                if (File.Exists(full)) File.Delete(full);
            }
            catch (IOException)
            {
                // File đang bị khóa (vd. đang được đọc): bỏ qua, chỉ còn file thừa trên đĩa
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private class AnhDanhMuc
        {
            public string MaDanhMuc { get; set; }
            public string DuongDan { get; set; }
        }
    }

    public class AnhDaXuLy
    {
        public byte[] DuLieu { get; set; }
        public string Duoi { get; set; }
    }
}
