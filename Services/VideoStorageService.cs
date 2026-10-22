using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.Hosting;
using ECOLearning.Models;
using Newtonsoft.Json;

namespace ECOLearning.Services
{
    /// <summary>
    /// Lưu video bài học. Đã cấu hình R2 (Secrets.config): file tải lên nằm tạm trong App_Data/video-tmp, một hàng đợi chạy nền
    /// nén bằng App_Data/ffmpeg.exe (720p, H.264, mỗi lần một video) rồi đẩy lên R2 và xóa file tạm. Không chạy được ffmpeg, nén lỗi
    /// hoặc bản nén không nhỏ hơn thì đẩy bản gốc. BaiHoc.duongDanVideo lưu "r2:videos/{id}.mp4" ngay khi tải lên; trong lúc chờ,
    /// VideoStream/Play phát từ file tạm. Chưa cấu hình R2: lưu vào assets/uploads/videos như trước.
    /// Mỗi việc có một file {id}.job.json: ứng dụng khởi động lại (recycle, deploy) thì Application_Start chạy tiếp các việc dở.
    /// </summary>
    public static class VideoStorageService
    {
        public const string TienToR2 = "r2:";
        private const string ThuMucR2 = "videos/";
        private const string ThuMucTam = "~/App_Data/video-tmp/";
        private const string FileFfmpeg = "~/App_Data/ffmpeg.exe";
        private const string FileSqlChuyenDoi = "~/App_Data/r2-migration.sql";
        private static readonly TimeSpan GioiHanNen = TimeSpan.FromMinutes(45);
        private static readonly Regex KeyHopLe = new Regex(@"^videos/[A-Za-z0-9_-]{1,64}\.[a-z0-9]{2,4}$");

        public class CongViecVideo
        {
            public string Id { get; set; }
            public string Key { get; set; }
            /// <summary>File nguồn, đường dẫn ứng dụng (~/...).</summary>
            public string FileNguon { get; set; }
            /// <summary>true: file nguồn là file tạm, xóa khi xong. Chuyển video cũ thì giữ file gốc.</summary>
            public bool XoaNguon { get; set; }
            public string ContentType { get; set; }
            public string TenGoc { get; set; }
            public long KichThuocGoc { get; set; }
            public int? SoGiay { get; set; }
            /// <summary>Chuyển video cũ: các đường dẫn trong BaiHoc được đổi sang r2:Key sau khi tải lên xong.</summary>
            public List<string> DuongDanCu { get; set; }
            public int SoLanNen { get; set; }
            public DateTime TaoLuc { get; set; }

            [JsonIgnore] public string TrangThai { get; set; } = "Chờ xử lý";
            [JsonIgnore] public string GhiChu { get; set; }
        }

        public class KetQuaVideo
        {
            public string Key { get; set; }
            public string TenGoc { get; set; }
            public long KichThuocGoc { get; set; }
            public long KichThuocLuu { get; set; }
            public bool DaNen { get; set; }
            public bool ThanhCong { get; set; }
            public string GhiChu { get; set; }
            public DateTime Luc { get; set; }
        }

        private static readonly ConcurrentDictionary<string, CongViecVideo> DangCho = new ConcurrentDictionary<string, CongViecVideo>(StringComparer.Ordinal);
        private static readonly ConcurrentQueue<CongViecVideo> HangDoi = new ConcurrentQueue<CongViecVideo>();
        private static readonly List<KetQuaVideo> LichSu = new List<KetQuaVideo>();
        private static readonly object KhoaFileSql = new object();
        private static int dangChay;

        // ---------------------------------------------------------------- đường dẫn lưu trong BaiHoc

