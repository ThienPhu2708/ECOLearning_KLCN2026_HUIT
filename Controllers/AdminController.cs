using System;
using System.Web.Mvc;

namespace ECOLearning.Controllers
{
    // Controller chuyển hướng tiện ích về Area Admin
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }
    }
}
