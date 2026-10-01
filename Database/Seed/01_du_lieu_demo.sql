-- =============================================================================
-- 01_du_lieu_demo.sql — Bộ dữ liệu demo để kiểm thử chức năng Admin & Giáo viên
-- Chạy SAU các script Database/Migrations/001 → 004.
-- Xóa toàn bộ dữ liệu demo: Database/Seed/99_xoa_du_lieu_demo.sql
--
-- Nhận diện dữ liệu demo: email @demo.ecolearning.vn, mã khóa học DMKH.., đơn hàng DMDH..,
-- danh mục DM04–DM06 (tên cố định bên dưới). Không sửa / xóa dữ liệu có sẵn.
-- Ngày tháng tính lùi từ thời điểm chạy script (GETDATE()) để biểu đồ luôn có số liệu.
-- Tất cả chạy trong 1 transaction: lỗi ở đâu thì không ghi gì cả.
--
-- TÀI KHOẢN DEMO (mật khẩu chung: Demo@123)
--   Giáo viên : khoa.tran@ / ha.le@ / bao.pham@demo.ecolearning.vn
--   Học viên  : an.nguyen@, binh.tran@, cuong.le@, dung.pham@, duc.vo@, han.dang@, huy.bui@,
--               linh.ho@, nam.ngo@, phuong.duong@ (hồ sơ GV chờ duyệt), tam.ly@ (hồ sơ GV bị từ chối),
--               vy.mai@ (tài khoản BỊ KHÓA) — tất cả @demo.ecolearning.vn
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM NguoiDung WHERE email LIKE '%@demo.ecolearning.vn')
BEGIN
    RAISERROR(N'Đã có dữ liệu demo. Chạy Database/Seed/99_xoa_du_lieu_demo.sql trước nếu muốn tạo lại.', 16, 1);
    RETURN;
END

IF OBJECT_ID(N'dbo.PhanHoiViPham', N'U') IS NULL OR COL_LENGTH(N'dbo.BaoCaoViPham', N'maBaiHoc') IS NULL
BEGIN
    RAISERROR(N'Cần chạy Database/Migrations 002, 003, 004 trước khi tạo dữ liệu demo.', 16, 1);
    RETURN;
END

DECLARE @Admin INT = (SELECT TOP 1 maNguoiDung FROM NguoiDung WHERE vaiTro = N'Admin' ORDER BY maNguoiDung);
IF @Admin IS NULL
BEGIN
    RAISERROR(N'Không tìm thấy tài khoản Admin.', 16, 1);
    RETURN;
END

DECLARE @Now DATETIME = GETDATE();
DECLARE @Pw VARCHAR(20) = 'Demo@123';

BEGIN TRANSACTION;

-- =============================================================================
-- 1. NGƯỜI DÙNG
-- =============================================================================
INSERT INTO NguoiDung (hoTen, email, matKhau, vaiTro, trangThai, ngaySinh, soDienThoai, gioiTinh) VALUES
 (N'Trần Minh Khoa',    'khoa.tran@demo.ecolearning.vn',    @Pw, N'GiaoVien', 1, '1988-04-12', '0901234561', N'Nam'),
 (N'Lê Thu Hà',         'ha.le@demo.ecolearning.vn',        @Pw, N'GiaoVien', 1, '1991-09-03', '0901234562', N'Nữ'),
 (N'Phạm Quốc Bảo',     'bao.pham@demo.ecolearning.vn',     @Pw, N'GiaoVien', 1, '1985-12-20', '0901234563', N'Nam'),
 (N'Nguyễn Văn An',     'an.nguyen@demo.ecolearning.vn',    @Pw, N'HocVien',  1, '2002-01-15', '0912000001', N'Nam'),
 (N'Trần Thị Bình',     'binh.tran@demo.ecolearning.vn',    @Pw, N'HocVien',  1, '2001-06-21', '0912000002', N'Nữ'),
 (N'Lê Hoàng Cường',    'cuong.le@demo.ecolearning.vn',     @Pw, N'HocVien',  1, '1999-11-02', '0912000003', N'Nam'),
 (N'Phạm Thu Dung',     'dung.pham@demo.ecolearning.vn',    @Pw, N'HocVien',  1, '2003-03-08', '0912000004', N'Nữ'),
 (N'Võ Minh Đức',       'duc.vo@demo.ecolearning.vn',       @Pw, N'HocVien',  1, '2000-08-30', '0912000005', N'Nam'),
 (N'Đặng Ngọc Hân',     'han.dang@demo.ecolearning.vn',     @Pw, N'HocVien',  1, '2004-02-14', '0912000006', N'Nữ'),
 (N'Bùi Gia Huy',       'huy.bui@demo.ecolearning.vn',      @Pw, N'HocVien',  1, '1998-07-19', '0912000007', N'Nam'),
 (N'Hồ Mỹ Linh',        'linh.ho@demo.ecolearning.vn',      @Pw, N'HocVien',  1, '2002-10-05', '0912000008', N'Nữ'),
 (N'Ngô Thanh Nam',     'nam.ngo@demo.ecolearning.vn',      @Pw, N'HocVien',  1, '1997-05-27', '0912000009', N'Nam'),
 (N'Dương Khánh Phương','phuong.duong@demo.ecolearning.vn', @Pw, N'HocVien',  1, '1995-12-11', '0912000010', N'Nữ'),
 (N'Lý Quang Tâm',      'tam.ly@demo.ecolearning.vn',       @Pw, N'HocVien',  1, '1996-04-04', '0912000011', N'Nam'),
 (N'Mai Thảo Vy',       'vy.mai@demo.ecolearning.vn',       @Pw, N'HocVien',  0, '2003-09-09', '0912000012', N'Nữ');

DECLARE @T1 INT = (SELECT maNguoiDung FROM NguoiDung WHERE email = 'khoa.tran@demo.ecolearning.vn');
DECLARE @T2 INT = (SELECT maNguoiDung FROM NguoiDung WHERE email = 'ha.le@demo.ecolearning.vn');
DECLARE @T3 INT = (SELECT maNguoiDung FROM NguoiDung WHERE email = 'bao.pham@demo.ecolearning.vn');

