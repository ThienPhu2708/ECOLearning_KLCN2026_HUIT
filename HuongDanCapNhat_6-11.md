# Hướng dẫn cập nhật ứng dụng Flutter theo chức năng học viên trên web

Ngày lập: 06/11/2026; cập nhật theo mã backend hiện tại. Phạm vi: trang chủ, không gian học tập, phụ đề, tài liệu và thảo luận bài học. Tài liệu này là bàn giao kỹ thuật, không có nghĩa là app đã được sửa hoặc kiểm thử.

## 1. Dự án và tài liệu tham chiếu

- Web/API: `D:\Nam4_DH\KLTN\ECOLearning` — ASP.NET MVC 5 / .NET Framework 4.8.
- App: `D:\Nam4_DH\KLTN\ECOLearning_App` — Flutter/Dart.
- Hợp đồng API cô đọng: `Database/HUONG_DAN_API_HOC_VIEN.md` trong dự án web.
- Backend cần migration `Database/Migrations/032_LessonDiscussion.sql` và `033_LessonDiscussionUtc.sql` trên **đúng database mà hosting đang dùng**. Migration 032 tạo `ThaoLuanBaiHoc` và `YeuThichThaoLuanBaiHoc`; không xóa `HoiThoai`/`TinNhan` cũ. Trước 033 phải sao lưu và đọc `033_HuongDan.md`; local đã chạy với `SE Asia Standard Time`, hosting phải xác minh trạng thái và dữ liệu cũ trước khi dùng `W. Europe Standard Time`.
- Các thay đổi API phải được build/publish lên hosting trước khi app gọi. Code có trong máy local không đồng nghĩa API trên hosting đã được cập nhật. Trên thiết bị thật, `localhost` là chính thiết bị, không phải máy chạy Visual Studio.

## 2. Mục tiêu và tình trạng đã đối chiếu

| Nội dung | Web/API hiện tại | App Flutter được đọc tại thời điểm lập tài liệu | Cần làm |
|---|---|---|---|
| Trang chủ | `GET /api/v1/home` trả các mục qua `HomeSectionService` | Đã có `HomeRepository`, `home_screen.dart`, `home_feed_sections.dart` | Đối chiếu từng mục, không tự bù khóa sai nhãn; kiểm thử dữ liệu thật |
| Gợi ý cá nhân | Có `GET /recommendations/personal` và `POST /recommendations/personal/refresh` | `RecommendationRepository.recommendations()` đang gọi `/recommendations` cũ | Thêm màn danh sách cá nhân và làm mới đúng API mới; giữ endpoint cũ nếu còn cần cho nội dung trang chủ |
| Video / CC | `hasSubtitle`, `subtitle.url`, endpoint WebVTT | `Lesson` hiện chưa parse thông tin phụ đề; có `lesson_video_player.dart` | Luôn có CC cho video, tải VTT có xác thực, đồng bộ theo thời gian video |
| Tài liệu | Danh sách/đường dẫn tải có API riêng | Đã có `lesson_documents_tab.dart`, `lesson_resources_repository.dart` | Rà soát nút, nhiều tệp, tên dài và xác thực tải |
| Thảo luận | API thảo luận theo bài, ảnh, thích, phản hồi; SignalR 2 | Trang học còn dùng `ChatEntry` và `TeacherChatRepository` cho chat 1–1 | Thêm repository/model/UI thảo luận mới, chuyển nút ở trang học sang đúng bài học |
| Bài khóa | API trả `isUnlocked`; web khóa theo tiến độ bài trước | App có `LearningController.isUnlocked()` | Đồng bộ với server, kiểm tra cả API và thao tác UI |

Danh sách trên là kết quả đọc mã nguồn những vùng liên quan, **chưa phải kết quả chạy app hoặc kiểm thử toàn bộ chức năng học viên**. Cần kiểm chứng trên phiên bản app/backend thực tế trước khi kết luận chức năng nào đang hoạt động.

## 3. Các tệp Flutter cần xem

Các đường dẫn tính từ `ECOLearning_App`:

```text
lib/main.dart
lib/core/config/api_config.dart
lib/data/models/lesson.dart
lib/data/models/home_feed.dart
lib/data/repositories/learning_repository.dart
lib/data/repositories/lesson_resources_repository.dart
lib/data/repositories/home_repository.dart
lib/data/repositories/recommendation_repository.dart
lib/data/repositories/teacher_chat_repository.dart       # chat riêng cũ
lib/presentation/screens/home/home_screen.dart
lib/presentation/widgets/home_feed_sections.dart
lib/presentation/screens/learning/learning_space_screen.dart
lib/presentation/screens/learning/learning_controller.dart
lib/presentation/screens/learning/lesson_video_player.dart
lib/presentation/screens/learning/lesson_documents_tab.dart
lib/presentation/screens/learning/lesson_notes_tab.dart
lib/presentation/screens/messages/chat_entry.dart         # nút chat riêng cũ
lib/presentation/screens/learning_support/learning_data_scope.dart
test/learning_test.dart
test/lesson_video_player_test.dart
test/lesson_resources_api_test.dart
test/home_feed_test.dart
test/api_learning_contract_test.dart
```

