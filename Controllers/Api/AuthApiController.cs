using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.Services.Api;

namespace ECOLearning.Controllers.Api
{
    /// <summary>Nhóm A – Tài khoản: đăng nhập, đăng ký OTP, quên mật khẩu, đăng xuất (Task_Tuan10.docx mục 6.1).</summary>
    [RoutePrefix("api/v1")]
    public class AuthApiController : ApiControllerBase
    {
        public class LoginRequest
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }

        public class OtpRequest
        {
            public string FullName { get; set; }
            public string Email { get; set; }
            public string Otp { get; set; }
            public string Password { get; set; }
            public string NewPassword { get; set; }
        }

        private static readonly string[] VaiTroDuocDung = { "HocVien", "GiaoVien" };

        /// <summary>{ token, expiresAt, user } sau khi đăng nhập / đăng ký thành công</summary>
        private ActionResult PhienDangNhap(NguoiDung user, string message = null)
        {
            DateTime hetHan;
            string token = new ApiTokenService(Db).TaoToken(user.maNguoiDung, Request.UserAgent, out hetHan);
            return Ok(new { token, expiresAt = ApiFormat.Date(hetHan), user = UserJson(user) }, message);
        }

        // A1. POST /api/v1/auth/login
        [HttpPost, Route("auth/login")]
        public ActionResult Login()
        {
            var req = Body<LoginRequest>();
            var errors = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(req.Email)) errors["email"] = "Vui lòng nhập Email!";
            if (string.IsNullOrEmpty(req.Password)) errors["password"] = "Vui lòng nhập Mật khẩu!";
            if (errors.Count > 0) return BadRequest(errors.Values.First(), "INVALID_DATA", errors);