-- Bảng tra học viên S1..S12
DECLARE @S TABLE (k INT PRIMARY KEY, id INT);
INSERT INTO @S (k, id)
SELECT v.k, nd.maNguoiDung
FROM (VALUES (1,'an.nguyen'),(2,'binh.tran'),(3,'cuong.le'),(4,'dung.pham'),(5,'duc.vo'),(6,'han.dang'),
             (7,'huy.bui'),(8,'linh.ho'),(9,'nam.ngo'),(10,'phuong.duong'),(11,'tam.ly'),(12,'vy.mai')) v(k, u)
JOIN NguoiDung nd ON nd.email = v.u + '@demo.ecolearning.vn';

-- =============================================================================
-- 2. DANH MỤC (DM06 đang TẠM NGƯNG nhưng vẫn có khóa học đang bán)
-- =============================================================================
INSERT INTO DanhMuc (maDanhMuc, tenDanhMuc, moTa, trangThai)
SELECT v.ma, v.ten, v.moTa, v.tt
FROM (VALUES ('DM04', N'Marketing & Kinh doanh', N'Digital marketing, bán hàng online, kỹ năng văn phòng', 1),
             ('DM05', N'Ngoại ngữ',              N'Tiếng Anh giao tiếp, tiếng Anh chuyên ngành',          1),
             ('DM06', N'Nhiếp ảnh & Video',      N'Chụp ảnh, quay dựng video cơ bản',                     0)) v(ma, ten, moTa, tt)
WHERE NOT EXISTS (SELECT 1 FROM DanhMuc d WHERE d.maDanhMuc = v.ma OR d.tenDanhMuc = v.ten);

