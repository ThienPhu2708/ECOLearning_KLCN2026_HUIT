-- =============================================================================
-- 024_Chat_HoiThoai_TinNhan.sql
--
-- Tính năng nhắn tin Real-time giữa Học viên và Giáo viên phụ trách khóa học.
-- Quy tắc nghiệp vụ:
--   1. Chỉ giữa học viên đã mua khóa và giáo viên phụ trách khóa học đó.
--   2. Mỗi cặp (maKhoaHoc, maHocVien) là một hội thoại duy nhất.
--   3. Giáo viên KHÔNG nhắn trước. Học viên gửi tin nhắn đầu tiên thì hội thoại
--      mới được khởi tạo và giáo viên mới được quyền trả lời.
--   4. Mốc đọc (hocVienDocDen, giaoVienDocDen) lưu maTinNhan đã xem đến, giúp
--      đếm tin chưa đọc siêu nhanh mà không cần cờ daXem trên từng tin.
--   5. Khóa vĩnh viễn/tạm khóa: trạng thái chuyển sang 'ChiDoc'.
--   6. Thu hồi tin nhắn: cờ daThuHoi (cho phép trong 5 phút đầu).
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF OBJECT_ID(N'dbo.HoiThoai', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HoiThoai (
        maHoiThoai     INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_HoiThoai PRIMARY KEY,
        maKhoaHoc      VARCHAR(20) NOT NULL CONSTRAINT FK_HoiThoai_KhoaHoc REFERENCES dbo.KhoaHoc (maKhoaHoc),
        maHocVien      INT NOT NULL CONSTRAINT FK_HoiThoai_HocVien REFERENCES dbo.NguoiDung (maNguoiDung),
        maGiaoVien     INT NOT NULL CONSTRAINT FK_HoiThoai_GiaoVien REFERENCES dbo.NguoiDung (maNguoiDung),
        ngayTao        DATETIME NOT NULL CONSTRAINT DF_HoiThoai_NgayTao DEFAULT (GETDATE()),
        tinCuoiLuc     DATETIME NULL,
        tinCuoiTomTat  NVARCHAR(200) NULL,
        hocVienDocDen  INT NOT NULL CONSTRAINT DF_HoiThoai_HVDocDen DEFAULT (0),
        giaoVienDocDen INT NOT NULL CONSTRAINT DF_HoiThoai_GVDocDen DEFAULT (0),
        trangThai      NVARCHAR(20) NOT NULL CONSTRAINT DF_HoiThoai_TrangThai DEFAULT (N'Mo'), -- 'Mo', 'ChiDoc', 'BiBaoCao'
        CONSTRAINT UQ_HoiThoai_KhoaHoc_HocVien UNIQUE (maKhoaHoc, maHocVien)
    );

    CREATE INDEX IX_HoiThoai_GiaoVien ON dbo.HoiThoai (maGiaoVien, tinCuoiLuc DESC);
    CREATE INDEX IX_HoiThoai_HocVien ON dbo.HoiThoai (maHocVien, tinCuoiLuc DESC);
END
GO

IF OBJECT_ID(N'dbo.TinNhan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TinNhan (
        maTinNhan   INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_TinNhan PRIMARY KEY,
        maHoiThoai  INT NOT NULL CONSTRAINT FK_TinNhan_HoiThoai REFERENCES dbo.HoiThoai (maHoiThoai) ON DELETE CASCADE,
        maNguoiGui  INT NOT NULL CONSTRAINT FK_TinNhan_NguoiGui REFERENCES dbo.NguoiDung (maNguoiDung),
        noiDung     NVARCHAR(2000) NOT NULL CONSTRAINT CHK_TinNhan_NoiDung CHECK (LEN(noiDung) > 0),
        ngayGui     DATETIME NOT NULL CONSTRAINT DF_TinNhan_NgayGui DEFAULT (GETDATE()),
        daThuHoi    BIT NOT NULL CONSTRAINT DF_TinNhan_DaThuHoi DEFAULT (0)
    );

    CREATE INDEX IX_TinNhan_HoiThoai_Id ON dbo.TinNhan (maHoiThoai, maTinNhan DESC);
END
GO

PRINT N'024: Đã tạo bảng HoiThoai và TinNhan cho hệ thống chat Real-time Học viên - Giáo viên.';
GO

