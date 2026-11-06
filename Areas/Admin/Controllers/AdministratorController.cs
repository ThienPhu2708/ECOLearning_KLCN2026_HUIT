using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Areas.Admin.Controllers
{
    public class AdministratorController : AdminBaseController
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        public ActionResult Index(string q = null, string status = null, int trang = 1)
            => View(Page(q, status, trang));

        [HttpPost, ValidateAntiForgeryToken, ValidateInput(false)]
        public ActionResult Create([Bind(Prefix = "NewAdmin", Include = "FullName,Email,Role,ConfirmAdmin")] NewAdministratorViewModel input)
        {
            input = input ?? new NewAdministratorViewModel();
            // Trang này chỉ tạo nhân sự phụ trách kiểm duyệt; không dùng để nhân bản Admin gốc.
            bool roleHopLe = input.Role == AccountAdminService.AdminKiemDuyetNoiDung;
            if (!roleHopLe) ModelState.AddModelError("NewAdmin.Role", "Vai trò không hợp lệ.");
            if (!input.ConfirmAdmin) ModelState.AddModelError("NewAdmin.ConfirmAdmin", "Vui lòng xác nhận cấp vai trò cho tài khoản mới.");
            if (!new AccountAdminService(db).IsInstalled())
                ModelState.AddModelError("", "Chưa khởi tạo dữ liệu quản lý tài khoản.");
            if (ModelState.IsValid)
            {
                // Chỉ Admin gốc dùng trang này; vai trò gửi lên luôn được whitelist ở server.
                var result = new AccountAdminService(db).TaoNhanVien(Convert.ToInt32(Session["UserId"]),
                    input.FullName, input.Email, input.Role);
                if (result.ThanhCong)
                {
                    TempData["SuccessMessage"] = result.ThongBao;
                    return RedirectToAction("Index");
                }
                ModelState.AddModelError("", result.ThongBao);
            }
            var page = Page(null, null, 1);
            page.NewAdmin = input;
            return View("Index", page);
        }

        private AdministratorViewModel Page(string q, string status, int trang)
        {
            ViewBag.Title = "Quản lý Admin";
            ViewBag.BreadcrumbCurrent = "Quản lý Admin";
            var management = new AccountAdminService(db);
            var page = new AdministratorViewModel { Installed = management.IsInstalled(),
                CurrentUserId = Convert.ToInt32(Session["UserId"]), Query = q,
                Status = status == "active" || status == "locked" ? status : null };
            if (!page.Installed) return page;
            management.MoKhoaHetHan();
            var admins = db.NguoiDung.Where(u => u.vaiTro == AccountAdminService.Admin || u.vaiTro == AccountAdminService.AdminKiemDuyetNoiDung);
            page.Total = admins.Count();
            page.Active = admins.Count(u => u.trangThai != false);
            PhanTrangViewModel pagination;
            page.Administrators = new AdminUserQueryService(db).DanhSach(q, null, page.Status, trang, out pagination,
                new[] { AccountAdminService.Admin, AccountAdminService.AdminKiemDuyetNoiDung });
            pagination.DonVi = "tài khoản quản trị";
            page.Pagination = pagination;
            return page;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
