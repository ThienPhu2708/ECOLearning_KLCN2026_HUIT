-- 033: Chuẩn hóa thời gian thảo luận về UTC. Chạy sau migration 032.
-- TRƯỚC KHI CHẠY: sao lưu database; xác nhận MỌI dữ liệu cũ trong hai bảng
-- được lưu cùng một múi giờ. Hosting đã kiểm tra là W. Europe Standard Time;
-- local có thể khác. Điền tên múi giờ Windows phù hợp vào biến bên dưới.
-- Nếu dữ liệu được nhập từ nhiều múi giờ hoặc có giờ mơ hồ khi đổi DST,
-- không chạy tự động: cần kiểm tra từng bản ghi trước.
-- Không dùng USE cố định: tự chọn đúng database trước khi chạy.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @LegacyTimeZone sysname = NULL; -- Hosting: N'W. Europe Standard Time'
IF @LegacyTimeZone IS NULL OR NOT EXISTS (SELECT 1 FROM sys.time_zone_info WHERE name = @LegacyTimeZone)
    THROW 51000, N'Hãy chỉ định múi giờ Windows của dữ liệu cũ trước khi chạy migration 033.', 1;
IF OBJECT_ID(N'dbo.ThaoLuanBaiHoc', N'U') IS NULL OR OBJECT_ID(N'dbo.YeuThichThaoLuanBaiHoc', N'U') IS NULL
    THROW 51001, N'Cần chạy migration 032 trước.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'ECOLearning.LessonDiscussionUtc033', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
    IF @LockResult < 0 THROW 51002, N'Không thể khóa migration 033.', 1;

    IF OBJECT_ID(N'dbo.LessonDiscussionUtc033Marker', N'U') IS NOT NULL
    BEGIN
        COMMIT TRANSACTION;
        PRINT N'Migration 033 đã được áp dụng; không quy đổi lại.';
        RETURN;
    END;
    IF OBJECT_ID(N'dbo.LessonDiscussionUtc033Backup', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.LessonDiscussionLikeUtc033Backup', N'U') IS NOT NULL
        THROW 51003, N'Đã có bảng sao lưu 033 nhưng thiếu marker. Dừng để kiểm tra thủ công.', 1;

    -- Bản sao giữ nguyên thời gian gốc để kiểm tra/khôi phục khi cần.
    SELECT maThaoLuan, ngayTao, ngayCapNhat INTO dbo.LessonDiscussionUtc033Backup FROM dbo.ThaoLuanBaiHoc;
    SELECT maThaoLuan, maNguoiDung, ngayTao INTO dbo.LessonDiscussionLikeUtc033Backup FROM dbo.YeuThichThaoLuanBaiHoc;

    UPDATE dbo.ThaoLuanBaiHoc SET
        ngayTao = CONVERT(datetime, (ngayTao AT TIME ZONE @LegacyTimeZone) AT TIME ZONE 'UTC'),
        ngayCapNhat = CASE WHEN ngayCapNhat IS NULL THEN NULL ELSE CONVERT(datetime, (ngayCapNhat AT TIME ZONE @LegacyTimeZone) AT TIME ZONE 'UTC') END;
    UPDATE dbo.YeuThichThaoLuanBaiHoc SET
        ngayTao = CONVERT(datetime, (ngayTao AT TIME ZONE @LegacyTimeZone) AT TIME ZONE 'UTC');

    DECLARE @DefaultName sysname, @Sql nvarchar(max);
    SELECT @DefaultName=dc.name FROM sys.default_constraints dc JOIN sys.columns c ON c.default_object_id=dc.object_id
        WHERE c.object_id=OBJECT_ID(N'dbo.ThaoLuanBaiHoc') AND c.name=N'ngayTao';
    IF @DefaultName IS NOT NULL BEGIN
        SET @Sql=N'ALTER TABLE dbo.ThaoLuanBaiHoc DROP CONSTRAINT '+QUOTENAME(@DefaultName); EXEC sys.sp_executesql @Sql;
    END;
    ALTER TABLE dbo.ThaoLuanBaiHoc ADD CONSTRAINT DF_ThaoLuanBaiHoc_NgayTao DEFAULT (GETUTCDATE()) FOR ngayTao;
    SET @DefaultName=NULL;
    SELECT @DefaultName=dc.name FROM sys.default_constraints dc JOIN sys.columns c ON c.default_object_id=dc.object_id
        WHERE c.object_id=OBJECT_ID(N'dbo.YeuThichThaoLuanBaiHoc') AND c.name=N'ngayTao';
    IF @DefaultName IS NOT NULL BEGIN
        SET @Sql=N'ALTER TABLE dbo.YeuThichThaoLuanBaiHoc DROP CONSTRAINT '+QUOTENAME(@DefaultName); EXEC sys.sp_executesql @Sql;
    END;
    ALTER TABLE dbo.YeuThichThaoLuanBaiHoc ADD CONSTRAINT DF_YeuThichThaoLuan_NgayTao DEFAULT (GETUTCDATE()) FOR ngayTao;

    CREATE TABLE dbo.LessonDiscussionUtc033Marker (appliedAtUtc datetime NOT NULL, legacyTimeZone sysname NOT NULL);
    INSERT INTO dbo.LessonDiscussionUtc033Marker(appliedAtUtc, legacyTimeZone) VALUES(GETUTCDATE(), @LegacyTimeZone);
    COMMIT TRANSACTION;
    PRINT N'Migration 033 hoàn tất. Hai bảng backup giữ các mốc giờ ban đầu.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
