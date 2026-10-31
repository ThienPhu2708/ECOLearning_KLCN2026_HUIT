using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    public class QuizRetakeEvent
    {
        public int Id { get; set; }
        public int ReviewId { get; set; }
        public int AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string Kind { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public string KindText => Kind == "RetakeStarted" ? "Bắt đầu làm lại" : "Yêu cầu làm lại của giáo viên";
    }
    public class QuizRetakeRequest
    {
        public int Id { get; set; }
        public int AttemptId { get; set; }
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public int StudentId { get; set; }
        public decimal? OriginalScore { get; set; }
        public string StudentName { get; set; }
        public string Status { get; set; }
        public string Followup { get; set; }
        public int? RetakeAttemptId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<QuizRetakeEvent> Events { get; set; } = new List<QuizRetakeEvent>();
        public QuizAttempt Retake { get; set; }
        public bool CanRetake => Followup == "Retake" && !RetakeAttemptId.HasValue;
        public string StatusText => Retake == null ? "Chờ học viên làm lại" : Retake.SubmittedAt.HasValue ? "Đã làm lại" : "Đang làm lại";

    }
    public class QuizRetakeService
    {
        readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
        public QuizRetakeService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 context) { db = context; }
        public bool Installed() => db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.QuizIntegrityReview','U') IS NOT NULL THEN 1 ELSE 0 END").Single() == 1;
        void Ready() { if (!Installed()) throw new InvalidOperationException("Cần chạy migration 028 để dùng chức năng yêu cầu làm lại."); }
        static string Text(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || text.Length > 2000) throw new InvalidOperationException("Nhập nội dung từ 1 đến 2000 ký tự.");
            return text;
        }
        void Lock(string key)
        {
            db.Database.ExecuteSqlCommand("DECLARE @r int; EXEC @r=sp_getapplock @Resource=@p0,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Integrity review lock timeout',1;", key);
        }
        QuizAttempt TeacherAttempt(int teacher, int attempt)
        {
            var quiz = new QuizService(db); var a = quiz.Attempt(attempt);
            if (a == null || !quiz.Owns(a.CourseId, teacher)) throw new InvalidOperationException("Không có quyền xem xét lượt thi này.");
            if (!a.SubmittedAt.HasValue) throw new InvalidOperationException("Chờ học viên nộp bài trước khi yêu cầu làm lại.");
            return a;
        }
        const string Query = @"SELECT r.*,a.CourseId,a.StudentId,a.Score OriginalScore,k.tenKhoaHoc CourseName,n.hoTen StudentName
