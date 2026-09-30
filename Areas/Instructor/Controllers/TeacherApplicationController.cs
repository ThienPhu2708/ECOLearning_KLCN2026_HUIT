using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

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

            ViewBag.User = user;
            ViewBag.ExistingApp = existingApp;
            return View();
        }

        // POST: /Instructor/TeacherApplication/Register (Xử lý nộp hồ sơ)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string specialization, string bioExperience, string bankName, string accountNumber, string accountName, bool termsAgreed = false, HttpPostedFileBase certificateFile = null)
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

            ViewBag.User = user;

            // 1. Kiểm tra validation các trường thông tin
            if (string.IsNullOrWhiteSpace(specialization))
            {
                ModelState.AddModelError("specialization", "Vui lòng nhập Lĩnh vực chuyên môn giảng dạy!");
            }

            if (string.IsNullOrWhiteSpace(bioExperience) || bioExperience.Trim().Length < 50)
            {
                ModelState.AddModelError("bioExperience", "Mô tả giới thiệu bản thân & kinh nghiệm phải có tối thiểu 50 ký tự!");
            }

            if (string.IsNullOrWhiteSpace(bankName) || string.IsNullOrWhiteSpace(accountNumber) || string.IsNullOrWhiteSpace(accountName))
            {
                ModelState.AddModelError("bankInfo", "Vui lòng nhập đầy đủ thông tin nhận thanh toán (Tên ngân hàng, Số TK, Tên chủ TK)!");
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

            // 4. Tạo bản ghi hồ sơ mới
            TeacherApplication app = new TeacherApplication
            {
                ApplicationId = "GVAPP_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + user.maNguoiDung,
                UserId = user.maNguoiDung,
                FullName = user.hoTen,
                Email = user.email,
                Phone = user.soDienThoai,
                Specialization = specialization.Trim(),
                BioExperience = bioExperience.Trim(),
                BankName = bankName.Trim(),
                AccountNumber = accountNumber.Trim(),
                AccountName = accountName.Trim().ToUpper(),
                CertificateFilePath = savedFilePath,
                CertificateFileName = originalFileName,
                CreatedAt = DateTime.Now,
                Status = "Pending"
            };

            TeacherApplicationService.Save(app);

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
