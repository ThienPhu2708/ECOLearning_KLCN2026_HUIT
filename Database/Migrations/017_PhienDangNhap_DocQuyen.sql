-- 017: Mỗi tài khoản Admin / Marketing chỉ có một phiên đăng nhập web hoạt động.
--   Đăng nhập mới tạo mã phiên mới thay mã cũ; phiên cũ bị đăng xuất ở lần truy cập kế tiếp (tối đa ~15 giây khi đang mở trang)
--   và thấy pop-up cho biết lần đăng nhập mới (thời gian, trình duyệt, IP). Mỗi lần một phiên bị đá ra ghi vào NhatKyQuanTri.
--   NguoiDung.maPhien         : mã phiên đang hoạt động (NULL = chưa đăng nhập lần nào từ khi có tính năng).
--   NguoiDung.phienLuc / phienIp / phienTrinhDuyet : thông tin lần đăng nhập đó, dùng cho pop-up của phiên bị thay.
-- Học viên, giáo viên không áp dụng. Chỉ thêm cột, không sửa dữ liệu cũ. Chạy lại được. Chạy sau 016 (cần bảng NhatKyQuanTri).
-- Trên hosting: bỏ dòng USE bên dưới (CSDL hosting có tên khác), chọn đúng CSDL rồi chạy.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF OBJECT_ID('dbo.NhatKyQuanTri') IS NULL
    RAISERROR(N'Cần chạy 016_QuanTri_NguoiDung.sql trước (nhật ký phiên bị thay ghi vào bảng NhatKyQuanTri).', 16, 1);
GO

IF COL_LENGTH('dbo.NguoiDung', 'maPhien') IS NULL
    ALTER TABLE dbo.NguoiDung ADD maPhien VARCHAR(32) NULL;
IF COL_LENGTH('dbo.NguoiDung', 'phienLuc') IS NULL
    ALTER TABLE dbo.NguoiDung ADD phienLuc DATETIME NULL;
IF COL_LENGTH('dbo.NguoiDung', 'phienIp') IS NULL
    ALTER TABLE dbo.NguoiDung ADD phienIp VARCHAR(45) NULL;
IF COL_LENGTH('dbo.NguoiDung', 'phienTrinhDuyet') IS NULL
    ALTER TABLE dbo.NguoiDung ADD phienTrinhDuyet NVARCHAR(100) NULL;
GO

PRINT N'017: đã bổ sung cột mã phiên đăng nhập cho NguoiDung (một phiên cho mỗi tài khoản Admin / Marketing).';



