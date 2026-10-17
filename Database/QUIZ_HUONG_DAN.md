# Quiz cuối khóa và chứng chỉ (nhánh ngoc)

## Cài đặt

Chạy `Database/Migrations/007_Quiz_ChungChi.sql` trên database ứng dụng, sau đó build lại web.
Máy Ngọc đã chạy migration này. File chạy lại được, không sửa dữ liệu học tập cũ.
Các bảng mới được truy cập qua SQL có tham số; không cần cập nhật EDMX bằng Visual Studio.

## Chạy trên máy khác sau khi lấy nhánh ngoc

1. Lấy code mới nhất của nhánh `ngoc`.
2. Mở SQL Server Management Studio, kết nối SQL Server đang chứa database ứng dụng.
3. Mở `Database/Migrations/007_Quiz_ChungChi.sql`, kiểm tra tên database ở dòng `USE`, rồi Execute toàn bộ file.
   Chạy một lần cho mỗi database; không cần chạy sau mỗi lần build. File có thể chạy lại.
   Nếu máy đã cập nhật đến migration 006 thì chỉ cần 007 cho tính năng quiz.
   Máy chưa có database phải khôi phục/tạo database nền trước; 007 không phải bản database đầy đủ.
4. Kiểm tra connection string trong Web.config trỏ đúng SQL Server/database trên máy đó, restore NuGet và Rebuild Solution trong Visual Studio.
5. Giáo viên nhập câu hỏi (hoặc tải mẫu Excel trong giao diện), cấu hình số câu theo mức độ rồi bật Mở quiz.
   Dữ liệu câu hỏi, lượt thi và chứng chỉ nằm trong database từng máy, không tự có khi pull code.
6. Học viên đã học đủ bài mới được thi; đạt quiz mới được nhận chứng chỉ và đánh giá khóa học.

Không cần chạy lại seed demo hoặc script xóa dữ liệu khi cập nhật tính năng này.

## Giáo viên

Tiến độ, điểm quiz, thống kê, chứng chỉ và xuất Excel được gộp vào **Quản lý học viên**.
Đường dẫn cũ `/Instructor/Quiz/Results` tự chuyển về trang này và giữ bộ lọc.

1. Vào **Ngân hàng câu hỏi & Quiz**, chọn khóa mình dạy.
2. Thêm câu hỏi: chủ đề, mức Dễ/Trung bình/Khó, 4 lựa chọn và một đáp án đúng.
   Có thể đổi tên chủ đề hoặc ngừng dùng câu hỏi. Ngừng dùng không xóa lịch sử bài thi.
3. Nhập Excel: tải mẫu `.xlsx`, giữ nguyên 10 cột, nhập tối đa 500 câu mỗi lần.
   Mức độ là 1/2/3, đáp án đúng là A/B/C/D, cho đảo đáp án là 0/1.
   Kiểm tra và xem trước; có lỗi thì không nhập dòng nào. Xác nhận mới lưu tất cả câu.
4. Cấu hình quiz: lấy câu ngẫu nhiên theo chủ đề và số lượng từng mức,
   hoặc chọn câu cố định khớp cơ cấu. Tối đa 100 câu, 180 phút, số lượt và tỷ lệ đạt tùy chỉnh.
   Có hai tùy chọn riêng để đảo câu và đảo đáp án. Câu phụ thuộc vị trí phải tắt đảo đáp án.
5. Chọn **Mở quiz** khi đủ câu, lưu rồi xem trước đề.
6. Vào **Quản lý học viên** để lọc theo khóa/học viên/trạng thái, xem thống kê,
   lịch sử bài làm, tiến độ từng bài và xuất Excel tổng hợp hoặc lịch sử.
7. Học viên học đủ bài và đạt ngưỡng quiz bấm **Nhận chứng chỉ** để hệ thống cấp ngay.
   Giáo viên chỉ theo dõi trạng thái, không cần duyệt chứng chỉ.
8. Học viên hết lượt và chưa đạt: mở **Chi tiết**, ghi lý do rồi **Cấp thêm 1 lượt**.
   Lượt bổ sung chỉ áp dụng cho học viên đó trong khóa đó, không xóa điểm và không tăng lượt cho cả lớp.
   Mỗi lần chỉ cấp một lượt khi học viên thực sự đã hết lượt; có lưu lịch sử và tối đa 100 lượt/người/khóa.

## Học viên

1. Trong trang học hoặc tab **Đã học đủ bài**, chọn **Làm Quizz ngay** (trang học có nút **Quiz & Chứng chỉ**).
2. Phải hoàn tất tất cả bài học và đã đăng ký khóa mới được bắt đầu quiz.
3. Mỗi lần bắt đầu dùng một lượt; tải lại hoặc mở thêm tab giữ nguyên lượt, đề và hạn giờ.
   Lựa chọn được tự lưu lên máy chủ trong khi có kết nối. Hết giờ sử dụng bản lưu cuối cùng;
   đáp án gửi muộn quá 5 giây không được dùng để thay đổi kết quả.