        public static bool LaR2(string duongDan)
        {
            return !string.IsNullOrEmpty(duongDan) && duongDan.Trim().StartsWith(TienToR2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Video tải lên hệ thống (trên máy chủ hoặc R2), không phải link ngoài.</summary>
        public static bool LaVideoTaiLen(string duongDan)
        {
            return LaR2(duongDan) || (!string.IsNullOrEmpty(duongDan) && duongDan.StartsWith(VideoDuration.ThuMucVideo, StringComparison.OrdinalIgnoreCase));
        }

        private static string KeyCua(string duongDan)
        {
            if (!LaR2(duongDan)) return null;
            string key = duongDan.Trim().Substring(TienToR2.Length);
            return KeyHopLe.IsMatch(key) ? key : null;
        }

        /// <summary>
        /// Link cho thẻ video của trình duyệt / app: video tải lên đi qua VideoStream/Play (hỗ trợ tua; video R2 được chuyển
        /// sang link ký có hạn dùng), YouTube / link ngoài giữ nguyên.
        /// </summary>
        public static string LinkPhat(string duongDan)
        {
            if (string.IsNullOrWhiteSpace(duongDan)) return "";
            string dd = duongDan.Trim();
            string play = VirtualPathUtility.ToAbsolute("~/VideoStream/Play");
            if (dd.StartsWith(VideoDuration.ThuMucVideo, StringComparison.OrdinalIgnoreCase))
                return play + "?file=" + Uri.EscapeDataString(dd.Substring(VideoDuration.ThuMucVideo.Length));
            if (LaR2(dd)) return play + "?src=" + Uri.EscapeDataString(dd);
            return dd;
        }

        /// <summary>Video R2 còn dùng được: đang chờ đẩy lên (file tạm còn) hoặc đã có trên bucket.</summary>
        public static bool TonTai(string duongDan)
        {
            string key = KeyCua(duongDan);
            if (key == null) return false;
            return FileTamDangCho(key) != null || R2Storage.LayThongTin(key) != null;
        }

        public static int? SoGiay(string duongDan)
        {
            string key = KeyCua(duongDan);
            if (key == null) return null;
            CongViecVideo cv;
            if (DangCho.TryGetValue(key, out cv)) return cv.SoGiay;
            var tt = R2Storage.LayThongTin(key);
            return tt != null ? tt.SoGiay : null;
        }

        /// <summary>Dung lượng video R2 (file tạm nếu đang chờ, object trên bucket nếu đã lên).</summary>
        public static long? KichThuoc(string duongDan)
        {
            string key = KeyCua(duongDan);
            string tam = FileTamDangCho(key);
            if (tam != null) return new FileInfo(tam).Length;
            var tt = key != null ? R2Storage.LayThongTin(key) : null;
            return tt != null ? tt.KichThuoc : (long?)null;
        }

        /// <summary>File nguồn của video đang chờ đẩy lên R2 (để phát tạm), null nếu đã xong.</summary>
        public static string FileTamDangCho(string key)
        {
            CongViecVideo cv;
            if (key == null || !DangCho.TryGetValue(key, out cv)) return null;
            string full = HostingEnvironment.MapPath(cv.FileNguon);
            return full != null && File.Exists(full) ? full : null;
        }

        public static bool LaKeyHopLe(string key)
        {
            return key != null && KeyHopLe.IsMatch(key);
        }

        // ---------------------------------------------------------------- tải lên

        /// <summary>Lưu video giáo viên tải lên, trả về giá trị lưu vào BaiHoc.duongDanVideo.</summary>
        public static string Luu(HttpPostedFileBase file)
        {
            string ext = (Path.GetExtension(file.FileName) ?? "").ToLowerInvariant();
            string id = Guid.NewGuid().ToString("N");
            if (!R2Storage.DaCauHinh)
            {
                string thuMuc = HostingEnvironment.MapPath("~" + VideoDuration.ThuMucVideo);
                Directory.CreateDirectory(thuMuc);
                file.SaveAs(Path.Combine(thuMuc, id + ext));
                return VideoDuration.ThuMucVideo + id + ext;
            }

            Directory.CreateDirectory(HostingEnvironment.MapPath(ThuMucTam));
            string nguon = ThuMucTam + id + ".src" + ext;
            string full = HostingEnvironment.MapPath(nguon);
            file.SaveAs(full);
            string thongTin;
            var cv = new CongViecVideo
            {
                Id = id,
                Key = ThuMucR2 + id + (KiemTraFfmpeg(out thongTin) ? ".mp4" : ext),
                FileNguon = nguon,
                XoaNguon = true,
                ContentType = LoaiNoiDung(ext),
                TenGoc = Path.GetFileName(file.FileName),
                KichThuocGoc = file.ContentLength,
                SoGiay = VideoDuration.DocSoGiayFile(full),
                TaoLuc = DateTime.Now
            };
            ThemCongViec(cv);
            return TienToR2 + cv.Key;
        }

        /// <summary>
        /// Đưa video cũ trong assets/uploads/videos lên R2: gom các file trùng nội dung (MD5), mỗi nội dung một việc.
        /// Tải lên xong thì đổi BaiHoc.duongDanVideo trong CSDL này và ghi câu lệnh UPDATE vào App_Data/r2-migration.sql để chạy trên hosting.
        /// </summary>
        public static string ChuyenVideoCu(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            if (!R2Storage.DaCauHinh) return "Chưa cấu hình R2 trong Secrets.config.";
            var duongDan = db.Database.SqlQuery<string>(
                "SELECT DISTINCT duongDanVideo FROM dbo.BaiHoc WHERE duongDanVideo LIKE N'/assets/uploads/videos/%'").ToList();
            var daXepHang = new HashSet<string>(DangCho.Values.Where(c => c.DuongDanCu != null).SelectMany(c => c.DuongDanCu), StringComparer.OrdinalIgnoreCase);

            var theoNoiDung = new Dictionary<string, List<string>>();
            int thieuFile = 0;
            foreach (string dd in duongDan.Where(d => !daXepHang.Contains(d)))
            {
                string ten = Path.GetFileName(dd);
                string full = string.IsNullOrEmpty(ten) || ten.Contains("..") ? null : HostingEnvironment.MapPath("~" + VideoDuration.ThuMucVideo + ten);
                if (full == null || !File.Exists(full)) { thieuFile++; continue; }
                string hash;
                using (var md5 = MD5.Create())
                using (var fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
                {
                    hash = BitConverter.ToString(md5.ComputeHash(fs));
                }
                if (!theoNoiDung.ContainsKey(hash)) theoNoiDung[hash] = new List<string>();
                theoNoiDung[hash].Add(dd);
            }

            string thongTin;
            bool coFfmpeg = KiemTraFfmpeg(out thongTin);
            foreach (var nhom in theoNoiDung.Values)
            {
                string ten = Path.GetFileName(nhom[0]);
                string ext = Path.GetExtension(ten).ToLowerInvariant();
                string id = Guid.NewGuid().ToString("N");
                string full = HostingEnvironment.MapPath("~" + VideoDuration.ThuMucVideo + ten);
                ThemCongViec(new CongViecVideo
                {
                    Id = id,
                    Key = ThuMucR2 + id + (coFfmpeg ? ".mp4" : ext),
                    FileNguon = "~" + VideoDuration.ThuMucVideo + ten,
                    XoaNguon = false,
                    ContentType = LoaiNoiDung(ext),
                    TenGoc = ten,
                    KichThuocGoc = new FileInfo(full).Length,
                    SoGiay = VideoDuration.DocSoGiayFile(full),
                    DuongDanCu = nhom,
                    TaoLuc = DateTime.Now
                });
            }

            if (theoNoiDung.Count == 0 && thieuFile == 0)
                return daXepHang.Count > 0 ? "Các video cũ đã nằm trong hàng đợi." : "Không còn bài học nào dùng video lưu trên máy chủ.";
            return "Đã xếp hàng " + theoNoiDung.Count + " video (" + theoNoiDung.Values.Sum(n => n.Count) + " đường dẫn trong bài học)"
                + (thieuFile > 0 ? "; " + thieuFile + " đường dẫn không còn file trên máy chủ nên bỏ qua" : "") + ".";
        }

        public static string DuongDanFileSql { get { return HostingEnvironment.MapPath(FileSqlChuyenDoi); } }

        // ---------------------------------------------------------------- trạng thái (trang Admin)

        public static List<CongViecVideo> DanhSachDangCho()
        {
            return DangCho.Values.OrderBy(c => c.TaoLuc).ToList();
        }

        public static List<KetQuaVideo> LichSuGanDay()
        {
            lock (LichSu) return LichSu.AsEnumerable().Reverse().ToList();
        }

        /// <summary>Đưa lại các việc bị lỗi (mạng, chưa cấu hình R2...) vào hàng đợi.</summary>
        public static int ThuLai()
        {
            int n = 0;
            foreach (var cv in DangCho.Values.Where(c => c.TrangThai == "Lỗi"))
            {
                cv.TrangThai = "Chờ xử lý";
                cv.GhiChu = null;
                HangDoi.Enqueue(cv);
                n++;
            }
            if (n > 0) ChayHangDoi();
            return n;
        }

        /// <summary>Application_Start: chạy tiếp các việc còn file .job.json, dọn file tạm không còn việc.</summary>
        public static void KhoiDong()
        {
            try
            {
                string thuMuc = HostingEnvironment.MapPath(ThuMucTam);
                if (!Directory.Exists(thuMuc)) return;
                var conViec = new HashSet<string>();
                foreach (string f in Directory.GetFiles(thuMuc, "*.job.json"))
                {
                    var cv = JsonConvert.DeserializeObject<CongViecVideo>(File.ReadAllText(f));
                    if (cv == null || !LaKeyHopLe(cv.Key)) continue;
                    conViec.Add(cv.Id);
                    if (DangCho.TryAdd(cv.Key, cv)) HangDoi.Enqueue(cv);
                }
                foreach (string f in Directory.GetFiles(thuMuc))
                {
                    string ten = Path.GetFileName(f);
                    int cham = ten.IndexOf('.');
                    if (cham > 0 && !conViec.Contains(ten.Substring(0, cham)) && File.GetLastWriteTime(f) < DateTime.Now.AddHours(-1))
                    {
                        try { File.Delete(f); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    }
                }
                if (!HangDoi.IsEmpty) ChayHangDoi();
            }
            catch (Exception ex)
            {
                AppLog.Ghi("VIDEO", "Không khôi phục được hàng đợi video: " + ex.Message);
            }
        }

        // ---------------------------------------------------------------- hàng đợi

        private static void ThemCongViec(CongViecVideo cv)
        {
            LuuJson(cv);
            DangCho[cv.Key] = cv;
            HangDoi.Enqueue(cv);
            ChayHangDoi();
        }

        private static void ChayHangDoi()
        {
            if (Interlocked.CompareExchange(ref dangChay, 1, 0) != 0) return;
            try
            {
                HostingEnvironment.QueueBackgroundWorkItem(ct => XuLyHangDoi(ct));
            }
            catch (InvalidOperationException)
            {
                // Ngoài ASP.NET (không có HostingEnvironment): chạy trên luồng nền thường
                ThreadPool.QueueUserWorkItem(_ => XuLyHangDoi(CancellationToken.None));
            }
        }

        private static void XuLyHangDoi(CancellationToken ct)
        {
            try
            {
                CongViecVideo cv;
                while (!ct.IsCancellationRequested && HangDoi.TryDequeue(out cv))
                {
                    if (!DangCho.ContainsKey(cv.Key)) continue;
                    try { XuLy(cv, ct); }
                    catch (Exception ex)
                    {
                        cv.TrangThai = "Lỗi";
                        cv.GhiChu = ex.Message;
                        AppLog.Ghi("VIDEO", "Lỗi xử lý " + cv.Key + ": " + ex);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref dangChay, 0);
                if (!HangDoi.IsEmpty && !ct.IsCancellationRequested) ChayHangDoi();
            }
        }

        private static void XuLy(CongViecVideo cv, CancellationToken ct)
        {
            string nguon = HostingEnvironment.MapPath(cv.FileNguon);
            if (nguon == null || !File.Exists(nguon))
            {
                KetThuc(cv, new KetQuaVideo { ThanhCong = false, GhiChu = "Không còn file nguồn" });
                return;
            }
            if (!R2Storage.DaCauHinh)
            {
                cv.TrangThai = "Lỗi";
                cv.GhiChu = "Chưa cấu hình R2";
                return;
            }

            string fileTaiLen = nguon, loai = cv.ContentType, ghiChu = null;
            string daNen = HostingEnvironment.MapPath(ThuMucTam + cv.Id + ".out.mp4");
            string thongTin;
            if (cv.SoLanNen >= 2)
            {
                ghiChu = "Nén thất bại 2 lần (ứng dụng khởi động lại giữa chừng), đẩy bản gốc";
            }
            else if (!KiemTraFfmpeg(out thongTin))
            {
                ghiChu = "Không nén: " + thongTin;
            }
            else
            {
                cv.SoLanNen++;
                LuuJson(cv);
                cv.TrangThai = "Đang nén";
                string loi;
                bool ok = ChayFfmpeg(nguon, daNen, ct, out loi);
                if (ct.IsCancellationRequested) { cv.TrangThai = "Chờ xử lý"; return; }
                long kichThuocNen = ok && File.Exists(daNen) ? new FileInfo(daNen).Length : 0;
                if (ok && kichThuocNen > 0 && kichThuocNen < new FileInfo(nguon).Length)
                {
                    fileTaiLen = daNen;
                    loai = "video/mp4";
                }
                else
                {
                    ghiChu = ok ? "Bản nén không nhỏ hơn bản gốc, đẩy bản gốc" : "Nén lỗi, đẩy bản gốc: " + loi;
                }
            }

            cv.SoGiay = VideoDuration.DocSoGiayFile(fileTaiLen) ?? cv.SoGiay;
            cv.TrangThai = "Đang tải lên R2";
            try
            {
                R2Storage.TaiLen(fileTaiLen, cv.Key, loai, cv.SoGiay);
            }
            catch (Exception ex)
            {
                cv.TrangThai = "Lỗi";
                cv.GhiChu = "Tải lên R2 lỗi: " + ex.Message;
                AppLog.Ghi("VIDEO", "Tải " + cv.Key + " lên R2 lỗi: " + ex.Message);
                return;
            }

            if (cv.DuongDanCu != null && cv.DuongDanCu.Count > 0) DoiDuongDanBaiHoc(cv);
            KetThuc(cv, new KetQuaVideo
            {
                ThanhCong = true,
                DaNen = fileTaiLen == daNen,
                KichThuocLuu = new FileInfo(fileTaiLen).Length,
                GhiChu = ghiChu
            });
        }

        private static void KetThuc(CongViecVideo cv, KetQuaVideo kq)
        {
            kq.Key = cv.Key;
            kq.TenGoc = cv.TenGoc;
            kq.KichThuocGoc = cv.KichThuocGoc;
            kq.Luc = DateTime.Now;
            CongViecVideo bo;
            DangCho.TryRemove(cv.Key, out bo);
            XoaFile(HostingEnvironment.MapPath(ThuMucTam + cv.Id + ".job.json"));
            XoaFile(HostingEnvironment.MapPath(ThuMucTam + cv.Id + ".out.mp4"));
            if (cv.XoaNguon) XoaFile(HostingEnvironment.MapPath(cv.FileNguon));
            lock (LichSu)
            {
                LichSu.Add(kq);
                if (LichSu.Count > 30) LichSu.RemoveAt(0);
            }
            AppLog.Ghi("VIDEO", cv.Key + (kq.ThanhCong ? " đã lên R2" : " bỏ qua") + " (" + cv.TenGoc + ", "
                + (kq.KichThuocGoc / 1048576.0).ToString("0.#") + "MB" + (kq.ThanhCong ? " -> " + (kq.KichThuocLuu / 1048576.0).ToString("0.#") + "MB" : "")
                + (string.IsNullOrEmpty(kq.GhiChu) ? "" : "; " + kq.GhiChu) + ")");
        }

        private static void DoiDuongDanBaiHoc(CongViecVideo cv)
        {
            string moi = TienToR2 + cv.Key;
            var sql = new StringBuilder();
            using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
            {
                foreach (string cu in cv.DuongDanCu)
                {
                    db.Database.ExecuteSqlCommand("UPDATE dbo.BaiHoc SET duongDanVideo = @p0 WHERE duongDanVideo = @p1", moi, cu);
                    sql.AppendLine("UPDATE dbo.BaiHoc SET duongDanVideo = N'" + moi.Replace("'", "''") + "' WHERE duongDanVideo = N'" + cu.Replace("'", "''") + "';");
                    Areas.Instructor.Controllers.ResourceController.DoiDuongDanTep(cu, moi);
                }
            }
            lock (KhoaFileSql)
            {
                string file = DuongDanFileSql;
                if (!File.Exists(file))
                {
                    File.WriteAllText(file, "-- Đổi video bài học sang Cloudflare R2 (tạo bởi Admin > Lưu trữ video). Chạy trên CSDL hosting SAU khi\r\n"
                        + "-- Secrets.config trên hosting đã có khóa R2. Chạy lại nhiều lần vẫn an toàn.\r\n", new UTF8Encoding(true));
                }
                File.AppendAllText(file, sql.ToString(), new UTF8Encoding(true));
            }
        }

        // ---------------------------------------------------------------- ffmpeg

        /// <summary>App_Data/ffmpeg.exe có chạy được không (cache theo thời điểm sửa file). thongTin: phiên bản hoặc lý do lỗi.</summary>
        public static bool KiemTraFfmpeg(out string thongTin)
        {
            string exe = HostingEnvironment.MapPath(FileFfmpeg);
            if (exe == null || !File.Exists(exe))
            {
                thongTin = "Chưa có App_Data/ffmpeg.exe";
                return false;
            }
            var fi = new FileInfo(exe);
            string khoa = "ffmpeg:" + fi.LastWriteTimeUtc.Ticks + ":" + fi.Length;
            var daCo = HttpRuntime.Cache[khoa] as Tuple<bool, string>;
            if (daCo == null)
            {
                bool ok = false;
                string tt;
                try
                {
                    var ra = new StringBuilder();
                    int? ma = ChayTienTrinh(exe, "-hide_banner -version", TimeSpan.FromSeconds(15), CancellationToken.None, ra);
                    string dau = ra.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                    ok = ma == 0 && dau.StartsWith("ffmpeg version", StringComparison.OrdinalIgnoreCase);
                    tt = ok ? dau : ma == null ? "ffmpeg.exe không phản hồi"
                        : ma == unchecked((int)0xC0000135) ? "ffmpeg.exe thiếu file DLL (bản shared); hãy dùng bản static (một file ffmpeg.exe)"
                        : "ffmpeg.exe không chạy được (mã " + ma + ")" + (dau.Length > 0 ? ": " + dau : "");
                }
                catch (Exception ex)
                {
                    tt = "Không chạy được ffmpeg.exe: " + ex.Message;
                }
                daCo = Tuple.Create(ok, tt);
                HttpRuntime.Cache.Insert(khoa, daCo, null, DateTime.UtcNow.AddMinutes(30), System.Web.Caching.Cache.NoSlidingExpiration);
            }
            thongTin = daCo.Item2;
            return daCo.Item1;
        }

        private static bool ChayFfmpeg(string nguon, string dich, CancellationToken ct, out string loi)
        {
            XoaFile(dich);
            string exe = HostingEnvironment.MapPath(FileFfmpeg);
            // 720p (không phóng to video nhỏ hơn), H.264 CRF 28, AAC 96k; faststart để phát được ngay khi mới tải một phần
            string args = "-hide_banner -nostdin -nostats -loglevel error -y -i \"" + nguon + "\" -map 0:v:0 -map 0:a:0? "
                + "-vf \"scale=-2:'min(720,trunc(ih/2)*2)'\" -c:v libx264 -preset veryfast -crf 28 -pix_fmt yuv420p "
                + "-c:a aac -b:a 96k -ac 2 -movflags +faststart -threads 2 \"" + dich + "\"";
            var ra = new StringBuilder();
            int? ma = ChayTienTrinh(exe, args, GioiHanNen, ct, ra);
            string chiTiet = ra.ToString().Trim();
            if (chiTiet.Length > 300) chiTiet = chiTiet.Substring(chiTiet.Length - 300);
            loi = ma == null ? (ct.IsCancellationRequested ? "ứng dụng đang tắt" : "quá " + GioiHanNen.TotalMinutes + " phút")
                : ma != 0 ? "mã " + ma + (chiTiet.Length > 0 ? ": " + chiTiet : "") : null;
            if (ma != 0) XoaFile(dich);
            return ma == 0;
        }

        /// <summary>Chạy tiến trình, gom stdout + stderr. Trả về mã thoát, null nếu quá giờ / bị hủy (đã kill).</summary>
        private static int? ChayTienTrinh(string exe, string args, TimeSpan gioiHan, CancellationToken ct, StringBuilder ra)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(exe)
            };
            using (var p = new Process { StartInfo = psi })
            {
                DataReceivedEventHandler gom = (s, e) => { if (e.Data != null) lock (ra) { if (ra.Length < 20000) ra.AppendLine(e.Data); } };
                p.OutputDataReceived += gom;
                p.ErrorDataReceived += gom;
                p.Start();
                try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception) { }
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                var het = DateTime.UtcNow + gioiHan;
                while (!p.WaitForExit(1000))
                {
                    if (ct.IsCancellationRequested || DateTime.UtcNow > het)
                    {
                        try { p.Kill(); } catch (Exception) { }
                        p.WaitForExit(5000);
                        return null;
                    }
                }
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        // ---------------------------------------------------------------- tiện ích

        private static void LuuJson(CongViecVideo cv)
        {
            Directory.CreateDirectory(HostingEnvironment.MapPath(ThuMucTam));
            File.WriteAllText(HostingEnvironment.MapPath(ThuMucTam + cv.Id + ".job.json"), JsonConvert.SerializeObject(cv));
        }

        private static void XoaFile(string path)
        {
            if (path == null) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public static string LoaiNoiDung(string ext)
        {
            switch ((ext ?? "").ToLowerInvariant())
            {
                case ".webm": return "video/webm";
                case ".mov": return "video/quicktime";
                case ".mkv": return "video/x-matroska";
                case ".ogv": return "video/ogg";
                default: return "video/mp4";
            }
        }
    }
}
