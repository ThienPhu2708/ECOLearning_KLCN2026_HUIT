using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;

namespace ECOLearning.Controllers
{
    public class ReviewController : Controller
    {
        // POST: Review/SubmitReview (UC6: Đánh giá sao & nhận xét khóa học khi hoàn thành)
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SubmitReview(string courseId, int rating, string comment)
        {
            int currentUserId = (Session["UserId"] as int?) ?? 0;
            if (currentUserId == 0 && Session["User"] is NguoiDung u)
            {
                currentUserId = u.maNguoiDung;
            }

            if (currentUserId == 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để gửi đánh giá!" });
            }

            if (string.IsNullOrEmpty(courseId) || rating < 1 || rating > 5)
            {
                return Json(new { success = false, message = "Dữ liệu đánh giá không hợp lệ!" });
            }

            try
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    var quiz = new ECOLearning.Services.QuizService(db);
                    var state = quiz.Installed() ? quiz.Student(courseId, currentUserId) : null;
                    if (state == null || !state.LessonsDone || !state.Passed)
                        return Json(new { success = false, message = "Bạn cần hoàn thành tất cả bài học và đạt quiz cuối khóa trước khi đánh giá." });
                    var existing = db.DanhGiaKhoaHoc.FirstOrDefault(d => d.maHocVien == currentUserId && d.maKhoaHoc == courseId);
                    if (existing != null)
                    {
                        existing.soSao = rating;
                        existing.noiDung = comment;
                        existing.ngayDanhGia = DateTime.Now;
                    }
                    else
                    {
                        string reviewId = "DG" + DateTime.Now.ToString("yyMMddHHmmss") + (new Random().Next(1000, 9999));
                        var dg = new DanhGiaKhoaHoc
                        {
                            maDanhGia = reviewId,
                            soSao = rating,
                            noiDung = comment,
                            ngayDanhGia = DateTime.Now,
                            maHocVien = currentUserId,
                            maKhoaHoc = courseId
                        };
                        db.DanhGiaKhoaHoc.Add(dg);
                    }
                    db.SaveChanges();
                }

                return Json(new { success = true, message = "Cảm ơn bạn đã đánh giá khóa học! Đánh giá đã được ghi nhận thành công." });
            }
            catch (Exception ex)
            {
                string detail = ex.InnerException?.InnerException?.Message
                    ?? ex.InnerException?.Message
                    ?? ex.Message;
                return Json(new { success = false, message = "Lỗi khi lưu đánh giá: " + detail });
            }
        }

        // GET: Review/GetCourseReviews
        public ActionResult GetCourseReviews(string courseId)
        {
            return PartialView();
        }
    }
}
