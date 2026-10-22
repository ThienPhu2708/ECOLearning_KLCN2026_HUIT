using System;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;

namespace ECOLearning.Services
{
    /// <summary>
    /// Đọc thời lượng thật (giây) của video MP4 / MOV / M4V nằm trong assets/uploads/videos, từ hộp "mvhd" của file.
    /// Chỉ đọc phần đầu các hộp cấp cao nhất và hộp "moov" (vài KB đến vài MB), không đọc cả file video.
    /// Kết quả được cache theo đường dẫn + thời điểm sửa file. Không đọc được (YouTube, định dạng khác, file hỏng) thì trả về null.
    /// Dùng để lưu tiến độ học theo độ dài video thật: cột BaiHoc.thoiLuong của dữ liệu cũ có thể lệch xa video đã tải lên.
    /// </summary>
    public static class VideoDuration
    {
        public const string ThuMucVideo = "/assets/uploads/videos/";
        private const long MoovToiDa = 64L * 1024 * 1024;

        public static int? LaySoGiay(string duongDanVideo)
        {
            if (string.IsNullOrWhiteSpace(duongDanVideo)) return null;
            // Video trên R2: độ dài lưu khi tải lên (hàng đợi hoặc metadata của object)
            if (duongDanVideo.StartsWith(VideoStorageService.TienToR2, StringComparison.OrdinalIgnoreCase))
            {
                return VideoStorageService.SoGiay(duongDanVideo);
            }
            if (!duongDanVideo.StartsWith(ThuMucVideo, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            string ten = Path.GetFileName(duongDanVideo);
            string duoi = (Path.GetExtension(ten) ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(ten) || ten.Contains("..") || (duoi != ".mp4" && duoi != ".m4v" && duoi != ".mov"))
            {
                return null;
            }

            string full = HostingEnvironment.MapPath("~" + ThuMucVideo + ten);
            if (full == null || !File.Exists(full)) return null;

            string khoa = "videodur:" + full.ToLowerInvariant() + ":" + File.GetLastWriteTimeUtc(full).Ticks;
            var daCo = HttpRuntime.Cache[khoa] as int?;
            if (daCo.HasValue) return daCo.Value > 0 ? daCo : null;

            int ketQua = 0;
            try
            {
                double? giay = DocMp4(full);
                if (giay.HasValue && giay.Value > 0 && giay.Value < 24 * 3600) ketQua = (int)Math.Round(giay.Value);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            HttpRuntime.Cache.Insert(khoa, ketQua, null, DateTime.UtcNow.AddHours(6), Cache.NoSlidingExpiration);
            return ketQua > 0 ? (int?)ketQua : null;
        }

        /// <summary>Độ dài (giây) của một file MP4 / MOV / M4V bất kỳ trên đĩa, null nếu không đọc được. Không cache.</summary>
        public static int? DocSoGiayFile(string duongDanFile)
        {
            try
            {
                double? giay = DocMp4(duongDanFile);
                if (giay.HasValue && giay.Value > 0 && giay.Value < 24 * 3600) return (int)Math.Round(giay.Value);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }

        private static double? DocMp4(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var br = new BinaryReader(fs))
            {
                long len = fs.Length;
                while (fs.Position + 8 <= len)
                {
                    long batDau = fs.Position;
                    long size = DocUInt32(br);
                    string type = Encoding.ASCII.GetString(br.ReadBytes(4));
                    long header = 8;
                    if (size == 1) { size = (long)DocUInt64(br); header = 16; }
                    else if (size == 0) { size = len - batDau; }
                    if (size < header) return null;

                    if (type == "moov")
                    {
                        long noiDung = size - header;
                        if (noiDung <= 0 || noiDung > MoovToiDa) return null;
                        return TimMvhd(br.ReadBytes((int)noiDung));
                    }
                    fs.Position = batDau + size;
                }
            }
            return null;
        }

        // Hộp mvhd: version(1) flags(3), v0: creation(4) modification(4) timescale(4) duration(4); v1: 8 8 4 8
        private static double? TimMvhd(byte[] moov)
        {
            int pos = 0;
            while (pos + 8 <= moov.Length)
            {
                long size = DocUInt32(moov, pos);
                string type = Encoding.ASCII.GetString(moov, pos + 4, 4);
                if (size < 8 || pos + size > moov.Length) return null;
                if (type == "mvhd")
                {
                    int p = pos + 8;
                    byte version = moov[p];
                    if (version == 1)
                    {
                        if (p + 32 > moov.Length) return null;
                        long timescale = DocUInt32(moov, p + 20);
                        ulong duration = DocUInt64(moov, p + 24);
                        return timescale > 0 ? duration / (double)timescale : (double?)null;
                    }
                    if (p + 20 > moov.Length) return null;
                    long ts = DocUInt32(moov, p + 12);
                    long dur = DocUInt32(moov, p + 16);
                    return ts > 0 ? dur / (double)ts : (double?)null;
                }
                pos += (int)size;
            }
            return null;
        }

        private static long DocUInt32(BinaryReader br)
        {
            byte[] b = br.ReadBytes(4);
            return b.Length < 4 ? 0 : DocUInt32(b, 0);
        }

        private static ulong DocUInt64(BinaryReader br)
        {
            byte[] b = br.ReadBytes(8);
            return b.Length < 8 ? 0 : DocUInt64(b, 0);
        }

        private static long DocUInt32(byte[] b, int i)
        {
            return ((long)b[i] << 24) | ((long)b[i + 1] << 16) | ((long)b[i + 2] << 8) | b[i + 3];
        }

        private static ulong DocUInt64(byte[] b, int i)
        {
            return ((ulong)DocUInt32(b, i) << 32) | (ulong)DocUInt32(b, i + 4);
        }
    }
}
