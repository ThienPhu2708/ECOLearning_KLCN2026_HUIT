using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Services;
using ECOLearning.Services.Api;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers.Api
{
    /// <summary>
    /// Khung JSON dùng chung cho API mã giảm giá (Đặc tả Marketing mục 13): thẻ mã và kết quả kiểm tra mã với giỏ hàng.
    /// </summary>
    public static class CouponJson
    {
        /// <summary>usable | upcoming | used | expired | paused | soldOut theo trạng thái mã trong ví (GanTrangThai).</summary>
        public static string TrangThai(MaTrongVi v)
        {
            switch (v.TrangThai)
            {
                case "CoTheDung": return "usable";
                case "SapDienRa": return "upcoming";
                case "DaDung": return "used";
            }
            switch (v.NhanTrangThai)
            {
                case "Hết lượt": return "soldOut";
                case "Tạm ngưng":
                case "Chiến dịch tạm dừng":
                case "Chiến dịch đã hủy": return "paused";
                default: return "expired";
            }
        }

        /// <summary>Thẻ mã (mục 13 "Thẻ mã"): dùng chung cho mọi API ví.</summary>
        public static Dictionary<string, object> The(MaGiamGiaHienThi m, string status, string statusText)
        {
            return new Dictionary<string, object>
            {
                { "code", m.MaCode },
                { "title", m.TenKhuyenMai },
                { "discountText", m.GiaTriNgan },
                { "note", m.GhiChuGiaTri },
                { "description", m.MoTaUuDai },
                { "conditions", m.DieuKien },
                { "isPublic", m.CongKhai },
                { "isLoyalty", m.LaThanThiet },
                { "saved", m.DaLuu },
                { "startsAt", ApiFormat.Day(m.NgayBatDau) },
                { "expiresAt", ApiFormat.Day(m.NgayKetThuc) },
                { "remaining", m.ConLai },
                { "status", status },
                { "statusText", statusText }
            };
        }

        /// <summary>Thẻ của mã chưa có trong ví (danh sách thu thập): đang chạy hoặc chưa bắt đầu.</summary>
        public static Dictionary<string, object> TheChuaLuu(MaGiamGiaHienThi m)
        {
            return The(m, m.ChuaBatDau ? "upcoming" : "usable", m.ChuaBatDau ? "Chưa bắt đầu" : (m.SapHetHan ? "Sắp hết hạn" : "Có thể dùng"));
        }

        public static Dictionary<string, object> TheTrongVi(MaTrongVi v)
        {
            return The(v.Ma, TrangThai(v), v.NhanTrangThai);
        }

        /// <summary>Kết quả POST coupons/check, cũng là khối "coupon" của cart/quote.</summary>
        public static object KetQuaKiemTra(OrderService.BaoGia bg, string code)
        {
            var kq = bg.UuDai ?? KetQuaApDungUuDai.Loi("Vui lòng nhập mã khuyến mãi!");
            var p = kq.ChuongTrinh;
            decimal giam = kq.Success ? kq.TienGiam : 0;
            return new
            {
                valid = kq.Success,
                code = p?.MaCode ?? (code ?? "").Trim().ToUpperInvariant(),
                title = p?.TenKhuyenMai,
                discountText = p?.MoTaUuDai,
                subtotal = ApiFormat.Money(bg.TamTinh),
                eligibleSubtotal = ApiFormat.Money(kq.Success ? kq.TienApDung : 0),
                eligibleCourseIds = kq.Success ? kq.KhoaApDung : new List<string>(),
                discount = ApiFormat.Money(giam),
                total = ApiFormat.Money(Math.Max(0, bg.TamTinh - giam)),
                requireLogin = kq.RequireLogin,
                message = kq.Message
            };
        }
    }

    /// <summary>
    /// API mã giảm giá cho ứng dụng (Đặc tả Marketing mục 13). Mọi endpoint gọi đúng các hàm của PromotionService
    /// (KiemTraCoupon = Check, LuuMa, LayViCuaToi, LayMaChoHocVien, LayChiTiet) nên web và app cùng một logic.
    /// Tiền là số nguyên VNĐ; giá mỗi khóa = giaBan.
    /// </summary>
    [RoutePrefix("api/v1")]
    public class CouponApiController : ApiControllerBase
    {
        public class CheckRequest
        {
            public string Code { get; set; }
            public List<string> CourseIds { get; set; }
        }

        private PromotionService Promotions => new PromotionService(Db);

        private ActionResult ChuaSanSang() => Fail(503, "COUPON_NOT_READY", "Chức năng mã giảm giá chưa sẵn sàng.");

        // POST /api/v1/coupons/check — chọn mã trong ví / gõ ở ô "Nhập mã khác" tại giỏ hàng (không bắt buộc đăng nhập)
        [HttpPost, Route("coupons/check")]
        public ActionResult Check()
        {
            var req = Body<CheckRequest>();
            if (!Promotions.IsInstalled()) return ChuaSanSang();
            var bg = new OrderService(Db).TinhBaoGia(CurrentUserId, req.CourseIds, string.IsNullOrWhiteSpace(req.Code) ? null : req.Code);

            var soHuu = bg.KhongHopLe.FirstOrDefault(k => k.LyDo == "Bạn đã sở hữu khóa học này");
            if (soHuu != null) return Fail(409, "COURSE_OWNED", "Bạn đã sở hữu khóa học " + soHuu.MaKhoaHoc + ". Vui lòng xóa khỏi giỏ hàng!");
            if (bg.Dong.Count == 0) return Fail(400, "CART_EMPTY", "Giỏ hàng của bạn đang trống!");
            if (string.IsNullOrWhiteSpace(req.Code)) bg.UuDai = KetQuaApDungUuDai.Loi("Vui lòng nhập mã khuyến mãi!");
            return Ok(CouponJson.KetQuaKiemTra(bg, req.Code), bg.UuDai.Message);
        }

        // GET /api/v1/coupons/collectable?courseId= — tab "Thu thập mã", dải mã trang chủ, mã ở trang khóa học
        [HttpGet, Route("coupons/collectable")]
        public ActionResult Collectable(string courseId = null)
        {
            var promotions = Promotions;
            if (!promotions.IsInstalled()) return ChuaSanSang();
            var ma = promotions.LayMaChoHocVien(CurrentUserId, null, string.IsNullOrWhiteSpace(courseId) ? null : courseId.Trim())
                .Where(m => !m.DaLuu).ToList();
            var hv = promotions.ThongKeHocVien(CurrentUserId);
            return Ok(new
            {
                coupons = ma.Select(CouponJson.TheChuaLuu).ToList(),
                loyaltyUpcoming = promotions.LayThanThietSapMo(CurrentUserId).Select(u => new
                {
                    title = u.TenKhuyenMai,
                    discountText = u.GiaTriNgan,
                    progress = u.TienDo
                }).ToList(),
                stats = hv == null ? null : new { coursesBought = hv.SoKhoa, totalSpent = ApiFormat.Money(hv.ChiTieu) }
            });
        }

        // GET /api/v1/ads — ảnh quảng cáo chiến dịch / chương trình đang chạy cho popup trang chủ app (token tùy chọn).
        // Ưu đãi thân thiết chỉ có trong danh sách khi người dùng đã đăng nhập và đủ điều kiện.
        [HttpGet, Route("ads")]
        public ActionResult Ads()
        {
            var qc = new QuangCaoService(Db);
            if (!qc.IsInstalled()) return Ok(new { items = new object[0] });
            var items = qc.LayDangChay(CurrentUserId).Select(a => new
            {
                type = a.Loai == QuangCaoService.LoaiChienDich ? "campaign" : "promotion",
                id = a.Ma,
                title = a.Ten,
                imageUrl = AbsoluteUrl(a.Anh),
                expiresAt = ApiFormat.Day(a.NgayKetThuc)
            }).ToList();
            return Ok(new { items });
        }

        // GET /api/v1/ads/{type}/{id} — trang ưu đãi mở ra khi bấm ảnh quảng cáo; type = campaign | promotion
        [HttpGet, Route("ads/{type}/{id}")]
        public ActionResult AdDetail(string type, string id)
        {
            string loai = type == "campaign" ? QuangCaoService.LoaiChienDich : type == "promotion" ? QuangCaoService.LoaiChuongTrinh : null;
            if (loai == null) return NotFoundJson("Loại ưu đãi không hợp lệ.", "OFFER_TYPE_INVALID");
            var qc = new QuangCaoService(Db);
            var trang = qc.LayTrang(loai, id, CurrentUserId);
            if (trang == null) return NotFoundJson("Ưu đãi không còn hiệu lực.", "OFFER_NOT_FOUND");

            var mapper = new ApiCourseMapper(Db, AbsoluteUrl);
            return Ok(new
            {
                type,
                id = trang.Ma,
                title = trang.TieuDe,
                description = trang.MoTa,
                imageUrl = AbsoluteUrl(trang.Anh),
                startsAt = ApiFormat.Day(trang.NgayBatDau),
                endsAt = ApiFormat.Day(trang.NgayKetThuc),
                offers = trang.Muc.Select(m => new
                {
                    id = m.MaKhuyenMai,
                    title = m.Ten,
                    discountText = m.GiaTri,
                    note = m.GhiChu,
                    conditions = m.DieuKien,
                    expiresAt = ApiFormat.Day(m.NgayKetThuc),
                    isDirectDiscount = m.LaGiamTrucTiep,
                    isLoyalty = m.LaThanThiet,
                    minOrder = m.DonToiThieu.HasValue ? (long?)ApiFormat.Money(m.DonToiThieu.Value) : null,
                    code = m.MaCode,
                    canSave = m.CoTheLuu,
                    saved = m.DaLuu,
                    message = m.ThongBao,
                    appliesToAll = m.ApDungMoiKhoa,
                    totalCourses = m.TongSoKhoa,
                    courses = m.Khoa.Count == 0 ? new List<CourseSummaryDto>() : mapper.Map(m.Khoa.Select(k => k.MaKhoaHoc).ToList())
                }).ToList()
            });
        }

        // GET /api/v1/coupons/loyalty-popup — popup "Ưu đãi dành riêng cho học viên thân thiết" (chỉ đọc; app tự quyết định tần suất hiện)
        [HttpGet, Route("coupons/loyalty-popup"), ApiAuthorize]
        public ActionResult LoyaltyPopup()
        {
            var promotions = Promotions;
            if (!promotions.IsInstalled()) return ChuaSanSang();
            var p = promotions.LayPopupThanThiet(CurrentUserId);
            if (p == null) return Ok(new { show = false });
            return Ok(new
            {
                show = true,
                kind = p.Loai == "dat" ? "eligible" : "upcoming",
                promotionId = p.MaKhuyenMai,
                code = p.MaCode,
                title = p.TieuDe,
                discountText = p.GiaTriNgan,
                note = p.GhiChuGiaTri,
                description = p.MoTa,
                conditions = p.DieuKien,
                expiresAt = p.HanDung.HasValue ? ApiFormat.Day(p.HanDung.Value) : null,
                progress = p.TienDo
            });
        }

        // POST /api/v1/coupons/{code}/save — nút "Lưu mã" và ô "Nhập mã" trong ví (lưu được cả mã riêng)
        [HttpPost, Route("coupons/{code}/save"), ApiAuthorize]
        public ActionResult Save(string code)
        {
            var promotions = Promotions;
            if (!promotions.IsInstalled()) return ChuaSanSang();
            if (promotions.LayTheoCode(code) == null) return Fail(404, "COUPON_NOT_FOUND", "Mã khuyến mãi không tồn tại!");

            var kq = promotions.LuuMa(CurrentUserId, code, tuDanhSach: false);
            if (!kq.Success) return Fail(400, "COUPON_NOT_SAVABLE", kq.Message);
            var v = promotions.LayMaTrongVi(CurrentUserId, code);
            return Ok(new { alreadySaved = kq.DaCoTrongVi, coupon = v == null ? null : CouponJson.TheTrongVi(v) }, kq.Message);
        }

        // GET /api/v1/coupons/wallet?tab=usable|used|expired — trang "Mã giảm giá của tôi"
        [HttpGet, Route("coupons/wallet"), ApiAuthorize]
        public ActionResult Wallet(string tab = "usable")
        {
            var promotions = Promotions;
            if (!promotions.IsInstalled()) return ChuaSanSang();
            var vi = promotions.LayViCuaToi(CurrentUserId, "codung");
            var ds = tab == "used" ? vi.DaDung : tab == "expired" ? vi.HetHieuLuc : vi.CoTheDung;
            return Ok(new
            {
                counts = new { usable = vi.CoTheDung.Count, used = vi.DaDung.Count, expired = vi.HetHieuLuc.Count },
                items = ds.Select(v =>
                {
                    var the = CouponJson.TheTrongVi(v);
                    the["savedAt"] = ApiFormat.Date(v.NgayLuu);
                    the["myUses"] = v.LuotCuaToi;
                    the["perUserLimit"] = v.GioiHanMoiHocVien;
                    the["expiringSoon"] = v.Ma.SapHetHan;
                    the["history"] = v.LichSu.Select(l => new
                    {
                        orderId = l.MaDonHang,
                        usedAt = ApiFormat.Date(l.NgaySuDung),
                        discount = ApiFormat.Money(l.TienGiam),
                        total = ApiFormat.Money(l.TongDonHang)
                    }).ToList();
                    return the;
                }).ToList()
            });
        }

        // GET /api/v1/coupons/my?courseIds=KH01,KH05 — chọn mã ở giỏ hàng: mã trong ví tab "Có thể dùng", mỗi mã chạy Check()
        [HttpGet, Route("coupons/my"), ApiAuthorize]
        public ActionResult My(string courseIds = null)
        {
            var promotions = Promotions;
            if (!promotions.IsInstalled()) return ChuaSanSang();
            var ids = (courseIds ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            List<DongGioHang> dong = null;
            if (ids.Count > 0)
            {
                var bg = new OrderService(Db).TinhBaoGia(CurrentUserId, ids, null);
                if (bg.Dong.Count > 0) dong = bg.DongGioHang();
            }
            var ds = promotions.LayMaChoHocVien(CurrentUserId, dong, null, chiTrongVi: true);
            var items = ds.Select(m =>
            {
                bool dung = dong != null && m.CoTheApDung;
                var the = CouponJson.The(m, m.ChuaBatDau ? "upcoming" : "usable", m.ChuaBatDau ? "Chưa bắt đầu" : (m.SapHetHan ? "Sắp hết hạn" : "Có thể dùng"));
                the["usable"] = dung;
                the["discount"] = ApiFormat.Money(dung ? m.TienGiam : 0);
                the["reason"] = dung ? null : (dong == null ? "Giỏ hàng của bạn đang trống!" : m.LyDo);
                return new { dung, giam = m.TienGiam, the };
            })
            .OrderByDescending(x => x.dung).ThenByDescending(x => x.giam)
            .ToList();
            return Ok(new
            {
                // Gợi ý mã giảm nhiều nhất; app không tự áp
                bestCode = items.Where(x => x.dung && x.giam > 0).Select(x => (string)x.the["code"]).FirstOrDefault(),
                items = items.Select(x => x.the).ToList()
            });
        }

        // GET /api/v1/coupons/{code} — chi tiết mã: thẻ mã + điều khoản + khóa áp dụng + lịch sử dùng của học viên
        [HttpGet, Route("coupons/{code}")]
        public ActionResult Detail(string code)
        {
            var promotions = Promotions;
            if (!promotions.IsInstalled()) return ChuaSanSang();
            // Chỉ xem được mã trong ví hoặc mã công khai đang lưu được; ngược lại 404 (không lộ mã riêng)
            var vm = promotions.LayChiTiet(CurrentUserId, code);
            if (vm == null) return Fail(404, "COUPON_NOT_FOUND", "Mã khuyến mãi không tồn tại!");

            var the = vm.TrongVi != null ? CouponJson.TheTrongVi(vm.TrongVi) : CouponJson.TheChuaLuu(vm.The);
            the["canSave"] = vm.CoTheLuu;
            the["policy"] = vm.ChuongTrinh.ChinhSach;
            the["terms"] = vm.DieuKhoan.Select(d => new { label = d.Key, value = d.Value }).ToList();
            the["courses"] = vm.KhoaHocApDung.Select(k => new { id = k.MaKhoaHoc, title = k.TenKhoaHoc, category = k.TenDanhMuc, price = ApiFormat.Money(k.GiaBan) }).ToList();
            the["myHistory"] = vm.LichSuCuaToi.Select(l => new
            {
                orderId = l.MaDonHang,
                usedAt = ApiFormat.Date(l.NgaySuDung),
                discount = ApiFormat.Money(l.TienGiam),
                total = ApiFormat.Money(l.TongDonHang)
            }).ToList();
            return Ok(the);
        }
    }
}
