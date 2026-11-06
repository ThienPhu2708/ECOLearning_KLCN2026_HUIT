# Triển khai migration 033 — thời gian thảo luận UTC

Không chạy tự động khi publish. Script chỉ tác động các bảng thảo luận và yêu thích thảo luận, không tác động chat riêng.

1. Sao lưu database và xác nhận có thể khôi phục. Tạm ngừng gửi/thích bình luận trong lúc chuyển đổi và triển khai code để không trộn định dạng giờ cũ/mới.
2. Chọn đúng database trên môi trường cần cập nhật và xác nhận migration 032 đã chạy.
3. Xác định **múi giờ của toàn bộ dữ liệu cũ**, không chỉ múi giờ hiện tại của server. Nếu từng nhập dữ liệu từ máy/hosting khác, cần xử lý riêng; không chạy đại trà. Hosting người dùng đã kiểm tra: `W. Europe Standard Time`; local có thể là `SE Asia Standard Time`.
4. Các thời điểm nằm trong khoảng giờ lặp hoặc không tồn tại khi chuyển giờ mùa hè không thể luôn xác định chính xác chỉ từ cột `datetime`. `AT TIME ZONE` áp dụng quy tắc SQL Server, không khôi phục được thông tin múi giờ đã mất. Kiểm tra các bản ghi liên quan và quyết định thủ công nếu cần độ chính xác tuyệt đối.
5. Mở `033_LessonDiscussionUtc.sql`, đặt `@LegacyTimeZone` thành tên múi giờ tương ứng, ví dụ `N'W. Europe Standard Time'` trên hosting. Script để `NULL` theo mặc định để ngăn chạy nhầm. Thử trước trên bản sao database với cùng phiên bản SQL Server, có `AT TIME ZONE` và `sys.time_zone_info`.
6. Chạy script một lần trên database đã sao lưu. Giao dịch tạo hai bảng sao lưu `LessonDiscussionUtc033Backup`, `LessonDiscussionLikeUtc033Backup` và marker `LessonDiscussionUtc033Marker`; script kiểm tra marker để tránh quy đổi lặp. Không xóa các bảng này trong quá trình xác minh.
7. Publish code web/API cùng thời điểm. Bản code mới yêu cầu marker 033; nếu chưa chạy migration, thảo luận tạm báo chưa sẵn sàng thay vì diễn giải nhầm giờ cũ là UTC. Không chạy code cũ trên database đã chuẩn hóa: nó sẽ diễn giải UTC như giờ local.
8. Kiểm tra vài bản ghi: `ngayTao` mới có giá trị gần `GETUTCDATE()`; `createdAt` JSON kết thúc bằng `Z`; bình luận vừa gửi hiện “vừa xong” ở cả web và app. Kiểm tra bản cũ, phản hồi, lượt thích, thứ tự, ngày thống kê, và hai múi giờ thiết bị.

**Khôi phục:** các bảng backup chỉ giữ những dòng tồn tại tại lúc migration chạy. Không ghi đè/khôi phục thủ công khi đã có bình luận mới; hãy dùng bản sao lưu database và kế hoạch khôi phục của quản trị viên, hoặc đối chiếu cẩn thận từng dòng. Giữ ứng dụng ngừng ghi trong quá trình khôi phục và khôi phục phiên bản code tương ứng.
