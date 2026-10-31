-- Báo lỗi câu hỏi: lưu bản câu hỏi của lượt thi, không sửa điểm hay lượt thi.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO
IF OBJECT_ID('dbo.QuizQuestionReport','U') IS NULL
CREATE TABLE dbo.QuizQuestionReport (
 Id INT IDENTITY PRIMARY KEY,
 AttemptId INT NOT NULL REFERENCES dbo.QuizAttempt(Id),
 QuestionId INT NOT NULL,
 StudentId INT NOT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 CourseId VARCHAR(20) NOT NULL REFERENCES dbo.KhoaHoc(maKhoaHoc),
 SnapshotJson NVARCHAR(MAX) NOT NULL,
 Reason NVARCHAR(80) NOT NULL,
 Content NVARCHAR(2000) NOT NULL,
 Status VARCHAR(20) NOT NULL DEFAULT 'Pending',
 Response NVARCHAR(2000) NULL,
 TeacherId INT NULL REFERENCES dbo.NguoiDung(maNguoiDung),
 CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
 UpdatedAt DATETIME NULL,
 CONSTRAINT UQ_QuizQuestionReport_AttemptQuestion UNIQUE(AttemptId,QuestionId),
 CONSTRAINT CK_QuizQuestionReport_Status CHECK(Status IN ('Pending','Processing','Corrected','Answered'))
);
GO
