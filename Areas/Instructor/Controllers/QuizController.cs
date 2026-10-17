using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ECOLearning.Services;
using ECOLearning.ViewModels;
using Newtonsoft.Json;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class QuizController : InstructorBaseController
    {
        private QuizService Service => new QuizService(db);
        private bool Course(string courseId)
        {
            if(!Service.Owns(courseId,CurrentTeacherId))return false;
            ViewBag.CourseId=courseId;ViewBag.CourseName=db.KhoaHoc.Find(courseId).tenKhoaHoc;return true;
        }
        public ActionResult Index(string courseId)
        {
            ViewBag.Courses=db.KhoaHoc.Where(k=>k.maGiaoVien==CurrentTeacherId).ToList();
            if(string.IsNullOrEmpty(courseId))
            {
                var courses = (List<ECOLearning.Models.KhoaHoc>)ViewBag.Courses;
                return View(courses.SelectMany(c => Service.Questions(c.maKhoaHoc)).OrderByDescending(q => q.Id).ToList());
            }
            if(!Course(courseId))return HttpNotFound();
            ViewBag.Config=Service.Config(courseId);return View(Service.Questions(courseId));
        }
        public ActionResult Edit(string courseId,int id=0)
        {
            if(!Course(courseId))return HttpNotFound();
            var q=id==0 ? new QuizQuestion{CourseId=courseId} : Service.Questions(courseId).SingleOrDefault(x=>x.Id==id && x.Active);
            if(q==null)return HttpNotFound();return View(q);
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult Edit(QuizQuestion model)
        {
            if(!Course(model.CourseId))return HttpNotFound();
            try {if(!ModelState.IsValid)throw new InvalidOperationException("Kiểm tra các trường thông tin.");bool wasOpen=Service.Config(model.CourseId)?.Published==true;Service.SaveQuestion(model);TempData["QuizMessage"]="Đã lưu câu hỏi."+(wasOpen && Service.Config(model.CourseId)?.Published!=true?" Quiz chuyển về nháp vì ngân hàng không còn đủ câu theo cấu hình. Hãy kiểm tra và mở lại quiz.":"");return RedirectToAction("Index",new{courseId=model.CourseId});}
            catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);return View(model);}
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult RenameTopic(string courseId,string oldTopic,string newTopic)
        {
            if(!Course(courseId))return HttpNotFound();
            try {Service.RenameTopic(courseId,oldTopic,newTopic);TempData["QuizMessage"]="Đã đổi tên chủ đề. Lượt thi cũ giữ nguyên.";}
            catch(InvalidOperationException ex){TempData["QuizMessage"]=ex.Message;}
            return RedirectToAction("Index",new{courseId});
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult Archive(string courseId,int id)
        {
            if(!Course(courseId))return HttpNotFound();bool wasOpen=Service.Config(courseId)?.Published==true;Service.Archive(courseId,id);TempData["QuizMessage"]="Đã ngừng dùng câu hỏi; các bài thi cũ vẫn được giữ nguyên."+(wasOpen && Service.Config(courseId)?.Published!=true?" Quiz chuyển về nháp do không còn đủ câu. Hãy cập nhật cấu hình trước khi mở lại.":"");return RedirectToAction("Index",new{courseId});
        }
        public ActionResult Configure(string courseId)
        {
            if(!Course(courseId))return HttpNotFound();ViewBag.Questions=Service.Questions(courseId,true);return View(Service.Config(courseId) ?? new QuizConfig{CourseId=courseId});
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult Configure(QuizConfig model,int[] selectedIds)
        {
            if(!Course(model.CourseId))return HttpNotFound();model.SelectedIdsJson=JsonConvert.SerializeObject(selectedIds ?? new int[0]);
            try {if(!ModelState.IsValid)throw new InvalidOperationException("Kiểm tra cấu hình.");Service.SaveConfig(model);TempData["QuizMessage"]="Đã lưu cấu hình. Các lượt thi đã bắt đầu giữ nguyên đề và điều kiện cũ.";return RedirectToAction("Index",new{courseId=model.CourseId});}
            catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);ViewBag.Questions=Service.Questions(model.CourseId,true);return View(model);}
        }
        public ActionResult Preview(string courseId)
        {
            if(!Course(courseId))return HttpNotFound();var c=Service.Config(courseId);
            if(c==null)return RedirectToAction("Configure",new{courseId});
            try {Service.ValidateConfig(c);}catch(InvalidOperationException ex){TempData["QuizMessage"]=ex.Message;return RedirectToAction("Configure",new{courseId});}
            ViewBag.Config=c;var pool=Service.Questions(courseId,true);
            var ids=JsonConvert.DeserializeObject<int[]>(c.SelectedIdsJson);
            pool=pool.Where(q=>c.RandomSelection ? string.IsNullOrWhiteSpace(c.Topic) || q.Topic==c.Topic : ids.Contains(q.Id)).ToList();
            var counts=new[]{c.EasyCount,c.MediumCount,c.HardCount};
            return View(pool.GroupBy(q=>q.Difficulty).SelectMany(g=>g.Take(counts[g.Key-1])).ToList());
        }
        public ActionResult Template() => File(QuizExcel.Write(QuizExcel.Headers,new[]{new[]{"Chương 1","1","2 + 2 bằng bao nhiêu?","3","4","5","6","B","2 + 2 = 4","1"}}),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","MauCauHoi.xlsx");
        public ActionResult Import(string courseId) {if(!Course(courseId))return HttpNotFound();return View();}
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult Import(string courseId,HttpPostedFileBase upload)
        {
            if(!Course(courseId))return HttpNotFound();var errors=new List<string>();var rows=new List<QuizQuestion>();
            try {
                if(upload==null || upload.ContentLength==0 || upload.ContentLength>2*1024*1024 || !upload.FileName.EndsWith(".xlsx",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Chọn file .xlsx, tối đa 2 MB.");
                rows=QuizExcel.Read(upload.InputStream,courseId,errors);
            }catch(Exception ex) when(ex is InvalidOperationException || ex is System.IO.InvalidDataException || ex is System.Xml.XmlException || ex is System.IO.IOException) {errors.Add("Không đọc được file Excel: "+ex.Message);}
            ViewBag.Errors=errors;
            if(errors.Count==0) {var token=Guid.NewGuid().ToString("N");TempData["QuizImport_"+token]=rows;ViewBag.Token=token;}
            return View("ImportPreview",rows);
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult ConfirmImport(string courseId,string token)
        {
            if(!Course(courseId))return HttpNotFound();var rows=TempData["QuizImport_"+token] as List<QuizQuestion>;
            if(rows==null || rows.Any(q=>q.CourseId!=courseId)) {TempData["QuizMessage"]="Bản xem trước đã hết hạn. Hãy tải lại file.";return RedirectToAction("Import",new{courseId});}
            using(var tx=db.Database.BeginTransaction()){foreach(var q in rows)Service.SaveQuestion(q);tx.Commit();}
            TempData["QuizMessage"]="Đã nhập "+rows.Count+" câu hỏi.";return RedirectToAction("Index",new{courseId});
        }
        public ActionResult Results(string courseId,string search="",string status="")
        {
            // Keep old bookmarks working; learner progress and grades now share one screen.
            return RedirectToAction("Index","Student",new{courseId,search,status});
        }
        internal static List<QuizStudentRow> Filter(List<QuizStudentRow> rows,string status)
        {
            switch(status) {
                case "notTaken":return rows.Where(r=>r.LessonsDone && r.Attempts==0).ToList();
                case "failed":return rows.Where(r=>r.BestScore.HasValue && !r.Passed && !r.HasOpenAttempt).ToList();
                case "passed":return rows.Where(r=>r.Passed).ToList();
                case "eligible":return rows.Where(r=>!r.IssuedAt.HasValue && r.LessonsDone && r.Passed).ToList();
                case "inProgress":return rows.Where(r=>r.HasOpenAttempt).ToList();
                case "issued":return rows.Where(r=>r.IssuedAt.HasValue).ToList();
                default:return rows;
            }
        }
        public ActionResult History(string courseId,int studentId)
        {
            if(!Course(courseId))return HttpNotFound();Service.CloseExpired(courseId,studentId);ViewBag.Student=Service.Student(courseId,studentId);
            ViewBag.Config=Service.Config(courseId);ViewBag.Grants=Service.Grants(courseId,studentId);
            ViewBag.Lessons=db.BaiHoc.Where(b=>(b.ChuongHoc!=null && b.ChuongHoc.maKhoaHoc==courseId) || (b.maChuong==null && b.maBaiHoc.StartsWith(courseId+"_"))).OrderBy(b=>b.thuTu).ToList();
            ViewBag.CompletedLessonIds=db.TienDoBaiHoc.Where(t=>t.maHocVien==studentId && t.daHoanThanh==true).Select(t=>t.maBaiHoc).ToList();
            return View(Service.Attempts(courseId,studentId));
        }
        public ActionResult Review(int id)
        {
            var a=Service.Attempt(id);if(a==null || !Course(a.CourseId))return HttpNotFound();
            if(!a.SubmittedAt.HasValue)return new HttpStatusCodeResult(409,"Bài thi đang làm.");
            ViewBag.ShowSolutions=true;return View("~/Views/Quiz/Take.cshtml",new QuizTakeViewModel{Attempt=a,Questions=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson),Answers=JsonConvert.DeserializeObject<int[]>(a.AnswersJson),Review=true});
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult GrantAttempt(string courseId,int studentId,string reason)
        {
            if(!Course(courseId))return HttpNotFound();
            try {Service.GrantAttempt(courseId,studentId,CurrentTeacherId,reason);TempData["QuizMessage"]="Đã cấp thêm một lượt riêng cho học viên này; điểm và lịch sử thi vẫn được giữ nguyên.";}
            catch(InvalidOperationException ex){TempData["QuizMessage"]=ex.Message;}
            return RedirectToAction("History",new{courseId,studentId});
        }
        public ActionResult Export(string courseId,string search="",string status="",bool history=false)
        {
            if(!string.IsNullOrEmpty(courseId) && !Service.Owns(courseId,CurrentTeacherId))return HttpNotFound();
            var rows=Filter(Service.Gradebook(CurrentTeacherId,courseId,search),status);string[] headers;IEnumerable<string[]> data;
            if(history) {
                headers=new[]{"Học viên","Email","Khóa học","Lượt làm","Bắt đầu (UTC)","Nộp (UTC)","Số câu đúng","Tổng câu","Điểm /10","Kết quả","Cách kết thúc"};
                data=rows.SelectMany(r=>Service.Attempts(r.CourseId,r.StudentId).Select(a=>new[]{r.Name,r.Email,r.CourseName,a.Id.ToString(),a.StartedAt.ToString("yyyy-MM-dd HH:mm"),a.SubmittedAt?.ToString("yyyy-MM-dd HH:mm") ?? "",a.Correct?.ToString() ?? "",a.Total.ToString(),a.Score?.ToString() ?? "",!a.SubmittedAt.HasValue?"Đang làm":a.Passed==true?"Đạt":"Chưa đạt",!a.SubmittedAt.HasValue?"":a.TimedOut?"Hết giờ":"Nộp bài"}));
            } else {
                headers=new[]{"Học viên","Email","Khóa học","Tiến độ %","Số lượt","Lượt được cấp thêm","Số câu đúng","Tổng câu","Điểm cao nhất /10","Quiz","Chứng chỉ","Mã chứng chỉ","Ngày cấp (UTC)"};
                data=rows.Select(r=>new[]{r.Name,r.Email,r.CourseName,r.Progress.ToString(),r.Attempts.ToString(),r.ExtraAttempts.ToString(),r.BestCorrect?.ToString() ?? "",r.BestTotal?.ToString() ?? "",r.BestScore?.ToString() ?? "",r.QuizStatus,r.CertificateStatus,r.Code ?? "",r.IssuedAt?.ToString("yyyy-MM-dd HH:mm") ?? ""});
            }
            return File(QuizExcel.Write(headers,data),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",history?"LichSuQuiz.xlsx":"KetQuaHocVien.xlsx");
        }
    }
}
