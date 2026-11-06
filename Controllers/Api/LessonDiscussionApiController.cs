using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ECOLearning.Hubs;
using ECOLearning.Services;

namespace ECOLearning.Controllers.Api
{
    /// <summary>API thảo luận bài học cho mobile. Chỉ trả tên và avatar người bình luận.</summary>
    [RoutePrefix("api/v1"), ApiAuthorize]
    public class LessonDiscussionApiController : ApiControllerBase
    {
        public class CreateRequest { public int? ParentId { get; set; } public string Content { get; set; } }

        private object ItemJson(ECOLearning.ViewModels.LessonDiscussionItemViewModel item, string courseId, string lessonId)
        {
            return new {
                item.Id, item.ParentId, item.Content, item.CreatedAt, item.AuthorName,
                authorAvatar = AbsoluteUrl(item.AuthorAvatar), item.IsTeacher, item.IsMine,
                item.ReplyToAuthor, item.ReplyToContent,
                item.LikeCount, item.LikedByMe, item.HasImage,
                imageUrl = item.HasImage ? AbsoluteUrl("/api/v1/learning/" + Uri.EscapeDataString(courseId) + "/lessons/" + Uri.EscapeDataString(lessonId) + "/discussions/" + item.Id + "/image") : null,
                replies = item.Replies.Select(r => ItemJson(r, courseId, lessonId)).ToList()
            };
        }

        [HttpGet, Route("learning/{courseId}/lessons/{lessonId}/discussions")]
        public ActionResult Get(string courseId, string lessonId, int page = 1, int pageSize = 20)
        {
            var service = new LessonDiscussionService(Db);
            if (!service.Installed()) return Fail(503, "DISCUSSION_NOT_INSTALLED", "Chức năng thảo luận chưa sẵn sàng.");
            var discussion = service.Get(courseId, lessonId, CurrentUserId, page, pageSize);
            if (discussion == null) return Forbidden("Không thể xem thảo luận của bài học này.");
            return Ok(new { items = discussion.Items.Select(i => ItemJson(i, courseId, lessonId)).ToList(), discussion.Page, discussion.PageSize, discussion.Total, discussion.TotalPages, discussion.TotalComments, discussion.TotalParticipants });
        }

        /// <summary>Dòng thời gian như web: cả bình luận và phản hồi đều theo thứ tự gửi.</summary>
        [HttpGet, Route("learning/{courseId}/lessons/{lessonId}/discussions/timeline")]
        public ActionResult Timeline(string courseId, string lessonId, int page = 1, int pageSize = 30)
        {
            var service = new LessonDiscussionService(Db);
            if (!service.Installed()) return Fail(503, "DISCUSSION_NOT_INSTALLED", "Chức năng thảo luận chưa sẵn sàng.");
            var discussion = service.GetTimeline(courseId, lessonId, CurrentUserId, page, pageSize);
            if (discussion == null) return Forbidden("Không thể xem thảo luận của bài học này.");
            return Ok(new { items = discussion.Items.Select(i => ItemJson(i, courseId, lessonId)).ToList(),
                discussion.Page, discussion.PageSize, discussion.Total, discussion.TotalPages,
                discussion.TotalComments, discussion.TotalParticipants });
        }

        [HttpGet, Route("learning/{courseId}/lessons/{lessonId}/discussions/stats")]
        public ActionResult Stats(string courseId, string lessonId, int days = 7)
        {
            var service = new LessonDiscussionService(Db);
            if (!service.Installed()) return Fail(503, "DISCUSSION_NOT_INSTALLED", "Chức năng thảo luận chưa sẵn sàng.");
            var stats = service.Stats(courseId, lessonId, CurrentUserId, days);
            return stats == null ? Forbidden("Bạn không có quyền xem thống kê thảo luận.") : Ok(stats);
        }

        [HttpPost, Route("learning/{courseId}/lessons/{lessonId}/discussions")]
        [ValidateInput(false)]
        public ActionResult Create(string courseId, string lessonId)
        {
            var request = Body<CreateRequest>();
            var result = new LessonDiscussionService(Db).Create(courseId, lessonId, CurrentUserId, request.ParentId, request.Content);
            if (!result.Success) return BadRequest(result.Message, "DISCUSSION_CREATE_FAILED");
            LessonDiscussionHub.BroadcastComment(courseId, lessonId, result.Item);
            return Ok(ItemJson(result.Item, courseId, lessonId));
        }

