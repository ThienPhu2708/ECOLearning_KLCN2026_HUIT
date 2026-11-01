using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Services;
using ECOLearning.Services.Api;
using ECOLearning.ViewModels;
using Newtonsoft.Json;

namespace ECOLearning.Controllers.Api
{
    /// <summary>
    /// Nhóm H – Quiz cuối khóa và chứng chỉ (Task_Tuan10.docx mục 6.8). Dùng QuizService của website:
    /// học đủ 100% bài mới làm được, giới hạn lượt và thời gian, đạt quiz rồi mới nhận chứng chỉ.
    /// Không trả đáp án đúng khi chưa đạt (giáo viên có thể cấp thêm lượt làm).
    /// </summary>
    [RoutePrefix("api/v1"), ApiAuthorize]
    public class QuizApiController : ApiControllerBase
    {
        public class AnswersRequest
        {
            public int[] Answers { get; set; }
            public string WriterToken {get;set;}
            public string ClientKey {get;set;}
            public bool Takeover {get;set;}
        }

        private QuizService Quiz => new QuizService(Db);

        private ActionResult ChuaCoQuiz() => NotFoundJson("Khóa học chưa có quiz cuối khóa.", "QUIZ_NOT_FOUND");

        // H1. GET /api/v1/courses/{id}/quiz
        [HttpGet, Route("courses/{id}/quiz")]
        public ActionResult Info(string id)
        {
            var quiz = Quiz;
            if (!quiz.Installed()) return ChuaCoQuiz();
            if (!new LearningService(Db).DaGhiDanh(CurrentUserId, id)) return Forbidden("Bạn chưa đăng ký khóa học này.", "NOT_ENROLLED");
            var cauHinh = quiz.Config(id);
            if (cauHinh == null || !cauHinh.Published) return NotFoundJson("Giáo viên chưa mở quiz cuối khóa.", "QUIZ_NOT_FOUND");

            quiz.CloseExpired(id, CurrentUserId);
            var st = quiz.Student(id, CurrentUserId);
            var luot = quiz.Attempts(id, CurrentUserId);
            var dangLam = luot.FirstOrDefault(a => !a.SubmittedAt.HasValue);
            var ganNhat = luot.FirstOrDefault(a => a.SubmittedAt.HasValue && !a.IsVoided);
            int duocPhep = cauHinh.MaxAttempts + (st?.ExtraAttempts ?? 0);

            string lyDo = null;
            if (dangLam == null)
            {
                if (st == null || !st.LessonsDone) lyDo = "Học đủ 100% bài học trước khi làm quiz.";
                else if (st.PendingReview) lyDo = "Kết quả đang chờ giáo viên xem xét do rời màn hình từ 3 lần.";
                else if (st.RetakeRequired) lyDo = "Giáo viên yêu cầu làm lại quiz. Thực hiện lượt làm lại được chỉ định trên website.";
                else if (st.Passed) lyDo = "Bạn đã đạt quiz của khóa học này.";
                else if (st.Attempts >= duocPhep) lyDo = "Bạn đã hết lượt làm. Liên hệ giáo viên để được xem xét cấp thêm lượt.";
            }
            return Ok(new
            {
                title = cauHinh.Title,
                questionCount = cauHinh.Total,
                minutes = cauHinh.Minutes,
                skillMatrix = cauHinh.UseMatrix?JsonConvert.DeserializeObject<List<QuizSkillRule>>(cauHinh.MatrixJson??"[]"):new List<QuizSkillRule>(),
                passPercent = cauHinh.PassPercent,
                attemptsUsed = st?.Attempts ?? 0,
                attemptsAllowed = duocPhep,
                lessonsCompleted = st?.CompletedLessons ?? 0,
                totalLessons = st?.TotalLessons ?? 0,
                canStart = lyDo == null,
                reason = lyDo,
                activeAttemptId = dangLam?.Id,
                passed = st != null && st.Passed,
                bestScore = st?.BestScore,
                retakeRequired = st != null && st.RetakeRequired,
                pendingReview = st != null && st.PendingReview,
                lastResult = ganNhat == null ? null : KetQua(ganNhat),
                certificate = new { issued = st != null && st.IssuedAt.HasValue, code = st?.Code }
            });
        }

