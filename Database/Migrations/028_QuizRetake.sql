-- Apply after 027. Preserves every historical score and certificate.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO
IF COL_LENGTH('dbo.QuizAttempt','IsVerification') IS NULL
 ALTER TABLE dbo.QuizAttempt ADD IsVerification BIT NOT NULL CONSTRAINT DF_QuizAttempt_IsVerification DEFAULT 0;
GO
IF OBJECT_ID('dbo.QuizIntegrityReview','U') IS NULL
BEGIN
 CREATE TABLE dbo.QuizIntegrityReview(
  Id INT IDENTITY PRIMARY KEY,
  AttemptId INT NOT NULL UNIQUE REFERENCES dbo.QuizAttempt(Id),
  Status VARCHAR(24) NOT NULL DEFAULT 'NeedReview' CHECK(Status IN ('AwaitingTeacher','NeedReview')),
  Followup VARCHAR(12) NOT NULL DEFAULT 'Retake' CHECK(Followup='Retake'),
  RetakeAttemptId INT NULL REFERENCES dbo.QuizAttempt(Id),
  CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
  UpdatedAt DATETIME NOT NULL DEFAULT GETUTCDATE()
 );
 CREATE UNIQUE INDEX UX_QuizIntegrityReview_Retake ON dbo.QuizIntegrityReview(RetakeAttemptId) WHERE RetakeAttemptId IS NOT NULL;
END;
GO
IF OBJECT_ID('dbo.QuizIntegrityEvent','U') IS NULL
BEGIN
 CREATE TABLE dbo.QuizIntegrityEvent(
  Id INT IDENTITY PRIMARY KEY,
  ReviewId INT NOT NULL REFERENCES dbo.QuizIntegrityReview(Id),
  AuthorId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
  Kind VARCHAR(24) NOT NULL CHECK(Kind IN ('Decision','RetakeStarted')),
  Content NVARCHAR(2000) NOT NULL,
  CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE()
 );
 CREATE INDEX IX_QuizIntegrityEvent_Review ON dbo.QuizIntegrityEvent(ReviewId,Id);
END;
GO
