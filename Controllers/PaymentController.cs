using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    /// <summary>
    /// Thanh toán ở chế độ THỬ NGHIỆM (Services/PaymentService):
    ///   GET  /Payment/Gateway?orderId=   trang cổng thử nghiệm (đóng vai trang của VNPay / MoMo), không trừ tiền thật
    ///   POST /Payment/Pay                người dùng chọn kết quả trên cổng → cổng gửi IPN có chữ ký → chuyển về Return
    ///   POST /Payment/Ipn                endpoint cổng gọi server-to-server (nguồn sự thật duy nhất để ghi danh)
    ///   GET  /Payment/Return             trang quay về sau khi thanh toán: chỉ kiểm tra chữ ký và hiển thị theo CSDL
    ///   GET  /Payment/Result?orderId=    kết quả một đơn; GET /Payment/History: lịch sử mua hàng
    /// </summary>
    public class PaymentController : Controller
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private int UserId => (Session["UserId"] as int?) ?? 0;

        private ActionResult YeuCauDangNhap()
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Request.RawUrl });
        }

        // GET: /Payment/Gateway?orderId=... (trang cổng thanh toán thử nghiệm)
        public ActionResult Gateway(string orderId)
        {
            if (UserId <= 0) return YeuCauDangNhap();
            var payment = new PaymentService(db);
            if (!payment.IsInstalled()) return HttpNotFound();
            payment.XuLyDonHetHan();
            var don = payment.LayDon(orderId);
            if (don == null || don.MaHocVien != UserId) return HttpNotFound();
            if (!don.DangCho) return RedirectToAction("Result", new { orderId });

            // Chế độ VNPay (Secrets.config: Payment:Mode = VNPay): chuyển thẳng sang trang thanh toán của VNPay Sandbox
            if (VnPayService.DangBat)
            {
                if (don.NgayHetHan.HasValue && don.NgayHetHan.Value < DateTime.Now.AddMinutes(1))
                {
                    TempData["PaymentError"] = "Đơn sắp hết hạn thanh toán, vui lòng tạo lại đơn từ giỏ hàng.";
                    return RedirectToAction("Result", new { orderId });
                }
                string returnUrl = Url.Action("VnpayReturn", "Payment", null, Request.Url.Scheme);
                return Redirect(new VnPayService(db).TaoUrlThanhToan(don, Request.UserHostAddress, returnUrl));
            }
            return View(don);
        }

        // GET: /Payment/VnpayReturn (VNPay chuyển trình duyệt về sau khi thanh toán)
        // Máy phát triển không nhận được IPN nên ở đây xác thực chữ ký + hỏi lại VNPay (querydr) rồi mới ghi nhận;
        // ghi nhận dùng chung hàm với IPN nên IPN tới sau (khi chạy trên máy chủ thật) chỉ nhận mã 02, không ghi danh lặp.
        public ActionResult VnpayReturn()
        {
            var thamSo = Request.QueryString.AllKeys.Where(k => k != null && k.StartsWith("vnp_")).ToDictionary(k => k, k => Request.QueryString[k]);
            string maDon = VnPayService.MaDonTuTxnRef(thamSo.ContainsKey("vnp_TxnRef") ? thamSo["vnp_TxnRef"] : null);
            var kq = new VnPayService(db).XuLyKetQua(thamSo, "RETURN");
            if (kq.MaPhanHoi == "97")
            {
                TempData["PaymentError"] = "Dữ liệu trả về từ VNPay không hợp lệ (sai chữ ký). Đơn hàng không thay đổi.";
            }
            else if (kq.MaPhanHoi == "04")
            {
                TempData["PaymentError"] = "Số tiền VNPay trả về không khớp đơn hàng. Đơn hàng không thay đổi.";
            }
            if (string.IsNullOrEmpty(maDon)) return RedirectToAction("History");
            return RedirectToAction("Result", new { orderId = maDon });
        }

        // GET: /Payment/VnpayIpn (VNPay gọi server-to-server; cần địa chỉ HTTPS công khai, cấu hình ở trang quản trị merchant)
        public JsonResult VnpayIpn()
        {
            var thamSo = Request.QueryString.AllKeys.Where(k => k != null && k.StartsWith("vnp_")).ToDictionary(k => k, k => Request.QueryString[k]);
            KetQuaIpn kq;
            try
            {
                kq = new VnPayService(db).XuLyKetQua(thamSo, "IPN");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("VNPay IPN error: " + ex);
                kq = KetQuaIpn.Tra("99", "Unknown error");
            }
            return Json(new { RspCode = kq.MaPhanHoi, Message = kq.ThongDiep }, JsonRequestBehavior.AllowGet);
        }

        // POST: /Payment/Pay (người dùng bấm kết quả trên cổng thử nghiệm)
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Pay(string orderId, string outcome)
        {
            if (UserId <= 0) return YeuCauDangNhap();
            var payment = new PaymentService(db);
            payment.XuLyDonHetHan();
            var don = payment.LayDon(orderId);
            if (don == null || don.MaHocVien != UserId) return HttpNotFound();
            if (!don.DangCho) return RedirectToAction("Result", new { orderId });

            string maKetQua = outcome == "fail" ? PaymentService.GdKhongDuSoDu
                : outcome == "cancel" ? PaymentService.GdKhachHuy : PaymentService.GdThanhCong;
            var ketQua = payment.TaoKetQuaCong(don, maKetQua);

            // Cổng thật gọi IPN từ máy chủ của cổng tới /Payment/Ipn. Cổng thử nghiệm chạy cùng ứng dụng nên gọi thẳng cùng hàm xử lý
            // (vẫn đi qua bước kiểm tra chữ ký, số tiền, trạng thái đơn như IPN thật).
            payment.XuLyKetQua(ketQua, "IPN");

            // Sau đó trình duyệt được chuyển về Return kèm tham số đã ký, giống cổng thật
            var rv = new System.Web.Routing.RouteValueDictionary();
            foreach (var kv in ketQua) rv[kv.Key] = kv.Value;
            return RedirectToAction("Return", rv);
        }

        // POST: /Payment/Ipn (cổng gọi server-to-server; không cần đăng nhập, xác thực bằng chữ ký)
        [HttpPost]
        public JsonResult Ipn()
        {
            var payment = new PaymentService(db);
            var thamSo = Request.Form.AllKeys.Where(k => k != null).ToDictionary(k => k, k => Request.Form[k]);
            var kq = payment.XuLyKetQua(thamSo, "IPN");
            return Json(new { RspCode = kq.MaPhanHoi, Message = kq.ThongDiep });
        }

        // GET: /Payment/Return (trình duyệt quay về từ cổng)
        public ActionResult Return()
        {
            var payment = new PaymentService(db);
            var thamSo = Request.QueryString.AllKeys.Where(k => k != null).ToDictionary(k => k, k => Request.QueryString[k]);
            string maDon;
            thamSo.TryGetValue("orderId", out maDon);
            bool hopLe = PaymentService.ChuKyHopLe(thamSo);
            payment.GhiNhatKy(maDon, "RETURN", null, hopLe, null, hopLe ? "Người dùng quay về từ cổng" : "Sai chữ ký ở trang quay về");
            if (!hopLe)
            {
                TempData["PaymentError"] = "Dữ liệu trả về từ cổng thanh toán không hợp lệ (sai chữ ký). Đơn hàng không thay đổi.";
            }
            return RedirectToAction("Result", new { orderId = maDon });
        }

        // GET: /Payment/Result?orderId=... (kết quả theo CSDL, không theo tham số trên URL)
        public ActionResult Result(string orderId)
        {
            if (UserId <= 0) return YeuCauDangNhap();
            var payment = new PaymentService(db);
            payment.XuLyDonHetHan();
            var don = payment.LayDon(orderId);
            if (don == null || don.MaHocVien != UserId) return HttpNotFound();

            if (don.TrangThai == PaymentService.DaThanhToan)
            {
                // Bỏ các khóa đã mua khỏi giỏ hàng (giỏ được giữ trong lúc chờ thanh toán)
                var cart = Session["Cart"] as List<CartItemViewModel>;
                if (cart != null)
                {
                    var daMua = new HashSet<string>(don.KhoaHoc.Select(k => k.MaKhoaHoc));
                    cart.RemoveAll(i => daMua.Contains(i.CourseId));
                    if (cart.Count == 0) Session.Remove("Applied_Coupon");
                }
            }
            ViewBag.Error = TempData["PaymentError"] as string;
            return View(don);
        }

        // GET: /Payment/History (lịch sử mua hàng của học viên)
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public ActionResult History()
        {
            if (UserId <= 0) return YeuCauDangNhap();
            var payment = new PaymentService(db);
            if (!payment.IsInstalled()) return View(new List<DonThanhToan>());
            payment.XuLyDonHetHan();
            return View(payment.LichSu(UserId));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
