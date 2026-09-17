# Cấu Trúc Hệ Thống Dự Án ECOLearning

Dự án **ECOLearning** được xây dựng trên nền tảng **ASP.NET MVC 5 (.NET Framework 4.8)** kết hợp với **Entity Framework (Database First)** và cơ sở dữ liệu **SQL Server**.

---

## 📁 1. Cấu Trúc Thư Mục Chính

```
ECOLearning/
├── App_Start/          # Cấu hình khởi tạo ứng dụng (Route, Bundle, Filter)
├── Areas/              # Các phân vùng quản trị độc lập (Admin, Instructor, Marketing)
├── Controllers/        # Xử lý logic điều hướng và nghiệp vụ ứng dụng
├── Models/             # Entity Framework EDMX & Entities đại diện bảng CSDL
├── ViewModels/         # Data Models thiết kế riêng cho từng giao diện View
├── Views/              # Giao diện hiển thị Razor (.cshtml)
├── Services/           # Các dịch vụ xử lý ngoài (Gửi Email OTP, Thanh toán,...)
├── assets/             # Tài nguyên giao diện tĩnh tùy chỉnh (CSS, JS, Images)
├── Scripts/ & Content/ # Thư viện Frontend chuẩn (jQuery, Bootstrap, CSS gốc)
├── CauTrucHeThong.md   # Tệp tài liệu mô tả cấu trúc và chức năng hệ thống
├── Global.asax (.cs)   # Quản lý vòng đời ứng dụng ASP.NET
└── Web.config          # File cấu hình ứng dụng (ConnectionString, AppSettings,...)
```

---

## ⚙️ 2. Chi Tiết Chức Năng Từng Thư Mục & File

### 🔴 `App_Start/` — Cấu Hình Khởi Tạo
- **`RouteConfig.cs`**: Định tuyến URL (URL Routing) đến Controller và Action tương ứng (VD: `/Home/Index`).
- **`BundleConfig.cs`**: Nén và tối ưu hóa các tệp CSS/JS.
- **`FilterConfig.cs`**: Đăng ký các bộ lọc toàn cục (Global Filters).

---

### 🟢 `Controllers/` — Điều Hướng & Xử Lý Logic
- **`HomeController.cs`**: Trang chủ, hiển thị danh sách khóa học nổi bật, tìm kiếm, lọc danh mục và các trang tĩnh (Giới thiệu, Liên hệ).
- **`AccountController.cs`**: Đăng ký, đăng nhập, quên mật khẩu, xác thực OTP qua Email, đổi mật khẩu.
- **`CourseController.cs`**: Trang danh sách khóa học, tìm kiếm/lọc khóa học và trang chi tiết khóa học.
- **`CartController.cs`**: Quản lý giỏ hàng (thêm, xóa, cập nhật), áp dụng mã giảm giá (coupon) và thủ tục thanh toán.
- **`LearningController.cs`**: Giao diện vào học (học thử / xem bài giảng video, bài học thử nghiệm).
- **`ProfileController.cs`**: Hồ sơ cá nhân học viên, cập nhật thông tin, xem các khóa học đã đăng ký.
- **`PaymentController.cs`**: Xử lý cổng thanh toán trực tuyến.

---

### 🟡 `Areas/` — Phân Vùng Quản Trị Hệ Thống
Chia dự án thành các phân vùng độc lập theo vai trò:
- **`Areas/Admin/`**: Trang quản trị tổng thể dành cho Quản trị viên (quản lý người dùng, duyệt khóa học, báo cáo doanh thu).
- **`Areas/Instructor/`**: Cổng dành cho Giáo viên (tạo/sửa khóa học, bài học, quản lý học viên, doanh thu cá nhân).
- **`Areas/Marketing/`**: Quản lý chương trình khuyến mãi, mã giảm giá, các chiến dịch Marketing.

---

### 🔵 `Models/` — Cơ Sở Dữ Liệu (Entity Framework)
- **`ECOLearning_Models.edmx`**: Sơ đồ kết nối CSDL SQL Server với mã nguồn C#.
- **`KhoaHoc.cs`, `NguoiDung.cs`, `DonHang.cs`, `BaiHoc.cs`, `DanhMuc.cs`,...**: Các lớp đại diện cho các bảng trong CSDL.
- **`sp_*_Result.cs`**: Các model chứa kết quả trả về từ Stored Procedures trong SQL Server (báo cáo, thống kê, xử lý nhanh).

---

### 🟣 `ViewModels/` — Dữ Liệu Truyền Vào Giao Diện View
- **`CartViewModel.cs` & `CartItemViewModel.cs`**: Chứa thông tin giỏ hàng, tạm tính, giảm giá voucher, tổng tiền.
- **`LoginViewModel.cs` & `RegisterViewModels.cs`**: Chứa dữ liệu form đăng nhập, đăng ký và xác thực mã OTP.
- **`ForgotPasswordViewModels.cs`**: Chứa thông tin yêu cầu đặt lại mật khẩu.
- **`StudentProfileViewModel.cs`**: Chứa dữ liệu thông tin cá nhân và các khóa học đã mua của học viên.

---

### 🟧 `Views/` — Giao Diện Người Dùng (Razor .cshtml)
- **`Views/Shared/`**:
  - `_Layout.cshtml`: Layout chung (Header, Footer, Navigation, Modal Đăng xuất, Toast notification).
  - `_ViewStart.cshtml`: Đặt Layout mặc định cho tất cả các View.
- **`Views/Home/`**: `Index.cshtml` (Trang chủ hệ thống), `About.cshtml`, `Contact.cshtml`.
- **`Views/Cart/`**: `Index.cshtml` (Giao diện giỏ hàng và thủ tục thanh toán).
- **`Views/Course/`**: `Index.cshtml` (Danh sách khóa học), `Details.cshtml` (Chi tiết khóa học).
- **`Views/Account/`**: `Login.cshtml`, `Register.cshtml`, `ForgotPassword.cshtml`, `ResetPassword.cshtml`.
- **`Views/Profile/`**: `Index.cshtml` (Trang cá nhân học viên).
- **`Views/Learning/`**: `Study.cshtml` (Giao diện xem video và tài liệu bài học).

---

### 🔷 `Services/` — Dịch Vụ Hệ Thống
- **`EmailService.cs`**: Xử lý gửi Email tự động (gửi mã OTP đăng ký/quên mật khẩu, gửi thông báo hệ thống qua SMTP).

---

### 🎨 `assets/` & `Scripts/` — Tài Nguyên Giao Diện
- **`assets/js/app.js`**: JavaScript xử lý tương tác UI/UX chung (Modal, Toast notification, Rating star, Cart warning toast).
- **`assets/css/`**: Các tệp CSS tùy chỉnh giao diện chuẩn thiết kế hiện đại.
- **`Scripts/jquery-3.7.0.min.js`**: Thư viện jQuery xử lý AJAX và tương tác DOM.

---

## 🛠 3. Các Tệp Cấu Hình Quan Trọng

- **`Web.config`**: Lưu trữ chuỗi kết nối CSDL SQL Server (`ConnectionString`), cấu hình tài khoản Email gửi OTP (`SMTP`), cài đặt ASP.NET MVC.
- **`Global.asax.cs`**: Nơi khởi chạy ứng dụng, đăng ký Routes và Bundles khi khởi động server.