Đề xuất bổ sung `lesson_discussion_repository.dart`, model thảo luận, widget/screen thảo luận và test tương ứng. Trước khi viết hãy đọc `ApiClient`, cơ chế `LearningDataScope`, session và test fixtures; dùng dependency injection hiện có để test được, không tạo mạng thật trong widget test.

## 4. Cấu hình kết nối và xác thực

1. Xác định base URL hiện hành trong `ApiConfig`/cấu hình chạy. Đường dẫn trong tài liệu dưới đây là tương đối với `/api/v1`.
2. API cần đăng nhập nhận `Authorization: Bearer <token>`. Đọc token qua cơ chế bảo mật sẵn có của app; không hard-code token, API key hoặc thông tin cá nhân; không đưa token vào query string, log hay analytics.
3. Endpoint trả JSON chuẩn `{ "success": true, "message": null, "data": ... }`; lỗi có `success`, `code`, `message`, có thể có `errors`. Xem `ApiControllerBase.cs` và `ApiClient` để tránh bóc `data` hai lần.
4. 401: xử lý phiên hết hạn theo luồng đăng nhập chung. 403: không có quyền. 404: tài nguyên/bài không tồn tại hoặc thiếu phụ đề/ảnh. 5xx/mất mạng: thông báo thân thiện và cho thử lại. Không hiện exception thô.
5. Một số API trả file nhị phân (`text/vtt`, ảnh, tài liệu), không phải JSON: tải bằng HTTP client hỗ trợ header và response bytes/text thích hợp. Không dùng trực tiếp widget tải ảnh/video nếu nó không gửi được Bearer token.
6. CORS không thay thế xác thực. Với Flutter Web, kiểm tra riêng CORS và khả năng gửi header; Android/iOS cần kiểm tra certificate HTTPS và cấu hình mạng của môi trường thử nghiệm. Không tắt kiểm tra TLS trong bản phát hành.

## 5. Trang chủ học viên

### 5.1 Dữ liệu trang chủ

`GET /api/v1/home` có thể nhận Bearer token; khi đã đăng nhập trả `data.isPersonalized` và danh sách `sections`. Thứ tự và nội dung lấy từ backend, mỗi mục có thể vắng mặt khi không có dữ liệu hoặc lỗi riêng.

Các key web/API đang dùng:

| Key | Nội dung | Cách hiển thị |
|---|---|---|
| `continue-learning` | Học tiếp | Mở đúng `courseId`/`lessonId`; dùng `positionSeconds`, tiến độ từ API |
| `favorites` | Khóa học yêu thích | Dùng danh sách và `total` backend trả |
| `best-sellers` | Bán chạy theo kỳ | Dùng `period`, `badge`, `enrollmentsInPeriod`; không tự thêm khóa khác để đủ số lượng |
| `because-you-learned` | Vì bạn đã học khóa mốc | Hiện tiêu đề/`anchorCourseTitle`; các khóa do backend chọn cùng danh mục với khóa vừa học |
| `top-rated` | Đánh giá cao | Hiện đúng dữ liệu/nhãn của mục |
| `new-releases` | Mới ra mắt | Hiện badge và note từ server nếu có |
| `featured-teachers` | Giáo viên nổi bật | Hiện đúng kiểu dữ liệu giáo viên |
| `recently-viewed` | Đã xem gần đây | Chỉ hiện khi backend trả |

`best-sellers` có thể dùng kỳ tuần, 30 ngày hoặc toàn thời gian theo quyết định của server; không giả định mọi dữ liệu đều là tuần này. Không tự đổi nhãn thành “Bán chạy” cho khóa do mục khác trả. Nếu API trả danh sách rỗng, ẩn mục hoặc hiện trạng thái phù hợp; không trộn kết quả phổ biến vào mục cá nhân. Một khóa có thể đủ điều kiện nhiều nhãn: model/UI nên hỗ trợ danh sách nhãn, nhưng API hiện có `badge`/`tag` đơn lẻ tùy endpoint; không tự suy ra thêm nhãn khi thiếu dữ liệu.

### 5.2 Trang “Khóa học gợi ý” và làm mới

