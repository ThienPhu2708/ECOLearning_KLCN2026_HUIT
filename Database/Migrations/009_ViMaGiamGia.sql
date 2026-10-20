-- =============================================================================
-- 009_ViMaGiamGia.sql  (chạy SAU 008)
--
-- Ví mã giảm giá của học viên:
--   1. ViMaGiamGia : mã học viên đã "lưu" (thu thập) ở trang chủ / trang khóa học / trang Mã giảm giá của tôi.
--                    Giỏ hàng chỉ gợi ý các mã trong ví. Mã đã lưu KHÔNG xóa được (không có thao tác hoàn tác).
--   2. KhuyenMai.chinhSach : mô tả chính sách Marketing viết cho học viên (hiển thị ở trang chi tiết mã).
--   3. Đưa các mã học viên đã từng dùng (SuDungUuDai) vào ví để trang "Mã giảm giá của tôi" có đủ lịch sử.
--
-- Chỉ thêm bảng / cột, không xóa dữ liệu. Chạy lại nhiều lần vẫn an toàn.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF OBJECT_ID(N'dbo.MaGiamGia', N'U') IS NULL OR OBJECT_ID(N'dbo.SuDungUuDai', N'U') IS NULL
BEGIN
    RAISERROR(N'Cần chạy 008_Marketing_KhuyenMai.sql trước.', 16, 1);
    SET NOEXEC ON;
END
GO

-- 1. Ví mã giảm giá -------------------------------------------------------------
IF OBJECT_ID(N'dbo.ViMaGiamGia', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ViMaGiamGia
    (
        maHocVien INT         NOT NULL,
        maGiamGia VARCHAR(20) NOT NULL,
        ngayLuu   DATETIME    NOT NULL CONSTRAINT DF_ViMaGiamGia_NgayLuu DEFAULT (GETDATE()),
        CONSTRAINT PK_ViMaGiamGia PRIMARY KEY (maHocVien, maGiamGia),
        CONSTRAINT FK_ViMaGiamGia_NguoiDung FOREIGN KEY (maHocVien) REFERENCES dbo.NguoiDung (maNguoiDung),
        CONSTRAINT FK_ViMaGiamGia_MaGiamGia FOREIGN KEY (maGiamGia) REFERENCES dbo.MaGiamGia (maGiamGia)
    );
END
GO

-- 2. Chính sách hiển thị cho học viên --------------------------------------------
IF COL_LENGTH(N'dbo.KhuyenMai', N'chinhSach') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD chinhSach NVARCHAR(1000) NULL;
GO

-- 3. Mã đã dùng trước khi có ví -> đưa vào ví -------------------------------------
INSERT INTO dbo.ViMaGiamGia (maHocVien, maGiamGia, ngayLuu)
SELECT s.maHocVien, s.maGiamGia, MIN(s.ngaySuDung)
FROM dbo.SuDungUuDai s
WHERE NOT EXISTS (SELECT 1 FROM dbo.ViMaGiamGia v WHERE v.maHocVien = s.maHocVien AND v.maGiamGia = s.maGiamGia)
GROUP BY s.maHocVien, s.maGiamGia;
GO

PRINT N'009: đã tạo ViMaGiamGia và cột KhuyenMai.chinhSach.';
GO
SET NOEXEC OFF;
GO
