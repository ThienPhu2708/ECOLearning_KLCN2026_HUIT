using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;
using Newtonsoft.Json;

namespace ECOLearning.Controllers
{
    public class QuizController : Controller
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();
        private int StudentId => (Session["UserId"] as int?) ?? 0;
        private QuizService Service => new QuizService(db);
        protected override void OnActionExecuting(ActionExecutingContext context)
        {
            if(StudentId<=0)context.Result=RedirectToAction("Login","Account",new{returnUrl=Request.RawUrl});
            else {var user=db.NguoiDung.Find(StudentId);if(user==null || user.trangThai==false)context.Result=new HttpStatusCodeResult(403);}
            base.OnActionExecuting(context);
        }
        public ActionResult Index(string courseId)
        {
            try {
                Service.RequireEnrollment(courseId,StudentId);Service.CloseExpired(courseId,StudentId);
                if (!StaffViewMode.LaChiXem(Session)) Service.EnsureCertificateWithoutQuiz(courseId,StudentId);
                ViewBag.RetakeCases=new QuizRetakeService(db).ForStudent(StudentId,courseId);
                return View(new QuizOverview{CourseId=courseId,CourseName=db.KhoaHoc.Find(courseId).tenKhoaHoc,Config=Service.Config(courseId),Student=Service.Student(courseId,StudentId),Attempts=Service.Attempts(courseId,StudentId),HasQuestionReports=new QuizQuestionReportService(db).HasReports(StudentId,courseId)});
            }catch(InvalidOperationException ex){return new HttpStatusCodeResult(403,ex.Message);}
        }
        [HttpPost,ValidateAntiForgeryToken]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public ActionResult Start(string courseId)
        {
            try {return RedirectToAction("Take",new{id=Service.Start(courseId,StudentId).Id});}
            catch(InvalidOperationException ex){TempData["QuizMessage"]=ex.Message;return RedirectToAction("Index",new{courseId});}
        }
        public ActionResult Take(int id)
        {
            var a=Service.Attempt(id);if(a==null || a.StudentId!=StudentId)return HttpNotFound();
            try {Service.RequireEnrollment(a.CourseId,StudentId);}catch(InvalidOperationException){return new HttpStatusCodeResult(403);}
            if(!a.SubmittedAt.HasValue && DateTime.UtcNow>=a.ExpiresAt){Service.Submit(id,StudentId,new int[0]);a=Service.Attempt(id);}
            // Failed learners may receive extra attempts later, so do not disclose their answer key.
            ViewBag.ShowSolutions=a.SubmittedAt.HasValue && !a.ReviewPending && a.Passed==true;
            ViewBag.Corrections=new QuizAssessmentService(db).Corrections(a.CourseId);
            return View(new QuizTakeViewModel{Attempt=a,Questions=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson),Answers=JsonConvert.DeserializeObject<int[]>(a.AnswersJson ?? "[]"),Review=a.SubmittedAt.HasValue});
        }
        [HttpPost,ValidateAntiForgeryToken]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public ActionResult SaveDraft(int id,int[] answers,string writerToken)
        {
            try{return Json(new{saved=Service.SaveDraft(id,StudentId,answers,writerToken,true)});}catch(InvalidOperationException){return new HttpStatusCodeResult(403);}
        }
        [HttpPost,ValidateAntiForgeryToken]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public ActionResult Submit(int id,int[] answers,string writerToken)
        {
            try {Service.Submit(id,StudentId,answers,writerToken,true);return RedirectToAction("Take",new{id});}catch(InvalidOperationException){return new HttpStatusCodeResult(403);}
        }
        [HttpPost,ValidateAntiForgeryToken]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public ActionResult ClaimCertificate(string courseId)
        {
            try{Service.ClaimCertificate(courseId,StudentId);return RedirectToAction("Certificate","Learning",new{courseId});}catch(InvalidOperationException ex){TempData["QuizMessage"]=ex.Message;}
            return RedirectToAction("Index",new{courseId});
        }
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult ReportQuestion(int attemptId,int questionId,string reason,string content) {
            try {new QuizQuestionReportService(db).Send(StudentId,attemptId,questionId,reason,content);return Json(new{success=true,message="Đã gửi báo cáo đến giáo viên."});}
            catch(InvalidOperationException ex){return Json(new{success=false,message=ex.Message});}
        }
        public ActionResult Reports(string courseId) {
            ViewBag.IsTeacher=false;ViewBag.CourseId=courseId;
            try {return View("~/Views/Quiz/Reports.cshtml",new QuizQuestionReportService(db).List(StudentId,false,courseId));}
            catch(InvalidOperationException ex){ViewBag.ReportError=ex.Message;return View("~/Views/Quiz/Reports.cshtml",new List<QuizQuestionReport>());}
        }
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult Visibility(int id,string eventKey,bool returned=false) {
            try {new QuizVisibilityService(db).Record(StudentId,id,eventKey,returned);return Json(new{success=true});}
            catch(InvalidOperationException ex){Response.StatusCode=409;return Json(new{success=false,message=ex.Message});}
        }
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult AcquireWriter(int id,string clientKey,bool takeover=false){
            try{return Json(new{success=true,token=new QuizWriterService(db).Acquire(id,StudentId,clientKey,takeover),answers=JsonConvert.DeserializeObject<int[]>(Service.Attempt(id).AnswersJson??"[]")});}
            catch(QuizSessionException ex){Response.StatusCode=409;return Json(new{success=false,conflict=true,message=ex.Message});}
            catch(InvalidOperationException ex){Response.StatusCode=409;return Json(new{success=false,conflict=false,message=ex.Message});}
        }
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult CheckWriter(int id,string writerToken){
            var a=Service.Attempt(id);if(a==null||a.StudentId!=StudentId)return HttpNotFound();
            if(a.SubmittedAt.HasValue||DateTime.UtcNow>=a.ExpiresAt)return Json(new{active=false,finished=true});
            try{new QuizWriterService(db).Require(id,writerToken);return Json(new{active=true});}
            catch(InvalidOperationException){return Json(new{active=false,finished=false});}
        }
        public ActionResult Appeals(string courseId){
            var regrade=new QuizRegradeService(db);
            ViewBag.IsTeacher=false;ViewBag.CourseId=courseId;ViewBag.Notices=regrade.Notices(StudentId);
            ViewBag.AppealAttempts=new List<QuizAttempt>();ViewBag.HasUsedAppeal=false;
            if(!string.IsNullOrWhiteSpace(courseId)){try{Service.RequireEnrollment(courseId,StudentId);Service.CloseExpired(courseId,StudentId);}catch(InvalidOperationException){return new HttpStatusCodeResult(403);}}
            var requests=regrade.Appeals(StudentId,false);
            if(!string.IsNullOrWhiteSpace(courseId)) {
                bool used=requests.Any(r=>r.CourseId==courseId);ViewBag.HasUsedAppeal=used;
                if(!used)ViewBag.AppealAttempts=Service.Attempts(courseId,StudentId).Where(a=>a.SubmittedAt.HasValue).ToList();
            }
            return View(requests);
        }
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult Appeal(int attemptId,string content){try{new QuizRegradeService(db).Appeal(StudentId,attemptId,content);TempData["AppealMessage"]="Đã gửi phúc khảo đến giáo viên.";}catch(InvalidOperationException ex){TempData["AppealMessage"]=ex.Message;}var a=Service.Attempt(attemptId);return RedirectToAction("Appeals",new{courseId=a!=null&&a.StudentId==StudentId?a.CourseId:null});}
        public ActionResult Retakes(string courseId){try{Service.RequireEnrollment(courseId,StudentId);Service.CloseExpired(courseId,StudentId);ViewBag.CourseId=courseId;ViewBag.CourseName=db.KhoaHoc.Find(courseId).tenKhoaHoc;ViewBag.VerificationConfig=Service.Config(courseId);return View(new QuizRetakeService(db).ForStudent(StudentId,courseId));}catch(InvalidOperationException){return new HttpStatusCodeResult(403);}}
        [HttpPost,ValidateAntiForgeryToken,StaffReadOnly]
        public ActionResult StartVerification(string courseId,int reviewId){try{return RedirectToAction("Take",new{id=new QuizRetakeService(db).StartRetake(StudentId,reviewId).Id});}catch(InvalidOperationException ex){TempData["RetakeMessage"]=ex.Message;return RedirectToAction("Retakes",new{courseId});}}
        protected override void Dispose(bool disposing){if(disposing)db.Dispose();base.Dispose(disposing);}
    }
}
