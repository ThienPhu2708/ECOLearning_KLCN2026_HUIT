-- A requested retake supersedes all earlier results in that course.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO
CREATE OR ALTER VIEW dbo.QuizEffectiveAttempt AS
SELECT a.* FROM dbo.QuizAttempt a
OUTER APPLY (
 SELECT TOP 1 r.RetakeAttemptId FROM dbo.QuizIntegrityReview r
 JOIN dbo.QuizAttempt original ON original.Id=r.AttemptId
 WHERE original.CourseId=a.CourseId AND original.StudentId=a.StudentId AND r.Followup='Retake'
 ORDER BY r.Id DESC
) latest
WHERE (NOT EXISTS(SELECT 1 FROM dbo.QuizIntegrityReview r JOIN dbo.QuizAttempt original ON original.Id=r.AttemptId WHERE original.CourseId=a.CourseId AND original.StudentId=a.StudentId AND r.Followup='Retake') AND a.IsVerification=0)
 OR (latest.RetakeAttemptId IS NOT NULL AND a.Id>=latest.RetakeAttemptId);
GO
IF OBJECT_ID('dbo.QuizCertificateRevocation','U') IS NULL
 CREATE TABLE dbo.QuizCertificateRevocation(
 Id INT IDENTITY PRIMARY KEY, ReviewId INT NOT NULL UNIQUE REFERENCES dbo.QuizIntegrityReview(Id),
 CourseId NVARCHAR(50) NOT NULL, StudentId INT NOT NULL, Code NVARCHAR(100) NOT NULL,
 IssuedAt DATETIME NOT NULL, RevokedAt DATETIME NOT NULL DEFAULT GETUTCDATE());
GO
-- Existing requests follow the same replacement rule. Keep the old issuance in audit history.
INSERT dbo.QuizCertificateRevocation(ReviewId,CourseId,StudentId,Code,IssuedAt)
SELECT latest.Id,c.CourseId,c.StudentId,c.Code,c.IssuedAt FROM dbo.QuizCertificate c
CROSS APPLY(SELECT TOP 1 r.Id,r.CreatedAt FROM dbo.QuizIntegrityReview r JOIN dbo.QuizAttempt a ON a.Id=r.AttemptId WHERE a.CourseId=c.CourseId AND a.StudentId=c.StudentId AND r.Followup='Retake' ORDER BY r.Id DESC) latest
WHERE c.IssuedAt IS NOT NULL AND c.IssuedAt<=latest.CreatedAt AND NOT EXISTS(SELECT 1 FROM dbo.QuizCertificateRevocation v WHERE v.ReviewId=latest.Id);
UPDATE c SET IssuedAt=NULL,IssuedBy=NULL FROM dbo.QuizCertificate c
JOIN dbo.QuizCertificateRevocation v ON v.CourseId=c.CourseId AND v.StudentId=c.StudentId AND v.Code=c.Code AND v.IssuedAt=c.IssuedAt;
GO
