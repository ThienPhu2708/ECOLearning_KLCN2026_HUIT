using System;
using System.Web.Mvc;

namespace ECOLearning.Areas.Instructor.Controllers
{
    [Authorize(Roles = "GiaoVien")]
    public class PayoutController : Controller
    {
        // GET: Instructor/Payout (UC13: Số dư khả dụng & lịch sử rút tiền)
        public ActionResult Index()
        {
            return View();
        }

        // POST: Instructor/Payout/RequestPayout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestPayout(decimal soTienYeuCau, string nganHang, string soTaiKhoan, string tenChuThe)
        {
            // Kiểm tra số dư khả dụng và ngưỡng tối thiểu
            return RedirectToAction("Index");
        }
    }
}
