using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Hubs
{
    /// <summary>Chỉ dùng để đồng bộ thời gian thực; ghi dữ liệu vẫn qua LessonDiscussionController/API.</summary>
    [Authorize]
    public class LessonDiscussionHub : Hub
    {
        private static string GroupName(string lessonId) { return "lesson_discussion_" + (lessonId ?? "").Trim(); }

        private int CurrentUserId()
        {
            if (Context.User == null || !Context.User.Identity.IsAuthenticated) return 0;
            using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
            {
                var user = db.NguoiDung.FirstOrDefault(u => u.email == Context.User.Identity.Name);
                return user != null && user.trangThai != false ? user.maNguoiDung : 0;
            }
        }

        public async Task JoinLesson(string courseId, string lessonId)
        {
            int userId = CurrentUserId();
            using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
            {
                if (!new LessonDiscussionService(db).CanAccess(courseId, lessonId, userId))
                    throw new HubException("Bạn không có quyền tham gia thảo luận này.");
            }
            await Groups.Add(Context.ConnectionId, GroupName(lessonId));
        }

        public Task LeaveLesson(string lessonId)
        {
            return Groups.Remove(Context.ConnectionId, GroupName(lessonId));
        }

        public static void BroadcastComment(string courseId, string lessonId, LessonDiscussionItemViewModel item)
        {
            GlobalHost.ConnectionManager.GetHubContext<LessonDiscussionHub>()
                .Clients.Group(GroupName(lessonId)).discussionCommentAdded(item);
        }

        public static void BroadcastLike(string courseId, string lessonId, int discussionId, int likeCount)
        {
            GlobalHost.ConnectionManager.GetHubContext<LessonDiscussionHub>()
                .Clients.Group(GroupName(lessonId)).discussionLikeChanged(new { id = discussionId, likeCount = likeCount });
        }
    }
}
