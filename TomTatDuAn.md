# 📋 Tóm Tắt Dự Án ECOLearning & Nhật Ký Phát Triển

> **Dự án:** ECOLearning - Nền tảng Đào tạo Trực tuyến (E-Learning Platform)  
> **Cấp độ:** Khóa luận tốt nghiệp (KLCN2026 - HUIT)  
> **Cập nhật ngày:** 02/10/2026  
> **Trạng thái:** Biên dịch thành công, chạy trên IIS Express (`https://localhost:44350/`). Đã gộp nhánh `phu` (giao diện Giáo viên của Ngọc) vào nhánh `thu` (module Admin).

---

## 🏛 1. Kiến Trúc & Công Nghệ Sử Dụng

- **Backend:** ASP.NET MVC 5, .NET Framework 4.8, C#
- **ORM / Truy xuất dữ liệu:** Entity Framework 6 (Database First, file `.edmx`). Các bảng mới ngoài `.edmx` (`DangKyGiaoVien`, `BaoCaoViPham`, `XuLyViPham`, `PhanHoiViPham`) được đọc/ghi bằng SQL thuần qua `db.Database`.
- **Cơ sở dữ liệu:** Microsoft SQL Server (`QL_HETHONG_CUNGCAP_KHOAHOC`), nhiều trigger nghiệp vụ (tự đăng ký khóa học khi thanh toán, tự tính tổng tiền đơn, tự tạo tiến độ...).
- **Frontend & Giao diện:** Razor View Engine (`.cshtml`), Bootstrap 5, Bootstrap Icons, SweetAlert2, biểu đồ SVG tự viết (`assets/js/charts.js`).
- **Xuất file:** Excel (CSV UTF-8), PDF thật bằng **PDFsharp-MigraDoc-GDI 1.50** (NuGet, giấy phép MIT).
- **Web Server:** IIS Express (HTTPS cổng 44350).
- **Phân quyền:** tự viết bằng Session (`Session["UserId"]`, `Session["Role"]`); mỗi Area có BaseController kiểm tra vai trò.

### Cấu trúc theo vai trò
| Khu vực | Thư mục | Vai trò |
| :--- | :--- | :--- |
| Học viên / trang công khai | `Controllers/`, `Views/` | `HocVien` (giáo viên cũng dùng được để mua & học khóa của người khác) |
| Quản trị | `Areas/Admin` | `Admin` |
| Giáo viên | `Areas/Instructor` | `GiaoVien` |
| Marketing | `Areas/Marketing` | `Marketing` |

---

## 🗄 2. Các Bảng Cơ Sở Dữ Liệu Trọng Yếu

1. **`NguoiDung`**: Tài khoản, thông tin cá nhân, vai trò (`HocVien`, `GiaoVien`, `Admin`, `Marketing`), `trangThai` (khóa/mở tài khoản).
2. **`DangKyGiaoVien`**: Hồ sơ đăng ký làm giáo viên (chuyên môn, kinh nghiệm, chứng chỉ, tài khoản ngân hàng, trạng thái `Pending/Approved/Rejected`).
3. **`DanhMuc`**: Danh mục khóa học; `trangThai` = tạm ngưng / đang hoạt động.
4. **`KhoaHoc`**: Thông tin khóa học, giá, giảng viên, `trangThai` (xem mục 2.1).
5. **`ChuongHoc` & `BaiHoc` & `TaiNguyen`**: Đề cương, video, tài liệu.
6. **`KiemDuyetKhoaHoc`**: Lịch sử kiểm duyệt khóa học (chờ duyệt / đã duyệt / từ chối + lý do).
7. **`DonHang` / `ChiTietDonHang` / `ThanhToan`**: Đơn hàng; doanh thu tính từ đơn `Đã thanh toán`.
8. **`DangKyKhoaHoc`, `TienDoHocTap`, `TienDoBaiHoc`**: Ghi danh và tiến độ học.
9. **`DanhGiaKhoaHoc`**: Đánh giá 1–5 sao (trigger yêu cầu học xong ≥ 25% khóa học).
10. **`NhanThanhToan`**: Yêu cầu rút tiền của giáo viên (`Chờ duyệt / Đã thanh toán / Từ chối`).
11. **`BaoCaoViPham`** *(mới)*: Học viên báo cáo vi phạm khóa học (loại vi phạm, mô tả, bài học bị báo cáo, trạng thái `Chờ xử lý / Đã xử lý / Đã bác bỏ`).
12. **`XuLyViPham`** *(mới)*: Lịch sử Admin xử lý (`Bác bỏ / Tạm khóa / Mở lại / Khóa vĩnh viễn`), hạn phản hồi, bước xử lý (`Chờ GV xử lý / Chờ Admin xem xét / Đã kết thúc`).
13. **`PhanHoiViPham`** *(mới)*: Lịch sử trao đổi giáo viên ↔ Admin trong một lần tạm khóa (`Đã chỉnh sửa / Khiếu nại / Yêu cầu sửa thêm`).