-- =============================================================================
-- 3. KHÓA HỌC (đủ mọi trạng thái)
-- =============================================================================
INSERT INTO KhoaHoc (maKhoaHoc, tenKhoaHoc, moTaNgan, moTa, mucTieuDauRa, giaBan, giaKhuyenMai, capDo, trangThai, maDanhMuc, maGiaoVien, anhBia) VALUES
 ('DMKH01', N'Lập trình C# từ cơ bản đến nâng cao', N'Nắm vững C# và lập trình hướng đối tượng qua 30+ bài thực hành.', N'Khóa học dành cho người mới bắt đầu lập trình, đi từ cú pháp cơ bản đến OOP, LINQ và xử lý bất đồng bộ.', N'Viết được ứng dụng console hoàn chỉnh; hiểu OOP; dùng LINQ thành thạo.', 899000, 499000, N'Cơ bản', N'Đã xuất bản', 'DM01', @T1, 'https://images.unsplash.com/photo-1515879218367-8466d910aaa4?w=800&auto=format&fit=crop&q=80'),
 ('DMKH02', N'ASP.NET MVC 5 thực chiến', N'Xây dựng website bán khóa học hoàn chỉnh với ASP.NET MVC 5 và Entity Framework.', N'Từ mô hình MVC, Razor, Entity Framework đến xác thực, phân quyền và triển khai lên IIS.', N'Tự xây dựng website MVC có CSDL, đăng nhập và phân quyền.', 1290000, 790000, N'Trung cấp', N'Đã xuất bản', 'DM01', @T1, 'https://images.unsplash.com/photo-1498050108023-c5249f4df085?w=800&auto=format&fit=crop&q=80'),
 ('DMKH03', N'SQL Server cho người mới bắt đầu', N'Khóa học MIỄN PHÍ: truy vấn, thiết kế CSDL và stored procedure.', N'Học SELECT, JOIN, GROUP BY, thiết kế bảng và viết stored procedure trên SQL Server.', N'Viết truy vấn nhiều bảng; thiết kế CSDL chuẩn hóa.', 0, NULL, N'Cơ bản', N'Đã xuất bản', 'DM01', @T1, 'https://images.unsplash.com/photo-1544383835-bda2bc66a55d?w=800&auto=format&fit=crop&q=80'),
 ('DMKH04', N'Thiết kế UI/UX với Figma cơ bản', N'Thiết kế giao diện web/mobile chuyên nghiệp với Figma.', N'Auto layout, component, prototype và quy trình làm việc với lập trình viên.', N'Tự thiết kế giao diện ứng dụng hoàn chỉnh bằng Figma.', 690000, NULL, N'Cơ bản', N'Đã xuất bản', 'DM02', @T2, 'https://images.unsplash.com/photo-1561070791-2526d30994b5?w=800&auto=format&fit=crop&q=80'),
 ('DMKH05', N'Python cho Khoa học dữ liệu', N'Phân tích dữ liệu với Pandas, NumPy và trực quan hóa bằng Matplotlib.', N'Làm sạch dữ liệu, phân tích khám phá và trực quan hóa trên bộ dữ liệu thực tế.', N'Tự phân tích một bộ dữ liệu thực tế và trình bày kết quả.', 1490000, 990000, N'Trung cấp', N'Đã xuất bản', 'DM03', @T3, 'https://images.unsplash.com/photo-1526379095098-d400fd0bf935?w=800&auto=format&fit=crop&q=80'),
 ('DMKH06', N'Machine Learning ứng dụng', N'Các thuật toán học máy cơ bản và ứng dụng với scikit-learn.', N'Hồi quy, phân loại, phân cụm và đánh giá mô hình.', N'Xây dựng và đánh giá mô hình học máy cho bài toán thực tế.', 1990000, NULL, N'Nâng cao', N'Chờ duyệt', 'DM03', @T3, 'https://images.unsplash.com/photo-1555949963-aa79dcee981c?w=800&auto=format&fit=crop&q=80'),
 ('DMKH07', N'Digital Marketing 2026', N'Chạy quảng cáo Facebook, Google và tối ưu chuyển đổi.', N'Lập kế hoạch, chạy và đo lường chiến dịch quảng cáo đa kênh.', N'Tự lên kế hoạch và triển khai chiến dịch quảng cáo.', 790000, 590000, N'Cơ bản', N'Cần sửa đổi', 'DM04', @T2, 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=800&auto=format&fit=crop&q=80'),
 ('DMKH08', N'Tiếng Anh giao tiếp cho dân IT', N'Tự tin họp, viết email và phỏng vấn bằng tiếng Anh.', N'Từ vựng chuyên ngành, mẫu câu họp daily, viết email và luyện phỏng vấn.', N'Giao tiếp tự tin trong môi trường IT quốc tế.', 590000, NULL, N'Cơ bản', N'Bản nháp', 'DM05', @T2, 'https://images.unsplash.com/photo-1503676260728-1c00da094a0b?w=800&auto=format&fit=crop&q=80'),
 ('DMKH09', N'Excel nâng cao cho văn phòng', N'Hàm nâng cao, Pivot Table, Power Query và dashboard.', N'Thành thạo các hàm tra cứu, PivotTable và tự động hóa báo cáo.', N'Tự làm dashboard báo cáo bằng Excel.', 490000, 390000, N'Trung cấp', N'Tạm khóa', 'DM04', @T3, 'https://images.unsplash.com/photo-1551288049-bebda4e38f71?w=800&auto=format&fit=crop&q=80'),
 ('DMKH10', N'Chụp ảnh sản phẩm bằng điện thoại', N'Chụp ảnh bán hàng đẹp chỉ với điện thoại và ánh sáng tự nhiên.', N'Bố cục, ánh sáng, hậu kỳ bằng Lightroom mobile.', N'Tự chụp bộ ảnh sản phẩm bán hàng chuyên nghiệp.', 390000, NULL, N'Cơ bản', N'Đã xuất bản', 'DM06', @T2, 'https://images.unsplash.com/photo-1452587925148-ce544e77e70d?w=800&auto=format&fit=crop&q=80'),
 ('DMKH11', N'Kiếm tiền online cấp tốc', N'Làm giàu nhanh chóng với các mẹo kiếm tiền online.', N'Khóa học bị khóa vĩnh viễn do nội dung không đúng mô tả.', N'(Khóa học vi phạm)', 299000, NULL, N'Cơ bản', N'Khóa vĩnh viễn', 'DM04', @T3, 'https://images.unsplash.com/photo-1553729459-efe14ef6055d?w=800&auto=format&fit=crop&q=80'),
 ('DMKH12', N'JavaScript ES6 hiện đại', N'Arrow function, Promise, async/await, module và DOM.', N'JavaScript hiện đại cho front-end: ES6+, xử lý bất đồng bộ và làm việc với API.', N'Viết ứng dụng web tương tác gọi API bằng JavaScript thuần.', 990000, 690000, N'Trung cấp', N'Đã xuất bản', 'DM01', @T1, 'https://images.unsplash.com/photo-1579468118864-1b9ea3c0db4a?w=800&auto=format&fit=crop&q=80'),
 ('DMKH13', N'Photoshop cơ bản cho người mới', N'Chỉnh sửa ảnh, ghép ảnh và thiết kế banner đơn giản.', N'Layer, mask, công cụ chọn vùng, chỉnh màu và xuất file.', N'Tự thiết kế banner quảng cáo bằng Photoshop.', 590000, 450000, N'Cơ bản', N'Tạm khóa', 'DM02', @T2, 'https://images.unsplash.com/photo-1572044162444-ad60f128bdea?w=800&auto=format&fit=crop&q=80');

-- =============================================================================
-- 4. CHƯƠNG & BÀI HỌC (video lấy từ assets/uploads/videos có sẵn trong repo)
-- =============================================================================
DECLARE @V TABLE (i INT PRIMARY KEY, path VARCHAR(500));
INSERT INTO @V VALUES
 (0, '/assets/uploads/videos/vid_KH260929150333_20260930121911_38701957.mp4'),
 (1, '/assets/uploads/videos/vid_KH260929150333_20260930121937_ce47af51.mp4'),
 (2, '/assets/uploads/videos/vid_KH260929150333_20260930122326_f57827e0.mp4'),
 (3, '/assets/uploads/videos/vid_KH260929150333_20260930122340_89927fa4.mp4'),
 (4, '/assets/uploads/videos/vid_KH260929150333_20260930122914_3b602423.mp4'),
 (5, '/assets/uploads/videos/vid_KH260929150333_20260930122938_d511db07.mp4'),
 (6, '/assets/uploads/videos/vid_KH260929150333_20260930122951_cca76a8a.mp4'),
 (7, '/assets/uploads/videos/vid_KH260929150333_20260930123009_3a897671.mp4');

DECLARE @L TABLE (kh VARCHAR(20), ch INT, chName NVARCHAR(200), no INT, name NVARCHAR(200), dur INT, preview BIT);
INSERT INTO @L VALUES
 ('DMKH01',1,N'Làm quen với C#',1,N'Cài đặt Visual Studio và chương trình đầu tiên',420,1),
 ('DMKH01',1,N'Làm quen với C#',2,N'Biến, kiểu dữ liệu và toán tử',780,0),
 ('DMKH01',1,N'Làm quen với C#',3,N'Câu lệnh rẽ nhánh và vòng lặp',900,0),
 ('DMKH01',2,N'Lập trình hướng đối tượng',4,N'Class, object và constructor',840,0),
 ('DMKH01',2,N'Lập trình hướng đối tượng',5,N'Kế thừa và đa hình',960,0),
 ('DMKH01',2,N'Lập trình hướng đối tượng',6,N'Interface và abstract class',720,0),
 ('DMKH02',1,N'Nền tảng MVC',1,N'Mô hình MVC hoạt động thế nào',600,1),
 ('DMKH02',1,N'Nền tảng MVC',2,N'Controller, Action và Routing',900,0),
 ('DMKH02',1,N'Nền tảng MVC',3,N'Razor View và Layout',840,0),
 ('DMKH02',2,N'Làm việc với dữ liệu',4,N'Entity Framework Database First',1020,0),
 ('DMKH02',2,N'Làm việc với dữ liệu',5,N'Xác thực và phân quyền với Session',960,0),
 ('DMKH02',2,N'Làm việc với dữ liệu',6,N'Triển khai website lên IIS',660,0),
 ('DMKH03',1,N'Truy vấn cơ bản',1,N'Giới thiệu SQL Server và SSMS',480,1),
 ('DMKH03',1,N'Truy vấn cơ bản',2,N'SELECT, WHERE và ORDER BY',720,0),
 ('DMKH03',2,N'Truy vấn nâng cao',3,N'JOIN nhiều bảng',840,0),
 ('DMKH03',2,N'Truy vấn nâng cao',4,N'GROUP BY và hàm tổng hợp',780,0),
 ('DMKH03',2,N'Truy vấn nâng cao',5,N'Viết Stored Procedure',900,0),
 ('DMKH04',1,N'Làm quen Figma',1,N'Giao diện Figma và frame',540,1),
 ('DMKH04',1,N'Làm quen Figma',2,N'Auto Layout',780,0),
 ('DMKH04',2,N'Thiết kế hệ thống',3,N'Component và Variant',900,0),
 ('DMKH04',2,N'Thiết kế hệ thống',4,N'Prototype và bàn giao cho lập trình viên',840,0),
 ('DMKH05',1,N'Python nền tảng',1,N'Cài đặt Anaconda và Jupyter',480,1),
 ('DMKH05',1,N'Python nền tảng',2,N'Cú pháp Python cho phân tích dữ liệu',900,0),
 ('DMKH05',2,N'Phân tích dữ liệu',3,N'Pandas: đọc và làm sạch dữ liệu',1080,0),
 ('DMKH05',2,N'Phân tích dữ liệu',4,N'NumPy và tính toán vector',840,0),
 ('DMKH05',2,N'Phân tích dữ liệu',5,N'Trực quan hóa với Matplotlib',960,0),
 ('DMKH05',2,N'Phân tích dữ liệu',6,N'Dự án: phân tích dữ liệu bán hàng',1200,0),
 ('DMKH06',1,N'Học máy cơ bản',1,N'Học máy là gì',600,1),
 ('DMKH06',1,N'Học máy cơ bản',2,N'Hồi quy tuyến tính',1020,0),
 ('DMKH06',2,N'Phân loại',3,N'Logistic Regression và cây quyết định',1140,0),
 ('DMKH06',2,N'Phân loại',4,N'Đánh giá mô hình',900,0),
 ('DMKH07',1,N'Tổng quan Digital Marketing',1,N'Phễu marketing và chân dung khách hàng',600,1),
 ('DMKH07',1,N'Tổng quan Digital Marketing',2,N'Chạy quảng cáo Facebook',960,0),
 ('DMKH07',2,N'Đo lường',3,N'Google Analytics 4 cơ bản',840,0),
 ('DMKH08',1,N'Giao tiếp hằng ngày',1,N'Giới thiệu bản thân trong team',420,1),
 ('DMKH08',1,N'Giao tiếp hằng ngày',2,N'Họp daily stand-up',540,0),
 ('DMKH09',1,N'Hàm nâng cao',1,N'VLOOKUP, XLOOKUP và INDEX MATCH',780,1),
 ('DMKH09',1,N'Hàm nâng cao',2,N'Hàm điều kiện IFS, SUMIFS, COUNTIFS',840,0),
 ('DMKH09',2,N'Báo cáo',3,N'PivotTable và PivotChart',900,0),
 ('DMKH09',2,N'Báo cáo',4,N'Power Query làm sạch dữ liệu',960,0),
 ('DMKH09',2,N'Báo cáo',5,N'Dashboard báo cáo tự động',1020,0),
 ('DMKH10',1,N'Kỹ thuật chụp',1,N'Bố cục và góc chụp sản phẩm',540,1),
 ('DMKH10',1,N'Kỹ thuật chụp',2,N'Tận dụng ánh sáng tự nhiên',600,0),
 ('DMKH10',2,N'Hậu kỳ',3,N'Chỉnh ảnh với Lightroom mobile',720,0),
 ('DMKH10',2,N'Hậu kỳ',4,N'Xuất ảnh cho sàn thương mại điện tử',480,0),
 ('DMKH11',1,N'Bí quyết làm giàu',1,N'Tư duy làm giàu nhanh',600,1),
 ('DMKH11',1,N'Bí quyết làm giàu',2,N'Các kênh kiếm tiền online',720,0),
 ('DMKH11',1,N'Bí quyết làm giàu',3,N'Đăng ký gói VIP để nhận bí quyết',300,0),
 ('DMKH12',1,N'ES6+',1,N'let, const và arrow function',600,1),
 ('DMKH12',1,N'ES6+',2,N'Destructuring, spread và module',780,0),
 ('DMKH12',2,N'Bất đồng bộ',3,N'Promise và async/await',900,0),
 ('DMKH12',2,N'Bất đồng bộ',4,N'Gọi API với fetch',840,0),
 ('DMKH12',2,N'Bất đồng bộ',5,N'Thao tác DOM và sự kiện',720,0),
 ('DMKH13',1,N'Làm quen Photoshop',1,N'Giao diện và layer',540,1),
 ('DMKH13',1,N'Làm quen Photoshop',2,N'Công cụ chọn vùng và mask',780,0),
 ('DMKH13',2,N'Thiết kế',3,N'Chỉnh màu ảnh',660,0),
 ('DMKH13',2,N'Thiết kế',4,N'Thiết kế banner quảng cáo',900,0);

INSERT INTO ChuongHoc (maChuong, tenChuong, thuTu, maKhoaHoc)
SELECT DISTINCT kh + '_C' + CAST(ch AS VARCHAR(5)), chName, ch, kh FROM @L;

INSERT INTO BaiHoc (maBaiHoc, tenBaiHoc, thoiLuong, thuTu, maChuong, duongDanVideo, choXemThu)
SELECT l.kh + '_' + CAST(l.no AS VARCHAR(5)), l.name, l.dur, l.no, l.kh + '_C' + CAST(l.ch AS VARCHAR(5)), v.path, l.preview
FROM @L l
JOIN @V v ON v.i = (l.no + CAST(RIGHT(l.kh, 2) AS INT)) % 8;

-- =============================================================================
-- 5. KIỂM DUYỆT KHÓA HỌC (lịch sử duyệt, 1 khóa chờ duyệt, 1 khóa bị từ chối)
-- =============================================================================
INSERT INTO KiemDuyetKhoaHoc (maKiemDuyet, ngayGui, ngayKiemDuyet, trangThai, lyDoTuChoi, maKhoaHoc, maAdmin)
SELECT 'DMKD' + RIGHT(k.maKhoaHoc, 2), DATEADD(DAY, -365, @Now), DATEADD(DAY, -364, @Now), N'Đã duyệt', NULL, k.maKhoaHoc, @Admin
FROM KhoaHoc k
WHERE k.maKhoaHoc IN ('DMKH01','DMKH02','DMKH03','DMKH04','DMKH05','DMKH09','DMKH10','DMKH11','DMKH12','DMKH13');

INSERT INTO KiemDuyetKhoaHoc (maKiemDuyet, ngayGui, ngayKiemDuyet, trangThai, lyDoTuChoi, maKhoaHoc, maAdmin) VALUES
 ('DMKD06', DATEADD(DAY, -2, @Now), NULL, N'Chờ duyệt', NULL, 'DMKH06', NULL),
 ('DMKD07', DATEADD(DAY, -9, @Now), DATEADD(DAY, -7, @Now), N'Từ chối',
  N'Video chương 2 bị mất tiếng từ phút 3. Vui lòng bổ sung tài liệu tham khảo cho bài Google Analytics.', 'DMKH07', @Admin);

-- =============================================================================
-- 6. ĐƠN HÀNG, THANH TOÁN, ĐĂNG KÝ KHÓA HỌC
--    Trigger trg_ThanhToan_TuDongDangKy tự đăng ký khóa học khi thanh toán thành công.
-- =============================================================================
DECLARE @E TABLE (id INT IDENTITY(1,1) PRIMARY KEY, s INT, kh VARCHAR(20), daysAgo INT, pct INT, method NVARCHAR(50));
INSERT INTO @E (s, kh, daysAgo, pct, method) VALUES
 (1,'DMKH01',300,100,N'VNPay'), (1,'DMKH02',250,67,N'MoMo'), (1,'DMKH12',20,40,N'VNPay'),
 (2,'DMKH01',280,83,N'Chuyển khoản'), (2,'DMKH05',150,50,N'VNPay'), (2,'DMKH09',60,60,N'MoMo'),
 (3,'DMKH02',200,33,N'VNPay'), (3,'DMKH04',120,100,N'VNPay'), (3,'DMKH13',45,50,N'MoMo'),
 (4,'DMKH04',330,75,N'Chuyển khoản'), (4,'DMKH05',90,100,N'VNPay'), (4,'DMKH12',15,20,N'VNPay'), (4,'DMKH10',20,25,N'MoMo'),
 (5,'DMKH01',180,50,N'MoMo'), (5,'DMKH11',100,67,N'VNPay'),
 (6,'DMKH05',75,17,N'VNPay'), (6,'DMKH12',10,0,N'MoMo'), (6,'DMKH09',50,40,N'VNPay'),
 (7,'DMKH02',130,50,N'VNPay'), (7,'DMKH04',40,25,N'Chuyển khoản'), (7,'DMKH01',5,0,N'VNPay'), (7,'DMKH10',12,50,N'VNPay'),
 (8,'DMKH01',60,33,N'MoMo'), (8,'DMKH13',35,25,N'VNPay'), (8,'DMKH05',0,0,N'VNPay'),
 (9,'DMKH12',25,60,N'VNPay'), (9,'DMKH02',8,17,N'MoMo'), (9,'DMKH04',0,0,N'VNPay'),
 (3,'DMKH12',0,0,N'MoMo'), (10,'DMKH04',1,0,N'VNPay'),
 (10,'DMKH01',95,67,N'VNPay'),
 (11,'DMKH05',210,33,N'Chuyển khoản'), (11,'DMKH02',160,50,N'VNPay'),
 (12,'DMKH04',240,25,N'MoMo');

-- Đơn hàng: 1 đơn / 1 khóa học. donGia = giá khuyến mãi nếu có.
INSERT INTO DonHang (maDonHang, ngayDat, tongTien, trangThai, maHocVien)
SELECT 'DMDH' + RIGHT('000' + CAST(e.id AS VARCHAR(5)), 3), DATEADD(MINUTE, -((e.id % 5) * 11), DATEADD(DAY, -e.daysAgo, @Now)), 0, N'Đã thanh toán', s.id
FROM @E e JOIN @S s ON s.k = e.s;

INSERT INTO ChiTietDonHang (maChiTiet, donGia, maDonHang, maKhoaHoc)
SELECT 'DMCT' + RIGHT('000' + CAST(e.id AS VARCHAR(5)), 3), ISNULL(k.giaKhuyenMai, k.giaBan),
       'DMDH' + RIGHT('000' + CAST(e.id AS VARCHAR(5)), 3), e.kh
FROM @E e JOIN KhoaHoc k ON k.maKhoaHoc = e.kh;

INSERT INTO ThanhToan (maThanhToan, soTien, phuongThuc, trangThaiThanhToan, maDonHang)
SELECT 'DMTT' + RIGHT('000' + CAST(e.id AS VARCHAR(5)), 3), ISNULL(k.giaKhuyenMai, k.giaBan), e.method, N'Đã thanh toán',
       'DMDH' + RIGHT('000' + CAST(e.id AS VARCHAR(5)), 3)
FROM @E e JOIN KhoaHoc k ON k.maKhoaHoc = e.kh;

-- Trigger đăng ký với ngày hiện tại -> đổi về đúng ngày đặt hàng
UPDATE dk SET dk.ngayDangKy = dh.ngayDat
FROM DangKyKhoaHoc dk
JOIN DonHang dh ON dh.maHocVien = dk.maHocVien
JOIN ChiTietDonHang ct ON ct.maDonHang = dh.maDonHang AND ct.maKhoaHoc = dk.maKhoaHoc
WHERE dh.maDonHang LIKE 'DMDH%';

-- Khóa học miễn phí DMKH03: đăng ký trực tiếp (không qua đơn hàng)
DECLARE @Free TABLE (id INT IDENTITY(1,1), s INT, daysAgo INT, pct INT);
INSERT INTO @Free (s, daysAgo, pct) VALUES (1,240,100), (5,170,60), (10,30,40), (3,14,20), (9,4,0);
INSERT INTO DangKyKhoaHoc (maDangKy, ngayDangKy, trangThaiDK, maHocVien, maKhoaHoc)
SELECT 'DMDK' + RIGHT('000' + CAST(f.id AS VARCHAR(5)), 3), DATEADD(DAY, -f.daysAgo, @Now), N'Đã kích hoạt', s.id, 'DMKH03'
FROM @Free f JOIN @S s ON s.k = f.s;
INSERT INTO @E (s, kh, daysAgo, pct, method) SELECT s, 'DMKH03', daysAgo, pct, NULL FROM @Free;

-- Đơn hàng chưa thanh toán (không tính doanh thu, không tạo đăng ký)
INSERT INTO DonHang (maDonHang, ngayDat, tongTien, trangThai, maHocVien)
SELECT v.ma, DATEADD(DAY, -v.d, @Now), 0, N'Chờ thanh toán', s.id
FROM (VALUES ('DMDH901', 9, 2), ('DMDH902', 6, 1)) v(ma, k, d) JOIN @S s ON s.k = v.k;
INSERT INTO ChiTietDonHang (maChiTiet, donGia, maDonHang, maKhoaHoc) VALUES
 ('DMCT901', 990000, 'DMDH901', 'DMKH05'),
 ('DMCT902', 690000, 'DMDH902', 'DMKH01');

-- =============================================================================
-- 7. TIẾN ĐỘ HỌC (hoàn thành pct% số bài đầu tiên của mỗi khóa đã đăng ký)
--    Trigger tự cập nhật % trong TienDoHocTap.
-- =============================================================================
INSERT INTO TienDoBaiHoc (thoiGianXemDungLai, daHoanThanh, ngayHoanThanh, lanTruyCapGanNhat, maHocVien, maBaiHoc)
SELECT b.thoiLuong, 1,
       DATEADD(DAY, -(e.daysAgo * (tot.n - b.thuTu + 1) / (tot.n + 1)), @Now),
       DATEADD(DAY, -(e.daysAgo * (tot.n - b.thuTu + 1) / (tot.n + 1)), @Now),
       s.id, b.maBaiHoc
FROM @E e
JOIN @S s ON s.k = e.s
JOIN ChuongHoc c ON c.maKhoaHoc = e.kh
JOIN BaiHoc b ON b.maChuong = c.maChuong
CROSS APPLY (SELECT COUNT(*) AS n FROM BaiHoc b2 JOIN ChuongHoc c2 ON c2.maChuong = b2.maChuong WHERE c2.maKhoaHoc = e.kh) tot
WHERE b.thuTu <= CEILING(e.pct * tot.n / 100.0);

-- Bài đang xem dở (chưa đạt 80%) ngay sau phần đã hoàn thành
INSERT INTO TienDoBaiHoc (thoiGianXemDungLai, daHoanThanh, ngayHoanThanh, lanTruyCapGanNhat, maHocVien, maBaiHoc)
SELECT b.thoiLuong / 3, 0, NULL, DATEADD(DAY, -1, @Now), s.id, b.maBaiHoc
FROM @E e
JOIN @S s ON s.k = e.s
JOIN ChuongHoc c ON c.maKhoaHoc = e.kh
JOIN BaiHoc b ON b.maChuong = c.maChuong
CROSS APPLY (SELECT COUNT(*) AS n FROM BaiHoc b2 JOIN ChuongHoc c2 ON c2.maChuong = b2.maChuong WHERE c2.maKhoaHoc = e.kh) tot
WHERE e.pct < 100 AND b.thuTu = CEILING(e.pct * tot.n / 100.0) + 1;

-- =============================================================================
-- 8. ĐÁNH GIÁ (trigger yêu cầu học viên hoàn thành >= 25% khóa học)
-- =============================================================================
INSERT INTO DanhGiaKhoaHoc (maDanhGia, soSao, noiDung, ngayDanhGia, maHocVien, maKhoaHoc)
SELECT 'DMDG' + RIGHT('00' + CAST(ROW_NUMBER() OVER (ORDER BY v.kh, v.s) AS VARCHAR(5)), 2), v.sao, v.nd, DATEADD(DAY, -v.d, @Now), s.id, v.kh
FROM (VALUES
 (1,'DMKH01',5,N'Thầy giảng rất dễ hiểu, bài tập thực hành sát thực tế. Rất đáng tiền!',200),
 (2,'DMKH01',4,N'Nội dung tốt, mong có thêm bài về async/await.',150),
 (5,'DMKH01',5,N'Từ người chưa biết gì giờ đã viết được ứng dụng nhỏ.',90),
 (10,'DMKH01',4,N'Khóa học chất lượng, phần OOP hơi nhanh.',40),
 (1,'DMKH02',5,N'Làm xong khóa này mình tự làm được website đồ án.',120),
 (7,'DMKH02',4,N'Phần Entity Framework rất hữu ích.',60),
 (11,'DMKH02',4,N'Ổn, nhưng nên cập nhật thêm phần triển khai.',100),
 (1,'DMKH03',5,N'Khóa miễn phí mà chất lượng như khóa trả phí.',180),
 (5,'DMKH03',4,N'Dễ hiểu, phù hợp người mới.',120),
 (3,'DMKH04',4,N'Học xong tự thiết kế được giao diện app.',70),
 (4,'DMKH04',5,N'Cô hướng dẫn auto layout cực kỳ chi tiết.',200),
 (2,'DMKH05',4,N'Bài tập Pandas rất thực tế.',80),
 (4,'DMKH05',5,N'Dự án cuối khóa rất hay, áp dụng được vào công việc.',30),
 (11,'DMKH05',3,N'Nội dung tốt nhưng tốc độ giảng hơi nhanh.',150),
 (2,'DMKH09',2,N'Công thức ở bài 2 bị sai, làm theo không ra kết quả.',40),
 (7,'DMKH10',4,N'Mẹo chụp ảnh bằng điện thoại rất hữu ích.',5),
 (5,'DMKH11',1,N'Nội dung không đúng mô tả, toàn quảng cáo gói VIP.',90),
 (1,'DMKH12',5,N'Giải thích async/await dễ hiểu nhất mình từng học.',10),
 (9,'DMKH12',4,N'Hay, mong có thêm bài về TypeScript.',12)
) v(s, kh, sao, nd, d)
JOIN @S s ON s.k = v.s;

-- =============================================================================
-- 9. HỒ SƠ ĐĂNG KÝ GIÁO VIÊN (đã duyệt / chờ duyệt / từ chối)
-- =============================================================================
INSERT INTO DangKyGiaoVien (maDon, maNguoiDung, chuyenMon, kinhNghiem, tenNganHang, soTaiKhoan, tenChuTaiKhoan, tepChungChi, tenTepChungChi, ngayGui, ngayDuyet, trangThai, lyDoTuChoi, maAdmin) VALUES
 ('DMGV001', @T1, N'Lập trình .NET, C#, ASP.NET MVC', N'10 năm phát triển phần mềm, 5 năm giảng dạy tại trung tâm đào tạo lập trình.', N'Vietcombank', '0071000123456', N'TRAN MINH KHOA', '/assets/uploads/certificates/cert_8_20260929101748.pdf', N'Chung_chi_MCSD.pdf', DATEADD(DAY,-400,@Now), DATEADD(DAY,-398,@Now), 'Approved', NULL, @Admin),
 ('DMGV002', @T2, N'Thiết kế UI/UX, Marketing', N'6 năm làm Product Designer, từng đào tạo nội bộ cho đội thiết kế.', N'Techcombank', '19033445566', N'LE THU HA', '/assets/uploads/certificates/cert_8_20260922230320.jpg', N'Google_UX_Certificate.jpg', DATEADD(DAY,-390,@Now), DATEADD(DAY,-389,@Now), 'Approved', NULL, @Admin),
 ('DMGV003', @T3, N'Khoa học dữ liệu, Python, Excel', N'Thạc sĩ Khoa học dữ liệu, 8 năm làm Data Analyst.', N'BIDV', '31410000778899', N'PHAM QUOC BAO', '/assets/uploads/certificates/cert_13_20260929134334.jpg', N'Bang_Thac_si.jpg', DATEADD(DAY,-380,@Now), DATEADD(DAY,-379,@Now), 'Approved', NULL, @Admin);

INSERT INTO DangKyGiaoVien (maDon, maNguoiDung, chuyenMon, kinhNghiem, tenNganHang, soTaiKhoan, tenChuTaiKhoan, tepChungChi, tenTepChungChi, ngayGui, ngayDuyet, trangThai, lyDoTuChoi, maAdmin)
SELECT v.ma, s.id, v.cm, v.kn, v.nh, v.stk, v.ten, v.tep, v.tenTep, DATEADD(DAY, -v.d, @Now),
       CASE WHEN v.tt = 'Rejected' THEN DATEADD(DAY, -v.d + 1, @Now) END, v.tt, v.lydo, CASE WHEN v.tt = 'Rejected' THEN @Admin END
FROM (VALUES
 ('DMGV004', 10, N'Tiếng Anh giao tiếp, IELTS', N'IELTS 8.0, 4 năm dạy tiếng Anh giao tiếp cho người đi làm.', N'ACB', '12345678', N'DUONG KHANH PHUONG', '/assets/uploads/certificates/cert_8_20260929101748.pdf', N'IELTS_8.0.pdf', 2, 'Pending', NULL),
 ('DMGV005', 7, N'Nhiếp ảnh sản phẩm', N'3 năm chụp ảnh cho các shop thời trang online.', N'MB Bank', '0909123456', N'BUI GIA HUY', '/assets/uploads/certificates/cert_8_20260922230320.jpg', N'Portfolio.jpg', 1, 'Pending', NULL),
 ('DMGV006', 11, N'Đầu tư chứng khoán', N'Tự học, 1 năm kinh nghiệm.', N'VPBank', '88889999', N'LY QUANG TAM', NULL, NULL, 20, 'Rejected', N'Chưa có chứng chỉ/bằng cấp chuyên môn phù hợp. Vui lòng bổ sung chứng chỉ hành nghề và minh chứng kinh nghiệm.')
) v(ma, k, cm, kn, nh, stk, ten, tep, tenTep, d, tt, lydo)
JOIN @S s ON s.k = v.k;

-- =============================================================================
-- 10. BÁO CÁO VI PHẠM & XỬ LÝ
--   DMKH12: 2 báo cáo CHỜ XỬ LÝ  -> Admin thử Tạm khóa / Bác bỏ
--   DMKH01: 1 báo cáo đã BÁC BỎ
--   DMKH13: TẠM KHÓA, QUÁ HẠN, giáo viên chưa phản hồi
--   DMKH09: TẠM KHÓA, giáo viên đã phản hồi -> CHỜ ADMIN XEM XÉT (có 1 vòng "Yêu cầu sửa thêm")
--   DMKH11: KHÓA VĨNH VIỄN (giáo viên khiếu nại không thành)
-- =============================================================================
INSERT INTO BaoCaoViPham (maBaoCao, maKhoaHoc, maNguoiBaoCao, loaiViPham, moTa, maBaiHoc, trangThai, ngayTao, ngayXuLy, maAdmin, ghiChuXuLy)
SELECT v.ma, v.kh, s.id, v.loai, v.moTa, v.bai, v.tt, DATEADD(DAY, -v.d, @Now),
       CASE WHEN v.tt <> N'Chờ xử lý' THEN DATEADD(DAY, -v.dxl, @Now) END,
       CASE WHEN v.tt <> N'Chờ xử lý' THEN @Admin END, v.ghiChu
FROM (VALUES
 ('DMBC01','DMKH12', 6, N'Sai sót kiến thức / thông tin', N'Ở phút 5 bài "Promise và async/await", giảng viên nói await chặn toàn bộ trình duyệt — thông tin này sai.', 'DMKH12_3', N'Chờ xử lý', 2, 0, NULL),
 ('DMBC02','DMKH12', 9, N'Video / tài liệu lỗi, không xem được', N'Video bài "Gọi API với fetch" bị đứng hình từ giữa bài, đã thử nhiều trình duyệt.', 'DMKH12_4', N'Chờ xử lý', 1, 0, NULL),
 ('DMBC03','DMKH01', 2, N'Nội dung không đúng mô tả', N'Mô tả ghi có phần async/await nhưng không thấy bài nào.', NULL, N'Đã bác bỏ', 30, 28, N'Nội dung async/await nằm trong lộ trình cập nhật, mô tả không sai lệch đáng kể.'),
 ('DMBC04','DMKH13', 3, N'Vi phạm bản quyền', N'Video bài "Chỉnh màu ảnh" lấy nguyên từ kênh YouTube của người khác, còn cả logo kênh gốc.', 'DMKH13_3', N'Đã xử lý', 12, 10, N'Đã tạm khóa khóa học: Video bài 3 sử dụng nội dung có bản quyền của bên thứ ba.'),
 ('DMBC05','DMKH13', 8, N'Vi phạm bản quyền', N'Bài 3 giống hệt video trên kênh khác.', 'DMKH13_3', N'Đã xử lý', 11, 10, N'Đã tạm khóa khóa học: Video bài 3 sử dụng nội dung có bản quyền của bên thứ ba.'),
 ('DMBC06','DMKH09', 2, N'Sai sót kiến thức / thông tin', N'Công thức SUMIFS ở bài 2 sai thứ tự tham số, làm theo ra kết quả sai.', 'DMKH09_2', N'Đã xử lý', 9, 8, N'Đã tạm khóa khóa học: Bài 2 có công thức sai, cần sửa lại video và tài liệu.'),
 ('DMBC07','DMKH11', 5, N'Nội dung không đúng mô tả', N'Khóa học quảng cáo kiếm tiền nhưng thực chất chỉ dụ mua gói VIP bên ngoài.', 'DMKH11_3', N'Đã xử lý', 45, 44, N'Đã tạm khóa khóa học: Nội dung không đúng mô tả, có dấu hiệu lừa đảo.')
) v(ma, kh, k, loai, moTa, bai, tt, d, dxl, ghiChu)
JOIN @S s ON s.k = v.k;

INSERT INTO XuLyViPham (maXuLy, maKhoaHoc, hanhDong, lyDo, hanPhanHoi, trangThaiTruoc, phanHoiGiaoVien, ngayPhanHoi, maAdmin, ngayXuLy, trangThaiXuLy) VALUES
 ('DMXL01','DMKH01', N'Bác bỏ', N'Nội dung async/await nằm trong lộ trình cập nhật, mô tả không sai lệch đáng kể.', NULL, NULL, NULL, NULL, @Admin, DATEADD(DAY,-28,@Now), NULL),
 ('DMXL02','DMKH13', N'Tạm khóa', N'Video bài 3 sử dụng nội dung có bản quyền của bên thứ ba. Vui lòng thay thế bằng video tự quay.', DATEADD(DAY,-3,@Now), N'Đã xuất bản', NULL, NULL, @Admin, DATEADD(DAY,-10,@Now), N'Chờ GV xử lý'),
 ('DMXL03','DMKH09', N'Tạm khóa', N'Bài 2 có công thức sai, cần sửa lại video và tài liệu.', DATEADD(DAY,2,@Now), N'Đã xuất bản',
   N'Đã quay lại video bài 2 với công thức SUMIFS đúng và bổ sung file bài tập mẫu.', DATEADD(HOUR,-5,@Now), @Admin, DATEADD(DAY,-8,@Now), N'Chờ Admin xem xét'),
 ('DMXL04','DMKH11', N'Tạm khóa', N'Nội dung không đúng mô tả, có dấu hiệu lừa đảo.', DATEADD(DAY,-37,@Now), N'Đã xuất bản',
   N'Khóa học chỉ giới thiệu các kênh kiếm tiền, việc mua gói VIP là tùy học viên.', DATEADD(DAY,-40,@Now), @Admin, DATEADD(DAY,-44,@Now), N'Đã kết thúc'),
 ('DMXL05','DMKH11', N'Khóa vĩnh viễn', N'Giáo viên không chỉnh sửa nội dung; bài 3 tiếp tục dẫn học viên mua gói VIP bên ngoài nền tảng.', NULL, N'Tạm khóa', NULL, NULL, @Admin, DATEADD(DAY,-35,@Now), NULL);

INSERT INTO PhanHoiViPham (maPhanHoi, maXuLy, maNguoiGui, vaiTroGui, loai, noiDung, hanPhanHoiMoi, ngayGui) VALUES
 ('DMPH01','DMXL03', @T3,    N'GiaoVien', N'Đã chỉnh sửa', N'Đã sửa lại công thức trong file Excel đính kèm.', NULL, DATEADD(DAY,-6,@Now)),
 ('DMPH02','DMXL03', @Admin, N'Admin',    N'Yêu cầu sửa thêm', N'Video bài 2 vẫn dùng công thức cũ, chỉ file đính kèm được sửa. Vui lòng quay lại video.', DATEADD(DAY,2,@Now), DATEADD(DAY,-5,@Now)),
 ('DMPH03','DMXL03', @T3,    N'GiaoVien', N'Đã chỉnh sửa', N'Đã quay lại video bài 2 với công thức SUMIFS đúng và bổ sung file bài tập mẫu.', NULL, DATEADD(HOUR,-5,@Now)),
 ('DMPH04','DMXL04', @T3,    N'GiaoVien', N'Khiếu nại', N'Khóa học chỉ giới thiệu các kênh kiếm tiền, việc mua gói VIP là tùy học viên.', NULL, DATEADD(DAY,-40,@Now));

-- =============================================================================
-- 11. YÊU CẦU NHẬN THANH TOÁN CỦA GIÁO VIÊN (trigger tự tính thực nhận)
-- =============================================================================
INSERT INTO NhanThanhToan (maPhieuChi, tiLeChietKhau, soTienYeuCau, thucNhan, ngayYeuCau, nganHang, trangThai, maGiaoVien, soTaiKhoan, tenChuTaiKhoan) VALUES
 ('DMPC01', 20, 3000000, 0, DATEADD(DAY,-90,@Now), N'Vietcombank', N'Đã thanh toán', @T1, '0071000123456', N'TRAN MINH KHOA'),
 ('DMPC02', 20, 1500000, 0, DATEADD(DAY,-2,@Now),  N'Vietcombank', N'Chờ duyệt',     @T1, '0071000123456', N'TRAN MINH KHOA'),
 ('DMPC03', 20, 2000000, 0, DATEADD(DAY,-30,@Now), N'BIDV',        N'Đã thanh toán', @T3, '31410000778899', N'PHAM QUOC BAO'),
 ('DMPC04', 20, 500000,  0, DATEADD(DAY,-15,@Now), N'Techcombank', N'Từ chối',       @T2, '19033445566', N'LE THU HA');

COMMIT TRANSACTION;

-- =============================================================================
-- TỔNG KẾT
-- =============================================================================
SELECT N'Người dùng demo' AS [Dữ liệu], COUNT(*) AS [Số lượng] FROM NguoiDung WHERE email LIKE '%@demo.ecolearning.vn'
UNION ALL SELECT N'Khóa học demo', COUNT(*) FROM KhoaHoc WHERE maKhoaHoc LIKE 'DMKH%'
UNION ALL SELECT N'Bài học demo', COUNT(*) FROM BaiHoc WHERE maBaiHoc LIKE 'DMKH%'
UNION ALL SELECT N'Đơn hàng demo (đã thanh toán)', COUNT(*) FROM DonHang WHERE maDonHang LIKE 'DMDH%' AND trangThai = N'Đã thanh toán'
UNION ALL SELECT N'Doanh thu demo (VNĐ)', CAST(SUM(tongTien) AS INT) FROM DonHang WHERE maDonHang LIKE 'DMDH%' AND trangThai = N'Đã thanh toán'
UNION ALL SELECT N'Lượt đăng ký khóa học demo', COUNT(*) FROM DangKyKhoaHoc WHERE maKhoaHoc LIKE 'DMKH%'
UNION ALL SELECT N'Đánh giá demo', COUNT(*) FROM DanhGiaKhoaHoc WHERE maKhoaHoc LIKE 'DMKH%'
UNION ALL SELECT N'Báo cáo vi phạm demo', COUNT(*) FROM BaoCaoViPham WHERE maBaoCao LIKE 'DMBC%';
GO
