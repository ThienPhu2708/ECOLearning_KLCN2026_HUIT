using Microsoft.Owin;
using Owin;
using System;
using System.Security.Principal;
using System.Web.Security;

[assembly: OwinStartup(typeof(ECOLearning.Startup))]

namespace ECOLearning
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Web MVC dùng Session, SignalR không có Session. Cookie đã được
            // AccountController ký/mã hóa khi đăng nhập phải được đọc riêng ở đây.
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments(new PathString("/signalr")))
                {
                    var identity = ChatIdentity(context.Request.Cookies[FormsAuthentication.FormsCookieName]);
                    if (identity != null) context.Request.User = identity;
                }
                await next();
            });
            // Bật SignalR 2 Hub endpoint (/signalr)
            app.MapSignalR();
        }

        public static IPrincipal ChatIdentity(string cookie)
        {
            if (string.IsNullOrWhiteSpace(cookie)) return null;
            try
            {
                var ticket = FormsAuthentication.Decrypt(cookie);
                if (ticket == null || ticket.Expired || string.IsNullOrWhiteSpace(ticket.Name)) return null;
                return new GenericPrincipal(new FormsIdentity(ticket), new string[0]);
            }
            catch (ArgumentException) { return null; }
            catch (FormatException) { return null; }
            catch (System.Security.Cryptography.CryptographicException) { return null; }
            catch (System.Web.HttpException) { return null; }
        }
    }
}

