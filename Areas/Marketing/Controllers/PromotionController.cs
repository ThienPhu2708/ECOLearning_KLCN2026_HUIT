using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Areas.Marketing.Controllers
{
    /// <summary>
    /// UC17: Quản lý chương trình khuyến mãi & coupon (chỉ Marketing / Admin; giáo viên không tự tạo giảm giá).
    /// Chương trình đã phát sinh giao dịch không bị xóa: chỉ tạm ngừng hoặc kết thúc, lịch sử giữ nguyên.
    /// </summary>
    public class PromotionController : MarketingBaseController
    {
        private PromotionService Service => new PromotionService(db);

        // GET: Marketing/Promotion
        public ActionResult Index(string search = null, string status = null, string doiTuong = null,
            string tuNgay = null, string denNgay = null, string chienDich = null)
        {
            if (!Service.IsInstalled()) return ChuaKhoiTao();

            DateTime? tu = DocNgay(tuNgay), den = DocNgay(denNgay);
            if (tu.HasValue && den.HasValue && den < tu)
            {
                TempData["Error"] = "Khoảng thời gian lọc không hợp lệ: ngày kết thúc trước ngày bắt đầu.";
                den = null;
            }

            var list = Service.LayDanhSach(search, status, doiTuong, tu, den, chienDich);

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = string.IsNullOrEmpty(status) ? "all" : status;
            ViewBag.CurrentDoiTuong = doiTuong;
            ViewBag.TuNgay = tu?.ToString("yyyy-MM-dd");
            ViewBag.DenNgay = den?.ToString("yyyy-MM-dd");
            ViewBag.ChienDich = chienDich;
            ViewBag.BreadcrumbCurrent = "Chương trình khuyến mãi";
            return View(list);
        }

        // GET: Marketing/Promotion/Details/KM...
        public ActionResult Details(string id)
        {
            if (!Service.IsInstalled()) return ChuaKhoiTao();
            var p = Service.LayTheoMa(id);
            if (p == null) return HttpNotFound();

            ViewBag.KhoaHoc = Service.LayKhoaHocApDung(id);
            ViewBag.LuotSuDung = Service.LayLuotSuDung(id);
            ViewBag.BreadcrumbCurrent = "Chi tiết chương trình";
            return View(p);
        }

        // GET: Marketing/Promotion/Create?doiTuong=ThanThiet
        public ActionResult Create(string doiTuong = null, string chienDich = null)
        {
            if (!Service.IsInstalled()) return ChuaKhoiTao();
            var f = new PromotionFormViewModel
            {
                DoiTuong = doiTuong == "ThanThiet" ? "ThanThiet" : "TatCa",
                MaChienDich = chienDich,
                NgayBatDau = DateTime.Today,
                NgayKetThuc = DateTime.Today.AddDays(30)
            };
            return HienThiForm(f);
        }

        // POST: Marketing/Promotion/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(PromotionFormViewModel f)
        {
            if (!Service.IsInstalled()) return ChuaKhoiTao();
            f.MaKhuyenMai = null;
            DocNgayForm(f);

            var service = Service;
            var loi = service.KiemTraForm(f, null);
            if (loi.Count > 0) return HienThiLoi(f, loi);

            try
            {
                string ma = service.Tao(f, MaNguoiDungHienTai);
                TempData["Success"] = "Đã tạo chương trình \"" + f.TenKhuyenMai + "\" với mã " + f.MaCode + ".";
                return RedirectToAction("Details", new { id = ma });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Create promotion error: " + ex);
                ModelState.AddModelError("", "Không lưu được chương trình. Vui lòng thử lại.");
                return HienThiForm(f);
            }
        }

        // GET: Marketing/Promotion/Edit/KM...
        public ActionResult Edit(string id)
        {
            if (!Service.IsInstalled()) return ChuaKhoiTao();
            var service = Service;
            var p = service.LayTheoMa(id);
            if (p == null) return HttpNotFound();
            if (p.TrangThaiHienThi(DateTime.Now) == "Đã kết thúc")
            {
                TempData["Error"] = "Chương trình đã kết thúc nên không thể chỉnh sửa.";
                return RedirectToAction("Details", new { id });
            }
            return HienThiForm(service.TaoFormTu(p));
        }

        // POST: Marketing/Promotion/Edit/KM...
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, PromotionFormViewModel f)
        {
            if (!Service.IsInstalled()) return ChuaKhoiTao();
            var service = Service;
            var p = service.LayTheoMa(id);
            if (p == null) return HttpNotFound();
            if (p.TrangThaiHienThi(DateTime.Now) == "Đã kết thúc")
            {
                TempData["Error"] = "Chương trình đã kết thúc nên không thể chỉnh sửa.";
                return RedirectToAction("Details", new { id });
            }

            f.MaKhuyenMai = p.MaKhuyenMai;
            f.SoLuotDaDung = p.SoLuotDaDung;
            DocNgayForm(f);
            var loi = service.KiemTraForm(f, p);
            if (loi.Count > 0) return HienThiLoi(f, loi);

            try
            {
                service.CapNhat(p, f);
                TempData["Success"] = "Đã cập nhật chương trình \"" + f.TenKhuyenMai + "\". Các đơn hàng đã hoàn tất không bị ảnh hưởng.";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Edit promotion error: " + ex);
                ModelState.AddModelError("", "Không lưu được thay đổi. Vui lòng thử lại.");
                return HienThiForm(f);
            }
        }

        // POST: Marketing/Promotion/ChangeStatus — hanhDong: pause / resume / end
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeStatus(string id, string hanhDong, string returnUrl = null)
        {
            if (!Service.IsInstalled()) return ChuaKhoiTao();
            bool ok;
            string message = Service.DoiTrangThai(id, hanhDong, out ok);
            TempData[ok ? "Success" : "Error"] = message;

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Details", new { id });
        }

        // ------------------------------------------------------------------ hỗ trợ

        private void DocNgayForm(PromotionFormViewModel f)
        {
            // Ô ngày gửi yyyy-MM-dd; đọc theo định dạng cố định để không phụ thuộc culture vi-VN
            f.NgayBatDau = DocNgay(Request.Form["NgayBatDau"]);
            f.NgayKetThuc = DocNgay(Request.Form["NgayKetThuc"]);
        }

        private ActionResult HienThiLoi(PromotionFormViewModel f, System.Collections.Generic.Dictionary<string, string> loi)
        {
            ModelState.Clear();
            foreach (var kv in loi) ModelState.AddModelError(kv.Key, kv.Value);
            return HienThiForm(f);
        }

        private ActionResult HienThiForm(PromotionFormViewModel f)
        {
            var service = Service;
            f.KhoaHocLuaChon = service.LayKhoaHocCoTheApDung();
            // Khi sửa, vẫn hiện các khóa đã gắn trước đó dù nay không còn mở bán
            if (f.LaChinhSua)
            {
                foreach (var k in service.LayKhoaHocApDung(f.MaKhuyenMai).Where(k => f.KhoaHocLuaChon.All(x => x.MaKhoaHoc != k.MaKhoaHoc)))
                    f.KhoaHocLuaChon.Add(k);
            }
            f.ChienDichLuaChon = service.LayChienDichDangMo();
            if (!string.IsNullOrEmpty(f.MaChienDich) && f.ChienDichLuaChon.All(c => c.MaChienDich != f.MaChienDich))
            {
                var cd = new CampaignService(db).LayTheoMa(f.MaChienDich);
                if (cd != null) f.ChienDichLuaChon.Add(new LuaChonChienDich { MaChienDich = cd.MaChienDich, TenChienDich = cd.TenChienDich });
            }
            ViewBag.BreadcrumbCurrent = f.LaChinhSua ? "Sửa chương trình" : "Tạo chương trình";
            return View("Form", f);
        }

        private ActionResult ChuaKhoiTao()
        {
            ViewBag.BreadcrumbCurrent = "Chưa khởi tạo";
            return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
        }
    }
}