### 2.1. Trạng thái khóa học (`KhoaHoc.trangThai`)
Hằng số dùng chung trong `Services/CourseStatus.cs`:
`Bản nháp` → `Chờ duyệt` → `Đã xuất bản` (dữ liệu cũ còn `Đã duyệt`, được coi như đã xuất bản) / `Cần sửa đổi`;
do vi phạm: `Tạm khóa`, `Khóa vĩnh viễn`. Khóa học bị khóa ẩn khỏi trang bán, không mua / đăng ký được.

---

## 🚀 3. Chi Tiết Các Tính Năng Đã Triển Khai & Hoàn Thiện

### 3.1. Phân hệ Học tập & Video Player (`Views/Learning/Study.cshtml`)
- **Trình phát video thông minh:**
  - Hỗ trợ xem video MP4 lưu trữ nội bộ trên server (`assets/uploads/videos/`) và video YouTube.
  - Tương tác video thuận tiện: Nhấp trực tiếp vào khung video để Play/Pause, phím tua lùi 10s, tùy chọn tốc độ (0.75x - 2.0x), xem toàn màn hình.
  - Seek bar và mốc thời gian hiển thị chính xác (`00:00 / MM:SS`).
- **Quy tắc hoàn thành bài học $\ge 80\%$:**
  - Khi học viên xem đạt $\ge 80\%$ thời lượng bài giảng (hoặc tua đến $\ge 80\%$):
    - Đổi ngay icon bài học trên cây đề cương (Sidebar) thành **tích xanh `✓`** (`check-icon completed`).
    - Hiển thị nhãn **`Xong`** màu xanh lá.
    - Cập nhật thanh tiến độ toàn khóa học trên Header (`X% (Y/Z bài)`).
    - Tự động gọi API `POST /Learning/UpdateProgress` lưu trạng thái hoàn thành vào bảng **`TienDoBaiHoc`** trong SQL Server.
- **Xem qua một lần & Tự động chuyển bài tiếp theo:**
  - Khi video kết thúc (`ended`), hệ thống ghi nhận đã xem qua bài học, hiển thị thông báo đếm ngược và tự động chuyển sang bài tiếp theo sau 1.5 giây.
  - Khi sang bài tiếp theo, bài học trước vẫn giữ nguyên tích xanh `✓` được nạp trực tiếp từ CSDL.
  - Khi hoàn thành bài học cuối cùng: Hệ thống chúc mừng và tự động kích hoạt Modal Đánh giá khóa học (`rating-modal`).
- **Hỗ trợ kiểm thử linh hoạt:**
  - Nút **`🔄 Đặt lại`** trên đầu danh sách bài học: Cho phép xóa sạch tiến độ trong CSDL (`/Learning/ResetProgress`) để kiểm thử lại từ đầu bất cứ lúc nào.
- **Báo cáo vi phạm** *(mới)*: nút **"Báo cáo vi phạm"** trên thanh tiêu đề (chỉ học viên đã đăng ký, không phải giáo viên của khóa), tự gắn bài học đang xem.
- Khóa học **Khóa vĩnh viễn**: học viên không vào học được nữa (giáo viên chủ khóa và Admin vẫn xem được).

### 3.2. Phân hệ Chi tiết Khóa học (`Views/Course/Details.cshtml`)
- **Chuẩn hóa thông số đánh giá thực tế:**
  - Điểm đánh giá sao và số lượt đánh giá được tính chuẩn xác từ CSDL bảng `DanhGiaKhoaHoc`.
  - Khóa học chưa có đánh giá: Trả về chuẩn xác `0.0 sao` và `0 lượt đánh giá` (không hiển thị dữ liệu giả định).
  - Phân bổ số sao (5★ đến 1★) và danh sách bình luận thực tế kèm ảnh đại diện, tên học viên, ngày đánh giá.
- **Cây đề cương bài giảng & Học thử (Preview):**
  - Hiển thị danh sách chương và bài học, tự động tính tổng thời lượng bài giảng.
  - Cho phép học thử video trực tiếp qua Modal đối với các bài học được bật quyền `choXemThu`.
  - Liên kết trực tiếp thông tin giảng viên phụ trách sang trang hồ sơ giảng viên.
