-- =============================================================================
-- 018_Marketing_DacTa.sql  (chạy SAU 008, 009)
--
-- Phân hệ Marketing theo đặc tả nghiệp vụ đã chốt (Đặc tả Marketing ECOLearning, mục 3–7). Giữ tên cột của 008/009,
-- chỉ bổ sung phần còn thiếu:
--   1. KhuyenMai: phạm vi theo danh mục (phamVi = DanhMuc + maDanhMucApDung).
--   2. MaGiamGia.gioiHanMoiHocVien cho phép NULL = không giới hạn số lần mỗi học viên.
--   3. ChienDich: kênh triển khai, ngân sách, chi phí thực tế, chỉ tiêu doanh thu / lượt dùng, ghi chú (tính ROI, % đạt chỉ tiêu);
--      5 trạng thái KeHoach / DangTrienKhai / TamDung / HoanThanh / DaHuy (chiến dịch tạm dừng / đã hủy thì mã thuộc
--      chiến dịch không dùng được). Dữ liệu cũ: HoatDong -> DangTrienKhai (chưa tới ngày bắt đầu -> KeHoach), KetThuc -> HoanThanh.
-- Giá khuyến mãi theo từng khóa (KhoaHoc.giaKhuyenMai) không còn được đọc ở đâu; cột giữ nguyên, không xóa dữ liệu.
-- Chỉ thêm cột / nới ràng buộc, không xóa dữ liệu. Chạy lại nhiều lần vẫn an toàn.
-- Trên hosting: bỏ dòng USE bên dưới (CSDL hosting có tên khác), chọn đúng CSDL rồi chạy.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF OBJECT_ID(N'dbo.ChienDich', N'U') IS NULL OR COL_LENGTH(N'dbo.KhuyenMai', N'phamVi') IS NULL OR OBJECT_ID(N'dbo.ViMaGiamGia', N'U') IS NULL
BEGIN
    RAISERROR(N'Cần chạy 008_Marketing_KhuyenMai.sql và 009_ViMaGiamGia.sql trước.', 16, 1);
    SET NOEXEC ON;
END
GO

-- 1. Phạm vi theo danh mục ---------------------------------------------------------
IF COL_LENGTH(N'dbo.KhuyenMai', N'maDanhMucApDung') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD maDanhMucApDung VARCHAR(20) NULL
        CONSTRAINT FK_KhuyenMai_DanhMucApDung FOREIGN KEY REFERENCES dbo.DanhMuc (maDanhMuc);
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CHK_KhuyenMai_PhamVi' AND definition LIKE N'%DanhMuc%')
BEGIN
    IF OBJECT_ID(N'dbo.CHK_KhuyenMai_PhamVi', N'C') IS NOT NULL ALTER TABLE dbo.KhuyenMai DROP CONSTRAINT CHK_KhuyenMai_PhamVi;
    ALTER TABLE dbo.KhuyenMai ADD CONSTRAINT CHK_KhuyenMai_PhamVi CHECK (phamVi IN (N'TatCa', N'DanhMuc', N'KhoaHoc'));
END
GO
IF OBJECT_ID(N'dbo.CHK_KhuyenMai_DanhMucApDung', N'C') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD CONSTRAINT CHK_KhuyenMai_DanhMucApDung CHECK (phamVi <> N'DanhMuc' OR maDanhMucApDung IS NOT NULL);
GO

-- 2. Số lượt mỗi học viên: NULL = không giới hạn -----------------------------------------
IF COLUMNPROPERTY(OBJECT_ID(N'dbo.MaGiamGia'), N'gioiHanMoiHocVien', 'AllowsNull') = 0
BEGIN
    IF OBJECT_ID(N'dbo.CHK_MaGiamGia_GioiHanMoiHocVien', N'C') IS NOT NULL ALTER TABLE dbo.MaGiamGia DROP CONSTRAINT CHK_MaGiamGia_GioiHanMoiHocVien;
    ALTER TABLE dbo.MaGiamGia ALTER COLUMN gioiHanMoiHocVien INT NULL;
    ALTER TABLE dbo.MaGiamGia ADD CONSTRAINT CHK_MaGiamGia_GioiHanMoiHocVien CHECK (gioiHanMoiHocVien IS NULL OR gioiHanMoiHocVien > 0);
END
GO

