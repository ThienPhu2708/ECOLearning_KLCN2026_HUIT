chịn-- =============================================================================
-- 011_DanhGia_HuuIch_PhanHoi.sql  (độc lập, chạy sau 010)
--
-- Phần "Đánh giá từ học viên" ở trang chi tiết khóa học (Services/CourseReviewService.cs):
--   1. DanhGiaHuuIch  : "Bạn thấy hữu ích?" — mỗi người dùng chọn Hữu ích (1) hoặc Không hữu ích (0) cho một đánh giá,
--                       bấm lại để bỏ chọn. Không tự chấm đánh giá của chính mình.
--   2. PhanHoiDanhGia : phản hồi của giáo viên dạy khóa học đó cho một đánh giá (mỗi đánh giá tối đa 1 phản hồi, sửa / xóa được).
--
-- ON DELETE CASCADE: xóa đánh giá (vd. Database/Seed/99_xoa_du_lieu_demo.sql) thì xóa luôn bình chọn / phản hồi đi kèm.
-- Chỉ thêm bảng, không đụng dữ liệu cũ. Chạy lại nhiều lần vẫn an toàn.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF OBJECT_ID(N'dbo.DanhGiaHuuIch', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DanhGiaHuuIch
    (
        maDanhGia   VARCHAR(20) NOT NULL,
        maNguoiDung INT         NOT NULL,
        huuIch      BIT         NOT NULL,       -- 1 = hữu ích, 0 = không hữu ích
        ngayCapNhat DATETIME    NOT NULL CONSTRAINT DF_DanhGiaHuuIch_Ngay DEFAULT (GETDATE()),
        CONSTRAINT PK_DanhGiaHuuIch PRIMARY KEY (maDanhGia, maNguoiDung),
        CONSTRAINT FK_DanhGiaHuuIch_DanhGia FOREIGN KEY (maDanhGia) REFERENCES dbo.DanhGiaKhoaHoc (maDanhGia) ON DELETE CASCADE,
        CONSTRAINT FK_DanhGiaHuuIch_NguoiDung FOREIGN KEY (maNguoiDung) REFERENCES dbo.NguoiDung (maNguoiDung)
    );
END
GO

IF OBJECT_ID(N'dbo.PhanHoiDanhGia', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PhanHoiDanhGia
    (
        maDanhGia   VARCHAR(20)    NOT NULL,
        maGiaoVien  INT            NOT NULL,
        noiDung     NVARCHAR(1000) NOT NULL,
        ngayTao     DATETIME       NOT NULL CONSTRAINT DF_PhanHoiDanhGia_NgayTao DEFAULT (GETDATE()),
        ngayCapNhat DATETIME       NULL,
        CONSTRAINT PK_PhanHoiDanhGia PRIMARY KEY (maDanhGia),
        CONSTRAINT FK_PhanHoiDanhGia_DanhGia FOREIGN KEY (maDanhGia) REFERENCES dbo.DanhGiaKhoaHoc (maDanhGia) ON DELETE CASCADE,
        CONSTRAINT FK_PhanHoiDanhGia_GiaoVien FOREIGN KEY (maGiaoVien) REFERENCES dbo.NguoiDung (maNguoiDung),
        CONSTRAINT CHK_PhanHoiDanhGia_NoiDung CHECK (LEN(LTRIM(RTRIM(noiDung))) > 0)
    );
END
GO

PRINT N'011: đã tạo DanhGiaHuuIch, PhanHoiDanhGia.';
GO
