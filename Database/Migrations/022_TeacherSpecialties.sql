-- Approved teaching categories; do not infer permissions from legacy free text.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO
IF OBJECT_ID('dbo.TeacherSpecialty') IS NULL
CREATE TABLE dbo.TeacherSpecialty (
 TeacherId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 CategoryId VARCHAR(20) NOT NULL REFERENCES dbo.DanhMuc(maDanhMuc),
 ApprovedBy INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 ApprovedAt DATETIME NOT NULL DEFAULT GETDATE(),
 CONSTRAINT PK_TeacherSpecialty PRIMARY KEY(TeacherId,CategoryId)
);
GO
IF OBJECT_ID('dbo.TeacherApplicationSpecialty') IS NULL
CREATE TABLE dbo.TeacherApplicationSpecialty (
 ApplicationId VARCHAR(80) NOT NULL,
 CategoryId VARCHAR(20) NOT NULL REFERENCES dbo.DanhMuc(maDanhMuc),
 CONSTRAINT PK_TeacherApplicationSpecialty PRIMARY KEY(ApplicationId,CategoryId)
);
GO
IF OBJECT_ID('dbo.TeacherSpecialtyRequest') IS NULL
CREATE TABLE dbo.TeacherSpecialtyRequest (
 Id INT IDENTITY PRIMARY KEY, TeacherId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 Categories NVARCHAR(MAX) NOT NULL, EvidenceUrl NVARCHAR(500) NOT NULL,
 Description NVARCHAR(2000) NOT NULL, Status VARCHAR(20) NOT NULL DEFAULT 'Pending',
 CreatedAt DATETIME NOT NULL DEFAULT GETDATE(), ReviewedAt DATETIME NULL,
 ReviewedBy INT NULL REFERENCES dbo.NguoiDung(maNguoiDung), Reason NVARCHAR(2000) NULL
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_TeacherSpecialtyRequest_Pending' AND object_id=OBJECT_ID('dbo.TeacherSpecialtyRequest'))
CREATE UNIQUE INDEX UX_TeacherSpecialtyRequest_Pending ON dbo.TeacherSpecialtyRequest(TeacherId) WHERE Status='Pending';
GO

IF COL_LENGTH('dbo.TeacherSpecialtyRequest','ApprovedCategories') IS NULL
 ALTER TABLE dbo.TeacherSpecialtyRequest ADD ApprovedCategories NVARCHAR(MAX) NULL;
GO

-- Existing teachers keep their previous access. Seed this cohort once only.
IF OBJECT_ID('dbo.TeacherSpecialtyPolicy') IS NULL
BEGIN
 CREATE TABLE dbo.TeacherSpecialtyPolicy (
  TeacherId INT NOT NULL PRIMARY KEY REFERENCES dbo.NguoiDung(maNguoiDung),
  LegacyAccess BIT NOT NULL DEFAULT 0
 );
 INSERT dbo.TeacherSpecialtyPolicy(TeacherId,LegacyAccess)
 SELECT u.maNguoiDung,1 FROM dbo.NguoiDung u
 WHERE u.vaiTro IN (N'GiaoVien',N'Giáo viên')
 AND NOT EXISTS(SELECT 1 FROM dbo.TeacherSpecialty s WHERE s.TeacherId=u.maNguoiDung)
 AND NOT EXISTS(SELECT 1 FROM dbo.DangKyGiaoVien d JOIN dbo.TeacherApplicationSpecialty s ON s.ApplicationId=d.maDon WHERE d.maNguoiDung=u.maNguoiDung);
END;
GO
