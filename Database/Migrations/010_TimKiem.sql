-- =============================================================================
-- 010_TimKiem.sql  (độc lập, chạy sau 009)
--
-- Gợi ý từ khóa khi gõ ở thanh tìm kiếm (Services/SearchService.cs):
--   TuKhoaTimKiem : thống kê gộp các từ khóa người dùng đã tìm (cả khách chưa đăng nhập).
--                   Chỉ lưu từ khóa và số lượt, KHÔNG lưu ai tìm (không có maNguoiDung) để không giữ dữ liệu cá nhân.
--                   Mỗi phiên chỉ cộng 1 lượt cho một từ khóa; chỉ gợi ý lại từ khóa có kết quả (soKetQua > 0).
--
-- Chỉ thêm bảng, không đụng dữ liệu cũ. Chạy lại nhiều lần vẫn an toàn.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF OBJECT_ID(N'dbo.TuKhoaTimKiem', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TuKhoaTimKiem
    (
        -- Dạng chuẩn hóa không dấu, chữ thường, một khoảng trắng: "lap trinh web" (khóa chính, gộp "Lập trình  Web" và "lap trinh web")
        tuKhoaKhongDau NVARCHAR(100) NOT NULL,
        -- Dạng hiển thị (giữ dấu) của lần tìm gần nhất: "lập trình web"
        tuKhoa         NVARCHAR(100) NOT NULL,
        soLuot         INT           NOT NULL CONSTRAINT DF_TuKhoaTimKiem_SoLuot DEFAULT (1),
        soKetQua       INT           NOT NULL CONSTRAINT DF_TuKhoaTimKiem_SoKetQua DEFAULT (0),
        lanDauTim      DATETIME      NOT NULL CONSTRAINT DF_TuKhoaTimKiem_LanDau DEFAULT (GETDATE()),
        lanCuoiTim     DATETIME      NOT NULL CONSTRAINT DF_TuKhoaTimKiem_LanCuoi DEFAULT (GETDATE()),
        CONSTRAINT PK_TuKhoaTimKiem PRIMARY KEY (tuKhoaKhongDau),
        CONSTRAINT CHK_TuKhoaTimKiem_SoLuot CHECK (soLuot > 0 AND soKetQua >= 0)
    );

    -- Gợi ý theo độ phổ biến
    CREATE INDEX IX_TuKhoaTimKiem_SoLuot ON dbo.TuKhoaTimKiem (soLuot DESC) INCLUDE (tuKhoa, soKetQua);
END
GO

PRINT N'010: đã tạo TuKhoaTimKiem.';
GO