FROM dbo.QuizIntegrityReview r JOIN dbo.QuizAttempt a ON a.Id=r.AttemptId JOIN dbo.KhoaHoc k ON k.maKhoaHoc=a.CourseId JOIN dbo.NguoiDung n ON n.maNguoiDung=a.StudentId ";
        void Load(QuizRetakeRequest review)
        {
            review.Events = db.Database.SqlQuery<QuizRetakeEvent>("SELECT e.*,n.hoTen AuthorName FROM dbo.QuizIntegrityEvent e JOIN dbo.NguoiDung n ON n.maNguoiDung=e.AuthorId WHERE e.ReviewId=@p0 AND e.Kind IN ('Decision','RetakeStarted') ORDER BY e.Id", review.Id).ToList();
            if (review.RetakeAttemptId.HasValue) review.Retake = new QuizService(db).Attempt(review.RetakeAttemptId.Value);
        }
        public QuizRetakeRequest ForTeacher(int teacher, int attempt)
        {
            var a = new QuizService(db).Attempt(attempt);
            if (a == null || !new QuizService(db).Owns(a.CourseId, teacher)) throw new InvalidOperationException("Không có quyền xem xét lượt thi này.");
            if (!Installed()) return null;
            var row = db.Database.SqlQuery<QuizRetakeRequest>(Query + "WHERE r.AttemptId=@p0", attempt).SingleOrDefault();
            if (row != null) Load(row); return row;
        }
        public List<QuizRetakeRequest> ForTeacherList(int teacher,string course=null)
        {
            if(!Installed())return new List<QuizRetakeRequest>();
            var rows=db.Database.SqlQuery<QuizRetakeRequest>(Query+"WHERE k.maGiaoVien=@p0 AND (@p1 IS NULL OR a.CourseId=@p1) ORDER BY r.UpdatedAt DESC",new System.Data.SqlClient.SqlParameter("@p0",teacher),new System.Data.SqlClient.SqlParameter("@p1",(object)course??DBNull.Value)).ToList();
            foreach(var row in rows)Load(row);return rows;
        }
        public void MarkSeen(int teacher,int attempt) {
            if(!Installed())return;
            db.Database.ExecuteSqlCommand("UPDATE r SET Status='NeedReview' FROM dbo.QuizIntegrityReview r JOIN dbo.QuizAttempt a ON a.Id=r.AttemptId JOIN dbo.KhoaHoc k ON k.maKhoaHoc=a.CourseId WHERE r.AttemptId=@p0 AND k.maGiaoVien=@p1 AND r.Status='AwaitingTeacher' AND r.Followup='Retake'",attempt,teacher);
        }
        public void RetakeSubmitted(int attempt)
        {
            if(!Installed())return;
            db.Database.ExecuteSqlCommand("UPDATE dbo.QuizIntegrityReview SET Status='AwaitingTeacher',UpdatedAt=GETUTCDATE() WHERE RetakeAttemptId=@p0 AND Followup='Retake'",attempt);
        }
        public List<QuizRetakeRequest> ForStudent(int student, string course)
        {
            new QuizService(db).RequireEnrollment(course, student);
            if (!Installed()) return new List<QuizRetakeRequest>();
            var rows = db.Database.SqlQuery<QuizRetakeRequest>(Query + "WHERE a.StudentId=@p0 AND a.CourseId=@p1 ORDER BY r.UpdatedAt DESC", student, course).ToList();
            foreach (var row in rows) Load(row); return rows;
        }
        int Ensure(int attempt)
        {
            return db.Database.SqlQuery<int>(@"IF NOT EXISTS(SELECT 1 FROM dbo.QuizIntegrityReview WHERE AttemptId=@p0) INSERT dbo.QuizIntegrityReview(AttemptId,Status) VALUES(@p0,'NeedReview'); SELECT Id FROM dbo.QuizIntegrityReview WHERE AttemptId=@p0;", attempt).Single();
        }
        int Event(int review, int author, string kind, string content)
        {
            return db.Database.SqlQuery<int>("INSERT dbo.QuizIntegrityEvent(ReviewId,AuthorId,Kind,Content) VALUES(@p0,@p1,@p2,@p3);SELECT CAST(SCOPE_IDENTITY() AS INT);", new System.Data.SqlClient.SqlParameter("@p0",review),new System.Data.SqlClient.SqlParameter("@p1",author),new System.Data.SqlClient.SqlParameter("@p2",kind),new System.Data.SqlClient.SqlParameter("@p3",content)).Single();
        }
        public void RequestRetake(int teacher,int attempt,string content)
        {
            Ready();var a=TeacherAttempt(teacher,attempt);content=Text(content);
            if(a.IsVoided)throw new InvalidOperationException("Kết quả này đã hết hiệu lực do yêu cầu làm lại trước đó.");
            if(a.IsVerification)throw new InvalidOperationException("Lượt làm lại chỉ được thực hiện một lần theo yêu cầu này.");
            using(var tx=db.Database.BeginTransaction()) {
                Lock("Quiz_"+a.CourseId+"_"+a.StudentId);
                Lock("QuizIntegrity_"+attempt);
                if(TeacherAttempt(teacher,attempt).IsVoided)throw new InvalidOperationException("Kết quả này đã hết hiệu lực.");
                if(new QuizService(db).Attempts(a.CourseId,a.StudentId).Any(x=>!x.SubmittedAt.HasValue))throw new InvalidOperationException("Chờ học viên nộp lượt đang làm trước khi yêu cầu làm lại.");
                var review=ForTeacher(teacher,attempt);
                if(review!=null&&(review.Followup=="Retake"||review.RetakeAttemptId.HasValue))throw new InvalidOperationException("Lượt thi này đã được yêu cầu làm lại. Theo dõi kết quả bên dưới.");
                var quiz=new QuizService(db);var config=quiz.Config(a.CourseId);
                if(config==null||!config.Published)throw new InvalidOperationException("Mở quiz với cấu hình hợp lệ trước khi yêu cầu học viên làm lại.");
                quiz.ValidateConfig(config);
                int id=review?.Id??Ensure(attempt);
                Event(id,teacher,"Decision",content);
                db.Database.ExecuteSqlCommand("UPDATE dbo.QuizIntegrityReview SET Status='NeedReview',Followup='Retake',UpdatedAt=GETUTCDATE() WHERE Id=@p0",id);
                db.Database.ExecuteSqlCommand(@"INSERT dbo.QuizCertificateRevocation(ReviewId,CourseId,StudentId,Code,IssuedAt) SELECT @p0,CourseId,StudentId,Code,IssuedAt FROM dbo.QuizCertificate WHERE CourseId=@p1 AND StudentId=@p2 AND IssuedAt IS NOT NULL;
UPDATE dbo.QuizCertificate SET IssuedAt=NULL,IssuedBy=NULL WHERE CourseId=@p1 AND StudentId=@p2;",id,a.CourseId,a.StudentId);
                tx.Commit();
            }
        }
        public QuizAttempt StartRetake(int student, int reviewId)
        {
            Ready(); var quiz = new QuizService(db);
            var head = db.Database.SqlQuery<QuizRetakeRequest>(Query + "WHERE r.Id=@p0 AND a.StudentId=@p1", reviewId, student).SingleOrDefault();
            if (head == null) throw new InvalidOperationException("Không tìm thấy yêu cầu làm lại của bạn.");
            quiz.RequireEnrollment(head.CourseId, student); quiz.CloseExpired(head.CourseId, student);
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                Lock("Quiz_" + head.CourseId + "_" + student);
                Lock("QuizIntegrity_" + head.AttemptId);
                var review = ForStudent(student, head.CourseId).Single(x => x.Id == reviewId);
                if (review.RetakeAttemptId.HasValue)
                {
                    if (review.Retake.SubmittedAt.HasValue) throw new InvalidOperationException("Lượt làm lại đã kết thúc. Xem kết quả mới tại trang quiz.");
                    tx.Commit(); return review.Retake;
                }
                if (!review.CanRetake) throw new InvalidOperationException("Giáo viên chưa yêu cầu làm lại lượt thi này.");
                if (quiz.Attempts(head.CourseId, student).Any(a => !a.SubmittedAt.HasValue)) throw new InvalidOperationException("Hoàn thành lượt thi đang mở trước khi làm lại.");
                var config = quiz.Config(head.CourseId);
                if (config == null || !config.Published) throw new InvalidOperationException("Quiz chưa mở. Liên hệ giáo viên để mở bài làm lại.");
                quiz.ValidateConfig(config);
                var attempt = quiz.CreateAttempt(config, student, true);
                db.Database.ExecuteSqlCommand("UPDATE dbo.QuizIntegrityReview SET RetakeAttemptId=@p0,UpdatedAt=GETUTCDATE() WHERE Id=@p1", attempt.Id, review.Id);
                Event(review.Id, student, "RetakeStarted", "Bắt đầu lượt làm lại #" + attempt.Id + ". Kết quả lượt này thay thế kết quả cũ và được dùng để xét cấp chứng chỉ.");
                tx.Commit(); return attempt;
            }
        }
    }
}
