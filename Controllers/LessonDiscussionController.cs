using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ECOLearning.Hubs;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Controllers
{
    /// <summary>AJAX endpoint cho khung thảo luận bài học trên website.</summary>
    public class LessonDiscussionController : Controller
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private int CurrentUserId()
        {
            int id = (Session["UserId"] as int?) ?? 0;
            if (id > 0 && db.NguoiDung.Any(u => u.maNguoiDung == id && u.trangThai != false)) return id;

            // Session có thể mất sau khi ứng dụng khởi động lại, nhưng cookie đăng nhập vẫn còn.
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = db.NguoiDung.FirstOrDefault(u => u.email == User.Identity.Name && u.trangThai != false);
                if (user != null)
                {
                    Session["UserId"] = user.maNguoiDung;
                    return user.maNguoiDung;
                }
            }
            return 0;
        }

        [HttpGet]
        public JsonResult Get(string courseId, string lessonId, int page = 1, int pageSize = 20, int? afterId = null)
        {
            int userId = CurrentUserId();
            var service = new LessonDiscussionService(db);
            if (!service.Installed()) return Json(new { success = false, message = "Thảo luận tạm thời chưa sẵn sàng. Vui lòng thử lại sau." }, JsonRequestBehavior.AllowGet);
            var data = service.GetTimeline(courseId, lessonId, userId, page, pageSize, afterId);
            if (data == null) return Json(new { success = false, message = "Bạn không có quyền xem thảo luận của bài học này." }, JsonRequestBehavior.AllowGet);
            return Json(new { success = true, discussion = data }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult Stats(string courseId, string lessonId, int days = 7)
        {
            int userId = CurrentUserId();
            var data = new LessonDiscussionService(db).Stats(courseId, lessonId, userId, days);
            if (data == null) return Json(new { success = false, message = "Bạn không có quyền xem thống kê thảo luận." }, JsonRequestBehavior.AllowGet);
            return Json(new { success = true, stats = data }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateAntiForgeryToken, ValidateInput(false)]
        public JsonResult Create(string courseId, string lessonId, int? parentId, string content, HttpPostedFileBase image)
        {
            int userId = CurrentUserId();
            var service = new LessonDiscussionService(db);
            if (!service.CanAccess(courseId, lessonId, userId)) return Json(new { success = false, message = "Không thể gửi bình luận trong bài học này." });
            string filename = null, path = null;
            try
            {
                if (image != null)
                {
                    if (image.ContentLength <= 0 || image.ContentLength > 5 * 1024 * 1024)
                        return Json(new { success = false, message = "Chọn ảnh JPG hoặc PNG, tối đa 5 MB." });
                    using (var original = System.Drawing.Image.FromStream(image.InputStream, true, true))
                    {
                        if ((original.RawFormat.Guid != ImageFormat.Jpeg.Guid && original.RawFormat.Guid != ImageFormat.Png.Guid) || (long)original.Width * original.Height > 20000000)
                            return Json(new { success = false, message = "Ảnh phải là JPG hoặc PNG và không quá 20 megapixel." });
                        string folder = Server.MapPath("~/App_Data/LessonDiscussionImages");
                        Directory.CreateDirectory(folder);
                        filename = Guid.NewGuid().ToString("N") + ".jpg";
                        path = Path.Combine(folder, filename);
                        int width = Math.Min(original.Width, 1920);
                        int height = Math.Max(1, (int)((double)original.Height * width / original.Width));
                        using (var clean = new Bitmap(width, height))
                        {
                            using (var graphics = Graphics.FromImage(clean)) { graphics.Clear(Color.White); graphics.DrawImage(original, 0, 0, width, height); }
                            clean.Save(path, ImageFormat.Jpeg);
                        }
                    }
                }
                var result = service.Create(courseId, lessonId, userId, parentId, content, filename);
                if (!result.Success && path != null && System.IO.File.Exists(path)) System.IO.File.Delete(path);
                if (result.Success) LessonDiscussionHub.BroadcastComment(courseId, lessonId, result.Item);
                return Json(new { success = result.Success, message = result.Message, item = result.Item });
            }
            catch (ArgumentException) { if (path != null && System.IO.File.Exists(path)) System.IO.File.Delete(path); return Json(new { success = false, message = "Tệp không phải ảnh JPG/PNG hợp lệ." }); }
            catch (OutOfMemoryException) { if (path != null && System.IO.File.Exists(path)) System.IO.File.Delete(path); return Json(new { success = false, message = "Không đọc được ảnh. Hãy thử ảnh nhỏ hơn." }); }
        }

        [HttpGet]
        public ActionResult Image(string courseId, string lessonId, int id)
        {
            var filename = new LessonDiscussionService(db).ImageName(courseId, lessonId, CurrentUserId(), id);
            if (filename == null) return HttpNotFound();
            var path = Path.Combine(Server.MapPath("~/App_Data/LessonDiscussionImages"), filename);
            if (!System.IO.File.Exists(path)) return HttpNotFound();
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(path, "image/jpeg");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public JsonResult ToggleLike(string courseId, string lessonId, int discussionId)
        {
            int userId = CurrentUserId();
            var result = new LessonDiscussionService(db).ToggleLike(courseId, lessonId, userId, discussionId);
            if (result.Success)
                LessonDiscussionHub.BroadcastLike(courseId, lessonId, discussionId, result.LikeCount);
            return Json(new { success = result.Success, message = result.Message, likeCount = result.LikeCount, likedByMe = result.LikedByMe });
        }
    }
}
