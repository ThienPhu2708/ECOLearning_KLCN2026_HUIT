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
            ViewBag.CourseId=courseId;ViewBag.CourseName=db.KhoaHoc.Find(courseId).tenKhoaHoc;ViewBag.Skills=new QuizAssessmentService(db).Skills(courseId);return true;
        }
        public ActionResult Index(string courseId)
        {
            ViewBag.Courses=db.KhoaHoc.Where(k=>k.maGiaoVien==CurrentTeacherId).ToList();
            ViewBag.QuizNotifications=new QuizQuestionReportService(db).TeacherNotifications(CurrentTeacherId,courseId);
            if(string.IsNullOrEmpty(courseId))
            {
                var courses = (List<ECOLearning.Models.KhoaHoc>)ViewBag.Courses;
                return View(courses.SelectMany(c => Service.Questions(c.maKhoaHoc)).OrderByDescending(q => q.Id).ToList());
            }
            if(!Course(courseId))return HttpNotFound();
            ViewBag.Config=Service.Config(courseId);return View(Service.Questions(courseId));
        }
        [HttpGet,OutputCache(NoStore=true,Duration=0,VaryByParam="*")]
        public ActionResult NotificationCounts(string courseId) {
            if(!string.IsNullOrWhiteSpace(courseId)&&!Service.Owns(courseId,CurrentTeacherId))return HttpNotFound();
            return Json(new QuizQuestionReportService(db).TeacherNotifications(CurrentTeacherId,courseId),JsonRequestBehavior.AllowGet);
        }
        public ActionResult Reports(string courseId) {
            if(!string.IsNullOrEmpty(courseId) && !Course(courseId))return HttpNotFound();
            ViewBag.IsTeacher=true;ViewBag.CourseId=courseId;ViewBag.BreadcrumbCurrent="Báo lỗi câu hỏi";
            try {return View("~/Views/Quiz/Reports.cshtml",new QuizQuestionReportService(db).List(CurrentTeacherId,true,courseId));}
            catch(InvalidOperationException ex){ViewBag.ReportError=ex.Message;return View("~/Views/Quiz/Reports.cshtml",new List<QuizQuestionReport>());}
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult RespondReport(int id,string status,string response) {
            try {new QuizQuestionReportService(db).Respond(CurrentTeacherId,id,status,response);TempData["ReportMessage"]="Đã cập nhật báo cáo.";}
            catch(InvalidOperationException ex){TempData["ReportMessage"]=ex.Message;}
            return RedirectToAction("Reports");
        }
        public ActionResult Edit(string courseId,int id=0)
        {
            if(!Course(courseId))return HttpNotFound();
            var q=id==0 ? new QuizQuestion{CourseId=courseId} : Service.Questions(courseId).SingleOrDefault(x=>x.Id==id && x.Active);
            if(q==null)return HttpNotFound();return View(q);
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult Edit(QuizQuestion model,string newSkillName)
        {
            if(!Course(model.CourseId))return HttpNotFound();ViewBag.NewSkillName=newSkillName;
            try {if(!ModelState.IsValid)throw new InvalidOperationException("Kiểm tra các trường thông tin.");bool wasOpen=Service.Config(model.CourseId)?.Published==true;new QuizAssessmentService(db).SaveQuestionWithSkill(model,CurrentTeacherId,newSkillName);TempData["QuizMessage"]="Đã lưu câu hỏi."+(wasOpen && Service.Config(model.CourseId)?.Published!=true?" Quiz chuyển về nháp vì ngân hàng không còn đủ câu theo cấu hình. Hãy kiểm tra và mở lại quiz.":"");return RedirectToAction("Index",new{courseId=model.CourseId});}
            catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);ViewBag.Skills=new QuizAssessmentService(db).Skills(model.CourseId);return View(model);}
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
            ViewBag.Config=c;return View(Service.SelectQuestions(c));
        }
        public ActionResult Skills(string courseId){if(!Course(courseId))return HttpNotFound();return View(new QuizAssessmentService(db).Skills(courseId));}
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult AddSkill(string courseId,string name){if(!Course(courseId))return HttpNotFound();try{new QuizAssessmentService(db).AddSkill(courseId,CurrentTeacherId,name);TempData["QuizMessage"]="Đã thêm kỹ năng.";}catch(InvalidOperationException ex){TempData["QuizMessage"]=ex.Message;}return RedirectToAction("Skills",new{courseId});}
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult DeleteSkill(string courseId,int id){if(!Course(courseId))return HttpNotFound();try{new QuizAssessmentService(db).DeleteSkill(courseId,CurrentTeacherId,id);TempData["QuizMessage"]="Đã xóa kỹ năng.";}catch(InvalidOperationException ex){TempData["QuizMessage"]=ex.Message;}return RedirectToAction("Skills",new{courseId});}
        public ActionResult Correction(int reportId){var report=new QuizQuestionReportService(db).List(CurrentTeacherId,true).SingleOrDefault(r=>r.Id==reportId);if(report==null)return HttpNotFound();ViewBag.CourseId=report.CourseId;return View(report);}
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult PreviewCorrection(int reportId,int[] accepted,bool allCredit=false,string reason=null){try{var plan=new QuizRegradeService(db).Preview(CurrentTeacherId,reportId,accepted,allCredit,reason);return RedirectToAction("CorrectionPreview",new{id=plan.Id});}catch(InvalidOperationException ex){TempData["ReportMessage"]=ex.Message;return RedirectToAction("Correction",new{reportId});}}
        public ActionResult CorrectionPreview(string id){try{return View(new QuizRegradeService(db).GetPreview(id,CurrentTeacherId));}catch(InvalidOperationException ex){TempData["ReportMessage"]=ex.Message;return RedirectToAction("Reports");}}
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult ConfirmCorrection(string id){try{new QuizRegradeService(db).Confirm(id,CurrentTeacherId);TempData["ReportMessage"]="Đã xác nhận. Toàn bộ lượt thi cùng phiên bản đã được xem xét; thông báo và lịch sử điểm đã lưu. Lượt đang làm áp dụng quy tắc khi nộp bài.";}catch(InvalidOperationException ex){TempData["ReportMessage"]=ex.Message;}return RedirectToAction("Reports");}
        public ActionResult GradeHistory(){return View(new QuizRegradeService(db).GradeHistory(CurrentTeacherId,true));}
        public ActionResult Appeals(string courseId){if(!string.IsNullOrWhiteSpace(courseId)&&!Course(courseId))return HttpNotFound();ViewBag.IsTeacher=true;ViewBag.CourseId=courseId;ViewBag.Notices=new List<QuizGradeAudit>();ViewBag.RetakeCases=new QuizRetakeService(db).ForTeacherList(CurrentTeacherId,courseId);var requests=new QuizRegradeService(db).Appeals(CurrentTeacherId,true);if(!string.IsNullOrWhiteSpace(courseId))requests=requests.Where(r=>r.CourseId==courseId).ToList();return View("~/Views/Quiz/Appeals.cshtml",requests);}
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult ReplyAppeal(int id,string status,string response){try{new QuizRegradeService(db).ReplyAppeal(CurrentTeacherId,id,status,response);TempData["AppealMessage"]="Đã lưu phản hồi cho học viên.";}catch(InvalidOperationException ex){TempData["AppealMessage"]=ex.Message;}return RedirectToAction("Appeals");}
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult AppealQuestion(int id,int questionId){try{return RedirectToAction("Correction",new{reportId=new QuizRegradeService(db).QuestionFromAppeal(CurrentTeacherId,id,questionId)});}catch(InvalidOperationException ex){TempData["AppealMessage"]=ex.Message;return RedirectToAction("Appeals");}}
        public ActionResult Template() => File(QuizWorkbookService.Template(),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","MauQuizHaiSheet.xlsx");
        public ActionResult Import(string courseId) {if(!Course(courseId))return HttpNotFound();return View();}
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult Import(string courseId,HttpPostedFileBase upload)
        {
            if(!Course(courseId))return HttpNotFound();var errors=new List<string>();var bundle=new QuizWorkbookImport{CourseId=courseId};
            try {
                if(upload==null || upload.ContentLength==0 || upload.ContentLength>2*1024*1024 || !upload.FileName.EndsWith(".xlsx",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Chọn file .xlsx, tối đa 2 MB.");
                bundle=QuizWorkbookService.Read(upload.InputStream,courseId,errors);
                if(errors.Count==0)new QuizWorkbookService(db).Validate(bundle);
            }catch(Exception ex) when(ex is InvalidOperationException || ex is System.IO.InvalidDataException || ex is System.Xml.XmlException || ex is System.IO.IOException) {errors.Add(ex.Message);}
            ViewBag.Errors=errors;ViewBag.Workbook=bundle;
            if(errors.Count==0) {var token=Guid.NewGuid().ToString("N");TempData["QuizImport_"+token]=bundle;ViewBag.Token=token;}
            return View("ImportPreview",bundle.Questions);
        }
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult ConfirmImport(string courseId,string token)
        {
            if(!Course(courseId))return HttpNotFound();var bundle=TempData["QuizImport_"+token] as QuizWorkbookImport;
            if(bundle==null || bundle.CourseId!=courseId || bundle.Questions.Any(q=>q.CourseId!=courseId)) {TempData["QuizMessage"]="Bản xem trước đã hết hạn. Hãy tải lại file.";return RedirectToAction("Import",new{courseId});}
            try {
                new QuizWorkbookService(db).Apply(bundle,CurrentTeacherId);
                TempData["QuizMessage"]="Đã nhập "+bundle.Questions.Count+" câu hỏi."+(bundle.Rules.Any()?" Đã lưu phân bổ đề thành bản nháp. Kiểm tra thời gian, số lượt và điểm tổng trước khi mở quiz.":"");
                return RedirectToAction(bundle.Rules.Any()?"Configure":"Index",new{courseId});
            }catch(InvalidOperationException ex){TempData["QuizMessage"]="Chưa nhập dữ liệu: "+ex.Message;return RedirectToAction("Import",new{courseId});}
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
            ViewBag.VisibilityCounts=new QuizVisibilityService(db).CountsForTeacher(CurrentTeacherId,courseId,studentId);
            return View(Service.Attempts(courseId,studentId));
        }
        public ActionResult Visibility(int id) {return RedirectToAction("Review",new{id,tab="log"});}
        public ActionResult Review(int id,string tab="paper")
        {
            var a=Service.Attempt(id);if(a==null || !Course(a.CourseId))return HttpNotFound();
            var visibility=new QuizVisibilityService(db);var events=visibility.ForTeacher(CurrentTeacherId,id);
            if(events.Any())visibility.MarkReviewed(CurrentTeacherId,id,events.Max(e=>e.Id));
            new QuizRetakeService(db).MarkSeen(CurrentTeacherId,id);
            ViewBag.RetakeCase=new QuizRetakeService(db).ForTeacher(CurrentTeacherId,id);ViewBag.RetakeAvailable=new QuizRetakeService(db).Installed();
            ViewBag.AttemptPendingReview=a.ReviewPending;ViewBag.AttemptReviewNote=a.ReviewNote;
            ViewBag.AttemptScore=a.Score;ViewBag.AttemptIsVerification=a.IsVerification;
            ViewBag.AttemptDetail=true;ViewBag.AttemptId=id;ViewBag.StudentId=a.StudentId;ViewBag.VisibilityCount=events.Count;ViewBag.AttemptSubmitted=a.SubmittedAt.HasValue;
            ViewBag.AttemptTab=tab=="log"||!a.SubmittedAt.HasValue?"log":"paper";
            ViewBag.BreadcrumbCurrent="Chi tiết lượt thi";ViewBag.ShowQuestionBankBreadcrumb=true;
            if(ViewBag.AttemptTab=="log")return View("Visibility",events);
            ViewBag.Corrections=new QuizAssessmentService(db).Corrections(a.CourseId);
            ViewBag.ShowSolutions=true;return View("~/Views/Quiz/Take.cshtml",new QuizTakeViewModel{Attempt=a,Questions=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson),Answers=JsonConvert.DeserializeObject<int[]>(a.AnswersJson??"[]"),Review=true});
        }
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult ApproveResult(int attemptId,string note){try{new QuizVisibilityService(db).ApproveResult(CurrentTeacherId,attemptId,note);TempData["RetakeMessage"]="Đã công nhận kết quả. Học viên nhận chứng chỉ nếu đủ điều kiện.";}catch(InvalidOperationException ex){TempData["RetakeMessage"]=ex.Message;}return RedirectToAction("Review",new{id=attemptId,tab="log"});}
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult RequestRetake(int attemptId,string content){try{new QuizRetakeService(db).RequestRetake(CurrentTeacherId,attemptId,content);TempData["RetakeMessage"]="Đã gửi yêu cầu làm lại cho học viên.";}catch(InvalidOperationException ex){TempData["RetakeMessage"]=ex.Message;}return RedirectToAction("Review",new{id=attemptId,tab="log"});}
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
