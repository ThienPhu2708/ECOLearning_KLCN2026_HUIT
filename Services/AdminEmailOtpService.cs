using System;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.Security;

namespace ECOLearning.Services
{
    public sealed class AdminOtpChallenge
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string PasswordStamp { get; set; }
        public bool RememberMe { get; set; }
        public string ReturnUrl { get; set; }
        public DateTime DeadlineUtc { get; set; }
        public DateTime CodeExpiresUtc { get; set; }
        public DateTime SentUtc { get; set; }
        public string Salt { get; set; }
        public string CodeHash { get; set; }
    }

    public static class AdminEmailOtpService
    {
        public const string PendingKey = "AdminOtpPending";
        private const string VerifiedKey = "AdminOtpVerifiedUser";
        private const string UntilKey = "AdminOtpVerifiedUntil";
        private const string CookieName = "ECO_AdminOtp";
        private static readonly object Gate = new object();
        private sealed class Limit { public int Sends; public int Failures; public DateTime LastSend; }
        public static bool Required(string role) => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        public static string Stamp(string value)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? "")));
        }
        private static Limit Limits(int userId, DateTime now)
        {
            string key = "AdminOtpLimit:" + userId;
            var value = HttpRuntime.Cache[key] as Limit;
            if (value != null) return value;
            value = new Limit();
            HttpRuntime.Cache.Insert(key, value, null, now.AddMinutes(15), Cache.NoSlidingExpiration);
            return value;
        }
        public static int ResendSeconds(AdminOtpChallenge pending, DateTime now)
            => pending == null ? 0 : Math.Max(0, (int)Math.Ceiling((pending.SentUtc.AddSeconds(60) - now).TotalSeconds));

        // Limits are shared across browser sessions for the same account. Sending never resets failures.
        public static string Send(AdminOtpChallenge pending, DateTime now, Func<string, bool> deliver)
        {
            string code, salt, hash;
            lock (Gate)
            {
                var limit = Limits(pending.UserId, now);
                if (limit.Failures >= 5 || limit.Sends >= 5) return "Bạn đã thử quá nhiều lần. Vui lòng chờ 15 phút rồi đăng nhập lại.";
                if (ResendSeconds(pending, now) > 0 || (now - limit.LastSend).TotalSeconds < 60)
                    return "Vui lòng chờ 60 giây giữa hai lần gửi mã.";
                uint number;
                using (var rng = RandomNumberGenerator.Create())
                {
                    var bytes = new byte[4];
                    do { rng.GetBytes(bytes); number = BitConverter.ToUInt32(bytes, 0); } while (number >= 4294000000u);
                }
                code = (number % 1000000).ToString("D6");
                salt = Guid.NewGuid().ToString("N");
                hash = Stamp(salt + code);
                pending.CodeHash = null; // A previous code cannot survive a resend, even when delivery fails.
                limit.Sends++;
                limit.LastSend = now;
                pending.SentUtc = now;
            }
            if (!deliver(code)) return "Chưa gửi được email xác thực. Vui lòng chờ rồi bấm Gửi lại mã.";
            lock (Gate)
            {
                pending.Salt = salt;
                pending.CodeHash = hash;
                pending.CodeExpiresUtc = now.AddMinutes(5);
            }
            return null;
        }

        public static string Verify(AdminOtpChallenge pending, string code, DateTime now)
        {
            lock (Gate)
            {
                if (pending == null || now >= pending.DeadlineUtc) return "Phiên xác thực đã hết hạn. Vui lòng đăng nhập lại.";
                var limit = Limits(pending.UserId, now);
                if (limit.Failures >= 5) return "Bạn đã nhập sai 5 lần. Vui lòng chờ 15 phút rồi đăng nhập lại.";
                if (pending.CodeHash == null || now >= pending.CodeExpiresUtc) return "Mã không còn hiệu lực. Vui lòng gửi lại mã.";
                var candidate = Stamp(pending.Salt + (code ?? "").Trim());
                int different = 0;
                for (int i = 0; i < candidate.Length; i++) different |= candidate[i] ^ pending.CodeHash[i];
                if (different != 0)
                {
                    limit.Failures++;
                    return limit.Failures >= 5 ? "Bạn đã nhập sai 5 lần. Vui lòng chờ 15 phút rồi đăng nhập lại."
                        : "Mã xác thực không đúng. Còn " + (5 - limit.Failures) + " lần thử.";
                }
                pending.CodeHash = null; // consume before creating any authenticated session
                return null;
            }
        }

        public static bool IsVerified(HttpSessionStateBase session)
            => session != null && session["UserId"] != null
                && Equals(session[VerifiedKey], session["UserId"])
                && session[UntilKey] is DateTime until && DateTime.UtcNow < until;

        public static void MarkVerified(HttpSessionStateBase session, HttpRequestBase request, HttpResponseBase response)
        {
            var until = DateTime.UtcNow.AddHours(12);
            session[VerifiedKey] = session["UserId"];
            session[UntilKey] = until;
            string ticket = session["UserId"] + "|" + (session[PhienDocQuyenService.KeySession] as string ?? "") + "|" + until.Ticks;
            response.Cookies.Add(new HttpCookie(CookieName, HttpServerUtility.UrlTokenEncode(MachineKey.Protect(Encoding.UTF8.GetBytes(ticket), CookieName)))
                { HttpOnly = true, Secure = request.IsSecureConnection, SameSite = SameSiteMode.Lax, Path = "/" });
        }

        public static bool Restore(HttpContextBase context, int userId, string sessionId)
        {
            try
            {
                string cookie = context.Request.Cookies[CookieName]?.Value;
                if (string.IsNullOrEmpty(cookie) || string.IsNullOrEmpty(sessionId)) return false;
                var bytes = MachineKey.Unprotect(HttpServerUtility.UrlTokenDecode(cookie), CookieName);
                if (bytes == null) return false;
                var parts = Encoding.UTF8.GetString(bytes).Split('|');
                if (parts.Length != 3 || parts[0] != userId.ToString() || parts[1] != sessionId
                    || !long.TryParse(parts[2], out long ticks) || ticks <= DateTime.UtcNow.Ticks) return false;
                context.Session[VerifiedKey] = userId;
                context.Session[UntilKey] = new DateTime(ticks, DateTimeKind.Utc);
                return true;
            }
            catch (Exception) { return false; }
        }
        public static void ClearCookie(HttpResponseBase response)
            => response.Cookies.Add(new HttpCookie(CookieName, "") { HttpOnly = true, Path = "/", Expires = DateTime.UtcNow.AddDays(-1) });
        public static string MaskEmail(string email)
        {
            var parts = (email ?? "").Split('@');
            return parts.Length == 2 && parts[0].Length > 0 ? parts[0][0] + "***@" + parts[1] : "email của bạn";
        }
    }
}
