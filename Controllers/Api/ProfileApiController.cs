using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Services;
using ECOLearning.Services.Api;

namespace ECOLearning.Controllers.Api
{
    /// <summary>Nhóm A – Hồ sơ: xem / sửa thông tin, đổi ảnh đại diện, đổi mật khẩu (A8 – A11).</summary>
    [RoutePrefix("api/v1"), ApiAuthorize]
    public class ProfileApiController : ApiControllerBase
    {
        public class UpdateRequest
        {
            public string FullName { get; set; }
            public string Phone { get; set; }
            public string Gender { get; set; }
            public string BirthDate { get; set; }
        }

        public class PasswordRequest
        {
            public string CurrentPassword { get; set; }
            public string NewPassword { get; set; }
        }

        private static readonly string[] GioiTinhHopLe = { "Nam", "Nữ", "Khác" };
        private static readonly string[] DuoiAnh = { ".jpg", ".jpeg", ".png", ".webp" };
        private const int DungLuongAnhToiDaMB = 5;

        // A8. GET /api/v1/me
        [HttpGet, Route("me")]
        public ActionResult Me() => Ok(UserJson(CurrentUser));

        // A9. PUT /api/v1/me
        [HttpPut, Route("me")]
        public ActionResult Update()
        {
            var req = Body<UpdateRequest>();
            var errors = new Dictionary<string, string>();
            string loiTen = ApiRules.KiemTraHoTen(req.FullName);
            if (loiTen != null) errors["fullName"] = loiTen;
            string loiSdt = ApiRules.KiemTraSoDienThoai(req.Phone);
            if (loiSdt != null) errors["phone"] = loiSdt;
            if (!string.IsNullOrWhiteSpace(req.Gender) && !GioiTinhHopLe.Contains(req.Gender.Trim()))
                errors["gender"] = "Giới tính chỉ nhận Nam, Nữ hoặc Khác!";
            DateTime? ngaySinh = null;
            if (!string.IsNullOrWhiteSpace(req.BirthDate))
            {
                DateTime d;
                if (!DateTime.TryParseExact(req.BirthDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                    errors["birthDate"] = "Ngày sinh không hợp lệ (định dạng yyyy-MM-dd)!";
                else if (d > DateTime.Today || d.Year < 1900)
                    errors["birthDate"] = "Ngày sinh không hợp lệ!";
                else ngaySinh = d;
            }
            if (errors.Count > 0) return BadRequest(errors.Values.First(), "INVALID_DATA", errors);

            var user = CurrentUser;
            user.hoTen = req.FullName.Trim();
            user.soDienThoai = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
            user.gioiTinh = string.IsNullOrWhiteSpace(req.Gender) ? null : req.Gender.Trim();
            user.ngaySinh = ngaySinh;
            Db.SaveChanges();
            return Ok(UserJson(user), "Cập nhật thông tin cá nhân thành công!");
        }

        // A10. POST /api/v1/me/avatar (multipart/form-data, trường "file")
        [HttpPost, Route("me/avatar")]
        public ActionResult Avatar()
        {
            var file = Request.Files.Count > 0 ? (Request.Files["file"] ?? Request.Files[0]) : null;
            if (file == null || file.ContentLength == 0)
                return BadRequest("Vui lòng chọn tệp hình ảnh!", "INVALID_FILE");
            if (file.ContentLength > DungLuongAnhToiDaMB * 1024 * 1024)
                return BadRequest("Ảnh tối đa " + DungLuongAnhToiDaMB + " MB!", "INVALID_FILE");

            string duoi = (Path.GetExtension(file.FileName) ?? "").ToLowerInvariant();
            byte[] dau = new byte[12];
            int doc = file.InputStream.Read(dau, 0, dau.Length);
            file.InputStream.Position = 0;
            // Kiểm tra nội dung thật của tệp (không chỉ đuôi tên): JPEG FFD8, PNG 89 50 4E 47, WEBP "RIFF....WEBP"
            bool laAnh = doc >= 12 && ((dau[0] == 0xFF && dau[1] == 0xD8)
                || (dau[0] == 0x89 && dau[1] == 0x50 && dau[2] == 0x4E && dau[3] == 0x47)
                || (dau[0] == 'R' && dau[1] == 'I' && dau[2] == 'F' && dau[3] == 'F' && dau[8] == 'W' && dau[9] == 'E' && dau[10] == 'B' && dau[11] == 'P'));
            if (!DuoiAnh.Contains(duoi) || !laAnh)
                return BadRequest("Định dạng tệp không hợp lệ! Vui lòng chọn ảnh JPG, PNG hoặc WEBP.", "INVALID_FILE");

            string thuMuc = Server.MapPath("~/assets/images/avatars/");
            Directory.CreateDirectory(thuMuc);
            string ten = "avatar_" + CurrentUserId + "_" + DateTime.Now.Ticks + duoi;
            file.SaveAs(Path.Combine(thuMuc, ten));

            var user = CurrentUser;
            user.anhDaiDien = "/assets/images/avatars/" + ten;
            Db.SaveChanges();
            return Ok(new { avatarUrl = AbsoluteUrl(user.anhDaiDien) }, "Tải ảnh đại diện mới thành công!");
        }

        // A11. POST /api/v1/me/change-password
        [HttpPost, Route("me/change-password")]
        public ActionResult ChangePassword()
        {
            var req = Body<PasswordRequest>();
            if (string.IsNullOrEmpty(req.CurrentPassword))
                return BadRequest("Vui lòng nhập Mật khẩu hiện tại!", "INVALID_DATA", new Dictionary<string, string> { { "currentPassword", "Vui lòng nhập Mật khẩu hiện tại!" } });
            string loi = ApiRules.KiemTraMatKhau(req.NewPassword, "Mật khẩu mới");
            if (loi != null) return BadRequest(loi, "WEAK_PASSWORD", new Dictionary<string, string> { { "newPassword", loi } });

            var user = CurrentUser;
            bool dung;
            try
            {
                dung = !string.IsNullOrEmpty(user.matKhau) && PasswordHasher.VerifyPassword(user.matKhau, req.CurrentPassword);
            }
            catch
            {
                dung = false;
            }
            if (!dung)
                return BadRequest("Mật khẩu hiện tại không chính xác!", "WRONG_PASSWORD", new Dictionary<string, string> { { "currentPassword", "Mật khẩu hiện tại không chính xác!" } });

            user.matKhau = PasswordHasher.HashPassword(req.NewPassword);
            Db.SaveChanges();
            // Đăng xuất các thiết bị khác, giữ thiết bị đang dùng
            new ApiTokenService(Db).ThuHoiTatCa(CurrentUserId, CurrentToken);
            return Ok(null, "Đổi mật khẩu tài khoản thành công!");
        }
    }
}
