using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers.Api
{
    /// <summary>
    /// API chat học viên - giáo viên cho ứng dụng di động. Toàn bộ quyền truy cập,
    /// giới hạn gửi tin, quy tắc "học viên nhắn trước" và mốc đã đọc dùng lại ChatService của website.
    /// </summary>
    [RoutePrefix("api/v1/chat"), ApiAuthorize]
    public class ChatApiController : ApiControllerBase
    {
        public class SendMessageRequest { public string Content { get; set; } }
        public class ReportConversationRequest { public string Reason { get; set; } }

        // GET /api/v1/chat/conversations?courseId=&status=all|unread|unanswered
        [HttpGet, Route("conversations")]
        public ActionResult Conversations(string courseId = null, string status = "all")
        {
            status = (status ?? "all").Trim().ToLowerInvariant();
            if (status != "all" && status != "unread" && status != "unanswered")
                return BadRequest("Trạng thái lọc không hợp lệ.");

            var service = new ChatService(Db);
            if (!service.DaCaiDat()) return Fail(503, "NOT_INSTALLED", "Chức năng chat chưa được kích hoạt trong CSDL.");

            var items = LaGiaoVien()
                ? service.LayDanhSachHoiThoaiGiaoVien(CurrentUserId, courseId, status)
                : service.LayDanhSachHoiThoaiHocVien(CurrentUserId);

            if (!LaGiaoVien())
            {
                if (!string.IsNullOrWhiteSpace(courseId)) items = items.Where(x => x.MaKhoaHoc == courseId).ToList();
                if (status == "unread") items = items.Where(x => x.SoTinChuaDoc > 0).ToList();
                if (status == "unanswered") items = items.Where(x => !x.DaTraLoi && x.TinCuoiLuc.HasValue).ToList();
            }
            return Ok(items.Select(ConversationJson).ToList());
        }

        // POST /api/v1/chat/courses/{courseId}/conversation
        // Chỉ học viên đã ghi danh được mở phòng để đặt câu hỏi cho giáo viên.
        [HttpPost, Route("courses/{courseId}/conversation")]
        public ActionResult OpenConversation(string courseId)
        {
            if (LaGiaoVien()) return Forbidden("Giáo viên không thể chủ động mở hội thoại với học viên.", "TEACHER_CANNOT_START");
            var service = new ChatService(Db);
            if (!service.DaCaiDat()) return Fail(503, "NOT_INSTALLED", "Chức năng chat chưa được kích hoạt trong CSDL.");

            var course = Db.KhoaHoc.FirstOrDefault(x => x.maKhoaHoc == courseId);
            if (course == null) return NotFoundJson("Không tìm thấy khóa học!", "COURSE_NOT_FOUND");
            if (!Db.DangKyKhoaHoc.Any(x => x.maKhoaHoc == courseId && x.maHocVien == CurrentUserId))
                return Forbidden("Bạn cần đăng ký khóa học này trước khi nhắn cho giáo viên.", "NOT_ENROLLED");

            var conversation = service.TimHoacMoHoiThoai(courseId, CurrentUserId);
            if (conversation == null) return Fail(409, "CONVERSATION_UNAVAILABLE", "Chưa thể mở hội thoại cho khóa học này.");
            return Ok(ConversationJson(conversation));
        }

        // GET /api/v1/chat/conversations/{id}/messages?beforeId=&limit=50
        [HttpGet, Route("conversations/{id:int}/messages")]
        public ActionResult Messages(int id, int? beforeId = null, int limit = 50)
        {
            var service = new ChatService(Db);
            var conversation = service.LayChiTietHoiThoai(id, CurrentUserId);
            if (conversation == null) return NotFoundJson("Không tìm thấy hội thoại hoặc bạn không có quyền truy cập.", "CONVERSATION_NOT_FOUND");
            limit = Math.Max(1, Math.Min(limit, 50));
            var messages = service.LayLichSuTinNhan(id, CurrentUserId, limit, beforeId);
            return Ok(new { conversation = ConversationJson(conversation), items = messages.Select(MessageJson).ToList() });
        }

        // POST /api/v1/chat/conversations/{id}/messages  { content }
        [HttpPost, Route("conversations/{id:int}/messages")]
        public ActionResult SendMessage(int id)
        {
            var request = Body<SendMessageRequest>();
            string content = (request.Content ?? "").Trim();
            if (content.Length == 0 || content.Length > 2000)
                return BadRequest(content.Length == 0 ? "Nội dung tin nhắn không được để trống." : "Tin nhắn tối đa 2.000 ký tự.");

            var result = new ChatService(Db).GuiTinNhan(id, CurrentUserId, content);
            if (!result.Success) return Fail(400, "MESSAGE_NOT_SENT", result.Message);
            return Ok(new { message = MessageJson(result.TinNhan), externalPaymentWarning = result.CanhBaoThanhToanNgoai }, "Đã gửi tin nhắn.");
        }

        // POST /api/v1/chat/conversations/{id}/read
        [HttpPost, Route("conversations/{id:int}/read")]
        public ActionResult MarkRead(int id)
        {
            var service = new ChatService(Db);
            if (service.LayChiTietHoiThoai(id, CurrentUserId) == null)
                return NotFoundJson("Không tìm thấy hội thoại hoặc bạn không có quyền truy cập.", "CONVERSATION_NOT_FOUND");
            service.DanhDauDaDoc(id, CurrentUserId);
            return Ok(new { conversationId = id }, "Đã đánh dấu đã đọc.");
        }

        // POST /api/v1/chat/conversations/{conversationId}/messages/{messageId}/recall
        [HttpPost, Route("conversations/{conversationId:int}/messages/{messageId:int}/recall")]
        public ActionResult Recall(int conversationId, int messageId)
        {
            var service = new ChatService(Db);
            if (service.LayChiTietHoiThoai(conversationId, CurrentUserId) == null)
                return NotFoundJson("Không tìm thấy hội thoại hoặc bạn không có quyền truy cập.", "CONVERSATION_NOT_FOUND");
            if (!service.ThuHoiTinNhan(messageId, CurrentUserId, conversationId))
                return Fail(409, "RECALL_NOT_ALLOWED", "Không thể thu hồi tin nhắn. Tin nhắn phải do bạn gửi và còn trong 5 phút đầu.");
            return Ok(new { conversationId, messageId }, "Đã thu hồi tin nhắn.");
        }

        // POST /api/v1/chat/conversations/{id}/image (multipart/form-data, field: image)
        [HttpPost, Route("conversations/{id:int}/image")]
        public ActionResult SendImage(int id, HttpPostedFileBase image)
        {
            var service = new ChatService(Db);
            if (service.LayChiTietHoiThoai(id, CurrentUserId) == null)
                return NotFoundJson("Không tìm thấy hội thoại hoặc bạn không có quyền truy cập.", "CONVERSATION_NOT_FOUND");
            if (image == null || image.ContentLength <= 0 || image.ContentLength > 5 * 1024 * 1024)
                return BadRequest("Chọn ảnh JPG hoặc PNG, tối đa 5 MB.");

            string filename = Guid.NewGuid().ToString("N") + ".jpg";
            string folder = Server.MapPath("~/App_Data/ChatImages");
            string path = Path.Combine(folder, filename);
            try
            {
                using (var original = System.Drawing.Image.FromStream(image.InputStream, true, true))
                {
                    if ((original.RawFormat.Guid != ImageFormat.Jpeg.Guid && original.RawFormat.Guid != ImageFormat.Png.Guid)
                        || (long)original.Width * original.Height > 20000000)
                        return BadRequest("Ảnh phải là JPG/PNG và không quá 20 megapixel.");
                    Directory.CreateDirectory(folder);
                    int width = Math.Min(original.Width, 1920);
                    int height = Math.Max(1, (int)((double)original.Height * width / original.Width));
                    using (var clean = new Bitmap(width, height))
                    using (var graphics = Graphics.FromImage(clean))
                    {
                        graphics.Clear(Color.White);
                        graphics.DrawImage(original, 0, 0, width, height);
                        clean.Save(path, ImageFormat.Jpeg);
                    }
                }
                var result = service.GuiAnh(id, CurrentUserId, filename);
                if (!result.Success)
                {
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    return Fail(400, "MESSAGE_NOT_SENT", result.Message);
                }
                return Ok(new { message = MessageJson(result.TinNhan), externalPaymentWarning = result.CanhBaoThanhToanNgoai }, "Đã gửi ảnh.");
            }
            catch (ArgumentException) { return BadRequest("Tệp không phải ảnh JPG/PNG hợp lệ."); }
            catch (OutOfMemoryException) { return BadRequest("Không đọc được ảnh này. Hãy thử ảnh nhỏ hơn."); }
        }

        // GET /api/v1/chat/messages/{id}/image. App tải ảnh với Authorization: Bearer <token>.
        [HttpGet, Route("messages/{id:int}/image")]
        public ActionResult Image(int id)
        {
            var message = Db.Database.SqlQuery<TinNhanItemViewModel>(
                "SELECT maTinNhan AS MaTinNhan, maHoiThoai AS MaHoiThoai, noiDung AS NoiDung, daThuHoi AS DaThuHoi FROM dbo.TinNhan WHERE maTinNhan=@id",
                new SqlParameter("@id", id)).FirstOrDefault();
            if (message == null || message.DaThuHoi || new ChatService(Db).LayChiTietHoiThoai(message.MaHoiThoai, CurrentUserId) == null)
                return HttpNotFound();
            string filename = ChatService.TenAnh(message.NoiDung);
            if (filename == null) return HttpNotFound();
            string path = Path.Combine(Server.MapPath("~/App_Data/ChatImages"), filename);
            if (!System.IO.File.Exists(path)) return HttpNotFound();
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(path, "image/jpeg");
        }

        // GET /api/v1/chat/unread-count
        [HttpGet, Route("unread-count")]
        public ActionResult UnreadCount()
        {
            return Ok(new { count = new ChatService(Db).DemTongTinChuaDoc(CurrentUserId, LaGiaoVien()) });
        }

        // POST /api/v1/chat/conversations/{id}/report  { reason }
        [HttpPost, Route("conversations/{id:int}/report")]
        public ActionResult Report(int id)
        {
            var request = Body<ReportConversationRequest>();
            string reason = (request.Reason ?? "").Trim();
            if (reason.Length == 0 || reason.Length > 1000)
                return BadRequest(reason.Length == 0 ? "Vui lòng nhập lý do báo cáo." : "Lý do báo cáo tối đa 1000 ký tự.");
            var service = new ChatService(Db);
            if (!service.BaoCaoHoiThoai(id, CurrentUserId, reason))
                return NotFoundJson("Không tìm thấy hội thoại hoặc bạn không có quyền truy cập.", "CONVERSATION_NOT_FOUND");
            return Ok(null, "Đã gửi báo cáo hội thoại đến Quản trị viên.");
        }

        private bool LaGiaoVien()
        {
            return string.Equals(CurrentUser?.vaiTro, "GiaoVien", StringComparison.OrdinalIgnoreCase)
                || string.Equals(CurrentUser?.vaiTro, "Giáo viên", StringComparison.OrdinalIgnoreCase);
        }

        private object ConversationJson(HoiThoaiItemViewModel item)
        {
            bool teacher = LaGiaoVien();
            return new
            {
                id = item.MaHoiThoai,
                courseId = item.MaKhoaHoc,
                courseName = item.TenKhoaHoc,
                courseCoverUrl = AbsoluteUrl(item.AnhBiaKhoaHoc),
                participant = new
                {
                    id = teacher ? item.MaHocVien : item.MaGiaoVien,
                    fullName = teacher ? item.TenHocVien : item.TenGiaoVien,
                    avatarUrl = AbsoluteUrl(teacher ? item.AnhDaiDienHocVien : item.AnhDaiDienGiaoVien),
                    role = teacher ? "HocVien" : "GiaoVien"
                },
                lastMessage = item.TinCuoiTomTat,
                lastMessageAt = ApiFormat.Date(item.TinCuoiLuc),
                unreadCount = item.SoTinChuaDoc,
                status = item.TrangThai,
                answered = item.DaTraLoi
            };
        }

        private object MessageJson(TinNhanItemViewModel item)
        {
            string imageUrl = item.ImageUrl == null ? null : AbsoluteUrl("/api/v1/chat/messages/" + item.MaTinNhan + "/image");
            return new
            {
                id = item.MaTinNhan,
                conversationId = item.MaHoiThoai,
                senderId = item.MaNguoiGui,
                senderName = item.TenNguoiGui,
                senderAvatarUrl = AbsoluteUrl(item.AnhDaiDien),
                content = item.ImageUrl == null ? item.NoiDung : "Ảnh",
                imageUrl = imageUrl,
                sentAt = ApiFormat.Date(item.NgayGui),
                isMine = item.LaCuaToi,
                recalled = item.DaThuHoi,
                canRecall = item.CoTheThuHoi,
                recallSeconds = item.RecallSeconds
            };
        }
    }
}