        // H2. POST /api/v1/courses/{id}/quiz/start
        [HttpPost, Route("courses/{id}/quiz/start")]
        public ActionResult Start(string id)
        {
            var quiz = Quiz;
            if (!quiz.Installed()) return ChuaCoQuiz();
            var writerRequest=Body<AnswersRequest>();
            Guid clientId;if(!Guid.TryParse(writerRequest.ClientKey,out clientId))return Forbidden("Cập nhật ứng dụng để sử dụng phiên quiz an toàn.","QUIZ_CLIENT_REQUIRED");
            QuizAttempt a;string writerToken;
            try
            {
                a = quiz.Start(id, CurrentUserId);
                writerToken=new QuizWriterService(Db).Acquire(a.Id,CurrentUserId,writerRequest.ClientKey,writerRequest.Takeover);
                a=quiz.Attempt(a.Id);
            }
            catch (QuizSessionException ex) {return Forbidden(ex.Message,"QUIZ_SESSION_ACTIVE");}
            catch (InvalidOperationException ex)
            {
                return Forbidden(ex.Message);
            }
            var cauHoi = JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson);
            var traLoi = JsonConvert.DeserializeObject<int[]>(a.AnswersJson ?? "[]") ?? new int[0];
            return Ok(new
            {
                writerToken = writerToken,
                attemptId = a.Id,
                title = a.Title,
                startedAt = ApiFormat.DateFromUtc(a.StartedAt),
                expiresAt = ApiFormat.DateFromUtc(a.ExpiresAt),
                remainingSeconds = Math.Max(0, (int)(a.ExpiresAt - DateTime.UtcNow).TotalSeconds),
                passPercent = a.PassPercent,
                questions = cauHoi.Select((q, i) => new { index = i, text = q.Text, options = q.Options, skillId=q.SkillId,skillName=q.SkillName,skillPassPercent=q.SkillPassPercent,skillRequired=q.SkillRequired }).ToList(),
                answers = Enumerable.Range(0, cauHoi.Count).Select(i => i < traLoi.Length ? traLoi[i] : -1).ToList()
            });
        }

        // H3. PUT /api/v1/quiz-attempts/{attemptId}/draft
        [HttpPut, Route("quiz-attempts/{attemptId:int}/draft")]
        public ActionResult Draft(int attemptId)
        {
            var req = Body<AnswersRequest>();
            try
            {
                return Ok(new { saved = Quiz.SaveDraft(attemptId, CurrentUserId, req.Answers ?? new int[0],req.WriterToken,true) });
            }
            catch (QuizSessionException ex) {return Forbidden(ex.Message,"QUIZ_SESSION_ACTIVE");}
            catch (InvalidOperationException ex)
            {
                return NotFoundJson(ex.Message, "ATTEMPT_NOT_FOUND");
            }
        }

        // H4. POST /api/v1/quiz-attempts/{attemptId}/submit
        [HttpPost, Route("quiz-attempts/{attemptId:int}/submit")]
        public ActionResult Submit(int attemptId)
        {
            var req = Body<AnswersRequest>();
            var quiz = Quiz;
            try
            {
                quiz.Submit(attemptId, CurrentUserId, req.Answers ?? new int[0],req.WriterToken,true);
            }
            catch (QuizSessionException ex) {return Forbidden(ex.Message,"QUIZ_SESSION_ACTIVE");}
            catch (InvalidOperationException ex)
            {
                return NotFoundJson(ex.Message, "ATTEMPT_NOT_FOUND");
            }
            var a = quiz.Attempt(attemptId);
            return Ok(KetQua(a, kemDapAn: a.Passed == true), a.ReviewPending ? "Kết quả chờ giáo viên xem xét do bạn rời màn hình từ 3 lần." : a.Passed == true ? "Chúc mừng bạn đã đạt quiz cuối khóa!" : "Bạn chưa đạt quiz cuối khóa.");
        }