Endpoint cũ `GET /recommendations` phục vụ khối gợi ý trang chủ, có thể trả `type=popular` và khóa bù. Đừng đổi ý nghĩa của endpoint cũ một cách âm thầm.

- `GET /api/v1/recommendations/personal?limit=12`: yêu cầu đăng nhập. `data` có `interactedCourses`, `total`, `items`, `badge`. Các `items` là khóa phù hợp của trang cá nhân, không bù khóa phổ biến.
- `POST /api/v1/recommendations/personal/refresh?limit=12`: tính lại cho người hiện tại dựa trên ma trận chung đã có; trả cùng cấu trúc để cập nhật UI. Không cần thông báo thành công; có thể bấm lại sau khi request trước kết thúc. Nút hiển thị “Đang làm mới...” và chống gửi song song trong lúc request đang chạy.
- `limit` server giới hạn 1–30. Không tạo cooldown 45 giây vì yêu cầu hiện tại cho phép bấm nhiều lần.
- Lịch tính ma trận chung không bị thay đổi. Nếu tương tác chưa đổi, gợi ý có thể không đổi; không xáo trộn ngẫu nhiên để tạo cảm giác thay đổi.
- Hiện đúng số lượng trả về, tất cả có nhãn “Phù hợp với bạn”; nếu không có kết quả, hiện trạng thái trống phù hợp thay vì bù khóa phổ biến. Giữ các nhãn khác khi model/API thực sự có dữ liệu.
- Demo: xem/yêu thích một khóa phù hợp điều kiện ở lĩnh vực khác, sau đó làm mới. Không hứa chắc danh sách thay đổi vì phụ thuộc ma trận và dữ liệu.

## 6. Không gian học tập

### 6.1 Lấy đề cương và chi tiết bài

- `GET /api/v1/learning/{courseId}`: khóa học, tiến độ, chương và danh sách bài. Mỗi bài có `id`, `title`, `durationSeconds`, `isPreview`, `isCompleted`, `lastPositionSeconds`, `documentsCount`, `hasDocuments`, `hasSubtitle`, `isUnlocked`.
- `GET /api/v1/learning/{courseId}/lessons/{lessonId}`: `id`, `title`, `videoUrl`, `videoType`, `hasSubtitle`, `subtitle`, `durationSeconds`, `lastPositionSeconds`, `isCompleted`, `isUnlocked`, `hasDocuments`, `previousLessonId`, `nextLessonId`, `documents`.
- `POST /api/v1/learning/{courseId}/lessons/{lessonId}/progress`: JSON `positionSeconds`, `durationSeconds`. Tiến độ và hoàn thành dựa vào phản hồi server. Không tự ghi hoàn thành chỉ từ giao diện.
- Mở bài đầu, bài đã hoàn thành hoặc bài sau bài đã hoàn thành theo `isUnlocked` và logic web. **Lưu ý kỹ thuật:** trường `isUnlocked` hiện được trả cho client, không được coi là bằng chứng mọi endpoint phát video đã thực thi cùng quy tắc khóa; nếu cần chống truy cập trực tiếp, backend phải kiểm tra quyền ở endpoint tương ứng và test riêng.
- Khi đổi bài: lưu tiến độ theo cơ chế hiện có; hủy request/timer/video/subtitle/discussion của bài cũ; tránh response chậm làm dữ liệu bài cũ ghi đè bài mới. Giữ đúng hành vi học tiếp và fullscreen hiện tại.
- Không thay đổi các workflow quiz, chứng chỉ, đánh giá, ghi chú hay thanh toán chỉ vì sửa UI; chạy regression cho các luồng này.

### 6.2 Nút CC và WebVTT

Web luôn hiện biểu tượng CC trên trình điều khiển nếu bài có video. Nếu bài **không có phụ đề do hệ thống cung cấp**, bấm CC hiện “Video hiện chưa hỗ trợ phụ đề” đúng khoảng 3 giây, sau đó tự tắt. Không kết luận video YouTube không có phụ đề riêng của YouTube; thông tin `hasSubtitle` chỉ phản ánh file của hệ thống.

Nếu `hasSubtitle=true`, `subtitle` có dạng:

```json
{"language":"vi","label":"Tiếng Việt","format":"webvtt","url":"https://HOST/api/v1/learning/COURSE/lessons/LESSON/subtitle"}
```

`GET subtitle.url` trả `text/vtt; charset=utf-8`, không bọc JSON; yêu cầu Bearer token. File nằm ở `App_Data` trên server. 404 `SUBTITLE_NOT_FOUND` có thể xảy ra nếu file bị xóa sau khi app đã nhận metadata: hiển thị lỗi thân thiện, không coi là đã tải thành công.

Đề xuất triển khai:

