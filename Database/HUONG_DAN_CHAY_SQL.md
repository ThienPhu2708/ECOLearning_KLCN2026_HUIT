# Hướng dẫn chạy các file SQL sau khi pull code

Sau khi pull nhánh có module Admin / Báo cáo vi phạm, mỗi bạn cần chạy thêm vài file `.sql` lên database
**trên máy mình** (`QL_HETHONG_CUNGCAP_KHOAHOC`). Nếu không chạy, trang **Báo cáo vi phạm** sẽ báo "Chưa khởi tạo dữ liệu"
và học viên không gửi được báo cáo.

Các file chỉ **thêm** bảng / cột / trigger, **không xóa dữ liệu** và **chạy lại nhiều lần vẫn an toàn**.

---

## Bước 1 — Bắt buộc: chạy các file migration theo đúng thứ tự
Thư mục: `Database/Migrations/`

| Thứ tự | File | Làm gì |
| :---: | :--- | :--- |
| 1 | `001_KhuyenNghi_ChoPhepGiaoVien.sql` | Sửa trigger gợi ý khóa học: giáo viên cũng được nhận gợi ý (giáo viên vẫn mua & học khóa của người khác). |
| 2 | `002_BaoCaoViPham.sql` | Tạo bảng `BaoCaoViPham` (học viên báo cáo) và `XuLyViPham` (Admin xử lý). |
| 3 | `003_BaoCaoViPham_BaiHoc.sql` | Thêm cột `maBaiHoc` để biết bài học nào bị báo cáo. |
| 4 | `004_XuLyViPham_TraoDoi.sql` | Thêm bước xử lý và bảng `PhanHoiViPham` (giáo viên ↔ Admin trao đổi khi khóa học bị tạm khóa). |
| 5 | `005_GoiY_LocCongTac.sql` | Gợi ý khóa học (lọc cộng tác): bảng `TuongTacKhoaHoc`, `course_similarity`, `GoiY_TrongSo`, view, hàm và 3 stored procedure (khóa tương tự, gợi ý cho bạn, nút "Gợi ý khóa học"). Sau khi chạy, xem `Recommender/README.md` để chạy job tính gợi ý. |
| 6 | `006_GoiY_GoiYLaiKhoaChiXem.sql` | Sửa quy tắc gợi ý: chỉ không gợi ý lại khóa **đã đăng ký / đã học / đã đánh giá**; khóa chỉ mới xem qua vẫn được gợi ý. Thiếu file này thì người xem nhiều khóa (chưa mua) sẽ thấy trang "Khóa học gợi ý" trống. |
| 7a | `007_GoiY_SoThichNguoiDung.sql` | Cold-start: bảng `SoThichNguoiDung` (lĩnh vực học viên mới chọn) và `KhaoSatSoThich` (đã trả lời / bỏ qua). Thiếu file này thì hộp thoại "Bạn quan tâm lĩnh vực nào?" không hiện; trang chủ vẫn chạy, học viên mới chỉ thấy khóa phổ biến. |
| 7b | `007_Quiz_ChungChi.sql` | Quiz cuối khóa và chứng chỉ: bảng `QuizQuestion`, `QuizConfig`, `QuizAttempt`, `QuizCertificate`, `QuizAttemptGrant`. Hai file `007` không phụ thuộc nhau, chạy cả hai. Thiếu file này thì trang Chứng chỉ báo "Chưa khởi tạo chức năng quiz và chứng chỉ" và học viên không gửi được đánh giá khóa học (phải đạt quiz mới được đánh giá). |
| 8 | `008_Marketing_KhuyenMai.sql` | Phân hệ Marketing: bảng `ChienDich` (chiến dịch) và `SuDungUuDai` (lịch sử dùng coupon theo đơn); thêm cột cấu hình cho `KhuyenMai` (loại ưu đãi % / số tiền, giảm tối đa, đơn tối thiểu, phạm vi khóa học, đối tượng thân thiết + tiêu chí, trạng thái, chiến dịch, công khai mã cho học viên) và giới hạn lượt dùng cho `MaGiamGia`; sửa trigger `trg_ChiTietDonHang_CapNhatTongTien` để tổng tiền đơn trừ tiền giảm. Thiếu file này thì các trang Marketing báo "Chưa khởi tạo dữ liệu" và giỏ hàng không áp dụng được coupon. |
| 9 | `009_ViMaGiamGia.sql` | Ví mã giảm giá: bảng `ViMaGiamGia` (mã học viên đã lưu ở trang chủ / trang khóa học, không xóa được) và cột `KhuyenMai.chinhSach` (mô tả chính sách hiển thị cho học viên). Thiếu file này thì trang Marketing và giỏ hàng báo "chưa sẵn sàng", trang "Mã giảm giá của tôi" không mở được. |
| 10 | `010_TimKiem.sql` | Thống kê từ khóa tìm kiếm: bảng `TuKhoaTimKiem` (từ khóa đã tìm, số lượt, số kết quả; không lưu người tìm). Thiếu file này thì tìm kiếm vẫn chạy nhưng gợi ý chỉ lấy từ tên khóa học / danh mục, không có từ khóa phổ biến. |
| 11 | `011_DanhGia_HuuIch_PhanHoi.sql` | Đánh giá khóa học: bảng `DanhGiaHuuIch` ("Bạn thấy hữu ích?") và `PhanHoiDanhGia` (phản hồi của giáo viên cho đánh giá). Thiếu file này thì trang chi tiết vẫn hiện đánh giá nhưng không có nút hữu ích / phản hồi. |
| 12 | `012_DanhMuc_AnhMinhHoa.sql` | Ảnh minh họa danh mục: thêm cột `DanhMuc.anhMinhHoa` (ảnh Admin tải lên ở Quản lý danh mục, hiện ở trang chủ, trang Tất cả lĩnh vực và banner trang danh mục), tạo các danh mục `CAT-002`..`CAT-006` nếu máy chưa có, điền mô tả còn trống và gắn ảnh có sẵn trong `assets/images/categories/` (chỉ điền chỗ trống, không ghi đè). Thiếu file này thì form danh mục không có ô chọn ảnh và các trang hiện biểu tượng như cũ. |
| 13 | `013_BoSung_KhoaHoc_DanhMuc.sql` | Dữ liệu demo: thêm khóa học để **mỗi danh mục có ít nhất 10 khóa đang bán** (mã `K13-...`, mỗi khóa 1 chương 1 bài xem thử, dùng chung video demo có trong repo). Chỉ thêm số khóa còn thiếu theo dữ liệu trên máy bạn. Chạy **sau 012** và sau khi đã nạp dữ liệu demo (cần tài khoản giáo viên). |
| 14 | `014_ThanhToan_ThuNghiem.sql` | Thanh toán và chi trả **thử nghiệm**: thêm cột trạng thái cho `DonHang` (phương thức, hạn thanh toán, ngày thanh toán), `ThanhToan` (cổng, mã giao dịch, ngày thanh toán), `NhanThanhToan` (Admin xử lý, mã giao dịch chi, lý do từ chối) và bảng `NhatKyThanhToan`. Thiếu file này thì giỏ hàng báo "Chức năng thanh toán chưa được khởi tạo" và trang Admin "Chi trả giáo viên" nhắc chạy 014. |
| 15 | `015_Api_Token_Otp.sql` | API cho **ứng dụng di động** (`/api/v1`): bảng `ApiToken` (token đăng nhập của app, chỉ lưu SHA-256, hạn 30 ngày) và `ApiOtp` (mã OTP đăng ký / quên mật khẩu, vì app không dùng Session như website). Thiếu file này thì app không đăng nhập được (`/api/v1/ping` báo `tokenTableReady: false`). |
| 16 | `016_QuanTri_NguoiDung.sql` | Admin quản lý người dùng / giáo viên / giám sát Marketing: thêm cột cho `NguoiDung` (ngày tạo, lý do / thời điểm / hạn khóa, Admin khóa, cờ buộc đổi mật khẩu tạm), bảng `NhatKyQuanTri` (nhật ký thao tác Admin), `KhoaHocTamAn` (khóa học bị ẩn khi giáo viên bị khóa, lưu trạng thái cũ) và cột Admin tạm dừng cho `KhuyenMai`. Thiếu file này thì 3 trang Quản lý người dùng, Quản lý giáo viên, Giám sát Marketing báo "Chưa khởi tạo dữ liệu"; đăng nhập và các trang khác vẫn chạy. |
| 17 | `017_PhienDangNhap_DocQuyen.sql` | Mỗi tài khoản **Admin / Marketing chỉ một phiên đăng nhập**: thêm cột `maPhien`, `phienLuc`, `phienIp`, `phienTrinhDuyet` cho `NguoiDung`. Người đăng nhập sau thay người trước; phiên cũ bị đăng xuất trong khoảng 15 giây, hiện pop-up (thời gian, trình duyệt, IP của lần đăng nhập mới) và ghi "Đăng nhập nơi khác" vào nhật ký quản trị. Chạy **sau 016**. Thiếu file này thì Admin / Marketing vẫn đăng nhập nhiều nơi cùng lúc như trước. |

