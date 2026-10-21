using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Mỗi tài khoản Admin / Marketing chỉ có một phiên web hoạt động (migration 017): người đăng nhập sau thay người trước.
    ///  - Đăng nhập: tạo mã phiên mới, ghi vào NguoiDung (kèm thời điểm, IP, trình duyệt) và Session["MaPhien"].
    ///  - Mỗi request (AccountStatusFilter): mã phiên trong Session khác mã trong CSDL thì phiên này đã bị thay -> đăng xuất,
    ///    hiện pop-up cho biết lần đăng nhập mới, ghi NhatKyQuanTri.
    ///  - Trang đang mở hỏi lại mỗi 15 giây (Views/Shared/_PhienDangNhap.cshtml) để pop-up hiện cả khi không thao tác.
    ///  - Session lưu trong bộ nhớ nên mất khi để trang lâu (20 phút) hoặc hosting khởi động lại ứng dụng; cookie ECO_Phien
    ///    (mã người dùng + mã phiên) cho phép khôi phục Session nếu mã phiên vẫn còn là mã trong CSDL (KhoiPhuc, gọi ở Global.asax),
    ///    nên Admin / Marketing không phải đăng nhập lại mà phiên bị thay vẫn bị đá. Đăng xuất thì thu hồi mã phiên.
    /// Không chặn người đến sau: người dùng đóng trình duyệt / mất mạng vẫn đăng nhập lại được ngay.
    /// Học viên, giáo viên không áp dụng (được đăng nhập nhiều thiết bị). Chưa chạy migration 017 thì tính năng tự tắt.
    /// Web.config không bật Forms authentication nên Session là nơi duy nhất xác định người đăng nhập.
    /// </summary>
    public static class PhienDocQuyenService
    {
        public const string KeySession = "MaPhien";
        private const string CookieBiThay = "ECO_PhienBiThay";
        private const string CookiePhien = "ECO_Phien";

        // Khôi phục Session từ cookie chỉ trong khoảng này kể từ lúc đăng nhập (một ngày làm việc); quá hạn thì đăng nhập lại
        private const int KhoiPhucToiDaGio = 12;

        // Có cột maPhien chưa (chưa chạy migration 017 thì bỏ qua); kiểm tra lại mỗi 5 phút
        private static bool? coCot;
        private static DateTime lanKiemTraCot = DateTime.MinValue;

        public static bool ApDung(string vaiTro)
        {
            return string.Equals(vaiTro, AccountAdminService.Admin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(vaiTro, AccountAdminService.Marketing, StringComparison.OrdinalIgnoreCase);
        }

        public static bool DaCaiDat(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            if (coCot != true && (DateTime.UtcNow - lanKiemTraCot).TotalMinutes >= 5)
            {
                coCot = db.Database.SqlQuery<int>("SELECT CASE WHEN COL_LENGTH('dbo.NguoiDung', 'maPhien') IS NULL THEN 0 ELSE 1 END").Single() == 1;
                lanKiemTraCot = DateTime.UtcNow;
            }
            return coCot == true;
        }

        /// <summary>Đăng nhập: tạo mã phiên mới thay mã cũ trong CSDL. Trả null khi vai trò không áp dụng hoặc chưa có cột.</summary>
        public static string BatDau(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, int maNguoiDung, string vaiTro, HttpRequestBase request)
        {
            if (!ApDung(vaiTro)) return null;
            try
            {
                if (!DaCaiDat(db)) return null;
                string maPhien = Guid.NewGuid().ToString("N");
                db.Database.ExecuteSqlCommand(@"
                    UPDATE dbo.NguoiDung SET maPhien = @ma, phienLuc = GETDATE(), phienIp = @ip, phienTrinhDuyet = @tb
                    WHERE maNguoiDung = @id",
                    new SqlParameter("@ma", maPhien),
                    new SqlParameter("@ip", LayIp(request)),
                    new SqlParameter("@tb", MoTaTrinhDuyet(request.UserAgent)),
                    new SqlParameter("@id", maNguoiDung));
                return maPhien;
            }
            catch (Exception ex)
            {
                // Lỗi CSDL không chặn đăng nhập
                System.Diagnostics.Debug.WriteLine("PhienDocQuyenService.BatDau error: " + ex.Message);
                return null;
            }
        }

        /// <param name="maPhienCsdl">NguoiDung.maPhien, đọc cùng trạng thái tài khoản trong AccountStatusFilter.</param>
        public static bool BiThay(HttpSessionStateBase session, string maPhienCsdl)
        {
            // Chưa ai đăng nhập từ khi có tính năng: chưa có gì để so
            if (string.IsNullOrEmpty(maPhienCsdl)) return false;
            return !string.Equals(maPhienCsdl, session[KeySession] as string, StringComparison.Ordinal);
        }

        /// <summary>Phiên này vừa bị thay: lấy thông tin lần đăng nhập mới để báo người dùng, ghi NhatKyQuanTri.</summary>
        public static PhienDangNhapMoi XuLyBiThay(int maNguoiDung, HttpRequestBase request)
        {
            var moi = new PhienDangNhapMoi();
            try
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    var p = db.Database.SqlQuery<PhienCsdl>(
                        "SELECT phienLuc AS Luc, phienIp AS Ip, phienTrinhDuyet AS TrinhDuyet FROM dbo.NguoiDung WHERE maNguoiDung = @p0",
                        maNguoiDung).FirstOrDefault();
                    if (p != null)
                    {
                        moi.ThoiGian = p.Luc.HasValue ? GioVietNam(p.Luc.Value).ToString("HH:mm dd/MM/yyyy") : null;
                        moi.TrinhDuyet = p.TrinhDuyet;
                        moi.Ip = p.Ip;
                    }

                    new AccountAdminService(db).GhiNhatKy(null, AccountAdminService.HdThayPhien, AccountAdminService.DoiTuongNguoiDung,
                        maNguoiDung.ToString(),
                        "Phiên trên " + MoTaTrinhDuyet(request.UserAgent) + " (IP " + LayIp(request) + ") bị đăng xuất vì tài khoản đăng nhập mới lúc "
                        + (moi.ThoiGian ?? "?") + " từ " + (moi.TrinhDuyet ?? "?") + " (IP " + (moi.Ip ?? "?") + ").");
                }
            }
            catch (Exception ex)
            {
                // Không ghi được nhật ký vẫn đăng xuất phiên cũ
                System.Diagnostics.Debug.WriteLine("PhienDocQuyenService.XuLyBiThay error: " + ex.Message);
            }
            return moi;
        }

        public static string ThongBao(PhienDangNhapMoi moi)
        {
            var chiTiet = new List<string>();
            if (moi.ThoiGian != null) chiTiet.Add("lúc " + moi.ThoiGian);
            if (moi.TrinhDuyet != null) chiTiet.Add("trình duyệt " + moi.TrinhDuyet);
            if (moi.Ip != null) chiTiet.Add("IP " + moi.Ip);
            return "Tài khoản của bạn vừa được đăng nhập ở nơi khác" + (chiTiet.Count > 0 ? " (" + string.Join(", ", chiTiet) + ")" : "")
                + " nên phiên làm việc này đã kết thúc. Nếu không phải bạn, hãy đăng nhập lại và đổi mật khẩu ngay.";
        }

        /// <summary>JSON trả cho lệnh AJAX của phiên bị thay; _PhienDangNhap.cshtml hiện pop-up từ dữ liệu này.</summary>
        public static object DuLieuAjax(PhienDangNhapMoi moi, string urlDangNhap)
        {
            return new
            {
                success = false,
                requireLogin = true,
                phienBiThay = true,
                message = ThongBao(moi),
                thoiGian = moi.ThoiGian,
                trinhDuyet = moi.TrinhDuyet,
                ip = moi.Ip,
                redirectUrl = urlDangNhap
            };
        }

        // ------------------------------------------------------------------ giữ đăng nhập khi Session mất

        /// <summary>Đăng nhập Admin / Marketing: ghi cookie để khôi phục Session; vai trò khác (maPhien null) thì xóa cookie cũ.</summary>
        public static void GhiCookiePhien(HttpRequestBase request, HttpResponseBase response, int maNguoiDung, string maPhien)
        {
            if (string.IsNullOrEmpty(maPhien))
            {
                XoaCookiePhien(response);
                return;
            }
            // Không đặt Expires: cookie mất khi đóng trình duyệt
            response.Cookies.Add(new HttpCookie(CookiePhien, maNguoiDung + "." + maPhien)
            {
                HttpOnly = true,
                Secure = request.IsSecureConnection,
                SameSite = SameSiteMode.Lax
            });
        }

        public static void XoaCookiePhien(HttpResponseBase response)
        {
            response.Cookies.Add(new HttpCookie(CookiePhien, "") { HttpOnly = true, Expires = DateTime.Now.AddDays(-1) });
        }

        /// <summary>
        /// Session đã mất (để trang lâu, ứng dụng khởi động lại) nhưng trình duyệt còn cookie ECO_Phien: khôi phục Session nếu mã phiên
        /// vẫn là mã trong CSDL, tài khoản đang hoạt động, vẫn là Admin / Marketing và đăng nhập chưa quá KhoiPhucToiDaGio giờ.
        /// Mã đã khác (tài khoản đăng nhập ở nơi khác) thì báo pop-up "đăng nhập ở nơi khác" như khi bị đá lúc đang mở trang.
        /// Gọi ở Application_PostAcquireRequestState, trước mọi controller (AdminBaseController kiểm tra Session rất sớm).
        /// </summary>
        public static void KhoiPhuc(HttpContextBase ctx)
        {
            var session = ctx.Session;
            if (session == null || session["UserId"] != null) return;
            var cookie = ctx.Request.Cookies[CookiePhien];
            if (cookie == null || string.IsNullOrEmpty(cookie.Value)) return;

            var phan = cookie.Value.Split('.');
            if (phan.Length != 2 || !int.TryParse(phan[0], out int maNguoiDung) || phan[1].Length != 32)
            {
                XoaCookiePhien(ctx.Response);
                return;
            }
            string maPhien = phan[1];

            try
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    if (!DaCaiDat(db)) return;
                    var p = db.Database.SqlQuery<PhienKhoiPhuc>(@"
                        SELECT maPhien AS MaPhien,
                               CAST(CASE WHEN phienLuc > DATEADD(HOUR, -@gio, GETDATE()) THEN 1 ELSE 0 END AS BIT) AS ConHan
                        FROM dbo.NguoiDung
                        WHERE maNguoiDung = @id AND ISNULL(trangThai, 1) = 1 AND vaiTro IN (N'Admin', N'Marketing')",
                        new SqlParameter("@gio", KhoiPhucToiDaGio), new SqlParameter("@id", maNguoiDung)).FirstOrDefault();

                    if (p == null || !p.ConHan)
                    {
                        // Tài khoản bị khóa / đổi vai trò / quá hạn: đăng nhập lại như bình thường
                        XoaCookiePhien(ctx.Response);
                        return;
                    }
                    if (!string.Equals(p.MaPhien, maPhien, StringComparison.Ordinal))
                    {
                        XoaCookiePhien(ctx.Response);
                        GhiCookieBiThay(ctx.Response, XuLyBiThay(maNguoiDung, ctx.Request));
                        return;
                    }

                    var u = db.NguoiDung.Find(maNguoiDung);
                    if (u == null) return;
                    session[KeySession] = maPhien;
                    session["UserId"] = u.maNguoiDung;
                    session["FullName"] = u.hoTen;
                    session["Email"] = u.email;
                    session["Role"] = u.vaiTro;
                    session["Avatar"] = string.IsNullOrEmpty(u.anhDaiDien) ? SessionNguoiDung.AnhDaiDienMacDinh : u.anhDaiDien;
                    session["User"] = SessionNguoiDung.BanSao(u);
                }
            }
            catch (Exception ex)
            {
                // Lỗi CSDL tạm thời: để người dùng tự đăng nhập lại, không chặn request
                System.Diagnostics.Debug.WriteLine("PhienDocQuyenService.KhoiPhuc error: " + ex.Message);
            }
        }

        /// <summary>
        /// Đăng xuất: đổi mã phiên trong CSDL sang mã ngẫu nhiên mới (nếu mã đang dùng đúng là của phiên này) để cookie cũ không khôi phục
        /// được nữa. Không đặt NULL: NULL nghĩa là "chưa có gì để so", phiên đã bị thay trước đó sẽ sống lại.
        /// </summary>
        public static void ThuHoi(int maNguoiDung, string maPhien)
        {
            if (maNguoiDung <= 0 || string.IsNullOrEmpty(maPhien)) return;
            try
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    if (!DaCaiDat(db)) return;
                    db.Database.ExecuteSqlCommand(
                        "UPDATE dbo.NguoiDung SET maPhien = @moi WHERE maNguoiDung = @id AND maPhien = @cu",
                        new SqlParameter("@moi", Guid.NewGuid().ToString("N")),
                        new SqlParameter("@id", maNguoiDung),
                        new SqlParameter("@cu", maPhien));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PhienDocQuyenService.ThuHoi error: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ cookie cho pop-up

        /// <summary>
        /// Lưu thông tin cho pop-up (Session vừa bị hủy nên không dùng TempData được): trang Đăng nhập đọc khi bị chuyển về,
        /// lượt hỏi định kỳ đọc khi một lệnh AJAX khác của trang (vd. đếm chấm đỏ) đã phát hiện phiên bị thay trước nó.
        /// </summary>
        public static void GhiCookieBiThay(HttpResponseBase response, PhienDangNhapMoi moi)
        {
            response.Cookies.Add(new HttpCookie(CookieBiThay, string.Join("|", Ma(moi.ThoiGian), Ma(moi.TrinhDuyet), Ma(moi.Ip)))
            {
                HttpOnly = true,
                Expires = DateTime.Now.AddMinutes(5)
            });
        }

        /// <summary>Đọc rồi xóa cookie: pop-up chỉ hiện một lần.</summary>
        public static PhienDangNhapMoi DocCookieBiThay(HttpRequestBase request, HttpResponseBase response)
        {
            var cookie = request.Cookies[CookieBiThay];
            if (cookie == null || string.IsNullOrEmpty(cookie.Value)) return null;
            response.Cookies.Add(new HttpCookie(CookieBiThay, "") { HttpOnly = true, Expires = DateTime.Now.AddDays(-1) });

            var p = cookie.Value.Split('|');
            return new PhienDangNhapMoi { ThoiGian = Giai(p, 0), TrinhDuyet = Giai(p, 1), Ip = Giai(p, 2) };
        }

        private static string Ma(string s) => HttpUtility.UrlEncode(s ?? "");

        private static string Giai(string[] p, int i)
        {
            string s = i < p.Length ? HttpUtility.UrlDecode(p[i]) : null;
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        // ------------------------------------------------------------------ IP, trình duyệt, giờ

        public static string LayIp(HttpRequestBase request)
        {
            // Sau proxy / load balancer của hosting thì IP thật nằm ở X-Forwarded-For (chỉ để hiển thị, không dùng phân quyền)
            string ip = (request.Headers["X-Forwarded-For"] ?? "").Split(',')[0].Trim();
            if (ip.Length == 0) ip = request.UserHostAddress ?? "";
            return ip.Length > 45 ? ip.Substring(0, 45) : ip;
        }

        /// <summary>"Chrome trên Windows", "Safari trên iOS"... từ User-Agent.</summary>
        public static string MoTaTrinhDuyet(string ua)
        {
            if (string.IsNullOrWhiteSpace(ua)) return "trình duyệt không rõ";

            string trinhDuyet = ua.Contains("Edg/") ? "Edge"
                : ua.Contains("OPR/") || ua.Contains("Opera") ? "Opera"
                : ua.Contains("coc_coc") ? "Cốc Cốc"
                : ua.Contains("Firefox/") ? "Firefox"
                : ua.Contains("Chrome/") || ua.Contains("CriOS/") ? "Chrome"
                : ua.Contains("Safari/") ? "Safari"
                : "Trình duyệt khác";
            string heDieuHanh = ua.Contains("Android") ? "Android"
                : ua.Contains("iPhone") || ua.Contains("iPad") ? "iOS"
                : ua.Contains("Windows") ? "Windows"
                : ua.Contains("Mac OS X") ? "macOS"
                : ua.Contains("Linux") ? "Linux"
                : null;
            return heDieuHanh == null ? trinhDuyet : trinhDuyet + " trên " + heDieuHanh;
        }

        // Máy chủ hosting có thể đặt giờ UTC: hiển thị theo GMT+7
        private static DateTime GioVietNam(DateTime local)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                return TimeZoneInfo.ConvertTime(local, TimeZoneInfo.Local, tz);
            }
            catch (TimeZoneNotFoundException)
            {
                return local.ToUniversalTime().AddHours(7);
            }
        }

        private class PhienKhoiPhuc
        {
            public string MaPhien { get; set; }
            public bool ConHan { get; set; }
        }

        private class PhienCsdl
        {
            public DateTime? Luc { get; set; }
            public string Ip { get; set; }
            public string TrinhDuyet { get; set; }
        }
    }

    /// <summary>Lần đăng nhập đã thay phiên hiện tại, hiển thị trong pop-up.</summary>
    public class PhienDangNhapMoi
    {
        public string ThoiGian { get; set; }
        public string TrinhDuyet { get; set; }
        public string Ip { get; set; }
    }
}
