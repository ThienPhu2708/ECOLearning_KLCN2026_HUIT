# API học viên: học tập, thảo luận và gợi ý

Các endpoint bên dưới dùng HTTPS và header `Authorization: Bearer <token>`; đường dẫn tương đối tính từ `/api/v1`. Phản hồi JSON dùng khung `{ success, message, data }`. Không đưa token vào URL hoặc log.

## Bài học, phụ đề và tài liệu

- `GET /learning/{courseId}`: đề cương. Mỗi bài có `isUnlocked`, `hasSubtitle`, `hasDocuments`, `documentsCount`. `isUnlocked` phản ánh thứ tự mở bài trên web; app cần dùng giá trị này để khóa giao diện.
- `GET /learning/{courseId}/lessons/{lessonId}`: URL video, `isUnlocked`, `hasSubtitle`, `subtitle`, `hasDocuments`, `documents`. Nếu `hasSubtitle` là false, app vẫn hiển thị nút CC và hiện “Video hiện chưa hỗ trợ phụ đề” trong 3 giây khi bấm. Nếu true, tải `subtitle.url` bằng Bearer token; nội dung là WebVTT tiếng Việt.
- `GET /learning/{courseId}/lessons/{lessonId}/documents`: danh sách tài liệu. Bật nút tải khi có tài liệu hợp lệ; dùng URL tải của từng tệp.

## Thảo luận theo bài học

- `GET /learning/{courseId}/lessons/{lessonId}/discussions/timeline?page=1&pageSize=30`: dòng thời gian phẳng giống web, bao gồm cả bình luận và phản hồi. Trang 1 là nhóm tin mới nhất; trong mỗi trang, tin cũ trước tin mới. `parentId`, `replyToAuthor`, `replyToContent` xác định trích dẫn; `isMine` giúp căn phải. `total` và `totalComments` đều tính cả phản hồi. Phân trang offset có thể trùng/thiếu khi có tin mới giữa các lần tải: client loại trùng và tải lại khi cần.
- `GET /learning/{courseId}/lessons/{lessonId}/discussions?page=1&pageSize=20`: bình luận chính và phản hồi một cấp; `totalComments` bao gồm phản hồi. Kết quả có `authorName`, `authorAvatar`, `createdAt`, `likeCount`, `likedByMe`, `hasImage`, `imageUrl` và `replies`. Sau migration 033, `createdAt` là ISO 8601 UTC có hậu tố `Z`; app đổi sang giờ địa phương để hiển thị thời gian tương đối.
- `POST /learning/{courseId}/lessons/{lessonId}/discussions`: JSON `{ "content": "Nội dung 😊", "parentId": null }`. Dùng `parentId` của bình luận chính để phản hồi.
- `POST /learning/{courseId}/lessons/{lessonId}/discussions/image`: multipart/form-data với `image` (bắt buộc, JPG/PNG tối đa 5 MB), `content` và `parentId` (tùy chọn). Có thể gửi ảnh không kèm chữ.
- `GET /learning/{courseId}/lessons/{lessonId}/discussions/{discussionId}/image`: tải ảnh với Bearer token. `imageUrl` trong danh sách trỏ tới endpoint này; bộ tải ảnh của app phải gửi header xác thực.
- `POST /learning/{courseId}/lessons/{lessonId}/discussions/{discussionId}/likes/toggle`: trả `likedByMe`, `likeCount`.

Thảo luận và ảnh dùng chung dữ liệu với web. Người dùng phải là học viên đã ghi danh hoặc giáo viên chủ khóa. Ảnh hiện lưu trong `App_Data/LessonDiscussionImages` của hosting; cần giữ thư mục này khi publish và sao lưu.

### Đồng bộ real-time

Kết nối SignalR 2 tại `/signalr`, gửi Bearer token trong header `Authorization` trên các request của kết nối; không truyền token trong query string. Dùng hub `lessonDiscussionHub`, gọi `joinLesson(courseId, lessonId)` sau khi kết nối. Nhận `discussionCommentAdded` thì tải lại danh sách để lấy trạng thái cá nhân và URL ảnh; `discussionLikeChanged` có `id`, `likeCount`. Sau reconnect cần gọi lại `joinLesson`. Nếu SDK mobile không gửi được header trên transport đã chọn, dùng polling danh sách khi mở màn hình; không coi mã này là WebSocket thuần.

## Gợi ý

- `GET /recommendations/personal?limit=12`: danh sách trang “Khóa học gợi ý”, chỉ các khóa phù hợp, không bù khóa phổ biến. `badge` là “Phù hợp với bạn”.
- `POST /recommendations/personal/refresh?limit=12`: tính lại cho người đăng nhập bằng ma trận chung hiện có, trả danh sách mới; không tính lại ma trận chung. Các tương tác không đổi có thể cho kết quả giống trước.
- `GET /home`: các mục bán chạy và “Vì bạn đã học” dùng chung dịch vụ trang chủ web. Endpoint `/recommendations` hiện có được giữ lại để tương thích ứng dụng cũ.

Sau triển khai cần migration `032_LessonDiscussion.sql` và `033_LessonDiscussionUtc.sql` trên đúng database; đọc `Database/Migrations/033_HuongDan.md` trước khi chuyển giờ cũ. Chức năng real-time cần được kiểm thử bằng SDK SignalR 2 của app và cấu hình proxy/hosting thực tế.