- *(mới)* Giáo viên xem khóa học của chính mình thấy nút **"Quản lý khóa học của bạn"** thay cho nút Mua; học viên đã đăng ký có link **"Báo cáo vi phạm khóa học"**.

### 3.3. Phân hệ Hồ sơ Giảng viên (`Views/User/Index.cshtml`)
- **Trang thông tin giảng viên công khai:**
  - Nạp dữ liệu từ bảng `DangKyGiaoVien` kết hợp `NguoiDung`.
  - Hiển thị tiểu sử, chuyên môn, kinh nghiệm, thông tin liên hệ.
  - **Thống kê tự động chuẩn CSDL:**
    - Tổng số khóa học do giảng viên phụ trách.
    - Tổng số học viên đã đăng ký vào các khóa học của giảng viên.
    - Đánh giá chất lượng trung bình tính trên toàn bộ các lượt đánh giá từ học viên.
  - Danh sách tất cả các khóa học do giảng viên này giảng dạy kèm phân trang.

### 3.4. Phân hệ Khóa học của tôi (`Views/Learning/MyCourses.cshtml`)
- Phân loại khóa học đang học và khóa học đã hoàn tất (100%).
- Tính toán tỷ lệ % hoàn thành dựa trên số bài học đã hoàn thành trong bảng `TienDoBaiHoc`.
- Nút điều hướng thông minh: Tự động dẫn vào bài học chưa hoàn thành gần nhất.
- Hỗ trợ cấp chứng chỉ hoàn thành khóa học dạng PDF (`/Learning/Certificate`).

### 3.5. Mua khóa học & vai trò Giáo viên kiêm Học viên *(mới)*
- Luồng tài khoản: đăng ký là **Học viên** → nộp hồ sơ giáo viên kèm chứng chỉ → Admin duyệt → thành **Giáo viên**; giáo viên vẫn quay lại trang học viên để mua & học khóa của giáo viên khác.
- Chặn giáo viên **tự mua / tự đăng ký** khóa học của chính mình (giỏ hàng, thanh toán, đăng ký miễn phí, tự ghi danh khi xem thử).
- Khóa học không còn mở bán (tạm khóa / khóa vĩnh viễn / chưa xuất bản) không thêm giỏ, thanh toán hay đăng ký được.

### 3.6. Module Admin (`Areas/Admin`) *(mới)*
- **Dashboard:** thẻ **"Báo cáo vi phạm chưa xử lý"**; bảng khóa học chờ duyệt có phân trang.
- **Quản lý danh mục:** thêm / sửa / **tạm ngưng** / **kích hoạt lại**; tên duy nhất (kiểm tra ở server, không phân biệt hoa thường); chỉ xóa được danh mục chưa có khóa học; lọc theo trạng thái, tìm kiếm, phân trang server 10 dòng. Danh mục tạm ngưng bị ẩn khỏi bộ lọc học viên và dropdown tạo khóa học của giáo viên, khóa học sẵn có vẫn bán bình thường.
- **Kiểm duyệt khóa học:** giữ nguyên luồng duyệt / từ chối; thêm lọc **danh mục**, **khoảng ngày gửi**, **sắp xếp ngày gửi** (mới/cũ), phân trang.
- **Duyệt hồ sơ giáo viên:** giữ nguyên luồng duyệt; thêm phân trang, căn chỉnh bảng.
- **Báo cáo vi phạm** (`ViolationReportController`): danh sách gom theo khóa học (tab Chờ xử lý / GV đã phản hồi / Đang tạm khóa / Khóa vĩnh viễn / Tất cả), sắp xếp (ưu tiên, mới nhất, cũ nhất, hạn phản hồi gần nhất). Trang chi tiết: **Bác bỏ**, **Tạm khóa** (lý do + 1–30 ngày phản hồi), **Yêu cầu sửa thêm** (gia hạn), **Mở lại** (khôi phục trạng thái trước khi khóa), **Khóa vĩnh viễn** (không xóa dữ liệu); xem báo cáo, lịch sử xử lý, trao đổi với giáo viên.
- **Báo cáo hệ thống:**
  - Thống kê **số giáo viên** và **khóa học theo trạng thái**; bảng **Doanh thu theo giáo viên**.
  - Biểu đồ **2 trục** Doanh thu (triệu VNĐ) & Đơn hoàn tất (`Charts.renderDualLineChart`).
  - Bộ lọc **"Phân tích dữ liệu"** theo danh mục / giáo viên áp dụng cho toàn bộ số liệu, biểu đồ và file xuất.
  - Xuất **Excel** và **PDF thật** (`Services/SystemReportPdfService.cs`), cùng kỳ báo cáo & bộ lọc với màn hình.

