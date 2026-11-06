using System;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Admin.Controllers
{
    /// <summary>
    /// Admin / Quản lý người dùng: danh sách, chi tiết, khóa / mở khóa có thời hạn, cảnh cáo,
    /// đặt lại mật khẩu (mật khẩu tạm gửi qua email), tạo tài khoản nhân viên Marketing / Admin / Kiểm duyệt nội dung.
    /// Quy tắc nghiệp vụ nằm ở AccountAdminService.
    /// </summary>
    public class UserController : AdminBaseController
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private int AdminId => Convert.ToInt32(Session["UserId"]);

        // GET: Admin/User
        public ActionResult Index(string q = null, string role = null, string status = null, int trang = 1)
        {
            ViewBag.BreadcrumbCurrent = "Quản lý người dùng";
            var quanTri = new AccountAdminService(db);
            if (!quanTri.IsInstalled())
            {
                ViewBag.ChuaKhoiTao = true;
                return View();
            }

            quanTri.MoKhoaHetHan();
            var service = new AdminUserQueryService(db);
            if (role != AccountAdminService.HocVien && role != AccountAdminService.GiaoVien && role != AccountAdminService.Marketing
                && role != AccountAdminService.Admin && role != AccountAdminService.AdminKiemDuyetNoiDung) role = null;
            if (status != "active" && status != "locked") status = null;

            ECOLearning.ViewModels.PhanTrangViewModel phanTrang;
            ViewBag.DanhSach = service.DanhSach(q, role, status, trang, out phanTrang);
            ViewBag.PhanTrang = phanTrang;
            ViewBag.ThongKe = service.ThongKe();
            ViewBag.Q = q;
            ViewBag.Role = role;
            ViewBag.Status = status;
            return View();
        }

        // GET: Admin/User/Details/5
        public ActionResult Details(int id)
        {
            var quanTri = new AccountAdminService(db);
            if (!quanTri.IsInstalled()) return RedirectToAction("Index");

            quanTri.MoKhoaHetHan(id);
            var model = new AdminUserQueryService(db).ChiTiet(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                return RedirectToAction("Index");
            }
            // Giáo viên có trang hồ sơ riêng (khóa học, doanh thu, vi phạm)
            if (model.VaiTro == AccountAdminService.GiaoVien)
            {
                return RedirectToAction("Details", "Teacher", new { id });
            }

            ViewBag.BreadcrumbParent = "Quản lý người dùng";
            ViewBag.BreadcrumbParentUrl = Url.Action("Index");
            ViewBag.BreadcrumbCurrent = model.HoTen;
            ViewBag.LaChinhMinh = id == AdminId;
            return View(model);
        }

        // POST: Admin/User/Lock
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Lock(int id, string lyDo, int soNgay = 7, string returnUrl = null)
        {
            return KetQua(new AccountAdminService(db).Khoa(AdminId, id, lyDo, soNgay), id, returnUrl);
        }

        // POST: Admin/User/Unlock
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Unlock(int id, string ghiChu, string returnUrl = null)
        {
            return KetQua(new AccountAdminService(db).MoKhoa(AdminId, id, ghiChu), id, returnUrl);
        }

        // POST: Admin/User/Warn
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Warn(int id, string noiDung, string returnUrl = null)
        {
            return KetQua(new AccountAdminService(db).CanhCao(AdminId, id, noiDung), id, returnUrl);
        }

        // POST: Admin/User/ResetPassword
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ResetPassword(int id, string returnUrl = null)
        {
            return KetQua(new AccountAdminService(db).DatLaiMatKhau(AdminId, id), id, returnUrl);
        }

        // POST: Admin/User/CreateStaff
        // ValidateInput(false): ký tự như "<", "&#" không làm hiện trang lỗi của ASP.NET mà được AccountAdminService.TaoNhanVien
        // từ chối bằng câu báo bình thường (họ tên chỉ nhận chữ cái, email kiểm tra bằng regex; mọi chỗ hiển thị đều mã hóa HTML)
        [HttpPost, ValidateAntiForgeryToken, ValidateInput(false)]
        public ActionResult CreateStaff(string hoTen, string email, string vaiTro)
        {
            var kq = new AccountAdminService(db).TaoNhanVien(AdminId, hoTen, email, vaiTro);
            TempData[kq.ThanhCong ? "SuccessMessage" : "ErrorMessage"] = kq.ThongBao;
            if (kq.ThanhCong && kq.MaMoi.HasValue) return RedirectToAction("Details", new { id = kq.MaMoi.Value });

            // Lỗi (trùng email...): chưa tạo gì nên không cần hoàn tác; mở lại hộp tạo tài khoản với dữ liệu đã nhập,
            // báo lỗi ngay trong hộp thay vì thanh thông báo đầu trang
            TempData.Remove("ErrorMessage");
            TempData["StaffForm"] = new[] { hoTen ?? "", email ?? "", vaiTro ?? "", kq.ThongBao };
            return RedirectToAction("Index");
        }

        private ActionResult KetQua(KetQuaQuanTri kq, int id, string returnUrl)
        {
            TempData[kq.ThanhCong ? "SuccessMessage" : "ErrorMessage"] = kq.ThongBao;
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Details", new { id });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
