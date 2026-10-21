-- 016: Admin quản lý người dùng, tài khoản giáo viên và giám sát Marketing.
--   1. NguoiDung       : ngày tạo, thông tin khóa tài khoản (lý do, thời điểm, hạn khóa, Admin khóa),
--                        cờ buộc đổi mật khẩu (mật khẩu tạm do hệ thống cấp cho nhân viên mới / Admin đặt lại).
--   2. NhatKyQuanTri   : nhật ký thao tác của Admin (khóa, mở khóa, cảnh cáo, tạo nhân viên, đặt lại mật khẩu, tạm dừng khuyến mãi).
--   3. KhoaHocTamAn    : khóa học đang bán bị ẩn vì giáo viên bị khóa, lưu trạng thái cũ để khôi phục khi mở khóa.
--   4. KhuyenMai       : Admin tạm dừng chương trình (Marketing không tự kích hoạt lại được).
-- Chỉ thêm cột / bảng, không sửa dữ liệu cũ. Chạy lại được.
-- Trên hosting: bỏ dòng USE bên dưới (CSDL hosting có tên khác), chọn đúng CSDL rồi chạy.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

-- 1. NguoiDung
IF COL_LENGTH('dbo.NguoiDung', 'ngayTao') IS NULL
    ALTER TABLE dbo.NguoiDung ADD ngayTao DATETIME NULL CONSTRAINT DF_NguoiDung_NgayTao DEFAULT (GETDATE());
IF COL_LENGTH('dbo.NguoiDung', 'lyDoKhoa') IS NULL
    ALTER TABLE dbo.NguoiDung ADD lyDoKhoa NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.NguoiDung', 'ngayKhoa') IS NULL
    ALTER TABLE dbo.NguoiDung ADD ngayKhoa DATETIME NULL;
IF COL_LENGTH('dbo.NguoiDung', 'khoaDenNgay') IS NULL
    ALTER TABLE dbo.NguoiDung ADD khoaDenNgay DATETIME NULL;          -- NULL khi đang khóa = khóa vĩnh viễn
IF COL_LENGTH('dbo.NguoiDung', 'maAdminKhoa') IS NULL
    ALTER TABLE dbo.NguoiDung ADD maAdminKhoa INT NULL;
IF COL_LENGTH('dbo.NguoiDung', 'canDoiMatKhau') IS NULL
    ALTER TABLE dbo.NguoiDung ADD canDoiMatKhau BIT NOT NULL CONSTRAINT DF_NguoiDung_CanDoiMatKhau DEFAULT (0);
GO

-- 2. Nhật ký thao tác quản trị
IF OBJECT_ID('dbo.NhatKyQuanTri') IS NULL
CREATE TABLE dbo.NhatKyQuanTri (
    maNhatKy INT IDENTITY(1, 1) CONSTRAINT PK_NhatKyQuanTri PRIMARY KEY,
    maAdmin INT NULL CONSTRAINT FK_NhatKyQuanTri_Admin REFERENCES dbo.NguoiDung (maNguoiDung),   -- NULL: hệ thống tự làm (hết hạn khóa)
    hanhDong VARCHAR(30) NOT NULL,          -- KhoaTaiKhoan / MoKhoa / CanhCao / TaoNhanVien / DatLaiMatKhau / TamDungKhuyenMai / ChoChayKhuyenMai
    loaiDoiTuong VARCHAR(20) NOT NULL,      -- NguoiDung / KhuyenMai
    maDoiTuong VARCHAR(30) NOT NULL,
    lyDo NVARCHAR(1000) NULL,
    ngayTao DATETIME NOT NULL CONSTRAINT DF_NhatKyQuanTri_NgayTao DEFAULT (GETDATE())
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NhatKyQuanTri_DoiTuong' AND object_id = OBJECT_ID('dbo.NhatKyQuanTri'))
    CREATE INDEX IX_NhatKyQuanTri_DoiTuong ON dbo.NhatKyQuanTri (loaiDoiTuong, maDoiTuong, ngayTao DESC);
GO

-- 3. Khóa học ẩn theo giáo viên bị khóa
IF OBJECT_ID('dbo.KhoaHocTamAn') IS NULL
CREATE TABLE dbo.KhoaHocTamAn (
    maKhoaHoc VARCHAR(20) NOT NULL CONSTRAINT PK_KhoaHocTamAn PRIMARY KEY
        CONSTRAINT FK_KhoaHocTamAn_KhoaHoc REFERENCES dbo.KhoaHoc (maKhoaHoc),
    maGiaoVien INT NOT NULL,
    trangThaiTruoc NVARCHAR(100) NOT NULL,
    ngayAn DATETIME NOT NULL CONSTRAINT DF_KhoaHocTamAn_NgayAn DEFAULT (GETDATE())
);
GO

-- 4. Admin tạm dừng chương trình khuyến mãi
IF COL_LENGTH('dbo.KhuyenMai', 'trangThai') IS NULL
    RAISERROR(N'Cần chạy 008_Marketing_KhuyenMai.sql trước phần 4.', 16, 1);
GO
IF COL_LENGTH('dbo.KhuyenMai', 'trangThai') IS NOT NULL AND COL_LENGTH('dbo.KhuyenMai', 'adminTamDung') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD adminTamDung BIT NOT NULL CONSTRAINT DF_KhuyenMai_AdminTamDung DEFAULT (0);
IF COL_LENGTH('dbo.KhuyenMai', 'trangThai') IS NOT NULL AND COL_LENGTH('dbo.KhuyenMai', 'lyDoAdminTamDung') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD lyDoAdminTamDung NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.KhuyenMai', 'trangThai') IS NOT NULL AND COL_LENGTH('dbo.KhuyenMai', 'ngayAdminTamDung') IS NULL
    ALTER TABLE dbo.KhuyenMai ADD ngayAdminTamDung DATETIME NULL;
GO

PRINT N'016: đã bổ sung cột khóa tài khoản cho NguoiDung, bảng NhatKyQuanTri, KhoaHocTamAn và cột Admin tạm dừng khuyến mãi.';
