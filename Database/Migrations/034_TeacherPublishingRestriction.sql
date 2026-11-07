-- Hạn chế đăng khóa học mới; thời gian lưu UTC, không thay đổi trạng thái tài khoản hoặc khóa học.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.TeacherPublishingRestriction', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TeacherPublishingRestriction (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TeacherPublishingRestriction PRIMARY KEY,
        TeacherId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
        CreatedBy INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
        StartedAtUtc DATETIME2 NOT NULL,
        EndsAtUtc DATETIME2 NULL,
        Reason NVARCHAR(500) NOT NULL,
        LiftedAtUtc DATETIME2 NULL,
        LiftedBy INT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
        LiftReason NVARCHAR(500) NULL
    );
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TeacherPublishingRestriction') AND name=N'UX_TeacherPublishingRestriction_Open')
    CREATE UNIQUE INDEX UX_TeacherPublishingRestriction_Open ON dbo.TeacherPublishingRestriction(TeacherId) WHERE LiftedAtUtc IS NULL;
COMMIT TRANSACTION;
