-- Published course edits are isolated from the live curriculum until approval.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO
IF OBJECT_ID('dbo.CourseRevision') IS NULL
CREATE TABLE dbo.CourseRevision (
 CourseId VARCHAR(20) NOT NULL PRIMARY KEY REFERENCES dbo.KhoaHoc(maKhoaHoc),
 TeacherId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 Payload NVARCHAR(MAX) NOT NULL,
 Status VARCHAR(16) NOT NULL CHECK (Status IN ('Draft','Pending','Rejected','Approved')),
 Version VARCHAR(32) NOT NULL,
 UpdatedAt DATETIME NOT NULL,
 SubmittedAt DATETIME NULL,
 ReviewedAt DATETIME NULL,
 AdminId INT NULL,
 Reason NVARCHAR(2000) NULL
);
GO
IF OBJECT_ID('dbo.CourseCompletionProtection') IS NULL
CREATE TABLE dbo.CourseCompletionProtection (
 CourseId VARCHAR(20) NOT NULL REFERENCES dbo.KhoaHoc(maKhoaHoc),
 StudentId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 CompletedAt DATETIME NOT NULL,
 CONSTRAINT PK_CourseCompletionProtection PRIMARY KEY(CourseId,StudentId)
);
GO
IF OBJECT_ID('dbo.CourseCertificateSnapshot') IS NULL
CREATE TABLE dbo.CourseCertificateSnapshot (
 CourseId VARCHAR(20) NOT NULL REFERENCES dbo.KhoaHoc(maKhoaHoc),
 StudentId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 CourseTitle NVARCHAR(500) NOT NULL,
 Instructor NVARCHAR(500) NULL,
 StudentName NVARCHAR(500) NULL,
 DurationSeconds INT NOT NULL,
 CONSTRAINT PK_CourseCertificateSnapshot PRIMARY KEY(CourseId,StudentId)
);
GO
