using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ECOLearning.Services
{
    /// <summary>
    /// Lỗi khi gọi Gemini. <see cref="HetHanMuc"/>: mọi mô hình đều hết hạn mức (429); <see cref="TamThoi"/>: quá tải / mất kết nối
    /// (báo "đang bận"); còn lại là lỗi cấu hình / yêu cầu (sai khóa, sai định dạng).
    /// </summary>
    public class GeminiException : Exception
    {
        public int MaTrangThai { get; private set; }
        public bool TamThoi { get; private set; }
        public bool HetHanMuc => MaTrangThai == 429;

        public GeminiException(string message, int maTrangThai, bool tamThoi) : base(message)
        {
            MaTrangThai = maTrangThai;
            TamThoi = tamThoi;
        }
    }

    public class GeminiKetQua
    {
        public JObject Body { get; set; }
        public string Model { get; set; }
        /// <summary>true: mô hình chính không dùng được nên câu này do một mô hình dự phòng trả lời.</summary>
        public bool DuPhong { get; set; }
    }

    /// <summary>
    /// Gọi Gemini API (generateContent) qua HttpClient. Cấu hình ở Secrets.config:
    ///   Gemini:ApiKey, Gemini:Model (mô hình chính), Gemini:FallbackModel (dự phòng; có thể ghi nhiều tên, ngăn cách bằng dấu phẩy),
    ///   Gemini:ThinkingLevel (low mặc định).
    /// Thứ tự thử: mô hình chính, rồi lần lượt các mô hình dự phòng.
    ///   - 5xx / hết giờ / mất kết nối (quá tải tạm thời): thử lại tối đa 3 lần (chờ 1 s, 2 s), vẫn lỗi thì sang mô hình kế.
    ///   - 429 (hết hạn mức; gói miễn phí chỉ cho khoảng 20 yêu cầu mỗi ngày cho mỗi mô hình): sang mô hình kế ngay và nhớ để
    ///     không gọi lại mô hình đó cho tới khi hết thời gian Google báo (tối đa 30 phút, sau đó thử lại).
    ///   - 404 (mô hình không còn): sang mô hình kế ngay.
    ///   - 400 / 401 / 403 (sai yêu cầu hoặc khóa): báo ngay, đổi mô hình cũng không khác.
    /// Có cấu hình Groq (<see cref="GroqClient"/>) thì khi mọi mô hình Gemini đều lỗi, câu hỏi được chuyển sang Groq.
    /// </summary>
    public class GeminiClient
    {
        private const string DiaChi = "https://generativelanguage.googleapis.com/v1beta/models/";
        private const int SoLanThuMoiMoHinh = 3;
        private static readonly TimeSpan NganSachThoiGian = TimeSpan.FromSeconds(70);
        private static readonly TimeSpan NghiToiDaKhiHetHanMuc = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan NghiMacDinhKhiHetHanMuc = TimeSpan.FromMinutes(1);

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        /// <summary>Mô hình đã hết hạn mức -> thời điểm được thử lại.</summary>
        private static readonly ConcurrentDictionary<string, DateTime> TamNghi = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        static GeminiClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        private readonly string apiKey;
        public string MoHinhChinh { get; private set; }
        public List<string> MoHinhDuPhong { get; private set; }
        /// <summary>low / medium / high ("" = mặc định của mô hình).</summary>
        public string MucSuyLuan { get; private set; }

        public GeminiClient()
        {
            apiKey = ConfigurationManager.AppSettings["Gemini:ApiKey"];
            MoHinhChinh = Doc("Gemini:Model", "gemini-3.5-flash");
            MoHinhDuPhong = Doc("Gemini:FallbackModel", "gemini-3.5-flash-lite")
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x.Length > 0 && !x.Equals(MoHinhChinh, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string suyLuan = ConfigurationManager.AppSettings["Gemini:ThinkingLevel"];
            MucSuyLuan = suyLuan == null ? "low" : suyLuan.Trim();
        }

        public bool DaCauHinh => !string.IsNullOrWhiteSpace(apiKey);

        private static string Doc(string khoa, string macDinh)
        {
            string v = ConfigurationManager.AppSettings[khoa];
            return string.IsNullOrWhiteSpace(v) ? macDinh : v.Trim();
        }

        /// <summary>
        /// contents: lịch sử + câu hỏi (định dạng Gemini: role + parts). tools: [{functionDeclarations: [...]}] hoặc null.
        /// </summary>
        public async Task<GeminiKetQua> TaoNoiDungAsync(JArray contents, string huongDan, JArray tools)
        {
            var groq = new GroqClient();
            if (!groq.DaCauHinh) return await TaoNoiDungGeminiAsync(contents, huongDan, tools);

            // Có Groq: Gemini rồi tới Groq (Groq:Primary = true thì đảo lại). Cả hai cùng lỗi thì báo lỗi của bên được thử đầu tiên.
            var thuTu = new List<Func<Task<GeminiKetQua>>>();
            if (DaCauHinh) thuTu.Add(() => TaoNoiDungGeminiAsync(contents, huongDan, tools));
            var goiGroq = new Func<Task<GeminiKetQua>>(() => groq.TaoNoiDungAsync(contents, huongDan, tools));
            if (groq.UuTien) thuTu.Insert(0, goiGroq); else thuTu.Add(goiGroq);

            GeminiException loiDau = null;
            for (int i = 0; i < thuTu.Count; i++)
            {
                try
                {
                    var kq = await thuTu[i]();
                    if (i > 0) kq.DuPhong = true;
                    return kq;
                }
                catch (GeminiException ex)
                {
                    System.Diagnostics.Trace.TraceWarning("Chatbot: " + ex.Message);
                    loiDau = loiDau ?? ex;
                }
            }
            throw loiDau;
        }

        private async Task<GeminiKetQua> TaoNoiDungGeminiAsync(JArray contents, string huongDan, JArray tools)
        {
            if (!DaCauHinh) throw new GeminiException("Chưa cấu hình Gemini:ApiKey trong Secrets.config.", 0, false);

            var body = new JObject { ["contents"] = contents };
            if (!string.IsNullOrWhiteSpace(huongDan))
                body["systemInstruction"] = new JObject { ["parts"] = new JArray(new JObject { ["text"] = huongDan }) };
            if (tools != null && tools.Count > 0) body["tools"] = tools;
            // Mức suy luận thấp: chat nhanh hơn khoảng 2-3 lần mà vẫn gọi công cụ đúng (đo 27/10/2026).
            if (!string.IsNullOrEmpty(MucSuyLuan))
                body["generationConfig"] = new JObject { ["thinkingConfig"] = new JObject { ["thinkingLevel"] = MucSuyLuan } };
            string json = body.ToString(Newtonsoft.Json.Formatting.None);

            var dongHo = Stopwatch.StartNew();
            GeminiException loiCuoi = null;
            var dsMoHinh = new List<string> { MoHinhChinh };
            dsMoHinh.AddRange(MoHinhDuPhong);

            for (int i = 0; i < dsMoHinh.Count; i++)
            {
                string model = dsMoHinh[i];
                DateTime den;
                if (TamNghi.TryGetValue(model, out den) && den > DateTime.UtcNow)
                {
                    loiCuoi = loiCuoi ?? new GeminiException("Gemini " + model + " đang hết hạn mức.", 429, true);
                    continue;
                }

                for (int lan = 1; lan <= SoLanThuMoiMoHinh && dongHo.Elapsed <= NganSachThoiGian; lan++)
                {
                    try
                    {
                        var kq = await GoiMotLanAsync(model, json);
                        return new GeminiKetQua { Body = kq, Model = model, DuPhong = i > 0 };
                    }
                    catch (GeminiException ex)
                    {
                        loiCuoi = ex;
                        if (ex.MaTrangThai == 429 || ex.MaTrangThai == 404) break;      // sang mô hình kế ngay
                        if (!ex.TamThoi) throw;                                          // 400 / 401 / 403
                        if (lan < SoLanThuMoiMoHinh) await Task.Delay(TimeSpan.FromSeconds(lan)); // 1 s, 2 s
                    }
                }
            }
            throw loiCuoi ?? new GeminiException("Gemini không phản hồi.", 0, true);
        }

        private async Task<JObject> GoiMotLanAsync(string model, string json)
        {
            var yc = new HttpRequestMessage(HttpMethod.Post, DiaChi + Uri.EscapeDataString(model) + ":generateContent")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            yc.Headers.Add("x-goog-api-key", apiKey);

            HttpResponseMessage tl;
            try
            {
                tl = await Http.SendAsync(yc).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                throw new GeminiException("Gemini phản hồi quá lâu (" + model + ").", 0, true);
            }
            catch (HttpRequestException ex)
            {
                throw new GeminiException("Không kết nối được Gemini: " + ex.Message, 0, true);
            }

            using (tl)
            {
                string noiDung = await tl.Content.ReadAsStringAsync().ConfigureAwait(false);
                int ma = (int)tl.StatusCode;
                if (tl.IsSuccessStatusCode)
                {
                    try { return JObject.Parse(noiDung); }
                    catch (Newtonsoft.Json.JsonException) { throw new GeminiException("Gemini trả về dữ liệu không đọc được.", ma, true); }
                }

                if (ma == 429) TamNghi[model] = DateTime.UtcNow + ThoiGianNghi(noiDung);
                bool tamThoi = ma == 429 || ma >= 500;
                throw new GeminiException("Gemini " + model + " trả " + ma + ": " + LayThongBaoLoi(noiDung), ma, tamThoi);
            }
        }

        /// <summary>Đọc RetryInfo.retryDelay ("78034s") trong lỗi 429; không có thì nghỉ 1 phút. Tối đa 30 phút rồi thử lại.</summary>
        private static TimeSpan ThoiGianNghi(string noiDung)
        {
            try
            {
                var chiTiet = JObject.Parse(noiDung)["error"]?["details"] as JArray;
                string s = chiTiet?.Select(d => (string)d["retryDelay"]).FirstOrDefault(x => !string.IsNullOrEmpty(x));
                double giay;
                if (s != null && double.TryParse(s.TrimEnd('s'), NumberStyles.Float, CultureInfo.InvariantCulture, out giay) && giay > 0)
                {
                    var t = TimeSpan.FromSeconds(giay);
                    return t > NghiToiDaKhiHetHanMuc ? NghiToiDaKhiHetHanMuc : t;
                }
            }
            catch (Newtonsoft.Json.JsonException) { }
            return NghiMacDinhKhiHetHanMuc;
        }

        private static string LayThongBaoLoi(string noiDung)
        {
            try
            {
                string m = (string)JObject.Parse(noiDung)["error"]?["message"];
                if (!string.IsNullOrEmpty(m)) return m.Length > 300 ? m.Substring(0, 300) : m;
            }
            catch (Newtonsoft.Json.JsonException) { }
            return "(không có nội dung lỗi)";
        }
    }
}