### Cách chạy trong SQL Server Management Studio (SSMS)
1. Mở SSMS, kết nối vào SQL Server trên máy bạn.
2. **File → Open → File...** → chọn file `001_...sql`.
3. Bấm **Execute** (hoặc phím **F5**). Thấy *"Commands completed successfully"* là xong.
4. Lặp lại với `002`, `003`, `004`, `005`, `006`, cả hai file `007`, `008`, `009`, `010`, `011`, `012`, `013`, `014`, `015`, `016`, `017` **theo đúng thứ tự**.

> Mỗi file đã có sẵn dòng `USE QL_HETHONG_CUNGCAP_KHOAHOC;` nên không cần chọn database trước.
> Nếu chạy bằng dòng lệnh `sqlcmd` thì thêm tham số **`-I`** (bật `QUOTED_IDENTIFIER`), nếu không `007_Quiz_ChungChi.sql`
> báo lỗi `CREATE INDEX failed ... 'QUOTED_IDENTIFIER'`. Sau khi chạy 014, lệnh sửa / xóa bảng `ThanhToan` chạy bằng `sqlcmd` (vd. `Seed/99_xoa_du_lieu_demo.sql`) cũng cần `-I`. SSMS bật sẵn nên không gặp lỗi này.
> Nếu database của bạn đặt **tên khác**, sửa dòng `USE ...` ở đầu file cho đúng tên.

