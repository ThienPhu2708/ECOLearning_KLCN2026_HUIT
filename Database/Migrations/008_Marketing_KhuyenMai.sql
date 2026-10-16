-- =============================================================================
-- 008_Marketing_KhuyenMai.sql  (chạy SAU 007)
--
-- Hoàn thiện phân hệ Marketing theo đặc tả DacTa_LuongChucNang_Marketing_ECOLearning.md:
--   1. ChienDich      : chiến dịch Marketing, gắn được nhiều chương trình khuyến mãi.
--   2. KhuyenMai      : thêm cột cấu hình (loại ưu đãi, giảm tối đa, đơn tối thiểu, phạm vi khóa học,
--                       đối tượng tất cả / học viên thân thiết + tiêu chí, trạng thái, chiến dịch,
--                       công khai mã cho học viên hay không).
--      mucUuDai giữ ý nghĩa "giá trị ưu đãi": % (1–100) khi loaiUuDai = PhanTram, số tiền VNĐ khi = SoTien.
--   3. MaGiamGia      : thêm giới hạn tổng lượt dùng và số lượt mỗi học viên.
--   4. SuDungUuDai    : lịch sử mỗi lần dùng coupon trên một đơn hàng đã thanh toán (đối soát, giới hạn, báo cáo).
--   5. trg_ChiTietDonHang_CapNhatTongTien: tổng tiền đơn = tổng đơn giá - tiền giảm đã ghi nhận
--      (trước đây trigger ghi đè tongTien = SUM(donGia), làm mất số tiền giảm của coupon).
--
-- Chỉ thêm bảng / cột (có giá trị mặc định) và sửa trigger, không xóa dữ liệu. Chạy lại nhiều lần vẫn an toàn.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

