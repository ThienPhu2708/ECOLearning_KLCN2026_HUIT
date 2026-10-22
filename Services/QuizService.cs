using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using ECOLearning.Models;
using ECOLearning.ViewModels;
using Newtonsoft.Json;

namespace ECOLearning.Services
{
    public class QuizService
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
        public QuizService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 context) { db = context; }
        private static SqlParameter P(string name, object value)
        {
            return value is DateTime
                ? new SqlParameter(name, SqlDbType.DateTime2) { Value = value }
                : new SqlParameter(name, value ?? DBNull.Value);
        }
        public bool Installed() => db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.QuizAttempt') IS NULL THEN 0 ELSE 1 END").Single() == 1;
        public bool Owns(string course, int teacher) => db.KhoaHoc.Any(k => k.maKhoaHoc == course && k.maGiaoVien == teacher);
        public void RequireEnrollment(string course, int student)
        {
            var user = db.NguoiDung.Find(student);
            if (user == null || user.trangThai == false || !db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == course && d.maHocVien == student))
                throw new InvalidOperationException("Bạn chưa đăng ký khóa học này hoặc tài khoản không hoạt động.");
        }
        public QuizConfig Config(string course) => db.Database.SqlQuery<QuizConfig>("SELECT * FROM dbo.QuizConfig WHERE CourseId=@c", P("@c",course)).SingleOrDefault();
        public List<QuizQuestion> Questions(string course, bool activeOnly = false)
        {
            var rows = db.Database.SqlQuery<QuizQuestion>("SELECT * FROM dbo.QuizQuestion WHERE CourseId=@c" + (activeOnly ? " AND Active=1" : "") + " ORDER BY Topic,Difficulty,Id", P("@c",course)).ToList();
            rows.ForEach(q => q.LoadOptions()); return rows;
        }
        public static void ValidateQuestion(QuizQuestion q)
        {
            if (string.IsNullOrWhiteSpace(q.Topic) || q.Topic.Length>120 || string.IsNullOrWhiteSpace(q.Text) || q.Text.Length>4000 ||
                q.Difficulty<1 || q.Difficulty>3 || q.Options==null || q.Options.Length!=4 ||
                q.Options.Any(o => string.IsNullOrWhiteSpace(o) || o.Length>2000) || q.Options.Select(o => o.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=4 ||
                q.CorrectOption<0 || q.CorrectOption>3 || (q.Explanation?.Length ?? 0)>2000)
                throw new InvalidOperationException("Nhập chủ đề, nội dung, mức độ 1–3, bốn đáp án khác nhau và một đáp án đúng.");
        }
        public void SaveQuestion(QuizQuestion q)
        {
            ValidateQuestion(q);
            var args = new[] {P("@c",q.CourseId),P("@topic",q.Topic.Trim()),P("@d",q.Difficulty),P("@t",q.Text.Trim()),
                P("@o",JsonConvert.SerializeObject(q.Options.Select(o => o.Trim()))),P("@correct",q.CorrectOption),P("@e",q.Explanation),P("@shuffle",q.ShuffleOptions),P("@id",q.Id)};
            if(q.Id==0) db.Database.ExecuteSqlCommand("INSERT dbo.QuizQuestion(CourseId,Topic,Difficulty,Text,OptionsJson,CorrectOption,Explanation,ShuffleOptions) VALUES(@c,@topic,@d,@t,@o,@correct,@e,@shuffle)",args);
            else if(db.Database.ExecuteSqlCommand("UPDATE dbo.QuizQuestion SET Topic=@topic,Difficulty=@d,Text=@t,OptionsJson=@o,CorrectOption=@correct,Explanation=@e,ShuffleOptions=@shuffle WHERE Id=@id AND CourseId=@c AND Active=1",args)!=1)
                throw new InvalidOperationException("Không tìm thấy câu hỏi đang hoạt động.");
            RefreshPublication(q.CourseId);
        }
        public void Archive(string course,int id)
        {
            db.Database.ExecuteSqlCommand("UPDATE dbo.QuizQuestion SET Active=0 WHERE CourseId=@c AND Id=@id",P("@c",course),P("@id",id));
            RefreshPublication(course);
        }
        private void RefreshPublication(string course)
        {
            var config=Config(course);
            if(config?.Published!=true)return;
            try {ValidateConfig(config);}
            catch(InvalidOperationException) {
                // New attempts must not see a published quiz whose bank no longer meets its blueprint.
                db.Database.ExecuteSqlCommand("UPDATE dbo.QuizConfig SET Published=0 WHERE CourseId=@c",P("@c",course));
            }
        }
        private List<QuizQuestion> Pool(QuizConfig config)
        {
            var all=Questions(config.CourseId,true);
            if(config.RandomSelection) return all.Where(q => string.IsNullOrWhiteSpace(config.Topic) || q.Topic==config.Topic).ToList();
            var ids=JsonConvert.DeserializeObject<int[]>(config.SelectedIdsJson ?? "[]").Distinct().ToArray();
            var selected=all.Where(q => ids.Contains(q.Id)).ToList();
            if(selected.Count!=ids.Length) throw new InvalidOperationException("Một số câu đã chọn không còn hoạt động hoặc không thuộc khóa học.");
            return selected;
        }
        public void ValidateConfig(QuizConfig c)
        {
            if(string.IsNullOrWhiteSpace(c.Title) || c.Title.Length>200 || c.Minutes<1 || c.Minutes>180 || c.PassPercent<1 || c.PassPercent>100 ||
                c.MaxAttempts<1 || c.MaxAttempts>100 || c.EasyCount<0 || c.MediumCount<0 || c.HardCount<0 || c.Total<1 || c.Total>100 || (c.Topic?.Length ?? 0)>120)
                throw new InvalidOperationException("Kiểm tra tiêu đề, số câu (1–100), thời gian (1–180 phút), lượt làm và tỷ lệ đạt.");
            var pool=Pool(c); var counts=new[]{c.EasyCount,c.MediumCount,c.HardCount};
            for(int i=0;i<3;i++) {
                int available=pool.Count(q => q.Difficulty==i+1);
                if(available<counts[i] || (!c.RandomSelection && available!=counts[i]))
                    throw new InvalidOperationException($"Mức {(i==0 ? "Dễ" : i==1 ? "Trung bình" : "Khó")}: chọn {counts[i]} câu nhưng có {available} câu phù hợp. Đề cố định phải khớp cơ cấu.");
            }
        }
        public void SaveConfig(QuizConfig c)
        {
            if(c.Published) ValidateConfig(c);
            else {
                // Drafts may have an insufficient question bank, but must satisfy database constraints.
                if(string.IsNullOrWhiteSpace(c.Title) || c.Total<1 || c.Total>100 || c.EasyCount<0 || c.MediumCount<0 || c.HardCount<0 || c.Minutes<1 || c.Minutes>180 || c.PassPercent<1 || c.PassPercent>100 || c.MaxAttempts<1 || c.MaxAttempts>100)
                    throw new InvalidOperationException("Cấu hình chưa hợp lệ.");
            }
            db.Database.ExecuteSqlCommand(@"MERGE dbo.QuizConfig WITH(HOLDLOCK) AS t USING(SELECT @c AS CourseId) s ON t.CourseId=s.CourseId
WHEN MATCHED THEN UPDATE SET Title=@title,Minutes=@m,PassPercent=@p,MaxAttempts=@max,EasyCount=@easy,MediumCount=@medium,HardCount=@hard,Topic=@topic,RandomSelection=@random,SelectedIdsJson=@ids,ShuffleQuestions=@sq,ShuffleAnswers=@sa,Published=@pub
WHEN NOT MATCHED THEN INSERT(CourseId,Title,Minutes,PassPercent,MaxAttempts,EasyCount,MediumCount,HardCount,Topic,RandomSelection,SelectedIdsJson,ShuffleQuestions,ShuffleAnswers,Published)
VALUES(@c,@title,@m,@p,@max,@easy,@medium,@hard,@topic,@random,@ids,@sq,@sa,@pub);",
                P("@c",c.CourseId),P("@title",c.Title),P("@m",c.Minutes),P("@p",c.PassPercent),P("@max",c.MaxAttempts),P("@easy",c.EasyCount),P("@medium",c.MediumCount),P("@hard",c.HardCount),
                P("@topic",c.Topic),P("@random",c.RandomSelection),P("@ids",c.SelectedIdsJson),P("@sq",c.ShuffleQuestions),P("@sa",c.ShuffleAnswers),P("@pub",c.Published));
        }
        public void RenameTopic(string course,string oldTopic,string newTopic)
        {
            if(string.IsNullOrWhiteSpace(oldTopic) || string.IsNullOrWhiteSpace(newTopic) || newTopic.Trim().Length>120)
                throw new InvalidOperationException("Tên chủ đề phải có từ 1 đến 120 ký tự.");
            using(var tx=db.Database.BeginTransaction()) {
                db.Database.ExecuteSqlCommand("UPDATE dbo.QuizQuestion SET Topic=@new WHERE CourseId=@c AND Topic=@old; UPDATE dbo.QuizConfig SET Topic=@new WHERE CourseId=@c AND Topic=@old;",P("@new",newTopic.Trim()),P("@old",oldTopic),P("@c",course));
                tx.Commit();
            }
        }
        public List<QuizStudentRow> Students(int teacher, string course = null, string search = null, int? student = null)
        {
            var rows=db.Database.SqlQuery<QuizStudentRow>(@"
SELECT d.maHocVien StudentId,n.hoTen Name,n.email Email,k.maKhoaHoc CourseId,k.tenKhoaHoc CourseName,
 (SELECT COUNT(*) FROM dbo.BaiHoc b LEFT JOIN dbo.ChuongHoc ch ON ch.maChuong=b.maChuong WHERE ch.maKhoaHoc=k.maKhoaHoc OR (b.maChuong IS NULL AND LEFT(b.maBaiHoc,LEN(k.maKhoaHoc)+1)=k.maKhoaHoc+'_')) TotalLessons,
 (SELECT COUNT(DISTINCT b.maBaiHoc) FROM dbo.BaiHoc b LEFT JOIN dbo.ChuongHoc ch ON ch.maChuong=b.maChuong JOIN dbo.TienDoBaiHoc td ON td.maBaiHoc=b.maBaiHoc AND td.maHocVien=d.maHocVien AND td.daHoanThanh=1 WHERE ch.maKhoaHoc=k.maKhoaHoc OR (b.maChuong IS NULL AND LEFT(b.maBaiHoc,LEN(k.maKhoaHoc)+1)=k.maKhoaHoc+'_')) CompletedLessons,
 (SELECT COUNT(*) FROM dbo.QuizAttempt a WHERE a.CourseId=k.maKhoaHoc AND a.StudentId=d.maHocVien) Attempts,
 ISNULL((SELECT SUM(g.Quantity) FROM dbo.QuizAttemptGrant g WHERE g.CourseId=k.maKhoaHoc AND g.StudentId=d.maHocVien),0) ExtraAttempts,
 best.Score BestScore,best.Correct BestCorrect,best.Total BestTotal,
 CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.QuizAttempt a WHERE a.CourseId=k.maKhoaHoc AND a.StudentId=d.maHocVien AND a.Passed=1) THEN 1 ELSE 0 END AS BIT) Passed,
 CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.QuizAttempt a WHERE a.CourseId=k.maKhoaHoc AND a.StudentId=d.maHocVien AND a.SubmittedAt IS NULL) THEN 1 ELSE 0 END AS BIT) HasOpenAttempt,
 cert.RequestedAt,cert.IssuedAt,cert.Code
