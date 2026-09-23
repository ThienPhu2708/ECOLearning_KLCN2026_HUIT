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

                // Thống kê tiến độ học tập từ CSDL
                int enrolledCount = 0;
                int completedCount = 0;
                try
                {
                    enrolledCount = db.DangKyKhoaHoc.Count(d => d.maHocVien == user.maNguoiDung);
                    completedCount = db.TienDoHocTap.Count(t => t.maHocVien == user.maNguoiDung && t.phanTramHoanThanh >= 100);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Profile Stats Error: " + ex.Message);
                    enrolledCount = 0;
                    completedCount = 0;
                }

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
                               (string.Equals(user.vaiTro, "GiaoVien", StringComparison.OrdinalIgnoreCase) ? "Giảng viên" : "Học viên"),
                    EnrolledCoursesCount = enrolledCount,
                    CompletedCoursesCount = completedCount,
                    TotalCertificates = completedCount
                };

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

                user.matKhau = PasswordHasher.HashPassword(newPassword);
                db.SaveChanges();

                return Json(new { success = true, message = "Đổi mật khẩu tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChangePassword Error: " + ex.Message);
                return Json(new { success = false, message = "Không thể đổi mật khẩu. Vui lòng thử lại sau!" });
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
