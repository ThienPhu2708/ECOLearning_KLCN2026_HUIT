using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Controllers
{
    /// <summary>
    /// Trợ lý Elearning (Gemini + công cụ tra dữ liệu, Services/ChatbotService.cs):
    ///   GET  /Chatbot        trang chat thử nghiệm
    ///   POST /Chatbot/Hoi    gửi câu hỏi, trả JSON { reply, courses, debug }
    ///   GET  /Chatbot/LichSu lịch sử của phiên (bong bóng chat ở trang chủ, Views/Shared/_ChatBubble.cshtml)
    ///   POST /Chatbot/Xoa    xóa lịch sử hội thoại của phiên
    /// Lịch sử lưu ở Session (không tin dữ liệu trình duyệt gửi lên); người dùng không đăng nhập vẫn hỏi được (công cụ hiện có chỉ tìm khóa học).
    /// </summary>
    public class ChatbotController : Controller
    {
        private const string KhoaLichSu = "Chatbot:LichSu";
        // API của app (ChatbotApiController) dùng chung giới hạn và khóa đếm này
        public const int SoCauToiDa = 20;
        public static readonly TimeSpan CuaSoGioiHan = TimeSpan.FromMinutes(10);

        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private int UserId => (Session["UserId"] as int?) ?? 0;

        /// <summary>
        /// Bật trợ lý bằng Chatbot:Enabled = true trong Secrets.config. Không khai báo thì tắt (mọi đường dẫn /Chatbot trả 404),
        /// để bản publish lên hosting không mở trang thử nghiệm và không tiêu hạn mức Gemini.
        /// </summary>
        public static bool DangBat => string.Equals((ConfigurationManager.AppSettings["Chatbot:Enabled"] ?? "").Trim(), "true", StringComparison.OrdinalIgnoreCase);

        // Kiểm tra ở bước xác thực (chạy trước ValidateAntiForgeryToken) để POST cũng trả 404 thay vì lỗi thiếu token
        protected override void OnAuthorization(AuthorizationContext filterContext)
        {
            if (!DangBat)
            {
                filterContext.Result = HttpNotFound();
                return;
            }
            base.OnAuthorization(filterContext);
        }

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> Hoi(string cauHoi)
        {
            cauHoi = (cauHoi ?? "").Trim();
            if (cauHoi.Length == 0) return Json(new { ok = false, reply = "Bạn hãy nhập câu hỏi nhé." });
            if (cauHoi.Length > ChatbotService.DoDaiCauHoiToiDa)
                return Json(new { ok = false, reply = "Câu hỏi hơi dài, bạn rút gọn dưới " + ChatbotService.DoDaiCauHoiToiDa + " ký tự giúp mình nhé." });
            if (VuotGioiHan())
                return Json(new { ok = false, reply = "Bạn hỏi hơi nhiều trong thời gian ngắn, vui lòng chờ vài phút rồi hỏi tiếp nhé." });

            var lichSu = Session[KhoaLichSu] as List<ChatLuot> ?? new List<ChatLuot>();
            try
            {
                var kq = await new ChatbotService(db, UserId).HoiAsync(cauHoi, lichSu);

                lichSu.Add(new ChatLuot { Vai = "user", NoiDung = cauHoi });
                lichSu.Add(new ChatLuot { Vai = "model", NoiDung = kq.TraLoi });
                if (lichSu.Count > ChatbotService.SoLuotNho * 2) lichSu.RemoveRange(0, lichSu.Count - ChatbotService.SoLuotNho * 2);
                Session[KhoaLichSu] = lichSu;

                return Json(new
                {
                    ok = true,
                    reply = kq.TraLoi,
                    courses = kq.KhoaHoc.Select(t => new
                    {
                        id = t.MaKhoaHoc,
                        ten = t.Ten,
                        anh = DuongDan(t.AnhBia),
                        danhMuc = t.DanhMuc,
                        giaoVien = t.GiaoVien,
                        gia = (long)t.Gia,
                        diem = Math.Round(t.Diem, 1),
                        soDanhGia = t.SoDanhGia,
                        url = Url.Action("Details", "Course", new { id = t.MaKhoaHoc })
                    }),
                    debug = new { model = kq.Model, duPhong = kq.DuPhong, congCu = kq.SoLuotCongCu, ms = kq.MiliGiay }
                });
            }
            catch (GeminiException ex)
            {
                System.Diagnostics.Trace.TraceError("Chatbot: " + ex.Message);
                string loi = ex.HetHanMuc
                    ? "Trợ lý đã hết lượt trả lời trong hôm nay, bạn vui lòng quay lại sau nhé."
                    : ex.TamThoi
                        ? "Trợ lý đang bận, bạn thử lại sau ít giây nhé."
                        : "Trợ lý tạm thời chưa hoạt động. Vui lòng liên hệ quản trị viên.";
                return Json(new { ok = false, reply = loi, debug = new { loi = ex.Message } });
            }
        }

        /// <summary>Lịch sử hội thoại của phiên (chỉ chữ) để bong bóng chat hiện lại tin cũ khi chuyển trang.</summary>
        [HttpGet]
        public JsonResult LichSu()
        {
            var lichSu = Session[KhoaLichSu] as List<ChatLuot> ?? new List<ChatLuot>();
            return Json(new { ok = true, tinNhan = lichSu.Select(l => new { vai = l.Vai, noiDung = l.NoiDung }) }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Xoa()
        {
            Session.Remove(KhoaLichSu);
            return Json(new { ok = true });
        }

        /// <summary>Tối đa <see cref="SoCauToiDa"/> câu trong 10 phút cho mỗi người (theo tài khoản, khách thì theo địa chỉ IP).</summary>
        private bool VuotGioiHan()
        {
            return RateLimiter.VuotGioiHan(KhoaGioiHan(UserId, Request.UserHostAddress), SoCauToiDa, CuaSoGioiHan);
        }

        public static string KhoaGioiHan(int userId, string ip) => "chat:" + (userId > 0 ? "u" + userId : "ip" + ip);

        private string DuongDan(string duongDan)
        {
            if (string.IsNullOrWhiteSpace(duongDan)) return null;
            return duongDan.StartsWith("~", StringComparison.Ordinal) ? Url.Content(duongDan) : duongDan;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && db != null)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
