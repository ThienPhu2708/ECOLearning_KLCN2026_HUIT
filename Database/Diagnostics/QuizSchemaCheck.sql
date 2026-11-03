-- READ ONLY: select the HOSTING database in SSMS before running.
-- This script does not create, alter, update, or delete any database objects/data.
SELECT DB_NAME() AS DatabaseName,
       CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS SqlVersion,
       compatibility_level AS CompatibilityLevel
FROM sys.databases WHERE name=DB_NAME();

SELECT expected.ObjectName, expected.ObjectType,
       CASE WHEN OBJECT_ID(N'dbo.'+expected.ObjectName,expected.ObjectType) IS NULL THEN N'MISSING' ELSE N'OK' END AS Result
FROM (VALUES
 (N'QuizQuestion',N'U'),(N'QuizConfig',N'U'),(N'QuizAttempt',N'U'),
 (N'QuizCertificate',N'U'),(N'QuizAttemptGrant',N'U'),
 (N'QuizQuestionReport',N'U'),(N'QuizVisibility',N'U'),(N'QuizWriter',N'U'),
 (N'QuizSkill',N'U'),(N'QuizCorrection',N'U'),(N'QuizRegradePreview',N'U'),
 (N'QuizGradeAudit',N'U'),(N'QuizAppeal',N'U'),(N'QuizAppealReply',N'U'),
 (N'QuizIntegrityReview',N'U'),(N'QuizIntegrityEvent',N'U'),
 (N'QuizVisibilitySeen',N'U'),(N'QuizCertificateRevocation',N'U'),
 (N'QuizEffectiveAttempt',N'V'),(N'QuizRecognizedAttempt',N'V'),(N'QuizValidCertificate',N'V')
) expected(ObjectName,ObjectType)
ORDER BY expected.ObjectType,expected.ObjectName;

SELECT expected.ObjectName,expected.ColumnName,
       CASE WHEN COL_LENGTH(N'dbo.'+expected.ObjectName,expected.ColumnName) IS NULL THEN N'MISSING' ELSE N'OK' END AS Result
FROM (VALUES
 (N'QuizQuestion',N'SkillId'),(N'QuizConfig',N'UseMatrix'),(N'QuizConfig',N'MatrixJson'),
 (N'QuizAttempt',N'SkillResultsJson'),(N'QuizAttempt',N'IsVerification'),
 (N'QuizAttempt',N'ReviewRequired'),(N'QuizAttempt',N'ReviewApprovedAt'),
 (N'QuizAttempt',N'ReviewApprovedBy'),(N'QuizAttempt',N'ReviewNote'),
 (N'QuizEffectiveAttempt',N'ReviewRequired'),(N'QuizEffectiveAttempt',N'ReviewApprovedAt')
) expected(ObjectName,ColumnName)
ORDER BY expected.ObjectName,expected.ColumnName;

SELECT s.name AS SchemaName,o.name AS ObjectName,o.type_desc AS ObjectType,o.modify_date AS ModifiedAt
FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id
WHERE o.name LIKE N'Quiz%' AND o.is_ms_shipped=0
ORDER BY o.name;
