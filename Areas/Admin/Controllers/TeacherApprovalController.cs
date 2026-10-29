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

        [HttpGet]
        public ActionResult Review(string appId) {
            var app = TeacherApplicationService.GetById(appId);
            if (app == null) return HttpNotFound();
            ViewBag.Categories = db.DanhMuc.Where(c => c.trangThai != false).OrderBy(c => c.tenDanhMuc).ToList();
            ViewBag.Requested = new TeacherSpecialtyService(db).Requested(appId);
            ViewBag.BreadcrumbParent = "Duyệt hồ sơ giáo viên";
            ViewBag.BreadcrumbParentUrl = Url.Action("Index");
            ViewBag.BreadcrumbCurrent = "Chi tiết hồ sơ";
            return View(app);
        }

        // POST: Admin/TeacherApproval/Approve (Phê duyệt hồ sơ -> Cấp vai trò GiaoVien)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Approve(string appId, string[] categoryIds)
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

                new TeacherSpecialtyService(db).ApproveApplication(appId, categoryIds, Convert.ToInt32(Session["UserId"]));
                return Json(new { success = true, message = "Đã duyệt hồ sơ và lĩnh vực giảng dạy." });
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