1. Thêm model phụ đề và parse `hasSubtitle`, `subtitle` trong `Lesson`/model chi tiết; có default an toàn khi API cũ chưa trả trường.
2. Tải nội dung VTT khi cần, qua `ApiClient`/Dio gửi token; parse các cue `start`, `end`, `text`. Hỗ trợ `WEBVTT`, dòng trống, cue identifier tùy chọn, timestamp có/không có giờ, nhiều dòng, BOM và ký tự tiếng Việt. Không parse bằng cách chia cố định mỗi ba dòng.
3. Đồng bộ cue với `VideoPlayerController.value.position`; xử lý seek, pause, resume, tua, thay bài. Với nội dung không đáng tin, hiển thị dưới dạng văn bản, không render HTML tùy ý.
4. CC bật/tắt hiển thị, có trạng thái truy cập được cho screen reader. Lưu lựa chọn người dùng nếu muốn giống web (`eco_subtitle_vi` là localStorage của web, không dùng chung thiết bị). Tắt CC không xóa dữ liệu đã tải.
5. Inline và fullscreen dùng cùng trạng thái, cùng cue và vị trí; không reset video hoặc caption khi vào/thoát fullscreen. Nút CC phải dùng được cả hai chế độ; thanh điều khiển vẫn tự ẩn khi đang phát theo hành vi hiện có.
6. Với video YouTube/nguồn iframe, đánh giá riêng khả năng điều khiển: track VTT của hệ thống không thể tự gắn vào iframe tùy ý. Nếu player YouTube không hỗ trợ overlay, xác định giải pháp hiển thị caption ngoài iframe hoặc hỗ trợ phụ đề YouTube gốc; đừng báo hỗ trợ nếu chưa chạy thực tế.
7. Khi video đổi hoặc màn hình đóng, hủy listener/timer, tránh giữ token và tránh cue cũ. Nếu dùng package parse VTT hoặc player mới, kiểm tra tương thích nền tảng, license và test trước khi thêm dependency.

### 6.3 Tài liệu và ghi chú

- `GET /api/v1/learning/{courseId}/lessons/{lessonId}/documents`: danh sách tài liệu bài. `GET /api/v1/learning/{courseId}/documents` lấy cả khóa.
- `GET /api/v1/documents/{documentId}/download`: tải tệp sau khi xác thực; backend có thể trả redirect cho liên kết ngoài. Không tự chuyển Bearer token sang host ngoài khác; kiểm tra an toàn redirect của HTTP client.
- Nếu không có tài liệu hợp lệ: nút “Tải tài liệu” disabled. Một tệp: bấm tải/mở trực tiếp. Nhiều tệp: hiện danh sách để chọn; tên quá dài dùng ellipsis, có thể xem tên đầy đủ bằng màn chi tiết/tooltip, không kéo vỡ layout.
- Đọc trường thực tế từ `DocumentJson` trong `LearningApiController` và model app, không giả định file luôn còn tồn tại (`available`/dữ liệu thực tế cần xác nhận). Thông báo khi tệp không còn.
- Ghi chú đã có API đồng bộ; giữ các endpoint `learning/{courseId}/notes`, `learning/{courseId}/lessons/{lessonId}/notes`, `PUT/DELETE /notes/{noteId}`. Không tạo kho ghi chú cục bộ thứ hai làm lệch dữ liệu.

## 7. Thảo luận nhiều người theo bài học

### 7.1 Nghiệp vụ và UI

