using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;
namespace ECOLearning.Services {
 public class QuizVisibilityRow {
  public int Id {get;set;}
  public DateTime LeftAt {get;set;} public DateTime? ReturnedAt {get;set;}
  public int Seconds {get;set;}
 }
 public class QuizVisibilityCount {public int AttemptId {get;set;} public int Count {get;set;}}
 public class QuizVisibilityAlert {
  public int AttemptId {get;set;}public int StudentId {get;set;}public string StudentName {get;set;}public string CourseId {get;set;}public string CourseName {get;set;}
  public int Count {get;set;}public int Seconds {get;set;}public int LastEventId {get;set;}public bool IsUnread {get;set;}public DateTime LastLeftAt {get;set;}public DateTime? SubmittedAt {get;set;}public bool PendingReview {get;set;}
 }
 public class QuizVisibilityService {
  readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
  public QuizVisibilityService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 context){db=context;}
  bool Ready(){return db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.QuizVisibility','U') IS NULL THEN 0 ELSE 1 END").Single()==1;}
  public void Record(int student,int attempt,string key,bool returned){
   if(!Ready())throw new InvalidOperationException("Chưa cài đặt nhật ký rời màn hình (migration 025).");
   Guid parsed;if(!Guid.TryParse(key,out parsed))throw new InvalidOperationException("Mã sự kiện không hợp lệ.");
   key=parsed.ToString("N");var quiz=new QuizService(db);var original=quiz.Attempt(attempt);
   if(original==null||original.StudentId!=student)throw new InvalidOperationException("Không tìm thấy lượt thi của bạn.");
   quiz.RequireEnrollment(original.CourseId,student);
   using(var tx=db.Database.CurrentTransaction==null?db.Database.BeginTransaction():null){
    db.Database.ExecuteSqlCommand("DECLARE @r int; EXEC @r=sp_getapplock @Resource=@p0,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Quiz visibility lock timeout',1;","Quiz_"+original.CourseId+"_"+student);
    var a=quiz.Attempt(attempt);var now=DateTime.UtcNow;
    var end=a.SubmittedAt.HasValue&&a.SubmittedAt.Value<a.ExpiresAt?a.SubmittedAt.Value:a.ExpiresAt;
    if(returned){db.Database.ExecuteSqlCommand("UPDATE dbo.QuizVisibility SET ReturnedAt=CASE WHEN @now<@end THEN @now ELSE @end END WHERE AttemptId=@a AND EventKey=@key AND ReturnedAt IS NULL",new SqlParameter("@now",now),new SqlParameter("@end",end),new SqlParameter("@a",attempt),new SqlParameter("@key",key));}
    else if(!a.SubmittedAt.HasValue&&now<a.ExpiresAt){
     db.Database.ExecuteSqlCommand(@"IF NOT EXISTS(SELECT 1 FROM dbo.QuizVisibility WITH(UPDLOCK,HOLDLOCK) WHERE AttemptId=@a AND EventKey=@key) AND (SELECT COUNT(*) FROM dbo.QuizVisibility WHERE AttemptId=@a)<1000 INSERT dbo.QuizVisibility(AttemptId,EventKey,LeftAt) VALUES(@a,@key,@now)",new SqlParameter("@a",attempt),new SqlParameter("@key",key),new SqlParameter("@now",now));
    }
    tx?.Commit();
   }
  }
  public void ApproveResult(int teacher,int attempt,string note){
   note=(note??"").Trim();if(note.Length==0||note.Length>2000)throw new InvalidOperationException("Nhập lý do công nhận từ 1 đến 2000 ký tự.");
   var quiz=new QuizService(db);var original=quiz.Attempt(attempt);
   if(original==null||!quiz.Owns(original.CourseId,teacher))throw new InvalidOperationException("Không có quyền xét kết quả lượt thi này.");
   using(var tx=db.Database.BeginTransaction()){
    db.Database.ExecuteSqlCommand("DECLARE @r int; EXEC @r=sp_getapplock @Resource=@p0,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Quiz review lock timeout',1;","Quiz_"+original.CourseId+"_"+original.StudentId);
    var a=quiz.Attempt(attempt);
    if(a.IsVoided||!a.ReviewPending)throw new InvalidOperationException("Lượt thi không còn chờ xem xét hoặc đã được yêu cầu làm lại.");
    db.Database.ExecuteSqlCommand("UPDATE dbo.QuizAttempt SET ReviewApprovedAt=GETUTCDATE(),ReviewApprovedBy=@p0,ReviewNote=@p1 WHERE Id=@p2 AND ReviewApprovedAt IS NULL",teacher,note,attempt);
    tx.Commit();
   }
  }
  bool AlertsReady(){return Ready()&&db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.QuizVisibilitySeen','U') IS NULL THEN 0 ELSE 1 END").Single()==1;}
  public int UnreadCount(int teacher) {
   if(!AlertsReady())return 0;
   return db.Database.SqlQuery<int>(@"SELECT COUNT(*) FROM (SELECT v.AttemptId FROM dbo.QuizVisibility v JOIN dbo.QuizAttempt a ON a.Id=v.AttemptId JOIN dbo.KhoaHoc k ON k.maKhoaHoc=a.CourseId LEFT JOIN dbo.QuizVisibilitySeen s ON s.AttemptId=a.Id AND s.TeacherId=@p0 WHERE k.maGiaoVien=@p0 GROUP BY v.AttemptId HAVING COUNT(*)>=3 AND (MAX(v.Id)>ISNULL(MAX(s.SeenEventId),0) OR (MAX(CAST(a.ReviewRequired AS INT))=1 AND MAX(a.SubmittedAt) IS NOT NULL AND MAX(a.ReviewApprovedAt) IS NULL AND EXISTS(SELECT 1 FROM dbo.QuizEffectiveAttempt e WHERE e.Id=v.AttemptId)))) flags",teacher).Single();
  }
  public List<QuizVisibilityAlert> Alerts(int teacher,string course=null,string search=null,bool includeSeen=false) {
   if(!AlertsReady())return new List<QuizVisibilityAlert>();
   var rows=db.Database.SqlQuery<QuizVisibilityAlert>(@"SELECT a.Id AttemptId,a.StudentId,n.hoTen StudentName,a.CourseId,k.tenKhoaHoc CourseName,COUNT(*) Count,MAX(v.Id) LastEventId,MAX(v.LeftAt) LastLeftAt,a.SubmittedAt,CAST(CASE WHEN a.SubmittedAt IS NOT NULL AND a.ReviewRequired=1 AND a.ReviewApprovedAt IS NULL AND EXISTS(SELECT 1 FROM dbo.QuizEffectiveAttempt e WHERE e.Id=a.Id) THEN 1 ELSE 0 END AS BIT) PendingReview,
CAST(CASE WHEN MAX(v.Id)>ISNULL(MAX(s.SeenEventId),0) THEN 1 ELSE 0 END AS BIT) IsUnread,
SUM(CASE WHEN ends.UntilAt<v.LeftAt THEN 0 ELSE DATEDIFF(second,v.LeftAt,ends.UntilAt) END) Seconds
FROM dbo.QuizVisibility v JOIN dbo.QuizAttempt a ON a.Id=v.AttemptId JOIN dbo.KhoaHoc k ON k.maKhoaHoc=a.CourseId JOIN dbo.NguoiDung n ON n.maNguoiDung=a.StudentId
LEFT JOIN dbo.QuizVisibilitySeen s ON s.AttemptId=a.Id AND s.TeacherId=@p0
CROSS APPLY(SELECT CASE WHEN a.ExpiresAt<COALESCE(a.SubmittedAt,GETUTCDATE()) THEN a.ExpiresAt ELSE COALESCE(a.SubmittedAt,GETUTCDATE()) END EndAt) cutoff
CROSS APPLY(SELECT CASE WHEN v.ReturnedAt IS NOT NULL AND v.ReturnedAt<cutoff.EndAt THEN v.ReturnedAt ELSE cutoff.EndAt END UntilAt) ends
WHERE k.maGiaoVien=@p0 AND (@p1 IS NULL OR a.CourseId=@p1)
GROUP BY a.Id,a.StudentId,n.hoTen,a.CourseId,k.tenKhoaHoc,a.SubmittedAt,a.ReviewRequired,a.ReviewApprovedAt
HAVING COUNT(*)>=3 ORDER BY IsUnread DESC,LastLeftAt DESC",new SqlParameter("@p0",teacher),new SqlParameter("@p1",string.IsNullOrWhiteSpace(course)?(object)DBNull.Value:course)).ToList();
   if(!includeSeen)rows=rows.Where(r=>r.IsUnread||r.PendingReview).ToList();
   if(!string.IsNullOrWhiteSpace(search))rows=rows.Where(r=>r.StudentName.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0||r.CourseName.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0).ToList();
   return rows;
  }
  public void MarkReviewed(int teacher,int attempt,int seenEventId) {
   if(seenEventId<=0||!AlertsReady())return;
   var quiz=new QuizService(db);var a=quiz.Attempt(attempt);
   if(a==null||!quiz.Owns(a.CourseId,teacher))throw new InvalidOperationException("Không có quyền xem nhật ký lượt thi này.");
   if(db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.QuizVisibility WHERE Id=@p0 AND AttemptId=@p1",seenEventId,attempt).Single()!=1)throw new InvalidOperationException("Mốc nhật ký không thuộc lượt thi này.");
   db.Database.ExecuteSqlCommand(@"MERGE dbo.QuizVisibilitySeen WITH(HOLDLOCK) AS t USING(SELECT @p0 TeacherId,@p1 AttemptId) s ON s.TeacherId=t.TeacherId AND s.AttemptId=t.AttemptId
WHEN MATCHED THEN UPDATE SET SeenEventId=CASE WHEN t.SeenEventId<@p2 THEN @p2 ELSE t.SeenEventId END,SeenAt=GETUTCDATE()
WHEN NOT MATCHED THEN INSERT(TeacherId,AttemptId,SeenEventId) VALUES(@p0,@p1,@p2);",teacher,attempt,seenEventId);
  }
  public Dictionary<int,int> CountsForTeacher(int teacher,string course,int student) {
   if(!Ready())return new Dictionary<int,int>();
   return db.Database.SqlQuery<QuizVisibilityCount>("SELECT v.AttemptId,COUNT(*) AS Count FROM dbo.QuizVisibility v JOIN dbo.QuizAttempt a ON a.Id=v.AttemptId JOIN dbo.KhoaHoc k ON k.maKhoaHoc=a.CourseId WHERE k.maGiaoVien=@p0 AND a.CourseId=@p1 AND a.StudentId=@p2 GROUP BY v.AttemptId",teacher,course,student).ToDictionary(x=>x.AttemptId,x=>x.Count);
  }
  public List<QuizVisibilityRow> ForTeacher(int teacher,int attempt){
   var a=new QuizService(db).Attempt(attempt);
   if(a==null || !new QuizService(db).Owns(a.CourseId,teacher))throw new InvalidOperationException("Không có quyền xem lượt thi này.");
   if(!Ready())return new List<QuizVisibilityRow>();
   var rows=db.Database.SqlQuery<QuizVisibilityRow>("SELECT Id,LeftAt,ReturnedAt,0 Seconds FROM dbo.QuizVisibility WHERE AttemptId=@p0 ORDER BY LeftAt",attempt).ToList();
   var end=a.SubmittedAt.HasValue?a.SubmittedAt.Value:DateTime.UtcNow;if(end>a.ExpiresAt)end=a.ExpiresAt;
   foreach(var row in rows){var until=row.ReturnedAt??end;if(until>end)until=end;row.Seconds=(int)Math.Max(0,(until-row.LeftAt).TotalSeconds);}
   return rows;
  }
 }
}
