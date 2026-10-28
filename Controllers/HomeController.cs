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
        private const int SoBanChayTrangChu = 10;
        private const int SoTiepTucHoc = 3;
        private const int SoKhoaMoiMucKhamPha = 10;
        private const int SoGiaoVienNoiBat = 4;
        private const int SoYeuThichTrangChu = 5; // nhiều hơn thì xem ở trang "Khóa học yêu thích" (nút Xem tất cả)

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
                    // "Bán chạy tuần này": khóa đang bán, chưa sở hữu (đã sở hữu thì vào học ở "Khóa học của tôi"), xếp theo lượt
                    // đăng ký trong 7 ngày (ít dữ liệu thì 30 ngày, rồi toàn thời gian). Trước đây lấy mọi khóa theo thứ tự CSDL.
                    int userId = (Session["UserId"] as int?) ?? 0;
                    var banChay = new HomeSectionService(db).BanChay(userId, SoBanChayTrangChu);
                    courses = banChay.KhoaHoc;
                    ViewBag.BanChay = banChay;
                    ViewBag.EnrollCounts = banChay.TongHocVien;

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

                // "Tiếp tục học" (dưới banner) và "Khóa học yêu thích của bạn"; lỗi một mục không làm hỏng trang chủ
                try
                {
                    var homeSections = new HomeSectionService(db);
                    ViewBag.TiepTucHoc = homeSections.TiepTucHoc(currentUserId, SoTiepTucHoc);
                    int tongYeuThich;
                    ViewBag.YeuThich = homeSections.YeuThich(currentUserId, SoYeuThichTrangChu, out tongYeuThich);
                    ViewBag.TongYeuThich = tongYeuThich;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Fetch home sections error: " + ex.Message);
                }

                // Các mục khám phá dưới "Bán chạy": mỗi mục lỗi riêng thì chỉ mục đó bị ẩn
                var khamPha = new HomeSectionService(db);
                Action<string, Action> nap = (ten, lam) =>
                {
                    try { lam(); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Fetch " + ten + " error: " + ex.Message); }
                };
                nap("related", () => ViewBag.ViBanDaHoc = khamPha.ViBanDaHoc(currentUserId, SoKhoaMoiMucKhamPha));
                nap("top-rated", () => ViewBag.DanhGiaCao = khamPha.DanhGiaCao(currentUserId, SoKhoaMoiMucKhamPha));
                nap("new", () => ViewBag.MoiRaMat = khamPha.MoiRaMat(currentUserId, SoKhoaMoiMucKhamPha));
                nap("teachers", () => ViewBag.GiaoVienNoiBat = khamPha.GiaoVienNoiBat(currentUserId, SoGiaoVienNoiBat));
                nap("recent", () => ViewBag.DaXemGanDay = khamPha.DaXemGanDay(currentUserId, SoKhoaMoiMucKhamPha));

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
