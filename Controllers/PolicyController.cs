using System.Web.Mvc;

namespace ECOLearning.Controllers
{
    /// <summary>
    /// Trang chính sách công khai (link ở footer): Điều khoản sử dụng, Chính sách bảo mật, Chính sách phân chia doanh thu.
    /// Số liệu nghiệp vụ (tỷ lệ chia doanh thu, mức rút tối thiểu) đọc từ hằng số dùng chung trong code, không gõ lại trong văn bản.
    /// </summary>
    public class PolicyController : Controller
    {
        /// <summary>Ngày cập nhật gần nhất của bộ chính sách; sửa nội dung trang nào thì đổi ngày ở đây.</summary>
        public const string NgayCapNhat = "30/10/2026";

        // GET: /Policy/Terms
        public ActionResult Terms()
        {
            return View();
        }

        // GET: /Policy/Privacy
        public ActionResult Privacy()
        {
            return View();
        }

        // GET: /Policy/RevenueShare
        public ActionResult RevenueShare()
        {
            return View();
        }
    }
}