        /// <summary>Kết quả một lượt làm; đạt thì kèm đáp án đúng và giải thích từng câu</summary>
        private static object KetQua(QuizAttempt a, bool kemDapAn = false)
        {
            object dapAn = null;
            if (kemDapAn && !a.ReviewPending)
            {
                var cauHoi = JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson);
                var traLoi = JsonConvert.DeserializeObject<int[]>(a.AnswersJson ?? "[]") ?? new int[0];
                dapAn = cauHoi.Select((q, i) => new
                {
                    index = i,
                    text = q.Text,
                    options = q.Options,
                    yourAnswer = i < traLoi.Length ? traLoi[i] : -1,
                    correctAnswer = q.CorrectOption,
                    explanation = q.Explanation
                }).ToList();
            }
            return new
            {
                attemptId = a.Id,
                score = a.ReviewPending ? (decimal?)null : a.Score,
                skillResults = a.ReviewPending ? new List<QuizSkillResult>() : JsonConvert.DeserializeObject<List<QuizSkillResult>>(a.SkillResultsJson??"[]"),
                correct = a.ReviewPending ? (int?)null : a.Correct,
                total = a.Total,
                passed = !a.ReviewPending && !a.IsVoided && a.Passed == true,
                pendingReview = a.ReviewPending,
                status = a.ReviewPending ? "Chờ giáo viên xem xét" : a.IsVoided ? "Hết hiệu lực" : a.Passed == true ? "Đạt" : "Chưa đạt",
                isVoided = a.IsVoided,
                passPercent = a.PassPercent,
                timedOut = a.TimedOut,
                submittedAt = ApiFormat.DateFromUtc(a.SubmittedAt),
                review = dapAn
            };
        }

        // H5. POST /api/v1/courses/{id}/certificate
        [HttpPost, Route("courses/{id}/certificate")]
        public ActionResult Claim(string id)
        {
            var quiz = Quiz;
            if (!quiz.Installed()) return ChuaCoQuiz();
            try
            {
                quiz.ClaimCertificate(id, CurrentUserId);
            }
            catch (QuizSessionException ex) {return Forbidden(ex.Message,"QUIZ_SESSION_ACTIVE");}
            catch (InvalidOperationException ex)
            {
                return Forbidden(ex.Message);
            }
            var cc = DanhSachChungChi(id).FirstOrDefault();
            return cc == null ? NotFoundJson("Không tìm thấy chứng chỉ.", "CERTIFICATE_NOT_FOUND") : Ok(cc, "Chứng chỉ đã được cấp!");
        }

        // H6. GET /api/v1/certificates
        [HttpGet, Route("certificates")]
        public ActionResult Certificates() => Ok(Quiz.Installed() ? DanhSachChungChi(null) : new List<object>());

        // H7. GET /api/v1/certificates/{courseId}
        [HttpGet, Route("certificates/{courseId}")]
        public ActionResult Certificate(string courseId)
        {
            var cc = Quiz.Installed() ? DanhSachChungChi(courseId).FirstOrDefault() : null;
            return cc == null ? NotFoundJson("Bạn chưa có chứng chỉ của khóa học này.", "CERTIFICATE_NOT_FOUND") : Ok(cc);
        }

        private class ChungChiRow
        {
            public string Code { get; set; }
            public string CourseId { get; set; }
            public string CourseTitle { get; set; }
            public string Instructor { get; set; }
            public string StudentName { get; set; }
            public DateTime IssuedAt { get; set; }
            public decimal? BestScore { get; set; }
        }

        private List<object> DanhSachChungChi(string courseId)
        {
            var rows = Db.Database.SqlQuery<ChungChiRow>(@"
                SELECT c.Code, c.CourseId, k.tenKhoaHoc AS CourseTitle, gv.hoTen AS Instructor, hv.hoTen AS StudentName, c.IssuedAt,
                       (SELECT MAX(a.Score) FROM dbo.QuizEffectiveAttempt a WHERE a.CourseId = c.CourseId AND a.StudentId = c.StudentId AND a.Passed = 1) AS BestScore
                FROM dbo.QuizValidCertificate c
                INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = c.CourseId
                INNER JOIN dbo.NguoiDung hv ON hv.maNguoiDung = c.StudentId
                LEFT JOIN dbo.NguoiDung gv ON gv.maNguoiDung = k.maGiaoVien
                WHERE c.StudentId = @u AND c.IssuedAt IS NOT NULL AND (@c IS NULL OR c.CourseId = @c)
                ORDER BY c.IssuedAt DESC",
                new SqlParameter("@u", CurrentUserId),
                new SqlParameter("@c", (object)courseId ?? DBNull.Value)).ToList();
            var snapshots = new CourseRevisionService(Db, Server.MapPath).Certificates(CurrentUserId);
            var thoiLuong = new ApiCourseMapper(Db, AbsoluteUrl).Map(rows.Select(r => r.CourseId)).ToDictionary(c => c.Id, c => c);
            return rows.Select(r => (object)new
            {
                code = r.Code,
                courseId = r.CourseId,
                courseTitle = snapshots.ContainsKey(r.CourseId) ? snapshots[r.CourseId].CourseTitle : r.CourseTitle,
                instructor = snapshots.ContainsKey(r.CourseId) ? snapshots[r.CourseId].Instructor : r.Instructor,
                studentName = snapshots.ContainsKey(r.CourseId) ? snapshots[r.CourseId].StudentName : r.StudentName,
                issuedAt = ApiFormat.DateFromUtc(r.IssuedAt),
                score = r.BestScore,
                durationSeconds = snapshots.ContainsKey(r.CourseId) ? snapshots[r.CourseId].DurationSeconds : thoiLuong.ContainsKey(r.CourseId) ? thoiLuong[r.CourseId].DurationSeconds : 0,
                imageUrl = thoiLuong.ContainsKey(r.CourseId) ? thoiLuong[r.CourseId].ImageUrl : null
            }).ToList();
        }
    }
}
