using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Marketing.Models;
using ECOLearning.Models;

namespace ECOLearning.Areas.Marketing.Controllers
{
    public class PromotionController : MarketingBaseController
    {
        // GET: Marketing/Promotion (UC17: Quản lý chương trình khuyến mãi & coupon)
        public ActionResult Index(string search = null, string status = null)
        {
            var now = DateTime.Now;
            var campaigns = db.KhuyenMai.Include("MaGiamGia").OrderByDescending(c => c.ngayBatDau).ToList();
            var orders = db.DonHang.Where(d => !string.IsNullOrEmpty(d.maGiamGia)).ToList();

            var promoList = new List<PromotionItem>();

            foreach (var c in campaigns)
            {
                var mgg = c.MaGiamGia.FirstOrDefault();
                string code = mgg != null ? mgg.maCode : c.maKhuyenMai;
                bool isMggPaused = mgg != null && mgg.trangThai == false;

                string currentStatus;
                if (c.ngayKetThuc < now)
                {
                    currentStatus = "Đã kết thúc";
                }
                else if (isMggPaused)
                {
                    currentStatus = "Tạm ngưng";
                }
                else if (c.ngayBatDau > now)
                {
                    currentStatus = "Chưa bắt đầu";
                }
                else
                {
                    currentStatus = "Đang chạy";
                }

                // Count usage
                int usedCount = 0;
                if (mgg != null)
                {
                    usedCount = orders.Count(d => d.maGiamGia == mgg.maGiamGia1 || d.maGiamGia == mgg.maCode);
                }

                promoList.Add(new PromotionItem
                {
                    MaKhuyenMai = c.maKhuyenMai,
                    TenKhuyenMai = c.tenKhuyenMai,
                    MaCode = code,
                    MucUuDai = c.mucUuDai,
                    GiaTriGiam = mgg != null ? mgg.giaTriGiam : (decimal)c.mucUuDai,
                    NgayBatDau = c.ngayBatDau,
                    NgayKetThuc = c.ngayKetThuc,
                    DieuKienApDung = string.IsNullOrEmpty(c.dieuKienApDung) ? "Toàn sàn" : c.dieuKienApDung,
                    TrangThai = currentStatus,
                    SoLuongDaDung = usedCount
                });
            }

            // Apply filters
            if (!string.IsNullOrEmpty(search))
            {
                string kw = search.Trim().ToLower();
                promoList = promoList.Where(p => 
                    (p.TenKhuyenMai != null && p.TenKhuyenMai.ToLower().Contains(kw)) ||
                    (p.MaCode != null && p.MaCode.ToLower().Contains(kw))
                ).ToList();
            }

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                promoList = promoList.Where(p => p.TrangThai.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status ?? "all";
            ViewBag.BreadcrumbCurrent = "Mã giảm giá & Khuyến mãi";

            return View(promoList);
        }

        // POST: Marketing/Promotion/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(PromotionCreateViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TenKhuyenMai) || string.IsNullOrWhiteSpace(model.MaCode))
            {
                TempData["Error"] = "Vui lòng điền đầy đủ các thông tin bắt buộc (*).";
                return RedirectToAction("Index");
            }

            // UC17: Date validation rule: End date must not precede start date
            if (model.NgayKetThuc < model.NgayBatDau)
            {
                TempData["Error"] = "Lỗi nghiệp vụ UC17: Ngày kết thúc không thể trước ngày bắt đầu!";
                return RedirectToAction("Index");
            }

            try
            {
                string codeUpper = model.MaCode.Trim().ToUpper();

                // Check duplicate code
                if (db.MaGiamGia.Any(m => m.maCode.ToUpper() == codeUpper))
                {
                    TempData["Error"] = "Mã coupon \"" + codeUpper + "\" đã tồn tại trên hệ thống. Vui lòng chọn mã khác.";
                    return RedirectToAction("Index");
                }

                // Resolve marketing staff user id
                int marketingUserId = 1;
                if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int sUid))
                {
                    marketingUserId = sUid;
                }
                else
                {
                    var user = db.NguoiDung.FirstOrDefault(u => u.vaiTro.Contains("Marketing") || u.vaiTro.Contains("Admin"));
                    if (user != null) marketingUserId = user.maNguoiDung;
                    else marketingUserId = db.NguoiDung.Select(u => u.maNguoiDung).FirstOrDefault();
                }

                string timeSuffix = DateTime.Now.ToString("yyMMddHHmmss");
                string kmId = "KM" + timeSuffix;
                string mggId = "MGG" + timeSuffix;

                var km = new KhuyenMai
                {
                    maKhuyenMai = kmId,
                    tenKhuyenMai = model.TenKhuyenMai.Trim(),
                    mucUuDai = model.MucUuDai,
                    ngayBatDau = model.NgayBatDau,
                    ngayKetThuc = model.NgayKetThuc,
                    dieuKienApDung = string.IsNullOrWhiteSpace(model.DieuKienApDung) ? "Toàn sàn" : model.DieuKienApDung.Trim(),
                    maNhanVienMarketing = marketingUserId
                };
                db.KhuyenMai.Add(km);

                var mgg = new MaGiamGia
                {
                    maGiamGia1 = mggId,
                    maCode = codeUpper,
                    giaTriGiam = model.GiaTriGiam > 0 ? model.GiaTriGiam : (decimal)model.MucUuDai,
                    trangThai = true,
                    maKhuyenMai = kmId
                };
                db.MaGiamGia.Add(mgg);

                db.SaveChanges();
                TempData["Success"] = "Đã khởi tạo chương trình khuyến mãi \"" + km.tenKhuyenMai + "\" (" + codeUpper + ") thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Đã xảy ra lỗi khi tạo chương trình khuyến mãi: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: Marketing/Promotion/ToggleStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleStatus(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index");
            }

            var km = db.KhuyenMai.Include("MaGiamGia").FirstOrDefault(c => c.maKhuyenMai == id);
            if (km != null)
            {
                var mgg = km.MaGiamGia.FirstOrDefault();
                if (mgg != null)
                {
                    // Toggle pause/active
                    mgg.trangThai = !(mgg.trangThai ?? true);
                    db.SaveChanges();
                    TempData["Success"] = mgg.trangThai == true ? "Đã kích hoạt lại mã coupon!" : "Đã tạm ngưng chương trình khuyến mãi!";
                }
            }

            return RedirectToAction("Index");
        }

        // POST: Marketing/Promotion/EndPromo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EndPromo(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index");
            }

            var km = db.KhuyenMai.Include("MaGiamGia").FirstOrDefault(c => c.maKhuyenMai == id);
            if (km != null)
            {
                km.ngayKetThuc = DateTime.Now.AddDays(-1);
                var mgg = km.MaGiamGia.FirstOrDefault();
                if (mgg != null)
                {
                    mgg.trangThai = false;
                }
                db.SaveChanges();
                TempData["Success"] = "Đã kết thúc chiến dịch khuyến mãi thành công!";
            }

            return RedirectToAction("Index");
        }
    }
}
