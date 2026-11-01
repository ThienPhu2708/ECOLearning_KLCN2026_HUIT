using System;
using System.Linq;
using System.Collections.Generic;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;
using Newtonsoft.Json;
class QuizAssessmentIntegration {
 static int checks;static void Check(bool ok,string text){if(!ok)throw new Exception("FAIL: "+text);Console.WriteLine("PASS: "+text);checks++;}
 static void Reject(Action action,string text){try{action();}catch(InvalidOperationException){Check(true,text);return;}throw new Exception("Expected rejection: "+text);}
 static int[] Answers(QuizAttempt a,int wrongId){return JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson).Select(q=>q.Id==wrongId?1:0).ToArray();}
 static int Main(){string id="QA"+Guid.NewGuid().ToString("N").Substring(0,10);var users=new List<int>();int teacher=0;
 using(var db=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())try{
 var owner=new NguoiDung{hoTen="Assessment fixture teacher",email=id+"t@test.invalid",matKhau="test-only",vaiTro="GiaoVien",trangThai=true};db.NguoiDung.Add(owner);db.SaveChanges();teacher=owner.maNguoiDung;
 db.KhoaHoc.Add(new KhoaHoc{maKhoaHoc=id,tenKhoaHoc="Assessment integration fixture",maGiaoVien=teacher,maDanhMuc=db.DanhMuc.First().maDanhMuc,giaBan=0,trangThai="Đã xuất bản"});db.SaveChanges();
 db.ChuongHoc.Add(new ChuongHoc{maChuong=id+"c",maKhoaHoc=id,tenChuong="Fixture",thuTu=1});db.SaveChanges();db.BaiHoc.Add(new BaiHoc{maBaiHoc=id+"_1",maChuong=id+"c",tenBaiHoc="Fixture",thuTu=1,thoiLuong=60});db.SaveChanges();
 for(int i=0;i<6;i++){var u=new NguoiDung{hoTen="Assessment fixture student "+i,email=id+i+"@test.invalid",matKhau="test-only",vaiTro="HocVien",trangThai=true};db.NguoiDung.Add(u);db.SaveChanges();users.Add(u.maNguoiDung);db.DangKyKhoaHoc.Add(new DangKyKhoaHoc{maDangKy=id+i,maKhoaHoc=id,maHocVien=u.maNguoiDung,ngayDangKy=DateTime.Now});db.TienDoBaiHoc.Add(new TienDoBaiHoc{maHocVien=u.maNguoiDung,maBaiHoc=id+"_1",daHoanThanh=true,ngayHoanThanh=DateTime.Now});}db.SaveChanges();
 var assessment=new QuizAssessmentService(db);var quiz=new QuizService(db);var regrade=new QuizRegradeService(db);
 assessment.AddSkill(id,teacher,"Syntax");assessment.AddSkill(id,teacher,"Loops");var skills=assessment.Skills(id);var syntax=skills.Single(x=>x.Name=="Syntax");var loops=skills.Single(x=>x.Name=="Loops");
 Reject(()=>assessment.AddSkill(id,users[0],"Foreign"),"only course teacher manages skills");
 for(int i=0;i<4;i++)quiz.SaveQuestion(new QuizQuestion{CourseId=id,Topic="Topic",Text="Question "+i,Options=new[]{"A","B","C","D"},CorrectOption=0,Difficulty=i<2?1:2,SkillId=i<2?syntax.Id:loops.Id,ShuffleOptions=false});
 var bank=quiz.Questions(id);int target=bank.Where(q=>q.SkillId==syntax.Id).Last().Id;
 var config=new QuizConfig{CourseId=id,Title="Fixture quiz",Minutes=10,PassPercent=50,MaxAttempts=3,EasyCount=2,MediumCount=2,HardCount=0,Published=true,ShuffleAnswers=false,ShuffleQuestions=false};quiz.SaveConfig(config);
 var legacy=quiz.Start(id,users[0]);quiz.Submit(legacy.Id,users[0],Answers(legacy,target));Check(quiz.Attempt(legacy.Id).Passed==true,"legacy quiz has no retroactive skill gate");
 config.UseMatrix=true;config.SkillRules=new List<QuizSkillRule>{new QuizSkillRule{SkillId=syntax.Id,Easy=2,PassPercent=100,Required=true},new QuizSkillRule{SkillId=loops.Id,Medium=2,PassPercent=50,Required=true}};quiz.SaveConfig(config);
 config.SkillRules[0].Easy=0;Reject(()=>quiz.SaveConfig(config),"required skill cannot have zero questions");config.SkillRules[0].Easy=3;Reject(()=>quiz.SaveConfig(config),"publishing rejects insufficient skill difficulty pool");config.SkillRules[0].Easy=2;quiz.SaveConfig(config);
 var first=quiz.Start(id,users[1]);var certified=quiz.Start(id,users[2]);var pending=quiz.Start(id,users[4]);var laterSubmit=quiz.Start(id,users[5]);
 Check(JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(first.SnapshotJson).All(q=>q.SkillId.HasValue),"matrix snapshots tag every selected question");
 config.SkillRules[0].PassPercent=50;quiz.SaveConfig(config);quiz.Submit(first.Id,users[1],Answers(first,target));
 first=quiz.Attempt(first.Id);Check(first.Score==7.5m && first.Passed==false,"high total cannot bypass frozen required skill threshold");
 var results=JsonConvert.DeserializeObject<List<QuizSkillResult>>(first.SkillResultsJson);Check(results.Single(r=>r.SkillId==syntax.Id).PassPercent==100,"changing config cannot lower existing attempt skill gate");
 quiz.Submit(certified.Id,users[2],Answers(certified,-1));certified=quiz.Attempt(certified.Id);quiz.ClaimCertificate(id,users[2]);var certificate=quiz.Student(id,users[2]).Code;
 var edited=bank.Single(q=>q.Id==target);edited.Text="A different version";quiz.SaveQuestion(edited);var otherVersion=quiz.Start(id,users[3]);quiz.Submit(otherVersion.Id,users[3],Answers(otherVersion,target));
 regrade.Appeal(users[1],first.Id,"Please check grading");Reject(()=>regrade.Appeal(users[2],first.Id,"Other account"),"appeal is isolated to attempt owner");Reject(()=>regrade.Appeal(users[1],first.Id,"Duplicate"),"duplicate appeal rejected");
 var secondAppealAttempt=quiz.Start(id,users[1]);quiz.Submit(secondAppealAttempt.Id,users[1],Answers(secondAppealAttempt,target));
 Reject(()=>regrade.Appeal(users[1],secondAppealAttempt.Id,"Another attempt same course"),"one appeal per learner-course across multiple attempts");
 var appeal=regrade.Appeals(teacher,true).Single();regrade.ReplyAppeal(teacher,appeal.Id,"Processing","Checking the answer");regrade.ReplyAppeal(teacher,appeal.Id,"Answered","The question will be reviewed");Check(regrade.Appeals(users[1],false).Single().Replies.Count==2,"appeal replies are append-only history");
 Reject(()=>regrade.Appeal(users[1],secondAppealAttempt.Id,"After teacher answered"),"teacher reply does not reset appeal allowance");
 Reject(()=>regrade.ReplyAppeal(users[0],appeal.Id,"Answered","No access"),"another user cannot resolve appeal");
 int report=regrade.QuestionFromAppeal(teacher,appeal.Id,target);
 Reject(()=>regrade.Preview(users[0],report,new[]{1},false,"Reason"),"another teacher cannot preview correction");
 var stale=regrade.Preview(teacher,report,new[]{1},false,"Accept B; wrong key originally A");
 Check(stale.Lines.Any(x=>x.Attempt.Id==first.Id) && stale.Lines.All(x=>x.Attempt.Id!=otherVersion.Id),"preview covers every old version and excludes edited version");
 Check(stale.OpenAttempts==2,"unfinished affected attempts shown separately");
 quiz.Submit(laterSubmit.Id,users[5],Answers(laterSubmit,-1));Reject(()=>regrade.Confirm(stale.Id,teacher),"new submission invalidates stale preview");
 var plan=regrade.Preview(teacher,report,new[]{1},false,"Confirmed: accept B instead of A");var certLine=plan.Lines.Single(x=>x.Attempt.Id==certified.Id);
 Check(certLine.Grade.CalculatedScore<certLine.Attempt.Score && certLine.Grade.Score==certLine.Attempt.Score,"preview never reduces recognized score when recalculated score drops");
 var duplicatePlan=regrade.Preview(teacher,report,new[]{1},false,"Same correction from another tab");
 var expiredPlan=regrade.Preview(teacher,report,new[]{1},false,"Expired preview");db.Database.ExecuteSqlCommand("UPDATE dbo.QuizRegradePreview SET ExpiresAt=DATEADD(minute,-1,GETUTCDATE()) WHERE Id=@p0",expiredPlan.Id);Reject(()=>regrade.Confirm(expiredPlan.Id,teacher),"expired preview cannot confirm");
 regrade.Confirm(plan.Id,teacher);Reject(()=>regrade.Confirm(duplicatePlan.Id,teacher),"second tab cannot apply duplicate version correction");Reject(()=>regrade.Confirm(plan.Id,teacher),"confirmation replay rejected");
 Check(quiz.Attempt(first.Id).Passed==true && quiz.Attempt(first.Id).Score==10m,"correction updates failed attempt to passed with skill gate satisfied");
 Check(quiz.Student(id,users[2]).Code==certificate,"issued certificate code and rights are preserved");
 Check(quiz.Attempt(otherVersion.Id).Score==7.5m,"different question version untouched");
 Check(regrade.GradeHistory(teacher,true).Count>0 && regrade.GradeHistory(users[0],true).Count==0,"teacher history isolated to owned courses");
 Check(regrade.Notices(users[1]).Count==1 && regrade.Notices(users[3]).Count==0,"adjustment notifications visible only to affected student");
 quiz.Submit(pending.Id,users[4],Answers(pending,target));Check(quiz.Attempt(pending.Id).Passed==true && regrade.Notices(users[4]).Count==1,"pending attempt applies correction and notification on submission");
 var reversed=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(legacy.SnapshotJson);var q0=reversed.First(q=>q.Id==target);q0.Options=new[]{"D","C","B","A"};q0.CorrectOption=3;
 var original=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(legacy.SnapshotJson).First(q=>q.Id==target);Check(QuizAssessmentService.Version(original)==QuizAssessmentService.Version(q0),"version matching survives answer shuffle");
 var shuffledAttempt=new QuizAttempt{SnapshotJson=JsonConvert.SerializeObject(new[]{q0}),AnswersJson="[2]",PassPercent=100};
 var shuffledRule=new QuizCorrection{QuestionId=target,VersionKey=QuizAssessmentService.Version(original),AcceptedJson="[\"B\"]"};
 Check(QuizAssessmentService.Evaluate(shuffledAttempt,new List<QuizCorrection>{shuffledRule}).CalculatedScore==10m,"accepted answer content maps correctly through shuffle");
 shuffledRule.AllCredit=true;shuffledAttempt.AnswersJson="[-1]";Check(QuizAssessmentService.Evaluate(shuffledAttempt,new List<QuizCorrection>{shuffledRule}).Score==10m,"invalid question credit applies even to unanswered question");
 var beforeBank=quiz.Questions(id).First();beforeBank.SkillId=int.MaxValue;Reject(()=>quiz.SaveQuestion(beforeBank),"question cannot use another or missing skill");
 assessment.AddSkill(id,teacher,"Mistaken unused skill");var unused=assessment.Skills(id).Single(x=>x.Name=="Mistaken unused skill");
 Reject(()=>assessment.DeleteSkill(id,users[0],unused.Id),"only owning teacher can delete skill");
 Reject(()=>assessment.DeleteSkill(id,teacher,int.MaxValue),"missing or foreign skill cannot be deleted");
 assessment.DeleteSkill(id,teacher,unused.Id);Check(!assessment.Skills(id).Any(x=>x.Id==unused.Id),"unused mistaken skill can be deleted");
 Reject(()=>assessment.DeleteSkill(id,teacher,syntax.Id),"matrix skill cannot be deleted");
 assessment.AddSkill(id,teacher,"Question-only skill");var attached=assessment.Skills(id).Single(x=>x.Name=="Question-only skill");
 quiz.SaveQuestion(new QuizQuestion{CourseId=id,Topic="Topic",Text="Attached question",Options=new[]{"A","B","C","D"},CorrectOption=0,SkillId=attached.Id});
 Reject(()=>assessment.DeleteSkill(id,teacher,attached.Id),"question skill cannot be deleted even outside matrix");
 Check(quiz.Student(id,users[2]).Code==certificate&&quiz.Attempt(first.Id).Passed==true,"skill deletion leaves historical grades and certificate unchanged");
 Func<string,QuizQuestion> makeQuestion=text=>new QuizQuestion{CourseId=id,Topic="Inline skill",Text=text,Options=new[]{"A","B","C","D"},CorrectOption=0};
 var inline=makeQuestion("Create with new skill");assessment.SaveQuestionWithSkill(inline,teacher,"Inline created skill");
 Check(quiz.Questions(id).Single(q=>q.Id==inline.Id).SkillId==assessment.Skills(id).Single(x=>x.Name=="Inline created skill").Id,"question save creates and assigns new skill atomically");
 var reuse=makeQuestion("Reuse named skill");assessment.SaveQuestionWithSkill(reuse,teacher," inline CREATED skill ");Check(reuse.SkillId==inline.SkillId&&assessment.Skills(id).Count(x=>x.Name=="Inline created skill")==1,"inline name reuses existing skill ignoring case and whitespace");
 var picked=makeQuestion("Pick existing skill");picked.SkillId=syntax.Id;assessment.SaveQuestionWithSkill(picked,teacher,null);Check(picked.SkillId==syntax.Id,"existing skill selection is preserved without a new name");
 var invalidInline=makeQuestion("Invalid inline question");invalidInline.Options[1]="A";Reject(()=>assessment.SaveQuestionWithSkill(invalidInline,teacher,"Not created"),"invalid question cannot create orphan skill");Check(!assessment.Skills(id).Any(x=>x.Name=="Not created"),"validation failure leaves skill bank unchanged");
 var missingQuestion=makeQuestion("Missing question");missingQuestion.Id=int.MaxValue;Reject(()=>assessment.SaveQuestionWithSkill(missingQuestion,teacher,"Rolled back skill"),"failed update rolls back new skill");Check(!assessment.Skills(id).Any(x=>x.Name=="Rolled back skill"),"new skill is not retained when question update fails");
 Reject(()=>assessment.SaveQuestionWithSkill(makeQuestion("Foreign teacher"),users[0],"Forbidden skill"),"inline creation requires course teacher ownership");
 regrade.Appeal(users[2],certified.Id,"Independent learner request");Check(regrade.Appeals(users[2],false).Count==1,"one learner using appeal does not consume another learner allowance");
 Console.WriteLine(checks+" assessment checks passed.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{
 db.Database.ExecuteSqlCommand(@"DELETE g FROM dbo.QuizGradeAudit g JOIN dbo.QuizAttempt a ON a.Id=g.AttemptId WHERE a.CourseId=@p0;
 DELETE r FROM dbo.QuizAppealReply r JOIN dbo.QuizAppeal p ON p.Id=r.AppealId JOIN dbo.QuizAttempt a ON a.Id=p.AttemptId WHERE a.CourseId=@p0;
 DELETE p FROM dbo.QuizAppeal p JOIN dbo.QuizAttempt a ON a.Id=p.AttemptId WHERE a.CourseId=@p0;
 DELETE dbo.QuizRegradePreview WHERE CourseId=@p0;DELETE dbo.QuizCorrection WHERE CourseId=@p0;DELETE dbo.QuizQuestionReport WHERE CourseId=@p0;
 DELETE w FROM dbo.QuizWriter w JOIN dbo.QuizAttempt a ON a.Id=w.AttemptId WHERE a.CourseId=@p0;
 DELETE v FROM dbo.QuizVisibility v JOIN dbo.QuizAttempt a ON a.Id=v.AttemptId WHERE a.CourseId=@p0;
 DELETE dbo.QuizCertificate WHERE CourseId=@p0;DELETE dbo.QuizAttemptGrant WHERE CourseId=@p0;DELETE dbo.QuizAttempt WHERE CourseId=@p0;DELETE dbo.QuizConfig WHERE CourseId=@p0;DELETE dbo.QuizQuestion WHERE CourseId=@p0;DELETE dbo.QuizSkill WHERE CourseId=@p0;
 DELETE dbo.TienDoBaiHoc WHERE maBaiHoc=@p1;DELETE dbo.TienDoHocTap WHERE maKhoaHoc=@p0;DELETE dbo.DangKyKhoaHoc WHERE maKhoaHoc=@p0;DELETE dbo.BaiHoc WHERE maBaiHoc=@p1;DELETE dbo.ChuongHoc WHERE maKhoaHoc=@p0;DELETE dbo.KhoaHoc WHERE maKhoaHoc=@p0;",id,id+"_1");
 foreach(var u in users)db.Database.ExecuteSqlCommand("DELETE dbo.NguoiDung WHERE maNguoiDung=@p0 AND email LIKE @p1",u,id+"%@test.invalid");if(teacher>0)db.Database.ExecuteSqlCommand("DELETE dbo.NguoiDung WHERE maNguoiDung=@p0 AND email=@p1",teacher,id+"t@test.invalid");Console.WriteLine("Assessment fixtures removed.");}
 }
}
