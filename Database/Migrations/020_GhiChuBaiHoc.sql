-- =============================================================================
-- 020_GhiChuBaiHoc.sql
--
-- Ghi chú bài học của học viên ("Khóa học của tôi" > học bài > Ghi chú bài học). Trước đây web lưu ghi chú trong localStorage của
-- trình duyệt nên app và web không thấy ghi chú của nhau; nay lưu trên máy chủ, web (LearningController) và API ứng dụng
-- (/api/v1/learning/{courseId}/notes, /api/v1/notes/{id}) dùng chung Services/LessonNoteService.cs.
--   thoiDiemGiay: mốc thời gian của video lúc ghi chú (giây), bấm vào ghi chú để tua tới mốc này.
--   Bài học bị xóa thì ghi chú của bài cũng bị xóa (ON DELETE CASCADE, giống TienDoBaiHoc / TaiNguyen).
-- Chỉ tạo bảng mới, không sửa dữ liệu cũ. Chạy lại nhiều lần vẫn an toàn.
-- Trên hosting: bỏ dòng USE bên dưới (CSDL hosting có tên khác), chọn đúng CSDL rồi chạy.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF OBJECT_ID(N'dbo.GhiChuBaiHoc', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GhiChuBaiHoc (
        maGhiChu     INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_GhiChuBaiHoc PRIMARY KEY,
        maHocVien    INT NOT NULL CONSTRAINT FK_GhiChuBaiHoc_NguoiDung REFERENCES dbo.NguoiDung (maNguoiDung),
        maBaiHoc     VARCHAR(20) NOT NULL CONSTRAINT FK_GhiChuBaiHoc_BaiHoc REFERENCES dbo.BaiHoc (maBaiHoc) ON DELETE CASCADE,
        thoiDiemGiay INT NOT NULL CONSTRAINT DF_GhiChuBaiHoc_ThoiDiem DEFAULT (0)
                     CONSTRAINT CHK_GhiChuBaiHoc_ThoiDiem CHECK (thoiDiemGiay >= 0),
        noiDung      NVARCHAR(1000) NOT NULL CONSTRAINT CHK_GhiChuBaiHoc_NoiDung CHECK (LEN(noiDung) > 0),
        ngayTao      DATETIME NOT NULL CONSTRAINT DF_GhiChuBaiHoc_NgayTao DEFAULT (GETDATE()),
        ngayCapNhat  DATETIME NULL
    );
    CREATE INDEX IX_GhiChuBaiHoc_HocVien_BaiHoc ON dbo.GhiChuBaiHoc (maHocVien, maBaiHoc, thoiDiemGiay);
END
GO

PRINT N'020: đã tạo bảng GhiChuBaiHoc (ghi chú bài học đồng bộ giữa web và ứng dụng).';
GO
