using System;
using System.Linq;
using System.Web.Mvc;
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
                SelectedHoiThoaiId = id
            };

            // Nếu có chọn một hội thoại hoặc chọn cái đầu tiên
            int selectedId = id ?? (conversations.FirstOrDefault()?.MaHoiThoai ?? 0);
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
                    noiDung = m.NoiDung,
                    ngayGui = m.NgayGui.ToString("HH:mm - dd/MM"),
                    laCuaToi = m.LaCuaToi,
                    coTheThuHoi = m.CoTheThuHoi,
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
                    noiDung = m.NoiDung,
                    ngayGui = m.NgayGui.ToString("HH:mm - dd/MM"),
                    laCuaToi = m.LaCuaToi,
                    coTheThuHoi = m.CoTheThuHoi,
                    daThuHoi = m.DaThuHoi
                })
            }, JsonRequestBehavior.AllowGet);
        }

        // GET: /Chat/UnreadCount
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