### Kiểm tra đã chạy đủ chưa
Mở một cửa sổ **New Query** và chạy:

```sql
USE QL_HETHONG_CUNGCAP_KHOAHOC;
SELECT
  CASE WHEN OBJECT_ID('dbo.BaoCaoViPham')  IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 002' END AS [002],
  CASE WHEN COL_LENGTH('dbo.BaoCaoViPham','maBaiHoc') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 003' END AS [003],
  CASE WHEN OBJECT_ID('dbo.PhanHoiViPham') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 004' END AS [004],
  CASE WHEN OBJECT_ID('dbo.course_similarity') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 005' END AS [005],
  CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.fn_GoiY_KhoaHocDuocGoiY')) LIKE N'%DangKyKhoaHoc%' THEN N'OK' ELSE N'THIẾU - chạy 006' END AS [006],
  CASE WHEN OBJECT_ID('dbo.SoThichNguoiDung') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 007_GoiY' END AS [007_GoiY],
  CASE WHEN OBJECT_ID('dbo.QuizAttemptGrant') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 007_Quiz' END AS [007_Quiz],
  CASE WHEN OBJECT_ID('dbo.SuDungUuDai') IS NOT NULL AND COL_LENGTH('dbo.KhuyenMai','congKhai') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 008' END AS [008],
  CASE WHEN OBJECT_ID('dbo.ViMaGiamGia') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 009' END AS [009],
  CASE WHEN OBJECT_ID('dbo.TuKhoaTimKiem') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 010' END AS [010],
  CASE WHEN OBJECT_ID('dbo.PhanHoiDanhGia') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 011' END AS [011],
  CASE WHEN COL_LENGTH('dbo.DanhMuc','anhMinhHoa') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 012' END AS [012],
  CASE WHEN EXISTS (SELECT 1 FROM dbo.KhoaHoc WHERE maKhoaHoc LIKE 'K13-%') THEN N'OK' ELSE N'THIẾU - chạy 013' END AS [013],
  CASE WHEN OBJECT_ID('dbo.NhatKyThanhToan') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 014' END AS [014],
  CASE WHEN OBJECT_ID('dbo.ApiToken') IS NOT NULL AND OBJECT_ID('dbo.ApiOtp') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 015' END AS [015],
  CASE WHEN OBJECT_ID('dbo.NhatKyQuanTri') IS NOT NULL AND COL_LENGTH('dbo.KhuyenMai','adminTamDung') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 016' END AS [016],
  CASE WHEN COL_LENGTH('dbo.NguoiDung','maPhien') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 017' END AS [017];
```

Cả 17 cột đều ra **OK** là được.

> **Trên hosting** (CSDL có tên khác, vd. `db74837`): mở file `016_...sql` / `017_...sql`, **xóa dòng `USE ...`** ở đầu, chọn đúng CSDL hosting rồi chạy.

