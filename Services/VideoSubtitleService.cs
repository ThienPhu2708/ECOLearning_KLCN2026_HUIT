using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ECOLearning.Services
{
    /// <summary>
    /// Tạo phụ đề tiếng Việt cho video bài học đã tải lên hệ thống.
    /// Job và WebVTT được lưu ở App_Data để không cần thay đổi schema CSDL. Công việc đang dở
    /// được khôi phục khi ứng dụng khởi động lại. Khóa Groq chỉ được đọc phía máy chủ.
    /// </summary>
    public static class VideoSubtitleService
    {
        public const string ChuaTao = "Chưa tạo";
        public const string DangCho = "Đang chờ";
        public const string DangTachAmThanh = "Đang tách âm thanh";
        public const string DangNhanDien = "Đang nhận diện";
        public const string SanSang = "Sẵn sàng";
        public const string Loi = "Lỗi";

        private const string ThuMuc = "~/App_Data/subtitles/";
        private const string Ffmpeg = "~/App_Data/ffmpeg.exe";
        private const long GioiHanAudioGroqFree = 24L * 1024 * 1024;
        // Whisper Turbo tối ưu ngữ cảnh khoảng 30 giây. Giữ đoạn 29 giây và chồng lấn 2 giây
        // để câu nói ở ranh giới không bị mất khi ghép phụ đề dài.
        private const double DoDaiDoanNhanDienGiay = 29d;
        private const double ChongLapDoanGiay = 2d;
        private const double DoDaiHienThiToiDaGiay = 4.5d;
        private const int SoKyTuMoiDongToiDa = 38;
        private const int SoKyTuMoiCaptionToiDa = SoKyTuMoiDongToiDa * 2;
        // On-demand Turbo giới hạn 20 RPM. 3,2 giây/request giữ dưới ngưỡng ngay cả khi video dài.
        private static readonly TimeSpan KhoangCachGuiGroq = TimeSpan.FromMilliseconds(3200);
        private const int SoLanThuLai429ToiDa = 8;
        // 520/52x là lỗi gateway tạm thời của Groq/Cloudflare, không phải video hay API key sai.
        // Chỉ retry ít lần với exponential backoff để không làm nghẽn hàng đợi phụ đề.
        private const int SoLanThuLaiLoiTamThoiToiDa = 4;
        private static readonly TimeSpan HanGiuKetQuaTam = TimeSpan.FromHours(24);
        private static readonly Regex MaHopLe = new Regex(@"^[A-Za-z0-9_-]{1,100}$", RegexOptions.Compiled);
        private static readonly ConcurrentDictionary<string, CongViecPhuDe> CongViec = new ConcurrentDictionary<string, CongViecPhuDe>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentQueue<CongViecPhuDe> HangDoi = new ConcurrentQueue<CongViecPhuDe>();
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(8) };
        private static readonly SemaphoreSlim KhoaGuiGroq = new SemaphoreSlim(1, 1);
        private static DateTime duocGuiGroqTuUtc = DateTime.MinValue;
        private static int dangChay;

        static VideoSubtitleService()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public sealed class TrangThaiPhuDe
        {
            public string TrangThai { get; set; }
            public string GhiChu { get; set; }
            public DateTime? CapNhatLuc { get; set; }
            public bool CoPhuDe { get; set; }
            public bool DangXuLy { get { return TrangThai == DangCho || TrangThai == DangTachAmThanh || TrangThai == DangNhanDien; } }
            public int TongDoan { get; set; }
            public int DaXuLyDoan { get; set; }
            public double ThoiLuongAudio { get; set; }
        }

        public sealed class KetQuaXepHang
        {
            public bool ThanhCong { get; set; }
            public string ThongBao { get; set; }
            public TrangThaiPhuDe TrangThai { get; set; }
        }

        private sealed class CongViecPhuDe
        {
            public string MaBaiHoc { get; set; }
            public string DuongDanVideo { get; set; }
            public string TrangThai { get; set; }
            public string GhiChu { get; set; }
            public DateTime TaoLuc { get; set; }
            public DateTime CapNhatLuc { get; set; }
            public int TongDoan { get; set; }
            public int DaXuLyDoan { get; set; }
            public double ThoiLuongAudio { get; set; }
        }

        /// <summary>Chấp nhận cả tên cấu hình mới lẫn GroqApiKey người dùng đã thêm trước đó.</summary>
        private static string ApiKey
        {
            get
            {
                string key = ConfigurationManager.AppSettings["Groq:SpeechApiKey"];
                if (string.IsNullOrWhiteSpace(key)) key = ConfigurationManager.AppSettings["GroqApiKey"];
                if (string.IsNullOrWhiteSpace(key)) key = ConfigurationManager.AppSettings["Groq:ApiKey"];
                return (key ?? "").Trim();
            }
        }

        private static string MoHinh
        {
            get
            {
                string model = (ConfigurationManager.AppSettings["Groq:SpeechModel"] ?? "").Trim();
                return string.IsNullOrWhiteSpace(model) ? "whisper-large-v3-turbo" : model;
            }
        }

        public static bool DaCauHinh { get { return ApiKey.Length > 0; } }

        public static TrangThaiPhuDe LayTrangThai(string maBaiHoc)
        {
            if (!MaHopLe.IsMatch(maBaiHoc ?? "")) return new TrangThaiPhuDe { TrangThai = ChuaTao };
            CongViecPhuDe cv;
            if (CongViec.TryGetValue(maBaiHoc, out cv)) return TuCongViec(cv);

            string job = DuongDanJob(maBaiHoc);
            if (File.Exists(job))
            {
                try
                {
                    cv = JsonConvert.DeserializeObject<CongViecPhuDe>(File.ReadAllText(job));
                    if (cv != null)
                    {
                        CongViec[maBaiHoc] = cv;
                        return TuCongViec(cv);
                    }
                }
                catch (Exception ex) { AppLog.Ghi("SUBTITLE", "Không đọc được job " + maBaiHoc + ": " + ex.Message); }
            }

            bool co = LaVttHoanChinh(DuongDanVtt(maBaiHoc));
            return new TrangThaiPhuDe { TrangThai = co ? SanSang : ChuaTao, CoPhuDe = co };
        }

        public static bool CoPhuDe(string maBaiHoc)
        {
            return MaHopLe.IsMatch(maBaiHoc ?? "") && LaVttHoanChinh(DuongDanVtt(maBaiHoc));
        }

        public static string LayFileVtt(string maBaiHoc)
        {
            string file = DuongDanVtt(maBaiHoc);
            return LaVttHoanChinh(file) ? file : null;
        }

        public static KetQuaXepHang XepHang(string maBaiHoc, string duongDanVideo)
        {
            if (!DaCauHinh)
                return new KetQuaXepHang { ThanhCong = false, ThongBao = "Chưa cấu hình Groq API key trong Secrets.config." };
            if (!MaHopLe.IsMatch(maBaiHoc ?? ""))
                return new KetQuaXepHang { ThanhCong = false, ThongBao = "Mã bài học không hợp lệ." };
            if (!VideoStorageService.LaVideoTaiLen(duongDanVideo))
                return new KetQuaXepHang { ThanhCong = false, ThongBao = "Chỉ hỗ trợ tạo phụ đề cho video được tải lên hệ thống, không hỗ trợ liên kết YouTube bên ngoài." };

            string thongTinFfmpeg;
            if (!VideoStorageService.KiemTraFfmpeg(out thongTinFfmpeg))
                return new KetQuaXepHang { ThanhCong = false, ThongBao = "FFmpeg chưa sẵn sàng: " + thongTinFfmpeg };

            DonDepKetQuaTam();

            CongViecPhuDe cu;
            if (CongViec.TryGetValue(maBaiHoc, out cu) && (cu.TrangThai == DangCho || cu.TrangThai == DangTachAmThanh || cu.TrangThai == DangNhanDien))
                return new KetQuaXepHang { ThanhCong = false, ThongBao = "Phụ đề đang được tạo. Vui lòng chờ hoàn tất.", TrangThai = TuCongViec(cu) };

            var cv = new CongViecPhuDe
            {
                MaBaiHoc = maBaiHoc,
                DuongDanVideo = duongDanVideo.Trim(),
                TrangThai = DangCho,
                TaoLuc = DateTime.UtcNow,
                CapNhatLuc = DateTime.UtcNow
            };
            Luu(cv);
            CongViec[maBaiHoc] = cv;
            HangDoi.Enqueue(cv);
            ChayHangDoi();
            return new KetQuaXepHang { ThanhCong = true, ThongBao = "Đã đưa video vào hàng đợi tạo phụ đề tiếng Việt.", TrangThai = TuCongViec(cv) };
        }

        /// <summary>Xóa phụ đề cũ khi giáo viên thay hoặc gỡ video.</summary>
        public static void Xoa(string maBaiHoc)
        {
            if (!MaHopLe.IsMatch(maBaiHoc ?? "")) return;
            CongViecPhuDe bo;
            CongViec.TryRemove(maBaiHoc, out bo);
            XoaFile(DuongDanVtt(maBaiHoc));
            XoaFile(DuongDanJob(maBaiHoc));
        }

        /// <summary>
        /// Video ở wizard tạo khóa học chưa có mã bài học. Sau khi phụ đề của mã tạm hoàn tất,
        /// chuyển WebVTT sang mã bài học chính thức mà không phải gọi lại Groq.
        /// </summary>
        public static bool ChuyenKetQua(string maTam, string maBaiHoc)
        {
            if (!MaHopLe.IsMatch(maTam ?? "") || !MaHopLe.IsMatch(maBaiHoc ?? "")) return false;
            if (string.Equals(maTam, maBaiHoc, StringComparison.OrdinalIgnoreCase)) return LaVttHoanChinh(DuongDanVtt(maBaiHoc));
            CongViecPhuDe dangXuLy;
            if (CongViec.TryGetValue(maTam, out dangXuLy)
                && (dangXuLy.TrangThai == DangCho || dangXuLy.TrangThai == DangTachAmThanh || dangXuLy.TrangThai == DangNhanDien)) return false;
            string nguon = DuongDanVtt(maTam);
            // Chỉ chuyển kết quả đã có dấu xác nhận rằng toàn bộ các đoạn audio đã được quét.
            // File VTT phiên bản cũ/đang dở không được phép biến thành phụ đề chính thức.
            if (!File.Exists(nguon) || !LaVttHoanChinh(nguon)) return false;
            string dich = DuongDanVtt(maBaiHoc);
            string trungGian = dich + ".move";
            try
            {
                XoaFile(trungGian);
                File.Copy(nguon, trungGian, true);
                ThayTheVttAnToan(trungGian, dich);
                XoaFile(nguon);
                XoaFile(DuongDanJob(maTam));
                return true;
            }
            finally { XoaFile(trungGian); }
        }

        /// <summary>Application_Start: khôi phục job đang dở sau khi IIS recycle/deploy.</summary>
        public static void KhoiDong()
        {
            try
            {
                string folder = DuongDanThuMuc();
                if (!Directory.Exists(folder)) return;
                DonDepKetQuaTam();
                foreach (string file in Directory.GetFiles(folder, "*.job.json"))
                {
                    CongViecPhuDe cv;
                    try { cv = JsonConvert.DeserializeObject<CongViecPhuDe>(File.ReadAllText(file)); }
                    catch { continue; }
                    if (cv == null || !MaHopLe.IsMatch(cv.MaBaiHoc ?? "")) continue;
                    CongViec[cv.MaBaiHoc] = cv;
                    if (cv.TrangThai == DangCho || cv.TrangThai == DangTachAmThanh || cv.TrangThai == DangNhanDien)
                    {
                        cv.TrangThai = DangCho;
                        cv.GhiChu = "Ứng dụng khởi động lại, đang tiếp tục xử lý.";
                        CapNhatVaLuu(cv);
                        HangDoi.Enqueue(cv);
                    }
                }
                if (!HangDoi.IsEmpty) ChayHangDoi();
            }
            catch (Exception ex) { AppLog.Ghi("SUBTITLE", "Không khôi phục được hàng đợi phụ đề: " + ex.Message); }
        }

        private static TrangThaiPhuDe TuCongViec(CongViecPhuDe cv)
        {
            bool co = LaVttHoanChinh(DuongDanVtt(cv.MaBaiHoc));
            bool dangXuLy = cv.TrangThai == DangCho || cv.TrangThai == DangTachAmThanh || cv.TrangThai == DangNhanDien;
            return new TrangThaiPhuDe
            {
                // Có thể còn VTT của lần tạo trước. Nó không được làm UI báo xong trong khi
                // công việc tạo lại đang chạy.
                TrangThai = dangXuLy ? cv.TrangThai : (co && cv.TrangThai != Loi ? SanSang : cv.TrangThai),
                GhiChu = cv.GhiChu,
                CapNhatLuc = cv.CapNhatLuc,
                CoPhuDe = co && !dangXuLy,
                TongDoan = cv.TongDoan,
                DaXuLyDoan = cv.DaXuLyDoan,
                ThoiLuongAudio = cv.ThoiLuongAudio
            };
        }

        private static void ChayHangDoi()
        {
            if (Interlocked.CompareExchange(ref dangChay, 1, 0) != 0) return;
            try { HostingEnvironment.QueueBackgroundWorkItem(ct => XuLyHangDoi(ct)); }
            catch (InvalidOperationException) { ThreadPool.QueueUserWorkItem(_ => XuLyHangDoi(CancellationToken.None)); }
        }

        private static void XuLyHangDoi(CancellationToken ct)
        {
            try
            {
                CongViecPhuDe cv;
                while (!ct.IsCancellationRequested && HangDoi.TryDequeue(out cv))
                {
                    // Job bị hủy khi giáo viên thay video sẽ bị bỏ qua, không được phép ghi lại VTT cũ.
                    if (!LaCongViecHienTai(cv)) continue;
                    try { XuLy(cv, ct); }
                    catch (Exception ex)
                    {
                        if (!LaCongViecHienTai(cv)) continue;
                        cv.TrangThai = Loi;
                        cv.GhiChu = RutGonLoi(ex.Message);
                        CapNhatVaLuu(cv);
                        AppLog.Ghi("SUBTITLE", "Lỗi tạo phụ đề " + cv.MaBaiHoc + ": " + ex);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref dangChay, 0);
                if (!HangDoi.IsEmpty && !ct.IsCancellationRequested) ChayHangDoi();
            }
        }

        private static void XuLy(CongViecPhuDe cv, CancellationToken ct)
        {
            string folder = DuongDanThuMuc();
            Directory.CreateDirectory(folder);
            string sourceTam = null, audio = null;
            try
            {
                cv.TrangThai = DangTachAmThanh;
                cv.GhiChu = null;
                CapNhatVaLuu(cv);

                string source = LayVideoNguon(cv, folder, out sourceTam);
                audio = Path.Combine(folder, cv.MaBaiHoc + ".audio.mp3");
                TachAmThanh(source, audio, ct);
                if (ct.IsCancellationRequested) throw new OperationCanceledException("Ứng dụng đang dừng.");
                if (!File.Exists(audio) || new FileInfo(audio).Length == 0) throw new InvalidOperationException("Không tách được âm thanh từ video.");
                if (new FileInfo(audio).Length > GioiHanAudioGroqFree)
                    throw new InvalidOperationException("Âm thanh sau nén vượt 24 MB. Hãy dùng video ngắn hơn hoặc chia video thành nhiều bài học.");

                cv.TrangThai = DangNhanDien;
                CapNhatVaLuu(cv);
                string vtt = NhanDienTheoDoan(audio, folder, cv, ct);
                if (string.IsNullOrWhiteSpace(vtt) || vtt == "WEBVTT\r\n") throw new InvalidOperationException("Groq không nhận được lời nói trong video.");

                // Trong lúc chờ Groq, giáo viên có thể đã thay video. Không ghi phụ đề của video cũ.
                if (!LaCongViecHienTai(cv)) return;

                string moi = DuongDanVtt(cv.MaBaiHoc) + ".new";
                File.WriteAllText(moi, vtt, new UTF8Encoding(true));
                ThayTheVttAnToan(moi, DuongDanVtt(cv.MaBaiHoc));
                cv.TrangThai = SanSang;
                cv.GhiChu = "Đã tạo phụ đề tiếng Việt.";
                cv.CapNhatLuc = DateTime.UtcNow;
                CongViecPhuDe bo;
                CongViec.TryRemove(cv.MaBaiHoc, out bo);
                XoaFile(DuongDanJob(cv.MaBaiHoc));
                AppLog.Ghi("SUBTITLE", "Đã tạo phụ đề cho " + cv.MaBaiHoc);
            }
            finally
            {
                XoaFile(audio);
                XoaFile(sourceTam);
            }
        }

        private static string LayVideoNguon(CongViecPhuDe cv, string folder, out string sourceTam)
        {
            sourceTam = null;
            string video = cv.DuongDanVideo ?? "";
            if (video.StartsWith(VideoStorageService.TienToR2, StringComparison.OrdinalIgnoreCase))
            {
                string key = video.Substring(VideoStorageService.TienToR2.Length);
                if (!VideoStorageService.LaKeyHopLe(key)) throw new InvalidOperationException("Đường dẫn video R2 không hợp lệ.");
                string dangCho = VideoStorageService.FileTamDangCho(key);
                if (!string.IsNullOrEmpty(dangCho)) return dangCho;
                sourceTam = Path.Combine(folder, cv.MaBaiHoc + ".source" + Path.GetExtension(key));
                R2Storage.TaiXuong(key, sourceTam);
                return sourceTam;
            }

            if (video.StartsWith(VideoDuration.ThuMucVideo, StringComparison.OrdinalIgnoreCase))
            {
                string ten = Path.GetFileName(video);
                if (string.IsNullOrEmpty(ten) || ten.Contains("..")) throw new InvalidOperationException("Tên file video không hợp lệ.");
                string local = HostingEnvironment.MapPath("~" + VideoDuration.ThuMucVideo + ten);
                if (string.IsNullOrEmpty(local) || !File.Exists(local)) throw new FileNotFoundException("Không tìm thấy file video trên máy chủ.");
                return local;
            }

            throw new InvalidOperationException("Chỉ hỗ trợ video được tải lên hệ thống.");
        }

        private static void TachAmThanh(string nguon, string dich, CancellationToken ct)
        {
            string exe = HostingEnvironment.MapPath(Ffmpeg);
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) throw new InvalidOperationException("Chưa có App_Data/ffmpeg.exe.");
            XoaFile(dich);
            string args = "-hide_banner -nostdin -nostats -loglevel error -y -i \"" + nguon + "\" -map 0:a:0 -vn -ac 1 -ar 16000 -c:a libmp3lame -b:a 24k \"" + dich + "\"";
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.GetDirectoryName(exe)
            };
            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                string error = process.StandardError.ReadToEnd();
                process.StandardOutput.ReadToEnd();
                DateTime den = DateTime.UtcNow.AddMinutes(30);
                while (!process.WaitForExit(500))
                {
                    if (ct.IsCancellationRequested || DateTime.UtcNow > den)
                    {
                        try { process.Kill(); } catch { }
                        throw new InvalidOperationException(ct.IsCancellationRequested ? "Ứng dụng đang dừng." : "FFmpeg tách âm thanh quá 30 phút.");
                    }
                }
                if (process.ExitCode != 0) throw new InvalidOperationException("FFmpeg không tách được âm thanh" + (string.IsNullOrWhiteSpace(error) ? "." : ": " + RutGonLoi(error)));
            }
        }

        private static async Task<string> GuiGroq(string audio, CancellationToken ct)
        {
            await KhoaGuiGroq.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                for (int lanThu = 0; lanThu <= SoLanThuLai429ToiDa; lanThu++)
                {
                    TimeSpan canCho = duocGuiGroqTuUtc - DateTime.UtcNow;
                    if (canCho > TimeSpan.Zero) await Task.Delay(canCho, ct).ConfigureAwait(false);

                    using (var form = new MultipartFormDataContent())
                    using (var file = new StreamContent(new FileStream(audio, FileMode.Open, FileAccess.Read, FileShare.Read)))
                    using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions"))
                    {
                        file.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
                        form.Add(file, "file", Path.GetFileName(audio));
                        form.Add(new StringContent(MoHinh), "model");
                        form.Add(new StringContent("vi"), "language");
                        form.Add(new StringContent("verbose_json"), "response_format");
                        form.Add(new StringContent("word"), "timestamp_granularities[]");
                        form.Add(new StringContent("segment"), "timestamp_granularities[]");
                        form.Add(new StringContent("0"), "temperature");
                        form.Add(new StringContent("Bài giảng trực tuyến bằng tiếng Việt. Giữ nguyên thuật ngữ công nghệ như Python, Java, JavaScript, C#, PHP, HTML, CSS."), "prompt");
                        request.Content = form;
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
                        request.Headers.UserAgent.ParseAdd("ECOLearning/1.0");
                        using (HttpResponseMessage response = await Http.SendAsync(request, ct).ConfigureAwait(false))
                        {
                            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (response.IsSuccessStatusCode)
                            {
                                duocGuiGroqTuUtc = DateTime.UtcNow.Add(KhoangCachGuiGroq);
                                return body;
                            }
                            int maLoi = (int)response.StatusCode;
                            if (maLoi == 429 && lanThu < SoLanThuLai429ToiDa)
                            {
                                TimeSpan choLai = LayThoiGianChoLai(response, body);
                                duocGuiGroqTuUtc = DateTime.UtcNow.Add(choLai > KhoangCachGuiGroq ? choLai : KhoangCachGuiGroq);
                                continue;
                            }
                            if (LaLoiGroqTamThoi(maLoi) && lanThu < SoLanThuLaiLoiTamThoiToiDa)
                            {
                                TimeSpan choLai = ThoiGianChoLoiTamThoi(lanThu);
                                duocGuiGroqTuUtc = DateTime.UtcNow.Add(choLai);
                                continue;
                            }
                            if (LaLoiGroqTamThoi(maLoi))
                                throw new InvalidOperationException("Dịch vụ tạo phụ đề Groq đang tạm thời không phản hồi (mã " + maLoi + "). Hệ thống đã tự thử lại nhưng chưa thành công; vui lòng tạo lại phụ đề sau vài phút.");
                            string message = body;
                            try { message = (string)JObject.Parse(body)["error"]?["message"] ?? body; } catch { }
                            throw new InvalidOperationException("Groq trả " + maLoi + ": " + RutGonLoi(message));
                        }
                    }
                }
                throw new InvalidOperationException("Groq đang giới hạn tốc độ quá lâu. Vui lòng thử lại sau.");
            }
            finally { KhoaGuiGroq.Release(); }
        }

        private static bool LaLoiGroqTamThoi(int maLoi)
        {
            return maLoi == 408 || maLoi == 500 || maLoi == 502 || maLoi == 503 || maLoi == 504
                || maLoi == 520 || maLoi == 521 || maLoi == 522 || maLoi == 523 || maLoi == 524;
        }

        private static TimeSpan ThoiGianChoLoiTamThoi(int lanThu)
        {
            int giay = Math.Min(30, 4 * (1 << Math.Min(lanThu, 3)));
            return TimeSpan.FromSeconds(giay);
        }

        private static TimeSpan LayThoiGianChoLai(HttpResponseMessage response, string body)
        {
            TimeSpan macDinh = TimeSpan.FromSeconds(4);
            if (response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue)
                return response.Headers.RetryAfter.Delta.Value.Add(TimeSpan.FromMilliseconds(200));
            var match = Regex.Match(body ?? "", @"try again in\s*(\d+)s", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            int giay;
            return match.Success && int.TryParse(match.Groups[1].Value, out giay)
                ? TimeSpan.FromSeconds(Math.Max(giay, 1) + 0.2d) : macDinh;
        }

        /// <summary>
        /// Nhận diện audio dài theo từng đoạn dưới 30 giây và cộng offset vào timestamp VTT.
        /// Gửi cả audio dài một lần khiến Whisper Large V3 Turbo chỉ trả ngữ cảnh đầu (~30 giây).
        /// </summary>
        private static string NhanDienTheoDoan(string audio, string folder, CongViecPhuDe cv, CancellationToken ct)
        {
            double tongGiay = LayThoiLuongAmThanh(audio);
            if (tongGiay <= 0) throw new InvalidOperationException("Không xác định được thời lượng audio để tạo phụ đề.");

            double buocDoan = DoDaiDoanNhanDienGiay - ChongLapDoanGiay;
            int tongDoan = Math.Max(1, (int)Math.Ceiling(Math.Max(0, tongGiay - ChongLapDoanGiay) / buocDoan));
            cv.ThoiLuongAudio = tongGiay;
            cv.TongDoan = tongDoan;
            cv.DaXuLyDoan = 0;
            cv.GhiChu = "Đang nhận diện phụ đề: đoạn 0/" + tongDoan + ".";
            CapNhatVaLuu(cv);

            var tatCaTu = new List<TuPhuDe>();
            for (int i = 0; i < tongDoan; i++)
            {
                if (ct.IsCancellationRequested) throw new OperationCanceledException("Ứng dụng đang dừng.");
                double batDau = i * buocDoan;
                string doan = Path.Combine(folder, cv.MaBaiHoc + ".part-" + i.ToString("D3") + ".mp3");
                try
                {
                    TachDoanAmThanh(audio, doan, batDau, Math.Min(DoDaiDoanNhanDienGiay, tongGiay - batDau), ct);
                    if (!File.Exists(doan) || new FileInfo(doan).Length == 0)
                        throw new InvalidOperationException("Không tạo được đoạn âm thanh " + (i + 1) + "/" + tongDoan + ".");

                    cv.GhiChu = "Đang nhận diện phụ đề: đoạn " + (i + 1) + "/" + tongDoan + ".";
                    CapNhatVaLuu(cv);
                    string json = GuiGroq(doan, ct).GetAwaiter().GetResult();
                    var tuDoan = LayTuPhuDe(json, batDau);
                    // Hai đoạn liền nhau có vùng overlap 2 giây. Bỏ từ nằm trọn trong vùng đã
                    // được đoạn trước xử lý, rồi khử thêm từ trùng timestamp sát ranh giới.
                    if (i > 0)
                        tuDoan = tuDoan.Where(t => t.KetThuc > batDau + ChongLapDoanGiay - 0.15d).ToList();
                    ThemTuKhongLap(tatCaTu, tuDoan);
                    cv.DaXuLyDoan = i + 1;
                    cv.GhiChu = "Đang nhận diện phụ đề: đoạn " + cv.DaXuLyDoan + "/" + tongDoan + ".";
                    CapNhatVaLuu(cv);
                }
                finally
                {
                    XoaFile(doan);
                }
            }
            if (cv.DaXuLyDoan != tongDoan)
                throw new InvalidOperationException("Chưa xử lý đủ tất cả đoạn âm thanh của video.");
            string noiDung = TaoNoiDungVtt(tatCaTu);
            if (string.IsNullOrWhiteSpace(noiDung)) throw new InvalidOperationException("Groq không nhận được lời nói trong video.");
            return "WEBVTT\r\nNOTE ECOLearning-Subtitle duration=" + tongGiay.ToString("0.###", CultureInfo.InvariantCulture)
                + " chunks=" + tongDoan + " completed=" + cv.DaXuLyDoan + "\r\n" + noiDung;
        }

        private static void TachDoanAmThanh(string audio, string dich, double batDau, double thoiLuong, CancellationToken ct)
        {
            if (thoiLuong <= 0) return;
            string exe = HostingEnvironment.MapPath(Ffmpeg);
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) throw new InvalidOperationException("Chưa có App_Data/ffmpeg.exe.");
            XoaFile(dich);
            // Đặt -ss sau input để cắt chính xác theo timeline MP3 (seek trước input có thể lệch ở audio VBR).
            string args = "-hide_banner -nostdin -nostats -loglevel error -y -i \"" + audio + "\" -ss " + batDau.ToString("0.###", CultureInfo.InvariantCulture)
                + " -t " + thoiLuong.ToString("0.###", CultureInfo.InvariantCulture)
                + " -vn -ac 1 -ar 16000 -c:a libmp3lame -b:a 24k \"" + dich + "\"";
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.GetDirectoryName(exe)
            };
            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                string error = process.StandardError.ReadToEnd();
                process.StandardOutput.ReadToEnd();
                DateTime den = DateTime.UtcNow.AddMinutes(5);
                while (!process.WaitForExit(500))
                {
                    if (ct.IsCancellationRequested || DateTime.UtcNow > den)
                    {
                        try { process.Kill(); } catch { }
                        throw new InvalidOperationException(ct.IsCancellationRequested ? "Ứng dụng đang dừng." : "FFmpeg cắt audio quá 5 phút.");
                    }
                }
                if (process.ExitCode != 0) throw new InvalidOperationException("FFmpeg không cắt được audio" + (string.IsNullOrWhiteSpace(error) ? "." : ": " + RutGonLoi(error)));
            }
        }

        private static double LayThoiLuongAmThanh(string audio)
        {
            string exe = HostingEnvironment.MapPath(Ffmpeg);
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return 0;
            var psi = new ProcessStartInfo(exe, "-hide_banner -nostdin -i \"" + audio + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.GetDirectoryName(exe)
            };
            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                string error = process.StandardError.ReadToEnd();
                process.StandardOutput.ReadToEnd();
                process.WaitForExit(30000);
                var match = Regex.Match(error, @"Duration:\s*(\d{2}):(\d{2}):(\d{2}(?:\.\d+)?)", RegexOptions.CultureInvariant);
                if (!match.Success) return 0;
                double giay;
                if (!double.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out giay)) return 0;
                return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 3600
                    + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 60 + giay;
            }
        }

        private sealed class TuPhuDe
        {
            public string NoiDung { get; set; }
            public double BatDau { get; set; }
            public double KetThuc { get; set; }
        }

        /// <summary>Chuyển verbose_json của Groq thành VTT ngắn, tối đa hai dòng mỗi caption.</summary>
        public static string ChuyenSangVtt(string json, double offsetGiay = 0)
        {
            string noiDung = TaoNoiDungVtt(LayTuPhuDe(json, offsetGiay));
            return "WEBVTT\r\n" + noiDung;
        }

        private static List<TuPhuDe> LayTuPhuDe(string json, double offsetGiay)
        {
            JObject data;
            try { data = JObject.Parse(json); }
            catch (JsonException) { throw new InvalidOperationException("Groq trả về dữ liệu phụ đề không hợp lệ."); }

            var ketQua = new List<TuPhuDe>();
            JArray words = data["words"] as JArray;
            if (words != null)
            {
                foreach (JToken word in words)
                {
                    double batDau, ketThuc;
                    string noiDung = ChuanHoaTu(word["word"]?.ToString());
                    if (string.IsNullOrWhiteSpace(noiDung)
                        || !double.TryParse(word["start"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out batDau)
                        || !double.TryParse(word["end"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out ketThuc)
                        || ketThuc <= batDau) continue;
                    ketQua.Add(new TuPhuDe { NoiDung = noiDung, BatDau = batDau + offsetGiay, KetThuc = ketThuc + offsetGiay });
                }
            }
            if (ketQua.Count > 0) return ketQua;

            // Fallback cho phản hồi không có word timestamp: chia đều text của segment để vẫn
            // không tạo caption khổng lồ che video.
            JArray segments = data["segments"] as JArray;
            if (segments == null) return ketQua;
            foreach (JToken segment in segments)
            {
                double batDau, ketThuc;
                if (!double.TryParse(segment["start"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out batDau)
                    || !double.TryParse(segment["end"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out ketThuc)
                    || ketThuc <= batDau) continue;
                string[] cacTu = Regex.Split((segment["text"]?.ToString() ?? "").Trim(), @"\s+")
                    .Select(ChuanHoaTu).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
                for (int i = 0; i < cacTu.Length; i++)
                {
                    double tuBatDau = batDau + (ketThuc - batDau) * i / cacTu.Length;
                    double tuKetThuc = batDau + (ketThuc - batDau) * (i + 1) / cacTu.Length;
                    ketQua.Add(new TuPhuDe { NoiDung = cacTu[i], BatDau = tuBatDau + offsetGiay, KetThuc = tuKetThuc + offsetGiay });
                }
            }
            return ketQua;
        }

        private static void ThemTuKhongLap(List<TuPhuDe> dich, IEnumerable<TuPhuDe> nguon)
        {
            foreach (TuPhuDe tu in nguon.OrderBy(t => t.BatDau))
            {
                string khoa = ChuanHoaSoSanh(tu.NoiDung);
                bool trung = dich.Skip(Math.Max(0, dich.Count - 12)).Any(cu => ChuanHoaSoSanh(cu.NoiDung) == khoa
                    && Math.Abs(cu.BatDau - tu.BatDau) < 0.45d && Math.Abs(cu.KetThuc - tu.KetThuc) < 0.6d);
                if (!trung) dich.Add(tu);
            }
        }

        private static string TaoNoiDungVtt(IEnumerable<TuPhuDe> tuNguon)
        {
            var sb = new StringBuilder();
            var cue = new List<TuPhuDe>();
            foreach (TuPhuDe tu in tuNguon.OrderBy(t => t.BatDau).ThenBy(t => t.KetThuc))
            {
                if (cue.Count > 0)
                {
                    string thu = NoiCacTu(cue.Concat(new[] { tu }));
                    double doDai = tu.KetThuc - cue[0].BatDau;
                    if ((thu.Length > SoKyTuMoiCaptionToiDa || doDai > DoDaiHienThiToiDaGiay) && cue.Count > 0)
                    {
                        ThemCueVtt(sb, cue);
                        cue.Clear();
                    }
                }
                cue.Add(tu);
                double thoiLuongCue = cue[cue.Count - 1].KetThuc - cue[0].BatDau;
                if (LaKetThucCau(tu.NoiDung) && thoiLuongCue >= 1.1d)
                {
                    ThemCueVtt(sb, cue);
                    cue.Clear();
                }
            }
            if (cue.Count > 0) ThemCueVtt(sb, cue);
            return sb.ToString();
        }

        private static void ThemCueVtt(StringBuilder sb, List<TuPhuDe> cue)
        {
            if (cue == null || cue.Count == 0) return;
            double batDau = cue[0].BatDau;
            double ketThuc = cue[cue.Count - 1].KetThuc;
            if (ketThuc <= batDau) return;
            string text = ChiaHaiDong(NoiCacTu(cue));
            if (string.IsNullOrWhiteSpace(text)) return;
            sb.Append("\r\n").Append(DinhDangThoiGian(batDau)).Append(" --> ").Append(DinhDangThoiGian(ketThuc)).Append("\r\n").Append(text).Append("\r\n");
        }

        private static string NoiCacTu(IEnumerable<TuPhuDe> cacTu)
        {
            var sb = new StringBuilder();
            foreach (TuPhuDe tu in cacTu)
            {
                string text = ChuanHoaTu(tu.NoiDung);
                if (text.Length == 0) continue;
                if (sb.Length > 0 && !LaDauCauDinhTruoc(text) && !LaKyTuMoNgoac(sb[sb.Length - 1])) sb.Append(' ');
                sb.Append(text);
            }
            return sb.ToString();
        }

        private static string ChiaHaiDong(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= SoKyTuMoiDongToiDa) return text;
            int viTri = text.LastIndexOf(' ', Math.Min(SoKyTuMoiDongToiDa, text.Length - 1));
            if (viTri <= 0 || text.Length - viTri - 1 > SoKyTuMoiDongToiDa) return text;
            return text.Substring(0, viTri) + "\n" + text.Substring(viTri + 1);
        }

        private static string ChuanHoaTu(string text) { return Regex.Replace((text ?? "").Trim(), @"\s+", " ").Replace("-->", "→"); }
        private static string ChuanHoaSoSanh(string text) { return ChuanHoaTu(text).ToLowerInvariant(); }
        private static bool LaKetThucCau(string text) { return Regex.IsMatch(text ?? "", @"[.!?…]$", RegexOptions.CultureInvariant); }
        private static bool LaDauCauDinhTruoc(string text) { return Regex.IsMatch(text ?? "", @"^[,.;:!?…%)\]\}]", RegexOptions.CultureInvariant); }
        private static bool LaKyTuMoNgoac(char value) { return value == '(' || value == '[' || value == '{' || value == '“'; }

        private static string DinhDangThoiGian(double giay)
        {
            if (giay < 0) giay = 0;
            long ms = (long)Math.Round(giay * 1000);
            long hour = ms / 3600000;
            ms %= 3600000;
            long minute = ms / 60000;
            ms %= 60000;
            long second = ms / 1000;
            ms %= 1000;
            return hour.ToString("D2") + ":" + minute.ToString("D2") + ":" + second.ToString("D2") + "." + ms.ToString("D3");
        }

        private static string DuongDanThuMuc()
        {
            string folder = HostingEnvironment.MapPath(ThuMuc);
            if (string.IsNullOrEmpty(folder)) throw new InvalidOperationException("Không xác định được thư mục App_Data/subtitles.");
            return folder;
        }

        private static string DuongDanVtt(string maBaiHoc) { return Path.Combine(DuongDanThuMuc(), (maBaiHoc ?? "") + ".vi.vtt"); }
        private static string DuongDanJob(string maBaiHoc) { return Path.Combine(DuongDanThuMuc(), (maBaiHoc ?? "") + ".job.json"); }

        private static bool LaVttHoanChinh(string file)
        {
            try
            {
                string dauFile = File.ReadAllText(file, Encoding.UTF8);
                var match = Regex.Match(dauFile, @"NOTE\s+ECOLearning-Subtitle\s+duration=[0-9.]+\s+chunks=(\d+)\s+completed=(\d+)", RegexOptions.CultureInvariant);
                if (!match.Success) return false;
                int tong, daXong;
                return int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out tong)
                    && int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out daXong)
                    && tong > 0 && daXong == tong;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        /// <summary>
        /// Lưu đúng một VTT chính thức cho mỗi bài học. File cũ vẫn an toàn nếu ứng dụng dừng
        /// ngay trong lúc thay thế; file mới chỉ xuất hiện khi đã ghi hoàn chỉnh.
        /// </summary>
        private static void ThayTheVttAnToan(string fileMoi, string fileDich)
        {
            if (File.Exists(fileDich)) File.Replace(fileMoi, fileDich, null);
            else File.Move(fileMoi, fileDich);
        }

        /// <summary>
        /// Token upload là GUID 32 ký tự, khác với mã bài học. VTT tạm không còn job và quá 24 giờ
        /// được xem là bản nháp bỏ dở; chỉ xóa chúng, không bao giờ xóa VTT chính thức của bài học.
        /// </summary>
        private static void DonDepKetQuaTam()
        {
            try
            {
                string folder = DuongDanThuMuc();
                if (!Directory.Exists(folder)) return;
                DateTime han = DateTime.UtcNow.Subtract(HanGiuKetQuaTam);
                const string duoiVtt = ".vi.vtt";
                foreach (string vtt in Directory.GetFiles(folder, "*.vi.vtt"))
                {
                    string ten = Path.GetFileName(vtt);
                    if (!ten.EndsWith(duoiVtt, StringComparison.OrdinalIgnoreCase)) continue;
                    string maTam = ten.Substring(0, ten.Length - duoiVtt.Length);
                    Guid boQua;
                    if (!Guid.TryParseExact(maTam, "N", out boQua)) continue;
                    if (File.Exists(DuongDanJob(maTam)) || File.GetLastWriteTimeUtc(vtt) >= han) continue;
                    XoaFile(vtt);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void Luu(CongViecPhuDe cv)
        {
            Directory.CreateDirectory(DuongDanThuMuc());
            File.WriteAllText(DuongDanJob(cv.MaBaiHoc), JsonConvert.SerializeObject(cv), new UTF8Encoding(true));
        }

        private static void CapNhatVaLuu(CongViecPhuDe cv)
        {
            cv.CapNhatLuc = DateTime.UtcNow;
            Luu(cv);
        }

        private static bool LaCongViecHienTai(CongViecPhuDe cv)
        {
            CongViecPhuDe hienTai;
            return cv != null && CongViec.TryGetValue(cv.MaBaiHoc, out hienTai) && object.ReferenceEquals(hienTai, cv);
        }

        private static string RutGonLoi(string text)
        {
            text = (text ?? "").Trim();
            return text.Length > 600 ? text.Substring(0, 600) : text;
        }

        private static void XoaFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