-- 3. Chiến dịch --------------------------------------------------------------------
IF COL_LENGTH(N'dbo.ChienDich', N'kenhTrienKhai') IS NULL
    ALTER TABLE dbo.ChienDich ADD kenhTrienKhai NVARCHAR(300) NULL;
IF COL_LENGTH(N'dbo.ChienDich', N'nganSach') IS NULL
    ALTER TABLE dbo.ChienDich ADD nganSach DECIMAL(18, 2) NOT NULL CONSTRAINT DF_ChienDich_NganSach DEFAULT (0)
        CONSTRAINT CHK_ChienDich_NganSach CHECK (nganSach >= 0);
IF COL_LENGTH(N'dbo.ChienDich', N'chiPhiThucTe') IS NULL
    ALTER TABLE dbo.ChienDich ADD chiPhiThucTe DECIMAL(18, 2) NOT NULL CONSTRAINT DF_ChienDich_ChiPhiThucTe DEFAULT (0)
        CONSTRAINT CHK_ChienDich_ChiPhiThucTe CHECK (chiPhiThucTe >= 0);
IF COL_LENGTH(N'dbo.ChienDich', N'chiTieuDoanhThu') IS NULL
    ALTER TABLE dbo.ChienDich ADD chiTieuDoanhThu DECIMAL(18, 2) NULL
        CONSTRAINT CHK_ChienDich_ChiTieuDoanhThu CHECK (chiTieuDoanhThu IS NULL OR chiTieuDoanhThu >= 0);
IF COL_LENGTH(N'dbo.ChienDich', N'chiTieuLuotDung') IS NULL
    ALTER TABLE dbo.ChienDich ADD chiTieuLuotDung INT NULL
        CONSTRAINT CHK_ChienDich_ChiTieuLuotDung CHECK (chiTieuLuotDung IS NULL OR chiTieuLuotDung >= 0);
IF COL_LENGTH(N'dbo.ChienDich', N'ghiChu') IS NULL
    ALTER TABLE dbo.ChienDich ADD ghiChu NVARCHAR(1000) NULL;
GO
-- Độ dài theo đặc tả (chỉ nới rộng)
IF COL_LENGTH(N'dbo.ChienDich', N'tenChienDich') < 400 ALTER TABLE dbo.ChienDich ALTER COLUMN tenChienDich NVARCHAR(200) NOT NULL;
IF COL_LENGTH(N'dbo.ChienDich', N'mucTieu') < 2000 ALTER TABLE dbo.ChienDich ALTER COLUMN mucTieu NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.ChienDich', N'doiTuong') < 600 ALTER TABLE dbo.ChienDich ALTER COLUMN doiTuong NVARCHAR(300) NULL;
GO

-- 5 trạng thái chiến dịch
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CHK_ChienDich_TrangThai' AND definition LIKE N'%DangTrienKhai%')
BEGIN
    IF OBJECT_ID(N'dbo.CHK_ChienDich_TrangThai', N'C') IS NOT NULL ALTER TABLE dbo.ChienDich DROP CONSTRAINT CHK_ChienDich_TrangThai;
    IF OBJECT_ID(N'dbo.DF_ChienDich_TrangThai', N'D') IS NOT NULL ALTER TABLE dbo.ChienDich DROP CONSTRAINT DF_ChienDich_TrangThai;

    UPDATE dbo.ChienDich SET trangThai = CASE WHEN ngayBatDau > GETDATE() THEN N'KeHoach' ELSE N'DangTrienKhai' END WHERE trangThai = N'HoatDong';
    UPDATE dbo.ChienDich SET trangThai = N'HoanThanh' WHERE trangThai = N'KetThuc';

    ALTER TABLE dbo.ChienDich ADD CONSTRAINT DF_ChienDich_TrangThai DEFAULT (N'KeHoach') FOR trangThai;
    ALTER TABLE dbo.ChienDich ADD CONSTRAINT CHK_ChienDich_TrangThai
        CHECK (trangThai IN (N'KeHoach', N'DangTrienKhai', N'TamDung', N'HoanThanh', N'DaHuy'));
END
GO

PRINT N'018: đã bổ sung phạm vi theo danh mục, số lượt mỗi học viên không giới hạn, các trường và 5 trạng thái của chiến dịch.';
GO
SET NOEXEC OFF;
GO
