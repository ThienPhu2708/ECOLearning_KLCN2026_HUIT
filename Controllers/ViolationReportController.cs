using System;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Controllers
{
    // Học viên gửi báo cáo vi phạm cho khóa học đã đăng ký
    public class ViolationReportController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // POST: ViolationReport/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Create(string courseId, string loaiViPham, string moTa, string maBaiHoc = null)
        {
            int userId = (Session["UserId"] as int?) ?? 0;
            if (userId == 0)
            {
                return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để gửi báo cáo!" });
            }

            var service = new ViolationReportService(db);
            if (!service.IsInstalled())
            {
                return Json(new { success = false, message = "Chức năng báo cáo vi phạm chưa sẵn sàng. Vui lòng thử lại sau!" });
            }

            try
            {
                string error = service.CreateReport(courseId, userId, loaiViPham, moTa, maBaiHoc);
                return error == null
                    ? Json(new { success = true, message = "Đã gửi báo cáo vi phạm. Quản trị viên sẽ xem xét trong thời gian sớm nhất. Cảm ơn bạn!" })
                    : Json(new { success = false, message = error });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Create violation report error: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi xảy ra khi gửi báo cáo. Vui lòng thử lại!" });
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