4. Điểm trên thang 10, điều kiện đạt xét tỷ lệ đúng chưa làm tròn. Xét điểm cao nhất.
   Chỉ hiện lời giải khi đã đạt; học viên chưa đạt vẫn có thể được giáo viên cấp thêm lượt,
   nên chưa công bố đáp án đúng. Giáo viên được xem lời giải sau khi nộp.
5. Học đủ bài + đạt quiz (ngưỡng do giáo viên đặt, ví dụ 80% hoặc 90%) → bấm Nhận chứng chỉ → cấp ngay và mở trang xem/in.
   Đường dẫn chứng chỉ cũ cũng kiểm tra điều kiện và bản ghi cấp thật.

## Trạng thái

Mỗi dòng trong quản lý học viên là một cặp học viên–khóa học, không gộp điểm hoặc tiến độ giữa các khóa.
Tiến độ được tính từ bài học thực tế, không dùng tiến độ giả của đơn hàng chưa ghi danh.
Trang học yêu cầu đúng tài khoản đăng nhập và lượt ghi danh. Giáo viên chủ khóa và admin có chế độ
xem trước không ghi tiến độ, không tự ghi danh. Không còn tự chọn một học viên bất kỳ khi chưa đăng nhập.
Lưu/đặt lại tiến độ có token chống giả mạo yêu cầu; bài cập nhật phải thuộc đúng khóa đã ghi danh.
Ba phần tách riêng: tiến độ bài học; quiz (chưa đủ điều kiện/chưa làm/đang làm/chưa đạt/đã đạt);
chứng chỉ (chưa đủ điều kiện/đủ điều kiện nhận/đã cấp).

Ngân hàng và cấu hình có thể thay đổi, nhưng mỗi lượt đã bắt đầu lưu bản sao nội dung,
đáp án, thứ tự, thời gian và ngưỡng đạt. Không sửa điểm trực tiếp.
Khi sửa/ngừng dùng câu khiến ngân hàng thiếu theo cơ cấu, quiz tự chuyển về nháp để giáo viên
kiểm tra và mở lại; học viên đang thi vẫn được hoàn tất đề đã lưu.
Tiến độ chỉ đếm bài của đúng khóa, và chưa học đủ thì không làm tròn lên 100%.
Kết quả hết giờ được chấm từ bản lưu, ghi ngày kết thúc theo hạn thi và đánh dấu **Hết giờ**.
Danh sách học viên, thống kê và xuất Excel đều dùng cùng cách xử lý bài đã hết hạn,
kể cả bài của tài khoản bị khóa sau khi bắt đầu.
Chứng chỉ đã được cấp là kết quả lịch sử: bổ sung bài học mới không thu hồi chứng chỉ hoặc điểm quiz cũ.
Trước lần nhận đầu tiên, hệ thống kiểm tra lại bài học và kết quả quiz ở máy chủ. Nhận lại không sinh mã mới.
Bản ghi chờ cấp cũ được cấp ngay khi học viên đủ điều kiện bấm Nhận chứng chỉ; giữ nguyên mã cũ.
Thống kê dùng điểm cao nhất của mỗi học viên–khóa trong bộ lọc; tỷ lệ đạt tính trên người đã có điểm.

## Kiểm tra và điểm quay lại

- Build bằng MSBuild của Visual Studio, .NET Framework 4.8.
- Chạy `Tests/Run-QuizTests.ps1` sau khi build để kiểm tra bằng dữ liệu tạm riêng.
  Bộ kiểm tra tự dọn các bản ghi nó tạo, không dùng kết quả học thật để thử chấm điểm.
- Khi IIS Express đã chạy và có dữ liệu demo 01, dùng Python môi trường Recommender chạy
  `Tests/QuizHttpSmoke.py http://localhost:PORT` để kiểm tra toàn bộ biểu mẫu và luồng HTTP.
  Script chỉ chấp nhận localhost và dọn khóa thử riêng trong khối finally.
- Nhánh mốc trước thay đổi: `ngoc-before-quiz-20261016`.
  Bản vá các thay đổi cũ chưa commit nằm tại `../tmp/ngoc-before-quiz-20261016.patch`.
- Không xóa bảng quiz để quay lại giao diện cũ: giữ bảng sẽ bảo toàn lịch sử đã tạo.
  Không dùng reset/clean khi chưa bảo toàn các file cá nhân và upload đang có.

Phạm vi bản này: trắc nghiệm một đáp án đúng, một quiz cuối khóa mỗi khóa;
chưa có tự luận hoặc nhiều quiz theo từng chương. Mỗi câu hiện có trọng số bằng nhau.
