using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>Thảo luận theo bài học; hoàn toàn tách biệt với ChatService nhắn tin 1-1.</summary>
    public class LessonDiscussionService
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public LessonDiscussionService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        private class DiscussionRow
        {
            public int Id { get; set; }
            public int? ParentId { get; set; }
            public string Content { get; set; }
            public DateTime CreatedAt { get; set; }
            public string AuthorName { get; set; }
            public string AuthorAvatar { get; set; }
            public bool IsTeacher { get; set; }
            public bool IsMine { get; set; }
            public int LikeCount { get; set; }
            public bool LikedByMe { get; set; }
        }

        public class Result
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public LessonDiscussionItemViewModel Item { get; set; }
            public int LikeCount { get; set; }
            public bool LikedByMe { get; set; }
        }

        public bool Installed()
        {
            try
            {
                return db.Database.SqlQuery<int>(@"SELECT CASE WHEN OBJECT_ID(N'dbo.ThaoLuanBaiHoc', N'U') IS NOT NULL
                    AND OBJECT_ID(N'dbo.YeuThichThaoLuanBaiHoc', N'U') IS NOT NULL
                    AND OBJECT_ID(N'dbo.LessonDiscussionUtc033Marker', N'U') IS NOT NULL THEN 1 ELSE 0 END").Single() == 1;
            }
            catch { return false; }
        }

        /// <summary>Chỉ học viên đã ghi danh hoặc giáo viên sở hữu khóa học được tham gia.</summary>
        public bool CanAccess(string courseId, string lessonId, int userId)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(courseId) || string.IsNullOrWhiteSpace(lessonId)) return false;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == courseId);
            if (course == null) return false;
            bool belongs = new LearningService(db).LayBaiHoc(courseId).Any(b => b.maBaiHoc == lessonId);
            if (!belongs) return false;
            return course.maGiaoVien == userId || db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == courseId && d.maHocVien == userId);
        }

        private List<DiscussionRow> Rows(string courseId, string lessonId, int userId)
        {
            return db.Database.SqlQuery<DiscussionRow>(@"
                SELECT t.maThaoLuan AS Id, t.maThaoLuanCha AS ParentId, t.noiDung AS Content, t.ngayTao AS CreatedAt,
                       ISNULL(u.hoTen, N'Người học') AS AuthorName, u.anhDaiDien AS AuthorAvatar,
                       CAST(CASE WHEN u.maNguoiDung = k.maGiaoVien THEN 1 ELSE 0 END AS bit) AS IsTeacher,
                       CAST(CASE WHEN t.maNguoiDung = @userId THEN 1 ELSE 0 END AS bit) AS IsMine,
                       ISNULL(l.likes, 0) AS LikeCount,
                       CAST(CASE WHEN mine.maNguoiDung IS NULL THEN 0 ELSE 1 END AS bit) AS LikedByMe
                FROM dbo.ThaoLuanBaiHoc t
                INNER JOIN dbo.NguoiDung u ON u.maNguoiDung = t.maNguoiDung
                INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = @courseId
                OUTER APPLY (SELECT COUNT(*) AS likes FROM dbo.YeuThichThaoLuanBaiHoc yl WHERE yl.maThaoLuan = t.maThaoLuan) l
                LEFT JOIN dbo.YeuThichThaoLuanBaiHoc mine ON mine.maThaoLuan = t.maThaoLuan AND mine.maNguoiDung = @userId
                WHERE t.maBaiHoc = @lessonId AND t.trangThai = N'HienThi'
                ORDER BY t.ngayTao ASC, t.maThaoLuan ASC",
                new SqlParameter("@courseId", courseId), new SqlParameter("@lessonId", lessonId), new SqlParameter("@userId", userId)).ToList();
        }

        private static LessonDiscussionItemViewModel Map(DiscussionRow r)
        {
            var image = Regex.Match(r.Content ?? "", @"(?:\n)?\[discussion-image:([a-f0-9]{32}\.jpg)\]$");
            return new LessonDiscussionItemViewModel
            {
                Id = r.Id, ParentId = r.ParentId, Content = image.Success ? (r.Content ?? "").Substring(0, image.Index) : r.Content,
                HasImage = image.Success,
                CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc).ToString("o"), AuthorName = r.AuthorName,
                AuthorAvatar = r.AuthorAvatar, IsTeacher = r.IsTeacher, IsMine = r.IsMine,
                LikeCount = r.LikeCount, LikedByMe = r.LikedByMe
            };
        }

        public LessonDiscussionPageViewModel Get(string courseId, string lessonId, int userId, int page, int pageSize)
        {
            if (!Installed() || !CanAccess(courseId, lessonId, userId)) return null;
            page = Math.Max(1, page); pageSize = Math.Max(1, Math.Min(50, pageSize));
            var rows = Rows(courseId, lessonId, userId);
            var roots = rows.Where(r => !r.ParentId.HasValue).OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).ToList();
            var rootIds = roots.Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.Id).ToList();
            var result = new LessonDiscussionPageViewModel
            {
                Page = page, PageSize = pageSize, Total = roots.Count,
                TotalPages = (int)Math.Ceiling(roots.Count / (double)pageSize),
                TotalComments = rows.Count,
                TotalParticipants = db.Database.SqlQuery<int>("SELECT COUNT(DISTINCT maNguoiDung) FROM dbo.ThaoLuanBaiHoc WHERE maBaiHoc=@lessonId AND trangThai=N'HienThi'", new SqlParameter("@lessonId", lessonId)).Single()
            };
            // Mỗi trang vẫn lấy nhóm bình luận mới nhất trước, nhưng hiển thị trong trang theo thời gian tăng dần.
            foreach (var root in roots.Where(r => rootIds.Contains(r.Id)).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id))
            {
                var item = Map(root);
                item.Replies = rows.Where(r => r.ParentId == root.Id).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Select(Map).ToList();
                result.Items.Add(item);
            }
            return result;
        }

        /// <summary>Luồng chat web: chọn các tin mới nhất, bao gồm cả phản hồi, rồi xếp từ cũ đến mới.</summary>
        public LessonDiscussionPageViewModel GetTimeline(string courseId, string lessonId, int userId, int page, int pageSize)
        {
            if (!Installed() || !CanAccess(courseId, lessonId, userId)) return null;
            page = Math.Max(1, page); pageSize = Math.Max(1, Math.Min(50, pageSize));
            var rows = Rows(courseId, lessonId, userId);
            var byId = rows.ToDictionary(r => r.Id);
            var selected = rows.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList();
            var result = new LessonDiscussionPageViewModel
            {
                Page = page, PageSize = pageSize, Total = rows.Count, TotalComments = rows.Count,
                TotalPages = (int)Math.Ceiling(rows.Count / (double)pageSize),
                TotalParticipants = db.Database.SqlQuery<int>("SELECT COUNT(DISTINCT maNguoiDung) FROM dbo.ThaoLuanBaiHoc WHERE maBaiHoc=@lessonId AND trangThai=N'HienThi'", new SqlParameter("@lessonId", lessonId)).Single()
            };
            foreach (var row in selected)
            {
                var item = Map(row);
                DiscussionRow parent;
                if (row.ParentId.HasValue && byId.TryGetValue(row.ParentId.Value, out parent))
                {
                    var original = Map(parent);
                    item.ReplyToAuthor = original.AuthorName;
                    item.ReplyToContent = string.IsNullOrWhiteSpace(original.Content) && original.HasImage ? "Hình ảnh" : original.Content;
                }
                result.Items.Add(item);
            }
            return result;
        }

        private LessonDiscussionItemViewModel GetOne(string courseId, string lessonId, int id, int userId)
        {
            var rows = Rows(courseId, lessonId, userId);
            var row = rows.FirstOrDefault(r => r.Id == id);
            if (row == null) return null;
            var item = Map(row);
            if (!row.ParentId.HasValue)
                item.Replies = rows.Where(r => r.ParentId == id).OrderBy(r => r.CreatedAt).Select(Map).ToList();
            return item;
        }

        public Result Create(string courseId, string lessonId, int userId, int? parentId, string content, string imageName = null)
        {
            if (!Installed()) return new Result { Message = "Chức năng thảo luận chưa sẵn sàng. Vui lòng liên hệ quản trị viên." };
            if (!CanAccess(courseId, lessonId, userId)) return new Result { Message = "Bạn không có quyền thảo luận trong bài học này." };
            content = (content ?? "").Trim();
            if (!string.IsNullOrEmpty(imageName) && !Regex.IsMatch(imageName, @"^[a-f0-9]{32}\.jpg$")) return new Result { Message = "Ảnh không hợp lệ." };
            if (content.IndexOf("[discussion-image:", StringComparison.OrdinalIgnoreCase) >= 0) return new Result { Message = "Nội dung bình luận không hợp lệ." };
            if (content.Length == 0 && imageName == null) return new Result { Message = "Hãy nhập bình luận hoặc chọn ảnh." };
            int maxContent = imageName == null ? 2000 : 1943;
            if (content.Length > maxContent) return new Result { Message = "Nội dung bình luận quá dài." };
            if (imageName != null) content += (content.Length > 0 ? "\n" : "") + "[discussion-image:" + imageName + "]";
            if (RateLimiter.VuotGioiHan("lesson-discussion:u" + userId, 15, TimeSpan.FromMinutes(1)))
                return new Result { Message = "Bạn đang gửi bình luận quá nhanh. Vui lòng thử lại sau ít phút." };

            if (parentId.HasValue)
            {
                var parent = db.Database.SqlQuery<ParentRow>("SELECT maThaoLuan AS Id, maThaoLuanCha AS ParentId FROM dbo.ThaoLuanBaiHoc WHERE maThaoLuan = @id AND maBaiHoc = @lessonId AND trangThai = N'HienThi'",
                    new SqlParameter("@id", parentId.Value), new SqlParameter("@lessonId", lessonId)).FirstOrDefault();
                if (parent == null) return new Result { Message = "Không tìm thấy bình luận cần phản hồi." };
                // Chỉ hỗ trợ một cấp phản hồi để giao diện dễ theo dõi.
                if (parent.ParentId.HasValue) parentId = parent.ParentId;
            }

            int id = db.Database.SqlQuery<int>(@"INSERT INTO dbo.ThaoLuanBaiHoc(maBaiHoc, maNguoiDung, maThaoLuanCha, noiDung)
                VALUES(@lessonId, @userId, @parentId, @content); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new SqlParameter("@lessonId", lessonId), new SqlParameter("@userId", userId),
                new SqlParameter("@parentId", (object)parentId ?? DBNull.Value), new SqlParameter("@content", content)).Single();
            return new Result { Success = true, Item = GetOne(courseId, lessonId, id, userId) };
        }

        private class ParentRow { public int Id { get; set; } public int? ParentId { get; set; } }

        public string ImageName(string courseId, string lessonId, int userId, int id)
        {
            if (!Installed() || !CanAccess(courseId, lessonId, userId)) return null;
            string content = db.Database.SqlQuery<string>("SELECT noiDung FROM dbo.ThaoLuanBaiHoc WHERE maThaoLuan=@id AND maBaiHoc=@lessonId AND trangThai=N'HienThi'", new SqlParameter("@id", id), new SqlParameter("@lessonId", lessonId)).FirstOrDefault();
            var match = Regex.Match(content ?? "", @"(?:\n)?\[discussion-image:([a-f0-9]{32}\.jpg)\]$");
            return match.Success ? match.Groups[1].Value : null;
        }

        public Result ToggleLike(string courseId, string lessonId, int userId, int discussionId)
        {
            if (!Installed()) return new Result { Message = "Chức năng thảo luận chưa được cài đặt." };
            if (!CanAccess(courseId, lessonId, userId)) return new Result { Message = "Bạn không có quyền thực hiện thao tác này." };
            bool exists = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.ThaoLuanBaiHoc WHERE maThaoLuan = @id AND maBaiHoc = @lessonId AND trangThai = N'HienThi'",
                new SqlParameter("@id", discussionId), new SqlParameter("@lessonId", lessonId)).Single() > 0;
            if (!exists) return new Result { Message = "Bình luận không còn tồn tại." };
            int removed = db.Database.ExecuteSqlCommand("DELETE FROM dbo.YeuThichThaoLuanBaiHoc WHERE maThaoLuan = @id AND maNguoiDung = @userId",
                new SqlParameter("@id", discussionId), new SqlParameter("@userId", userId));
            bool liked = removed == 0;
            if (liked)
            {
                try
                {
                    db.Database.ExecuteSqlCommand("INSERT INTO dbo.YeuThichThaoLuanBaiHoc(maThaoLuan, maNguoiDung) VALUES(@id, @userId)",
                        new SqlParameter("@id", discussionId), new SqlParameter("@userId", userId));
                }
                catch { liked = true; }
            }
            int count = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.YeuThichThaoLuanBaiHoc WHERE maThaoLuan = @id", new SqlParameter("@id", discussionId)).Single();
            return new Result { Success = true, LikedByMe = liked, LikeCount = count };
        }

        public LessonDiscussionStatsViewModel Stats(string courseId, string lessonId, int userId, int days)
        {
            if (!Installed() || !CanAccess(courseId, lessonId, userId)) return null;
            int[] allowed = { 2, 4, 7, 14, 21 };
            if (!allowed.Contains(days)) days = 7;
            // Thống kê theo ngày Việt Nam, quy đổi mốc đầu ngày sang UTC để so với CSDL.
            DateTime from = DateTime.UtcNow.AddHours(7).Date.AddDays(-(days - 1)).AddHours(-7);
            var stat = db.Database.SqlQuery<StatsRow>(@"SELECT COUNT(*) AS Comments,
                    ISNULL(SUM(CASE WHEN maThaoLuanCha IS NULL THEN 0 ELSE 1 END), 0) AS Replies,
                    COUNT(DISTINCT maNguoiDung) AS Participants
                FROM dbo.ThaoLuanBaiHoc
                WHERE maBaiHoc=@lessonId AND trangThai=N'HienThi' AND ngayTao >= @from",
                new SqlParameter("@lessonId", lessonId), new SqlParameter("@from", from)).FirstOrDefault() ?? new StatsRow();
            int likes = db.Database.SqlQuery<int>(@"SELECT COUNT(*) FROM dbo.YeuThichThaoLuanBaiHoc yl
                INNER JOIN dbo.ThaoLuanBaiHoc t ON t.maThaoLuan=yl.maThaoLuan
                WHERE t.maBaiHoc=@lessonId AND t.trangThai=N'HienThi' AND t.ngayTao >= @from",
                new SqlParameter("@lessonId", lessonId), new SqlParameter("@from", from)).Single();
            return new LessonDiscussionStatsViewModel { Days = days, Comments = stat.Comments, Replies = stat.Replies, Participants = stat.Participants, Likes = likes };
        }

        private class StatsRow { public int Comments { get; set; } public int Replies { get; set; } public int Participants { get; set; } }
    }
}
