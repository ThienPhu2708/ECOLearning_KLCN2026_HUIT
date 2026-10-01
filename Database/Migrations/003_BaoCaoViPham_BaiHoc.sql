-- =============================================================================
-- 003_BaoCaoViPham_BaiHoc.sql   (chạy SAU 002_BaoCaoViPham.sql)
-- Mục đích: Học viên báo cáo vi phạm từ trang học sẽ ghi lại bài học đang xem,
--           để quản trị viên biết chính xác bài học bị báo cáo.
--   BaoCaoViPham.maBaiHoc : có thể để trống (báo cáo cho toàn khóa học)
--   Không đặt khóa ngoại để giáo viên vẫn xóa / sửa bài học được.
-- Chỉ thêm cột, không sửa / xóa dữ liệu cũ. Chạy lại nhiều lần vẫn an toàn.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF COL_LENGTH(N'dbo.BaoCaoViPham', N'maBaiHoc') IS NULL
BEGIN
    ALTER TABLE dbo.BaoCaoViPham ADD maBaiHoc VARCHAR(20) NULL;
END
GO