            string email = ApiRules.ChuanHoaEmail(req.Email);
            var user = Db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == email);
            bool dung = false;
            try
            {
                dung = user != null && !string.IsNullOrEmpty(user.matKhau) && PasswordHasher.VerifyPassword(user.matKhau, req.Password);
            }
            catch
            {
                dung = false;
            }
            if (!dung)
            {
                // Dự phòng giống website: tài khoản cũ xác thực qua stored procedure sp_DangNhap
                try
                {
                    var sp = Db.sp_DangNhap(req.Email.Trim(), req.Password).FirstOrDefault();
                    if (sp != null && sp.maNguoiDung > 0)
                    {
                        user = Db.NguoiDung.Find(sp.maNguoiDung);
                        dung = user != null;
                    }
                }
                catch
                {
                    // bỏ qua: coi như sai mật khẩu
                }
            }
            // Không nói rõ sai email hay sai mật khẩu (chống dò tài khoản)
            if (!dung) return Fail(401, "WRONG_CREDENTIALS", "Tài khoản hoặc mật khẩu không chính xác.");
            if (user.trangThai == false)
            {
                // Hết thời hạn khóa thì tự mở; còn khóa thì báo lý do và thời hạn
                AccountAdminService.MoKhoaHetHanKhiDangNhap(Db, user.maNguoiDung);
                Db.Entry(user).Reload();
                if (user.trangThai == false) return Fail(403, "ACCOUNT_LOCKED", AccountAdminService.CauBaoKhoa(Db, user.maNguoiDung));
            }
            if (!VaiTroDuocDung.Contains(user.vaiTro))
                return Fail(403, "ROLE_NOT_ALLOWED", "Ứng dụng dành cho học viên. Tài khoản quản trị vui lòng đăng nhập trên website.");

            return PhienDangNhap(user, "Đăng nhập thành công!");
        }

        // A2. POST /api/v1/auth/register/send-otp
        [HttpPost, Route("auth/register/send-otp")]
        public ActionResult RegisterSendOtp()
        {
            var req = Body<OtpRequest>();
            var errors = new Dictionary<string, string>();
            string loiTen = ApiRules.KiemTraHoTen(req.FullName);
            if (loiTen != null) errors["fullName"] = loiTen;
            string loiEmail = ApiRules.KiemTraEmail(req.Email);
            if (loiEmail != null) errors["email"] = loiEmail;
            if (errors.Count > 0) return BadRequest(errors.Values.First(), "INVALID_DATA", errors);

            string email = ApiRules.ChuanHoaEmail(req.Email);
            if (Db.NguoiDung.Any(u => u.email.ToLower() == email))
                return Fail(409, "EMAIL_EXISTS", "Email này đã được đăng ký trên hệ thống!", new Dictionary<string, string> { { "email", "Email này đã được đăng ký trên hệ thống!" } });

            return GuiOtp(email, ApiOtpService.DangKy, req.FullName.Trim(),
                (ma) => EmailService.SendOtpEmail(email, req.FullName.Trim(), ma, "Mã xác thực OTP đăng ký tài khoản - E-Learning"));
        }

        // A3. POST /api/v1/auth/register/verify-otp
        [HttpPost, Route("auth/register/verify-otp")]
        public ActionResult RegisterVerifyOtp()
        {
            var req = Body<OtpRequest>();
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Otp))
                return BadRequest("Vui lòng nhập đầy đủ Email và mã OTP 6 số!");
            var loi = new ApiOtpService(Db).XacThuc(ApiRules.ChuanHoaEmail(req.Email), ApiOtpService.DangKy, req.Otp);
            if (loi != null) return BadRequest(loi.Item2, loi.Item1, new Dictionary<string, string> { { "otp", loi.Item2 } });
            return Ok(new { valid = true }, "Xác thực mã OTP thành công! Vui lòng thiết lập mật khẩu.");
        }

        // A4. POST /api/v1/auth/register
        [HttpPost, Route("auth/register")]
        public ActionResult Register()
        {
            var req = Body<OtpRequest>();
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Otp))
                return BadRequest("Thông tin đăng ký không đầy đủ!");
            string loiMatKhau = ApiRules.KiemTraMatKhau(req.Password);
            if (loiMatKhau != null)
                return BadRequest(loiMatKhau, "WEAK_PASSWORD", new Dictionary<string, string> { { "password", loiMatKhau } });
            if (!string.IsNullOrWhiteSpace(req.FullName))
            {
                string loiTen = ApiRules.KiemTraHoTen(req.FullName);
                if (loiTen != null) return BadRequest(loiTen, "INVALID_DATA", new Dictionary<string, string> { { "fullName", loiTen } });
            }

            string email = ApiRules.ChuanHoaEmail(req.Email);
            if (Db.NguoiDung.Any(u => u.email.ToLower() == email))
                return Fail(409, "EMAIL_EXISTS", "Email này đã được đăng ký trên hệ thống!");

            string hoTenOtp;
            Tuple<string, string> loi;
            if (!new ApiOtpService(Db).DungMa(email, ApiOtpService.DangKy, req.Otp, out hoTenOtp, out loi))
                return BadRequest(loi.Item2, loi.Item1, new Dictionary<string, string> { { "otp", loi.Item2 } });

            string hoTen = !string.IsNullOrWhiteSpace(req.FullName) ? req.FullName.Trim()
                : !string.IsNullOrWhiteSpace(hoTenOtp) ? hoTenOtp : email.Split('@')[0];
            string hash = PasswordHasher.HashPassword(req.Password);

            // Giống website: tạo qua stored procedure sp_DangKyTaiKhoan, lỗi thì thêm trực tiếp
            int maMoi = 0;
            try
            {
                var sp = Db.sp_DangKyTaiKhoan(hoTen, email, hash, "HocVien", null, null, null).FirstOrDefault();
                if (sp != null && sp.maNguoiDungMoi.HasValue) maMoi = (int)sp.maNguoiDungMoi.Value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("API sp_DangKyTaiKhoan: " + ex.Message);
            }
            NguoiDung user = maMoi > 0 ? Db.NguoiDung.Find(maMoi) : null;
            if (user == null)
            {
                user = new NguoiDung
                {
                    hoTen = hoTen,
                    email = email,
                    matKhau = hash,
                    vaiTro = "HocVien",
                    trangThai = true,
                    anhDaiDien = ApiCourseMapper.AnhDaiDienMacDinh
                };
                Db.NguoiDung.Add(user);
                Db.SaveChanges();
            }
            return PhienDangNhap(user, "Đăng ký tài khoản thành công!");
        }

        // A5. POST /api/v1/auth/forgot/send-otp
        [HttpPost, Route("auth/forgot/send-otp")]
        public ActionResult ForgotSendOtp()
        {
            var req = Body<OtpRequest>();
            string loiEmail = ApiRules.KiemTraEmail(req.Email);
            if (loiEmail != null) return BadRequest(loiEmail, "INVALID_DATA", new Dictionary<string, string> { { "email", loiEmail } });

            string email = ApiRules.ChuanHoaEmail(req.Email);
            var user = Db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == email);
            if (user == null) return Fail(404, "EMAIL_NOT_FOUND", "Email này chưa được đăng ký trên hệ thống!");

            return GuiOtp(email, ApiOtpService.QuenMatKhau, user.hoTen,
                (ma) => EmailService.SendForgotOtpEmail(email, user.hoTen, ma));
        }

        // A6. POST /api/v1/auth/forgot/reset
        [HttpPost, Route("auth/forgot/reset")]
        public ActionResult ForgotReset()
        {
            var req = Body<OtpRequest>();
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Otp))
                return BadRequest("Vui lòng nhập đầy đủ Email và mã xác thực OTP!");
            string loiMatKhau = ApiRules.KiemTraMatKhau(req.NewPassword, "Mật khẩu mới");
            if (loiMatKhau != null)
                return BadRequest(loiMatKhau, "WEAK_PASSWORD", new Dictionary<string, string> { { "newPassword", loiMatKhau } });

            string email = ApiRules.ChuanHoaEmail(req.Email);
            var user = Db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == email);
            if (user == null) return Fail(404, "EMAIL_NOT_FOUND", "Email này chưa được đăng ký trên hệ thống!");

            string boQua;
            Tuple<string, string> loi;
            if (!new ApiOtpService(Db).DungMa(email, ApiOtpService.QuenMatKhau, req.Otp, out boQua, out loi))
                return BadRequest(loi.Item2, loi.Item1, new Dictionary<string, string> { { "otp", loi.Item2 } });

            user.matKhau = PasswordHasher.HashPassword(req.NewPassword);
            Db.SaveChanges();
            AccountAdminService.DaDoiMatKhau(Db, user.maNguoiDung);
            // Đặt lại mật khẩu: đăng xuất mọi thiết bị đang dùng token cũ
            new ApiTokenService(Db).ThuHoiTatCa(user.maNguoiDung);
            return Ok(null, "Thiết lập mật khẩu mới thành công! Vui lòng đăng nhập lại.");
        }

        // A7. POST /api/v1/auth/logout
        [HttpPost, Route("auth/logout"), ApiAuthorize]
        public ActionResult Logout()
        {
            new ApiTokenService(Db).ThuHoi(CurrentToken);
            return Ok(null, "Đã đăng xuất.");
        }

        public class GoogleRequest
        {
            public string IdToken { get; set; }
        }

        // A12. POST /api/v1/auth/google
        // App lấy ID token bằng google_sign_in (serverClientId = Client ID của web) nên dùng chung cách xác thực với website
        [HttpPost, Route("auth/google")]
        public ActionResult Google()
        {
            var req = Body<GoogleRequest>();
            if (string.IsNullOrWhiteSpace(req.IdToken)) return BadRequest("Thiếu thông tin xác thực Google!");

            var hoSo = GoogleAuthService.VerifyIdToken(req.IdToken);
            if (hoSo == null) return Fail(401, "GOOGLE_INVALID_TOKEN", "Không xác thực được tài khoản Google. Vui lòng thử lại!");

            var user = GoogleAuthService.TimHoacTaoNguoiDung(Db, hoSo);
            if (user.trangThai == false)
            {
                AccountAdminService.MoKhoaHetHanKhiDangNhap(Db, user.maNguoiDung);
                Db.Entry(user).Reload();
                if (user.trangThai == false) return Fail(403, "ACCOUNT_LOCKED", AccountAdminService.CauBaoKhoa(Db, user.maNguoiDung));
            }
            if (!VaiTroDuocDung.Contains(user.vaiTro))
                return Fail(403, "ROLE_NOT_ALLOWED", "Ứng dụng dành cho học viên. Tài khoản quản trị vui lòng đăng nhập trên website.");

            return PhienDangNhap(user, "Đăng nhập bằng Google thành công!");
        }

        private ActionResult GuiOtp(string email, string mucDich, string hoTen, Func<string, (bool Success, string Message)> gui)
        {
            var otp = new ApiOtpService(Db);
            int cho = otp.GiayPhaiCho(email, mucDich);
            if (cho > 0)
                return Fail(429, "TOO_MANY_REQUESTS", string.Format("Vui lòng chờ {0} giây trước khi gửi lại mã OTP.", cho));

            string ma = otp.TaoMa(email, mucDich, hoTen);
            var kq = gui(ma);
            if (!kq.Success)
                return Fail(502, "EMAIL_FAILED", "Không thể gửi email OTP: " + kq.Message + ". Vui lòng thử lại sau!");
            return Ok(new { expiresInSeconds = ApiOtpService.PhutHetHan * 60 },
                "Mã xác thực OTP đã được gửi đến email " + email + ". Vui lòng kiểm tra hộp thư (kể cả mục Thư rác/Spam).");
        }
    }
}
