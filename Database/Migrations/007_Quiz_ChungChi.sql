-- Quiz cuối khóa. Không sửa dữ liệu học tập hiện có. Chạy lại được.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.QuizQuestion') IS NULL
CREATE TABLE dbo.QuizQuestion (
 Id INT IDENTITY PRIMARY KEY, CourseId VARCHAR(20) NOT NULL REFERENCES dbo.KhoaHoc(maKhoaHoc),
 Topic NVARCHAR(120) NOT NULL, Difficulty INT NOT NULL CHECK(Difficulty BETWEEN 1 AND 3),
 Text NVARCHAR(4000) NOT NULL, OptionsJson NVARCHAR(MAX) NOT NULL,
 CorrectOption INT NOT NULL CHECK(CorrectOption BETWEEN 0 AND 3),
 Explanation NVARCHAR(2000) NULL, ShuffleOptions BIT NOT NULL DEFAULT 1,
 Active BIT NOT NULL DEFAULT 1
);
IF OBJECT_ID('dbo.QuizConfig') IS NULL
CREATE TABLE dbo.QuizConfig (
 CourseId VARCHAR(20) PRIMARY KEY REFERENCES dbo.KhoaHoc(maKhoaHoc),
 Title NVARCHAR(200) NOT NULL, Minutes INT NOT NULL CHECK(Minutes BETWEEN 1 AND 180),
 PassPercent INT NOT NULL CHECK(PassPercent BETWEEN 1 AND 100),
 MaxAttempts INT NOT NULL CHECK(MaxAttempts BETWEEN 1 AND 100),
 EasyCount INT NOT NULL CHECK(EasyCount>=0), MediumCount INT NOT NULL CHECK(MediumCount>=0),
 HardCount INT NOT NULL CHECK(HardCount>=0), Topic NVARCHAR(120) NULL,
 RandomSelection BIT NOT NULL, SelectedIdsJson NVARCHAR(MAX) NOT NULL,
 ShuffleQuestions BIT NOT NULL, ShuffleAnswers BIT NOT NULL, Published BIT NOT NULL,
 CHECK(EasyCount+MediumCount+HardCount BETWEEN 1 AND 100)
);
IF OBJECT_ID('dbo.QuizAttempt') IS NULL
CREATE TABLE dbo.QuizAttempt (
 Id INT IDENTITY PRIMARY KEY, CourseId VARCHAR(20) NOT NULL REFERENCES dbo.KhoaHoc(maKhoaHoc),
 StudentId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 StartedAt DATETIME2 NOT NULL, ExpiresAt DATETIME2 NOT NULL, SubmittedAt DATETIME2 NULL,
 SnapshotJson NVARCHAR(MAX) NOT NULL, AnswersJson NVARCHAR(MAX) NULL,
 Total INT NOT NULL, Correct INT NULL, Score DECIMAL(5,2) NULL, Passed BIT NULL,
 PassPercent INT NOT NULL, Title NVARCHAR(200) NOT NULL
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_QuizAttempt_Student' AND object_id=OBJECT_ID('dbo.QuizAttempt'))
CREATE INDEX IX_QuizAttempt_Student ON dbo.QuizAttempt(CourseId,StudentId,SubmittedAt);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_QuizAttempt_Open' AND object_id=OBJECT_ID('dbo.QuizAttempt'))
CREATE UNIQUE INDEX UX_QuizAttempt_Open ON dbo.QuizAttempt(CourseId,StudentId) WHERE SubmittedAt IS NULL;
IF OBJECT_ID('dbo.QuizCertificate') IS NULL
CREATE TABLE dbo.QuizCertificate (
 CourseId VARCHAR(20) NOT NULL REFERENCES dbo.KhoaHoc(maKhoaHoc),
 StudentId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 RequestedAt DATETIME2 NOT NULL, IssuedAt DATETIME2 NULL,
 IssuedBy INT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 Code VARCHAR(50) NOT NULL UNIQUE, PRIMARY KEY(CourseId,StudentId)
);
IF COL_LENGTH('dbo.QuizAttempt','TimedOut') IS NULL
ALTER TABLE dbo.QuizAttempt ADD TimedOut BIT NOT NULL CONSTRAINT DF_QuizAttempt_TimedOut DEFAULT 0;
IF OBJECT_ID('dbo.QuizAttemptGrant') IS NULL
CREATE TABLE dbo.QuizAttemptGrant (
 Id INT IDENTITY PRIMARY KEY, CourseId VARCHAR(20) NOT NULL REFERENCES dbo.KhoaHoc(maKhoaHoc),
 StudentId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 TeacherId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 Reason NVARCHAR(400) NOT NULL, GrantedAt DATETIME2 NOT NULL,
 Quantity INT NOT NULL CONSTRAINT DF_QuizGrant_Quantity DEFAULT 1 CHECK(Quantity=1)
);
COMMIT;
GO
