using System;
using System.Web.Mvc;

namespace ECOLearning.Controllers
{
    // Controller chuyển hướng tiện ích về Area Admin
    [Authorize(Roles = "Admin,Admin Kiểm duyệt nội dung")]
    public class AdminController : Controller
    {
        public ActionResult Index()
        {
            if (ECOLearning.Services.AccountAdminService.LaAdminKiemDuyetNoiDung(Session["Role"] as string))
                return RedirectToAction("Index", "CourseApproval", new { area = "Admin" });
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }
    }
}
