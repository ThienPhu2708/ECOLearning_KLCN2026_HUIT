using System;
using System.Linq;
using System.Web.Mvc;
using System.Web;
using System.IO;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    public class ChatController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private int GetCurrentUserId()
        {
            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int id))
            {
                return id;
            }
            if (User.Identity.IsAuthenticated)
            {
                var user = db.NguoiDung.FirstOrDefault(u => u.email == User.Identity.Name);
                if (user != null)
                {
                    Session["UserId"] = user.maNguoiDung;
                    Session["User"] = SessionNguoiDung.BanSao(user);
                    return user.maNguoiDung;
                }
            }
            return 0;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SendImage(int conversationId, HttpPostedFileBase image)
        {
            int userId = GetCurrentUserId();
            var service = new ChatService(db);
            if (userId <= 0 || service.LayChiTietHoiThoai(conversationId, userId) == null)
                return Json(new { success = false, message = "Không có quyền gửi ảnh vào hội thoại." });
            if (image == null || image.ContentLength <= 0 || image.ContentLength > 5 * 1024 * 1024)
                return Json(new { success = false, message = "Chọn ảnh JPG hoặc PNG, tối đa 5 MB." });
            string filename = Guid.NewGuid().ToString("N") + ".jpg";
            string folder = Server.MapPath("~/App_Data/ChatImages");
            string path = Path.Combine(folder, filename);
            try
            {
                using (var original = System.Drawing.Image.FromStream(image.InputStream, true, true))
                {
                    if ((original.RawFormat.Guid != ImageFormat.Jpeg.Guid && original.RawFormat.Guid != ImageFormat.Png.Guid)
                        || (long)original.Width * original.Height > 20000000)
                        return Json(new { success = false, message = "Ảnh phải là JPG/PNG và không quá 20 megapixel." });
                    Directory.CreateDirectory(folder);
                    int width = Math.Min(original.Width, 1920);
                    int height = Math.Max(1, (int)((double)original.Height * width / original.Width));
                    using (var clean = new Bitmap(width, height))
                    {
                        using (var graphics = Graphics.FromImage(clean)) { graphics.Clear(Color.White); graphics.DrawImage(original, 0, 0, width, height); }
                        clean.Save(path, ImageFormat.Jpeg);
                    }
                }
                var result = service.GuiAnh(conversationId, userId, filename);
                if (!result.Success) { System.IO.File.Delete(path); return Json(new { success = false, message = result.Message }); }
                ECOLearning.Hubs.ChatHub.PhatAnh(result, userId);
                var m = result.TinNhan;
                return Json(new { success = true, message = new { maTinNhan = m.MaTinNhan, maHoiThoai = m.MaHoiThoai, maNguoiGui = m.MaNguoiGui, tenNguoiGui = m.TenNguoiGui, anhDaiDien = m.AnhDaiDien, noiDung = "Ảnh", imageUrl = m.ImageUrl, ngayGui = m.NgayGui.ToString("HH:mm - dd/MM"), coTheThuHoi = true } });
            }
            catch (ArgumentException) { return Json(new { success = false, message = "Tệp không phải ảnh JPG/PNG hợp lệ." }); }
            catch (OutOfMemoryException) { return Json(new { success = false, message = "Không đọc được ảnh này. Hãy thử ảnh nhỏ hơn." }); }
        }

        [HttpGet]
        public ActionResult Image(int id)
        {
            int userId = GetCurrentUserId();
            if (userId <= 0) return new HttpStatusCodeResult(401);
            var message = db.Database.SqlQuery<TinNhanItemViewModel>("SELECT maTinNhan AS MaTinNhan, maHoiThoai AS MaHoiThoai, noiDung AS NoiDung, daThuHoi AS DaThuHoi FROM dbo.TinNhan WHERE maTinNhan=@id", new SqlParameter("@id", id)).FirstOrDefault();
            if (message == null || message.DaThuHoi || new ChatService(db).LayChiTietHoiThoai(message.MaHoiThoai, userId) == null) return HttpNotFound();
            string filename = ChatService.TenAnh(message.NoiDung);
            if (filename == null) return HttpNotFound();
            string path = Path.Combine(Server.MapPath("~/App_Data/ChatImages"), filename);
            if (!System.IO.File.Exists(path)) return HttpNotFound();
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(path, "image/jpeg");
        }

        // GET: /Chat/ (Trang Tin nhắn của học viên)
        public ActionResult Index(int? id)
        {
            int userId = GetCurrentUserId();
            if (userId <= 0)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.RawUrl });
            }

            var service = new ChatService(db);
            if (!service.DaCaiDat())
            {
                ViewBag.Error = "Chức năng chat chưa được kích hoạt trong CSDL.";
                return View(new StudentChatIndexViewModel());
            }

            var conversations = service.LayDanhSachHoiThoaiHocVien(userId);
            var model = new StudentChatIndexViewModel
            {
                DanhSachHoiThoai = conversations,
                SelectedHoiThoaiId = null
            };

            // Chỉ mở khi học viên chọn; xem danh sách không tự đánh dấu đã đọc.
            int selectedId = id ?? 0;
            if (selectedId > 0)
            {
                var currentConv = conversations.FirstOrDefault(c => c.MaHoiThoai == selectedId) 
                               ?? service.LayChiTietHoiThoai(selectedId, userId);

                if (currentConv != null && currentConv.MaHocVien == userId)
                {
                    model.SelectedHoiThoaiId = selectedId;
                    model.PhongChatHienTai = new ChatRoomViewModel
                    {
                        HoiThoai = currentConv,
                        DanhSachTinNhan = service.LayLichSuTinNhan(selectedId, userId),
                        LaGiaoVien = false,
                        CurrentUserId = userId
                    };
                    service.DanhDauDaDoc(selectedId, userId);
                    currentConv.SoTinChuaDoc = 0;
                }
            }

            return View(model);
        }

        // GET: /Chat/GetOrCreateForCourse?courseId=... (Dành cho trang Study - Hỏi giáo viên)
        [HttpGet]
        public JsonResult GetOrCreateForCourse(string courseId)
        {
            int userId = GetCurrentUserId();
            if (userId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để chat với giáo viên." }, JsonRequestBehavior.AllowGet);
            }

            var service = new ChatService(db);
            if (!service.DaCaiDat())
            {
                return Json(new { success = false, message = "Chức năng chat chưa được kích hoạt." }, JsonRequestBehavior.AllowGet);
            }

            var conv = service.TimHoacMoHoiThoai(courseId, userId);
            if (conv == null)
            {
                return Json(new { success = false, message = "Bạn cần đăng ký khóa học này trước khi đặt câu hỏi cho giáo viên." }, JsonRequestBehavior.AllowGet);
            }

            var messages = service.LayLichSuTinNhan(conv.MaHoiThoai, userId);
            service.DanhDauDaDoc(conv.MaHoiThoai, userId);

            return Json(new
            {
                success = true,
                conversation = conv,
                messages = messages.Select(m => new
                {
                    maTinNhan = m.MaTinNhan,
                    maHoiThoai = m.MaHoiThoai,
                    maNguoiGui = m.MaNguoiGui,
                    tenNguoiGui = m.TenNguoiGui,
                    anhDaiDien = m.AnhDaiDien,
                    noiDung = m.ImageUrl != null ? "Ảnh" : m.NoiDung,
                    imageUrl = m.ImageUrl,
                    ngayGui = m.NgayGui.ToString("HH:mm - dd/MM"),
                    laCuaToi = m.LaCuaToi,
                    coTheThuHoi = m.CoTheThuHoi,
                    recallSeconds = m.RecallSeconds,
                    daThuHoi = m.DaThuHoi
                }),
                currentUserId = userId
            }, JsonRequestBehavior.AllowGet);
        }

        // GET: /Chat/Messages?conversationId=...&beforeId=...
        [HttpGet]
        public JsonResult Messages(int conversationId, int? beforeId)
        {
            int userId = GetCurrentUserId();
            if (userId <= 0)
            {
                return Json(new { success = false, message = "Chưa đăng nhập" }, JsonRequestBehavior.AllowGet);
            }

            var service = new ChatService(db);
            var conv = service.LayChiTietHoiThoai(conversationId, userId);
            if (conv == null || (conv.MaHocVien != userId && conv.MaGiaoVien != userId))
            {
                return Json(new { success = false, message = "Không có quyền truy cập." }, JsonRequestBehavior.AllowGet);
            }

            var messages = service.LayLichSuTinNhan(conversationId, userId, 50, beforeId);

            return Json(new
            {
                success = true,
                messages = messages.Select(m => new
                {
                    maTinNhan = m.MaTinNhan,
                    maHoiThoai = m.MaHoiThoai,
                    maNguoiGui = m.MaNguoiGui,
                    tenNguoiGui = m.TenNguoiGui,
                    anhDaiDien = m.AnhDaiDien,
                    noiDung = m.ImageUrl != null ? "Ảnh" : m.NoiDung,
                    imageUrl = m.ImageUrl,
                    ngayGui = m.NgayGui.ToString("HH:mm - dd/MM"),
                    laCuaToi = m.LaCuaToi,
                    coTheThuHoi = m.CoTheThuHoi,
                    recallSeconds = m.RecallSeconds,
                    daThuHoi = m.DaThuHoi
                })
            }, JsonRequestBehavior.AllowGet);
        }

        // GET: /Chat/UnreadCount
        [HttpGet]
        public JsonResult CourseUnreadCount(string courseId)
        {
            int userId = GetCurrentUserId();
            if (userId <= 0) return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);
            var service = new ChatService(db);
            var conversation = service.LayDanhSachHoiThoaiHocVien(userId).FirstOrDefault(c => c.MaKhoaHoc == courseId);
            return Json(new { count = conversation == null ? 0 : conversation.SoTinChuaDoc }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult UnreadCount()
        {
            int userId = GetCurrentUserId();
            if (userId <= 0)
            {
                return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);
            }

            string role = Session["Role"]?.ToString() ?? "";
            bool isTeacher = role.IndexOf("GiaoVien", StringComparison.OrdinalIgnoreCase) >= 0 
                          || role.IndexOf("Giáo viên", StringComparison.OrdinalIgnoreCase) >= 0
                          || role.IndexOf("Instructor", StringComparison.OrdinalIgnoreCase) >= 0;

            var service = new ChatService(db);
            int count = service.DemTongTinChuaDoc(userId, isTeacher);

            return Json(new { count = count }, JsonRequestBehavior.AllowGet);
        }

        // POST: /Chat/Report
        [HttpPost, ValidateAntiForgeryToken]
        public JsonResult Report(int conversationId, string reason)
        {
            int userId = GetCurrentUserId();
            if (userId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." });
            }

            var service = new ChatService(db);
            bool ok = service.BaoCaoHoiThoai(conversationId, userId, reason);

            return Json(new { success = ok, message = ok ? "Đã gửi báo cáo hội thoại đến Quản trị viên." : "Không thể báo cáo." });
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