### 3.7. Giáo viên xử lý vi phạm (`Areas/Instructor`) *(mới)*
- Banner trên Dashboard khi có khóa học bị tạm khóa chờ xử lý; tab **"Vi phạm"** và nhãn trạng thái trong Quản lý khóa học.
- Trang **Xử lý vi phạm** (`ViolationController`): đếm ngược hạn chót, lý do khóa, các báo cáo (ẩn danh người báo cáo, có bài học bị báo cáo), chọn **"Tôi đã chỉnh sửa"** hoặc **"Khiếu nại"**, lịch sử trao đổi.
- Khóa học **Tạm khóa**: được sửa nội dung, nút gửi trong trang sửa thành **"Lưu & gửi phản hồi vi phạm"** (lưu trước rồi chuyển sang trang Xử lý vi phạm); không tự gửi duyệt / xóa được. **Khóa vĩnh viễn**: không sửa / xóa.
- Dropdown danh mục khi tạo / sửa khóa học ẩn danh mục tạm ngưng.
- **Quản lý học viên:** sắp xếp bằng nút trên tiêu đề cột (ngày tham gia, tiến độ, đánh giá; tăng/giảm).

### 3.8. Giao diện chung Admin & Giáo viên *(mới)*
- `assets/css/dashboard-tables.css`: nội dung ô bảng (tên, ngày, nhãn, nút) không rớt dòng, cột chữ dài vẫn xuống dòng gọn, tiêu đề cột được xuống dòng để hạn chế cuộn ngang.
- `assets/js/table-pagination.js`: phân trang **10 dòng/trang** cho bảng có thuộc tính `data-paginate="10"`, tự phối hợp với bộ lọc / tìm kiếm sẵn có của trang.

---

## 🛠 4. Danh Sách Tệp Mã Nguồn Chính Đã Được Nâng Cấp

### 4.1. Đợt trước (học viên)
| Đường Dẫn Tệp | Mô Tả Thay Đổi |
| :--- | :--- |
| **`Controllers/LearningController.cs`** | Bổ sung hàm `GetCurrentStudent()`, cập nhật logic `UpdateProgress` ($\ge 80\%$, lưu `TienDoBaiHoc`), thêm action `ResetProgress`. |
| **`Views/Learning/Study.cshtml`** | Thêm trình xử lý `timeupdate`, `ended`, tự động chuyển bài `nextLessonUrl`, hiển thị tích xanh `✓`, xử lý Play/Pause tiện lợi. |
| **`Controllers/CourseController.cs`** | Tính toán số sao trung bình thực tế và tổng lượt đánh giá từ bảng `DanhGiaKhoaHoc`. |
| **`Views/Course/Details.cshtml`** | Chuẩn hóa UI Hero Section và Section 4 (Đánh giá từ học viên) về `0.0 sao / 0 lượt đánh giá` khi chưa có dữ liệu thực tế. |
| **`Controllers/UserController.cs`** | Nạp dữ liệu từ `DangKyGiaoVien`, tính toán các chỉ số thống kê tổng học viên và điểm đánh giá trung bình. |
| **`Views/User/Index.cshtml`** | Thiết kế giao diện trang hồ sơ giảng viên chuẩn UI/UX, hiển thị thống kê và danh sách khóa học. |

