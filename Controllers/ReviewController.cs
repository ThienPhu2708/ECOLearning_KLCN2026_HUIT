using System;
using System.Web.Mvc;

namespace ECOLearning.Controllers
{
    [Authorize(Roles = "HocVien")]
    public class ReviewController : Controller
    {
        // POST: Review/SubmitReview (UC6: Đánh giá sao & nhận xét khóa học khi tiến độ >= 70%)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitReview(string courseId, int rating, string comment)
        {
            return Json(new { success = true });
        }

        // GET: Review/GetCourseReviews
        public ActionResult GetCourseReviews(string courseId)
        {
            return PartialView();
        }
    }
}
