# API chat cho ứng dụng di động

Mọi endpoint dùng header `Authorization: Bearer <token>` (token từ `POST /api/v1/auth/login`).
Phản hồi JSON chung: `{ success, message, data }`. Thời gian dùng ISO-8601 có múi giờ máy chủ.

| Method | Endpoint | Mục đích |
|---|---|---|
| GET | `/api/v1/chat/conversations?courseId=&status=all\|unread\|unanswered` | Danh sách hội thoại của tài khoản hiện tại |
| POST | `/api/v1/chat/courses/{courseId}/conversation` | Học viên đã ghi danh mở/lấy hội thoại với giáo viên |
| GET | `/api/v1/chat/conversations/{id}/messages?beforeId=&limit=50` | Lấy lịch sử, phân trang ngược bằng `beforeId` |
| POST | `/api/v1/chat/conversations/{id}/messages` | Gửi tin nhắn chữ, body `{ "content": "..." }` |
| POST | `/api/v1/chat/conversations/{id}/image` | Gửi `multipart/form-data`, trường tệp là `image` |
| POST | `/api/v1/chat/conversations/{id}/read` | Đánh dấu toàn bộ tin trong phòng đã đọc |
| POST | `/api/v1/chat/conversations/{conversationId}/messages/{messageId}/recall` | Thu hồi tin của chính mình trong 5 phút |
| GET | `/api/v1/chat/messages/{id}/image` | Tải ảnh riêng tư của tin nhắn; cần Bearer token |
| GET | `/api/v1/chat/unread-count` | Tổng số tin chưa đọc |
| POST | `/api/v1/chat/conversations/{id}/report` | Báo cáo hội thoại, body `{ "reason": "..." }` |

`conversation` gồm `id`, `courseId`, `courseName`, `courseCoverUrl`, `participant`, `lastMessage`, `lastMessageAt`, `unreadCount`, `status`, `answered`.

`message` gồm `id`, `conversationId`, `senderId`, `senderName`, `senderAvatarUrl`, `content`, `imageUrl`, `sentAt`, `isMine`, `recalled`, `canRecall`, `recallSeconds`.

Quy tắc nghiệp vụ được áp dụng giống website: chỉ học viên đã ghi danh được khởi tạo chat, giáo viên chỉ trả lời sau tin đầu tiên của học viên, mỗi tài khoản tối đa 20 tin/phút, nội dung dài tối đa 2.000 ký tự, ảnh JPG/PNG tối đa 5 MB/20 MP, và tin chỉ thu hồi trong 5 phút. Ảnh dùng endpoint riêng tư nên app cần tải bằng HTTP client có gắn Bearer token, thay vì `Image.network` không có header.
