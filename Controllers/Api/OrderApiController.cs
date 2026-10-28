using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.Services.Api;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers.Api
{
    /// <summary>
    /// Trang VNPay chuyển WebView của app về sau khi thanh toán: ghi nhận kết quả (cùng hàm với /Payment/VnpayReturn
    /// của website: kiểm chữ ký, hỏi lại VNPay bằng querydr) rồi hiện trang ngắn, KHÔNG chuyển hướng tiếp.
    /// App thấy địa chỉ chứa "/api/v1/payments/vnpay-return" thì đóng WebView và gọi F4 lấy trạng thái đơn.
    /// Không cần token: VNPay chuyển trình duyệt về, không mang header đăng nhập.
    /// </summary>
    public class PaymentReturnApiController : ApiControllerBase
    {
        public const string DuongDanQuayVe = "/api/v1/payments/vnpay-return";

        // GET /api/v1/payments/vnpay-return?vnp_...
        [HttpGet, Route("api/v1/payments/vnpay-return")]
        public ActionResult VnpayReturn()
        {
            var thamSo = Request.QueryString.AllKeys.Where(k => k != null && k.StartsWith("vnp_")).ToDictionary(k => k, k => Request.QueryString[k]);
            string maDon = VnPayService.MaDonTuTxnRef(thamSo.ContainsKey("vnp_TxnRef") ? thamSo["vnp_TxnRef"] : null);
            bool thanhCong = false;
            try
            {
                var kq = new VnPayService(Db).XuLyKetQua(thamSo, "RETURN");
                thanhCong = kq.DaThanhToan;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("API VNPay return: " + ex);
            }
            string tieuDe = thanhCong ? "Thanh toán thành công" : "Thanh toán chưa hoàn tất";
            string html = "<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
                + "<title>" + tieuDe + "</title></head><body style=\"font-family:sans-serif;text-align:center;padding:48px 16px;color:#0F172A\">"
                + "<h2 style=\"color:" + (thanhCong ? "#10B981" : "#EF4444") + "\">" + tieuDe + "</h2>"
                + "<p>Mã đơn: " + System.Web.HttpUtility.HtmlEncode(maDon ?? "") + "</p><p style=\"color:#64748B\">Đang quay lại ứng dụng...</p></body></html>";
            return Content(html, "text/html", System.Text.Encoding.UTF8);
        }
    }

    /// <summary>
    /// Nhóm F – Giỏ hàng, đơn hàng, thanh toán VNPay (Task_Tuan10.docx mục 6.6 và luồng 6.9).
    /// Giỏ hàng lưu trên máy; server luôn tính lại giá, mã giảm giá và điều kiện mua.
    /// Thanh toán: app mở paymentUrl trong WebView → VNPay chuyển về /api/v1/payments/vnpay-return (ghi nhận kết quả)
    /// → app bắt địa chỉ này, đóng WebView rồi gọi F4.
    /// </summary>
    [RoutePrefix("api/v1"), ApiAuthorize]
    public class OrderApiController : ApiControllerBase
    {
        public class CartRequest
        {
            public List<string> CourseIds { get; set; }
            public string CouponCode { get; set; }
            public string PaymentMethod { get; set; }
        }

        private static readonly Dictionary<string, string> TenPhuongThuc = new Dictionary<string, string>
        {
            { "Napas", "Thẻ ATM nội địa" },
            { "Visa/Mastercard", "Thẻ quốc tế Visa / Mastercard / JCB" },
            { "MoMo/VNPay", "Ví điện tử MoMo / VNPay" }
        };

        // F1. POST /api/v1/cart/quote
        [HttpPost, Route("cart/quote")]
        public ActionResult Quote()
        {
            var req = Body<CartRequest>();
            var bg = new OrderService(Db).TinhBaoGia(CurrentUserId, req.CourseIds, req.CouponCode);
            var the = new ApiCourseMapper(Db, AbsoluteUrl).Map(bg.Dong.Select(d => d.MaKhoaHoc));
            return Ok(new
            {
                items = the,
                // subTotal = tổng giá đã giảm trực tiếp (khớp price trên thẻ); discount = tiền giảm của coupon;
                // directDiscount = tổng tiền được giảm trực tiếp so với giá gốc (originalPrice)
                subTotal = ApiFormat.Money(bg.TamTinh),
                directDiscount = ApiFormat.Money(bg.TienGiamTrucTiep),
                discount = ApiFormat.Money(bg.TienGiam),
                total = ApiFormat.Money(bg.TongTien),
                // Cùng khung với POST /api/v1/coupons/check (Đặc tả Marketing mục 13)
                coupon = bg.UuDai == null ? null : CouponJson.KetQuaKiemTra(bg, req.CouponCode),
                invalidItems = bg.KhongHopLe.Select(x => new { id = x.MaKhoaHoc, reason = x.LyDo }).ToList(),
                paymentMethods = PaymentService.PhuongThucHopLe.Select(pt =>
                {
                    bool dung = !VnPayService.DangBat || VnPayService.HoTroPhuongThuc(pt);
                    return new
                    {
                        id = pt,
                        name = TenPhuongThuc.ContainsKey(pt) ? TenPhuongThuc[pt] : pt,
                        enabled = dung,
                        reason = dung ? null : "Chưa hỗ trợ ở môi trường thử nghiệm VNPay Sandbox"
                    };
                }).ToList()
            });
        }

        // F2. GET /api/v1/coupons/my: chuyển sang CouponApiController (Đặc tả Marketing mục 13)

        // F3. POST /api/v1/orders
        [HttpPost, Route("orders")]
        public ActionResult CreateOrder()
        {
            var req = Body<CartRequest>();
            string phuongThuc = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "Napas" : req.PaymentMethod.Trim();
            var kq = new OrderService(Db).TaoDon(CurrentUserId, req.CourseIds, req.CouponCode, phuongThuc);
            if (!kq.Success) return Fail(kq.Status, kq.Code, kq.Message);

            if (!kq.CanThanhToan)
            {
                return Ok(new { orderId = kq.MaDonHang, total = 0L, expiresAt = (string)null, requiresPayment = false, paymentUrl = (string)null },
                    "Đơn hàng 0đ đã được xác nhận. Khóa học đã được thêm vào hồ sơ học tập của bạn.");
            }

            var don = new PaymentService(Db).LayDon(kq.MaDonHang);
            string url = VnPayService.DangBat
                ? new VnPayService(Db).TaoUrlThanhToan(don, IpKhach(), AbsoluteUrl(PaymentReturnApiController.DuongDanQuayVe))
                // Cổng thử nghiệm nội bộ của website (cần đăng nhập web): app nên chạy với Payment:Mode = VNPay
                : AbsoluteUrl("/Payment/Gateway?orderId=" + Uri.EscapeDataString(kq.MaDonHang));
            return Ok(new
            {
                orderId = kq.MaDonHang,
                total = ApiFormat.Money(kq.TongTien),
                expiresAt = ApiFormat.Date(don?.NgayHetHan),
                requiresPayment = true,
                paymentUrl = url,
                gateway = VnPayService.DangBat ? "vnpay-sandbox" : "web-sandbox"
            }, "Đã tạo đơn hàng, đang chuyển sang cổng thanh toán...");
        }

        // F4. GET /api/v1/orders/{orderId}
        [HttpGet, Route("orders/{orderId}")]
        public ActionResult Order(string orderId)
        {
            var payment = new PaymentService(Db);
            if (!payment.IsInstalled()) return NotFoundJson();
            payment.XuLyDonHetHan();
            var don = payment.LayDon(orderId);
            if (don == null || don.MaHocVien != CurrentUserId) return NotFoundJson("Không tìm thấy đơn hàng.", "ORDER_NOT_FOUND");

            string maGd = Db.Database.SqlQuery<string>(
                "SELECT TOP 1 maGiaoDichCong FROM dbo.ThanhToan WHERE maDonHang = @dh AND trangThaiThanhToan = @paid ORDER BY ngayThanhToan DESC",
                new SqlParameter("@dh", don.MaDonHang), new SqlParameter("@paid", PaymentService.DaThanhToan)).FirstOrDefault();
            return Ok(new
            {
                orderId = don.MaDonHang,
                status = MaTrangThai(don),
                statusText = don.TrangThai,
                total = ApiFormat.Money(don.TongTien),
                subTotal = ApiFormat.Money(don.TongTruocGiam),
                discount = ApiFormat.Money(Math.Max(0, don.TongTruocGiam - don.TongTien)),
                createdAt = ApiFormat.Date(don.NgayDat),
                expiresAt = ApiFormat.Date(don.NgayHetHan),
                paidAt = ApiFormat.Date(don.NgayThanhToan),
                paymentMethod = don.PhuongThuc,
                transactionNo = maGd,
                items = don.KhoaHoc.Select(k => new
                {
                    courseId = k.MaKhoaHoc,
                    title = k.TenKhoaHoc,
                    imageUrl = AbsoluteUrl(string.IsNullOrWhiteSpace(k.AnhBia) ? ApiCourseMapper.AnhBiaMacDinh : k.AnhBia),
                    price = ApiFormat.Money(k.DonGia)
                }).ToList()
            });
        }

        // F5. GET /api/v1/orders?page=
        [HttpGet, Route("orders")]
        public ActionResult Orders(int page = 1, int pageSize = 12)
        {
            page = ChuanHoaTrang(page);
            pageSize = ChuanHoaKichThuoc(pageSize);
            var payment = new PaymentService(Db);
            if (!payment.IsInstalled()) return Ok(Paged(new object[0], page, pageSize, 0));
            payment.XuLyDonHetHan();
            var ds = payment.LichSu(CurrentUserId);
            var trang = ds.Skip((page - 1) * pageSize).Take(pageSize).Select(d => new
            {
                orderId = d.MaDonHang,
                status = MaTrangThai(d),
                statusText = d.TrangThai,
                total = ApiFormat.Money(d.TongTien),
                itemsCount = d.KhoaHoc.Count,
                firstCourseTitle = d.KhoaHoc.Select(k => k.TenKhoaHoc).FirstOrDefault(),
                imageUrl = AbsoluteUrl(d.KhoaHoc.Select(k => k.AnhBia).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ?? ApiCourseMapper.AnhBiaMacDinh),
                createdAt = ApiFormat.Date(d.NgayDat),
                paidAt = ApiFormat.Date(d.NgayThanhToan),
                transactionNo = d.MaGiaoDich
            }).ToList();
            return Ok(Paged(trang, page, pageSize, ds.Count));
        }

        /// <summary>pending / paid / failed / expired / cancelled</summary>
        private static string MaTrangThai(DonThanhToan d)
        {
            switch (d.TrangThai)
            {
                case PaymentService.DaThanhToan: return "paid";
                case PaymentService.ThatBai: return "failed";
                case PaymentService.HetHan: return "expired";
                case PaymentService.DaHuy: return "cancelled";
                case PaymentService.ChoThanhToan: return d.DangCho ? "pending" : "expired";
                default: return "paid"; // đơn cũ trước migration 014 ("Hoàn tất", "Success"...)
            }
        }

        // F6. POST /api/v1/courses/{id}/enroll-free
        [HttpPost, Route("courses/{id}/enroll-free")]
        public ActionResult EnrollFree(string id)
        {
            var k = Db.KhoaHoc.FirstOrDefault(x => x.maKhoaHoc == id);
            if (k == null || !CourseStatus.IsPublished(k.trangThai))
                return NotFoundJson("Khóa học hiện không mở đăng ký!", "COURSE_NOT_FOUND");
            if (k.maGiaoVien == CurrentUserId)
                return Forbidden("Bạn là giáo viên của khóa học này nên không thể tự đăng ký học!", "NOT_ALLOWED");
            if (k.giaBan > 0)
                return BadRequest("Khóa học này có học phí. Vui lòng thêm vào giỏ hàng và tiến hành thanh toán!", "NOT_FREE");
            if (Db.DangKyKhoaHoc.Any(d => d.maHocVien == CurrentUserId && d.maKhoaHoc == id))
                return Fail(409, "ALREADY_OWNED", "Bạn đã đăng ký khóa học này rồi!");

            Db.DangKyKhoaHoc.Add(new DangKyKhoaHoc
            {
                maDangKy = "DK" + DateTime.Now.ToString("yyMMddHHmmss") + new Random().Next(1000, 9999),
                ngayDangKy = DateTime.Now,
                trangThaiDK = "Đã kích hoạt",
                maHocVien = CurrentUserId,
                maKhoaHoc = id
            });
            Db.SaveChanges();
            return Ok(new { courseId = id }, "Đăng ký khóa học thành công!");
        }
    }
}
