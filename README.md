# 🚀 ECOLearning - Nền Tảng Đào Tạo Trực Tuyến

Hệ thống cung cấp & quản lý khóa học trực tuyến chuẩn UI/UX, được xây dựng trên nền tảng **ASP.NET MVC 5 (.NET Framework 4.8)** kết hợp **Entity Framework (Database First)** và cơ sở dữ liệu **SQL Server**.

---

## 🛠 1. Yêu Cầu Hệ Thống (Prerequisites)
Trước khi khởi chạy dự án, máy tính của bạn cần chuẩn bị:
- **Visual Studio 2022** (hoặc Visual Studio 2019) đã cài đặt Workload **ASP.NET and web development**.
- **.NET Framework 4.8 SDK / Runtime**.
- **Microsoft SQL Server** (2016 trở lên hoặc SQL Server Express) & **SQL Server Management Studio (SSMS)**.

---

## 📥 2. Hướng Dẫn Khởi Chạy Dự Án (Step-by-Step Guide)

### **Bước 1: Clone Repository về máy**
```bash
git clone https://github.com/ThienPhu2708/ECOLearning_KLCN2026_HUIT.git
cd ECOLearning_KLCN2026_HUIT
```

### **Bước 2: Cấu hình Cơ sở dữ liệu SQL Server**
1. Mở **SQL Server Management Studio (SSMS)** và kết nối tới SQL Server của bạn.
2. Thực thi Script CSDL để tạo Database **`QL_HETHONG_CUNGCAP_KHOAHOC`**.
3. Cập nhật chuỗi kết nối trong tệp `Web.config`:
   - Mở tệp `Web.config` tại thư mục gốc dự án.
   - Tìm thẻ `<connectionStrings>` và điều chỉnh thông số `data source` thành tên máy SQL Server của bạn (VD: `localhost`, `.\SQLEXPRESS` hoặc `NAME_COMPUTER`):
     ```xml
     <add name="QL_HETHONG_CUNGCAP_KHOAHOCEntities1" connectionString="metadata=res://*/Models.ECOLearning_Models.csdl|res://*/Models.ECOLearning_Models.ssdl|res://*/Models.ECOLearning_Models.msl;provider=System.Data.SqlClient;provider connection string=&quot;data source=YOUR_SERVER_NAME;initial catalog=QL_HETHONG_CUNGCAP_KHOAHOC;integrated security=True;MultipleActiveResultSets=True;App=EntityFramework&quot;" providerName="System.Data.EntityClient" />
     ```

### **Bước 3: Mở Dự Án trong Visual Studio**
1. Mở tệp **`ECOLearning.sln`** bằng Visual Studio 2022.
2. Click chuột phải vào **Solution 'ECOLearning'** ➔ chọn **Restore NuGet Packages** để tải lại các thư viện cần thiết.

### **Bước 4: Biên dịch & Chạy dự án (Run)**
1. Nhấn nút **Build Solution** (hoặc phím tắt `Ctrl + Shift + B`) để biên dịch mã nguồn.
2. Nhấn phím **`F5`** (hoặc `Ctrl + F5`) để chạy hệ thống trên IIS Express.
3. Trình duyệt tự động mở trang chủ (Ví dụ: `https://localhost:44350/`).

---

## 🔑 3. Tài Khoản Thử Nghiệm (Demo Accounts)

| Vai trò | Email đăng nhập | Mật khẩu |
| :--- | :--- | :--- |
| **Học viên (Student)** | `hocvien@elearning.vn` | `123456` |
| **Giảng viên (Instructor)** | `giao_vien@elearning.vn` | `123456` |
| **Quản trị viên (Admin)** | `admin@elearning.vn` | `123456` |

---

## 📁 4. Tài Liệu Cấu Trúc Hệ Thống
Vui lòng xem chi tiết cấu trúc thư mục và mô tả chức năng từng file tại [CauTrucHeThong.md](CauTrucHeThong.md).
