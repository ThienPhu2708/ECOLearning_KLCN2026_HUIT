using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Controllers
{
    public class ReviewController : Controller
    {
        // POST: Review/SubmitReview (UC6: Đánh giá sao & nhận xét khóa học khi hoàn thành)
        [HttpPost]
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

        private int CurrentUserId
        {
            get
            {
                int id = (Session["UserId"] as int?) ?? 0;
                if (id == 0 && Session["User"] is NguoiDung u) id = u.maNguoiDung;
                return id;
            }
        }

        // POST: Review/Helpful — "Bạn thấy hữu ích?" (helpful = true / false; bấm lại lựa chọn đang có để bỏ)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Helpful(string reviewId, bool helpful)
        {
            int userId = CurrentUserId;
            if (userId == 0) return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để bình chọn đánh giá." });
            using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
            {
                var service = new CourseReviewService(db);
                if (!service.IsInstalled()) return Json(new { success = false, message = "Chức năng chưa sẵn sàng (cần chạy migration 011)." });
                var kq = service.BinhChon(reviewId, userId, helpful);
                return Json(new { success = kq.Success, message = kq.Message, helpfulCount = kq.SoHuuIch, notHelpfulCount = kq.SoKhongHuuIch, myVote = kq.BinhChon });
            }
        }

        // POST: Review/Reply — giáo viên của khóa viết / sửa phản hồi cho một đánh giá.
        // ValidateInput(false): cho phép ký tự như "<" trong nội dung; mọi chỗ hiển thị đều encode (Razor / textContent).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public JsonResult Reply(string reviewId, string content)
        {
            int userId = CurrentUserId;
            if (userId == 0) return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập." });
            using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
            {
                var service = new CourseReviewService(db);
                if (!service.IsInstalled()) return Json(new { success = false, message = "Chức năng chưa sẵn sàng (cần chạy migration 011)." });
                var kq = service.LuuPhanHoi(reviewId, userId, content);
                return Json(new
                {
                    success = kq.Success,
                    message = kq.Message,
                    content = kq.NoiDung,
                    dateText = kq.Success ? ReviewTime.ThoiGianTruoc(kq.NgayCapNhat ?? kq.NgayTao) + (kq.NgayCapNhat.HasValue ? " (đã sửa)" : "") : null
                });
            }
        }

        // POST: Review/DeleteReply
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DeleteReply(string reviewId)
        {
            int userId = CurrentUserId;
            if (userId == 0) return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập." });
            using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
            {
                var service = new CourseReviewService(db);
                if (!service.IsInstalled()) return Json(new { success = false, message = "Chức năng chưa sẵn sàng (cần chạy migration 011)." });
                var kq = service.XoaPhanHoi(reviewId, userId);
                return Json(new { success = kq.Success, message = kq.Message });
            }
        }
    }
}
