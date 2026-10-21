using System;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Controllers
{
    /// <summary>Thả tim / bỏ tim khóa học (AJAX). Trang danh sách ở LearningController.Favorites.</summary>
    public class FavoriteController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // POST: Favorite/Toggle
        [HttpPost]
        [ValidateAntiForgeryToken]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public JsonResult Toggle(string courseId)
        {
            int userId = (Session["UserId"] as int?) ?? 0;
            if (userId <= 0)
            {
                return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để thả tim khóa học!" });
            }
            if (string.IsNullOrWhiteSpace(courseId))
            {
                return Json(new { success = false, message = "Thiếu mã khóa học." });
            }

            try
            {
                var result = new FavoriteService(db).Toggle(userId, courseId.Trim());
                if (!result.Success)
                {
                    return Json(new { success = false, message = result.Message });
                }
                return Json(new
                {
                    success = true,
                    liked = result.Liked,
                    count = result.Count,
                    message = result.Liked ? "Đã thêm vào Khóa học đã tim" : "Đã bỏ tim khóa học"
                });
            }
            catch (Exception ex)
            {
                // Chưa chạy migration 005 (chưa có bảng TuongTacKhoaHoc) hoặc lỗi kết nối
                System.Diagnostics.Debug.WriteLine("Toggle favorite error: " + ex.Message);
                return Json(new { success = false, message = "Chức năng thả tim chưa sẵn sàng. Vui lòng thử lại sau!" });
            }
        }

        // GET: Favorite/State — trạng thái tim hiện tại (mã các khóa đang tim + số khóa đã tim) để đồng bộ lại
        // giao diện khi quay lại trang bằng nút Back hoặc chuyển về tab cũ (assets/js/favorite.js)
        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0)]
        public JsonResult State()
        {
            int userId = (Session["UserId"] as int?) ?? 0;
            if (userId <= 0)
            {
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }
            try
            {
                var service = new FavoriteService(db);
                return Json(new { success = true, likedIds = service.GetLikedIds(userId), count = service.CountFavorites(userId) },
                    JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Favorite state error: " + ex.Message);
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }
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