FROM dbo.DangKyKhoaHoc d JOIN dbo.KhoaHoc k ON k.maKhoaHoc=d.maKhoaHoc JOIN dbo.NguoiDung n ON n.maNguoiDung=d.maHocVien
OUTER APPLY(SELECT TOP 1 Score,Correct,Total FROM dbo.QuizAttempt a WHERE a.CourseId=k.maKhoaHoc AND a.StudentId=d.maHocVien AND a.SubmittedAt IS NOT NULL ORDER BY Score DESC,Id DESC) best
LEFT JOIN dbo.QuizCertificate cert ON cert.CourseId=k.maKhoaHoc AND cert.StudentId=d.maHocVien
WHERE k.maGiaoVien=@t AND (@c IS NULL OR k.maKhoaHoc=@c) AND (@student IS NULL OR d.maHocVien=@student) ORDER BY k.tenKhoaHoc,n.hoTen",P("@t",teacher),P("@c",string.IsNullOrEmpty(course)?null:course),P("@student",student)).ToList();
            if(db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.CourseCompletionProtection') IS NULL THEN 0 ELSE 1 END").Single()==1) {
                var protectedRows=new HashSet<string>(db.Database.SqlQuery<string>("SELECT CourseId+':'+CAST(StudentId AS VARCHAR(20)) FROM dbo.CourseCompletionProtection").ToList());
                foreach(var row in rows) row.CompletedBeforeRevision=protectedRows.Contains(row.CourseId+":"+row.StudentId);
            }
            if(!string.IsNullOrWhiteSpace(search)) rows=rows.Where(r=>r.Name.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0 || r.Email.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0).ToList();
            return rows;
        }
        public QuizStudentRow Student(string course,int student)
        {
            var teacher=db.KhoaHoc.Where(k=>k.maKhoaHoc==course).Select(k=>k.maGiaoVien).Single();
            return Students(teacher,course,null,student).SingleOrDefault();
        }
        public List<QuizStudentRow> Gradebook(int teacher,string course=null,string search=null)
        {
            var rows=Students(teacher,course,search);
            foreach(var row in rows.Where(r=>r.HasOpenAttempt))CloseExpired(row.CourseId,row.StudentId);
            return rows.Any(r=>r.HasOpenAttempt)?Students(teacher,course,search):rows;
        }
        public List<QuizAttemptGrant> Grants(string course,int student) => db.Database.SqlQuery<QuizAttemptGrant>("SELECT * FROM dbo.QuizAttemptGrant WHERE CourseId=@c AND StudentId=@s ORDER BY Id DESC",P("@c",course),P("@s",student)).ToList();
        public void GrantAttempt(string course,int student,int teacher,string reason)
        {
            if(!Owns(course,teacher))throw new InvalidOperationException("Không có quyền cấp lượt thi của khóa này.");
            if(string.IsNullOrWhiteSpace(reason) || reason.Trim().Length>400)throw new InvalidOperationException("Nhập lý do cấp lượt thi (tối đa 400 ký tự).");
            RequireEnrollment(course,student);CloseExpired(course,student);
            using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)) {
                Lock("Quiz_"+course+"_"+student);
                var state=Student(course,student);var config=Config(course);
                if(config==null || state==null || state.Passed || state.HasOpenAttempt || state.Attempts<config.MaxAttempts+state.ExtraAttempts)
                    throw new InvalidOperationException("Chỉ cấp thêm lượt cho học viên chưa đạt, đã hết lượt và không có bài đang làm.");
                if(config.MaxAttempts+state.ExtraAttempts>=100)throw new InvalidOperationException("Tối đa 100 lượt làm cho mỗi học viên trong khóa.");
                db.Database.ExecuteSqlCommand("INSERT dbo.QuizAttemptGrant(CourseId,StudentId,TeacherId,Reason,GrantedAt,Quantity) VALUES(@c,@s,@t,@reason,@now,1)",P("@c",course),P("@s",student),P("@t",teacher),P("@reason",reason.Trim()),P("@now",DateTime.UtcNow));
                tx.Commit();
            }
        }
        public List<QuizAttempt> Attempts(string course,int student) => db.Database.SqlQuery<QuizAttempt>("SELECT * FROM dbo.QuizAttempt WHERE CourseId=@c AND StudentId=@s ORDER BY Id DESC",P("@c",course),P("@s",student)).ToList();
        public QuizAttempt Attempt(int id) => db.Database.SqlQuery<QuizAttempt>("SELECT * FROM dbo.QuizAttempt WHERE Id=@id",P("@id",id)).SingleOrDefault();
        private void Lock(string resource)
        {
            db.Database.ExecuteSqlCommand(@"DECLARE @result INT; EXEC @result=sys.sp_getapplock @Resource=@r,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @result<0 THROW 51000,N'Không khóa được dữ liệu. Vui lòng thử lại.',1;",P("@r",resource));
        }
        private static void Shuffle<T>(IList<T> list)
        {
            using(var rng=RandomNumberGenerator.Create()) {
                var bytes=new byte[4];
                for(int i=list.Count-1;i>0;i--) { rng.GetBytes(bytes); int j=(int)(BitConverter.ToUInt32(bytes,0)%(uint)(i+1)); T v=list[i];list[i]=list[j];list[j]=v; }
            }
        }
        public QuizAttempt Start(string course,int student)
        {
            RequireEnrollment(course,student);
            CloseExpired(course,student);
            using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)) {
                Lock("Quiz_"+course+"_"+student);
                var active=Attempts(course,student).FirstOrDefault(a=>!a.SubmittedAt.HasValue);
                if(active!=null) { tx.Commit();return active; }
                var state=Student(course,student);
                if(state==null || !state.LessonsDone) throw new InvalidOperationException("Học đủ 100% bài học trước khi làm quiz.");
                if(state.Passed) throw new InvalidOperationException("Bạn đã đạt quiz của khóa học này.");
                var config=Config(course);
                if(config==null || !config.Published) throw new InvalidOperationException("Giáo viên chưa mở quiz cuối khóa.");
                if(state.Attempts>=config.MaxAttempts+state.ExtraAttempts) throw new InvalidOperationException("Bạn đã hết lượt làm. Liên hệ giáo viên để được xem xét cấp thêm lượt.");
                ValidateConfig(config);
                var pool=Pool(config);var selected=new List<QuizQuestion>();var counts=new[]{config.EasyCount,config.MediumCount,config.HardCount};
                for(int i=0;i<3;i++) {var group=pool.Where(q=>q.Difficulty==i+1).ToList();if(config.RandomSelection) Shuffle(group);selected.AddRange(group.Take(counts[i]));}
                if(config.ShuffleQuestions) Shuffle(selected);
                var snapshot=new List<QuizSnapshotQuestion>();
                foreach(var q in selected) {
                    var order=Enumerable.Range(0,4).ToList();if(config.ShuffleAnswers && q.ShuffleOptions) Shuffle(order);
                    snapshot.Add(new QuizSnapshotQuestion {Id=q.Id,Text=q.Text,Options=order.Select(i=>q.Options[i]).ToArray(),CorrectOption=order.IndexOf(q.CorrectOption),Explanation=q.Explanation,Difficulty=q.Difficulty,Topic=q.Topic});
                }
                var now=DateTime.UtcNow;
                int id=db.Database.SqlQuery<int>(@"INSERT dbo.QuizAttempt(CourseId,StudentId,StartedAt,ExpiresAt,SnapshotJson,Total,PassPercent,Title) VALUES(@c,@s,@now,@end,@json,@total,@pass,@title); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    P("@c",course),P("@s",student),P("@now",now),P("@end",now.AddMinutes(config.Minutes)),P("@json",JsonConvert.SerializeObject(snapshot)),P("@total",snapshot.Count),P("@pass",config.PassPercent),P("@title",config.Title)).Single();
                tx.Commit();return Attempt(id);
            }
        }
        private void Grade(QuizAttempt a,int[] answers)
        {
            var qs=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson);
            var normalized=Enumerable.Range(0,qs.Count).Select(i=>i<answers.Length && answers[i]>=0 && answers[i]<4 ? answers[i] : -1).ToArray();
            int correct=qs.Where((q,i)=>q.CorrectOption==normalized[i]).Count();
            decimal score=Math.Round(10m*correct/qs.Count,2);
            var now=DateTime.UtcNow;bool timedOut=now>=a.ExpiresAt;
            db.Database.ExecuteSqlCommand("UPDATE dbo.QuizAttempt SET SubmittedAt=@now,AnswersJson=@answers,Correct=@correct,Score=@score,Passed=@passed,TimedOut=@timeout WHERE Id=@id AND SubmittedAt IS NULL",
                P("@now",timedOut?a.ExpiresAt:now),P("@timeout",timedOut),P("@answers",JsonConvert.SerializeObject(normalized)),P("@correct",correct),P("@score",score),P("@passed",100m*correct>=a.PassPercent*qs.Count),P("@id",a.Id));
        }
        public void Submit(int id,int student,int[] answers)
        {
            var original=Attempt(id);
            if(original==null || original.StudentId!=student) throw new InvalidOperationException("Không tìm thấy lượt làm.");
            RequireEnrollment(original.CourseId,student);
            using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)) {
                // Acquire the application lock before taking row read locks: two simultaneous
                // submits must not deadlock while one waits to update the other's shared row.
                Lock("Quiz_"+original.CourseId+"_"+student);
                var a=Attempt(id);
                if(!a.SubmittedAt.HasValue) Grade(a,DateTime.UtcNow>a.ExpiresAt.AddSeconds(5) ? JsonConvert.DeserializeObject<int[]>(a.AnswersJson ?? "[]") : answers ?? new int[0]);
                tx.Commit();
            }
        }
        public bool SaveDraft(int id,int student,int[] answers)
        {
            var original=Attempt(id);
            if(original==null || original.StudentId!=student)throw new InvalidOperationException("Không tìm thấy lượt làm.");
            RequireEnrollment(original.CourseId,student);
            using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)) {
                Lock("Quiz_"+original.CourseId+"_"+student);var a=Attempt(id);
                if(a.SubmittedAt.HasValue || DateTime.UtcNow>a.ExpiresAt){tx.Commit();return false;}
                var normalized=Enumerable.Range(0,a.Total).Select(i=>answers!=null && i<answers.Length && answers[i]>=0 && answers[i]<4?answers[i]:-1).ToArray();
                db.Database.ExecuteSqlCommand("UPDATE dbo.QuizAttempt SET AnswersJson=@json WHERE Id=@id AND SubmittedAt IS NULL",P("@json",JsonConvert.SerializeObject(normalized)),P("@id",id));
                tx.Commit();return true;
            }
        }
        public void CloseExpired(string course,int student)
        {
            // Finalization must also work for a learner whose account was subsequently disabled.
            // Ownership/access checks belong to the invoking instructor or learner controller.
            using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)) {
                Lock("Quiz_"+course+"_"+student);
                var expired=Attempts(course,student).FirstOrDefault(a=>!a.SubmittedAt.HasValue && a.ExpiresAt.AddSeconds(5)<DateTime.UtcNow);
                if(expired!=null)Grade(expired,JsonConvert.DeserializeObject<int[]>(expired.AnswersJson ?? "[]"));
                tx.Commit();
            }
        }
        public void ClaimCertificate(string course,int student)
        {
            RequireEnrollment(course,student);
            using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)) {
                Lock("Quiz_"+course+"_"+student);
                var state=Student(course,student);
                if(state?.IssuedAt.HasValue==true){tx.Commit();return;}
                if(state==null || !state.LessonsDone || !state.Passed)
                    throw new InvalidOperationException("Cần học đủ bài và đạt ngưỡng quiz của khóa trước khi nhận chứng chỉ.");
                // Claiming is the issuance event. Upgrade legacy pending records without changing their code.
                db.Database.ExecuteSqlCommand(@"IF EXISTS(SELECT 1 FROM dbo.QuizCertificate WHERE CourseId=@c AND StudentId=@s)
UPDATE dbo.QuizCertificate SET IssuedAt=@now,IssuedBy=NULL WHERE CourseId=@c AND StudentId=@s AND IssuedAt IS NULL;
ELSE INSERT dbo.QuizCertificate(CourseId,StudentId,RequestedAt,IssuedAt,IssuedBy,Code) VALUES(@c,@s,@now,@now,NULL,@code);",
                    P("@c",course),P("@s",student),P("@now",DateTime.UtcNow),P("@code","CERT-"+Guid.NewGuid().ToString("N")));
                tx.Commit();
            }
        }
    }
}
