-- =============================================================================
-- 032_LessonDiscussion.sql
-- Thảo luận nhiều người theo từng bài học. Không thay đổi HoiThoai/TinNhan
-- (chức năng nhắn tin 1-1 cũ vẫn được giữ nguyên dữ liệu).
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF OBJECT_ID(N'dbo.ThaoLuanBaiHoc', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ThaoLuanBaiHoc (
        maThaoLuan       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ThaoLuanBaiHoc PRIMARY KEY,
        maBaiHoc         VARCHAR(20) NOT NULL CONSTRAINT FK_ThaoLuanBaiHoc_BaiHoc
                            REFERENCES dbo.BaiHoc(maBaiHoc),
        maNguoiDung      INT NOT NULL CONSTRAINT FK_ThaoLuanBaiHoc_NguoiDung
                            REFERENCES dbo.NguoiDung(maNguoiDung),
        maThaoLuanCha    INT NULL,
        noiDung          NVARCHAR(2000) NOT NULL,
        ngayTao          DATETIME NOT NULL CONSTRAINT DF_ThaoLuanBaiHoc_NgayTao DEFAULT (GETDATE()),
        ngayCapNhat      DATETIME NULL,
        trangThai        NVARCHAR(20) NOT NULL CONSTRAINT DF_ThaoLuanBaiHoc_TrangThai DEFAULT (N'HienThi'),
        CONSTRAINT CK_ThaoLuanBaiHoc_NoiDung CHECK (LEN(LTRIM(RTRIM(noiDung))) > 0),
        CONSTRAINT CK_ThaoLuanBaiHoc_TrangThai CHECK (trangThai IN (N'HienThi', N'An', N'DaXoa')),
        CONSTRAINT FK_ThaoLuanBaiHoc_Cha FOREIGN KEY (maThaoLuanCha)
            REFERENCES dbo.ThaoLuanBaiHoc(maThaoLuan)
    );

    CREATE INDEX IX_ThaoLuanBaiHoc_BaiHoc_Ngay
        ON dbo.ThaoLuanBaiHoc(maBaiHoc, trangThai, ngayTao DESC);
    CREATE INDEX IX_ThaoLuanBaiHoc_Cha
        ON dbo.ThaoLuanBaiHoc(maThaoLuanCha, ngayTao);
END
GO

IF OBJECT_ID(N'dbo.YeuThichThaoLuanBaiHoc', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.YeuThichThaoLuanBaiHoc (
        maThaoLuan       INT NOT NULL,
        maNguoiDung      INT NOT NULL,
        ngayTao          DATETIME NOT NULL CONSTRAINT DF_YeuThichThaoLuan_NgayTao DEFAULT (GETDATE()),
        CONSTRAINT PK_YeuThichThaoLuanBaiHoc PRIMARY KEY (maThaoLuan, maNguoiDung),
        CONSTRAINT FK_YeuThichThaoLuan_ThaoLuan FOREIGN KEY (maThaoLuan)
            REFERENCES dbo.ThaoLuanBaiHoc(maThaoLuan) ON DELETE CASCADE,
        CONSTRAINT FK_YeuThichThaoLuan_NguoiDung FOREIGN KEY (maNguoiDung)
            REFERENCES dbo.NguoiDung(maNguoiDung)
    );

    CREATE INDEX IX_YeuThichThaoLuan_NguoiDung
        ON dbo.YeuThichThaoLuanBaiHoc(maNguoiDung, ngayTao DESC);
END
GO

PRINT N'032: Da tao bang thao luan theo bai hoc va yeu thich binh luan.';
GO
