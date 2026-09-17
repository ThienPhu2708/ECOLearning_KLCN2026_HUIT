using System;
using System.Web.Mvc;

namespace ECOLearning.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        // POST: Payment/ProcessPayment (Tạo đơn hàng & kết nối cổng thanh toán)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessPayment(string paymentMethod, string couponCode)
        {
            // Tạo DonHang, ChiTietDonHang, điều hướng sang VNPay / MoMo
            return RedirectToAction("Success", new { orderId = "DH" + DateTime.Now.Ticks });
        }

        // GET: Payment/VNPayReturn
        public ActionResult VNPayReturn()
        {
            // Xử lý callback sau thanh toán
            return View();
        }

        // GET: Payment/Success
        public ActionResult Success(string orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }
    }
}
