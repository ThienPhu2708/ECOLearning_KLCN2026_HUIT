using System;
using System.Collections.Generic;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.Services.Api;

namespace ECOLearning.Services
{
    /// <summary>
    /// Đăng nhập Google dùng chung cho website (Account/GoogleLogin) và app (A12 POST /api/v1/auth/google).
    /// Chỉ tin thông tin máy chủ Google trả về, KHÔNG tin email/họ tên do client gửi lên
    /// (nếu tin, ai cũng có thể gửi email của người khác, kể cả Admin, để đăng nhập không cần mật khẩu).
    /// </summary>
    public static class GoogleAuthService
    {
        // Client ID OAuth của web (không phải khóa bí mật), phải trùng client_id trong Views/Account/Login.cshtml.
        // App Flutter dùng chính ID này làm serverClientId nên ID token của app cũng có aud = ClientId.
        public const string ClientId = "16112335076-7inckr0r5mo4og4gmi70c5t10j82bg4o.apps.googleusercontent.com";

        private const string TenMacDinh = "Học viên Google";

        public class GoogleTokenPayload
        {
            public string Email { get; set; }
            public string Name { get; set; }
            public string GivenName { get; set; }
            public string FamilyName { get; set; }
            public string Picture { get; set; }
            public string Sub { get; set; }
        }

        // ID Token (One Tap trên web / google_sign_in trên app): Google kiểm tra chữ ký + hạn dùng;
        // ta kiểm tra token cấp cho đúng client này và email đã xác minh
        public static GoogleTokenPayload VerifyIdToken(string idToken)
        {
            var dict = GoiGoogle("https://oauth2.googleapis.com/tokeninfo?id_token=" + Uri.EscapeDataString(idToken), null);
            if (dict == null || GiaTri(dict, "aud") != ClientId) return null;

            string iss = GiaTri(dict, "iss");
            if (iss != "accounts.google.com" && iss != "https://accounts.google.com") return null;

            return TaoHoSoGoogle(dict);
        }

        // Access Token (cửa sổ popup trên web): kiểm tra token cấp cho đúng web này rồi mới lấy thông tin người dùng
        public static GoogleTokenPayload VerifyAccessToken(string accessToken)
        {
            var info = GoiGoogle("https://oauth2.googleapis.com/tokeninfo?access_token=" + Uri.EscapeDataString(accessToken), null);
            if (info == null || GiaTri(info, "aud") != ClientId) return null;

            var dict = GoiGoogle("https://www.googleapis.com/oauth2/v3/userinfo", accessToken);
            if (dict == null || GiaTri(dict, "sub") != GiaTri(info, "sub")) return null;

            return TaoHoSoGoogle(dict);
        }

        /// <summary>
        /// Tìm tài khoản theo email Google; chưa có thì tạo Học viên mới, có rồi thì cập nhật họ tên / ảnh đại diện từ Google.
        /// Không kiểm tra khóa tài khoản / vai trò: nơi gọi tự kiểm tra vì website và app báo lỗi khác nhau.
        /// </summary>
        public static NguoiDung TimHoacTaoNguoiDung(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, GoogleTokenPayload hoSo)
        {
            string email = hoSo.Email.Trim().ToLower();

            // Họ và tên đầy đủ từ Google (ưu tiên Họ + Tên nếu có)
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

            string avatar = string.IsNullOrWhiteSpace(hoSo.Picture) ? ApiCourseMapper.AnhDaiDienMacDinh : hoSo.Picture.Trim();

            // Tuyệt đối không lấy tên tài khoản / tiền tố email làm họ tên
            if (string.IsNullOrWhiteSpace(name) || name.Equals(email.Split('@')[0], StringComparison.OrdinalIgnoreCase))
            {
                name = TenMacDinh;
            }

            var user = db.NguoiDung.FirstOrDefault(u => u.email.ToLower() == email);
            if (user == null)
            {
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
                return user;
            }

            bool isUpdated = false;
            if (!string.IsNullOrEmpty(avatar) && user.anhDaiDien != avatar)
            {
                user.anhDaiDien = avatar;
                isUpdated = true;
            }

            // Cập nhật họ và tên đầy đủ từ Google vào csdl
            if (name != TenMacDinh)
            {
                // Cập nhật nếu tên trong DB khác tên Google, hoặc DB đang chứa tên tài khoản/email prefix
                if (user.hoTen != name || user.hoTen.Equals(email.Split('@')[0], StringComparison.OrdinalIgnoreCase) || user.hoTen.Equals(TenMacDinh, StringComparison.OrdinalIgnoreCase))
                {
                    user.hoTen = name;
                    isUpdated = true;
                }
            }

            if (isUpdated)
            {
                db.SaveChanges();
            }
            return user;
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
    }
}
