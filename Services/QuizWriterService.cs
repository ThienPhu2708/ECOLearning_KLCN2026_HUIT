using System;
using System.Data;
using System.Linq;
using ECOLearning.Models;
namespace ECOLearning.Services {
 public class QuizSessionException:InvalidOperationException {public QuizSessionException():base("Lượt thi đang mở ở phiên khác. Chuyển sang đây sẽ ngừng quyền sửa ở phiên cũ."){} }
 public class QuizWriterRow {public string ClientKey{get;set;}public string Token{get;set;} }
 public class QuizWriterService {
  readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
  public QuizWriterService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 context){db=context;}
  void Ready(){if(db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.QuizWriter','U') IS NULL THEN 0 ELSE 1 END").Single()!=1)throw new InvalidOperationException("Cần cập nhật migration 026 để bảo vệ lượt thi.");}
  public string Acquire(int attempt,int student,string client,bool takeover){
   Ready();Guid guid;if(!Guid.TryParse(client,out guid))throw new InvalidOperationException("Phiên làm bài không hợp lệ. Cập nhật ứng dụng rồi thử lại.");
   client=guid.ToString("N");var quiz=new QuizService(db);var a=quiz.Attempt(attempt);
   if(a==null||a.StudentId!=student)throw new InvalidOperationException("Không tìm thấy lượt thi.");quiz.RequireEnrollment(a.CourseId,student);
   using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)){
    db.Database.ExecuteSqlCommand("DECLARE @r INT;EXEC @r=sys.sp_getapplock @Resource=@p0,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;IF @r<0 THROW 51000,'Quiz lock failed',1;","Quiz_"+a.CourseId+"_"+student);
    a=quiz.Attempt(attempt);if(a.SubmittedAt.HasValue||DateTime.UtcNow>=a.ExpiresAt)throw new InvalidOperationException("Lượt thi đã kết thúc.");
    var row=db.Database.SqlQuery<QuizWriterRow>("SELECT ClientKey,Token FROM dbo.QuizWriter WITH(UPDLOCK,HOLDLOCK) WHERE AttemptId=@p0",attempt).SingleOrDefault();
    if(row!=null&&row.ClientKey!=client&&!takeover)throw new QuizSessionException();
    var token=row!=null&&row.ClientKey==client?row.Token:Guid.NewGuid().ToString("N");
    if(row==null)db.Database.ExecuteSqlCommand("INSERT dbo.QuizWriter(AttemptId,ClientKey,Token) VALUES(@p0,@p1,@p2)",attempt,client,token);
    else db.Database.ExecuteSqlCommand("UPDATE dbo.QuizWriter SET ClientKey=@p1,Token=@p2,UpdatedAt=GETUTCDATE() WHERE AttemptId=@p0",attempt,client,token);
    tx.Commit();return token;
   }
  }
  // Called under the same quiz application lock as acquiring/changing the writer.
  public void Require(int attempt,string token){Ready();if(string.IsNullOrEmpty(token)||!db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.QuizWriter WHERE AttemptId=@p0 AND Token=@p1",attempt,token).Any(x=>x==1))throw new QuizSessionException();}
  public bool Active(int student){return student>0&&db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.QuizAttempt WHERE StudentId=@p0 AND SubmittedAt IS NULL AND ExpiresAt>GETUTCDATE()",student).Single()>0;}
 }
}
