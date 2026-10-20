using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    public class AccountController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Account/Login (Hiển thị form Đăng nhập / Đăng ký UI/UX)
        [HttpGet]
        public ActionResult Login(string returnUrl, string mode = "login")
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.AuthMode = string.IsNullOrEmpty(mode) ? "login" : mode.ToLower();

            // Nếu học viên đã đăng nhập rồi thì chuyển hướng về Trang chủ
            if (User.Identity.IsAuthenticated || Session["UserId"] != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // GET: Account/Register (Alias cho tab Đăng ký)
        [HttpGet]
        public ActionResult Register(string returnUrl)
        {
            return RedirectToAction("Login", new { returnUrl = returnUrl, mode = "register" });
        }

        // POST: Account/Login (Xử lý đăng nhập - OWASP ASVS v4.0 & NIST SP 800-63B Compliant)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AuthMode = "login";
                return View(model);
            }

            string lowerEmail = model.Email.Trim().ToLower();

            try
            {
                // 1. Kiểm tra qua Entity Framework NguoiDung với PasswordHasher.VerifyPassword
                var user = db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == lowerEmail);
                if (user != null)
                {
                    if (user.trangThai.HasValue && !user.trangThai.Value)
                    {
                        ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa hoặc chưa được kích hoạt.");
                        ViewBag.AuthMode = "login";
                        return View(model);
                    }

                    // Xác thực mật khẩu đã được mã hóa Hash
                    if (PasswordHasher.VerifyPassword(user.matKhau, model.Password))
                    {
                        SetUserSessionAndAuthCookie(user.maNguoiDung, user.hoTen, user.email, user.vaiTro, user.anhDaiDien, model.RememberMe);

                        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                        {
                            return Redirect(model.ReturnUrl);
                        }

                        // Phân quyền điều hướng theo vai trò
                        if (string.Equals(user.vaiTro, "Admin", StringComparison.OrdinalIgnoreCase))
                        {
                            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                        }
                        else if (string.Equals(user.vaiTro, "Marketing", StringComparison.OrdinalIgnoreCase))
                        {
                            return RedirectToAction("Index", "Dashboard", new { area = "Marketing" });
                        }

                        // Giáo viên và Học viên sau khi đăng nhập đều chuyển đến Trang chủ trước
                        return RedirectToAction("Index", "Home");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("EF Login Exception: " + ex.Message);
            }

            try
            {
                // 2. Thử xác thực qua Stored Procedure sp_DangNhap (Dự phòng)
                var spResult = db.sp_DangNhap(model.Email, model.Password).FirstOrDefault();
                if (spResult != null && spResult.maNguoiDung > 0)
                {
                    SetUserSessionAndAuthCookie(spResult.maNguoiDung, spResult.hoTen, spResult.email, spResult.vaiTro, spResult.anhDaiDien, model.RememberMe);

                    if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }

                    if (string.Equals(spResult.vaiTro, "Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                    }
                    else if (string.Equals(spResult.vaiTro, "Marketing", StringComparison.OrdinalIgnoreCase))
                    {
                        return RedirectToAction("Index", "Dashboard", new { area = "Marketing" });
                    }

                    // Giáo viên và Học viên sau khi đăng nhập đều chuyển đến Trang chủ trước
                    return RedirectToAction("Index", "Home");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("sp_DangNhap Exception: " + ex.Message);
            }

            // OWASP ASVS v4.0 2.1.1: Thông báo lỗi đăng nhập chung chung phòng chống User Enumeration
            ModelState.AddModelError("", "Tài khoản hoặc mật khẩu không chính xác.");
            ViewBag.AuthMode = "login";
            return View(model);
        }

        // POST: Account/CheckEmailExists (AJAX cho Bước 1 Đăng ký)
        [HttpPost]
        public JsonResult CheckEmailExists(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { exists = false, message = "Email không được để trống" });
            }

            string lowerEmail = email.Trim().ToLower();
            bool isExist = false;

            try
            {
                isExist = db.NguoiDung.Any(u => u.email.ToLower() == lowerEmail);
            }
            catch
            {
                isExist = false;
            }

            return Json(new { exists = isExist, message = isExist ? "Email này đã được đăng ký trên hệ thống!" : "Email hợp lệ" });
        }

        // POST: Account/SendOtp (AJAX tạo mã OTP 6 số ngẫu nhiên & gửi qua Email)
        [HttpPost]
        public JsonResult SendOtp(string fullName, string email)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return Json(new { success = false, message = "Vui lòng nhập Họ và tên!" });
            }

            string trimmedName = fullName.Trim();
            bool hasDigit = trimmedName.Any(char.IsDigit);
            bool hasSpecial = trimmedName.Any(ch => !char.IsLetter(ch) && !char.IsWhiteSpace(ch));

            if (hasDigit && hasSpecial)
            {
                return Json(new { success = false, message = "Họ và tên chỉ được chứa chữ cái, không được chứa chữ số và ký tự đặc biệt!" });
            }
            if (hasDigit)
            {
                return Json(new { success = false, message = "Họ và tên không được chứa chữ số (0-9)!" });
            }
            if (hasSpecial)
            {
                return Json(new { success = false, message = "Họ và tên không được chứa ký tự đặc biệt!" });
            }
            if (trimmedName.Length < 2)
            {
                return Json(new { success = false, message = "Họ và tên phải có tối thiểu 2 ký tự!" });
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "Email không được để trống!" });
            }

            string lowerEmail = email.Trim().ToLower();

            if (!System.Text.RegularExpressions.Regex.IsMatch(lowerEmail, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                return Json(new { success = false, message = "Định dạng Email không hợp lệ (ví dụ: tenban@gmail.com)!" });
            }

            try
            {
                bool isExist = db.NguoiDung.Any(u => u.email.ToLower() == lowerEmail);
                if (isExist)
                {
                    return Json(new { success = false, message = "Email này đã được đăng ký trên hệ thống!" });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SendOtp Check Error: " + ex.Message);
            }

            // Tạo mã OTP ngẫu nhiên 6 chữ số
            Random rand = new Random();
            string otpCode = rand.Next(100000, 999999).ToString();
            DateTime expiryTime = DateTime.Now.AddMinutes(5); // Mã hết hạn sau 5 phút (NIST SP 800-63B)

            // Khởi tạo thông tin và cờ xác thực OTP trong Session
            Session["Register_Email"] = lowerEmail;
            Session["Register_FullName"] = string.IsNullOrEmpty(fullName) ? null : fullName.Trim();
            Session["Register_OTP"] = otpCode;
            Session["Register_OTP_Expiry"] = expiryTime;
            Session["Register_OTP_Attempts"] = 0; // Đếm số lần nhập sai
            Session["Register_OtpVerified"] = false; // Cờ xác thực OTP Server-side
            Session["Register_OtpVerified_Email"] = null;

            // Gửi email chứa mã OTP thực tế
            var mailResult = EmailService.SendOtpEmail(lowerEmail, fullName, otpCode, "Mã xác thực OTP đăng ký tài khoản - E-Learning");

            if (mailResult.Success)
            {
                return Json(new { success = true, message = "Mã xác thực OTP đã được gửi đến email " + email + ". Vui lòng kiểm tra hộp thư (kể cả mục Thư rác/Spam)." });
            }
            else
            {
                return Json(new { success = false, message = "Không thể gửi email OTP: " + mailResult.Message + ". Vui lòng thử lại sau!" });
            }
        }

        // POST: Account/VerifyRegisterOtp (AJAX Xác thực OTP Bước 2 - Đánh cờ Session thành công ở Server-side)
        [HttpPost]
        public JsonResult VerifyRegisterOtp(string email, string otp)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ Email và mã OTP 6 số!" });
            }

            string lowerEmail = email.Trim().ToLower();
            string sessionEmail = Session["Register_Email"] as string;
            string sessionOtp = Session["Register_OTP"] as string;
            DateTime? expiryTime = Session["Register_OTP_Expiry"] as DateTime?;
            int attempts = (Session["Register_OTP_Attempts"] as int?) ?? 0;

            if (string.IsNullOrEmpty(sessionEmail) || sessionEmail != lowerEmail)
            {
                return Json(new { success = false, message = "Email không trùng khớp với thông tin đã gửi OTP!" });
            }

            // OWASP ASVS v4.0 Sec 2.2.1 & NIST SP 800-63B: Giới hạn tối đa 5 lần nhập sai OTP (Anti Brute-Force)
            if (attempts >= 5)
            {
                ClearRegisterSession();
                return Json(new { success = false, message = "Bạn đã nhập sai mã OTP quá 5 lần. Mã OTP đã bị vô hiệu hóa, vui lòng yêu cầu mã OTP mới!" });
            }

            // Kiểm tra thời gian hết hạn OTP (5 phút)
            if (expiryTime.HasValue && DateTime.Now > expiryTime.Value)
            {
                return Json(new { success = false, message = "Mã OTP đã hết hạn (sau 5 phút). Vui lòng nhấn gửi lại mã OTP mới!" });
            }

            // So khớp mã OTP thực tế (Tuyệt đối không có backdoor 123456)
            if (otp.Trim() != sessionOtp)
            {
                attempts++;
                Session["Register_OTP_Attempts"] = attempts;
                int remaining = 5 - attempts;
                return Json(new { success = false, message = string.Format("Mã xác thực OTP không chính xác! Còn lại {0} lần thử.", remaining) });
            }

            // Xác thực OTP thành công -> Gán cờ Server-side lock để cho phép Bước 3 tạo mật khẩu
            Session["Register_OtpVerified"] = true;
            Session["Register_OtpVerified_Email"] = lowerEmail;

            return Json(new { success = true, message = "Xác thực mã OTP thành công! Vui lòng thiết lập mật khẩu mới." });
        }

        // POST: Account/VerifyOtpAndRegister (AJAX Hoàn tất Đăng ký ở Bước 3 - Kiểm tra cờ Server OTP & Mã hóa Mật khẩu)
        [HttpPost]
        public JsonResult VerifyOtpAndRegister(string fullName, string email, string otp, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return Json(new { success = false, message = "Thông tin đăng ký không đầy đủ!" });
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = Session["Register_FullName"] as string;
            }

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                string trimmedName = fullName.Trim();
                bool hasDigit = trimmedName.Any(char.IsDigit);
                bool hasSpecial = trimmedName.Any(ch => !char.IsLetter(ch) && !char.IsWhiteSpace(ch));
                if (hasDigit && hasSpecial)
                {
                    return Json(new { success = false, message = "Họ và tên chỉ được chứa chữ cái, không được chứa chữ số và ký tự đặc biệt!" });
                }
                if (hasDigit)
                {
                    return Json(new { success = false, message = "Họ và tên không được chứa chữ số (0-9)!" });
                }
                if (hasSpecial)
                {
                    return Json(new { success = false, message = "Họ và tên không được chứa ký tự đặc biệt!" });
                }
            }

            string lowerEmail = email.Trim().ToLower();

            // CHỐNG BYPASS OTP SERVER-SIDE: Bắt buộc kiểm tra cờ Session["Register_OtpVerified"]
            bool isOtpVerified = (Session["Register_OtpVerified"] as bool?) ?? false;
            string verifiedEmail = Session["Register_OtpVerified_Email"] as string;

            if (!isOtpVerified || string.IsNullOrEmpty(verifiedEmail) || verifiedEmail != lowerEmail)
            {
                return Json(new { success = false, message = "Phiên xác thực OTP không hợp lệ hoặc chưa hoàn tất Bước 2! Vui lòng xác thực mã OTP trước khi tạo mật khẩu." });
            }

            // OWASP ASVS v4.0 & Ràng buộc bảo mật mật khẩu (tối thiểu 8 ký tự, có in hoa, số và ký tự đặc biệt)
            var missingRules = new List<string>();
            if (string.IsNullOrEmpty(password) || password.Length < 8)
            {
                missingRules.Add("tối thiểu 8 ký tự");
            }
            if (string.IsNullOrEmpty(password) || !password.Any(char.IsUpper))
            {
                missingRules.Add("chữ in hoa (A-Z)");
            }
            if (string.IsNullOrEmpty(password) || !password.Any(char.IsDigit))
            {
                missingRules.Add("chữ số (0-9)");
            }
            if (string.IsNullOrEmpty(password) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                missingRules.Add("ký tự đặc biệt (!@#$%^&*...)");
            }

            if (missingRules.Any())
            {
                return Json(new { success = false, message = "Mật khẩu còn thiếu: " + string.Join(", ", missingRules) + "." });
            }

            // OWASP ASVS v4.0 Sec 2.4: Mã hóa mật khẩu an toàn với Salt (PBKDF2/HMAC-SHA256) trước khi ghi CSDL
            string hashedPassword = PasswordHasher.HashPassword(password);

            try
            {
                // 1. Thử gọi Stored Procedure sp_DangKyTaiKhoan
                var spResult = db.sp_DangKyTaiKhoan(fullName, lowerEmail, hashedPassword, "HocVien", null, null, null).FirstOrDefault();
                if (spResult != null && spResult.maNguoiDungMoi.HasValue && spResult.maNguoiDungMoi.Value > 0)
                {
                    ClearRegisterSession();
                    return Json(new { success = true, message = "Đăng ký tài khoản thành công! Vui lòng đăng nhập với mật khẩu vừa tạo." });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("sp_DangKyTaiKhoan Exception: " + ex.Message);
            }

            try
            {
                // 2. Dự phòng: Thêm trực tiếp vào Entity Framework DbSet NguoiDung
                bool isExist = db.NguoiDung.Any(u => u.email.ToLower() == lowerEmail);
                if (isExist)
                {
                    return Json(new { success = false, message = "Email này đã được đăng ký trên hệ thống trước đó!" });
                }

                NguoiDung newStudent = new NguoiDung
                {
                    hoTen = string.IsNullOrWhiteSpace(fullName) ? lowerEmail.Split('@')[0] : fullName.Trim(),
                    email = lowerEmail,
                    matKhau = hashedPassword, // Lưu chuỗi Hash an toàn
                    vaiTro = "HocVien",
                    trangThai = true,
                    anhDaiDien = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=100&auto=format&fit=crop&q=80"
                };

                db.NguoiDung.Add(newStudent);
                db.SaveChanges();

                ClearRegisterSession();
                return Json(new { success = true, message = "Đăng ký tài khoản thành công! Vui lòng đăng nhập với mật khẩu vừa tạo." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DbSet Add Error: " + ex.Message);
            }

            ClearRegisterSession();
            return Json(new { success = false, message = "Đã xảy ra lỗi khi tạo tài khoản. Vui lòng thử lại sau!" });
        }

        // POST: Account/SendForgotOtp (AJAX Quên mật khẩu - Gửi mã OTP ngẫu nhiên)
        [HttpPost]
        public JsonResult SendForgotOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "Vui lòng nhập Email!" });
            }

            string lowerEmail = email.Trim().ToLower();

            if (!System.Text.RegularExpressions.Regex.IsMatch(lowerEmail, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                return Json(new { success = false, message = "Định dạng Email không hợp lệ (ví dụ: tenban@gmail.com)!" });
            }

            string fullName = "Học viên";

            try
            {
                var user = db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == lowerEmail);
                if (user == null)
                {
                    return Json(new { success = false, message = "Email này chưa được đăng ký trên hệ thống!" });
                }
                else
                {
                    fullName = user.hoTen;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SendForgotOtp DB Check Exception: " + ex.Message);
            }

            Random rand = new Random();
            string otpCode = rand.Next(100000, 999999).ToString();
            DateTime expiryTime = DateTime.Now.AddMinutes(5);

            Session["Forgot_Email"] = lowerEmail;
            Session["Forgot_FullName"] = fullName;
            Session["Forgot_OTP"] = otpCode;
            Session["Forgot_OTP_Expiry"] = expiryTime;
            Session["Forgot_OTP_Attempts"] = 0;
            Session["Forgot_OTP_Verified"] = false;
            Session["Forgot_OTP_Verified_Email"] = null;

            // Gửi email OTP khôi phục mật khẩu thực tế
            var mailResult = EmailService.SendForgotOtpEmail(lowerEmail, fullName, otpCode);

            if (mailResult.Success)
            {
                return Json(new { success = true, message = "Mã xác thực OTP đã được gửi về email " + email + ". Vui lòng kiểm tra hộp thư (kể cả mục Thư rác/Spam)." });
            }
            else
            {
                return Json(new { success = false, message = "Không thể gửi email xác thực OTP: " + mailResult.Message + ". Vui lòng thử lại sau!" });
            }
        }

        // POST: Account/VerifyForgotOtp (AJAX Xác thực mã OTP bước 2 trước khi đặt lại mật khẩu)
        [HttpPost]
        public JsonResult VerifyForgotOtp(string email, string otp)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ Email và mã xác thực OTP!" });
            }

            string lowerEmail = email.Trim().ToLower();
            string sessionEmail = Session["Forgot_Email"] as string;
            string sessionOtp = Session["Forgot_OTP"] as string;
            DateTime? expiryTime = Session["Forgot_OTP_Expiry"] as DateTime?;
            int attempts = (Session["Forgot_OTP_Attempts"] as int?) ?? 0;

            if (string.IsNullOrEmpty(sessionEmail) || sessionEmail != lowerEmail)
            {
                return Json(new { success = false, message = "Email không trùng khớp với yêu cầu khôi phục mật khẩu!" });
            }

            if (attempts >= 5)
            {
                Session.Remove("Forgot_OTP");
                Session.Remove("Forgot_OTP_Expiry");
                return Json(new { success = false, message = "Bạn đã nhập sai mã OTP quá 5 lần. Vui lòng yêu cầu gửi lại mã OTP mới!" });
            }

            if (expiryTime.HasValue && DateTime.Now > expiryTime.Value)
            {
                return Json(new { success = false, message = "Mã OTP đã hết hạn (sau 5 phút). Vui lòng nhấn gửi lại mã mới!" });
            }

            // So khớp chính xác OTP thực tế (Không có backdoor 123456)
            if (otp.Trim() != sessionOtp)
            {
                attempts++;
                Session["Forgot_OTP_Attempts"] = attempts;
                int remaining = 5 - attempts;
                return Json(new { success = false, message = string.Format("Mã xác thực OTP không chính xác! Còn lại {0} lần thử.", remaining) });
            }

            Session["Forgot_OTP_Verified"] = true;
            Session["Forgot_OTP_Verified_Email"] = lowerEmail;
            return Json(new { success = true, message = "Xác thực mã OTP thành công! Vui lòng thiết lập mật khẩu mới." });
        }

        // POST: Account/ResetPassword (AJAX Đặt lại mật khẩu mới ở bước 3 - Server-side OTP lock & Password Hashing)
        [HttpPost]
        public JsonResult ResetPassword(string email, string otp, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword))
            {
                return Json(new { success = false, message = "Thông tin không hợp lệ!" });
            }

            string lowerEmail = email.Trim().ToLower();

            // CHỐNG BYPASS OTP SERVER-SIDE
            bool isVerified = (Session["Forgot_OTP_Verified"] as bool?) ?? false;
            string verifiedEmail = Session["Forgot_OTP_Verified_Email"] as string;

            if (!isVerified || string.IsNullOrEmpty(verifiedEmail) || verifiedEmail != lowerEmail)
            {
                return Json(new { success = false, message = "Phiên xác thực OTP không hợp lệ! Vui lòng xác thực mã OTP trước khi đặt mật khẩu mới." });
            }

            // Ràng buộc bảo mật mật khẩu mới (tối thiểu 8 ký tự, có in hoa, số và ký tự đặc biệt)
            var missingRules = new List<string>();
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
            {
                missingRules.Add("tối thiểu 8 ký tự");
            }
            if (string.IsNullOrEmpty(newPassword) || !newPassword.Any(char.IsUpper))
            {
                missingRules.Add("chữ in hoa (A-Z)");
            }
            if (string.IsNullOrEmpty(newPassword) || !newPassword.Any(char.IsDigit))
            {
                missingRules.Add("chữ số (0-9)");
            }
            if (string.IsNullOrEmpty(newPassword) || !newPassword.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                missingRules.Add("ký tự đặc biệt (!@#$%^&*...)");
            }

            if (missingRules.Any())
            {
                return Json(new { success = false, message = "Mật khẩu mới còn thiếu: " + string.Join(", ", missingRules) + "." });
            }

            // Mã hóa mật khẩu mới trước khi cập nhật CSDL
            string hashedPassword = PasswordHasher.HashPassword(newPassword);

            try
            {
                var user = db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == lowerEmail);
                if (user != null)
                {
                    user.matKhau = hashedPassword;
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ResetPassword Error: " + ex.Message);
            }

            Session.Remove("Forgot_Email");
            Session.Remove("Forgot_FullName");
            Session.Remove("Forgot_OTP");
            Session.Remove("Forgot_OTP_Expiry");
            Session.Remove("Forgot_OTP_Attempts");
            Session.Remove("Forgot_OTP_Verified");
            Session.Remove("Forgot_OTP_Verified_Email");

            return Json(new { success = true, message = "Thiết lập mật khẩu mới thành công!" });
        }

        private void ClearRegisterSession()
        {
            Session.Remove("Register_Email");
            Session.Remove("Register_FullName");
            Session.Remove("Register_OTP");
            Session.Remove("Register_OTP_Expiry");
            Session.Remove("Register_OTP_Attempts");
            Session.Remove("Register_OtpVerified");
            Session.Remove("Register_OtpVerified_Email");
        }

        // Client ID OAuth của web (không phải khóa bí mật), phải trùng client_id trong Views/Account/Login.cshtml
        private const string GoogleClientId = "16112335076-7inckr0r5mo4og4gmi70c5t10j82bg4o.apps.googleusercontent.com";

        // POST: Account/GoogleLogin (Xác thực token với Google rồi Đăng nhập / Đăng ký)
        // Chỉ tin thông tin máy chủ Google trả về, KHÔNG tin email/họ tên do trình duyệt gửi lên
        // (nếu tin, ai cũng có thể gửi email của người khác, kể cả Admin, để đăng nhập không cần mật khẩu).
        [HttpPost]
        public JsonResult GoogleLogin(string idToken, string accessToken)
        {
            GoogleTokenPayload hoSo = null;
            if (!string.IsNullOrWhiteSpace(idToken))
            {
                hoSo = VerifyGoogleIdToken(idToken);
            }
            else if (!string.IsNullOrWhiteSpace(accessToken))
            {
                hoSo = VerifyGoogleAccessToken(accessToken);
            }

            if (hoSo == null)
            {
                return Json(new { success = false, message = "Không xác thực được tài khoản Google. Vui lòng thử lại!" });
            }

            string email = hoSo.Email.Trim().ToLower();

            // 1. Họ và tên đầy đủ từ Google (ưu tiên Họ + Tên nếu có)
            string name = "";
            if (!string.IsNullOrWhiteSpace(hoSo.FamilyName) && !string.IsNullOrWhiteSpace(hoSo.GivenName))
            {
                name = $"{hoSo.FamilyName.Trim()} {hoSo.GivenName.Trim()}";
            }
            else if (!string.IsNullOrWhiteSpace(hoSo.Name))
            {
                name = hoSo.Name.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(hoSo.GivenName))
            {
                name = hoSo.GivenName.Trim();
            }

            string avatar = string.IsNullOrWhiteSpace(hoSo.Picture) ? "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=100&auto=format&fit=crop&q=80" : hoSo.Picture.Trim();

            // Tuyệt đối không lấy tên tài khoản / tiền tố email làm họ tên
            if (string.IsNullOrWhiteSpace(name) || name.Equals(email.Split('@')[0], StringComparison.OrdinalIgnoreCase))
            {
                name = "Học viên Google";
            }

            try
            {
                // 3. Tra cứu tài khoản trong CSDL SQL Server (NguoiDung)
                var user = db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == email);
                if (user == null)
                {
                    // Tạo mới bản ghi Học viên với họ tên đầy đủ từ Google
                    user = new NguoiDung
                    {
                        hoTen = name,
                        email = email,
                        // Mật khẩu ngẫu nhiên, không ai biết: muốn đăng nhập bằng mật khẩu thì dùng "Quên mật khẩu" để đặt
                        matKhau = PasswordHasher.HashPassword(Guid.NewGuid().ToString("N") + "Aa1!"),
                        vaiTro = "HocVien",
                        trangThai = true,
                        anhDaiDien = avatar
                    };
                    db.NguoiDung.Add(user);
                    db.SaveChanges();
                }
                else
                {
                    if (user.trangThai.HasValue && !user.trangThai.Value)
                    {
                        return Json(new { success = false, message = "Tài khoản của bạn đã bị khóa hoặc chưa được kích hoạt." });
                    }

                    bool isUpdated = false;
                    if (!string.IsNullOrEmpty(avatar) && user.anhDaiDien != avatar)
                    {
                        user.anhDaiDien = avatar;
                        isUpdated = true;
                    }

                    // CẬP NHẬT họ và tên đầy đủ từ Google vào csdl
                    if (!string.IsNullOrWhiteSpace(name) && name != "Học viên Google")
                    {
                        // Cập nhật nếu tên trong DB khác tên Google, hoặc DB đang chứa tên tài khoản/email prefix
                        if (user.hoTen != name || user.hoTen.Equals(email.Split('@')[0], StringComparison.OrdinalIgnoreCase) || user.hoTen.Equals("Học viên Google", StringComparison.OrdinalIgnoreCase))
                        {
                            user.hoTen = name;
                            isUpdated = true;
                        }
                    }

                    if (isUpdated)
                    {
                        db.SaveChanges();
                    }
                }

                // 4. Tạo Session và FormsAuthentication Cookie riêng của hệ thống với họ tên đầy đủ
                SetUserSessionAndAuthCookie(user.maNguoiDung, user.hoTen, user.email, user.vaiTro, user.anhDaiDien, false);
                return Json(new { success = true, message = "Đăng nhập xác thực bằng tài khoản Google thành công!", redirectUrl = Url.Action("Index", "Home") });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GoogleLogin Error: " + ex.Message);
            }

            return Json(new { success = false, message = "Đã xảy ra lỗi khi kết nối hệ thống. Vui lòng thử lại sau!" });
        }

        // ID Token (One Tap): Google kiểm tra chữ ký + hạn dùng; ta kiểm tra token cấp cho đúng web này và email đã xác minh
        private GoogleTokenPayload VerifyGoogleIdToken(string idToken)
        {
            var dict = GoiGoogle("https://oauth2.googleapis.com/tokeninfo?id_token=" + Uri.EscapeDataString(idToken), null);
            if (dict == null || GiaTri(dict, "aud") != GoogleClientId) return null;

            string iss = GiaTri(dict, "iss");
            if (iss != "accounts.google.com" && iss != "https://accounts.google.com") return null;

            return TaoHoSoGoogle(dict);
        }

        // Access Token (cửa sổ popup): kiểm tra token cấp cho đúng web này rồi mới lấy thông tin người dùng
        private GoogleTokenPayload VerifyGoogleAccessToken(string accessToken)
        {
            var info = GoiGoogle("https://oauth2.googleapis.com/tokeninfo?access_token=" + Uri.EscapeDataString(accessToken), null);
            if (info == null || GiaTri(info, "aud") != GoogleClientId) return null;

            var dict = GoiGoogle("https://www.googleapis.com/oauth2/v3/userinfo", accessToken);
            if (dict == null || GiaTri(dict, "sub") != GiaTri(info, "sub")) return null;

            return TaoHoSoGoogle(dict);
        }

        private static GoogleTokenPayload TaoHoSoGoogle(Dictionary<string, object> dict)
        {
            string email = GiaTri(dict, "email");
            // email_verified: tokeninfo trả chuỗi "true", userinfo trả kiểu bool
            if (string.IsNullOrWhiteSpace(email) || !string.Equals(GiaTri(dict, "email_verified"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new GoogleTokenPayload
            {
                Email = email,
                Name = GiaTri(dict, "name"),
                GivenName = GiaTri(dict, "given_name"),
                FamilyName = GiaTri(dict, "family_name"),
                Picture = GiaTri(dict, "picture"),
                Sub = GiaTri(dict, "sub")
            };
        }

        // Gọi API Google, token sai/hết hạn (HTTP 400/401) hoặc lỗi mạng thì trả null
        private static Dictionary<string, object> GoiGoogle(string url, string bearerToken)
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
                using (var client = new System.Net.WebClient())
                {
                    client.Encoding = System.Text.Encoding.UTF8;
                    if (bearerToken != null)
                    {
                        client.Headers[System.Net.HttpRequestHeader.Authorization] = "Bearer " + bearerToken;
                    }
                    string json = client.DownloadString(url);
                    return new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Google token validation error: " + ex.Message);
                return null;
            }
        }

        private static string GiaTri(Dictionary<string, object> dict, string key)
        {
            object v;
            return dict.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : null;
        }

        public class GoogleTokenPayload
        {
            public string Email { get; set; }
            public string Name { get; set; }
            public string GivenName { get; set; }
            public string FamilyName { get; set; }
            public string Picture { get; set; }
            public string Sub { get; set; }
        }

        // GET: Account/Logout
        [HttpGet]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();

            return RedirectToAction("Index", "Home");
        }

        // POST: Account/Logout (UC2: Đăng xuất)
        [HttpPost]
        [ActionName("Logout")]
        [ValidateAntiForgeryToken]
        public ActionResult LogoutPost()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();

            return RedirectToAction("Index", "Home");
        }

        #region Helper Methods
        private void SetUserSessionAndAuthCookie(int userId, string fullName, string email, string role, string avatar, bool rememberMe)
        {
            FormsAuthentication.SetAuthCookie(email, rememberMe);

            Session["UserId"] = userId;
            Session["FullName"] = fullName;
            Session["Email"] = email;
            Session["Role"] = string.IsNullOrEmpty(role) ? "HocVien" : role;
            Session["Avatar"] = string.IsNullOrEmpty(avatar) ? "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=100&auto=format&fit=crop&q=80" : avatar;
            Session["User"] = db.NguoiDung.Find(userId);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && db != null)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        #endregion
    }
}
