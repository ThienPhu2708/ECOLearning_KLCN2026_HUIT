using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;

namespace ECOLearning.Controllers
{
    public class HomeController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Trang chủ (Hero banner, Khóa học nổi bật, Gợi ý UC7)
        public ActionResult Index()
        {
            bool isLoggedIn = User.Identity.IsAuthenticated || Session["UserId"] != null;
            List<KhoaHoc> courses = new List<KhoaHoc>();
            try
            {
                if (isLoggedIn)
                {
                    courses = db.KhoaHoc
                        .Include(k => k.NguoiDung)
                        .Include(k => k.DanhMuc)
                        .Include(k => k.DanhGiaKhoaHoc)
                        .Where(k => k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active")
                        .ToList();

                    // Lấy danh sách danh mục thực sự có trong CSDL SQL Server
                    ViewBag.Categories = db.DanhMuc
                        .Where(c => c.trangThai == null || c.trangThai == true)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Fetch home data error: " + ex.Message);
            }

            return View(courses);
        }

        public ActionResult About()
        {
            ViewBag.Message = "ECO Learning Platform";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Contact ECO Learning Support";
            return View();
        }

        // POST: Home/ContactSubmit (Gửi tin nhắn từ Form Liên hệ)
        [HttpPost]
        public JsonResult ContactSubmit(string fullName, string email, string subject, string content)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ Họ tên, Email và Nội dung liên hệ!" });
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(email.Trim(), @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                return Json(new { success = false, message = "Định dạng Email không hợp lệ!" });
            }

            string finalSubject = string.IsNullOrWhiteSpace(subject) ? "Liên hệ từ Website E-Learning" : subject.Trim();
            var result = ECOLearning.Services.EmailService.SendContactMessage(fullName.Trim(), email.Trim(), finalSubject, content.Trim());

            if (result.Success)
            {
                return Json(new { success = true, message = "Đã gửi tin nhắn liên hệ thành công! Đội ngũ hỗ trợ sẽ phản hồi qua email sớm nhất." });
            }

            return Json(new { success = false, message = "Không thể gửi tin nhắn lúc này: " + result.Message });
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
