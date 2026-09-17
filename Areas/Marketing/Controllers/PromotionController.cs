using System;
using System.Web.Mvc;

namespace ECOLearning.Areas.Marketing.Controllers
{
    [Authorize(Roles = "Marketing")]
    public class PromotionController : Controller
    {
        // GET: Marketing/Promotion (UC17: Quản lý chương trình khuyến mãi & coupon)
        public ActionResult Index()
        {
            return View();
        }

        // POST: Marketing/Promotion/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FormCollection form)
        {
            // Thiết lập tên, mức giảm %, mã code, ngày bắt đầu - kết thúc
            return RedirectToAction("Index");
        }

        // POST: Marketing/Promotion/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleStatus(string id)
        {
            return RedirectToAction("Index");
        }
    }
}
