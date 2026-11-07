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
            quiz.CloseExpired(id, CurrentUserId);
            quiz.EnsureCertificateWithoutQuiz(id, CurrentUserId);
            var cauHinh = quiz.Config(id);
            if (cauHinh == null) return NotFoundJson("Giáo viên chưa mở quiz cuối khóa.", "QUIZ_NOT_FOUND");

            var st = quiz.Student(id, CurrentUserId);
            var luot = quiz.Attempts(id, CurrentUserId);
            var dangLam = luot.FirstOrDefault(a => !a.SubmittedAt.HasValue);
            var ganNhat = luot.FirstOrDefault(a => a.SubmittedAt.HasValue);
            int duocPhep = cauHinh.MaxAttempts + (st?.ExtraAttempts ?? 0);

            string lyDo = null;
            if (dangLam == null)
            {
                if (!cauHinh.Published) lyDo = "Giáo viên chưa mở quiz cuối khóa.";
                else if (st == null || !st.LessonsDone) lyDo = "Học đủ 100% bài học trước khi làm quiz.";
                else if (st.PendingReview) lyDo = "Kết quả đang chờ giáo viên xem xét do rời màn hình từ 3 lần.";
                else if (st.RetakeRequired) lyDo = "Giáo viên yêu cầu làm lại quiz. Thực hiện lượt làm lại được chỉ định trên website.";
                else if (st.Passed || st.IssuedAt.HasValue) lyDo = "Bạn đã hoàn thành khóa học và không cần làm quiz này.";
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
                certificate = new { issued = st != null && st.IssuedAt.HasValue, code = st?.Code },
                supportsQuizReview = true
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
            return Ok(AttemptPayload(a,writerToken));
        }
        private object AttemptPayload(QuizAttempt a,string writerToken){
            var questions=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson);
            var answers=JsonConvert.DeserializeObject<int[]>(a.AnswersJson??"[]")??new int[0];
            return new{supportsQuizReview=true,writerToken,attemptId=a.Id,title=a.IsVerification?"Quiz làm lại":a.Title,isVerification=a.IsVerification,startedAt=ApiFormat.DateFromUtc(a.StartedAt),expiresAt=ApiFormat.DateFromUtc(a.ExpiresAt),remainingSeconds=Math.Max(0,(int)(a.ExpiresAt-DateTime.UtcNow).TotalSeconds),passPercent=a.PassPercent,departureCount=Db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.QuizVisibility WHERE AttemptId=@p0",a.Id).Single(),openDepartureKey=Db.Database.SqlQuery<string>("SELECT TOP 1 EventKey FROM dbo.QuizVisibility WHERE AttemptId=@p0 AND ReturnedAt IS NULL ORDER BY Id DESC",a.Id).FirstOrDefault(),questions=questions.Select((q,i)=>new{index=i,questionId=q.Id,text=q.Text,options=q.Options,skillId=q.SkillId,skillName=q.SkillName,skillPassPercent=q.SkillPassPercent,skillRequired=q.SkillRequired}),answers=Enumerable.Range(0,questions.Count).Select(i=>i<answers.Length?answers[i]:-1)};
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
            if (kemDapAn && !a.ReviewPending && !a.IsVoided)
            {
                var cauHoi = JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson);
                var traLoi = JsonConvert.DeserializeObject<int[]>(a.AnswersJson ?? "[]") ?? new int[0];
                dapAn = cauHoi.Select((q, i) => new
                {
                    index = i,
                    questionId = q.Id,
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
                supportsQuizReview = true,
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

        public class QuizSupportRequest {public int AttemptId{get;set;}public int QuestionId{get;set;}public string Reason{get;set;}public string Content{get;set;}public string EventKey{get;set;}public bool Returned{get;set;}public string WriterToken{get;set;}}
        [HttpGet,Route("courses/{id}/quiz/history")]
        public ActionResult History(string id){
            try{Quiz.RequireEnrollment(id,CurrentUserId);Quiz.CloseExpired(id,CurrentUserId);return Ok(Quiz.Attempts(id,CurrentUserId).Select(a=>new{attemptId=a.Id,startedAt=ApiFormat.DateFromUtc(a.StartedAt),submittedAt=ApiFormat.DateFromUtc(a.SubmittedAt),isVerification=a.IsVerification,isVoided=a.IsVoided,pendingReview=a.ReviewPending,result=a.SubmittedAt.HasValue?KetQua(a):null}));}catch(InvalidOperationException ex){return Forbidden(ex.Message);}
        }
        [HttpGet,Route("quiz-attempts/{attemptId:int}/result")]
        public ActionResult AttemptResult(int attemptId){
            var a=Quiz.Attempt(attemptId);if(a==null||a.StudentId!=CurrentUserId)return NotFoundJson("Không tìm thấy lượt thi.");
            try{Quiz.RequireEnrollment(a.CourseId,CurrentUserId);Quiz.CloseExpired(a.CourseId,CurrentUserId);a=Quiz.Attempt(attemptId);if(!a.SubmittedAt.HasValue)return Fail(409,"QUIZ_NOT_SUBMITTED","Lượt thi chưa kết thúc.");return Ok(KetQua(a,a.Passed==true));}catch(InvalidOperationException ex){return Forbidden(ex.Message);}
        }
        [HttpPost,Route("quiz-attempts/{attemptId:int}/resume")]
        public ActionResult Resume(int attemptId){
            var request=Body<AnswersRequest>();Guid key;if(!Guid.TryParse(request.ClientKey,out key))return Fail(400,"QUIZ_CLIENT_REQUIRED","Thiếu mã phiên quiz hợp lệ.");
            var a=Quiz.Attempt(attemptId);if(a==null||a.StudentId!=CurrentUserId)return NotFoundJson("Không tìm thấy lượt thi.");
            try{Quiz.RequireEnrollment(a.CourseId,CurrentUserId);Quiz.CloseExpired(a.CourseId,CurrentUserId);a=Quiz.Attempt(attemptId);if(a.SubmittedAt.HasValue)return Fail(409,"QUIZ_FINISHED","Lượt thi đã kết thúc. Xem kết quả đã lưu.");string token=new QuizWriterService(Db).Acquire(a.Id,CurrentUserId,request.ClientKey,request.Takeover);return Ok(AttemptPayload(Quiz.Attempt(a.Id),token));}catch(QuizSessionException ex){return Fail(409,"QUIZ_SESSION_ACTIVE",ex.Message);}catch(InvalidOperationException ex){return Forbidden(ex.Message);}
        }
        [HttpPost,Route("quiz-attempts/{attemptId:int}/visibility")]
        public ActionResult RecordVisibility(int attemptId){
            var request=Body<QuizSupportRequest>();var a=Quiz.Attempt(attemptId);if(a==null||a.StudentId!=CurrentUserId)return NotFoundJson("Không tìm thấy lượt thi.");
            try{if(!a.SubmittedAt.HasValue&&DateTime.UtcNow<a.ExpiresAt)new QuizWriterService(Db).Require(attemptId,request.WriterToken);new QuizVisibilityService(Db).Record(CurrentUserId,attemptId,request.EventKey,request.Returned);return Ok(new{count=Db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.QuizVisibility WHERE AttemptId=@p0",attemptId).Single()});}catch(QuizSessionException ex){return Fail(409,"QUIZ_SESSION_ACTIVE",ex.Message);}catch(InvalidOperationException ex){return Forbidden(ex.Message);}
        }
        [HttpPost,Route("quiz-attempts/{attemptId:int}/writer/check")]
        public ActionResult CheckWriter(int attemptId){
            var request=Body<AnswersRequest>();var a=Quiz.Attempt(attemptId);if(a==null||a.StudentId!=CurrentUserId)return NotFoundJson("Không tìm thấy lượt thi.");
            try{Quiz.RequireEnrollment(a.CourseId,CurrentUserId);if(a.SubmittedAt.HasValue||DateTime.UtcNow>=a.ExpiresAt)return Ok(new{active=false,finished=true});new QuizWriterService(Db).Require(attemptId,request.WriterToken);return Ok(new{active=true,finished=false});}catch(InvalidOperationException ex){return Fail(409,"QUIZ_SESSION_ACTIVE",ex.Message);}
        }
        [HttpPost,Route("quiz-attempts/{attemptId:int}/reports")]
        public ActionResult SendQuestionReport(int attemptId){try{var r=Body<QuizSupportRequest>();new QuizQuestionReportService(Db).Send(CurrentUserId,attemptId,r.QuestionId,r.Reason,r.Content);return Ok(new{sent=true},"Đã gửi báo lỗi đến giáo viên.");}catch(InvalidOperationException ex){return Forbidden(ex.Message);}}
        [HttpPost,Route("courses/{id}/quiz/appeals")]
        public ActionResult SendAppeal(string id){try{var r=Body<QuizSupportRequest>();Quiz.RequireEnrollment(id,CurrentUserId);var a=Quiz.Attempt(r.AttemptId);if(a==null||a.StudentId!=CurrentUserId||a.CourseId!=id)return NotFoundJson("Không tìm thấy lượt thi.");new QuizRegradeService(Db).Appeal(CurrentUserId,r.AttemptId,r.Content);return Ok(new{sent=true},"Đã gửi yêu cầu phúc khảo.");}catch(InvalidOperationException ex){return Forbidden(ex.Message);}}
        [HttpGet,Route("courses/{id}/quiz/support")]
        public ActionResult Support(string id){try{
            Quiz.RequireEnrollment(id,CurrentUserId);Quiz.CloseExpired(id,CurrentUserId);var regrade=new QuizRegradeService(Db);
            var appeals=regrade.Appeals(CurrentUserId,false).Where(x=>x.CourseId==id).ToList();
            var reports=new QuizQuestionReportService(Db).List(CurrentUserId,false,id);
            var retakes=new QuizRetakeService(Db).ForStudent(CurrentUserId,id);
            return Ok(new{
                canAppeal=appeals.Count==0&&Quiz.Attempts(id,CurrentUserId).Any(a=>a.SubmittedAt.HasValue),
                reports=reports.Select(r=>new{id=r.Id,attemptId=r.AttemptId,questionId=r.QuestionId,question=r.Question.Text,reason=r.Reason,content=r.Content,status=r.Status,statusText=r.StatusText,response=r.Response,createdAt=ApiFormat.DateFromUtc(r.CreatedAt)}),
                appeals=appeals.Select(r=>new{id=r.Id,attemptId=r.AttemptId,content=r.Content,status=r.Status,createdAt=ApiFormat.DateFromUtc(r.CreatedAt),replies=r.Replies.Select(x=>new{content=x.Content,status=x.Status,teacherName=x.TeacherName,createdAt=ApiFormat.DateFromUtc(x.CreatedAt)})}),
                retakes=retakes.Select(r=>new{id=r.Id,attemptId=r.AttemptId,canStart=r.CanRetake,statusText=r.StatusText,reason=r.Events.LastOrDefault(e=>e.Kind=="Decision")?.Content,teacherName=r.Events.LastOrDefault(e=>e.Kind=="Decision")?.AuthorName,createdAt=ApiFormat.DateFromUtc(r.CreatedAt),retakeAttemptId=r.RetakeAttemptId,submitted=r.Retake?.SubmittedAt.HasValue??false,result=r.Retake!=null&&r.Retake.SubmittedAt.HasValue?KetQua(r.Retake):null}),
                notices=regrade.Notices(CurrentUserId).Where(x=>Quiz.Attempt(x.AttemptId)?.CourseId==id).Select(x=>new{attemptId=x.AttemptId,oldScore=x.OldScore,score=x.RecognizedScore,reason=x.Reason,teacherName=x.TeacherName,createdAt=ApiFormat.DateFromUtc(x.CreatedAt)})
            });
        }catch(InvalidOperationException ex){return Forbidden(ex.Message);}}
        [HttpPost,Route("courses/{id}/quiz/retakes/{reviewId:int}/start")]
        public ActionResult StartRetake(string id,int reviewId){
            var request=Body<AnswersRequest>();Guid key;if(!Guid.TryParse(request.ClientKey,out key))return Fail(400,"QUIZ_CLIENT_REQUIRED","Thiếu mã phiên quiz hợp lệ.");
            try{if(!new QuizRetakeService(Db).ForStudent(CurrentUserId,id).Any(r=>r.Id==reviewId))return NotFoundJson("Không tìm thấy yêu cầu làm lại.");var a=new QuizRetakeService(Db).StartRetake(CurrentUserId,reviewId);string token=new QuizWriterService(Db).Acquire(a.Id,CurrentUserId,request.ClientKey,request.Takeover);return Ok(AttemptPayload(Quiz.Attempt(a.Id),token));}catch(QuizSessionException ex){return Fail(409,"QUIZ_SESSION_ACTIVE",ex.Message);}catch(InvalidOperationException ex){return Forbidden(ex.Message);}
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
            if (Quiz.Installed() && new LearningService(Db).DaGhiDanh(CurrentUserId, courseId))
                Quiz.EnsureCertificateWithoutQuiz(courseId, CurrentUserId);
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
