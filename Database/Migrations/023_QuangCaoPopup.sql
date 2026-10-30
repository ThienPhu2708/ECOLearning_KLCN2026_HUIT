-- =============================================================================
-- 023_QuangCaoPopup.sql
--
-- Ảnh quảng cáo cho chiến dịch và chương trình khuyến mãi (kể cả ưu đãi thân thiết), hiện dạng popup ở trang chủ.
-- Bấm vào ảnh thì mở trang liệt kê các ưu đãi của chiến dịch / chương trình đó (Controllers/UuDaiController.cs).
--   ChienDich.anhQuangCao, KhuyenMai.anhQuangCao : đường dẫn ảnh (assets/images/promotions/...), NULL = không có ảnh.
--   ChienDich.hienPopup,  KhuyenMai.hienPopup    : 1 = hiện popup quảng cáo trong thời gian chạy (mặc định 0).
-- Chỉ thêm cột, không sửa dữ liệu cũ. Chạy lại nhiều lần vẫn an toàn.
-- Chạy sau 008, 018 (bảng ChienDich, KhuyenMai). Thiếu file này thì các trang Marketing vẫn dùng bình thường,
-- chỉ không có mục ảnh quảng cáo.
-- Trên hosting: bỏ dòng USE bên dưới (CSDL hosting có tên khác), chọn đúng CSDL rồi chạy.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF COL_LENGTH(N'dbo.ChienDich', N'anhQuangCao') IS NULL
    ALTER TABLE dbo.ChienDich ADD anhQuangCao NVARCHAR(500) NULL;
GO
IF COL_LENGTH(N'dbo.ChienDich', N'hienPopup') IS NULL
    ALTER TABLE dbo.ChienDich ADD hienPopup BIT NOT NULL CONSTRAINT DF_ChienDich_HienPopup DEFAULT (0);
GO
IF COL_LENGTH(N'dbo.KhuyenMai', N'anhQuangCao') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD anhQuangCao NVARCHAR(500) NULL;
GO
IF COL_LENGTH(N'dbo.KhuyenMai', N'hienPopup') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD hienPopup BIT NOT NULL CONSTRAINT DF_KhuyenMai_HienPopup DEFAULT (0);
GO