-- 1. Chiến dịch Marketing ------------------------------------------------------
IF OBJECT_ID(N'dbo.ChienDich', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChienDich
    (
        maChienDich         VARCHAR(20)    NOT NULL,
        tenChienDich        NVARCHAR(150)  NOT NULL,
        mucTieu             NVARCHAR(500)  NULL,
        noiDung             NVARCHAR(2000) NULL,
        doiTuong            NVARCHAR(255)  NULL,
        ngayBatDau          DATETIME       NOT NULL,
        ngayKetThuc         DATETIME       NOT NULL,
        trangThai           NVARCHAR(20)   NOT NULL CONSTRAINT DF_ChienDich_TrangThai DEFAULT (N'HoatDong'),
        maNhanVienMarketing INT            NOT NULL,
        ngayTao             DATETIME       NOT NULL CONSTRAINT DF_ChienDich_NgayTao DEFAULT (GETDATE()),
        CONSTRAINT PK_ChienDich PRIMARY KEY (maChienDich),
        CONSTRAINT FK_ChienDich_NguoiDung FOREIGN KEY (maNhanVienMarketing) REFERENCES dbo.NguoiDung (maNguoiDung),
        CONSTRAINT CHK_ChienDich_ThoiGian CHECK (ngayKetThuc >= ngayBatDau),
        CONSTRAINT CHK_ChienDich_TrangThai CHECK (trangThai IN (N'HoatDong', N'KetThuc'))
    );
END
GO

-- 2. Cấu hình chương trình khuyến mãi ------------------------------------------
IF COL_LENGTH(N'dbo.KhuyenMai', N'loaiUuDai') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD loaiUuDai NVARCHAR(20) NOT NULL
        CONSTRAINT DF_KhuyenMai_LoaiUuDai DEFAULT (N'PhanTram')
        CONSTRAINT CHK_KhuyenMai_LoaiUuDai CHECK (loaiUuDai IN (N'PhanTram', N'SoTien'));
GO
IF COL_LENGTH(N'dbo.KhuyenMai', N'giamToiDa') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD giamToiDa DECIMAL(18, 2) NULL
        CONSTRAINT CHK_KhuyenMai_GiamToiDa CHECK (giamToiDa IS NULL OR giamToiDa > 0);
GO
IF COL_LENGTH(N'dbo.KhuyenMai', N'donHangToiThieu') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD donHangToiThieu DECIMAL(18, 2) NULL
        CONSTRAINT CHK_KhuyenMai_DonHangToiThieu CHECK (donHangToiThieu IS NULL OR donHangToiThieu >= 0);
GO
IF COL_LENGTH(N'dbo.KhuyenMai', N'phamVi') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD phamVi NVARCHAR(20) NOT NULL
        CONSTRAINT DF_KhuyenMai_PhamVi DEFAULT (N'TatCa')
        CONSTRAINT CHK_KhuyenMai_PhamVi CHECK (phamVi IN (N'TatCa', N'KhoaHoc'));
GO
IF COL_LENGTH(N'dbo.KhuyenMai', N'doiTuong') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD doiTuong NVARCHAR(20) NOT NULL
        CONSTRAINT DF_KhuyenMai_DoiTuong DEFAULT (N'TatCa')
        CONSTRAINT CHK_KhuyenMai_DoiTuong CHECK (doiTuong IN (N'TatCa', N'ThanThiet'));
GO
IF COL_LENGTH(N'dbo.KhuyenMai', N'soKhoaToiThieu') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD soKhoaToiThieu INT NULL
        CONSTRAINT CHK_KhuyenMai_SoKhoaToiThieu CHECK (soKhoaToiThieu IS NULL OR soKhoaToiThieu > 0);
GO
IF COL_LENGTH(N'dbo.KhuyenMai', N'chiTieuToiThieu') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD chiTieuToiThieu DECIMAL(18, 2) NULL
        CONSTRAINT CHK_KhuyenMai_ChiTieuToiThieu CHECK (chiTieuToiThieu IS NULL OR chiTieuToiThieu > 0);
GO
IF COL_LENGTH(N'dbo.KhuyenMai', N'trangThai') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD trangThai NVARCHAR(20) NOT NULL
        CONSTRAINT DF_KhuyenMai_TrangThai DEFAULT (N'HoatDong')
        CONSTRAINT CHK_KhuyenMai_TrangThai CHECK (trangThai IN (N'HoatDong', N'TamNgung', N'KetThuc'));
GO
IF COL_LENGTH(N'dbo.KhuyenMai', N'maChienDich') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD maChienDich VARCHAR(20) NULL
        CONSTRAINT FK_KhuyenMai_ChienDich FOREIGN KEY REFERENCES dbo.ChienDich (maChienDich);
GO
IF COL_LENGTH(N'dbo.KhuyenMai', N'ngayTao') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD ngayTao DATETIME NOT NULL
        CONSTRAINT DF_KhuyenMai_NgayTao DEFAULT (GETDATE());
GO
-- 1 = học viên thấy mã ở giỏ hàng / trang khóa học; 0 = mã kín, Marketing gửi riêng
IF COL_LENGTH(N'dbo.KhuyenMai', N'congKhai') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD congKhai BIT NOT NULL
        CONSTRAINT DF_KhuyenMai_CongKhai DEFAULT (1);
GO

-- Chương trình cũ đang tạm ngưng bằng cách tắt coupon -> ghi vào cột trạng thái mới
UPDATE km SET trangThai = N'TamNgung'
FROM dbo.KhuyenMai km
WHERE km.trangThai = N'HoatDong'
  AND km.ngayKetThuc >= GETDATE()
  AND EXISTS (SELECT 1 FROM dbo.MaGiamGia mg WHERE mg.maKhuyenMai = km.maKhuyenMai AND mg.trangThai = 0);
GO

-- 3. Giới hạn lượt dùng coupon -------------------------------------------------
IF COL_LENGTH(N'dbo.MaGiamGia', N'gioiHanLuotDung') IS NULL
    ALTER TABLE dbo.MaGiamGia ADD gioiHanLuotDung INT NULL
        CONSTRAINT CHK_MaGiamGia_GioiHanLuotDung CHECK (gioiHanLuotDung IS NULL OR gioiHanLuotDung > 0);
GO
IF COL_LENGTH(N'dbo.MaGiamGia', N'gioiHanMoiHocVien') IS NULL
    ALTER TABLE dbo.MaGiamGia ADD gioiHanMoiHocVien INT NOT NULL
        CONSTRAINT DF_MaGiamGia_GioiHanMoiHocVien DEFAULT (1)
        CONSTRAINT CHK_MaGiamGia_GioiHanMoiHocVien CHECK (gioiHanMoiHocVien > 0);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_MaGiamGia_MaCode' AND object_id = OBJECT_ID(N'dbo.MaGiamGia'))
   AND NOT EXISTS (SELECT maCode FROM dbo.MaGiamGia GROUP BY maCode HAVING COUNT(*) > 1)
   AND NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                   JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                   WHERE i.object_id = OBJECT_ID(N'dbo.MaGiamGia') AND i.is_unique = 1 AND c.name = N'maCode')
    CREATE UNIQUE INDEX UQ_MaGiamGia_MaCode ON dbo.MaGiamGia (maCode);
