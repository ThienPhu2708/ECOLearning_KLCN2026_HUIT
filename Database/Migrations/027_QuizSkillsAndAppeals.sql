-- Additive, rerunnable. Deploy after 024-026. No historical grades/certificates changed.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO
IF OBJECT_ID('dbo.QuizSkill','U') IS NULL
CREATE TABLE dbo.QuizSkill(Id INT IDENTITY PRIMARY KEY,CourseId VARCHAR(20) NOT NULL REFERENCES dbo.KhoaHoc(maKhoaHoc),Name NVARCHAR(120) NOT NULL,Active BIT NOT NULL DEFAULT 1,CONSTRAINT UQ_QuizSkill_Name UNIQUE(CourseId,Name));
IF COL_LENGTH('dbo.QuizQuestion','SkillId') IS NULL ALTER TABLE dbo.QuizQuestion ADD SkillId INT NULL REFERENCES dbo.QuizSkill(Id);
IF COL_LENGTH('dbo.QuizConfig','UseMatrix') IS NULL ALTER TABLE dbo.QuizConfig ADD UseMatrix BIT NOT NULL DEFAULT 0;
IF COL_LENGTH('dbo.QuizConfig','MatrixJson') IS NULL ALTER TABLE dbo.QuizConfig ADD MatrixJson NVARCHAR(MAX) NOT NULL DEFAULT N'[]';
IF COL_LENGTH('dbo.QuizAttempt','SkillResultsJson') IS NULL ALTER TABLE dbo.QuizAttempt ADD SkillResultsJson NVARCHAR(MAX) NULL;
GO
IF OBJECT_ID('dbo.QuizCorrection','U') IS NULL
CREATE TABLE dbo.QuizCorrection(Id INT IDENTITY PRIMARY KEY,CourseId VARCHAR(20) NOT NULL REFERENCES dbo.KhoaHoc(maKhoaHoc),QuestionId INT NOT NULL,VersionKey VARCHAR(64) NOT NULL,AcceptedJson NVARCHAR(MAX) NOT NULL,AllCredit BIT NOT NULL,Reason NVARCHAR(2000) NOT NULL,TeacherId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),CONSTRAINT UQ_QuizCorrection_Version UNIQUE(CourseId,QuestionId,VersionKey));
IF OBJECT_ID('dbo.QuizRegradePreview','U') IS NULL
CREATE TABLE dbo.QuizRegradePreview(Id VARCHAR(32) NOT NULL PRIMARY KEY,TeacherId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),CourseId VARCHAR(20) NOT NULL REFERENCES dbo.KhoaHoc(maKhoaHoc),ReportId INT NOT NULL REFERENCES dbo.QuizQuestionReport(Id),PayloadJson NVARCHAR(MAX) NOT NULL,StateHash VARCHAR(64) NOT NULL,ExpiresAt DATETIME NOT NULL,AppliedAt DATETIME NULL);
IF OBJECT_ID('dbo.QuizGradeAudit','U') IS NULL
CREATE TABLE dbo.QuizGradeAudit(Id INT IDENTITY PRIMARY KEY,CorrectionId INT NOT NULL REFERENCES dbo.QuizCorrection(Id),AttemptId INT NOT NULL REFERENCES dbo.QuizAttempt(Id),OldScore DECIMAL(5,2) NOT NULL,CalculatedScore DECIMAL(5,2) NOT NULL,RecognizedScore DECIMAL(5,2) NOT NULL,OldPassed BIT NOT NULL,RecognizedPassed BIT NOT NULL,OldSkills NVARCHAR(MAX) NULL,NewSkills NVARCHAR(MAX) NULL,CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),CONSTRAINT UQ_QuizGradeAudit UNIQUE(CorrectionId,AttemptId));
IF OBJECT_ID('dbo.QuizAppeal','U') IS NULL
CREATE TABLE dbo.QuizAppeal(Id INT IDENTITY PRIMARY KEY,AttemptId INT NOT NULL UNIQUE REFERENCES dbo.QuizAttempt(Id),StudentId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),Content NVARCHAR(2000) NOT NULL,Status VARCHAR(20) NOT NULL DEFAULT 'Pending',CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE());
IF OBJECT_ID('dbo.QuizAppealReply','U') IS NULL
CREATE TABLE dbo.QuizAppealReply(Id INT IDENTITY PRIMARY KEY,AppealId INT NOT NULL REFERENCES dbo.QuizAppeal(Id),TeacherId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),Content NVARCHAR(2000) NOT NULL,Status VARCHAR(20) NOT NULL,CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE());
GO