> **Sau khi chạy 005/006 phải chạy job Python ít nhất một lần** (`Recommender/README.md`), nếu không bảng
> `course_similarity` trống và không có gợi ý lọc cộng tác nào. Kiểm tra: `SELECT COUNT(*) FROM course_similarity;` phải lớn hơn 0.

### Thanh toán qua VNPay Sandbox (không bắt buộc)
Mặc định hệ thống dùng **cổng thanh toán thử nghiệm nội bộ**. Muốn thanh toán qua VNPay Sandbox:
1. Đăng ký tài khoản test tại https://sandbox.vnpayment.vn/devreg/ (VNPay gửi `vnp_TmnCode` và `vnp_HashSecret` qua email), hoặc xin bạn đã đăng ký.
2. Sao chép `Secrets.config.example` (thư mục gốc dự án) thành `Secrets.config`, điền 2 giá trị trên, để `Payment:Mode` là `VNPay`.
3. **Không commit** `Secrets.config` (đã có trong `.gitignore`).
4. Thanh toán bằng thẻ test NCB `9704198526191432198`, tên `NGUYEN VAN A`, ngày phát hành `07/15`, OTP `123456`.

---

## Bước 2 — Không bắt buộc: nạp dữ liệu demo để test

Thư mục: `Database/Seed/`

| File | Làm gì |
| :--- | :--- |
| `01_du_lieu_demo.sql` | Tạo dữ liệu demo: 3 giáo viên, 12 học viên, 13 khóa học đủ mọi trạng thái, đơn hàng ~22 triệu rải trong 1 năm, tiến độ học, đánh giá, hồ sơ giáo viên, báo cáo vi phạm đủ các tình huống, yêu cầu rút tiền. |
| `99_xoa_du_lieu_demo.sql` | Xóa **sạch** dữ liệu demo (kể cả dữ liệu phát sinh khi thao tác bằng tài khoản demo). **Không** đụng dữ liệu thật. |

- Chạy `01_du_lieu_demo.sql` **sau khi đã chạy đủ 4 file migration**. Nếu chạy lần 2 sẽ báo "Đã có dữ liệu demo" — muốn tạo lại thì chạy `99_...` trước.
- Tài khoản demo — mật khẩu chung **`Demo@123`**, đuôi email **`@demo.ecolearning.vn`** (có dấu chấm giữa tên và họ):
  - Giáo viên: `khoa.tran`, `ha.le`, `bao.pham`
  - Học viên: `an.nguyen`, `binh.tran`, `cuong.le`, `dung.pham`, `duc.vo`, `han.dang`, `huy.bui`, `linh.ho`, `nam.ngo`,
    `phuong.duong` (hồ sơ GV chờ duyệt), `tam.ly` (hồ sơ GV bị từ chối), `vy.mai` (tài khoản bị khóa)
  - Ví dụ: `khoa.tran@demo.ecolearning.vn` / `Demo@123`

---

## Bước 3 — Mở Visual Studio

1. Chuột phải **Solution** → **Restore NuGet Packages** (bắt buộc lần đầu: dự án vừa thêm thư viện xuất PDF `PDFsharp-MigraDoc-gdi`).
2. **Build → Rebuild Solution**, rồi **Ctrl + F5**.

---

## Gặp lỗi?

| Lỗi | Cách xử lý |
| :--- | :--- |
| `Database 'QL_HETHONG_CUNGCAP_KHOAHOC' does not exist` | Database trên máy bạn tên khác → sửa dòng `USE ...` ở đầu file. |
| `Invalid object name 'BaoCaoViPham'` khi chạy 003/004 | Chưa chạy `002` → chạy lại theo đúng thứ tự 001 → 004. |
| `Cần chạy Database/Migrations 002, 003, 004 trước...` khi chạy dữ liệu demo | Chạy đủ 4 file migration trước. |
| `Đã có dữ liệu demo...` | Đã nạp rồi; muốn nạp lại thì chạy `99_xoa_du_lieu_demo.sql` trước. |
| Build lỗi thiếu `PdfSharp` / `MigraDoc` | Chưa Restore NuGet Packages (Bước 3). |
| Web báo lỗi kết nối CSDL | Sửa `data source=` trong `Web.config` cho đúng SQL Server trên máy bạn (ví dụ `.` hoặc `.\SQLEXPRESS`). **Không commit** thay đổi này. |
