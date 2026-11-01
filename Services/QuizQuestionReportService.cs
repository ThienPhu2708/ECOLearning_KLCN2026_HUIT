using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;
using Newtonsoft.Json;
namespace ECOLearning.Services {
 public class QuizQuestionReport {
  public int Id {get;set;} public int AttemptId {get;set;} public int QuestionId {get;set;}
  public int StudentId {get;set;} public string CourseId {get;set;} public string CourseName {get;set;}
  public string StudentName {get;set;} public string SnapshotJson {get;set;}
  public string Reason {get;set;} public string Content {get;set;} public string Status {get;set;}
  public string Response {get;set;} public DateTime CreatedAt {get;set;} public DateTime? UpdatedAt {get;set;}
  public QuizSnapshotQuestion Question { get { return JsonConvert.DeserializeObject<QuizSnapshotQuestion>(SnapshotJson); } }
  public string StatusText { get { return Status=="Processing" ? "Đang xử lý" : Status=="Corrected" ? "Đã chỉnh sửa" : Status=="Answered" ? "Đã giải đáp" : "Chờ xử lý"; } }
 }
 public class QuizTeacherNotificationCounts {public int Appeals {get;set;}public int Reports {get;set;}}
 public class QuizQuestionReportService {
  readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
  public QuizQuestionReportService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 context){db=context;}
  static SqlParameter P(string name,object value){return new SqlParameter(name,value??DBNull.Value);}
  public static readonly string[] Reasons={"Câu hỏi không rõ","Đáp án có vẻ sai","Nhiều đáp án đúng","Lỗi hiển thị","Vấn đề khác"};
  void Ready(){if(db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.QuizQuestionReport','U') IS NULL THEN 0 ELSE 1 END").Single()!=1)throw new InvalidOperationException("Chức năng báo lỗi câu hỏi chưa được cài đặt (migration 024).");}
  public QuizTeacherNotificationCounts TeacherNotifications(int teacher,string course=null) {
   var counts=new QuizTeacherNotificationCounts();course=string.IsNullOrWhiteSpace(course)?null:course;
   int installed=db.Database.SqlQuery<int>("SELECT (CASE WHEN OBJECT_ID('dbo.QuizQuestionReport','U') IS NOT NULL THEN 1 ELSE 0 END)+(CASE WHEN OBJECT_ID('dbo.QuizAppeal','U') IS NOT NULL THEN 2 ELSE 0 END)+(CASE WHEN OBJECT_ID('dbo.QuizIntegrityReview','U') IS NOT NULL THEN 4 ELSE 0 END)").Single();
   if((installed&1)!=0)counts.Reports=db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.QuizQuestionReport r JOIN dbo.KhoaHoc k ON k.maKhoaHoc=r.CourseId WHERE k.maGiaoVien=@p0 AND (@p1 IS NULL OR r.CourseId=@p1) AND r.Status IN ('Pending','Processing')",P("@p0",teacher),P("@p1",course)).Single();
   if((installed&2)!=0)counts.Appeals=db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.QuizAppeal p JOIN dbo.QuizAttempt a ON a.Id=p.AttemptId JOIN dbo.KhoaHoc k ON k.maKhoaHoc=a.CourseId WHERE k.maGiaoVien=@p0 AND (@p1 IS NULL OR a.CourseId=@p1) AND p.Status IN ('Pending','Processing')",P("@p0",teacher),P("@p1",course)).Single();
   if((installed&4)!=0)counts.Appeals+=db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.QuizIntegrityReview r JOIN dbo.QuizAttempt a ON a.Id=r.AttemptId JOIN dbo.KhoaHoc k ON k.maKhoaHoc=a.CourseId WHERE k.maGiaoVien=@p0 AND (@p1 IS NULL OR a.CourseId=@p1) AND r.Status='AwaitingTeacher' AND r.Followup='Retake'",P("@p0",teacher),P("@p1",course)).Single();
   return counts;
  }
  public bool HasReports(int student,string course) {
   if(db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.QuizQuestionReport','U') IS NULL THEN 0 ELSE 1 END").Single()==0)return false;
   return db.Database.SqlQuery<int>("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.QuizQuestionReport WHERE StudentId=@p0 AND CourseId=@p1) THEN 1 ELSE 0 END",student,course).Single()==1;
  }
  public void Send(int student,int attemptId,int questionId,string reason,string content){
   Ready(); var quiz=new QuizService(db); var a=quiz.Attempt(attemptId);
   if(a==null || a.StudentId!=student)throw new InvalidOperationException("Không tìm thấy lượt thi của bạn.");
   quiz.RequireEnrollment(a.CourseId,student);
   var q=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson).SingleOrDefault(x=>x.Id==questionId);
   if(q==null)throw new InvalidOperationException("Câu hỏi không thuộc lượt thi này.");
   content=(content??"").Trim();
   if(!Reasons.Contains(reason))throw new InvalidOperationException("Vui lòng chọn lý do báo lỗi.");
   if(content.Length>2000 || (reason=="Vấn đề khác" && content.Length==0))throw new InvalidOperationException("Nhập mô tả tối đa 2000 ký tự; lý do khác cần có mô tả.");
   try {db.Database.ExecuteSqlCommand(@"INSERT dbo.QuizQuestionReport(AttemptId,QuestionId,StudentId,CourseId,SnapshotJson,Reason,Content) VALUES(@a,@q,@s,@c,@snapshot,@r,@text)",P("@a",a.Id),P("@q",q.Id),P("@s",student),P("@c",a.CourseId),P("@snapshot",JsonConvert.SerializeObject(q)),P("@r",reason),P("@text",content));}
   catch(SqlException e){if(e.Number==2601||e.Number==2627)throw new InvalidOperationException("Bạn đã báo lỗi câu này trong lượt thi này. Xem tại Báo lỗi của tôi.");throw;}
  }
  public List<QuizQuestionReport> List(int user,bool teacher,string course=null){
   Ready();
   var rows=db.Database.SqlQuery<QuizQuestionReport>(@"SELECT r.*,k.tenKhoaHoc CourseName,n.hoTen StudentName FROM dbo.QuizQuestionReport r JOIN dbo.KhoaHoc k ON k.maKhoaHoc=r.CourseId JOIN dbo.NguoiDung n ON n.maNguoiDung=r.StudentId WHERE ((@teacher=1 AND k.maGiaoVien=@u) OR (@teacher=0 AND r.StudentId=@u)) AND (@c IS NULL OR r.CourseId=@c) ORDER BY r.Id DESC",P("@teacher",teacher),P("@u",user),P("@c",string.IsNullOrWhiteSpace(course)?null:course)).ToList();
   if(!teacher){foreach(var r in rows){var a=new QuizService(db).Attempt(r.AttemptId);if(!a.SubmittedAt.HasValue && DateTime.UtcNow<a.ExpiresAt){r.Response=null;r.Status="Pending";} // Do not leak solutions during a timed attempt.
    var q=r.Question;q.CorrectOption=-1;q.Explanation=null;r.SnapshotJson=JsonConvert.SerializeObject(q);
   }}
   return rows;
  }
  public void Respond(int teacher,int id,string status,string response){
   Ready();response=(response??"").Trim();
   if(!new[]{"Processing","Corrected","Answered"}.Contains(status))throw new InvalidOperationException("Trạng thái không hợp lệ.");
   if(response.Length>2000 || (status!="Processing" && response.Length==0))throw new InvalidOperationException("Vui lòng nhập phản hồi, tối đa 2000 ký tự.");
   int count=db.Database.ExecuteSqlCommand(@"UPDATE r SET Status=@status,Response=@text,TeacherId=@u,UpdatedAt=GETUTCDATE() FROM dbo.QuizQuestionReport r JOIN dbo.KhoaHoc k ON k.maKhoaHoc=r.CourseId WHERE r.Id=@id AND k.maGiaoVien=@u",P("@status",status),P("@text",response),P("@u",teacher),P("@id",id));
   if(count!=1)throw new InvalidOperationException("Không tìm thấy báo cáo thuộc khóa học của bạn.");
  }
 }
}
