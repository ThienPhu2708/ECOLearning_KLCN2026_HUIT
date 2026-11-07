using System;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Admin.Controllers
{
    /// <summary>
    /// Admin / Quản lý giáo viên (giáo viên đã được duyệt; hồ sơ đăng ký mới ở trang Duyệt hồ sơ Giáo viên).
    /// Khóa / mở khóa / cảnh cáo / đặt lại mật khẩu dùng chung các action của UserController (partial _AccountActions):
    /// khóa giáo viên thì khóa học đang bán chuyển "Tạm ẩn", phiếu chi bị tạm giữ, học viên đã mua vẫn học.
    /// </summary>
    public class TeacherController : AdminBaseController
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Admin/Teacher
        public ActionResult Index(string q = null, string status = null, string sort = "priority")
        {
            ViewBag.BreadcrumbCurrent = "Quản lý giáo viên";
            var quanTri = new AccountAdminService(db);
            if (!quanTri.IsInstalled())
            {
                ViewBag.ChuaKhoiTao = true;
                return View();
            }

            quanTri.MoKhoaHetHan();
            if (status != "active" && status != "locked" && status != "violation") status = null;
            if (sort != "revenue" && sort != "rating" && sort != "students" && sort != "newest") sort = "priority";

            var service = new AdminTeacherService(db);
            ViewBag.ThongKe = service.ThongKe(service.DanhSach(null, null, null));
            ViewBag.DanhSach = service.DanhSach(q, status, sort);
            ViewBag.Q = q;
            ViewBag.Status = status;
            ViewBag.Sort = sort;
            return View();
        }

        // GET: Admin/Teacher/Details/5
        public ActionResult Details(int id)
        {
            var quanTri = new AccountAdminService(db);
            if (!quanTri.IsInstalled()) return RedirectToAction("Index");

            quanTri.MoKhoaHetHan(id);
            var model = new AdminTeacherService(db).ChiTiet(id);
            if (model == null)
            {
                // Không phải giáo viên (học viên / nhân viên): mở trang người dùng
                return RedirectToAction("Details", "User", new { id });
            }

            ViewBag.BreadcrumbParent = "Quản lý giáo viên";
            ViewBag.BreadcrumbParentUrl = Url.Action("Index");
            ViewBag.BreadcrumbCurrent = model.CoBan.HoTen;
            ViewBag.LaChinhMinh = id == Convert.ToInt32(Session["UserId"]);
            var restrictionService = new TeacherPublishingRestrictionService(db);
            ViewBag.PublishingRestrictionInstalled = restrictionService.Installed();
            ViewBag.PublishingRestriction = restrictionService.Active(id);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RestrictPublishing(int id, string duration, string reason)
        {
            if (!LaAdminGoc) return new HttpStatusCodeResult(403);
            var result = new TeacherPublishingRestrictionService(db).Apply(Convert.ToInt32(Session["UserId"]), id, duration, reason);
            TempData[result.ThanhCong ? "SuccessMessage" : "ErrorMessage"] = result.ThongBao;
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult LiftPublishingRestriction(int id)
        {
            if (!LaAdminGoc) return new HttpStatusCodeResult(403);
            var result = new TeacherPublishingRestrictionService(db).Lift(Convert.ToInt32(Session["UserId"]), id);
            TempData[result.ThanhCong ? "SuccessMessage" : "ErrorMessage"] = result.ThongBao;
            return RedirectToAction("Details", new { id });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
