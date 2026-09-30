using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Admin.Controllers
{
    public class TeacherApprovalController : AdminBaseController
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Admin/TeacherApproval (Danh sách hồ sơ đăng ký Giảng viên)
        [HttpGet]
        public ActionResult Index(string statusFilter = "All")
        {
            var allApps = TeacherApplicationService.GetAll().OrderByDescending(a => a.CreatedAt).ToList();

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                allApps = allApps.Where(a => string.Equals(a.Status, statusFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            ViewBag.StatusFilter = statusFilter;
            ViewBag.PendingCount = TeacherApplicationService.GetAll().Count(a => a.Status == "Pending");
            ViewBag.ApprovedCount = TeacherApplicationService.GetAll().Count(a => a.Status == "Approved");
            ViewBag.RejectedCount = TeacherApplicationService.GetAll().Count(a => a.Status == "Rejected");
            ViewBag.BreadcrumbCurrent = "Duyệt hồ sơ giáo viên";

            return View(allApps);
        }

        // POST: Admin/TeacherApproval/Approve (Phê duyệt hồ sơ -> Cấp vai trò GiaoVien)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Approve(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return Json(new { success = false, message = "Mã hồ sơ không hợp lệ!" });
            }

            try
            {
                var app = TeacherApplicationService.GetById(appId);
                if (app == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy hồ sơ xét duyệt!" });
                }

                // 1. Cập nhật vai trò người dùng trong CSDL SQL Server
                var user = db.NguoiDung.Find(app.UserId);
                if (user == null)
                {
                    user = db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == app.Email.ToLower());
                }

                if (user != null)
                {
                    user.vaiTro = "GiaoVien";
                    db.SaveChanges();
                }

                // 2. Cập nhật trạng thái đơn đăng ký
                int? adminId = Session["UserId"] != null ? Convert.ToInt32(Session["UserId"]) : (int?)null;
                bool updated = TeacherApplicationService.UpdateStatus(appId, "Approved", null, adminId);

                if (updated)
                {
                    return Json(new { success = true, message = "Đã phê duyệt thành công! Tài khoản " + app.Email + " đã được cấp vai trò Giảng viên (GiaoVien)." });
                }
                else
                {
                    return Json(new { success = false, message = "Không thể cập nhật trạng thái hồ sơ!" });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Approve Teacher Application Exception: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // POST: Admin/TeacherApproval/Reject (Từ chối hồ sơ + nhập lý do)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Reject(string appId, string rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return Json(new { success = false, message = "Mã hồ sơ không hợp lệ!" });
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                return Json(new { success = false, message = "Vui lòng nhập lý do từ chối hồ sơ!" });
            }

            try
            {
                var app = TeacherApplicationService.GetById(appId);
                if (app == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy hồ sơ xét duyệt!" });
                }

                int? adminId = Session["UserId"] != null ? Convert.ToInt32(Session["UserId"]) : (int?)null;
                bool updated = TeacherApplicationService.UpdateStatus(appId, "Rejected", rejectionReason.Trim(), adminId);

                if (updated)
                {
                    return Json(new { success = true, message = "Đã chuyển hồ sơ sang trạng thái Từ chối." });
                }
                else
                {
                    return Json(new { success = false, message = "Không thể cập nhật trạng thái hồ sơ!" });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Reject Teacher Application Exception: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
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
