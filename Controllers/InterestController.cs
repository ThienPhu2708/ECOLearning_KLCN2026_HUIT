using System;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Controllers
{
    /// <summary>
    /// Lĩnh vực quan tâm (cold-start của gợi ý khóa học), gọi bằng AJAX từ hộp thoại
    /// Views/Shared/_InterestPicker.cshtml ở trang chủ và trang "Khóa học gợi ý".
    /// </summary>
    public class InterestController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // POST: Interest/Save — lưu 1–3 lĩnh vực, thay lựa chọn cũ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Save(string[] categoryIds)
        {
            int userId = (Session["UserId"] as int?) ?? 0;
            if (userId <= 0)
            {
                return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để chọn lĩnh vực quan tâm." });
            }

            try
            {
                var result = new InterestService(db).Save(userId, categoryIds);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                // Chưa chạy migration 007 hoặc lỗi kết nối
                System.Diagnostics.Debug.WriteLine("Save interests error: " + ex.Message);
                return Json(new { success = false, message = "Chưa lưu được lựa chọn. Vui lòng thử lại sau." });
            }
        }

        // POST: Interest/Skip — bỏ qua bước chọn, không tự mở lại
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Skip()
        {
            int userId = (Session["UserId"] as int?) ?? 0;
            if (userId <= 0)
            {
                return Json(new { success = false, requireLogin = true });
            }

            try
            {
                new InterestService(db).Skip(userId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Skip interests error: " + ex.Message);
                return Json(new { success = false });
            }
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
