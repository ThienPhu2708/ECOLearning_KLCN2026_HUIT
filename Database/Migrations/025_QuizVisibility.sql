-- Browser visibility signals are advisory, never a grading rule.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO
IF OBJECT_ID('dbo.QuizVisibility','U') IS NULL
CREATE TABLE dbo.QuizVisibility(
 Id INT IDENTITY PRIMARY KEY,
 AttemptId INT NOT NULL REFERENCES dbo.QuizAttempt(Id),
 EventKey VARCHAR(64) NOT NULL,
 LeftAt DATETIME NOT NULL,
 ReturnedAt DATETIME NULL,
 CONSTRAINT UQ_QuizVisibility_Event UNIQUE(AttemptId,EventKey)
);
GO