- Thảo luận gắn với **một bài học**, không phải một cuộc trò chuyện riêng. Người tham gia là học viên đã ghi danh khóa và giáo viên chủ khóa; backend kiểm tra quyền. Không hiển thị thông tin hồ sơ cá nhân, email, số điện thoại hoặc liên kết xem hồ sơ người bình luận; chỉ avatar, tên, nhãn giảng viên nếu có.
- Trên trang học, thay luồng `ChatEntry` 1–1 bằng nút “Thảo luận bài học” và mở panel/bottom sheet/screen mới theo thiết kế app. Giữ riêng bảng, API và màn inbox 1–1 cũ để không mất dữ liệu; không đưa nhầm lịch sử riêng vào thảo luận.
- Đầu danh sách: tổng `totalComments` bao gồm phản hồi. Mỗi bình luận: avatar, tên, thời gian tương đối, nội dung, ảnh nếu có, `Thích` và `Phản hồi`; không hiện dấu `...` hay dòng nhắc report spam khi chưa có nghiệp vụ tương ứng.
- Web mới hiển thị bình luận và phản hồi theo một dòng thời gian: tin gửi trước ở trên, tin gửi sau ở dưới. Phản hồi có trích dẫn tên/nội dung của bình luận gốc. Bấm Phản hồi cần cho biết rõ người đang được trả lời ngay trên ô nhập; có thể đóng trạng thái trả lời để trở về bình luận chung. Không xóa mục tiêu phản hồi chỉ vì người dùng chạm lại ô nhập. Bấm Gửi truyền `parentId`; bình luận chung không truyền hoặc truyền `null`. Backend hiện gom phản hồi về bình luận gốc, không hỗ trợ chuỗi lồng vô hạn.
- Nút Thích thể hiện `likedByMe`, `likeCount`; tránh bấm song song dẫn tới đảo trạng thái liên tục. Khi lỗi, khôi phục trạng thái hoặc tải lại.
- Biểu tượng cảm xúc là Unicode trong nội dung. Thêm picker phù hợp mobile; giữ con trỏ, không cắt đôi surrogate pair/emoji, bảo đảm giới hạn ký tự server. Nên hỗ trợ bàn phím emoji hệ điều hành.
- Cho phép ảnh JPG/PNG <= 5 MB, xem trước và bỏ ảnh trước gửi; có thể chỉ gửi ảnh hoặc ảnh kèm chữ. Web chuyển ảnh sang JPEG và giới hạn 20 megapixel. Hiện lỗi dễ hiểu; không tin MIME/phần mở rộng phía client để thay thế kiểm tra server.
- Web desktop: Enter gửi, Alt+Enter xuống dòng. Trên điện thoại có thể dùng phím gửi của bàn phím và nút Gửi; không bắt buộc Alt trên bàn phím ảo. Không gửi khi đang nhập bằng IME/composition.
- Thời gian `2 ngày trước`, `2 tuần trước` là thời gian đăng bình luận, **không phải** các nút thống kê/lọc. Sau migration 033, `createdAt` là thời gian UTC có hậu tố `Z`; parse theo ISO 8601 và chuyển sang local khi cần. Không cộng cố định 6 hoặc 7 giờ.

### 7.2 API thảo luận

Các đường dẫn tương đối với `/api/v1`; gửi token trong header.

**Lấy dòng thời gian giống web — khuyến nghị cho app mới**

```http
GET /learning/{courseId}/lessons/{lessonId}/discussions/timeline?page=1&pageSize=30
Authorization: Bearer <token>
```

`data.items` là danh sách phẳng gồm cả bình luận chính và phản hồi, sắp xếp từ cũ đến mới **trong trang**. Trang 1 lấy tối đa 30 tin mới nhất; `page=2` lấy nhóm cũ hơn. `total` và `totalComments` đều là tổng số tin bao gồm phản hồi; `totalPages` dựa trên số tin. Giới hạn `pageSize` từ 1 đến 50. Khi tải trang cũ, đặt dữ liệu trang 2 trước trang 1 và loại trùng theo `id`. Vì phân trang dùng offset, tin mới đến giữa hai lần tải có thể làm dịch trang: loại trùng và tải lại khi cần; hiện chưa có cursor cố định.

Mỗi tin có `id`, `parentId`, `content`, `createdAt` UTC, `authorName`, `authorAvatar`, `isTeacher`, `isMine`, `likeCount`, `likedByMe`, `hasImage`, `imageUrl`, `replyToAuthor`, `replyToContent` và `replies` (trống trong timeline). Với tin gốc, `parentId`/thông tin trích dẫn là null; với phản hồi, hiện “Phản hồi [replyToAuthor]” cùng `replyToContent`. Nếu bình luận gốc đã bị ẩn hoặc không còn thấy được, thông tin trích dẫn có thể null; không tự hiển thị thông tin cá nhân khác. `isMine=true` căn phải, còn lại căn trái. Nhãn giáo viên là “Giáo viên”.

Ví dụ `data.items` (rút gọn):

```json
[
  {"id": 100, "parentId": null, "content": "Em có câu hỏi", "createdAt": "2026-11-06T05:00:00.0000000Z", "authorName": "Học viên", "isMine": false, "replyToAuthor": null, "replyToContent": null},
  {"id": 101, "parentId": 100, "content": "Em xem lại ví dụ nhé", "createdAt": "2026-11-06T05:02:00.0000000Z", "authorName": "Giáo viên", "isMine": true, "replyToAuthor": "Học viên", "replyToContent": "Em có câu hỏi"}
]
```

Ví dụ chỉ minh họa các trường quan trọng; response thực tế vẫn bao gồm `success`, `message`, `data` và các trường ảnh/lượt thích/phân trang. Khi có tin mới, lấy lại trang 1; nếu người dùng đang xem tin cũ, không bắt buộc kéo xuống đáy. Sau khi chính người dùng gửi thành công, cuộn xuống tin mới.

**API danh sách theo nhóm cũ — giữ tương thích**