        // Multipart: image (JPG/PNG), content và parentId tùy chọn.
        [HttpPost, Route("learning/{courseId}/lessons/{lessonId}/discussions/image")]
        public ActionResult CreateWithImage(string courseId, string lessonId)
        {
            var service = new LessonDiscussionService(Db);
            if (!service.Installed()) return Fail(503, "DISCUSSION_NOT_INSTALLED", "Chức năng thảo luận chưa sẵn sàng.");
            if (!service.CanAccess(courseId, lessonId, CurrentUserId)) return Forbidden("Không thể tham gia thảo luận của bài học này.");
            var image = Request.Files["image"];
            if (image == null || image.ContentLength <= 0 || image.ContentLength > 5 * 1024 * 1024)
                return BadRequest("Chọn ảnh JPG hoặc PNG, tối đa 5 MB.", "INVALID_IMAGE");
            int parent;
            string parentText = Request.Form["parentId"];
            if (!string.IsNullOrWhiteSpace(parentText) && (!int.TryParse(parentText, out parent) || parent <= 0)) return BadRequest("Phản hồi không hợp lệ.");
            int? parentId = string.IsNullOrWhiteSpace(parentText) ? (int?)null : int.Parse(parentText);
            string path = null;
            try
            {
                using (var original = System.Drawing.Image.FromStream(image.InputStream, true, true))
                {
                    if ((original.RawFormat.Guid != ImageFormat.Jpeg.Guid && original.RawFormat.Guid != ImageFormat.Png.Guid) || (long)original.Width * original.Height > 20000000)
                        return BadRequest("Ảnh phải là JPG hoặc PNG và không quá 20 megapixel.", "INVALID_IMAGE");
                    var folder = Server.MapPath("~/App_Data/LessonDiscussionImages");
                    Directory.CreateDirectory(folder);
                    var filename = Guid.NewGuid().ToString("N") + ".jpg";
                    path = Path.Combine(folder, filename);
                    int width = Math.Min(original.Width, 1920), height = Math.Max(1, (int)((double)original.Height * width / original.Width));
                    using (var clean = new Bitmap(width, height))
                    {
                        using (var graphics = Graphics.FromImage(clean)) { graphics.Clear(Color.White); graphics.DrawImage(original, 0, 0, width, height); }
                        clean.Save(path, ImageFormat.Jpeg);
                    }
                    var result = service.Create(courseId, lessonId, CurrentUserId, parentId, Request.Form["content"], filename);
                    if (!result.Success) { System.IO.File.Delete(path); path = null; return BadRequest(result.Message, "DISCUSSION_CREATE_FAILED"); }
                    path = null;
                    LessonDiscussionHub.BroadcastComment(courseId, lessonId, result.Item);
                    return Ok(ItemJson(result.Item, courseId, lessonId));
                }
            }
            catch (ArgumentException) { if (path != null && System.IO.File.Exists(path)) System.IO.File.Delete(path); return BadRequest("Tệp ảnh không hợp lệ.", "INVALID_IMAGE"); }
            catch (OutOfMemoryException) { if (path != null && System.IO.File.Exists(path)) System.IO.File.Delete(path); return BadRequest("Không đọc được ảnh này.", "INVALID_IMAGE"); }
        }

        [HttpGet, Route("learning/{courseId}/lessons/{lessonId}/discussions/{discussionId:int}/image")]
        public ActionResult Image(string courseId, string lessonId, int discussionId)
        {
            var filename = new LessonDiscussionService(Db).ImageName(courseId, lessonId, CurrentUserId, discussionId);
            if (filename == null) return NotFoundJson("Không tìm thấy ảnh.", "IMAGE_NOT_FOUND");
            var path = Path.Combine(Server.MapPath("~/App_Data/LessonDiscussionImages"), filename);
            if (!System.IO.File.Exists(path)) return NotFoundJson("Không tìm thấy ảnh.", "IMAGE_NOT_FOUND");
            Response.Cache.SetCacheability(HttpCacheability.NoCache); Response.Cache.SetNoStore();
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(path, "image/jpeg");
        }

        [HttpPost, Route("learning/{courseId}/lessons/{lessonId}/discussions/{discussionId}/likes/toggle")]
        public ActionResult ToggleLike(string courseId, string lessonId, int discussionId)
        {
            var result = new LessonDiscussionService(Db).ToggleLike(courseId, lessonId, CurrentUserId, discussionId);
            if (!result.Success) return BadRequest(result.Message, "DISCUSSION_LIKE_FAILED");
            LessonDiscussionHub.BroadcastLike(courseId, lessonId, discussionId, result.LikeCount);
            return Ok(new { likeCount = result.LikeCount, likedByMe = result.LikedByMe });
        }
    }
}
