using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Controllers
{
    public class HomeController : Controller
    {
        // Mục gợi ý trang chủ là slider 5 khóa một lượt
        private const int SoGoiYTrangChu = 10;

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
                    // Bỏ khóa người dùng đã đăng ký (khóa 0đ vừa bấm "Đăng ký học" hoặc khóa đã mua):
                    // đã sở hữu thì vào học ở "Khóa học của tôi", không quảng bá lại ở trang chủ
                    int userId = (Session["UserId"] as int?) ?? 0;
                    courses = db.KhoaHoc
                        .Include(k => k.NguoiDung)
                        .Include(k => k.DanhMuc)
                        .Include(k => k.DanhGiaKhoaHoc)
                        .Where(k => k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active")
                        .Where(k => !db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == k.maKhoaHoc && d.maHocVien == userId))
                        .ToList();

                    // Số học viên mỗi khóa: một truy vấn GROUP BY nhẹ. Không Include(DangKyKhoaHoc) vì nạp cùng lúc
                    // 2 danh sách con sinh truy vấn UNION ALL cần nhiều bộ nhớ, SQL Server có thể bắt chờ tới 25 giây.
                    ViewBag.EnrollCounts = db.DangKyKhoaHoc
                        .GroupBy(d => d.maKhoaHoc)
                        .Select(g => new { MaKhoaHoc = g.Key, SoHocVien = g.Count() })
                        .ToDictionary(x => x.MaKhoaHoc, x => x.SoHocVien);

                    // Lấy danh sách danh mục thực sự có trong CSDL SQL Server
                    // Sắp theo mã như trang Tất cả lĩnh vực (CategoryController.All) để biểu tượng khớp nhau
                    ViewBag.Categories = db.DanhMuc
                        .Where(c => c.trangThai == null || c.trangThai == true)
                        .OrderBy(c => c.maDanhMuc)
                        .ToList();

                    // Ảnh minh họa Admin tải lên (migration 012); danh mục chưa có ảnh vẫn hiện biểu tượng
                    ViewBag.CategoryImages = new CategoryImageService(db).LayTatCa();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Fetch home data error: " + ex.Message);            }

            // UC7: "Khóa học dành riêng cho bạn" (RecommendationService.GetForHome):
            //  "Vì bạn đã xem X" → gợi ý tổng hợp đã tính sẵn → "Vì bạn quan tâm …" (lĩnh vực đã chọn) → khóa phổ biến.
            // Học viên mới chưa trả lời lần nào thì hộp thoại chọn lĩnh vực quan tâm tự mở (Views/Shared/_InterestPicker).
            if (isLoggedIn)
            {
                int currentUserId = (Session["UserId"] as int?) ?? 0;
                ECOLearning.ViewModels.InterestPickerViewModel picker = null;
                try
                {
                    picker = new InterestService(db).BuildPicker(currentUserId, Session["Role"] as string);
                    ViewBag.InterestPicker = picker;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Fetch interests error: " + ex.Message);
                }

                // Mục "Mã khuyến mãi" dưới Danh mục đào tạo: mã công khai đang chạy để học viên lưu vào ví
                string role = Session["Role"] as string;
                if (role == "HocVien" || role == "GiaoVien")
                {
                    try
                    {
                        var promotions = new PromotionService(db);
                        if (promotions.IsInstalled())
                        {
                            ViewBag.HomeCoupons = promotions.LayMaChoHocVien(currentUserId);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Fetch coupons error: " + ex.Message);
                    }
                }

                try
                {
                    var recommendationService = new RecommendationService(db);
                    if (recommendationService.IsInstalled())
                    {
                        ViewBag.HomeRecommendation = recommendationService.GetForHome(currentUserId, SoGoiYTrangChu, picker != null);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Fetch recommendations error: " + ex.Message);
                }
            }

            return View(courses);
        }

        public ActionResult About()
        {
            ViewBag.Message = "E-Learning Platform";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Contact E-Learning Support";
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
