// Integration checks use isolated temporary records and remove them in finally.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;
using Newtonsoft.Json;

class QuizIntegration
{
    static int checks;
    static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);checks++;}
    static void Reject(Action action,string name){try{action();}catch(InvalidOperationException){Check(true,name);return;}throw new Exception("Expected rejection: "+name);}
    static void Sql(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db,string sql,string course){db.Database.ExecuteSqlCommand(sql,new SqlParameter("@c",course));}
    static int Main()
    {
        string course="QT"+Guid.NewGuid().ToString("N").Substring(0,10),email=course+"@quiz.test";int student=0,second=0;
        using(var db=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1()) {
            try {
                var teacher=db.NguoiDung.First(u=>u.vaiTro=="GiaoVien");
                var category=db.DanhMuc.First();
                var user=new NguoiDung{hoTen="Quiz integration",email=email,matKhau="Test-only",vaiTro="HocVien",trangThai=true};
                var user2=new NguoiDung{hoTen="Quiz integration 2",email="2"+email,matKhau="Test-only",vaiTro="HocVien",trangThai=true};
                db.NguoiDung.Add(user);db.NguoiDung.Add(user2);db.SaveChanges();student=user.maNguoiDung;second=user2.maNguoiDung;
                db.KhoaHoc.Add(new KhoaHoc{maKhoaHoc=course,tenKhoaHoc="Quiz integration temporary",maGiaoVien=teacher.maNguoiDung,maDanhMuc=category.maDanhMuc,giaBan=0,trangThai="Đã xuất bản",capDo="Cơ bản"});db.SaveChanges();
                db.ChuongHoc.Add(new ChuongHoc{maChuong=course+"C",maKhoaHoc=course,tenChuong="Test",thuTu=1});
                db.SaveChanges();db.BaiHoc.Add(new BaiHoc{maBaiHoc=course+"_1",maChuong=course+"C",tenBaiHoc="Test",thuTu=1,thoiLuong=60});db.SaveChanges();
                foreach(int id in new[]{student,second})db.DangKyKhoaHoc.Add(new DangKyKhoaHoc{maDangKy=course+id,maHocVien=id,maKhoaHoc=course,ngayDangKy=DateTime.Now,trangThaiDK="Đang học"});db.SaveChanges();
                var service=new QuizService(db);
                for(int d=1;d<=3;d++)service.SaveQuestion(new QuizQuestion{CourseId=course,Topic="Chủ đề thử",Difficulty=d,Text="Question "+d,Options=new[]{"Đáp án đúng","Sai 1","Sai 2","Sai 3"},CorrectOption=0,Explanation="Snapshot explanation",ShuffleOptions=d!=2});
                var qs=service.Questions(course,true);
                var config=new QuizConfig{CourseId=course,Title="Quiz integration",Minutes=1,PassPercent=80,MaxAttempts=3,EasyCount=1,MediumCount=1,HardCount=0,Published=true};
                service.SaveConfig(config);
                Reject(()=>service.Start(course,student),"incomplete lessons cannot start");
                Reject(()=>service.ClaimCertificate(course,student),"no quiz pass cannot request certificate");
                config.HardCount=2;Reject(()=>service.SaveConfig(config),"insufficient difficulty pool rejected");config.HardCount=0;
                config.RandomSelection=false;config.SelectedIdsJson=JsonConvert.SerializeObject(new[]{qs[0].Id,qs[1].Id});service.SaveConfig(config);
                foreach(int id in new[]{student,second})db.TienDoBaiHoc.Add(new TienDoBaiHoc{maHocVien=id,maBaiHoc=course+"_1",daHoanThanh=true,ngayHoanThanh=DateTime.Now});db.SaveChanges();
                Check(service.Student(course,student).Progress==100,"canonical lesson progress");
                var a=service.Start(course,student);
                var writers=new QuizWriterService(db);string client1=Guid.NewGuid().ToString("N"),client2=Guid.NewGuid().ToString("N");
                string writer1=writers.Acquire(a.Id,student,client1,false);
                Check(writers.Acquire(a.Id,student,client1,false)==writer1,"same writer resumes without rotating token");
                Reject(()=>writers.Acquire(a.Id,second,client2,true),"other learner cannot take over");
                Reject(()=>writers.Acquire(a.Id,student,client2,false),"second client needs explicit takeover");
                Check(service.SaveDraft(a.Id,student,new[]{0,-1},writer1,true),"writer can save");
                Reject(()=>service.SaveDraft(a.Id,student,new[]{1,1},null,true),"missing token cannot save");
                string writer2=writers.Acquire(a.Id,student,client2,true);
                Reject(()=>service.SaveDraft(a.Id,student,new[]{1,1},writer1,true),"old writer cannot overwrite after takeover");
                Reject(()=>service.Submit(a.Id,student,new[]{1,1},writer1,true),"old writer cannot submit early");
                Check(service.SaveDraft(a.Id,student,new[]{0,-1},writer2,true),"new writer can save");
                Check(service.Start(course,student).Id==a.Id,"resume does not consume another attempt");
                Check(service.Attempt(a.Id).ExpiresAt==a.ExpiresAt,"takeover does not reset deadline");
                Check(new QuizWriterService(db).Active(student),"chatbot is blocked by active attempt");
                Check(!new QuizWriterService(db).Active(second),"another student's quiz does not block chatbot");
Check(service.Start(course,student).Id==a.Id,"reload reuses active attempt");
                var snapshot=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson);
                Check(snapshot.Count==2 && snapshot.All(q=>q.Options[q.CorrectOption]=="Đáp án đúng"),"shuffled answers preserve correct answer");
                Check(snapshot.Single(q=>q.Difficulty==2).CorrectOption==0,"question opt-out preserves answer order");
                var edited=qs[0];edited.Text="Changed after start";edited.Options[0]="New correct answer";service.SaveQuestion(edited);
                Check(service.Attempt(a.Id).SnapshotJson==a.SnapshotJson,"editing bank cannot change active attempt");
                Reject(()=>service.Submit(a.Id,second,new[]{0,0}),"other student cannot submit attempt");
                service.Submit(a.Id,student,new int[0]);Check(service.Attempt(a.Id).Score==0,"unanswered questions score zero");
                Reject(()=>service.ClaimCertificate(course,student),"failed quiz cannot request certificate");
                var tasks=Enumerable.Range(0,2).Select(_=>Task.Run(()=>{using(var other=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())return new QuizService(other).Start(course,student).Id;})).ToArray();Task.WaitAll(tasks);
                Check(tasks[0].Result==tasks[1].Result,"concurrent starts create one attempt");
                a=service.Attempt(tasks[0].Result);snapshot=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson);
                service.Submit(a.Id,student,snapshot.Select(q=>q.CorrectOption).ToArray());
                Check(service.Attempt(a.Id).Score==10 && service.Attempt(a.Id).Passed==true,"server grades correct shuffled submission");
                var submittedId=a.Id;
                var submitTasks=Enumerable.Range(0,2).Select(_=>Task.Run(()=>{using(var other=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())new QuizService(other).Submit(submittedId,student,new int[0]);})).ToArray();Task.WaitAll(submitTasks);
                Check(service.Attempt(a.Id).Score==10,"concurrent submissions stay idempotent without deadlock");
                service.Submit(a.Id,student,new int[0]);Check(service.Attempt(a.Id).Score==10,"duplicate submit cannot overwrite grade");
                Reject(()=>service.Start(course,student),"passed learner cannot start again");
                service.ClaimCertificate(course,student);service.ClaimCertificate(course,student);

                var state=service.Student(course,student);
                Check(state.IssuedAt.HasValue && state.Code.StartsWith("CERT-"),"certificate claim automatically issues without approval");
                Check(db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.QuizCertificate WHERE CourseId=@c AND StudentId=@s AND IssuedBy IS NULL",new SqlParameter("@c",course),new SqlParameter("@s",student)).Single()==1,"repeated automatic claim keeps one certificate without teacher approval");
                db.Database.ExecuteSqlCommand("UPDATE dbo.QuizCertificate SET IssuedAt=NULL WHERE CourseId=@c AND StudentId=@s",new SqlParameter("@c",course),new SqlParameter("@s",student));
                service.ClaimCertificate(course,student);
                Check(service.Student(course,student).IssuedAt.HasValue && service.Student(course,student).Code==state.Code,"legacy pending certificate is automatically issued with same code");
                Check(state.Attempts==2 && state.BestScore==10,"best score selected over failed attempt");
                config.MaxAttempts=1;service.SaveConfig(config);a=service.Start(course,second);
                snapshot=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson);
                Check(service.SaveDraft(a.Id,second,new[]{snapshot[0].CorrectOption,-1}),"answers saved on server during attempt");
                db.Database.ExecuteSqlCommand("UPDATE dbo.QuizAttempt SET ExpiresAt=DATEADD(MINUTE,-1,SYSUTCDATETIME()) WHERE Id=@id",new SqlParameter("@id",a.Id));
                Check(!service.SaveDraft(a.Id,second,snapshot.Select(q=>q.CorrectOption).ToArray()),"late draft cannot change saved answers");
                service.Submit(a.Id,second,snapshot.Select(q=>q.CorrectOption).ToArray());Check(service.Attempt(a.Id).Score==5 && service.Attempt(a.Id).SubmittedAt.HasValue,"expired attempt grades saved draft and ignores forged late answers");
                Check(service.Attempt(a.Id).TimedOut && service.Attempt(a.Id).SubmittedAt==service.Attempt(a.Id).ExpiresAt,"expired grade records actual deadline and timeout reason");
                Reject(()=>service.Start(course,second),"attempt limit enforced");
                Reject(()=>service.GrantAttempt(course,second,student,"Test"),"only course owner can grant another attempt");
                Reject(()=>service.GrantAttempt(course,second,teacher.maNguoiDung,""),"extra attempt requires recorded reason");
                service.GrantAttempt(course,second,teacher.maNguoiDung,"Reviewed failed attempt");
                Check(service.Student(course,second).ExtraAttempts==1 && service.Student(course,student).ExtraAttempts==0,"extra attempt applies only to selected learner-course");
                Reject(()=>service.GrantAttempt(course,second,teacher.maNguoiDung,"Duplicate click"),"duplicate grant cannot create unused extra attempts");
                a=service.Start(course,second);Check(service.Attempts(course,second).Count==2,"teacher grant permits exactly one additional attempt");
                service.Archive(course,qs[0].Id);
                Check(service.Config(course).Published==false,"insufficient fixed bank automatically unpublishes quiz");
                Check(service.Start(course,second).Id==a.Id,"closing quiz does not interrupt an active snapshot");
                user2.trangThai=false;db.SaveChanges();
                db.Database.ExecuteSqlCommand("UPDATE dbo.QuizAttempt SET ExpiresAt=DATEADD(MINUTE,-1,SYSUTCDATETIME()) WHERE Id=@id",new SqlParameter("@id",a.Id));
                var gradebook=service.Gradebook(teacher.maNguoiDung,course);
                Check(!gradebook.Single(r=>r.StudentId==second).HasOpenAttempt && service.Attempt(a.Id).TimedOut,"gradebook finalizes expired work even for disabled learner");
                Check(service.Student(course,student).BestScore==10,"archiving bank preserves historical grades");
                db.BaiHoc.Add(new BaiHoc{maBaiHoc=course+"_2",maChuong=course+"C",tenBaiHoc="Added after certificate",thuTu=2,thoiLuong=60});db.SaveChanges();
                state=service.Student(course,student);
                Check(!state.LessonsDone && state.Passed && state.CertificateStatus=="Đã cấp","new lessons do not revoke previous quiz pass or issued certificate");
                service.ClaimCertificate(course,student);
                Check(service.Student(course,student).Code==state.Code,"repeated issuance preserves historical certificate code");
                Check(new QuizStudentRow{TotalLessons=200,CompletedLessons=199}.Progress==99,"incomplete lesson progress never rounds to 100 percent");
                var errors=new List<string>();var excel=QuizExcel.Write(QuizExcel.Headers,new[]{new[]{"Chủ đề Unicode","3","Nội dung < > &","Đúng","Sai A","Sai B","Sai C","A","Giải thích","0"}});
                List<QuizQuestion> imported;using(var stream=new MemoryStream(excel))imported=QuizExcel.Read(stream,course,errors);
                Check(errors.Count==0 && imported.Count==1 && imported[0].Text=="Nội dung < > &" && !imported[0].ShuffleOptions,"xlsx template roundtrip Unicode and options");
                using(var stream=new MemoryStream(QuizExcel.Write(QuizExcel.Headers,new[]{new[]{"Topic","4","Invalid","A","B","C","D","A","","1"}})))QuizExcel.Read(stream,course,errors);
                Check(errors.Count>0,"Excel reports invalid row before saving");
                Check(service.Students(teacher.maNguoiDung,course).Count==2,"separate learner-course result rows");
                Console.WriteLine("ALL "+checks+" CHECKS PASSED");return 0;
            }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
            finally {
                Sql(db,@"DELETE dbo.QuizAttemptGrant WHERE CourseId=@c;DELETE dbo.QuizCertificate WHERE CourseId=@c;DELETE dbo.QuizWriter WHERE AttemptId IN (SELECT Id FROM dbo.QuizAttempt WHERE CourseId=@c);DELETE dbo.QuizAttempt WHERE CourseId=@c;DELETE dbo.QuizConfig WHERE CourseId=@c;DELETE dbo.QuizQuestion WHERE CourseId=@c;
DELETE dbo.TienDoBaiHoc WHERE maBaiHoc=@c+'_1';DELETE dbo.TienDoHocTap WHERE maKhoaHoc=@c;DELETE dbo.DangKyKhoaHoc WHERE maKhoaHoc=@c;
DELETE dbo.BaiHoc WHERE maChuong=@c+'C';DELETE dbo.ChuongHoc WHERE maKhoaHoc=@c;DELETE dbo.KhoaHoc WHERE maKhoaHoc=@c;",course);
                db.Database.ExecuteSqlCommand("DELETE dbo.NguoiDung WHERE email IN (@e,@e2)",new SqlParameter("@e",email),new SqlParameter("@e2","2"+email));
                Console.WriteLine("Temporary quiz fixtures removed.");
            }
        }
    }
}
