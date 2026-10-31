-- Three recorded departures require a teacher decision before result recognition.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO
IF COL_LENGTH('dbo.QuizAttempt','ReviewRequired') IS NULL
 ALTER TABLE dbo.QuizAttempt ADD ReviewRequired BIT NOT NULL CONSTRAINT DF_QuizAttempt_ReviewRequired DEFAULT 0,
 ReviewApprovedAt DATETIME NULL, ReviewApprovedBy INT NULL REFERENCES dbo.NguoiDung(maNguoiDung), ReviewNote NVARCHAR(2000) NULL;
GO
UPDATE a SET ReviewRequired=1 FROM dbo.QuizAttempt a WHERE a.SubmittedAt IS NOT NULL AND a.ReviewRequired=0 AND (SELECT COUNT(*) FROM dbo.QuizVisibility v WHERE v.AttemptId=a.Id)>=3;
GO
EXEC sp_refreshview N'dbo.QuizEffectiveAttempt';
GO
CREATE OR ALTER VIEW dbo.QuizRecognizedAttempt AS
SELECT a.* FROM dbo.QuizEffectiveAttempt a WHERE a.ReviewRequired=0 OR a.ReviewApprovedAt IS NOT NULL;
GO
CREATE OR ALTER VIEW dbo.QuizValidCertificate AS
SELECT c.* FROM dbo.QuizCertificate c WHERE c.IssuedAt IS NOT NULL AND NOT EXISTS(
 SELECT 1 FROM dbo.QuizEffectiveAttempt a WHERE a.CourseId=c.CourseId AND a.StudentId=c.StudentId AND a.SubmittedAt IS NOT NULL AND a.ReviewRequired=1 AND a.ReviewApprovedAt IS NULL);
GO
