-- 012: Ảnh minh họa cho danh mục khóa học (Admin > Quản lý danh mục), hiển thị ở mục "Khám phá theo lĩnh vực" trang chủ.
-- Lưu đường dẫn ảnh (vd. /assets/images/categories/DM01-1a2b3c4d.jpg); file ảnh nằm trong assets/images/categories/.
-- Thêm cột, sau đó gắn ảnh cho các danh mục hiện có (chỉ điền chỗ còn trống, không ghi đè). Chạy lại được.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF COL_LENGTH('dbo.DanhMuc', 'anhMinhHoa') IS NULL
BEGIN
    ALTER TABLE dbo.DanhMuc ADD anhMinhHoa NVARCHAR(500) NULL;
END
GO

-- Dữ liệu: ảnh minh họa cho các danh mục hiện có. File ảnh nằm sẵn trong repo (assets/images/categories/).
-- CAT-002..CAT-006 được tạo trên máy phát triển; máy nào chưa có thì thêm (trùng mã hoặc trùng tên thì bỏ qua).
-- Mô tả và ảnh chỉ điền khi đang trống, không ghi đè thay đổi Admin đã làm.
SET NOCOUNT ON;

DECLARE @DM TABLE (ma VARCHAR(20) PRIMARY KEY, ten NVARCHAR(100), moTa NVARCHAR(500), anh NVARCHAR(500));
INSERT INTO @DM (ma, ten, moTa, anh) VALUES
 ('DM01',    N'Lập trình Web & CNTT',      N'Các khóa học lập trình Web, phần mềm và CNTT',                                   N'/assets/images/categories/DM01-d54fcc6f.jpg'),
 ('DM02',    N'Thiết kế UI/UX & Đồ họa',   N'Thiết kế giao diện, đồ họa',                                                     N'/assets/images/categories/DM02-ee150cec.jpg'),
 ('DM03',    N'Khoa học dữ liệu & AI',     N'Phân tích dữ liệu, học máy',                                                     N'/assets/images/categories/DM03-475cb7de.jpg'),
 ('DM04',    N'Marketing & Kinh doanh',    N'Digital marketing, bán hàng online, kỹ năng văn phòng',                         N'/assets/images/categories/DM04-a994c062.jpg'),
 ('DM06',    N'Nhiếp ảnh & Video',         N'Chụp ảnh, quay dựng video cơ bản',                                               N'/assets/images/categories/DM06-ff0eca1e.jpg'),
 ('CAT-002', N'Tài chính & Kế toán',       N'Kế toán doanh nghiệp, thuế, báo cáo tài chính và quản lý tài chính cá nhân',    N'/assets/images/categories/CAT-002-c4189c0e.jpg'),
 ('CAT-003', N'Marketing',                 N'Quảng cáo trực tuyến, SEO, nội dung và đo lường hiệu quả marketing',            N'/assets/images/categories/CAT-003-733e4cd0.jpg'),
 ('CAT-004', N'Thiết kế',                  N'Thiết kế nội thất, kiến trúc, ấn phẩm in ấn, bao bì và đồ họa chuyển động',      N'/assets/images/categories/CAT-004-9938a943.jpg'),
 ('CAT-005', N'Ngoại ngữ',                 N'Tiếng Anh giao tiếp, luyện thi IELTS / TOEIC, tiếng Nhật, tiếng Hàn, tiếng Trung', N'/assets/images/categories/CAT-005-4b1be6bb.jpg'),
 ('CAT-006', N'Y tế & Sức khỏe',           N'Sơ cứu, dinh dưỡng, luyện tập và chăm sóc sức khỏe thể chất lẫn tinh thần',      N'/assets/images/categories/CAT-006-0b1fa143.jpg');

INSERT INTO dbo.DanhMuc (maDanhMuc, tenDanhMuc, moTa, trangThai)
SELECT v.ma, v.ten, v.moTa, 1
FROM @DM v
WHERE v.ma LIKE 'CAT-%'
  AND NOT EXISTS (SELECT 1 FROM dbo.DanhMuc d WHERE d.maDanhMuc = v.ma OR d.tenDanhMuc = v.ten);

UPDATE d SET d.moTa = v.moTa
FROM dbo.DanhMuc d JOIN @DM v ON v.ma = d.maDanhMuc
WHERE d.moTa IS NULL OR LTRIM(RTRIM(d.moTa)) = N'';

UPDATE d SET d.anhMinhHoa = v.anh
FROM dbo.DanhMuc d JOIN @DM v ON v.ma = d.maDanhMuc
WHERE d.anhMinhHoa IS NULL OR LTRIM(RTRIM(d.anhMinhHoa)) = N'';
GO
