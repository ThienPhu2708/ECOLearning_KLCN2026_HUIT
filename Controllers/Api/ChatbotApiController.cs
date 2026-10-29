using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using ECOLearning.Services;

namespace ECOLearning.Controllers.Api
{
    /// <summary>
    /// Nhóm K – Trợ lý Elearning cho app (cùng ChatbotService / Gemini với bong bóng chat của website).
    /// Tắt bằng Chatbot:Enabled (Secrets.config) như website: khi tắt mọi đường dẫn trả 404 CHATBOT_DISABLED.
    /// Lịch sử hội thoại lưu trên máy chủ theo tài khoản (website lưu theo Session, app không có Session);
    /// khách chưa đăng nhập vẫn hỏi được nhưng mỗi câu là một lượt mới, không nhớ câu trước.
    /// </summary>
    [RoutePrefix("api/v1")]
    public class ChatbotApiController : ApiControllerBase
    {
        private static readonly TimeSpan GiuLichSu = TimeSpan.FromHours(2);

        public class AskRequest
        {
            public string Question { get; set; }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (!ChatbotController.DangBat)
            {
                filterContext.Result = NotFoundJson("Trợ lý Elearning hiện chưa được bật.", "CHATBOT_DISABLED");
                return;
            }
            base.OnActionExecuting(filterContext);
        }

        // K1. POST /api/v1/chatbot/ask  { question } → { reply, courses }
        [HttpPost, Route("chatbot/ask")]
        public async Task<ActionResult> Ask()
        {
            string cauHoi = (Body<AskRequest>().Question ?? "").Trim();
            if (cauHoi.Length == 0) return BadRequest("Bạn hãy nhập câu hỏi nhé.");
            if (cauHoi.Length > ChatbotService.DoDaiCauHoiToiDa)
                return BadRequest("Câu hỏi hơi dài, bạn rút gọn dưới " + ChatbotService.DoDaiCauHoiToiDa + " ký tự giúp mình nhé.");
            if (RateLimiter.VuotGioiHan(ChatbotController.KhoaGioiHan(CurrentUserId, IpKhach()), ChatbotController.SoCauToiDa, ChatbotController.CuaSoGioiHan))
                return Fail(429, "TOO_MANY_REQUESTS", "Bạn hỏi hơi nhiều trong thời gian ngắn, vui lòng chờ vài phút rồi hỏi tiếp nhé.");

            var lichSu = LichSuCuaToi(taoMoi: true);
            List<ChatLuot> banSao;
            lock (lichSu) banSao = lichSu.ToList();

            ChatbotKetQua kq;
            try
            {
                kq = await new ChatbotService(Db, CurrentUserId).HoiAsync(cauHoi, banSao);
            }
            catch (GeminiException ex)
            {
                System.Diagnostics.Trace.TraceError("Chatbot API: " + ex.Message);
                if (ex.HetHanMuc) return Fail(503, "CHATBOT_QUOTA", "Trợ lý đã hết lượt trả lời trong hôm nay, bạn vui lòng quay lại sau nhé.");
                if (ex.TamThoi) return Fail(503, "CHATBOT_BUSY", "Trợ lý đang bận, bạn thử lại sau ít giây nhé.");
                return Fail(503, "CHATBOT_UNAVAILABLE", "Trợ lý tạm thời chưa hoạt động. Vui lòng liên hệ quản trị viên.");
            }

            lock (lichSu)
            {
                lichSu.Add(new ChatLuot { Vai = "user", NoiDung = cauHoi });
                lichSu.Add(new ChatLuot { Vai = "model", NoiDung = kq.TraLoi });
                int thua = lichSu.Count - ChatbotService.SoLuotNho * 2;
                if (thua > 0) lichSu.RemoveRange(0, thua);
            }

            return Ok(new
            {
                reply = kq.TraLoi,
                courses = kq.KhoaHoc.Select(t => new
                {
                    id = t.MaKhoaHoc,
                    title = t.Ten,
                    thumbnail = AbsoluteUrl(t.AnhBia),
                    category = t.DanhMuc,
                    instructor = t.GiaoVien,
                    price = (long)t.Gia,
                    rating = Math.Round(t.Diem, 1),
                    reviewCount = t.SoDanhGia
                }).ToList()
            });
        }

        // K2. GET /api/v1/chatbot/history → { messages: [{ role: user | model, content }] } (khách: luôn rỗng)
        [HttpGet, Route("chatbot/history")]
        public ActionResult History()
        {
            var lichSu = LichSuCuaToi(taoMoi: false);
            List<object> tinNhan;
            if (lichSu == null) tinNhan = new List<object>();
            else lock (lichSu) tinNhan = lichSu.Select(l => (object)new { role = l.Vai, content = l.NoiDung }).ToList();
            return Ok(new { messages = tinNhan });
        }

        // K3. DELETE /api/v1/chatbot/history : bắt đầu cuộc trò chuyện mới
        [HttpDelete, Route("chatbot/history")]
        public ActionResult ClearHistory()
        {
            if (CurrentUserId > 0) HttpRuntime.Cache.Remove(KhoaLichSu(CurrentUserId));
            return Ok(null, "Đã xóa lịch sử trò chuyện.");
        }

        private static string KhoaLichSu(int userId) => "ChatbotApi:LichSu:u" + userId;

        /// <summary>Lịch sử của tài khoản (giữ 2 giờ kể từ lần dùng cuối). Khách: danh sách tạm, không lưu lại.</summary>
        private List<ChatLuot> LichSuCuaToi(bool taoMoi)
        {
            if (CurrentUserId <= 0) return taoMoi ? new List<ChatLuot>() : null;
            string khoa = KhoaLichSu(CurrentUserId);
            var daCo = HttpRuntime.Cache[khoa] as List<ChatLuot>;
            if (daCo != null || !taoMoi) return daCo;
            var moi = new List<ChatLuot>();
            // Add trả bản đã có nếu một yêu cầu khác vừa tạo trước
            return HttpRuntime.Cache.Add(khoa, moi, null, Cache.NoAbsoluteExpiration, GiuLichSu, CacheItemPriority.Normal, null) as List<ChatLuot> ?? moi;
        }
    }
}
