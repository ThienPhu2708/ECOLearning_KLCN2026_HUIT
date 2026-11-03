using System;
using System.Collections.Generic;
using System.Web;
using ECOLearning.Services;

public static class AdminEmailOtpTests
{
    private static int count;
    private static void Check(bool pass, string label)
    {
        if (!pass) throw new Exception(label);
        count++;
        Console.WriteLine("PASS " + label);
    }
    private static AdminOtpChallenge Pending(int id, DateTime now) => new AdminOtpChallenge
        { UserId = id, DeadlineUtc = now.AddMinutes(15) };
    private sealed class Session : HttpSessionStateBase
    {
        private readonly Dictionary<string, object> data = new Dictionary<string, object>();
        public override object this[string key] { get => data.ContainsKey(key) ? data[key] : null; set => data[key] = value; }
    }
    private sealed class Request : HttpRequestBase
    {
        private readonly HttpCookieCollection cookies = new HttpCookieCollection();
        public override HttpCookieCollection Cookies => cookies;
        public override bool IsSecureConnection => true;
    }
    private sealed class Response : HttpResponseBase
    {
        private readonly HttpCookieCollection cookies = new HttpCookieCollection();
        public override HttpCookieCollection Cookies => cookies;
    }
    private sealed class Context : HttpContextBase
    {
        private readonly Session session = new Session();
        private readonly Request request = new Request();
        private readonly Response response = new Response();
        public override HttpSessionStateBase Session => session;
        public override HttpRequestBase Request => request;
        public override HttpResponseBase Response => response;
    }
    public static int Main()
    {
        try
        {
            DateTime now = DateTime.UtcNow;
            int id = -Math.Abs(Guid.NewGuid().GetHashCode() % 100000000) - 100;
            Check(AdminEmailOtpService.Required("aDmIn"), "Admin role requires OTP regardless of case");
            Check(!AdminEmailOtpService.Required("GiaoVien") && !AdminEmailOtpService.Required("Marketing"), "Other roles keep existing login");
            var pending = Pending(id, now);
            string code = null;
            Check(AdminEmailOtpService.Send(pending, now, value => { code = value; return true; }) == null, "Issue OTP using injected delivery, no email sent by tests");
            Check(code.Length == 6 && System.Text.RegularExpressions.Regex.IsMatch(code, "^[0-9]{6}$"), "Six numeric digits");
            Check(pending.CodeHash != code && pending.CodeHash != null, "Challenge stores digest instead of plaintext code");
            Check(AdminEmailOtpService.Send(pending, now.AddSeconds(59), _ => { throw new Exception("Must not send"); }) != null, "Enforce resend cooldown");
            Check(AdminEmailOtpService.Verify(pending, code, now.AddSeconds(30)) == null, "Valid OTP accepted");
            Check(AdminEmailOtpService.Verify(pending, code, now.AddSeconds(31)) != null, "Consumed OTP cannot be replayed");
            pending = Pending(id - 1, now);
            AdminEmailOtpService.Send(pending, now, value => { code = value; return true; });
            Check(AdminEmailOtpService.Verify(pending, code, now.AddMinutes(5)) != null, "Exact expiration boundary rejects OTP");
            string old = code;
            AdminEmailOtpService.Send(pending, now.AddMinutes(5), value => { code = value; return true; });
            Check(pending.CodeExpiresUtc == now.AddMinutes(10), "Resend creates fresh five-minute validity");
            if (old != code) Check(AdminEmailOtpService.Verify(pending, old, now.AddMinutes(5)) != null, "Previous code invalidated after resend");
            Check(AdminEmailOtpService.Verify(pending, code, now.AddMinutes(5)) == null, "Replacement OTP works");
            pending = Pending(id - 2, now);
            AdminEmailOtpService.Send(pending, now, value => { code = value; return true; });
            for (int i = 0; i < 5; i++) Check(AdminEmailOtpService.Verify(pending, "wrong", now) != null, "Incorrect attempt " + (i + 1));
            Check(AdminEmailOtpService.Verify(pending, code, now) != null, "Correct code cannot bypass exhausted attempts");
            var anotherSession = Pending(id - 2, now);
            Check(AdminEmailOtpService.Send(anotherSession, now.AddMinutes(2), _ => true) != null, "New browser session cannot reset account failure limit");
            pending = Pending(id - 3, now);
            for (int i = 0; i < 5; i++) Check(AdminEmailOtpService.Send(pending, now.AddMinutes(i), _ => true) == null, "Allowed email send " + (i + 1));
            Check(AdminEmailOtpService.Send(pending, now.AddMinutes(5), _ => true) != null, "Bound total sends per account");
            pending = Pending(id - 4, now);
            AdminEmailOtpService.Send(pending, now, value => { code = value; return true; });
            Check(AdminEmailOtpService.Send(pending, now.AddMinutes(1), _ => false) != null, "Delivery failure surfaced");
            Check(AdminEmailOtpService.Verify(pending, code, now.AddMinutes(1)) != null, "Failed resend does not retain previous OTP");
            Check(AdminEmailOtpService.Verify(pending, code, now.AddMinutes(15)) != null, "Challenge expires after fifteen minutes");
            var session = new Session();
            session["UserId"] = 7;
            session["Role"] = "Admin";
            Check(!AdminEmailOtpService.IsVerified(session), "Role/session identity alone cannot authorize Admin");
            var context = new Context();
            context.Session["UserId"] = 7;
            context.Session[PhienDocQuyenService.KeySession] = "fixture-session";
            AdminEmailOtpService.MarkVerified(context.Session, context.Request, context.Response);
            Check(AdminEmailOtpService.IsVerified(context.Session), "Successful OTP marks correct session identity");
            var proof = context.Response.Cookies["ECO_AdminOtp"];
            Check(proof.HttpOnly && proof.Secure && proof.SameSite == SameSiteMode.Lax, "Verification cookie security attributes");
            var restored = new Context();
            restored.Request.Cookies.Add(new HttpCookie("ECO_AdminOtp", proof.Value));
            Check(AdminEmailOtpService.Restore(restored, 7, "fixture-session"), "Signed OTP proof restores matching exclusive session");
            Check(!AdminEmailOtpService.Restore(restored, 8, "fixture-session"), "Proof cannot authenticate another user");
            Check(!AdminEmailOtpService.Restore(restored, 7, "different-session"), "Proof cannot authenticate another login session");
            restored.Request.Cookies.Set(new HttpCookie("ECO_AdminOtp", "tampered"));
            Check(!AdminEmailOtpService.Restore(restored, 7, "fixture-session"), "Tampered proof rejected");
            context.Session["UserId"] = 8;
            Check(!AdminEmailOtpService.IsVerified(context.Session), "Session identity change requires new OTP");
            AdminEmailOtpService.ClearCookie(context.Response);
            Check(context.Response.Cookies["ECO_AdminOtp"].Expires < DateTime.UtcNow, "Logout expires OTP proof");
            Check(AdminEmailOtpService.MaskEmail("admin@example.com") == "a***@example.com", "Email masked in browser");
            Console.WriteLine(count + " checks passed; no SMTP or database writes.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
