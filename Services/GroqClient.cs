using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ECOLearning.Services
{
    /// <summary>
    /// Gọi Groq (API tương thích OpenAI: /chat/completions) làm nguồn dự phòng cho Gemini. Cấu hình ở Secrets.config:
    ///   Groq:ApiKey, Groq:Model (một hoặc nhiều tên, ngăn cách bằng dấu phẩy; thử lần lượt - hạn mức của Groq tính riêng cho từng mô hình),
    ///   Groq:Primary (true = thử Groq trước Gemini, dùng khi muốn so sánh hai bên).
    /// Phần còn lại của chatbot vẫn nói định dạng Gemini (contents / functionCall / functionResponse): lớp này đổi qua lại ở biên,
    /// nên ChatbotService và ChatbotCongCu không phải sửa gì. Lỗi dùng chung <see cref="GeminiException"/>.
    ///   - 5xx / hết giờ / mất kết nối / tool_use_failed (mô hình viết sai lệnh gọi công cụ): thử lại 1 lần rồi sang mô hình kế.
    ///   - 429 (hết hạn mức): sang mô hình kế ngay, nhớ theo Retry-After (tối đa 30 phút).
    ///   - 404: sang mô hình kế. 400 / 401 / 403: báo ngay.
    /// </summary>
    public class GroqClient
    {
        private const string DiaChi = "https://api.groq.com/openai/v1/chat/completions";
        private const int SoLanThuMoiMoHinh = 2;
        private static readonly TimeSpan NghiToiDaKhiHetHanMuc = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan NghiMacDinhKhiHetHanMuc = TimeSpan.FromMinutes(1);

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        /// <summary>Mô hình đã hết hạn mức -> thời điểm được thử lại.</summary>
        private static readonly ConcurrentDictionary<string, DateTime> TamNghi = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        static GroqClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        private readonly string apiKey;
        public List<string> MoHinh { get; private set; }
        public bool UuTien { get; private set; }

        public GroqClient()
        {
            apiKey = ConfigurationManager.AppSettings["Groq:ApiKey"];
            string ds = ConfigurationManager.AppSettings["Groq:Model"];
            if (string.IsNullOrWhiteSpace(ds)) ds = "llama-3.3-70b-versatile,openai/gpt-oss-120b";
            MoHinh = ds.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            UuTien = string.Equals((ConfigurationManager.AppSettings["Groq:Primary"] ?? "").Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }

        public bool DaCauHinh => !string.IsNullOrWhiteSpace(apiKey) && MoHinh.Count > 0;

        /// <summary>Cùng chữ ký với GeminiClient: nhận và trả về định dạng Gemini.</summary>
        public async Task<GeminiKetQua> TaoNoiDungAsync(JArray contents, string huongDan, JArray tools)
        {
            if (!DaCauHinh) throw new GeminiException("Chưa cấu hình Groq:ApiKey trong Secrets.config.", 0, false);

            var body = new JObject
            {
                ["messages"] = ChuyenTinNhan(contents, huongDan),
                // Thấp hơn mặc định để chọn công cụ và tham số ổn định hơn
                ["temperature"] = 0.3
            };
            var congCu = ChuyenCongCu(tools);
            if (congCu.Count > 0) body["tools"] = congCu;

            GeminiException loiCuoi = null;
            foreach (string model in MoHinh)
            {
                DateTime den;
                if (TamNghi.TryGetValue(model, out den) && den > DateTime.UtcNow)
                {
                    loiCuoi = loiCuoi ?? new GeminiException("Groq " + model + " đang hết hạn mức.", 429, true);
                    continue;
                }

                body["model"] = model;
                string json = body.ToString(Formatting.None);
                for (int lan = 1; lan <= SoLanThuMoiMoHinh; lan++)
                {
                    try
                    {
                        var kq = await GoiMotLanAsync(model, json);
                        return new GeminiKetQua { Body = ChuyenPhanHoi(kq), Model = "groq:" + model, DuPhong = true };
                    }
                    catch (GeminiException ex)
                    {
                        loiCuoi = ex;
                        if (ex.MaTrangThai == 429 || ex.MaTrangThai == 404) break;   // sang mô hình kế ngay
                        if (!ex.TamThoi) throw;                                       // 400 / 401 / 403
                    }
                }
            }
            throw loiCuoi ?? new GeminiException("Groq không phản hồi.", 0, true);
        }

        private async Task<JObject> GoiMotLanAsync(string model, string json)
        {
            var yc = new HttpRequestMessage(HttpMethod.Post, DiaChi)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            yc.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            yc.Headers.UserAgent.ParseAdd("ECOLearning/1.0");   // Groq nằm sau Cloudflare, thiếu User-Agent có thể bị chặn 403

            HttpResponseMessage tl;
            try
            {
                tl = await Http.SendAsync(yc).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                throw new GeminiException("Groq phản hồi quá lâu (" + model + ").", 0, true);
            }
            catch (HttpRequestException ex)
            {
                throw new GeminiException("Không kết nối được Groq: " + ex.Message, 0, true);
            }

            using (tl)
            {
                string noiDung = await tl.Content.ReadAsStringAsync().ConfigureAwait(false);
                int ma = (int)tl.StatusCode;
                if (tl.IsSuccessStatusCode)
                {
                    try { return JObject.Parse(noiDung); }
                    catch (JsonException) { throw new GeminiException("Groq trả về dữ liệu không đọc được.", ma, true); }
                }

                if (ma == 429)
                {
                    var cho = tl.Headers.RetryAfter != null ? tl.Headers.RetryAfter.Delta : null;
                    var t = cho.HasValue && cho.Value > TimeSpan.Zero ? cho.Value : NghiMacDinhKhiHetHanMuc;
                    TamNghi[model] = DateTime.UtcNow + (t > NghiToiDaKhiHetHanMuc ? NghiToiDaKhiHetHanMuc : t);
                }
                bool tamThoi = ma == 429 || ma >= 500 || (ma == 400 && noiDung.Contains("tool_use_failed"));
                throw new GeminiException("Groq " + model + " trả " + ma + ": " + LayThongBaoLoi(noiDung), ma, tamThoi);
            }
        }

        /// <summary>Gemini contents -> OpenAI messages. Mã lệnh gọi công cụ tự đánh số, ghép lại theo thứ tự và tên.</summary>
        private static JArray ChuyenTinNhan(JArray contents, string huongDan)
        {
            var messages = new JArray();
            if (!string.IsNullOrWhiteSpace(huongDan))
                messages.Add(new JObject { ["role"] = "system", ["content"] = huongDan });

            var chua = new List<KeyValuePair<string, string>>();   // lệnh gọi chưa có kết quả: (tên, mã)
            int dem = 0;
            foreach (var c in contents.OfType<JObject>())
            {
                var parts = c["parts"] as JArray ?? new JArray();
                if ((string)c["role"] == "model")
                {
                    string chu = string.Join("", parts.Where(p => p["text"] != null && !LaSuyNghi(p)).Select(p => (string)p["text"]));
                    var lenh = new JArray();
                    chua.Clear();
                    foreach (var p in parts.Where(p => p["functionCall"] != null))
                    {
                        string ten = (string)p["functionCall"]["name"];
                        string ma = "call_" + (++dem);
                        chua.Add(new KeyValuePair<string, string>(ten, ma));
                        lenh.Add(new JObject
                        {
                            ["id"] = ma,
                            ["type"] = "function",
                            ["function"] = new JObject
                            {
                                ["name"] = ten,
                                ["arguments"] = (p["functionCall"]["args"] ?? new JObject()).ToString(Formatting.None)
                            }
                        });
                    }
                    if (chu.Length == 0 && lenh.Count == 0) continue;
                    var m = new JObject { ["role"] = "assistant", ["content"] = chu.Length > 0 ? (JToken)chu : JValue.CreateNull() };
                    if (lenh.Count > 0) m["tool_calls"] = lenh;
                    messages.Add(m);
                }
                else
                {
                    foreach (var p in parts.Where(p => p["functionResponse"] != null))
                    {
                        string ten = (string)p["functionResponse"]["name"];
                        int vt = chua.FindIndex(x => x.Key == ten);
                        string ma = vt >= 0 ? chua[vt].Value : "call_" + (++dem);
                        if (vt >= 0) chua.RemoveAt(vt);
                        messages.Add(new JObject
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = ma,
                            ["content"] = (p["functionResponse"]["response"] ?? new JObject()).ToString(Formatting.None)
                        });
                    }
                    string chuNguoi = string.Join("", parts.Where(p => p["text"] != null).Select(p => (string)p["text"]));
                    if (chuNguoi.Length > 0) messages.Add(new JObject { ["role"] = "user", ["content"] = chuNguoi });
                }
            }
            return messages;
        }

        /// <summary>[{functionDeclarations: [...]}] -> [{type: "function", function: {...}}].</summary>
        private static JArray ChuyenCongCu(JArray tools)
        {
            var kq = new JArray();
            if (tools == null) return kq;
            foreach (var nhom in tools)
            {
                foreach (var f in nhom["functionDeclarations"] as JArray ?? new JArray())
                {
                    kq.Add(new JObject
                    {
                        ["type"] = "function",
                        ["function"] = new JObject
                        {
                            ["name"] = f["name"],
                            ["description"] = f["description"],
                            ["parameters"] = f["parameters"]
                        }
                    });
                }
            }
            return kq;
        }

        /// <summary>OpenAI chat completion -> dạng Gemini { candidates: [{ content: { role, parts } }] }.</summary>
        private static JObject ChuyenPhanHoi(JObject r)
        {
            var parts = new JArray();
            var msg = r["choices"]?[0]?["message"] as JObject;
            if (msg != null)
            {
                string chu = (string)msg["content"];
                if (!string.IsNullOrWhiteSpace(chu)) parts.Add(new JObject { ["text"] = chu });
                foreach (var tc in msg["tool_calls"] as JArray ?? new JArray())
                {
                    JObject args;
                    try { args = JObject.Parse((string)tc["function"]?["arguments"] ?? "{}"); }
                    catch (JsonException) { args = new JObject(); }
                    parts.Add(new JObject
                    {
                        ["functionCall"] = new JObject { ["name"] = (string)tc["function"]?["name"], ["args"] = args }
                    });
                }
            }
            if (parts.Count == 0) return new JObject();   // ChatbotService sẽ báo "chưa trả lời được"
            return new JObject
            {
                ["candidates"] = new JArray(new JObject
                {
                    ["content"] = new JObject { ["role"] = "model", ["parts"] = parts }
                })
            };
        }

        private static bool LaSuyNghi(JToken p)
        {
            return p["thought"] != null && (bool)p["thought"];
        }

        private static string LayThongBaoLoi(string noiDung)
        {
            try
            {
                string m = (string)JObject.Parse(noiDung)["error"]?["message"];
                if (!string.IsNullOrEmpty(m)) return m.Length > 300 ? m.Substring(0, 300) : m;
            }
            catch (JsonException) { }
            return "(không có nội dung lỗi)";
        }
    }
}
