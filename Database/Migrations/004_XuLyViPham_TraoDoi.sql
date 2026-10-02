-- =============================================================================
-- 004_XuLyViPham_TraoDoi.sql   (chạy SAU 002 và 003)
-- Mục đích: Giáo viên xử lý khóa học bị tạm khóa và trao đổi với Admin
--   1. XuLyViPham.trangThaiXuLy : bước hiện tại của một lần Tạm khóa
--        N'Chờ GV xử lý' -> N'Chờ Admin xem xét' -> (Admin yêu cầu sửa thêm) -> N'Chờ GV xử lý' ...
--        N'Đã kết thúc' khi Admin mở lại hoặc khóa vĩnh viễn
--   2. PhanHoiViPham            : lịch sử trao đổi trong một lần Tạm khóa
--        loai: N'Đã chỉnh sửa' | N'Khiếu nại' (giáo viên) | N'Yêu cầu sửa thêm' (Admin)
-- Không xóa / sửa dữ liệu cũ, chỉ bổ sung. Chạy lại nhiều lần vẫn an toàn.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

-- 1. Bước xử lý của lần Tạm khóa
IF COL_LENGTH(N'dbo.XuLyViPham', N'trangThaiXuLy') IS NULL
BEGIN
    ALTER TABLE dbo.XuLyViPham ADD trangThaiXuLy NVARCHAR(50) NULL;
END
GO

-- Gán bước cho các lần Tạm khóa đã có
UPDATE x
SET trangThaiXuLy = CASE
        WHEN k.trangThai <> N'Tạm khóa' THEN N'Đã kết thúc'
        WHEN x.phanHoiGiaoVien IS NOT NULL THEN N'Chờ Admin xem xét'
        ELSE N'Chờ GV xử lý'
    END
FROM dbo.XuLyViPham x
INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = x.maKhoaHoc
WHERE x.hanhDong = N'Tạm khóa' AND x.trangThaiXuLy IS NULL;
GO

-- Chỉ lần Tạm khóa MỚI NHẤT của khóa học đang tạm khóa còn mở; các lần cũ hơn coi như đã kết thúc
UPDATE x
SET trangThaiXuLy = N'Đã kết thúc'
FROM dbo.XuLyViPham x
WHERE x.hanhDong = N'Tạm khóa'
  AND x.trangThaiXuLy <> N'Đã kết thúc'
  AND EXISTS (SELECT 1 FROM dbo.XuLyViPham n
              WHERE n.maKhoaHoc = x.maKhoaHoc AND n.hanhDong = N'Tạm khóa' AND n.ngayXuLy > x.ngayXuLy);
GO

-- 2. Lịch sử trao đổi
IF OBJECT_ID(N'dbo.PhanHoiViPham', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PhanHoiViPham (
        maPhanHoi      VARCHAR(30)    NOT NULL CONSTRAINT PK_PhanHoiViPham PRIMARY KEY,
        maXuLy         VARCHAR(30)    NOT NULL,      -- lần Tạm khóa mà trao đổi này thuộc về
        maNguoiGui     INT            NOT NULL,
        vaiTroGui      NVARCHAR(20)   NOT NULL,      -- N'GiaoVien' | N'Admin'
        loai           NVARCHAR(50)   NOT NULL,
        noiDung        NVARCHAR(2000) NOT NULL,
        hanPhanHoiMoi  DATETIME       NULL,          -- hạn mới khi Admin yêu cầu sửa thêm
        ngayGui        DATETIME       NOT NULL CONSTRAINT DF_PhanHoiViPham_NgayGui DEFAULT (GETDATE()),

        CONSTRAINT FK_PhanHoiViPham_XuLy      FOREIGN KEY (maXuLy)     REFERENCES dbo.XuLyViPham(maXuLy),
        CONSTRAINT FK_PhanHoiViPham_NguoiDung FOREIGN KEY (maNguoiGui) REFERENCES dbo.NguoiDung(maNguoiDung),
        CONSTRAINT CHK_PhanHoiViPham_VaiTro CHECK (vaiTroGui IN (N'GiaoVien', N'Admin')),
        CONSTRAINT CHK_PhanHoiViPham_Loai   CHECK (loai IN (N'Đã chỉnh sửa', N'Khiếu nại', N'Yêu cầu sửa thêm'))
    );

    CREATE INDEX IX_PhanHoiViPham_XuLy ON dbo.PhanHoiViPham (maXuLy, ngayGui);
END
GO
