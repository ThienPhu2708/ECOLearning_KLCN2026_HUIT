using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning
{
    /// <summary>
    /// Bộ lọc toàn cục cho phiên web (không áp dụng API: API dùng token và không có Session).
    ///  - Tài khoản bị Admin khóa (hoặc đã bị xóa) thì đăng xuất ngay ở lần truy cập kế tiếp,
    ///    thay vì vẫn dùng được đến khi phiên hết hạn.
    ///  - Vai trò đổi trong CSDL (vd. được duyệt làm giáo viên) thì cập nhật lại Session["Role"].
    ///  - Tài khoản đang dùng mật khẩu tạm do Admin cấp (canDoiMatKhau, migration 016) chỉ được vào trang Hồ sơ để đổi mật khẩu.
    ///  - Admin / Marketing: tài khoản đã đăng nhập ở nơi khác thì phiên này bị đăng xuất, kèm pop-up (PhienDocQuyenService, migration 017).
    /// Đọc CSDL tối đa mỗi 60 giây cho một phiên để không làm chậm trang; Admin / Marketing đọc mỗi request (một dòng theo khóa chính).
    /// </summary>
    public class AccountStatusFilter : ActionFilterAttribute
    {
        private const int ChuKyGiay = 60;
        public const string KeyCanDoiMatKhau = "CanDoiMatKhau";

        // Có cột canDoiMatKhau chưa (chưa chạy migration 016 thì bỏ qua phần mật khẩu tạm); kiểm tra lại mỗi 5 phút
        private static bool? coCotDoiMatKhau;
        private static DateTime lanKiemTraCot = DateTime.MinValue;

        // Thời điểm Admin vừa đổi trạng thái tài khoản (khóa, đặt lại mật khẩu...): phiên của người đó kiểm tra lại ngay,
        // không chờ hết chu kỳ 60 giây. Lưu trong bộ nhớ ứng dụng (một máy chủ); khởi động lại thì chu kỳ 60 giây vẫn bảo đảm.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, DateTime> lanThayDoi =
            new System.Collections.Concurrent.ConcurrentDictionary<int, DateTime>();

        public static void DanhDauThayDoi(int userId)
        {
            lanThayDoi[userId] = DateTime.UtcNow;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.IsChildAction) return;
            var session = filterContext.HttpContext.Session;
            if (session == null || session["UserId"] == null) return;
            if (!int.TryParse(session["UserId"].ToString(), out int userId)) return;

            string keyLanKiemTra = "_TkKiemTra_" + userId;
            var lanTruoc = session[keyLanKiemTra] as DateTime?;
            bool vuaThayDoi = lanTruoc.HasValue && lanThayDoi.TryGetValue(userId, out DateTime luc) && luc >= lanTruoc.Value;
            // Phiên bị thay phải phát hiện ngay, không chờ hết chu kỳ
            bool docQuyen = PhienDocQuyenService.ApDung(session["Role"] as string);
            if (!lanTruoc.HasValue || vuaThayDoi || docQuyen || (DateTime.UtcNow - lanTruoc.Value).TotalSeconds >= ChuKyGiay)
            {
                var tt = DocTrangThai(userId);
                if (tt != null)
                {
                    if (!tt.HoatDong)
                    {
                        DangXuat(filterContext);
                        return;
                    }
                    if (PhienDocQuyenService.ApDung(tt.VaiTro) && PhienDocQuyenService.BiThay(session, tt.MaPhien))
                    {
                        DangXuatDoThayPhien(filterContext, userId);
                        return;
                    }
                    session[keyLanKiemTra] = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(tt.VaiTro) && !string.Equals(tt.VaiTro, session["Role"]?.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        session["Role"] = tt.VaiTro;
                    }
                    if (tt.CanDoiMatKhau) session[KeyCanDoiMatKhau] = true;
                    else session.Remove(KeyCanDoiMatKhau);
                }
            }

            if (session[KeyCanDoiMatKhau] as bool? == true && !DuocVaoKhiCanDoiMatKhau(filterContext))
            {
                const string thongBao = "Bạn đang dùng mật khẩu tạm. Vui lòng đổi mật khẩu mới trước khi tiếp tục.";
                var url = new UrlHelper(filterContext.RequestContext).Action("Index", "Profile", new { area = "", tab = "password" });
                filterContext.Result = LaAjax(filterContext)
                    ? (ActionResult)new JsonResult { Data = new { success = false, message = thongBao, redirectUrl = url }, JsonRequestBehavior = JsonRequestBehavior.AllowGet }
                    : new RedirectResult(url);
            }
        }

        private static bool DuocVaoKhiCanDoiMatKhau(ActionExecutingContext c)
        {
            string controller = c.ActionDescriptor.ControllerDescriptor.ControllerName;
            string action = c.ActionDescriptor.ActionName;
            if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase)) return true;   // đăng xuất, quên mật khẩu
            return string.Equals(controller, "Profile", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(action, "Index", StringComparison.OrdinalIgnoreCase) || string.Equals(action, "ChangePassword", StringComparison.OrdinalIgnoreCase));
        }

        private static void KetThucPhien(ActionExecutingContext c)
        {
            // Bị khóa / bị thay phiên: không cho cookie khôi phục lại Session
            PhienDocQuyenService.XoaCookiePhien(c.HttpContext.Response);
            FormsAuthentication.SignOut();
            c.HttpContext.Session.Clear();
            c.HttpContext.Session.Abandon();
        }

        // Admin / Marketing: tài khoản vừa đăng nhập ở nơi khác. AJAX (kể cả lượt hỏi định kỳ) nhận JSON để pop-up hiện ngay trên trang đang mở;
        // trang thường chuyển về Đăng nhập và pop-up hiện ở đó. Cookie luôn ghi để lệnh đến sau vẫn biết lý do (Session đã hủy).
        private static void DangXuatDoThayPhien(ActionExecutingContext c, int userId)
        {
            var moi = PhienDocQuyenService.XuLyBiThay(userId, c.HttpContext.Request);
            KetThucPhien(c);
            PhienDocQuyenService.GhiCookieBiThay(c.HttpContext.Response, moi);

            var url = new UrlHelper(c.RequestContext).Action("Login", "Account", new { area = "" });
            c.Result = LaAjax(c)
                ? (ActionResult)new JsonResult { Data = PhienDocQuyenService.DuLieuAjax(moi, url), JsonRequestBehavior = JsonRequestBehavior.AllowGet }
                : new RedirectResult(url);
        }

        private static void DangXuat(ActionExecutingContext c)
        {
            KetThucPhien(c);

            const string thongBao = "Tài khoản của bạn đã bị khóa. Vui lòng đăng nhập lại để xem chi tiết.";
            var url = new UrlHelper(c.RequestContext).Action("Login", "Account", new { area = "", locked = 1 });
            c.Result = LaAjax(c)
                ? (ActionResult)new JsonResult { Data = new { success = false, requireLogin = true, message = thongBao, redirectUrl = url }, JsonRequestBehavior = JsonRequestBehavior.AllowGet }
                : new RedirectResult(url);
        }

        private static bool LaAjax(ActionExecutingContext c)
        {
            var req = c.HttpContext.Request;
            return req.IsAjaxRequest() || (req.AcceptTypes != null && req.AcceptTypes.Any(t => t.Contains("json")));
        }

        private static TrangThaiPhien DocTrangThai(int userId)
        {
            try
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    if (coCotDoiMatKhau != true && (DateTime.UtcNow - lanKiemTraCot).TotalMinutes >= 5)
                    {
                        coCotDoiMatKhau = db.Database.SqlQuery<int>("SELECT CASE WHEN COL_LENGTH('dbo.NguoiDung', 'canDoiMatKhau') IS NULL THEN 0 ELSE 1 END").Single() == 1;
                        lanKiemTraCot = DateTime.UtcNow;
                    }

                    string cotDoiMatKhau = coCotDoiMatKhau == true ? "canDoiMatKhau" : "CAST(0 AS BIT)";
                    string cotPhien = PhienDocQuyenService.DaCaiDat(db) ? "maPhien" : "CAST(NULL AS VARCHAR(32))";
                    string sql = "SELECT CAST(ISNULL(trangThai, 1) AS BIT) AS HoatDong, vaiTro AS VaiTro, " + cotDoiMatKhau + " AS CanDoiMatKhau, "
                        + cotPhien + " AS MaPhien FROM dbo.NguoiDung WHERE maNguoiDung = @p0";
                    // Không còn dòng nào: tài khoản đã bị xóa, coi như bị khóa
                    return db.Database.SqlQuery<TrangThaiPhien>(sql, userId).FirstOrDefault() ?? new TrangThaiPhien { HoatDong = false };
                }
            }
            catch (Exception ex)
            {
                // Lỗi CSDL tạm thời: không đăng xuất nhầm người dùng, lần sau kiểm tra lại
                System.Diagnostics.Debug.WriteLine("AccountStatusFilter error: " + ex.Message);
                return null;
            }
        }

        private class TrangThaiPhien
        {
            public bool HoatDong { get; set; }
            public string VaiTro { get; set; }
            public bool CanDoiMatKhau { get; set; }
            public string MaPhien { get; set; }
        }
    }
}
