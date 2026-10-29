using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class TeacherApplicationController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: /Instructor/TeacherApplication/Register
        [HttpGet]
        public ActionResult Register()
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { area = "", returnUrl = Url.Action("Register", "TeacherApplication", new { area = "Instructor" }) });
            }

            var user = db.NguoiDung.Find(userId.Value);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // Nếu người dùng đã có role Giáo viên hoặc Admin -> Chuyển vào Dashboard
            if (string.Equals(user.vaiTro, "GiaoVien", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.vaiTro, "Giáo viên", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.vaiTro, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Instructor" });
            }

            // Kiểm tra trạng thái đơn đã nộp trước đó
            var existingApp = TeacherApplicationService.GetByUserId(user.maNguoiDung);
            if (existingApp != null)
            {
                if (existingApp.Status == "Pending")
                {
                    return RedirectToAction("Status");
                }
            }

            ViewBag.SpecialtyCategories = db.DanhMuc.Where(c => c.trangThai != false).OrderBy(c => c.tenDanhMuc).ToList();
            ViewBag.SelectedSpecialties = Request.HttpMethod == "POST" ? (Request.Form.GetValues("categoryIds") ?? new string[0]) : new string[0];
            ViewBag.User = user;
            ViewBag.ExistingApp = existingApp;

            // Nộp lại hồ sơ đã bị từ chối: điền sẵn thông tin cũ để người dùng chỉ sửa phần cần sửa
            var form = new TeacherRegisterFormViewModel { AccountName = BankDirectory.ChuanHoaTenChuTaiKhoan(user.hoTen) };
            if (existingApp != null)
            {
                // Tên ngân hàng cũ có thể gõ tự do ("ACB Chi nhánh Gò Mây"): nhận ra ngân hàng trong danh mục, nhắc kiểm tra lại
                var bank = BankDirectory.DoanNganHang(existingApp.BankName);

                form.BioExperience = existingApp.BioExperience;
                form.BankCode = bank != null ? bank.Code : null;
                form.BankNote = BankDirectory.GhiChuTenCu(existingApp.BankName, bank);
                form.AccountNumber = existingApp.AccountNumber;
                form.AccountName = string.IsNullOrWhiteSpace(existingApp.AccountName) ? form.AccountName : existingApp.AccountName;
            }
            ViewBag.Form = form;
            return View();
        }

        // POST: /Instructor/TeacherApplication/Register (Xử lý nộp hồ sơ)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string bioExperience, string bankCode, string accountNumber, string accountName, bool termsAgreed = false, HttpPostedFileBase certificateFile = null, string[] categoryIds = null)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var user = db.NguoiDung.Find(userId.Value);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            ViewBag.SpecialtyCategories = db.DanhMuc.Where(c => c.trangThai != false).OrderBy(c => c.tenDanhMuc).ToList();
            ViewBag.SelectedSpecialties = Request.HttpMethod == "POST" ? (Request.Form.GetValues("categoryIds") ?? new string[0]) : new string[0];
            ViewBag.User = user;
            ViewBag.ExistingApp = TeacherApplicationService.GetByUserId(user.maNguoiDung);
            // Giữ lại giá trị đã nhập nếu phải hiện lại form vì lỗi
            ViewBag.Form = new TeacherRegisterFormViewModel
            {

                BioExperience = bioExperience,
                BankCode = bankCode,
                AccountNumber = accountNumber,
                AccountName = accountName
            };

            var priorApp = TeacherApplicationService.GetByUserId(user.maNguoiDung);
            if (user.vaiTro == "GiaoVien" || user.vaiTro == "Admin" || (priorApp != null && priorApp.Status == "Pending"))
                return RedirectToAction("Status");
            try { new TeacherSpecialtyService(db).ValidateSelection(categoryIds); }
            catch (InvalidOperationException ex) { ModelState.AddModelError("categoryIds", ex.Message); }

            // 1. Kiểm tra validation các trường thông tin
            if (string.IsNullOrWhiteSpace(bioExperience) || bioExperience.Trim().Length < 50)
            {
                ModelState.AddModelError("bioExperience", "Mô tả giới thiệu bản thân & kinh nghiệm phải có tối thiểu 50 ký tự!");
            }

            // Thông tin nhận thanh toán: ngân hàng chỉ chọn từ danh mục (không gõ tự do), số tài khoản chỉ gồm chữ số,
            // tên chủ tài khoản IN HOA KHÔNG DẤU như ngân hàng ghi. Chuẩn hóa ở server, không tin dữ liệu trình duyệt gửi lên.
            var nganHang = BankDirectory.Find(bankCode);
            if (nganHang == null)
            {
                ModelState.AddModelError("bankCode", "Vui lòng chọn ngân hàng nhận thanh toán trong danh sách!");
            }
            string tenNganHang = nganHang != null ? nganHang.ShortName : null;

            string soTaiKhoan = BankDirectory.ChuanHoaSoTaiKhoan(accountNumber);
            if (!BankDirectory.SoTaiKhoanHopLe(soTaiKhoan))
            {
                ModelState.AddModelError("accountNumber", "Số tài khoản chỉ gồm chữ số, dài từ 6 đến 19 số!");
            }

            string tenChuTaiKhoan = BankDirectory.ChuanHoaTenChuTaiKhoan(accountName);
            if (tenChuTaiKhoan.Length < 2 || tenChuTaiKhoan.Length > 100)
            {
                ModelState.AddModelError("accountName", "Vui lòng nhập tên chủ tài khoản (in hoa, không dấu, như trên ứng dụng ngân hàng)!");
            }

            if (!termsAgreed)
            {
                ModelState.AddModelError("termsAgreed", "Bạn cần tích chọn đồng ý với Điều khoản hợp tác giảng dạy!");
            }

            // 2. Validate tệp đính kèm (PDF, PNG, JPG <= 5MB)
            string savedFilePath = null;
            string originalFileName = null;

            if (certificateFile == null || certificateFile.ContentLength == 0)
            {
                ModelState.AddModelError("certificateFile", "Vui lòng tải lên tệp bằng cấp hoặc chứng chỉ chuyên môn!");
            }
            else
            {
                if (certificateFile.ContentLength > 5 * 1024 * 1024) // 5MB limit
                {
                    ModelState.AddModelError("certificateFile", "Kích thước tệp chứng chỉ không được vượt quá 5MB!");
                }

                string ext = Path.GetExtension(certificateFile.FileName).ToLower();
                string[] validExts = { ".pdf", ".png", ".jpg", ".jpeg" };
                if (!validExts.Contains(ext))
                {
                    ModelState.AddModelError("certificateFile", "Định dạng tệp không hợp lệ! Hệ thống chỉ chấp nhận file .PDF, .PNG, .JPG, .JPEG!");
                }
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            // 3. Upload file lên server ~/assets/uploads/certificates/
            try
            {
                string uploadDir = Server.MapPath("~/assets/uploads/certificates/");
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                originalFileName = Path.GetFileName(certificateFile.FileName);
                string ext = Path.GetExtension(certificateFile.FileName).ToLower();
                string safeFileName = "cert_" + user.maNguoiDung + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ext;
                string fullPath = Path.Combine(uploadDir, safeFileName);

                certificateFile.SaveAs(fullPath);
                savedFilePath = Url.Content("~/assets/uploads/certificates/" + safeFileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Upload certificate error: " + ex.Message);
                ModelState.AddModelError("", "Không thể lưu tệp chứng chỉ. Vui lòng thử lại!");
                return View();
            }

            // Display summary only; category IDs are the authoritative specialty selection.
            string specialtySummary = string.Join(", ", db.DanhMuc.Where(c => categoryIds.Contains(c.maDanhMuc)).Select(c => c.tenDanhMuc).ToList());
            if (specialtySummary.Length > 200) specialtySummary = specialtySummary.Substring(0, 197) + "...";
            // 4. Tạo bản ghi hồ sơ mới
            TeacherApplication app = new TeacherApplication
            {
                ApplicationId = "GVAPP_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + user.maNguoiDung,
                UserId = user.maNguoiDung,
                FullName = user.hoTen,
                Email = user.email,
                Phone = user.soDienThoai,
                Specialization = specialtySummary,
                BioExperience = bioExperience.Trim(),
                BankName = tenNganHang,
                AccountNumber = soTaiKhoan,
                AccountName = tenChuTaiKhoan,
                CertificateFilePath = savedFilePath,
                CertificateFileName = originalFileName,
                CreatedAt = DateTime.Now,
                Status = "Pending"
            };

            TeacherApplicationService.Save(app);
            try { new TeacherSpecialtyService(db).SaveRequested(app.ApplicationId, user.maNguoiDung, categoryIds); }
            catch (InvalidOperationException ex) { ModelState.AddModelError("categoryIds", ex.Message); return View(); }

            return RedirectToAction("Status");
        }

        // GET: /Instructor/TeacherApplication/Status (Xem trạng thái xét duyệt hồ sơ)
        [HttpGet]
        public ActionResult Status()
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var user = db.NguoiDung.Find(userId.Value);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // Nếu tài khoản đã là Giáo viên -> Chuyển vào Dashboard
            if (string.Equals(user.vaiTro, "GiaoVien", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.vaiTro, "Giáo viên", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Instructor" });
            }

            var app = TeacherApplicationService.GetByUserId(user.maNguoiDung);
            if (app == null)
            {
                return RedirectToAction("Register");
            }

            return View(app);
        }

        private int? GetCurrentUserId()
        {
            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int id))
            {
                return id;
            }

            if (User.Identity.IsAuthenticated)
            {
                string email = User.Identity.Name;
                var user = db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == email.ToLower());
                if (user != null)
                {
                    Session["UserId"] = user.maNguoiDung;
                    return user.maNguoiDung;
                }
            }

            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && db != null)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
