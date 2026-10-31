using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ECOLearning.Models;
using ECOLearning.ViewModels;
using Newtonsoft.Json;
namespace ECOLearning.Services {
 public class QuizGrade {public int Correct;public decimal Score;public bool Passed;public List<QuizSkillResult> Skills;public decimal CalculatedScore;}
 public class QuizCorrection {public int Id{get;set;}public string CourseId{get;set;}public int QuestionId{get;set;}public string VersionKey{get;set;}public string AcceptedJson{get;set;}public bool AllCredit{get;set;}public string Reason{get;set;}}
 public class QuizAssessmentService {
  readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
  public QuizAssessmentService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 context){db=context;}
  public bool Installed(){return db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.QuizSkill','U') IS NULL THEN 0 ELSE 1 END").Single()==1;}
  public List<QuizSkill> Skills(string course){if(!Installed())return new List<QuizSkill>();return db.Database.SqlQuery<QuizSkill>("SELECT * FROM dbo.QuizSkill WHERE CourseId=@p0 ORDER BY Name",course).ToList();}
  public void AddSkill(string course,int teacher,string name){if(!new QuizService(db).Owns(course,teacher))throw new InvalidOperationException("Không có quyền sửa kỹ năng khóa học này.");if(!Installed())throw new InvalidOperationException("Cần chạy migration 027.");name=(name??"").Trim();if(name.Length==0||name.Length>120)throw new InvalidOperationException("Tên kỹ năng cần từ 1 đến 120 ký tự.");if(Skills(course).Any(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Kỹ năng đã tồn tại.");try{db.Database.ExecuteSqlCommand("INSERT dbo.QuizSkill(CourseId,Name) VALUES(@p0,@p1)",course,name);}catch(System.Data.SqlClient.SqlException e){if(e.Number==2601||e.Number==2627)throw new InvalidOperationException("Kỹ năng đã tồn tại.");throw;}}
  public void SaveQuestionWithSkill(QuizQuestion question,int teacher,string newSkillName) {
   var quiz=new QuizService(db);
   if(!quiz.Owns(question.CourseId,teacher))throw new InvalidOperationException("Không có quyền sửa câu hỏi khóa học này.");
   QuizService.ValidateQuestion(question);
   newSkillName=(newSkillName??"").Trim();
   if(newSkillName.Length>120)throw new InvalidOperationException("Tên kỹ năng tối đa 120 ký tự.");
   using(var tx=db.Database.BeginTransaction()) {
    if(newSkillName.Length>0) {
     db.Database.ExecuteSqlCommand("DECLARE @r int; EXEC @r=sp_getapplock @Resource=@p0,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 50001,'Quiz skill lock timeout',1;","QuizImport_"+question.CourseId);
     var skill=Skills(question.CourseId).SingleOrDefault(x=>x.Name.Equals(newSkillName,StringComparison.OrdinalIgnoreCase));
     if(skill==null){AddSkill(question.CourseId,teacher,newSkillName);skill=Skills(question.CourseId).Single(x=>x.Name.Equals(newSkillName,StringComparison.OrdinalIgnoreCase));}
     if(!skill.Active)throw new InvalidOperationException("Kỹ năng này đã ngừng sử dụng. Hãy chọn kỹ năng khác.");
     question.SkillId=skill.Id;
    }
    quiz.SaveQuestion(question);
    tx.Commit();
   }
  }
  public void DeleteSkill(string course,int teacher,int id) {
   if(!new QuizService(db).Owns(course,teacher))throw new InvalidOperationException("Không có quyền xóa kỹ năng khóa học này.");
   if(!Installed())throw new InvalidOperationException("Cần chạy migration 027.");
   using(var tx=db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable)) {
    var skill=db.Database.SqlQuery<QuizSkill>("SELECT * FROM dbo.QuizSkill WHERE Id=@p0 AND CourseId=@p1",id,course).SingleOrDefault();
    if(skill==null)throw new InvalidOperationException("Không tìm thấy kỹ năng trong khóa học này.");
    var config=new QuizService(db).Config(course);
    if(config!=null&&Rules(config).Any(r=>r.SkillId==id))throw new InvalidOperationException("Kỹ năng đang dùng trong phân bổ đề. Hãy đặt số câu bằng 0, bỏ bắt buộc và lưu cấu hình trước khi xóa.");
    if(db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.QuizQuestion WHERE SkillId=@p0",id).Single()>0)throw new InvalidOperationException("Kỹ năng đang gắn với câu hỏi. Hãy đổi hoặc gỡ kỹ năng ở các câu hỏi đó trước khi xóa.");
    db.Database.ExecuteSqlCommand("DELETE dbo.QuizSkill WHERE Id=@p0 AND CourseId=@p1",id,course);
    tx.Commit();
   }
  }
  public List<QuizSkillRule> Rules(QuizConfig config){try{return JsonConvert.DeserializeObject<List<QuizSkillRule>>(config.MatrixJson??"[]")??new List<QuizSkillRule>();}catch(JsonException){throw new InvalidOperationException("Ma trận kỹ năng không hợp lệ.");}}
  public void Normalize(QuizConfig c){if(!c.UseMatrix)return;var input=c.SkillRules??Rules(c);if(input.Any(r=>r.Easy<0||r.Medium<0||r.Hard<0||r.Easy>100||r.Medium>100||r.Hard>100))throw new InvalidOperationException("Số câu mỗi mức từ 0 đến 100.");if(input.Any(r=>r.Required&&r.Total==0))throw new InvalidOperationException("Kỹ năng bắt buộc phải có ít nhất một câu. Bỏ đánh dấu bắt buộc nếu không dùng trong đề.");var rules=input.Where(r=>r.Total>0).ToList();var skills=Skills(c.CourseId);if(rules.Count==0||rules.Select(r=>r.SkillId).Distinct().Count()!=rules.Count)throw new InvalidOperationException("Chọn ít nhất một kỹ năng, không lặp kỹ năng.");foreach(var r in rules){var skill=skills.SingleOrDefault(x=>x.Id==r.SkillId&&x.Active);if(skill==null||r.Easy<0||r.Medium<0||r.Hard<0||r.Total>100||r.PassPercent<1||r.PassPercent>100)throw new InvalidOperationException("Kiểm tra kỹ năng, số câu và ngưỡng đạt 1–100%.");r.Name=skill.Name;}c.Topic=null;c.EasyCount=rules.Sum(r=>r.Easy);c.MediumCount=rules.Sum(r=>r.Medium);c.HardCount=rules.Sum(r=>r.Hard);c.MatrixJson=JsonConvert.SerializeObject(rules);}
  public void ValidatePool(QuizConfig c,List<QuizQuestion> pool){var rules=Rules(c);if(!c.RandomSelection&&pool.Any(q=>!rules.Any(r=>r.SkillId==q.SkillId)))throw new InvalidOperationException("Đề cố định chứa câu chưa thuộc kỹ năng đã chọn.");foreach(var r in rules){int[] counts={r.Easy,r.Medium,r.Hard};for(int d=1;d<=3;d++){int n=pool.Count(q=>q.SkillId==r.SkillId&&q.Difficulty==d);if(n<counts[d-1]||(!c.RandomSelection&&n!=counts[d-1]))throw new InvalidOperationException("Kỹ năng "+r.Name+" chưa đủ/khớp câu mức "+d+" theo ma trận.");}}}
  public static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
  public static string Version(QuizSnapshotQuestion q){return Hash(JsonConvert.SerializeObject(new{q.Id,q.Text,Options=q.Options.OrderBy(x=>x,StringComparer.Ordinal).ToArray(),Correct=q.CorrectOption>=0&&q.CorrectOption<q.Options.Length?q.Options[q.CorrectOption]:null,q.Explanation,q.Topic,q.Difficulty}));}
  public List<QuizCorrection> Corrections(string course){if(!Installed())return new List<QuizCorrection>();return db.Database.SqlQuery<QuizCorrection>("SELECT Id,CourseId,QuestionId,VersionKey,AcceptedJson,AllCredit,Reason FROM dbo.QuizCorrection WHERE CourseId=@p0",course).OrderBy(r=>r.Id).ToList();}
  public static QuizGrade Evaluate(QuizAttempt a,List<QuizCorrection> corrections){
   var questions=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson);var answers=JsonConvert.DeserializeObject<int[]>(a.AnswersJson??"[]");
   var raw=new bool[questions.Count];var recognized=new bool[questions.Count];
   for(int i=0;i<questions.Count;i++){var q=questions[i];int answer=i<answers.Length?answers[i]:-1;bool original=answer==q.CorrectOption;var rule=corrections.SingleOrDefault(r=>r.QuestionId==q.Id&&r.VersionKey==Version(q));raw[i]=rule==null?original:rule.AllCredit||(answer>=0&&answer<q.Options.Length&&JsonConvert.DeserializeObject<string[]>(rule.AcceptedJson).Contains(q.Options[answer],StringComparer.Ordinal));recognized[i]=original||raw[i];}
   var skills=questions.Select((q,i)=>new{q,i}).Where(x=>x.q.SkillId.HasValue).GroupBy(x=>x.q.SkillId.Value).Select(g=>{var q=g.First().q;int correct=g.Count(x=>recognized[x.i]);return new QuizSkillResult{SkillId=g.Key,Name=q.SkillName,Total=g.Count(),Correct=correct,CalculatedCorrect=g.Count(x=>raw[x.i]),PassPercent=q.SkillPassPercent,Required=q.SkillRequired,Passed=100*correct>=q.SkillPassPercent*g.Count()};}).ToList();
   var previous=JsonConvert.DeserializeObject<List<QuizSkillResult>>(a.SkillResultsJson??"[]")??new List<QuizSkillResult>();foreach(var skill in skills){var old=previous.FirstOrDefault(x=>x.SkillId==skill.SkillId);if(old!=null){skill.Correct=Math.Max(skill.Correct,old.Correct);skill.Passed|=old.Passed;}}
   int totalCorrect=recognized.Count(x=>x);decimal score=Math.Round(10m*totalCorrect/questions.Count,2);bool passed=100m*totalCorrect>=a.PassPercent*questions.Count&&skills.All(x=>!x.Required||x.Passed);
   return new QuizGrade{CalculatedScore=Math.Round(10m*raw.Count(x=>x)/questions.Count,2),Correct=Math.Max(totalCorrect,a.Correct??0),Score=Math.Max(score,a.Score??0),Passed=passed||a.Passed==true,Skills=skills};
  }
 }
}