```http
GET /learning/{courseId}/lessons/{lessonId}/discussions?page=1&pageSize=20
Authorization: Bearer <token>
```

Ví dụ khung dữ liệu (các giá trị chỉ minh họa):

```json
{
  "success": true,
  "message": null,
  "data": {
    "items": [{
      "id": 123, "parentId": null, "content": "Em có câu hỏi 😊",
      "createdAt": "2026-11-06T02:00:00.0000000Z",
      "authorName": "Học viên", "authorAvatar": null,
      "isTeacher": false, "isMine": true, "likeCount": 0,
      "likedByMe": false, "hasImage": false, "imageUrl": null,
      "replies": []
    }],
    "page": 1, "pageSize": 20, "total": 1, "totalPages": 1,
    "totalComments": 1, "totalParticipants": 1
  }
}
```

Với endpoint danh sách cũ này, `total` là số bình luận gốc; `totalComments` gồm gốc và phản hồi. Server giới hạn pageSize 1–50. Phản hồi của từng gốc nằm trong `replies`. Endpoint cũ không bảo đảm thứ tự thời gian chung giữa các gốc và phản hồi; dùng endpoint `/timeline` cho giao diện chat mới. Nếu backend trả dữ liệu trống hoặc 403, giữ đúng trạng thái trống/không có quyền; không hiện 0 như thể tải thành công khi request lỗi.

**Gửi chữ/emoji hoặc phản hồi**

```http
POST /learning/{courseId}/lessons/{lessonId}/discussions
Content-Type: application/json
Authorization: Bearer <token>

{"content":"Em muốn hỏi thêm 😊","parentId":null}
```

Khi trả lời: `parentId` là ID bình luận gốc. JSON trả `data` là bình luận mới. Server giới hạn nội dung 2.000 ký tự khi không có ảnh; có giới hạn tốc độ 15 lần/phút. Nếu request timeout sau khi server đã ghi, không tự gửi lại mù quáng vì có thể tạo trùng; tải lại danh sách để đối chiếu. API hiện chưa có idempotency key.

**Gửi ảnh**

```http
POST /learning/{courseId}/lessons/{lessonId}/discussions/image
Content-Type: multipart/form-data; boundary=...  # để thư viện HTTP tự tạo boundary
Authorization: Bearer <token>

image: <file JPG hoặc PNG>
content: <tùy chọn>
parentId: <tùy chọn>
```

Không gửi base64 vào JSON thay file multipart. Văn bản kèm ảnh có giới hạn thấp hơn (1.943 ký tự theo code hiện tại), do server lưu marker ảnh trong cùng trường nội dung. Client nên hiển thị giới hạn phù hợp và xử lý lỗi server. App cần kiểm tra quyền truy cập ảnh picker tương ứng nền tảng, không yêu cầu quyền không cần thiết.

**Xem ảnh**

`GET /learning/{courseId}/lessons/{lessonId}/discussions/{discussionId}/image` trả JPEG nhị phân, yêu cầu Bearer; `imageUrl` là URL đầy đủ. Dùng image provider hỗ trợ `headers` hoặc tải bằng client đã xác thực; cache ảnh riêng tư cẩn thận và xóa cache nhạy cảm khi đổi tài khoản. Không gắn Bearer vào URL.

**Thích**

`POST /learning/{courseId}/lessons/{lessonId}/discussions/{discussionId}/likes/toggle` trả `data.likeCount`, `data.likedByMe`. Chỉ cho phép một request toggle tại một thời điểm trên mỗi bình luận.

Endpoint `/discussions/stats?days=...` vẫn tồn tại để tương thích, nhưng UI mới không sử dụng các nút 2/4/7/14/21 ngày. Không nhầm thống kê này với nhãn thời gian tương đối.

### 7.3 Real-time

Backend dùng **ASP.NET SignalR 2**, không phải ASP.NET Core SignalR và không phải WebSocket thuần. `Startup.cs` hỗ trợ cookie web hoặc Bearer trong header `Authorization`; `LessonDiscussionHub` kiểm tra người tham gia trước khi join.

