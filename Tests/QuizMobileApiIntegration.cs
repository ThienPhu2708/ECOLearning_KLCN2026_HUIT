using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;
using ECOLearning.Controllers.Api;
class QuizMobileApiIntegration {
 static int checks;
 static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);checks++;}
 class Request:HttpRequestBase {readonly MemoryStream input;public Request(object body){input=new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body??new{})));}public override Stream InputStream=>input;public override Uri Url=>new Uri("https://localhost:44350/api/v1/");public override string ApplicationPath=>"/";}
 class Context:HttpContextBase {readonly Request request;public Context(object body){request=new Request(body);}public override HttpRequestBase Request=>request;}
 static JObject Call(int student,object body,Func<QuizApiController,ActionResult> action){using(var c=new QuizApiController()){
 typeof(ApiControllerBase).GetField("<CurrentUserId>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(c,student);
 c.ControllerContext=new ControllerContext(new Context(body),new RouteData(),c);
 var result=action(c);var payload=typeof(ApiJsonResult).GetField("body",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(result);
 return JObject.FromObject(payload,JsonSerializer.Create(new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver()}));
 }}
 static JObject Courses(int student,string status,bool separate=true){using(var c=new LearningApiController()){
 typeof(ApiControllerBase).GetField("<CurrentUserId>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(c,student);c.ControllerContext=new ControllerContext(new Context(null),new RouteData(),c);c.Url=new UrlHelper(c.ControllerContext.RequestContext,new RouteCollection());var result=c.MyCourses(status,separateCertificates:separate);var payload=typeof(ApiJsonResult).GetField("body",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(result);return JObject.FromObject(payload,JsonSerializer.Create(new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver()}));
 }}
 static int[] Correct(QuizAttempt a)=>JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson).Select(q=>q.CorrectOption).ToArray();
 static int Main(){string id="QM"+Guid.NewGuid().ToString("N").Substring(0,10);var users=new List<int>();int teacher=0;
 using(var db=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())try{
  var t=new NguoiDung{hoTen="Mobile API fixture teacher",email=id+"t@test.invalid",matKhau="fixture",vaiTro="GiaoVien",trangThai=true};db.NguoiDung.Add(t);db.SaveChanges();teacher=t.maNguoiDung;
  db.KhoaHoc.Add(new KhoaHoc{maKhoaHoc=id,tenKhoaHoc="Mobile API fixture course",maGiaoVien=teacher,maDanhMuc=db.DanhMuc.First().maDanhMuc,giaBan=0,trangThai="Đã xuất bản"});db.SaveChanges();
  db.ChuongHoc.Add(new ChuongHoc{maChuong=id+"c",maKhoaHoc=id,tenChuong="Fixture",thuTu=1});db.SaveChanges();db.BaiHoc.Add(new BaiHoc{maBaiHoc=id+"_1",maChuong=id+"c",tenBaiHoc="Fixture",thuTu=1,thoiLuong=60});db.SaveChanges();
  for(int i=0;i<2;i++){var u=new NguoiDung{hoTen="Mobile API fixture learner "+i,email=id+i+"@test.invalid",matKhau="fixture",vaiTro="HocVien",trangThai=true};db.NguoiDung.Add(u);db.SaveChanges();users.Add(u.maNguoiDung);db.DangKyKhoaHoc.Add(new DangKyKhoaHoc{maDangKy=id+i,maKhoaHoc=id,maHocVien=u.maNguoiDung,ngayDangKy=DateTime.Now});db.TienDoBaiHoc.Add(new TienDoBaiHoc{maHocVien=u.maNguoiDung,maBaiHoc=id+"_1",daHoanThanh=true,ngayHoanThanh=DateTime.Now});}db.SaveChanges();
  var quiz=new QuizService(db);var retakes=new QuizRetakeService(db);var notifications=new QuizQuestionReportService(db);
  for(int i=0;i<2;i++)quiz.SaveQuestion(new QuizQuestion{CourseId=id,Topic="Topic",Text="Retake question "+i,Options=new[]{"A","B","C","D"},CorrectOption=0,Difficulty=1});
  var config=new QuizConfig{CourseId=id,Minutes=20,MaxAttempts=2,PassPercent=80,EasyCount=2,MediumCount=0,HardCount=0,Published=true};quiz.SaveConfig(config);

  string key=Guid.NewGuid().ToString("N");var info=Call(users[0],null,c=>c.Info(id));Check((bool)info["data"]["supportsQuizReview"],"info advertises supported mobile quiz workflows");
  var start=Call(users[0],new{clientKey=key},c=>c.Start(id));var data=start["data"];int attempt=(int)data["attemptId"];string token=(string)data["writerToken"];int question=(int)data["questions"][0]["questionId"];
  Check(question>0&&data["questions"][0]["correctOption"]==null,"start returns server question IDs without answer key");
  Check(!(bool)Call(users[1],null,c=>c.AttemptResult(attempt))["success"],"another learner cannot read attempt result");
  Check(!(bool)Call(users[1],new{questionId=question,reason="Đáp án có vẻ sai"},c=>c.SendQuestionReport(attempt))["success"],"another learner cannot report someone else's attempt");
  Check((bool)Call(users[0],new{questionId=question,reason="Đáp án có vẻ sai",content="Mobile question report"},c=>c.SendQuestionReport(attempt))["success"],"learner sends question report through authenticated API");
  Check(!(bool)Call(users[0],new{questionId=question,reason="Đáp án có vẻ sai"},c=>c.SendQuestionReport(attempt))["success"],"duplicate mobile report rejected");
  for(int i=0;i<3;i++)Check((bool)Call(users[0],new{writerToken=token,eventKey=Guid.NewGuid().ToString("N"),returned=false},c=>c.RecordVisibility(attempt))["success"],"record departure "+(i+1));
  var submitted=Call(users[0],new{writerToken=token,answers=Correct(quiz.Attempt(attempt))},c=>c.Submit(attempt));var result=submitted["data"];
  Check((bool)result["pendingReview"]&&!(bool)result["passed"]&&result["score"].Type==JTokenType.Null&&result["review"].Type==JTokenType.Null,"pending API result withholds score and solutions and cannot claim a pass");
  Check((string)Call(users[0],new{clientKey=key},c=>c.Resume(attempt))["code"]=="QUIZ_FINISHED"&&quiz.Attempts(id,users[0]).Count==1,"resuming finished history never creates or consumes another attempt");
  Check(!(bool)Call(users[0],null,c=>c.Claim(id))["success"],"pending mobile quiz cannot claim certificate");
  new QuizVisibilityService(db).ApproveResult(teacher,attempt,"Reviewed mobile departure log.");
  result=Call(users[0],null,c=>c.AttemptResult(attempt))["data"];Check((bool)result["passed"]&&result["review"].Count()==2,"approved result exposes answers only to owning learner");
  quiz.ClaimCertificate(id,users[0]);Check((int)Courses(users[0],"certified")["data"]["total"]==1&&(int)Courses(users[0],"completed")["data"]["total"]==0,"API separates issued certificate from completed lesson tab");Check((int)Courses(users[0],"completed",false)["data"]["total"]==1,"legacy app completed tab still includes issued certificates");
  Check((bool)Call(users[0],new{attemptId=attempt,content="Please review my quiz."},c=>c.SendAppeal(id))["success"],"mobile learner submits appeal");
  Check(!(bool)Call(users[0],new{attemptId=attempt,content="Again"},c=>c.SendAppeal(id))["success"],"mobile appeal allowance is enforced server-side");
  var support=Call(users[0],null,c=>c.Support(id))["data"];Check(!(bool)support["canAppeal"]&&support["appeals"].Count()==1&&support["reports"].Count()==1,"support history returns own reports and used appeal state");
  Check(!Call(users[1],null,c=>c.Support(id))["data"]["reports"].Any(),"support history cannot leak another learner's reports");
  retakes.RequestRetake(teacher,attempt,"Retake from mobile.");int review=retakes.ForTeacher(teacher,attempt).Id;Check((int)Courses(users[0],"certified")["data"]["total"]==0&&(int)Courses(users[0],"completed")["data"]["total"]==1,"retake moves revoked certificate course back to completed lessons tab");
  Check(!(bool)Call(users[1],new{clientKey=key},c=>c.StartRetake(id,review))["success"],"retake request is isolated to assigned learner");
  var retry=Call(users[0],new{clientKey=key},c=>c.StartRetake(id,review))["data"];int retryId=(int)retry["attemptId"];Check((bool)retry["isVerification"]&&retry["writerToken"]!=null,"mobile retake issues a protected replacement attempt");
  var second=Call(users[0],new{clientKey=Guid.NewGuid().ToString("N")},c=>c.StartRetake(id,review));Check((string)second["code"]=="QUIZ_SESSION_ACTIVE","retake takeover requires explicit confirmation");
  var repeated=Call(users[0],new{clientKey=key},c=>c.StartRetake(id,review))["data"];Check((int)repeated["attemptId"]==retryId&&(string)repeated["expiresAt"]==(string)retry["expiresAt"],"retake resume preserves attempt and deadline");
  Call(users[0],new{writerToken=(string)retry["writerToken"],answers=Correct(quiz.Attempt(retryId))},c=>c.Submit(retryId));
  result=Call(users[0],null,c=>c.AttemptResult(attempt))["data"];Check((bool)result["isVoided"]&&!(bool)result["passed"]&&result["review"].Type==JTokenType.Null,"superseded original is not represented as valid pass or answer review");
  var history=Call(users[0],null,c=>c.History(id))["data"];Check(history.Count()==2&&(bool)history.Last()["isVoided"],"mobile history includes replacement and explicitly voided original");
  Console.WriteLine(checks+" mobile API checks passed.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{
  db.Database.ExecuteSqlCommand(@"DELETE reply FROM dbo.QuizAppealReply reply JOIN dbo.QuizAppeal appeal ON appeal.Id=reply.AppealId JOIN dbo.QuizAttempt a ON a.Id=appeal.AttemptId WHERE a.CourseId=@p0;
DELETE appeal FROM dbo.QuizAppeal appeal JOIN dbo.QuizAttempt a ON a.Id=appeal.AttemptId WHERE a.CourseId=@p0;
DELETE dbo.QuizQuestionReport WHERE CourseId=@p0;
DELETE e FROM dbo.QuizIntegrityEvent e JOIN dbo.QuizIntegrityReview r ON r.Id=e.ReviewId JOIN dbo.QuizAttempt a ON a.Id=r.AttemptId WHERE a.CourseId=@p0;
DELETE v FROM dbo.QuizCertificateRevocation v JOIN dbo.QuizIntegrityReview r ON r.Id=v.ReviewId JOIN dbo.QuizAttempt a ON a.Id=r.AttemptId WHERE a.CourseId=@p0;
DELETE r FROM dbo.QuizIntegrityReview r JOIN dbo.QuizAttempt a ON a.Id=r.AttemptId WHERE a.CourseId=@p0;
DELETE g FROM dbo.QuizGradeAudit g JOIN dbo.QuizAttempt a ON a.Id=g.AttemptId WHERE a.CourseId=@p0;
DELETE dbo.QuizCertificate WHERE CourseId=@p0;DELETE dbo.QuizAttemptGrant WHERE CourseId=@p0;
DELETE s FROM dbo.QuizVisibilitySeen s JOIN dbo.QuizAttempt a ON a.Id=s.AttemptId WHERE a.CourseId=@p0;DELETE v FROM dbo.QuizVisibility v JOIN dbo.QuizAttempt a ON a.Id=v.AttemptId WHERE a.CourseId=@p0;
DELETE w FROM dbo.QuizWriter w JOIN dbo.QuizAttempt a ON a.Id=w.AttemptId WHERE a.CourseId=@p0;DELETE dbo.QuizAttempt WHERE CourseId=@p0;DELETE dbo.QuizConfig WHERE CourseId=@p0;DELETE dbo.QuizQuestion WHERE CourseId=@p0;
DELETE dbo.TienDoBaiHoc WHERE maBaiHoc=@p1;DELETE dbo.TienDoHocTap WHERE maKhoaHoc=@p0;DELETE dbo.DangKyKhoaHoc WHERE maKhoaHoc=@p0;DELETE dbo.BaiHoc WHERE maBaiHoc=@p1;DELETE dbo.ChuongHoc WHERE maKhoaHoc=@p0;DELETE dbo.KhoaHoc WHERE maKhoaHoc=@p0 AND tenKhoaHoc=N'Mobile API fixture course';",id,id+"_1");
  foreach(var user in users)db.Database.ExecuteSqlCommand("DELETE dbo.NguoiDung WHERE maNguoiDung=@p0 AND email LIKE @p1",user,id+"%@test.invalid");if(teacher>0)db.Database.ExecuteSqlCommand("DELETE dbo.NguoiDung WHERE maNguoiDung=@p0 AND email=@p1",teacher,id+"t@test.invalid");Console.WriteLine("Mobile API fixtures removed.");
 }
 }
}
