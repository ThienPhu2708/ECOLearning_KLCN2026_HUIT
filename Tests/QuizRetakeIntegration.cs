using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;
using Newtonsoft.Json;
class QuizRetakeIntegration {
 static int checks;static void Check(bool condition,string text){if(!condition)throw new Exception("FAIL: "+text);Console.WriteLine("PASS: "+text);checks++;}
 static void Reject(Action action,string text){try{action();}catch(InvalidOperationException){Check(true,text);return;}throw new Exception("Expected rejection: "+text);}
 static int[] Correct(QuizAttempt a){return JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson).Select(q=>q.CorrectOption).ToArray();}
 static int Main(){var id="QT"+Guid.NewGuid().ToString("N").Substring(0,10);var users=new List<int>();int teacher=0;
 using(var db=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())try {
  var t=new NguoiDung{hoTen="Retake fixture teacher",email=id+"t@test.invalid",matKhau="fixture",vaiTro="GiaoVien",trangThai=true};db.NguoiDung.Add(t);db.SaveChanges();teacher=t.maNguoiDung;
  db.KhoaHoc.Add(new KhoaHoc{maKhoaHoc=id,tenKhoaHoc="Retake fixture course",maGiaoVien=teacher,maDanhMuc=db.DanhMuc.First().maDanhMuc,giaBan=0,trangThai="Đã xuất bản"});db.SaveChanges();
  db.ChuongHoc.Add(new ChuongHoc{maChuong=id+"c",maKhoaHoc=id,tenChuong="Fixture",thuTu=1});db.SaveChanges();db.BaiHoc.Add(new BaiHoc{maBaiHoc=id+"_1",maChuong=id+"c",tenBaiHoc="Fixture",thuTu=1,thoiLuong=60});db.SaveChanges();
  for(int i=0;i<3;i++){var u=new NguoiDung{hoTen="Retake fixture learner "+i,email=id+i+"@test.invalid",matKhau="fixture",vaiTro="HocVien",trangThai=true};db.NguoiDung.Add(u);db.SaveChanges();users.Add(u.maNguoiDung);db.DangKyKhoaHoc.Add(new DangKyKhoaHoc{maDangKy=id+i,maKhoaHoc=id,maHocVien=u.maNguoiDung,ngayDangKy=DateTime.Now});db.TienDoBaiHoc.Add(new TienDoBaiHoc{maHocVien=u.maNguoiDung,maBaiHoc=id+"_1",daHoanThanh=true,ngayHoanThanh=DateTime.Now});}db.SaveChanges();
  var quiz=new QuizService(db);var retakes=new QuizRetakeService(db);var notifications=new QuizQuestionReportService(db);
  for(int i=0;i<2;i++)quiz.SaveQuestion(new QuizQuestion{CourseId=id,Topic="Topic",Text="Retake question "+i,Options=new[]{"A","B","C","D"},CorrectOption=0,Difficulty=1});
  var config=new QuizConfig{CourseId=id,Minutes=20,MaxAttempts=2,PassPercent=80,EasyCount=2,MediumCount=0,HardCount=0,Published=true};quiz.SaveConfig(config);
  var original=quiz.Start(id,users[0]);quiz.Submit(original.Id,users[0],Correct(original));quiz.ClaimCertificate(id,users[0]);var old=quiz.Attempt(original.Id);var code=quiz.Student(id,users[0]).Code;Check(quiz.CertificateCourseIds(users[0]).Contains(id)&&!quiz.CertificateCourseIds(users[1]).Contains(id),"certificate tab contains only learner issued certificates");
  var active=quiz.Start(id,users[1]);Reject(()=>retakes.RequestRetake(teacher,active.Id,"Not submitted"),"cannot request retake while original is still in progress");
  Reject(()=>retakes.RequestRetake(users[1],original.Id,"Foreign teacher"),"only course teacher requests retake");Reject(()=>retakes.RequestRetake(teacher,original.Id," "),"reason required before creating request");
  Check(retakes.ForStudent(users[0],id).Count==0,"invalid requests leave no draft records");
  retakes.RequestRetake(teacher,original.Id,"Please demonstrate understanding on a separate attempt.");var request=retakes.ForTeacher(teacher,original.Id);Check(!quiz.CertificateCourseIds(users[0]).Contains(id),"retake request removes revoked certificate from certificate tab");
  Reject(()=>quiz.Start(id,users[0]),"ordinary start cannot bypass requested retake before it begins");
  Check(request.CanRetake&&request.Events.Count==1&&request.Events[0].Content.Contains("understanding"),"request stores teacher reason and allows one retake");
  Check(retakes.ForStudent(users[0],id).Count==1&&retakes.ForStudent(users[1],id).Count==0,"request visible only to targeted learner");
  Reject(()=>retakes.ForTeacher(users[1],original.Id),"other teacher cannot read retake record");Reject(()=>retakes.StartRetake(users[1],request.Id),"other learner cannot start requested retake");
  Reject(()=>retakes.RequestRetake(teacher,original.Id,"Duplicate"),"duplicate request cannot grant multiple retakes");
  var tasks=Enumerable.Range(0,2).Select(i=>Task.Run(()=>{using(var connection=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())return new QuizRetakeService(connection).StartRetake(users[0],request.Id).Id;})).ToArray();Task.WaitAll(tasks);Check(tasks.Select(x=>x.Result).Distinct().Count()==1,"concurrent starts create exactly one separate retake");
  var a=quiz.Attempt(tasks[0].Result);Check(a.IsVerification&&a.Title.StartsWith("Quiz làm lại"),"retake is explicitly marked as verification attempt");
  var resumed=retakes.StartRetake(users[0],request.Id);Check(resumed.Id==a.Id&&resumed.ExpiresAt==a.ExpiresAt,"reload resumes same retake without resetting deadline");
  Check(quiz.Student(id,users[0]).Attempts==1&&!quiz.Student(id,users[0]).Passed&&quiz.Student(id,users[0]).BestScore==null&&quiz.Student(id,users[0]).RetakeRequired,"pending retake blocks old result without consuming ordinary allowance");
  Check(!quiz.Student(id,users[0]).IssuedAt.HasValue&&quiz.Attempt(original.Id).IsVoided,"old certificate and result cease to be valid");
  Reject(()=>quiz.ClaimCertificate(id,users[0]),"certificate cannot be reclaimed during retake");
  var writer=new QuizWriterService(db);var token=writer.Acquire(a.Id,users[0],Guid.NewGuid().ToString("N"),false);quiz.Submit(a.Id,users[0],new[]{-1,-1},token,true);
  Check(quiz.Attempt(a.Id).Score==0m&&quiz.Attempt(a.Id).Passed==false,"retake has its own graded result under writer protection");
  Check(quiz.Attempt(original.Id).Score==old.Score&&quiz.Attempt(original.Id).AnswersJson==old.AnswersJson&&quiz.Student(id,users[0]).BestScore==0m&&!quiz.Student(id,users[0]).Passed&&!quiz.Student(id,users[0]).IssuedAt.HasValue&&!quiz.Student(id,users[0]).RetakeRequired,"low retake replaces old pass while preserving historical answers");
  Reject(()=>quiz.ClaimCertificate(id,users[0]),"failed retake cannot reclaim old certificate");
  Check(quiz.Student(id,users[0]).Attempts==1&&quiz.Grants(id,users[0]).Count==0,"retake is not counted as ordinary attempt or extra attempt grant");
  Check(notifications.TeacherNotifications(teacher,id).Appeals==1,"completed retake notifies the owning teacher");
  retakes.MarkSeen(users[1],original.Id);Check(notifications.TeacherNotifications(teacher,id).Appeals==1,"another user cannot clear teacher notification");retakes.MarkSeen(teacher,original.Id);Check(notifications.TeacherNotifications(teacher,id).Appeals==0,"teacher opening completed retake clears its reminder");
  Reject(()=>retakes.StartRetake(users[0],request.Id),"completed retake cannot be started a second time");Reject(()=>retakes.RequestRetake(teacher,a.Id,"Loop"),"cannot chain verification retakes indefinitely");
  Check(retakes.ForStudent(users[0],id).Single().StatusText=="Đã làm lại","learner sees completion status without violation conclusions");
  quiz.Submit(active.Id,users[1],new[]{-1,-1});config.Published=false;quiz.SaveConfig(config);Reject(()=>retakes.RequestRetake(teacher,active.Id,"Closed quiz"),"teacher must open valid quiz before requesting retake");Check(retakes.ForStudent(users[1],id).Count==0,"closed quiz failure leaves no request");config.Published=true;quiz.SaveConfig(config);
  retakes.RequestRetake(teacher,active.Id,"Verify after failed original");var failedRequest=retakes.ForTeacher(teacher,active.Id);var proof=retakes.StartRetake(users[1],failedRequest.Id);quiz.Submit(proof.Id,users[1],Correct(proof));
  Check(quiz.Attempt(proof.Id).Passed==true&&quiz.Student(id,users[1]).Passed&&quiz.Student(id,users[1]).BestScore==10m,"passing retake replaces failed original result");quiz.ClaimCertificate(id,users[1]);Check(quiz.Student(id,users[1]).IssuedAt.HasValue,"passing replacement qualifies for certificate");
  Reject(()=>quiz.Start(id,users[1]),"cannot start ordinary quiz after passing replacement");
  var recovery=quiz.Start(id,users[0]);Check(!recovery.IsVerification&&quiz.Student(id,users[0]).Attempts==2,"unused ordinary allowance remains after failed retake");quiz.Submit(recovery.Id,users[0],Correct(recovery));quiz.ClaimCertificate(id,users[0]);Check(quiz.Student(id,users[0]).IssuedAt.HasValue&&quiz.Student(id,users[0]).Code!=code,"requalification issues a new certificate code, leaving revoked code invalid");
  var visibility=new QuizVisibilityService(db);var limited=quiz.Start(id,users[2]);
  for(int i=0;i<2;i++)visibility.Record(users[2],limited.Id,Guid.NewGuid().ToString("N"),false);
  quiz.Submit(limited.Id,users[2],new[]{-1,-1});Check(!quiz.Attempt(limited.Id).ReviewPending,"two departures do not hold result");
  var flagged=quiz.Start(id,users[2]);string duplicateKey=Guid.NewGuid().ToString("N");visibility.Record(users[2],flagged.Id,duplicateKey,false);visibility.Record(users[2],flagged.Id,duplicateKey,false);
  for(int i=0;i<2;i++)visibility.Record(users[2],flagged.Id,Guid.NewGuid().ToString("N"),false);
  quiz.SaveDraft(flagged.Id,users[2],Correct(flagged));db.Database.ExecuteSqlCommand("UPDATE dbo.QuizAttempt SET ExpiresAt=DATEADD(second,-1,GETUTCDATE()) WHERE Id=@p0",flagged.Id);quiz.CloseExpired(id,users[2]);
  var held=quiz.Attempt(flagged.Id);Check(held.Score==10m&&held.Passed==true&&held.ReviewPending&&held.TimedOut,"three departures hold even a passing automatically expired submission");
  Check(visibility.ForTeacher(teacher,held.Id).Count==3,"duplicate departure event is counted once");
  Check(!quiz.Student(id,users[2]).Passed&&quiz.Student(id,users[2]).PendingReview&&quiz.Student(id,users[2]).BestScore==null,"held grade is not recognized for course completion");
  Reject(()=>quiz.ClaimCertificate(id,users[2]),"held grade cannot claim certificate");Check(!quiz.CertificateCourseIds(users[2]).Contains(id),"held result is absent from certificate tab");Reject(()=>quiz.Start(id,users[2]),"held learner cannot bypass review with ordinary start");
  var marker=visibility.ForTeacher(teacher,held.Id).Max(x=>x.Id);visibility.MarkReviewed(teacher,held.Id,marker);Check(visibility.UnreadCount(teacher)==1&&visibility.Alerts(teacher,id).Single().PendingReview,"opening log does not dismiss pending decision reminder");
  Reject(()=>visibility.ApproveResult(users[1],held.Id,"Foreign"),"only owning teacher can recognize result");Reject(()=>visibility.ApproveResult(teacher,held.Id," "),"teacher recognition requires recorded reason");
  visibility.ApproveResult(teacher,held.Id,"Reviewed the answers and screen departure log.");Check(!quiz.Attempt(held.Id).ReviewPending&&quiz.Student(id,users[2]).Passed&&quiz.Student(id,users[2]).BestScore==10m,"teacher approval recognizes existing result without changing score");
  Check(quiz.Attempt(held.Id).ReviewApprovedBy==teacher&&quiz.Attempt(held.Id).ReviewNote.Contains("Reviewed"),"teacher decision identity and reason remain recorded");Check(visibility.UnreadCount(teacher)==0,"resolved seen decision clears menu reminder");Reject(()=>visibility.ApproveResult(teacher,held.Id,"Again"),"approved result cannot be approved twice");quiz.ClaimCertificate(id,users[2]);Check(quiz.Student(id,users[2]).IssuedAt.HasValue,"approved passing grade can receive certificate");
  retakes.RequestRetake(teacher,held.Id,"Do the replacement test.");var repeat=retakes.StartRetake(users[2],retakes.ForTeacher(teacher,held.Id).Id);for(int i=0;i<3;i++)visibility.Record(users[2],repeat.Id,Guid.NewGuid().ToString("N"),false);quiz.Submit(repeat.Id,users[2],Correct(repeat));Check(quiz.Attempt(repeat.Id).ReviewPending&&quiz.Student(id,users[2]).PendingReview,"replacement attempt also requires review after three departures");Reject(()=>visibility.ApproveResult(teacher,held.Id,"Restore superseded original"),"superseded original cannot be restored by approval");visibility.ApproveResult(teacher,repeat.Id,"Checked replacement result.");Check(quiz.Student(id,users[2]).Passed&&!quiz.Student(id,users[2]).PendingReview,"replacement result becomes recognized after teacher approval");
  Console.WriteLine(checks+" retake checks passed.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{
  db.Database.ExecuteSqlCommand(@"DELETE e FROM dbo.QuizIntegrityEvent e JOIN dbo.QuizIntegrityReview r ON r.Id=e.ReviewId JOIN dbo.QuizAttempt a ON a.Id=r.AttemptId WHERE a.CourseId=@p0;
DELETE v FROM dbo.QuizCertificateRevocation v JOIN dbo.QuizIntegrityReview r ON r.Id=v.ReviewId JOIN dbo.QuizAttempt a ON a.Id=r.AttemptId WHERE a.CourseId=@p0;
DELETE r FROM dbo.QuizIntegrityReview r JOIN dbo.QuizAttempt a ON a.Id=r.AttemptId WHERE a.CourseId=@p0;
DELETE g FROM dbo.QuizGradeAudit g JOIN dbo.QuizAttempt a ON a.Id=g.AttemptId WHERE a.CourseId=@p0;
DELETE dbo.QuizCertificate WHERE CourseId=@p0;DELETE dbo.QuizAttemptGrant WHERE CourseId=@p0;
DELETE s FROM dbo.QuizVisibilitySeen s JOIN dbo.QuizAttempt a ON a.Id=s.AttemptId WHERE a.CourseId=@p0;DELETE v FROM dbo.QuizVisibility v JOIN dbo.QuizAttempt a ON a.Id=v.AttemptId WHERE a.CourseId=@p0;
DELETE w FROM dbo.QuizWriter w JOIN dbo.QuizAttempt a ON a.Id=w.AttemptId WHERE a.CourseId=@p0;DELETE dbo.QuizAttempt WHERE CourseId=@p0;DELETE dbo.QuizConfig WHERE CourseId=@p0;DELETE dbo.QuizQuestion WHERE CourseId=@p0;
DELETE dbo.TienDoBaiHoc WHERE maBaiHoc=@p1;DELETE dbo.TienDoHocTap WHERE maKhoaHoc=@p0;DELETE dbo.DangKyKhoaHoc WHERE maKhoaHoc=@p0;DELETE dbo.BaiHoc WHERE maBaiHoc=@p1;DELETE dbo.ChuongHoc WHERE maKhoaHoc=@p0;DELETE dbo.KhoaHoc WHERE maKhoaHoc=@p0 AND tenKhoaHoc=N'Retake fixture course';",id,id+"_1");
  foreach(var user in users)db.Database.ExecuteSqlCommand("DELETE dbo.NguoiDung WHERE maNguoiDung=@p0 AND email LIKE @p1",user,id+"%@test.invalid");if(teacher>0)db.Database.ExecuteSqlCommand("DELETE dbo.NguoiDung WHERE maNguoiDung=@p0 AND email=@p1",teacher,id+"t@test.invalid");Console.WriteLine("Retake fixtures removed.");
 }
 }
}
