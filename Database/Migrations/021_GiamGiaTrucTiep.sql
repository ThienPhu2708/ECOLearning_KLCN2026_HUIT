-- =============================================================================
-- 021_GiamGiaTrucTiep.sql
--
-- Khuyến mãi kiểu "Giảm trực tiếp" (không cần mã): server tính một lần giá hiệu lực của từng khóa học
-- (Services/CourseDiscountService.cs) rồi web và API ứng dụng cùng hiển thị giá cũ gạch ngang, giá mới và nhãn "-X%".
--   KhuyenMai.loaiKhuyenMai : Coupon (mặc định, mã nhập khi thanh toán) | GiamTrucTiep (tự áp dụng, không có dòng MaGiamGia).
--   ChiTietDonHang.giaGoc   : giá gốc (giaBan) của khóa tại lúc tạo đơn; donGia vẫn là giá thanh toán. Đơn cũ để NULL (= donGia).
--   ChiTietDonHang.maKhuyenMaiTrucTiep : chương trình giảm trực tiếp đã áp dụng cho dòng đơn (NULL = không giảm).
-- Chỉ thêm cột, không sửa dữ liệu cũ (mọi khuyến mãi hiện có là Coupon). Chạy lại nhiều lần vẫn an toàn.
-- Chạy sau 008, 009, 018. Thiếu file này thì các trang Marketing báo "Chưa khởi tạo dữ liệu".
-- Trên hosting: bỏ dòng USE bên dưới (CSDL hosting có tên khác), chọn đúng CSDL rồi chạy.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF COL_LENGTH(N'dbo.KhuyenMai', N'loaiKhuyenMai') IS NULL
BEGIN
    ALTER TABLE dbo.KhuyenMai ADD loaiKhuyenMai NVARCHAR(20) NOT NULL
        CONSTRAINT DF_KhuyenMai_LoaiKhuyenMai DEFAULT (N'Coupon');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CHK_KhuyenMai_LoaiKhuyenMai')
BEGIN
    ALTER TABLE dbo.KhuyenMai ADD CONSTRAINT CHK_KhuyenMai_LoaiKhuyenMai
        CHECK (loaiKhuyenMai IN (N'Coupon', N'GiamTrucTiep'));
END
GO

IF COL_LENGTH(N'dbo.ChiTietDonHang', N'giaGoc') IS NULL
BEGIN
    ALTER TABLE dbo.ChiTietDonHang ADD giaGoc DECIMAL(18, 2) NULL;
END
GO

IF COL_LENGTH(N'dbo.ChiTietDonHang', N'maKhuyenMaiTrucTiep') IS NULL
BEGIN
    ALTER TABLE dbo.ChiTietDonHang ADD maKhuyenMaiTrucTiep VARCHAR(20) NULL
        CONSTRAINT FK_ChiTietDonHang_KhuyenMaiTrucTiep REFERENCES dbo.KhuyenMai (maKhuyenMai);
END
GO

PRINT N'021: đã thêm loại khuyến mãi "Giảm trực tiếp" (KhuyenMai.loaiKhuyenMai) và giá gốc / chương trình ở dòng đơn hàng (ChiTietDonHang).';
GO
