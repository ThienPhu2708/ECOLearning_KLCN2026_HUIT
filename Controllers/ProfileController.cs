using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    public class ProfileController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Profile (UC7: Quản lý hồ sơ cá nhân Học viên)
        [HttpGet]
        public ActionResult Index()
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Profile") });
            }

            try
            {
                var user = db.NguoiDung.Find(userId.Value);
                if (user == null)
                {
                    string userEmail = Session["Email"]?.ToString();
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        user = db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == userEmail.ToLower());
                    }
                }

                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Refresh the web session after profile changes made through the mobile API.
                Session["Avatar"] = string.IsNullOrWhiteSpace(user.anhDaiDien)
                    ? SessionNguoiDung.AnhDaiDienMacDinh : user.anhDaiDien;
                Session["User"] = SessionNguoiDung.BanSao(user);

                // Thống kê tiến độ học tập từ CSDL (dựa trên TienDoBaiHoc — bảng theo dõi tiến độ thật của hệ thống,
                // không dùng TienDoHocTap vì bảng này không được bất kỳ luồng nghiệp vụ nào ghi dữ liệu)
                int enrolledCount = 0;
                int completedCount = 0;
                try
                {
                    var enrollments = db.DangKyKhoaHoc.Where(d => d.maHocVien == user.maNguoiDung).ToList();
                    enrolledCount = enrollments.Count;

                    foreach (var dk in enrollments)
                    {
                        var lessonIds = db.BaiHoc
                            .Where(b => (b.ChuongHoc != null && b.ChuongHoc.maKhoaHoc == dk.maKhoaHoc)
                                     || (b.maChuong == null && b.maBaiHoc.StartsWith(dk.maKhoaHoc + "_")))
                            .Select(b => b.maBaiHoc)
                            .ToList();

                        if (lessonIds.Count == 0) continue;

                        int doneCount = db.TienDoBaiHoc
                            .Count(t => t.maHocVien == user.maNguoiDung && lessonIds.Contains(t.maBaiHoc) && t.daHoanThanh == true);

                        if (doneCount >= lessonIds.Count)
                        {
                            completedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Profile Stats Error: " + ex.Message);
                    enrolledCount = 0;
                    completedCount = 0;
                }

                bool isTeacher = string.Equals(user.vaiTro, "GiaoVien", StringComparison.OrdinalIgnoreCase);

                var model = new StudentProfileViewModel
                {
                    UserId = user.maNguoiDung,
                    FullName = user.hoTen,
                    Email = user.email,
                    Phone = user.soDienThoai,
                    Gender = string.IsNullOrEmpty(user.gioiTinh) ? "Nam" : user.gioiTinh,
                    BirthDate = user.ngaySinh,
                    AvatarUrl = string.IsNullOrEmpty(user.anhDaiDien) ? "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=100&auto=format&fit=crop&q=80" : user.anhDaiDien,
                    RoleName = string.Equals(user.vaiTro, "Admin", StringComparison.OrdinalIgnoreCase) ? "Quản trị viên" :
                               string.Equals(user.vaiTro, "Marketing", StringComparison.OrdinalIgnoreCase) ? "Nhân viên Marketing" :
                               (isTeacher ? "Giảng viên" : "Học viên"),
                    IsTeacher = isTeacher,
                    EnrolledCoursesCount = enrolledCount,
                    CompletedCoursesCount = completedCount,
                    TotalCertificates = completedCount
                };

                if (isTeacher)
                {
                    var teacherApp = TeacherApplicationService.GetByUserId(user.maNguoiDung);
                    if (teacherApp != null)
                    {
                        model.ApplicationId = teacherApp.ApplicationId;
                        model.Specialization = teacherApp.Specialization;
                        model.BioExperience = teacherApp.BioExperience;
                        model.BankName = teacherApp.BankName;
                        model.AccountNumber = teacherApp.AccountNumber;
                        model.AccountName = teacherApp.AccountName;
                        model.CertificateFilePath = teacherApp.CertificateFilePath;
                        model.CertificateFileName = teacherApp.CertificateFileName;
                        model.TeacherStatus = teacherApp.Status;
                    }
                }

                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Profile Index Error: " + ex.Message);
                return RedirectToAction("Login", "Account");
            }
        }

        // POST: Profile/UpdateInfo (Cập nhật thông tin cá nhân)
        [HttpPost]
        public JsonResult UpdateInfo(string fullName, string phone, string gender, DateTime? birthDate)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Json(new { success = false, message = "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại!" });
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return Json(new { success = false, message = "Họ và tên không được để trống!" });
            }

            try
            {
                var user = db.NguoiDung.Find(userId.Value);
                if (user == null)
                {
                    string userEmail = Session["Email"]?.ToString();
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        user = db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == userEmail.ToLower());
                    }
                }

                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng trong hệ thống!" });
                }

                user.hoTen = fullName.Trim();
                user.soDienThoai = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
                user.gioiTinh = gender;
                user.ngaySinh = birthDate;

                db.SaveChanges();

                // Cập nhật lại Session hiển thị toàn bộ hệ thống
                Session["FullName"] = fullName.Trim();

                return Json(new { success = true, message = "Cập nhật thông tin cá nhân thành công!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateInfo Error: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi xảy ra khi lưu thông tin: " + ex.Message });
            }
        }

        // POST: Profile/UploadAvatar (Tải ảnh đại diện mới)
        [HttpPost]
        public JsonResult UploadAvatar(HttpPostedFileBase avatarFile)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Json(new { success = false, message = "Phiên làm việc đã hết hạn!" });
            }

            if (avatarFile == null || avatarFile.ContentLength == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn tệp hình ảnh!" });
            }

            try
            {
                string extension = Path.GetExtension(avatarFile.FileName).ToLower();
                string[] validExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                if (!validExtensions.Contains(extension))
                {
                    return Json(new { success = false, message = "Định dạng tệp không hợp lệ! Vui lòng chọn ảnh JPG, PNG hoặc WEBP." });
                }

                string uploadDir = Server.MapPath("~/assets/images/avatars/");
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                string fileName = "avatar_" + userId.Value + "_" + DateTime.Now.Ticks + extension;
                string fullPath = Path.Combine(uploadDir, fileName);

                avatarFile.SaveAs(fullPath);

                string relativeUrl = Url.Content("~/assets/images/avatars/" + fileName);

                // Cập nhật CSDL
                var user = db.NguoiDung.Find(userId.Value);
                if (user == null)
                {
                    string userEmail = Session["Email"]?.ToString();
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        user = db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == userEmail.ToLower());
                    }
                }

                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy tài khoản người dùng!" });
                }

                user.anhDaiDien = relativeUrl;
                db.SaveChanges();

                // Cập nhật Session
                Session["Avatar"] = relativeUrl;

                return Json(new { success = true, message = "Tải ảnh đại diện mới thành công!", avatarUrl = relativeUrl });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UploadAvatar Error: " + ex.Message);
                return Json(new { success = false, message = "Không thể tải ảnh lên: " + ex.Message });
            }
        }

        // POST: Profile/ChangePassword (Đổi mật khẩu tài khoản)
        [HttpPost]
        public JsonResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Json(new { success = false, message = "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại!" });
            }

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                return Json(new { success = false, message = "Vui lòng nhập Mật khẩu hiện tại!" });
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                return Json(new { success = false, message = "Mật khẩu mới phải có tối thiểu 8 ký tự!" });
            }

            if (newPassword != confirmPassword)
            {
                return Json(new { success = false, message = "Mật khẩu xác nhận không khớp với mật khẩu mới!" });
            }

            try
            {
                var user = db.NguoiDung.Find(userId.Value);
                if (user == null)
                {
                    string userEmail = Session["Email"]?.ToString();
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        user = db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == userEmail.ToLower());
                    }
                }

                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng!" });
                }

                if (!PasswordHasher.VerifyPassword(user.matKhau, currentPassword))
                {
                    return Json(new { success = false, message = "Mật khẩu hiện tại không chính xác!" });
                }

                if (PasswordHasher.VerifyPassword(user.matKhau, newPassword))
                {
                    return Json(new { success = false, message = "Mật khẩu mới phải khác mật khẩu hiện tại!" });
                }

                user.matKhau = PasswordHasher.HashPassword(newPassword);
                db.SaveChanges();

                // Đổi xong mật khẩu tạm do Admin cấp: mở lại toàn bộ trang (AccountStatusFilter)
                bool laMatKhauTam = Session[AccountStatusFilter.KeyCanDoiMatKhau] as bool? == true;
                AccountAdminService.DaDoiMatKhau(db, user.maNguoiDung);
                Session.Remove(AccountStatusFilter.KeyCanDoiMatKhau);

                return Json(new { success = true, message = "Đổi mật khẩu tài khoản thành công!", redirectUrl = laMatKhauTam ? TrangChuTheoVaiTro() : null });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChangePassword Error: " + ex.Message);
                return Json(new { success = false, message = "Không thể đổi mật khẩu. Vui lòng thử lại sau!" });
            }
        }

        // POST: Profile/UpdateTeacherProfile (Cập nhật hồ sơ giáo viên và đồng bộ CSDL)
        [HttpPost]
        public JsonResult UpdateTeacherProfile(string specialization, string bioExperience, string bankCode, string accountNumber, string accountName, HttpPostedFileBase certificateFile)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Json(new { success = false, message = "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại!" });
            }

            try
            {
                var user = db.NguoiDung.Find(userId.Value);
                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng!" });
                }

                if (!string.Equals(user.vaiTro, "GiaoVien", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = "Chức năng chỉ dành cho tài khoản Giáo viên/Giảng viên!" });
                }

                // Thông tin nhận thù lao: ngân hàng chỉ chọn trong danh mục (không gõ tự do), số tài khoản chỉ gồm chữ số,
                // tên chủ tài khoản IN HOA KHÔNG DẤU — kiểm tra / chuẩn hóa ở server như trang đăng ký giáo viên
                var nganHang = BankDirectory.Find(bankCode);
                if (nganHang == null)
                {
                    return Json(new { success = false, message = "Vui lòng chọn ngân hàng thụ hưởng trong danh sách!" });
                }
                string soTaiKhoan = BankDirectory.ChuanHoaSoTaiKhoan(accountNumber);
                if (!BankDirectory.SoTaiKhoanHopLe(soTaiKhoan))
                {
                    return Json(new { success = false, message = "Số tài khoản chỉ gồm chữ số, dài từ 6 đến 19 số!" });
                }
                string tenChuTaiKhoan = BankDirectory.ChuanHoaTenChuTaiKhoan(accountName);
                if (tenChuTaiKhoan.Length < 2 || tenChuTaiKhoan.Length > 100)
                {
                    return Json(new { success = false, message = "Vui lòng nhập tên chủ tài khoản (in hoa, không dấu)!" });
                }

                var app = TeacherApplicationService.GetByUserId(user.maNguoiDung);
                if (app == null)
                {
                    app = new TeacherApplication
                    {
                        ApplicationId = "GVAPP_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + user.maNguoiDung,
                        UserId = user.maNguoiDung,
                        FullName = user.hoTen,
                        Email = user.email,
                        Phone = user.soDienThoai,
                        CreatedAt = DateTime.Now,
                        ReviewedAt = DateTime.Now,
                        Status = "Approved"
                    };
                }

                app.Specialization = specialization?.Trim();
                app.BioExperience = bioExperience?.Trim();
                app.BankName = nganHang.ShortName;
                app.AccountNumber = soTaiKhoan;
                app.AccountName = tenChuTaiKhoan;

                // Xử lý tệp chứng chỉ mới tải lên
                if (certificateFile != null && certificateFile.ContentLength > 0)
                {
                    string ext = Path.GetExtension(certificateFile.FileName).ToLower();
                    string[] allowedExts = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                    if (!allowedExts.Contains(ext))
                    {
                        return Json(new { success = false, message = "Định dạng tệp không hỗ trợ! Vui lòng chọn tệp PDF, JPG, PNG hoặc DOCX." });
                    }

                    if (certificateFile.ContentLength > 10 * 1024 * 1024)
                    {
                        return Json(new { success = false, message = "Dung lượng tệp chứng chỉ không được vượt quá 10MB!" });
                    }

                    string uploadsDir = Server.MapPath("~/assets/uploads/certificates");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }

                    string cleanFileName = Path.GetFileName(certificateFile.FileName);
                    string uniqueFileName = $"cert_{user.maNguoiDung}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    string savedPath = Path.Combine(uploadsDir, uniqueFileName);
                    certificateFile.SaveAs(savedPath);

                    app.CertificateFilePath = $"/assets/uploads/certificates/{uniqueFileName}";
                    app.CertificateFileName = cleanFileName;
                }

                // Đồng bộ và lưu xuống CSDL DangKyGiaoVien
                TeacherApplicationService.Save(app);

                return Json(new
                {
                    success = true,
                    message = "Cập nhật hồ sơ giáo viên thành công!",
                    certificateUrl = app.CertificateFilePath,
                    certificateName = app.CertificateFileName
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateTeacherProfile Error: " + ex.Message);
                return Json(new { success = false, message = "Lỗi khi cập nhật hồ sơ giáo viên: " + ex.Message });
            }
        }

        #region Helper Methods
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

        /// <summary>Trang làm việc chính theo vai trò, dùng sau khi đổi xong mật khẩu tạm.</summary>
        private string TrangChuTheoVaiTro()
        {
            string role = Session["Role"]?.ToString();
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return Url.Action("Index", "Dashboard", new { area = "Admin" });
            if (string.Equals(role, "Marketing", StringComparison.OrdinalIgnoreCase)) return Url.Action("Index", "Dashboard", new { area = "Marketing" });
            return Url.Action("Index", "Home", new { area = "" });
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