GO

-- 4. Lịch sử sử dụng ưu đãi ----------------------------------------------------
IF OBJECT_ID(N'dbo.SuDungUuDai', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SuDungUuDai
    (
        maSuDung      INT IDENTITY(1, 1) NOT NULL,
        maDonHang     VARCHAR(20)    NOT NULL,
        maKhuyenMai   VARCHAR(20)    NOT NULL,
        maGiamGia     VARCHAR(20)    NOT NULL,
        maHocVien     INT            NOT NULL,
        tienTruocGiam DECIMAL(18, 2) NOT NULL,
        tienGiam      DECIMAL(18, 2) NOT NULL,
        ngaySuDung    DATETIME       NOT NULL CONSTRAINT DF_SuDungUuDai_NgaySuDung DEFAULT (GETDATE()),
        CONSTRAINT PK_SuDungUuDai PRIMARY KEY (maSuDung),
        -- Mỗi đơn hàng chỉ áp dụng một ưu đãi (không cộng dồn nhiều coupon)
        CONSTRAINT UQ_SuDungUuDai_DonHang UNIQUE (maDonHang),
        CONSTRAINT FK_SuDungUuDai_DonHang   FOREIGN KEY (maDonHang)   REFERENCES dbo.DonHang (maDonHang),
        CONSTRAINT FK_SuDungUuDai_KhuyenMai FOREIGN KEY (maKhuyenMai) REFERENCES dbo.KhuyenMai (maKhuyenMai),
        CONSTRAINT FK_SuDungUuDai_MaGiamGia FOREIGN KEY (maGiamGia)   REFERENCES dbo.MaGiamGia (maGiamGia),
        CONSTRAINT FK_SuDungUuDai_NguoiDung FOREIGN KEY (maHocVien)   REFERENCES dbo.NguoiDung (maNguoiDung),
        CONSTRAINT CHK_SuDungUuDai_TienGiam CHECK (tienGiam >= 0 AND tienGiam <= tienTruocGiam)
    );
    CREATE INDEX IX_SuDungUuDai_MaGiamGia ON dbo.SuDungUuDai (maGiamGia, maHocVien);
END
GO

-- 5. Tổng tiền đơn hàng = tổng đơn giá - tiền giảm đã ghi nhận ----------------
CREATE OR ALTER TRIGGER dbo.trg_ChiTietDonHang_CapNhatTongTien
ON dbo.ChiTietDonHang
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @DonHangAffected TABLE (maDonHang VARCHAR(20));
    INSERT INTO @DonHangAffected (maDonHang)
    SELECT maDonHang FROM inserted
    UNION
    SELECT maDonHang FROM deleted;

    UPDATE dh
    SET tongTien = CASE WHEN tong.tien - ISNULL(sd.tienGiam, 0) < 0 THEN 0 ELSE tong.tien - ISNULL(sd.tienGiam, 0) END
    FROM dbo.DonHang dh
    JOIN @DonHangAffected a ON dh.maDonHang = a.maDonHang
    CROSS APPLY (SELECT ISNULL(SUM(ct.donGia), 0) AS tien FROM dbo.ChiTietDonHang ct WHERE ct.maDonHang = dh.maDonHang) tong
    LEFT JOIN dbo.SuDungUuDai sd ON sd.maDonHang = dh.maDonHang;
END;
GO

PRINT N'008: đã tạo ChienDich, SuDungUuDai, bổ sung cấu hình KhuyenMai / MaGiamGia và sửa trigger tổng tiền đơn hàng.';
GO