1. Dùng client tương thích SignalR 2 và kiểm chứng Android/iOS; truyền `Authorization: Bearer <token>` trên các request transport được dùng. Không truyền token qua query string. Không mặc định thư viện `signalr_netcore` tương thích SignalR 2.
2. Kết nối tới gốc `/signalr`, hub `lessonDiscussionHub`; sau kết nối gọi `joinLesson(courseId, lessonId)`. Khi đổi bài gọi `leaveLesson(oldLessonId)` rồi join bài mới. Khi rời màn, đóng subscription/kết nối phù hợp vòng đời app.
3. Sự kiện `discussionCommentAdded`: tải lại trang phù hợp hoặc đồng bộ an toàn theo ID. Payload broadcast có trạng thái cá nhân của người gửi, không dùng `isMine`/`likedByMe` của payload cho người khác; lấy danh sách API để có trạng thái đúng và `imageUrl`.
4. `discussionLikeChanged` có `id`, `likeCount`; chỉ cập nhật số lượt cho bình luận tương ứng, không thay `likedByMe` của người đang xem nếu người khác bấm.
5. Sau mất mạng/reconnect, join lại nhóm và tải lại dữ liệu để bù sự kiện đã mất. Có thể dùng polling khi client không gửi header được, nhưng đó là fallback, không được gọi là real-time.
6. Tránh gọi liên tục khi màn hình ở nền, không tạo nhiều kết nối cho mỗi lần build widget. Thực hiện kiểm thử hai tài khoản trên hai thiết bị và hai bài học khác nhau. Nếu cần đảm bảo quyền bị thu hồi ngay trong kết nối đang mở, backend cần cơ chế kiểm tra/loại khỏi nhóm; không giả định join một lần giải quyết mọi thay đổi quyền.

## 8. Phân quyền, dữ liệu và an toàn

- API thảo luận phải kiểm tra cả tài khoản, khóa và bài thuộc khóa. UI ẩn nút không thay cho xác thực ở server. Test học viên đã mua, chưa mua, giáo viên chủ khóa, tài khoản bị khóa, bài khác khóa.
- Ảnh bình luận được lưu tại `App_Data/LessonDiscussionImages` trên hosting, **chưa dùng R2**. Hosting cần quyền ghi/đọc, sao lưu, không xóa thư mục này khi publish. Nhiều instance cần storage dùng chung; máy chủ restart/triển khai không được xóa ảnh ngoài ý muốn.
- Bản mới của endpoint gợi ý, ảnh và Bearer SignalR phải được deploy; migration `032` phải được áp dụng vào database hosting. Không đưa `Secrets.config`, key, token, ảnh thật hoặc dữ liệu cá nhân vào Git.
- Nội dung người dùng là văn bản; không hiển thị HTML tùy ý. Không cho xem hồ sơ người bình luận. Hạn chế truy vấn thừa, dọn listener và giữ thông tin được tải về phù hợp chính sách ứng dụng.

## 9. Kế hoạch triển khai đề xuất

1. Mở workspace `ECOLearning_App`; đọc `AGENTS.md` nếu dự án có, chạy `git status` để giữ thay đổi người dùng.
2. Xác nhận phiên bản API hosting và migration; tạo hai tài khoản học viên cùng ghi danh, một giáo viên chủ khóa và một tài khoản không ghi danh để kiểm thử.
3. Cập nhật model/repository bài học: parse trường mới, tải VTT bằng token, giữ fallback cho backend cũ nếu cần triển khai độc lập.
4. Thêm CC vào player inline/fullscreen, thông báo 3 giây, parser và đồng bộ cue; viết test parser + widget + seek.
5. Thêm model/repository thảo luận, mapping ảnh và xử lý phân trang; viết test endpoint mock, lỗi, parentId, upload multipart và auth headers.
6. Thay `ChatEntry` trên trang học bằng giao diện thảo luận theo bài; triển khai gửi, phản hồi, thích, emoji, ảnh và trạng thái rỗng/lỗi/loading.
7. Kết nối SignalR 2 hoặc fallback đã được ghi rõ; test chuyển bài, reconnect, hai tài khoản. Không hứa real-time nếu mới chỉ polling.
8. Cập nhật gợi ý cá nhân và làm mới; đối chiếu `GET /home`, các nhãn, danh sách rỗng và điều kiện đăng nhập.
9. Chạy format/analyze/test; sau đó kiểm thử thiết bị thật/emulator và backend hosting, ghi rõ phiên bản, kết quả, lỗi còn lại.

## 10. Danh sách kiểm thử nghiệm thu

### Trang chủ và gợi ý

- [ ] Khách và học viên đăng nhập nhận đúng nhóm section; thiếu section không gây crash.
- [ ] `best-sellers` chỉ có các khóa backend trả với nhãn bán chạy; period/tiêu đề khớp server.
- [ ] “Vì bạn đã học” dùng đúng khóa mốc và các khóa cùng lĩnh vực; không tự thêm khóa khác.
- [ ] Trang gợi ý cá nhân có 0, 3, 4 và nhiều kết quả; số lượng và badge khớp; không bù phổ biến.
- [ ] Làm mới nhiều lần không chặn 45 giây, không gọi đồng thời; trạng thái loading/lỗi rõ ràng; nếu tương tác không đổi kết quả giống nhau là hợp lệ.

