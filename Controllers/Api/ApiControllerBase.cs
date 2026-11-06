using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.SessionState;
using ECOLearning.Models;
using ECOLearning.Services.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace ECOLearning.Controllers.Api
{
    /// <summary>API này cần đăng nhập: thiếu / sai token trả 401.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
    public sealed class ApiAuthorizeAttribute : Attribute
    {
        
    }

    /// <summary>
    /// Lớp nền của API cho ứng dụng di động (/api/v1/..., tài liệu: Tuan10/Task_Tuan10.docx mục 5 – 6).
    /// - Không dùng Session (app gửi token trong header Authorization: Bearer &lt;token&gt;).
    /// - Mọi phản hồi cùng khung: { success, message, data } hoặc { success: false, code, message, errors }.
    /// - Lỗi không lường trước trả 500 dạng JSON, không lộ chi tiết lỗi ra ngoài.
    /// </summary>
    [SessionState(SessionStateBehavior.Disabled)]
    public abstract class ApiControllerBase : Controller
    {
        protected readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 Db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private NguoiDung currentUser;

        /// <summary>Mã người dùng của token hợp lệ (0 = chưa đăng nhập).</summary>
        protected int CurrentUserId { get; private set; }

        protected NguoiDung CurrentUser => CurrentUserId <= 0 ? null : (currentUser ?? (currentUser = Db.NguoiDung.Find(CurrentUserId)));

        /// <summary>Token gốc trong header (để thu hồi khi đăng xuất)</summary>
        protected string CurrentToken { get; private set; }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            CurrentToken = DocToken(Request);
            if (CurrentToken != null)
            {
                int userId = new ApiTokenService(Db).XacThuc(CurrentToken);
                if (userId > 0)
                {
                    var user = Db.NguoiDung.Find(userId);
                    if (user != null && user.trangThai == false)
                    {
                        filterContext.Result = Fail(403, "ACCOUNT_LOCKED", "Tài khoản của bạn đã bị khóa.");
                        return;
                    }
                    currentUser = user;
                    CurrentUserId = user != null ? userId : 0;
                }
            }

            bool canDangNhap = filterContext.ActionDescriptor.IsDefined(typeof(ApiAuthorizeAttribute), true)
                || filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(ApiAuthorizeAttribute), true);
            if (canDangNhap && CurrentUserId <= 0)
            {
                filterContext.Result = Fail(401, "UNAUTHORIZED", "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.");
                return;
            }
            base.OnActionExecuting(filterContext);
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            System.Diagnostics.Trace.TraceError("API " + Request.HttpMethod + " " + Request.RawUrl + ": " + filterContext.Exception);
            filterContext.ExceptionHandled = true;
            filterContext.Result = Fail(500, "SERVER_ERROR", "Có lỗi xảy ra, vui lòng thử lại.");
        }

        private static string DocToken(HttpRequestBase request)
        {
            string header = request.Headers["Authorization"];
            if (string.IsNullOrWhiteSpace(header)) return null;
            const string bearer = "Bearer ";
            if (!header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)) return null;
            string token = header.Substring(bearer.Length).Trim();
            return token.Length == 0 ? null : token;
        }

        // ------------------------------------------------------------------ phản hồi

        protected ActionResult Ok(object data = null, string message = null)
        {
            return new ApiJsonResult(200, new { success = true, message, data });
        }

        protected ActionResult Fail(int status, string code, string message, IDictionary<string, string> errors = null)
        {
            return new ApiJsonResult(status, new { success = false, code, message, errors });
        }

        protected ActionResult BadRequest(string message, string code = "INVALID_DATA", IDictionary<string, string> errors = null)
            => Fail(400, code, message, errors);

        protected ActionResult NotFoundJson(string message = "Không tìm thấy dữ liệu.", string code = "NOT_FOUND")
            => Fail(404, code, message);

        protected ActionResult Forbidden(string message, string code = "NOT_ELIGIBLE") => Fail(403, code, message);

        /// <summary>Dữ liệu phân trang (mục 5.4): { items, page, pageSize, total, totalPages }</summary>
        protected static object Paged<T>(IEnumerable<T> items, int page, int pageSize, int total)
        {
            return new
            {
                items,
                page,
                pageSize,
                total,
                totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
            };
        }

        protected static int ChuanHoaTrang(int page) => page < 1 ? 1 : page;

        protected static int ChuanHoaKichThuoc(int pageSize, int macDinh = 12) => pageSize <= 0 ? macDinh : Math.Min(pageSize, 50);

        // ------------------------------------------------------------------ dữ liệu gửi lên

        /// <summary>Đọc body JSON thành đối tượng (thiếu body / sai định dạng → đối tượng rỗng).</summary>
        protected T Body<T>() where T : class, new()
        {
            try
            {
                var stream = Request.InputStream;
                if (stream.CanSeek) stream.Position = 0;
                using (var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true))
                {
                    string json = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(json)) return new T();
                    return JsonConvert.DeserializeObject<T>(json) ?? new T();
                }
            }
            catch (JsonException)
            {
                return new T();
            }
        }

        // ------------------------------------------------------------------ đường dẫn

        /// <summary>
        /// Đường dẫn đầy đủ cho app (ảnh, video, trang thanh toán). Địa chỉ gốc lấy theo thứ tự:
        /// appSettings "Api:PublicBaseUrl" → header X-Forwarded-Host (tunnel) → địa chỉ của request.
        /// </summary>
        protected string AbsoluteUrl(string duongDan)
        {
            if (string.IsNullOrWhiteSpace(duongDan)) return null;
            duongDan = duongDan.Trim();
            if (duongDan.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || duongDan.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return duongDan;
            if (duongDan.StartsWith("~")) duongDan = duongDan.Substring(1);
            if (!duongDan.StartsWith("/")) duongDan = "/" + duongDan;
            return DiaChiGoc() + duongDan;
        }

        protected string DiaChiGoc()
        {
            string cauHinh = (ConfigurationManager.AppSettings["Api:PublicBaseUrl"] ?? "").Trim().TrimEnd('/');
            if (cauHinh.Length > 0) return cauHinh;
            string host = Request.Headers["X-Forwarded-Host"];
            if (!string.IsNullOrWhiteSpace(host))
            {
                string proto = Request.Headers["X-Forwarded-Proto"];
                return (string.IsNullOrWhiteSpace(proto) ? "https" : proto.Split(',')[0].Trim()) + "://" + host.Split(',')[0].Trim();
            }
            return Request.Url.GetLeftPart(UriPartial.Authority);
        }

        /// <summary>Đối tượng User (mục 5.5): thông tin tài khoản + số khóa đang học / hoàn thành / chứng chỉ.</summary>
        protected object UserJson(NguoiDung u)
        {
            int dangHoc = 0, hoanThanh = 0, chungChi = 0;
            var maKhoa = Db.DangKyKhoaHoc.Where(d => d.maHocVien == u.maNguoiDung).Select(d => d.maKhoaHoc).ToList();
            if (maKhoa.Count > 0)
            {
                var tienDo = new ECOLearning.Services.LearningService(Db).TienDoCacKhoa(u.maNguoiDung, maKhoa);
                hoanThanh = tienDo.Values.Count(t => t.DaHoanThanh);
                dangHoc = tienDo.Count - hoanThanh;
            }
            try
            {
                chungChi = Db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM dbo.QuizValidCertificate WHERE StudentId = @u AND IssuedAt IS NOT NULL",
                    new System.Data.SqlClient.SqlParameter("@u", u.maNguoiDung)).Single();
            }
            catch
            {
                chungChi = 0; // chưa chạy migration 007
            }
            return new
            {
                id = u.maNguoiDung,
                fullName = u.hoTen,
                email = u.email,
                avatarUrl = AbsoluteUrl(string.IsNullOrWhiteSpace(u.anhDaiDien) ? ECOLearning.Services.Api.ApiCourseMapper.AnhDaiDienMacDinh : u.anhDaiDien),
                role = u.vaiTro,
                phone = u.soDienThoai,
                gender = u.gioiTinh,
                birthDate = ApiFormat.Day(u.ngaySinh),
                joinDate = (string)null,
                inProgressCount = dangHoc,
                completedCount = hoanThanh,
                certificateCount = chungChi
            };
        }

        private static readonly System.Text.RegularExpressions.Regex YoutubeRegex = new System.Text.RegularExpressions.Regex(
            @"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/|youtube\.com\/shorts\/)([^""&?\/\s]{11})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        /// Link phát video của bài học. videoType: "file" (video tải lên, phát qua /VideoStream/Play có hỗ trợ tua),
        /// "youtube" (video_player không phát được, app mở bằng trình phát YouTube / trình duyệt), "none" (chưa có video).
        /// </summary>
        protected void VideoCuaBai(string duongDanVideo, out string videoUrl, out string videoType)
        {
            videoUrl = null;
            videoType = "none";
            if (string.IsNullOrWhiteSpace(duongDanVideo)) return;
            string dd = duongDanVideo.Trim();
            // Video tải lên (máy chủ hoặc R2): phát qua /VideoStream/Play; video R2 được chuyển hướng sang link ký có hạn dùng
            if (ECOLearning.Services.VideoStorageService.LaVideoTaiLen(dd))
            {
                videoUrl = AbsoluteUrl(ECOLearning.Services.VideoStorageService.LinkPhat(dd));
                videoType = "file";
                return;
            }
            var yt = YoutubeRegex.Match(dd);
            if (yt.Success)
            {
                videoUrl = "https://www.youtube.com/watch?v=" + yt.Groups[1].Value;
                videoType = "youtube";
                return;
            }
            videoUrl = AbsoluteUrl(dd);
            videoType = "file";
        }

        protected string IpKhach()
        {
            string fwd = Request.Headers["CF-Connecting-IP"] ?? Request.Headers["X-Forwarded-For"];
            return !string.IsNullOrWhiteSpace(fwd) ? fwd.Split(',')[0].Trim() : Request.UserHostAddress;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Db.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Trả JSON (camelCase, UTF-8) kèm mã HTTP, không để IIS thay trang lỗi mặc định vào body.</summary>
    public class ApiJsonResult : ActionResult
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        private readonly int status;
        private readonly object body;

        public ApiJsonResult(int status, object body)
        {
            this.status = status;
            this.body = body;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = status;
            response.TrySkipIisCustomErrors = true;
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            response.Write(JsonConvert.SerializeObject(body, Settings));
        }
    }

    /// <summary>Định dạng dữ liệu trả về theo quy ước mục 5.4.</summary>
    public static class ApiFormat
    {
        /// <summary>Thời gian giờ máy chủ (giờ Việt Nam) → "2026-10-20T10:09:00+07:00"</summary>
        public static string Date(DateTime? local)
        {
            if (!local.HasValue) return null;
            var dt = DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified);
            return new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt)).ToString("yyyy-MM-dd'T'HH:mm:sszzz");
        }

        /// <summary>Thời gian lưu theo UTC (bảng Quiz*) → giờ Việt Nam</summary>
        public static string DateFromUtc(DateTime? utc)
        {
            if (!utc.HasValue) return null;
            return Date(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime());
        }

        /// <summary>Chỉ ngày: "2005-08-27"</summary>
        public static string Day(DateTime? d) => d.HasValue ? d.Value.ToString("yyyy-MM-dd") : null;

        public static long Money(decimal value) => (long)Math.Round(value, 0, MidpointRounding.AwayFromZero);
    }
}