### 4.2. Đợt này (Admin, vi phạm, báo cáo)
| Đường Dẫn Tệp | Mô Tả Thay Đổi |
| :--- | :--- |
| `Services/CourseStatus.cs` | Hằng số & gom nhóm trạng thái khóa học dùng chung. |
| `Services/ViolationReportService.cs`, `Models/ViolationReportModels.cs` | Nghiệp vụ báo cáo / xử lý / trao đổi vi phạm (SQL thuần). |
| `Services/SystemReportPdfService.cs` | Tạo file PDF báo cáo hệ thống (PDFsharp-MigraDoc). |
| `Areas/Admin/Controllers/CategoryController.cs` + view | Quản lý danh mục đầy đủ. |
| `Areas/Admin/Controllers/SystemReportController.cs` + view | Thống kê GV / trạng thái, bộ lọc phân tích, doanh thu theo GV, biểu đồ 2 trục, Excel/PDF. |
| `Areas/Admin/Controllers/ViolationReportController.cs` + views `ViolationReport/*` | Báo cáo vi phạm phía Admin. |
| `Areas/Admin/Controllers/CourseApprovalController.cs` + view | Lọc danh mục / ngày, sắp xếp, phân trang. |
| `Areas/Admin/Controllers/DashboardController.cs`, `_LayoutAdmin.cshtml` | Thẻ vi phạm, menu "Báo cáo vi phạm", nạp CSS/JS dùng chung. |
| `Areas/Instructor/Controllers/ViolationController.cs` + view `Violation/Index` | Trang giáo viên xử lý vi phạm. |
| `Areas/Instructor/Controllers/CourseController.cs`, `DashboardController.cs`, `StudentController.cs` + views | Chặn thao tác khi bị khóa, dropdown danh mục, banner, sắp xếp học viên (chỉ thêm đoạn nhỏ vào code của Ngọc). |
| `Controllers/ViolationReportController.cs` | Học viên gửi báo cáo vi phạm. |
| `Controllers/CartController.cs`, `CourseController.cs`, `LearningController.cs` | Chặn tự mua khóa của mình, chặn mua khóa bị khóa, nút báo cáo trong trang học. |
| `assets/js/charts.js` | Thêm `renderDualLineChart` (giữ nguyên các hàm cũ). |
| `assets/css/dashboard-tables.css`, `assets/js/table-pagination.js` | Căn chỉnh bảng & phân trang dùng chung. |
| `packages.config`, `ECOLearning.csproj` | Thêm PDFsharp-MigraDoc-GDI và các file mới. |

---

## 🗃 5. Script Cơ Sở Dữ Liệu & Dữ Liệu Demo

> 👉 Hướng dẫn từng bước cho các bạn sau khi pull code: **[Database/HUONG_DAN_CHAY_SQL.md](Database/HUONG_DAN_CHAY_SQL.md)**

### 5.1. Migrations (chạy lần lượt trong SSMS, chỉ bổ sung, không xóa dữ liệu)
| File | Nội dung |
| :--- | :--- |
| `Database/Migrations/001_KhuyenNghi_ChoPhepGiaoVien.sql` | Trigger gợi ý khóa học chấp nhận cả `GiaoVien` (giáo viên cũng là người học). |
| `Database/Migrations/002_BaoCaoViPham.sql` | Tạo bảng `BaoCaoViPham`, `XuLyViPham`. |
| `Database/Migrations/003_BaoCaoViPham_BaiHoc.sql` | Thêm cột `BaoCaoViPham.maBaiHoc`. |
| `Database/Migrations/004_XuLyViPham_TraoDoi.sql` | Thêm `XuLyViPham.trangThaiXuLy`, bảng `PhanHoiViPham`. |

Chưa chạy 002–003 thì trang Báo cáo vi phạm hiện hướng dẫn chạy script; chưa chạy 004 thì phần trao đổi giáo viên ↔ Admin được ẩn, các chức năng khác vẫn chạy.

### 5.2. Dữ liệu demo
- Tạo: `Database/Seed/01_du_lieu_demo.sql` — Xóa sạch: `Database/Seed/99_xoa_du_lieu_demo.sql` (chỉ xóa dữ liệu demo).
- Gồm 15 người dùng, 3 danh mục mới (DM06 tạm ngưng), 13 khóa học đủ mọi trạng thái, 57 bài học, 34 đơn đã thanh toán (~22 triệu, rải từ 11/2025 đến tháng hiện tại), tiến độ, đánh giá, hồ sơ giáo viên (duyệt / chờ / từ chối), báo cáo vi phạm đủ các tình huống, yêu cầu rút tiền.
- Tài khoản demo (mật khẩu chung **`Demo@123`**, đuôi `@demo.ecolearning.vn`):
  - Giáo viên: `khoa.tran`, `ha.le`, `bao.pham`
  - Học viên: `an.nguyen`, `binh.tran`, `cuong.le`, `dung.pham`, `duc.vo`, `han.dang`, `huy.bui`, `linh.ho`, `nam.ngo`, `phuong.duong` (hồ sơ GV chờ duyệt), `tam.ly` (hồ sơ GV bị từ chối), `vy.mai` (tài khoản bị khóa)
- Tài khoản có sẵn: Admin `admin@elearning.vn` / `Admin123456`.

