using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Services;

namespace ECOLearning.Controllers.Api
{
    /// <summary>Nhóm J – Hỗ trợ: báo cáo vi phạm khóa học (ViolationReportController) và form liên hệ (Home/ContactSubmit) của website.</summary>
    [RoutePrefix("api/v1")]
    public class SupportApiController : ApiControllerBase
    {
        private const int SoLienHeToiDa = 5;
        private static readonly TimeSpan CuaSoLienHe = TimeSpan.FromMinutes(10);
        private const int DoDaiLienHeToiDa = 5000;

        public class ReportRequest
        {
            public string Type { get; set; }
            public string Description { get; set; }
            public string LessonId { get; set; }
        }

        public class ContactRequest
        {
            public string FullName { get; set; }
            public string Email { get; set; }
            public string Subject { get; set; }
            public string Content { get; set; }
        }

        // J1. GET /api/v1/violation-types : các loại vi phạm học viên được chọn khi báo cáo
        [HttpGet, Route("violation-types")]
        public ActionResult ViolationTypes()
        {
            return Ok(ViolationReportService.ViolationTypes);
        }

        // J2. POST /api/v1/courses/{id}/reports  { type, description, lessonId? }
        // Cùng quy tắc website: đã đăng ký khóa, không phải khóa của mình, mỗi khóa chỉ một báo cáo đang chờ xử lý
        [HttpPost, Route("courses/{id}/reports"), ApiAuthorize]
        public ActionResult ReportCourse(string id)
        {
            var req = Body<ReportRequest>();
            var errors = new Dictionary<string, string>();
            if (!ViolationReportService.ViolationTypes.Contains(req.Type)) errors["type"] = "Vui lòng chọn loại vi phạm hợp lệ!";
            int doDai = (req.Description ?? "").Trim().Length;
            if (doDai < 10) errors["description"] = "Vui lòng mô tả cụ thể vi phạm (tối thiểu 10 ký tự)!";
            else if (doDai > 2000) errors["description"] = "Mô tả tối đa 2000 ký tự!";
            if (errors.Count > 0) return BadRequest(errors.Values.First(), "INVALID_DATA", errors);

            var service = new ViolationReportService(Db);
            if (!service.IsInstalled()) return Fail(503, "NOT_INSTALLED", "Chức năng báo cáo vi phạm chưa sẵn sàng. Vui lòng thử lại sau!");

            int userId = CurrentUserId;
            var course = Db.KhoaHoc.Where(k => k.maKhoaHoc == id).Select(k => new { k.maGiaoVien }).FirstOrDefault();
            if (course == null) return NotFoundJson("Không tìm thấy khóa học!", "COURSE_NOT_FOUND");
            if (course.maGiaoVien == userId) return Forbidden("Bạn không thể báo cáo khóa học do chính mình tạo!", "OWN_COURSE");
            if (!Db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == id && d.maHocVien == userId))
                return Forbidden("Chỉ học viên đã đăng ký khóa học mới được gửi báo cáo vi phạm!", "NOT_ENROLLED");
            if (service.HasPendingReport(id, userId))
                return Fail(409, "REPORT_PENDING", "Bạn đã có một báo cáo đang chờ xử lý cho khóa học này. Vui lòng chờ quản trị viên xem xét!");
            if (!string.IsNullOrWhiteSpace(req.LessonId) && !service.LessonBelongsToCourse(req.LessonId.Trim(), id))
                return BadRequest("Bài học được chọn không thuộc khóa học này!", "INVALID_DATA",
                    new Dictionary<string, string> { { "lessonId", "Bài học được chọn không thuộc khóa học này!" } });

            // CreateReport kiểm tra lại toàn bộ (nguồn quy tắc chung với website) rồi mới ghi
            string loi = service.CreateReport(id, userId, req.Type, req.Description, req.LessonId);
            if (loi != null) return BadRequest(loi);
            return Ok(null, "Đã gửi báo cáo vi phạm. Quản trị viên sẽ xem xét trong thời gian sớm nhất. Cảm ơn bạn!");
        }

        // J3. POST /api/v1/contact  { fullName, email, subject?, content }
        // Đã đăng nhập mà bỏ trống họ tên / email thì lấy theo tài khoản. Gửi email về hộp thư hỗ trợ (SmtpEmail) như website.
        [HttpPost, Route("contact")]
        public ActionResult Contact()
        {
            var req = Body<ContactRequest>();
            var user = CurrentUser;
            string hoTen = !string.IsNullOrWhiteSpace(req.FullName) ? req.FullName.Trim() : user?.hoTen;
            string email = !string.IsNullOrWhiteSpace(req.Email) ? req.Email.Trim() : user?.email;
            string noiDung = (req.Content ?? "").Trim();

            var errors = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(hoTen)) errors["fullName"] = "Vui lòng nhập Họ và tên!";
            else if (hoTen.Length > 100) errors["fullName"] = "Họ và tên tối đa 100 ký tự!";
            string loiEmail = ApiRules.KiemTraEmail(email);
            if (loiEmail != null) errors["email"] = loiEmail;
            if (noiDung.Length == 0) errors["content"] = "Vui lòng nhập Nội dung liên hệ!";
            else if (noiDung.Length > DoDaiLienHeToiDa) errors["content"] = "Nội dung tối đa " + DoDaiLienHeToiDa + " ký tự!";
            if (errors.Count > 0) return BadRequest(errors.Values.First(), "INVALID_DATA", errors);

            // Mỗi lần gửi là một email thật: chặn gửi dồn dập (theo tài khoản, khách thì theo địa chỉ IP)
            string khoa = "contact:" + (CurrentUserId > 0 ? "u" + CurrentUserId : "ip" + IpKhach());
            if (RateLimiter.VuotGioiHan(khoa, SoLienHeToiDa, CuaSoLienHe))
                return Fail(429, "TOO_MANY_REQUESTS", "Bạn gửi liên hệ hơi nhiều, vui lòng chờ vài phút rồi thử lại.");

            string tieuDe = string.IsNullOrWhiteSpace(req.Subject) ? "Liên hệ từ Ứng dụng E-Learning" : req.Subject.Trim();
            var kq = EmailService.SendContactMessage(hoTen, email, tieuDe, noiDung);
            if (!kq.Success) return Fail(502, "EMAIL_FAILED", "Không thể gửi tin nhắn lúc này: " + kq.Message);
            return Ok(null, "Đã gửi tin nhắn liên hệ thành công! Đội ngũ hỗ trợ sẽ phản hồi qua email sớm nhất.");
        }
    }
}
