using System;
using System.Web.Mvc;
using ECOLearning.Services;
using ECOLearning.Services.Api;

namespace ECOLearning.Controllers.Api
{
    /// <summary>Kiểm tra kết nối (P-0) và trả 404 dạng JSON cho đường dẫn /api/v1/... không tồn tại.</summary>
    [RoutePrefix("api/v1")]
    public class PingApiController : ApiControllerBase
    {
        // GET /api/v1/ping
        [HttpGet, Route("ping")]
        public ActionResult Ping()
        {
            return Ok(new
            {
                service = "ECOLearning API",
                version = "v1",
                serverTime = ApiFormat.Date(DateTime.Now),
                loggedIn = CurrentUserId > 0,
                // Địa chỉ dùng để tạo link ảnh / video / VNPay quay về: chạy qua tunnel mà vẫn là localhost thì đặt Api:PublicBaseUrl
                publicBaseUrl = DiaChiGoc(),
                tokenTableReady = new ApiTokenService(Db).IsInstalled(),
                payment = VnPayService.DangBat ? "vnpay-sandbox" : "web-sandbox"
            });
        }

    }

    /// <summary>
    /// Đường dẫn /api/... không khớp API nào: trả 404 dạng JSON thay cho trang lỗi HTML.
    /// Route thường "api/{*path}" khai báo trong RouteConfig, ngay sau các route [Route] của API.
    /// </summary>
    public class ApiNotFoundController : ApiControllerBase
    {
        public ActionResult Index(string path)
        {
            return NotFoundJson("Không có API /api/" + path + " (kiểm tra lại đường dẫn hoặc phương thức GET / POST / PUT).", "API_NOT_FOUND");
        }
    }
}