### 5.3. Lưu ý khi pull code
- Thư mục `packages/` không được commit: sau khi pull, chuột phải Solution → **Restore NuGet Packages** (cần cho PDFsharp).
- `Web.config` giữ connection string riêng của từng máy (`data source=.` hoặc tên instance), không nên commit đè.

---

## 🧪 6. Hướng Dẫn Kiểm Thử

### 6.1. Hoàn thành bài học $\ge 80\%$
1. Chạy dự án trên IIS Express và truy cập:
   `https://localhost:44350/Learning/Study?courseId=KH260929150333&lessonId=KH260929150333_1`
2. Bấm phát video (Bài 1 có thời lượng 6 giây):
   - Đến giây thứ 4.8 ($\ge 80\%$): Bài 1 bên Sidebar lập tức xuất hiện **tích xanh `✓`** và nhãn **`Xong`**, đồng thời thanh tiến độ trên Header cập nhật.
   - Đến giây thứ 6.0 (kết thúc video): Hệ thống thông báo đếm ngược 1.5 giây và tự động chuyển sang **Bài 2: Cấu trúc lệnh cơ bản**.
   - Tại trang Bài 2: Bài 1 vẫn giữ nguyên tích xanh `✓` được nạp trực tiếp từ CSDL `TienDoBaiHoc`.
3. Nếu muốn xóa tiến độ để kiểm thử lại từ đầu: Bấm nút **`🔄 Đặt lại`** ở góc trên danh sách bài học bên phải.

### 6.2. Luồng báo cáo vi phạm (dùng dữ liệu demo)
1. Học viên `an.nguyen` → Khóa học của tôi → mở một khóa → **Báo cáo vi phạm**.
2. Admin → **Báo cáo vi phạm** → mở khóa học có báo cáo chờ xử lý → **Tạm khóa** (nhập lý do, số ngày).
3. Giáo viên của khóa → Dashboard → **Xử lý ngay** → sửa nội dung → **Lưu & gửi phản hồi vi phạm** → mô tả → **Gửi xem xét lại**.
4. Admin → tab **"GV đã phản hồi"** → **Mở lại**, **Yêu cầu sửa thêm** hoặc **Khóa vĩnh viễn**.
- Có sẵn tình huống: `DMKH13` (tạm khóa, quá hạn, chưa phản hồi — GV `ha.le`), `DMKH09` (GV đã phản hồi — GV `bao.pham`), `DMKH11` (khóa vĩnh viễn).

### 6.3. Báo cáo hệ thống
- Đổi kỳ Tháng / Quý / Năm, lọc theo danh mục / giáo viên → số liệu, biểu đồ 2 đường, bảng doanh thu theo GV thay đổi; **Xuất Excel / Xuất PDF** cho ra file đúng phạm vi đang lọc.

---

## ⚠️ 7. Việc Còn Tồn Đọng / Cần Lưu Ý

- **Bảo mật `LearningController`** (chưa sửa): khi mất Session, `GetCurrentStudent()` lấy đại học viên khác (có thể "đăng nhập nhầm" tài khoản người khác); trang học không kiểm tra đã mua khóa học; `UpdateProgress` tự ghi danh người đang xem.
- **Quản lý người dùng** (khóa/mở tài khoản, phân quyền) của Admin chưa xây dựng.
- **Dashboard Admin:** chỉ số `+22.8%` vẫn là số cố định; thẻ "Khóa học hoạt động" chưa đếm trạng thái `Đã duyệt`; điều kiện lọc doanh thu khác trang Báo cáo hệ thống (có thêm `Đã duyệt`).
- Admin không đọc lại CSDL mỗi request nên tài khoản bị khóa giữa phiên vẫn dùng tiếp đến khi hết Session; `Controllers/AdminController.cs` dùng `[Authorize(Roles = "Admin")]` không khớp cơ chế Session.
- Trigger gợi ý khóa học chỉ nhận trạng thái `Đã xuất bản` trong khi dữ liệu cũ dùng `Đã duyệt`; chức năng gợi ý khóa học chưa có ở phía ứng dụng.
- Trang **Đề cương** (Curriculum) của giáo viên chưa chặn sửa khi khóa học bị khóa vĩnh viễn.
- Mật khẩu các tài khoản Admin / Marketing / demo đang lưu dạng chữ thường (hệ thống vẫn chấp nhận để tương thích); mật khẩu ứng dụng Gmail nằm trong `Web.config`.
- "Lượt ghi danh" ở Báo cáo thống kê của giáo viên là **số đăng ký mới trong kỳ đang chọn**, không phải tổng học viên.
