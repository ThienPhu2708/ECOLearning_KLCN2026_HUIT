using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Hosting;

namespace ECOLearning.Services
{
    /// <summary>
    /// Nhật ký chẩn đoán ghi ra App_Data/logs/app-yyyyMMdd.log (giờ GMT+7), để biết trên hosting:
    ///  - APP_START / APP_END: ứng dụng khởi động / tắt, kèm lý do tắt, bộ nhớ và thời gian đã chạy. Session lưu trong bộ nhớ nên
    ///    mỗi lần ứng dụng tắt là mọi người dùng bị đăng xuất; APP_END cho biết vì sao (vượt giới hạn bộ nhớ, tái chế app pool, đổi file...).
    ///  - LOI: lỗi không bắt được (trang, người dùng, nội dung lỗi). Nhiều action chỉ Debug.WriteLine rồi nuốt lỗi nên không nằm ở đây.
    /// Chỉ ghi chẩn đoán: không ghi mật khẩu, cookie, chuỗi truy vấn. Không bao giờ ném lỗi ra ngoài (ghi log hỏng không được làm hỏng web).
    /// App_Data không bị IIS phục vụ trực tiếp và ghi vào đó không làm ứng dụng khởi động lại. Tự xóa file quá 14 ngày.
    /// </summary>
    public static class AppLog
    {
        private const long ToiDaByteMoiNgay = 2 * 1024 * 1024;
        private const int GiuSoNgay = 14;

        private static readonly object khoa = new object();
        private static DateTime batDauLuc = DateTime.UtcNow;

        public static void Ghi(string loai, string noiDung)
        {
            try
            {
                string thuMuc = Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "logs");
                var gio = DateTime.UtcNow.AddHours(7);
                string dong = "[" + gio.ToString("yyyy-MM-dd HH:mm:ss") + " GMT+7] [pid " + Process.GetCurrentProcess().Id + "] " + loai + ": "
                    + noiDung + Environment.NewLine;

                lock (khoa)
                {
                    Directory.CreateDirectory(thuMuc);
                    string file = Path.Combine(thuMuc, "app-" + gio.ToString("yyyyMMdd") + ".log");
                    // Đủ dung lượng của ngày thì dừng (tránh lỗi lặp vô hạn làm đầy ổ đĩa hosting)
                    if (File.Exists(file) && new FileInfo(file).Length > ToiDaByteMoiNgay) return;
                    File.AppendAllText(file, dong, Encoding.UTF8);
                }
            }
            catch
            {
                // Ghi log hỏng không được ảnh hưởng người dùng
            }
        }

        public static void KhoiDong()
        {
            batDauLuc = DateTime.UtcNow;
            DonLogCu();
            Ghi("APP_START", "Ứng dụng khởi động. " + BoNho());
        }

        public static void Tat()
        {
            string lyDo = HostingEnvironment.ShutdownReason.ToString();
            var sb = new StringBuilder();
            sb.Append("Ứng dụng tắt (mọi phiên đăng nhập trong bộ nhớ mất). Lý do: ").Append(lyDo);
            sb.Append(". Đã chạy ").Append((DateTime.UtcNow - batDauLuc).ToString(@"d\.hh\:mm\:ss")).Append(". ").Append(BoNho());

            // ASP.NET giữ thông điệp và dấu vết của lệnh tắt trong trường nội bộ; đọc bằng reflection, lỗi thì bỏ qua
            try
            {
                var runtime = typeof(HttpRuntime).InvokeMember("_theRuntime", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.GetField, null, null, null);
                if (runtime != null)
                {
                    string thongDiep = runtime.GetType().InvokeMember("_shutDownMessage", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField, null, runtime, null) as string;
                    string dauVet = runtime.GetType().InvokeMember("_shutDownStack", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField, null, runtime, null) as string;
                    if (!string.IsNullOrWhiteSpace(thongDiep)) sb.Append(" | Thông điệp: ").Append(thongDiep.Trim().Replace("\r", " ").Replace("\n", " "));
                    if (!string.IsNullOrWhiteSpace(dauVet)) sb.Append(" | Dấu vết: ").Append(dauVet.Trim().Replace("\r", " ").Replace("\n", " "));
                }
            }
            catch
            {
            }
            Ghi("APP_END", sb.ToString());
        }

        /// <param name="nguon">"MVC" (action ném lỗi) hoặc "ASP.NET" (lỗi ngoài action).</param>
        public static void GhiLoi(string nguon, Exception ex, HttpContextBase ctx)
        {
            if (ex == null) return;
            try
            {
                var http = ex as HttpException;
                if (http != null && http.GetHttpCode() == 404) return;   // đường dẫn không có, bot dò: không phải lỗi của ứng dụng

                // Một lỗi đi qua bộ lọc MVC rồi còn tới Application_Error (khi trang lỗi tùy biến tắt): chỉ ghi một lần cho mỗi request
                if (ctx != null)
                {
                    if (ctx.Items.Contains("ECO_DaGhiLoi")) return;
                    ctx.Items["ECO_DaGhiLoi"] = true;
                }

                var sb = new StringBuilder();
                sb.Append(nguon).Append(" | ");
                if (ctx != null)
                {
                    string role = null, userId = null;
                    try
                    {
                        userId = ctx.Session?["UserId"]?.ToString();
                        role = ctx.Session?["Role"]?.ToString();
                    }
                    catch
                    {
                    }
                    sb.Append(ctx.Request.HttpMethod).Append(' ').Append(ctx.Request.Url?.AbsolutePath)
                      .Append(" | user=").Append(userId ?? "-").Append(" role=").Append(role ?? "-")
                      .Append(" | ");
                }
                sb.Append(ex.ToString().Replace("\r", " ").Replace("\n", " "));
                Ghi("LOI", sb.ToString());
            }
            catch
            {
            }
        }

        private static string BoNho()
        {
            try
            {
                var p = Process.GetCurrentProcess();
                return "Bộ nhớ: " + (p.WorkingSet64 / 1048576) + " MB (đỉnh " + (p.PeakWorkingSet64 / 1048576) + " MB)";
            }
            catch
            {
                return "";
            }
        }

        private static void DonLogCu()
        {
            try
            {
                string thuMuc = Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "logs");
                if (!Directory.Exists(thuMuc)) return;
                foreach (var f in Directory.GetFiles(thuMuc, "app-*.log"))
                {
                    if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-GiuSoNgay)) File.Delete(f);
                }
            }
            catch
            {
            }
        }
    }
}
