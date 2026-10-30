using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Areas.Marketing.Controllers
{
    /// <summary>
    /// Chiến dịch Marketing (đặc tả mục 4): mục tiêu, đối tượng, kênh, ngân sách / chi phí / chỉ tiêu, thời gian và 5 trạng thái
    /// Kế hoạch → Đang triển khai → (Tạm dừng) → Hoàn thành, hoặc Đã hủy. Gắn các chương trình khuyến mãi liên quan;
    /// trang chi tiết có lượt dùng, tiền giảm, doanh thu, % đạt chỉ tiêu và ROI.
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
            return HienThiForm(new CampaignFormViewModel
            {
                NgayBatDau = DateTime.Today,
                NgayKetThuc = DateTime.Today.AddDays(30),
                NganSach = 0,
                ChiPhiThucTe = 0
            });
        }

        // POST: Marketing/Campaign/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CampaignFormViewModel f, System.Web.HttpPostedFileBase anhQuangCao = null, bool hienPopup = false)
        {
            if (!DaKhoiTao) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            f.MaChienDich = null;
            DocNgayForm(f);
            var service = Service;
            var loi = service.KiemTraForm(f, null);
            AnhDaXuLy anh;
            var loiAnh = new QuangCaoService(db).KiemTraForm(QuangCaoService.LoaiChienDich, null, anhQuangCao, false, hienPopup, out anh);
            if (loiAnh != null) ViewBag.QuangCaoLoi = loiAnh;
            if (loi.Count > 0 || loiAnh != null) return HienThiLoi(f, loi);

            try
            {
                string ma = service.Tao(f, MaNguoiDungHienTai);
                new QuangCaoService(db).LuuTuForm(QuangCaoService.LoaiChienDich, ma, anh, false, hienPopup, Server);
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
            if (c.DaKetThuc)
            {
                TempData["Error"] = "Chiến dịch đã " + c.TrangThaiHienThi.ToLower() + " nên không thể chỉnh sửa.";
                return RedirectToAction("Details", new { id });
            }
            return HienThiForm(service.TaoFormTu(c));
        }

        // POST: Marketing/Campaign/Edit/CD...
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, CampaignFormViewModel f, System.Web.HttpPostedFileBase anhQuangCao = null, bool xoaAnh = false, bool hienPopup = false)
        {
            if (!DaKhoiTao) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            var service = Service;
            var c = service.LayTheoMa(id);
            if (c == null) return HttpNotFound();
            if (c.DaKetThuc)
            {
                TempData["Error"] = "Chiến dịch đã " + c.TrangThaiHienThi.ToLower() + " nên không thể chỉnh sửa.";
                return RedirectToAction("Details", new { id });
            }

            f.MaChienDich = c.MaChienDich;
            DocNgayForm(f);
            var loi = service.KiemTraForm(f, c);
            AnhDaXuLy anh;
            var loiAnh = new QuangCaoService(db).KiemTraForm(QuangCaoService.LoaiChienDich, c.MaChienDich, anhQuangCao, xoaAnh, hienPopup, out anh);
            if (loiAnh != null) ViewBag.QuangCaoLoi = loiAnh;
            if (loi.Count > 0 || loiAnh != null) return HienThiLoi(f, loi);

            try
            {
                service.CapNhat(c.MaChienDich, f);
                new QuangCaoService(db).LuuTuForm(QuangCaoService.LoaiChienDich, c.MaChienDich, anh, xoaAnh, hienPopup, Server);
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

        // POST: Marketing/Campaign/ChangeStatus/CD... — trangThai: DangTrienKhai / TamDung / HoanThanh / DaHuy
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeStatus(string id, string trangThai)
        {
            if (!DaKhoiTao) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            bool ok;
            string message = Service.DoiTrangThai(id, trangThai, out ok);
            TempData[ok ? "Success" : "Error"] = message;
            return RedirectToAction("Details", new { id });
        }

        // POST: Marketing/Campaign/Delete/CD... — chỉ khi chưa gắn khuyến mãi nào
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id)
        {
            if (!DaKhoiTao) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            bool ok;
            string message = Service.Xoa(id, out ok);
            TempData[ok ? "Success" : "Error"] = message;
            return ok ? RedirectToAction("Index") : RedirectToAction("Details", new { id });
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
            var qc = new QuangCaoService(db);
            ViewBag.QuangCaoCoSan = qc.IsInstalled();
            ViewBag.QuangCao = qc.Lay(QuangCaoService.LoaiChienDich, f.MaChienDich);
            ViewBag.BreadcrumbCurrent = f.LaChinhSua ? "Sửa chiến dịch" : "Tạo chiến dịch";
            return View("Form", f);
        }
    }
}
