using System;
using System.Collections.Generic;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;
using Newtonsoft.Json;
namespace ECOLearning.Services {
 public class QuizRegradePlan {public string Id{get;set;}public int TeacherId{get;set;}public string CourseId{get;set;}public int ReportId{get;set;}public string PayloadJson{get;set;}public string StateHash{get;set;}public DateTime ExpiresAt{get;set;}public DateTime? AppliedAt{get;set;} public QuizCorrection Rule=>JsonConvert.DeserializeObject<QuizCorrection>(PayloadJson);public List<QuizRegradeLine> Lines{get;set;}public int OpenAttempts{get;set;}}
 public class QuizRegradeLine {public QuizAttempt Attempt;public QuizGrade Grade;public string StudentName;}
 public class QuizGradeAudit {public int Id{get;set;}public int AttemptId{get;set;}public decimal OldScore{get;set;}public decimal CalculatedScore{get;set;}public decimal RecognizedScore{get;set;}public bool OldPassed{get;set;}public bool RecognizedPassed{get;set;}public DateTime CreatedAt{get;set;}public string Reason{get;set;}public string CourseName{get;set;}public string TeacherName{get;set;}public int CorrectionId{get;set;}}
 public class QuizAppeal {public int Id{get;set;}public int AttemptId{get;set;}public int StudentId{get;set;}public string Content{get;set;}public string Status{get;set;}public DateTime CreatedAt{get;set;}public string CourseName{get;set;}public string CourseId{get;set;}public string StudentName{get;set;}public List<QuizAppealReply> Replies{get;set;}public List<QuizSnapshotQuestion> Questions{get;set;} }
 public class QuizAppealReply {public string Content{get;set;}public string Status{get;set;}public DateTime CreatedAt{get;set;}public string TeacherName{get;set;}}
 public class QuizRegradeService {
  readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
  public QuizRegradeService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 context){db=context;}
  void Ready(){if(!new QuizAssessmentService(db).Installed())throw new InvalidOperationException("Cần chạy migration 027.");}
  void Own(string course,int teacher){if(!new QuizService(db).Owns(course,teacher))throw new InvalidOperationException("Bạn không phụ trách khóa học này.");}
  static string Content(string value){value=(value??"").Trim();if(value.Length==0||value.Length>2000)throw new InvalidOperationException("Nội dung cần từ 1 đến 2000 ký tự.");return value;}
  List<QuizAttempt> Attempts(string course){return db.Database.SqlQuery<QuizAttempt>("SELECT * FROM dbo.QuizAttempt WHERE CourseId=@p0 ORDER BY Id",course).ToList();}
  static bool Matches(QuizAttempt a,QuizCorrection rule){return JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson).Any(q=>q.Id==rule.QuestionId&&QuizAssessmentService.Version(q)==rule.VersionKey);}
  string State(string course){return QuizAssessmentService.Hash(JsonConvert.SerializeObject(new{Attempts=Attempts(course).Where(a=>a.SubmittedAt.HasValue).Select(a=>new{a.Id,a.SnapshotJson,a.AnswersJson,a.Score,a.Passed,a.Correct,a.SkillResultsJson}),Rules=new QuizAssessmentService(db).Corrections(course)}));}
  void Fill(QuizRegradePlan plan){var rules=new QuizAssessmentService(db).Corrections(plan.CourseId);var proposed=plan.Rule;if(rules.Any(r=>r.QuestionId==proposed.QuestionId&&r.VersionKey==proposed.VersionKey))throw new InvalidOperationException("Phiên bản câu hỏi này đã được điều chỉnh. Xem lịch sử thay vì xác nhận lại.");rules.Add(proposed);var attempts=Attempts(plan.CourseId).Where(a=>Matches(a,proposed)).ToList();plan.OpenAttempts=attempts.Count(a=>!a.SubmittedAt.HasValue);var studentIds=attempts.Select(a=>a.StudentId).Distinct().ToList();var names=db.NguoiDung.Where(u=>studentIds.Contains(u.maNguoiDung)).Select(u=>new{u.maNguoiDung,u.hoTen}).ToDictionary(x=>x.maNguoiDung,x=>x.hoTen);plan.Lines=attempts.Where(a=>a.SubmittedAt.HasValue).Select(a=>new QuizRegradeLine{Attempt=a,Grade=QuizAssessmentService.Evaluate(a,rules),StudentName=names.ContainsKey(a.StudentId)?names[a.StudentId]:"Học viên"}).ToList();}
  public QuizRegradePlan Preview(int teacher,int reportId,int[] accepted,bool allCredit,string reason){
   Ready();var report=db.Database.SqlQuery<QuizQuestionReport>("SELECT r.*,k.tenKhoaHoc CourseName,n.hoTen StudentName FROM dbo.QuizQuestionReport r JOIN dbo.KhoaHoc k ON k.maKhoaHoc=r.CourseId JOIN dbo.NguoiDung n ON n.maNguoiDung=r.StudentId WHERE r.Id=@p0",reportId).SingleOrDefault();if(report==null)throw new InvalidOperationException("Không tìm thấy báo cáo.");Own(report.CourseId,teacher);var q=report.Question;reason=Content(reason);accepted=(accepted??new int[0]).Distinct().ToArray();if(!allCredit&&(accepted.Length==0||accepted.Any(i=>i<0||i>=q.Options.Length)))throw new InvalidOperationException("Chọn ít nhất một đáp án hợp lệ hoặc cho toàn bộ học viên điểm câu này.");
   var rule=new QuizCorrection{CourseId=report.CourseId,QuestionId=q.Id,VersionKey=QuizAssessmentService.Version(q),AcceptedJson=JsonConvert.SerializeObject(allCredit?new string[0]:accepted.Select(i=>q.Options[i]).ToArray()),AllCredit=allCredit,Reason=reason};
   var plan=new QuizRegradePlan{Id=Guid.NewGuid().ToString("N"),TeacherId=teacher,CourseId=report.CourseId,ReportId=reportId,PayloadJson=JsonConvert.SerializeObject(rule),ExpiresAt=DateTime.UtcNow.AddMinutes(15)};
   using(var tx=db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable)){Lock(plan.CourseId);Fill(plan);plan.StateHash=State(plan.CourseId);db.Database.ExecuteSqlCommand("INSERT dbo.QuizRegradePreview(Id,TeacherId,CourseId,ReportId,PayloadJson,StateHash,ExpiresAt) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)",plan.Id,teacher,plan.CourseId,reportId,plan.PayloadJson,plan.StateHash,plan.ExpiresAt);tx.Commit();}return plan;
  }
  void Lock(string course){db.Database.ExecuteSqlCommand("DECLARE @r INT;EXEC @r=sys.sp_getapplock @Resource=@p0,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;IF @r<0 THROW 51000,'Assessment lock failed',1;","QuizAssessment_"+course);}
  public QuizRegradePlan GetPreview(string id,int teacher){Ready();var plan=db.Database.SqlQuery<QuizRegradePlan>("SELECT *,0 OpenAttempts FROM dbo.QuizRegradePreview WHERE Id=@p0 AND TeacherId=@p1",id??"",teacher).SingleOrDefault();if(plan==null||plan.AppliedAt.HasValue||plan.ExpiresAt<=DateTime.UtcNow)throw new InvalidOperationException("Bản xem trước không còn hiệu lực. Hãy tạo lại.");Own(plan.CourseId,teacher);if(State(plan.CourseId)!=plan.StateHash)throw new InvalidOperationException("Dữ liệu đã thay đổi. Hãy tạo bản xem trước mới.");Fill(plan);return plan;}
  public void Confirm(string id,int teacher){
   Ready();var header=GetPreview(id,teacher);using(var tx=db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable)){
    Lock(header.CourseId);var plan=GetPreview(id,teacher);
    if(State(plan.CourseId)!=plan.StateHash)throw new InvalidOperationException("Kết quả hoặc quy tắc đã thay đổi sau khi xem trước. Hãy xem trước lại để xác nhận đúng ảnh hưởng.");
    var rule=plan.Rule;
    int correction=db.Database.SqlQuery<int>("INSERT dbo.QuizCorrection(CourseId,QuestionId,VersionKey,AcceptedJson,AllCredit,Reason,TeacherId) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6);SELECT CAST(SCOPE_IDENTITY() AS INT);",rule.CourseId,rule.QuestionId,rule.VersionKey,rule.AcceptedJson,rule.AllCredit,rule.Reason,teacher).Single();
    foreach(var line in plan.Lines){var a=line.Attempt;var g=line.Grade;
     db.Database.ExecuteSqlCommand("INSERT dbo.QuizGradeAudit(CorrectionId,AttemptId,OldScore,CalculatedScore,RecognizedScore,OldPassed,RecognizedPassed,OldSkills,NewSkills) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8)",correction,a.Id,a.Score??0,g.CalculatedScore,g.Score,a.Passed??false,g.Passed,(object)a.SkillResultsJson??DBNull.Value,JsonConvert.SerializeObject(g.Skills));
     db.Database.ExecuteSqlCommand("UPDATE dbo.QuizAttempt SET Score=@p0,Correct=@p1,Passed=@p2,SkillResultsJson=@p3 WHERE Id=@p4 AND SubmittedAt IS NOT NULL",g.Score,g.Correct,g.Passed,JsonConvert.SerializeObject(g.Skills),a.Id);
    }
    db.Database.ExecuteSqlCommand("UPDATE dbo.QuizQuestionReport SET Status='Corrected',Response=@p0,TeacherId=@p1,UpdatedAt=GETUTCDATE() WHERE Id=@p2",rule.Reason,teacher,plan.ReportId);
    db.Database.ExecuteSqlCommand("UPDATE dbo.QuizRegradePreview SET AppliedAt=GETUTCDATE() WHERE Id=@p0",plan.Id);tx.Commit();
   }
  }
  public List<QuizGradeAudit> Notices(int student){return GradeHistory(student,false);}
  public List<QuizGradeAudit> GradeHistory(int user,bool teacher){Ready();return db.Database.SqlQuery<QuizGradeAudit>(@"SELECT g.*,c.Reason,k.tenKhoaHoc CourseName,n.hoTen TeacherName FROM dbo.QuizGradeAudit g JOIN dbo.QuizAttempt a ON a.Id=g.AttemptId JOIN dbo.QuizCorrection c ON c.Id=g.CorrectionId JOIN dbo.KhoaHoc k ON k.maKhoaHoc=a.CourseId JOIN dbo.NguoiDung n ON n.maNguoiDung=c.TeacherId WHERE (@p1=1 AND k.maGiaoVien=@p0) OR (@p1=0 AND a.StudentId=@p0) ORDER BY g.Id DESC",user,teacher).ToList();}
  public void Appeal(int student,int attempt,string content) {
   Ready();var quiz=new QuizService(db);var a=quiz.Attempt(attempt);
   if(a==null||a.StudentId!=student)throw new InvalidOperationException("Không tìm thấy lượt thi của bạn.");
   quiz.RequireEnrollment(a.CourseId,student);
   if(!a.SubmittedAt.HasValue)throw new InvalidOperationException("Nộp bài trước khi gửi phúc khảo.");
   content=Content(content);
   using(var tx=db.Database.BeginTransaction()) {
    db.Database.ExecuteSqlCommand("DECLARE @r int; EXEC @r=sp_getapplock @Resource=@p0,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 50001,'Quiz appeal lock timeout',1;","QuizAppeal_"+a.CourseId+"_"+student);
    if(db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.QuizAppeal p JOIN dbo.QuizAttempt a ON a.Id=p.AttemptId WHERE p.StudentId=@p0 AND a.CourseId=@p1",student,a.CourseId).Single()>0)
     throw new InvalidOperationException("Mỗi học viên chỉ được phúc khảo một lần cho mỗi khóa học. Bạn đã gửi yêu cầu; hãy theo dõi phản hồi bên dưới.");
    try{db.Database.ExecuteSqlCommand("INSERT dbo.QuizAppeal(AttemptId,StudentId,Content) VALUES(@p0,@p1,@p2)",attempt,student,content);}
    catch(System.Data.SqlClient.SqlException e){if(e.Number==2601||e.Number==2627)throw new InvalidOperationException("Bạn đã gửi yêu cầu phúc khảo cho khóa học này.");throw;}
    tx.Commit();
   }
  }
  public List<QuizAppeal> Appeals(int user,bool teacher){Ready();var rows=db.Database.SqlQuery<QuizAppeal>(@"SELECT p.*,a.CourseId,k.tenKhoaHoc CourseName,n.hoTen StudentName FROM dbo.QuizAppeal p JOIN dbo.QuizAttempt a ON a.Id=p.AttemptId JOIN dbo.KhoaHoc k ON k.maKhoaHoc=a.CourseId JOIN dbo.NguoiDung n ON n.maNguoiDung=p.StudentId WHERE (@p1=1 AND k.maGiaoVien=@p0) OR (@p1=0 AND p.StudentId=@p0) ORDER BY p.Id DESC",user,teacher).ToList();foreach(var row in rows){if(teacher)row.Questions=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(new QuizService(db).Attempt(row.AttemptId).SnapshotJson);row.Replies=db.Database.SqlQuery<QuizAppealReply>("SELECT r.Content,r.Status,r.CreatedAt,n.hoTen TeacherName FROM dbo.QuizAppealReply r JOIN dbo.NguoiDung n ON n.maNguoiDung=r.TeacherId WHERE r.AppealId=@p0 ORDER BY r.Id",row.Id).ToList();}return rows;}
  public int QuestionFromAppeal(int teacher,int appeal,int questionId){
   Ready();var request=Appeals(teacher,true).SingleOrDefault(x=>x.Id==appeal);if(request==null)throw new InvalidOperationException("Không có quyền xử lý phúc khảo.");
   var a=new QuizService(db).Attempt(request.AttemptId);var q=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson).SingleOrDefault(x=>x.Id==questionId);if(q==null)throw new InvalidOperationException("Câu hỏi không thuộc lượt thi phúc khảo.");
   var existing=db.Database.SqlQuery<int>("SELECT Id FROM dbo.QuizQuestionReport WHERE AttemptId=@p0 AND QuestionId=@p1",a.Id,questionId).ToList();if(existing.Any())return existing[0];
   return db.Database.SqlQuery<int>("INSERT dbo.QuizQuestionReport(AttemptId,QuestionId,StudentId,CourseId,SnapshotJson,Reason,Content) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6);SELECT CAST(SCOPE_IDENTITY() AS INT);",a.Id,q.Id,a.StudentId,a.CourseId,JsonConvert.SerializeObject(q),"Đáp án có vẻ sai","Giáo viên xem xét từ phúc khảo #"+appeal).Single();
  }
  public void LogInitial(QuizAttempt working,QuizGrade grade){
   var matches=new QuizAssessmentService(db).Corrections(working.CourseId).Where(r=>Matches(working,r)).ToList();if(!matches.Any())return;var original=QuizAssessmentService.Evaluate(working,new List<QuizCorrection>());
   foreach(var rule in matches)db.Database.ExecuteSqlCommand("INSERT dbo.QuizGradeAudit(CorrectionId,AttemptId,OldScore,CalculatedScore,RecognizedScore,OldPassed,RecognizedPassed,OldSkills,NewSkills) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8)",rule.Id,working.Id,original.Score,grade.CalculatedScore,grade.Score,original.Passed,grade.Passed,JsonConvert.SerializeObject(original.Skills),JsonConvert.SerializeObject(grade.Skills));
  }
  public void ReplyAppeal(int teacher,int appeal,string status,string response){Ready();if(status!="Processing"&&status!="Answered")throw new InvalidOperationException("Trạng thái không hợp lệ.");response=Content(response);using(var tx=db.Database.BeginTransaction()){int rows=db.Database.ExecuteSqlCommand("UPDATE p SET Status=@p0 FROM dbo.QuizAppeal p JOIN dbo.QuizAttempt a ON a.Id=p.AttemptId JOIN dbo.KhoaHoc k ON k.maKhoaHoc=a.CourseId WHERE p.Id=@p1 AND k.maGiaoVien=@p2",status,appeal,teacher);if(rows!=1)throw new InvalidOperationException("Không có quyền xử lý phúc khảo.");db.Database.ExecuteSqlCommand("INSERT dbo.QuizAppealReply(AppealId,TeacherId,Content,Status) VALUES(@p0,@p1,@p2,@p3)",appeal,teacher,response,status);tx.Commit();}}
 }
}
