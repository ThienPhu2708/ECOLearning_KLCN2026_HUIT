using System;
using System.Collections.Generic;

namespace ECOLearning.ViewModels
{
    /// <summary>Dữ liệu công khai của một bình luận. Không trả email, mã người dùng hay trang cá nhân.</summary>
    public class LessonDiscussionItemViewModel
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Content { get; set; }
        public bool HasImage { get; set; }
        public string ReplyToAuthor { get; set; }
        public string ReplyToContent { get; set; }
        public string CreatedAt { get; set; }
        public string AuthorName { get; set; }
        public string AuthorAvatar { get; set; }
        public bool IsTeacher { get; set; }
        public bool IsMine { get; set; }
        public int LikeCount { get; set; }
        public bool LikedByMe { get; set; }
        public List<LessonDiscussionItemViewModel> Replies { get; set; }

        public LessonDiscussionItemViewModel()
        {
            Replies = new List<LessonDiscussionItemViewModel>();
        }
    }

    public class LessonDiscussionPageViewModel
    {
        public List<LessonDiscussionItemViewModel> Items { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
        public int TotalComments { get; set; }
        public int TotalParticipants { get; set; }

        public LessonDiscussionPageViewModel()
        {
            Items = new List<LessonDiscussionItemViewModel>();
        }
    }

    public class LessonDiscussionStatsViewModel
    {
        public int Days { get; set; }
        public int Comments { get; set; }
        public int Replies { get; set; }
        public int Participants { get; set; }
        public int Likes { get; set; }
    }
}
