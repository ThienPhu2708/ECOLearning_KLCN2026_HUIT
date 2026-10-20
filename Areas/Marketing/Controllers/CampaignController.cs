using System;
using System.Collections.Generic;
using System.Web.Mvc;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Areas.Marketing.Controllers
{
    /// <summary>
    /// Chiến dịch Marketing: theo dõi mục tiêu / thời gian / đối tượng và gắn các chương trình khuyến mãi liên quan.
    /// Không trùng chức năng CRUD khuyến mãi; kết thúc chiến dịch chỉ đổi trạng thái, giữ chương trình và giao dịch cũ.
    /// </summary>
    public class CampaignController : MarketingBaseController
    {
        private CampaignService Service => new CampaignService(db);

        private bool DaKhoiTao => new PromotionService(db).IsInstalled();

        // GET: Marketing/Campaign
        public ActionResult Index(string search = null, string status = null, string tuNgay = null, string denNgay = null)
        {
            if (!DaKhoiTao) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            DateTime? tu = DocNgay(tuNgay), den = DocNgay(denNgay);
            if (tu.HasValue && den.HasValue && den < tu)
            {
                TempData["Error"] = "Khoảng thời gian lọc không hợp lệ: ngày kết thúc trước ngày bắt đầu.";
                den = null;
            }

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = string.IsNullOrEmpty(status) ? "all" : status;
            ViewBag.TuNgay = tu?.ToString("yyyy-MM-dd");
            ViewBag.DenNgay = den?.ToString("yyyy-MM-dd");
            ViewBag.BreadcrumbCurrent = "Chiến dịch Marketing";
            return View(Service.LayDanhSach(search, status, tu, den));
        }

        // GET: Marketing/Campaign/Details/CD...
        public ActionResult Details(string id)
        {
            if (!DaKhoiTao) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            var c = Service.LayTheoMa(id);
            if (c == null) return HttpNotFound();
            ViewBag.ChuongTrinh = new PromotionService(db).LayDanhSach(maChienDich: id);
            ViewBag.BreadcrumbCurrent = "Chi tiết chiến dịch";
            return View(c);
        }

        // GET: Marketing/Campaign/Create
        public ActionResult Create()
        {
            if (!DaKhoiTao) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            return HienThiForm(new CampaignFormViewModel { NgayBatDau = DateTime.Today, NgayKetThuc = DateTime.Today.AddDays(30) });
        }

        // POST: Marketing/Campaign/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CampaignFormViewModel f)
        {
            if (!DaKhoiTao) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            f.MaChienDich = null;
            DocNgayForm(f);
            var service = Service;
            var loi = service.KiemTraForm(f, null);
            if (loi.Count > 0) return HienThiLoi(f, loi);

            try
            {
                string ma = service.Tao(f, MaNguoiDungHienTai);
                TempData["Success"] = "Đã tạo chiến dịch \"" + f.TenChienDich + "\".";
                return RedirectToAction("Details", new { id = ma });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Create campaign error: " + ex);
                ModelState.AddModelError("", "Không lưu được chiến dịch. Vui lòng thử lại.");
                return HienThiForm(f);
            }
        }

        // GET: Marketing/Campaign/Edit/CD...
        public ActionResult Edit(string id)
        {
            if (!DaKhoiTao) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            var service = Service;
            var c = service.LayTheoMa(id);
            if (c == null) return HttpNotFound();
            if (c.TrangThaiLuu == "KetThuc")
            {
                TempData["Error"] = "Chiến dịch đã kết thúc nên không thể chỉnh sửa.";
                return RedirectToAction("Details", new { id });
            }
            return HienThiForm(service.TaoFormTu(c));
        }

        // POST: Marketing/Campaign/Edit/CD...
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, CampaignFormViewModel f)
        {
            if (!DaKhoiTao) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            var service = Service;
            var c = service.LayTheoMa(id);
            if (c == null) return HttpNotFound();
            if (c.TrangThaiLuu == "KetThuc")
            {
                TempData["Error"] = "Chiến dịch đã kết thúc nên không thể chỉnh sửa.";
                return RedirectToAction("Details", new { id });
            }

            f.MaChienDich = c.MaChienDich;
            DocNgayForm(f);
            var loi = service.KiemTraForm(f, c);
            if (loi.Count > 0) return HienThiLoi(f, loi);

            try
            {
                service.CapNhat(c.MaChienDich, f);
                TempData["Success"] = "Đã cập nhật chiến dịch \"" + f.TenChienDich + "\".";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Edit campaign error: " + ex);
                ModelState.AddModelError("", "Không lưu được thay đổi. Vui lòng thử lại.");
                return HienThiForm(f);
            }
        }

        // POST: Marketing/Campaign/End/CD...
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult End(string id)
        {
            if (!DaKhoiTao) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            var c = Service.LayTheoMa(id);
            if (c == null) return HttpNotFound();
            if (c.TrangThaiLuu == "KetThuc")
            {
                TempData["Error"] = "Chiến dịch đã kết thúc trước đó.";
            }
            else
            {
                Service.KetThuc(id);
                TempData["Success"] = "Đã kết thúc chiến dịch \"" + c.TenChienDich + "\". Các chương trình khuyến mãi và giao dịch liên quan được giữ nguyên.";
            }
            return RedirectToAction("Details", new { id });
        }

        private void DocNgayForm(CampaignFormViewModel f)
        {
            f.NgayBatDau = DocNgay(Request.Form["NgayBatDau"]);
            f.NgayKetThuc = DocNgay(Request.Form["NgayKetThuc"]);
        }

        private ActionResult HienThiLoi(CampaignFormViewModel f, Dictionary<string, string> loi)
        {
            ModelState.Clear();
            foreach (var kv in loi) ModelState.AddModelError(kv.Key, kv.Value);
            return HienThiForm(f);
        }

        private ActionResult HienThiForm(CampaignFormViewModel f)
        {
            f.ChuongTrinhLuaChon = Service.LayChuongTrinhCoTheGan(f.MaChienDich);
            ViewBag.BreadcrumbCurrent = f.LaChinhSua ? "Sửa chiến dịch" : "Tạo chiến dịch";
            return View("Form", f);
        }
    }
}
