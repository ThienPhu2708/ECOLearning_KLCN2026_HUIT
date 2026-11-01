-- Teacher-specific read markers. Never deletes or changes visibility events.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO
IF OBJECT_ID('dbo.QuizVisibilitySeen','U') IS NULL
CREATE TABLE dbo.QuizVisibilitySeen(
 TeacherId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 AttemptId INT NOT NULL REFERENCES dbo.QuizAttempt(Id),
 SeenEventId INT NOT NULL,
 SeenAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
 CONSTRAINT PK_QuizVisibilitySeen PRIMARY KEY(TeacherId,AttemptId)
);
GO
