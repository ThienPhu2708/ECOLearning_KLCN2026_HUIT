-- =============================================================================
-- 007_GoiY_SoThichNguoiDung.sql  (chạy SAU 005 và 006)
--
-- Xử lý cold-start cho học viên mới: lần đầu vào trang chủ, học viên chọn 1–3 lĩnh vực quan tâm.
-- Mỗi lĩnh vực lấy khóa nổi bật nhất làm "khóa mồi", rồi tra ma trận course_similarity của các khóa mồi
-- để gợi ý (RecommendationService.LayTheoSoThich). Thuật toán lọc cộng tác giữ nguyên.
--
-- Sở thích KHÔNG ghi vào TuongTacKhoaHoc, để không làm sai lệch tín hiệu thật của ma trận.
--
--   SoThichNguoiDung : các lĩnh vực (danh mục) học viên đã chọn
--   KhaoSatSoThich   : học viên đã trả lời hoặc đã bỏ qua bước chọn, để không hỏi lại
--
-- Chỉ tạo bảng mới, không đổi dữ liệu cũ. Chạy lại nhiều lần vẫn an toàn.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF OBJECT_ID(N'dbo.SoThichNguoiDung', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SoThichNguoiDung
    (
        maNguoiDung INT         NOT NULL,
        maDanhMuc   VARCHAR(20) NOT NULL,
        ngayChon    DATETIME    NOT NULL CONSTRAINT DF_SoThichNguoiDung_NgayChon DEFAULT (GETDATE()),
        CONSTRAINT PK_SoThichNguoiDung PRIMARY KEY (maNguoiDung, maDanhMuc),
        CONSTRAINT FK_SoThichNguoiDung_NguoiDung FOREIGN KEY (maNguoiDung) REFERENCES dbo.NguoiDung (maNguoiDung),
        CONSTRAINT FK_SoThichNguoiDung_DanhMuc   FOREIGN KEY (maDanhMuc)   REFERENCES dbo.DanhMuc (maDanhMuc)
    );
END
GO

IF OBJECT_ID(N'dbo.KhaoSatSoThich', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KhaoSatSoThich
    (
        maNguoiDung INT      NOT NULL,
        daBoQua     BIT      NOT NULL CONSTRAINT DF_KhaoSatSoThich_DaBoQua DEFAULT (0),
        ngayCapNhat DATETIME NOT NULL CONSTRAINT DF_KhaoSatSoThich_NgayCapNhat DEFAULT (GETDATE()),
        CONSTRAINT PK_KhaoSatSoThich PRIMARY KEY (maNguoiDung),
        CONSTRAINT FK_KhaoSatSoThich_NguoiDung FOREIGN KEY (maNguoiDung) REFERENCES dbo.NguoiDung (maNguoiDung)
    );
END
GO

PRINT N'007: đã tạo bảng SoThichNguoiDung và KhaoSatSoThich.';
GO
