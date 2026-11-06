using System;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    public class RecommendationController : Controller
    {
        private const int SoGoiYTrenTrang = 12;

        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Trang "Khóa học gợi ý" (UC7). Mỗi lần mở trang, thuật toán lọc cộng tác tính lại
        // gợi ý cho người đang đăng nhập từ ma trận độ tương đồng đã lưu và lưu vào KhoaHocKhuyenNghi.
        public ActionResult Index()
        {
            if (!User.Identity.IsAuthenticated && Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.RawUrl });
            }

            int currentUserId = (Session["UserId"] as int?) ?? 0;
            InterestPickerViewModel picker = null;
            try
            {
                picker = new InterestService(db).BuildPicker(currentUserId, Session["Role"] as string);
                ViewBag.InterestPicker = picker;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Fetch interests error: " + ex.Message);
            }

            RecommendationPageViewModel model;
            try
            {
                model = new RecommendationService(db).GetPersonalizedPage(currentUserId, SoGoiYTrenTrang, picker != null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Recommendation error: " + ex.Message);
                model = new RecommendationPageViewModel
                {
                    DaKhoiTao = true,
                    ThongBaoLoi = "Không tính được gợi ý lúc này. Vui lòng thử lại sau."
                };
            }
            return View(model);
        }

        /// <summary>
        /// Làm mới gợi ý cho đúng một học viên từ ma trận tương đồng đã có.
        /// Stored procedure chỉ thay kết quả cũ của người này, không tính lại
        /// cosine hay ma trận của toàn hệ thống.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Refresh()
        {
            if (!User.Identity.IsAuthenticated && Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Recommendation") });
            }

            int currentUserId = (Session["UserId"] as int?) ?? 0;
            try
            {
                new RecommendationService(db).Compute(currentUserId, SoGoiYTrenTrang, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Refresh recommendation error: " + ex.Message);
            }

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
