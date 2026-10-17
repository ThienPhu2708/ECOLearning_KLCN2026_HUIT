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
                return View(new QuizOverview{CourseId=courseId,CourseName=db.KhoaHoc.Find(courseId).tenKhoaHoc,Config=Service.Config(courseId),Student=Service.Student(courseId,StudentId),Attempts=Service.Attempts(courseId,StudentId)});
            }catch(InvalidOperationException ex){return new HttpStatusCodeResult(403,ex.Message);}
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult Start(string courseId)
        {
            try {return RedirectToAction("Take",new{id=Service.Start(courseId,StudentId).Id});}
            catch(InvalidOperationException ex){TempData["QuizMessage"]=ex.Message;return RedirectToAction("Index",new{courseId});}
        }
        public ActionResult Take(int id)
        {
            var a=Service.Attempt(id);if(a==null || a.StudentId!=StudentId)return HttpNotFound();
            try {Service.RequireEnrollment(a.CourseId,StudentId);}catch(InvalidOperationException){return new HttpStatusCodeResult(403);}
            if(!a.SubmittedAt.HasValue && DateTime.UtcNow>a.ExpiresAt.AddSeconds(5)){Service.Submit(id,StudentId,new int[0]);a=Service.Attempt(id);}
            // Failed learners may receive extra attempts later, so do not disclose their answer key.
            ViewBag.ShowSolutions=a.SubmittedAt.HasValue && a.Passed==true;
            return View(new QuizTakeViewModel{Attempt=a,Questions=JsonConvert.DeserializeObject<List<QuizSnapshotQuestion>>(a.SnapshotJson),Answers=JsonConvert.DeserializeObject<int[]>(a.AnswersJson ?? "[]"),Review=a.SubmittedAt.HasValue});
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult SaveDraft(int id,int[] answers)
        {
            try{return Json(new{saved=Service.SaveDraft(id,StudentId,answers)});}catch(InvalidOperationException){return new HttpStatusCodeResult(403);}
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult Submit(int id,int[] answers)
        {
            try {Service.Submit(id,StudentId,answers);return RedirectToAction("Take",new{id});}catch(InvalidOperationException){return new HttpStatusCodeResult(403);}
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult ClaimCertificate(string courseId)
        {
            try{Service.ClaimCertificate(courseId,StudentId);return RedirectToAction("Certificate","Learning",new{courseId});}catch(InvalidOperationException ex){TempData["QuizMessage"]=ex.Message;}
            return RedirectToAction("Index",new{courseId});
        }
        protected override void Dispose(bool disposing){if(disposing)db.Dispose();base.Dispose(disposing);}
    }
}