### Video, CC, tài liệu

- [ ] Không phụ đề: CC hiện, bấm hiện đúng thông báo 3 giây, không gây crash; bấm liên tiếp thời gian hiển thị được làm mới.
- [ ] Có phụ đề: VTT tải với token, tiếng Việt hiển thị đúng nội dung và mốc; seek từ đầu/giữa/cuối không kẹt caption cũ.
- [ ] Pause/play, tua, đổi tốc độ, fullscreen vào/ra, xoay màn hình, đổi bài và mất mạng không làm mất đồng bộ hoặc rò rỉ controller.
- [ ] Test video ngắn, video trên 9 phút, cue nhiều dòng, khoảng im lặng, Unicode; VTT 401/404/hỏng; video YouTube nếu được hỗ trợ.
- [ ] 0/1/nhiều tài liệu, tên cực dài, tệp hỏng/mất, link ngoài, tải với token, xoay màn hình.
- [ ] `isUnlocked`, tiến độ 80%, xem tiếp, ghi chú, quiz, chứng chỉ và đánh giá không bị hồi quy.

### Thảo luận và real-time

- [ ] Bình luận chung, bình luận emoji, ảnh, chữ+ảnh, ảnh-only; chặn ảnh sai định dạng/quá giới hạn; không mất bản nháp khi lỗi.
- [ ] Chọn phản hồi đúng tên, chạm lại ô nhập không mất mục tiêu, xóa mục tiêu về bình luận chung; gửi đúng `parentId`, hiển thị trích dẫn và theo thứ tự thời gian chung.
- [ ] Hai học viên + giáo viên cùng bài thấy bình luận/thích mới; bài/khóa khác không nhận; 1–1 cũ không lẫn với thảo luận.
- [ ] Timeline: trang 1 mới nhất, tin cũ hơn ở phía trên; phản hồi mới về tin gốc cũ vẫn xuất hiện; tải trang trước không trùng ID. Kiểm tra tổng gồm phản hồi, ảnh tải có header, tên dài/không avatar, bàn phím và màn hình nhỏ.
- [ ] Mất mạng, timeout khi server có thể đã ghi, reconnect, đổi bài trong lúc gửi/tải, đổi tài khoản, token hết hạn/thu hồi.
- [ ] Tài khoản chưa ghi danh không đọc/gửi/tải ảnh; không lộ email, số điện thoại hoặc hồ sơ người bình luận.

### Trước phát hành

- [ ] `flutter analyze`, các bài test liên quan (`flutter test` hoặc nhóm test khi toàn bộ suite quá lâu); bổ sung test mới cho model/repository/player/thảo luận.
- [ ] Build Android/iOS theo môi trường đội phát triển; kiểm tra trên emulator và ít nhất một thiết bị thật nếu có.
- [ ] Kiểm tra URL hosting, HTTPS, migration, quyền thư mục ảnh, proxy SignalR, CORS nếu có Flutter Web.
- [ ] Kiểm tra Git: không commit token, `Secrets.config`, ảnh/tệp người dùng hoặc dữ liệu database.

## 11. Các giới hạn cần phối hợp backend

1. App hiện chưa được chỉnh trong lần viết tài liệu này. API mới cũng phải được publish mới có trên hosting.
2. `isUnlocked` chỉ là thông tin client ở code API đã kiểm tra; muốn chặn bỏ qua khóa bài bằng cách gọi API trực tiếp cần kiểm toán quyền ở backend.
3. API ảnh yêu cầu Bearer; player/image widget mặc định có thể không gửi header. Cần kiểm thử client cụ thể thay vì giả định.
4. SignalR 2 qua proxy, transport và Bearer cần test thực tế trên SDK mobile. Nếu client không hỗ trợ header ở một transport, cần thống nhất giải pháp backend an toàn; polling không phải real-time.
5. API gửi bình luận chưa có idempotency key. Retry sau timeout có thể tạo bản sao; cần phối hợp backend nếu muốn bảo đảm gửi đúng một lần.
6. Thảo luận hiện là phản hồi một cấp; chưa có API báo cáo, chỉnh sửa, xóa bình luận hay thông báo chưa đọc của thảo luận mới. Không thiết kế nút cho các tính năng đó như thể đã hoạt động.
7. Ảnh hiện nằm trên filesystem hosting chứ không phải R2. Khi chuyển sang R2 cần thiết kế migration/lưu trữ và giữ tương thích các ảnh đã có.
8. Tài liệu này mô tả các vùng đã đối chiếu; các chức năng khác như giỏ hàng, thanh toán, hồ sơ, quiz cần audit riêng theo yêu cầu và phiên bản thực tế.
