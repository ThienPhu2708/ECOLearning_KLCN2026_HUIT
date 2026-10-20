-- =============================================================================
-- 002_BaoCaoViPham.sql
-- Mục đích: Chức năng Báo cáo vi phạm khóa học (hậu kiểm nội dung).
--   - BaoCaoViPham : học viên báo cáo nội dung không phù hợp / sai sót của khóa học
--   - XuLyViPham   : lịch sử Admin xử lý (bác bỏ, tạm khóa, mở lại, khóa vĩnh viễn)
--                    và phản hồi của giáo viên khi khóa học bị tạm khóa
-- Trạng thái khóa học mới dùng trong KhoaHoc.trangThai: N'Tạm khóa', N'Khóa vĩnh viễn'
--   (cột nvarchar sẵn có, không cần đổi schema bảng KhoaHoc).
-- Chỉ TẠO bảng mới, không sửa / xóa dữ liệu cũ. Chạy lại nhiều lần vẫn an toàn.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

IF OBJECT_ID(N'dbo.BaoCaoViPham', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BaoCaoViPham (
        maBaoCao       VARCHAR(30)    NOT NULL CONSTRAINT PK_BaoCaoViPham PRIMARY KEY,
        maKhoaHoc      VARCHAR(20)    NOT NULL,
        maNguoiBaoCao  INT            NOT NULL,
        loaiViPham     NVARCHAR(100)  NOT NULL,
        moTa           NVARCHAR(2000) NOT NULL,
        -- N'Chờ xử lý' | N'Đã xử lý' | N'Đã bác bỏ'
        trangThai      NVARCHAR(50)   NOT NULL CONSTRAINT DF_BaoCaoViPham_TrangThai DEFAULT (N'Chờ xử lý'),
        ngayTao        DATETIME       NOT NULL CONSTRAINT DF_BaoCaoViPham_NgayTao DEFAULT (GETDATE()),
        ngayXuLy       DATETIME       NULL,
        maAdmin        INT            NULL,
        ghiChuXuLy     NVARCHAR(1000) NULL,

        CONSTRAINT FK_BaoCaoViPham_KhoaHoc   FOREIGN KEY (maKhoaHoc)     REFERENCES dbo.KhoaHoc(maKhoaHoc),
        CONSTRAINT FK_BaoCaoViPham_NguoiDung FOREIGN KEY (maNguoiBaoCao) REFERENCES dbo.NguoiDung(maNguoiDung),
        CONSTRAINT FK_BaoCaoViPham_Admin     FOREIGN KEY (maAdmin)       REFERENCES dbo.NguoiDung(maNguoiDung),
        CONSTRAINT CHK_BaoCaoViPham_TrangThai CHECK (trangThai IN (N'Chờ xử lý', N'Đã xử lý', N'Đã bác bỏ'))
    );

    CREATE INDEX IX_BaoCaoViPham_KhoaHoc_TrangThai ON dbo.BaoCaoViPham (maKhoaHoc, trangThai);
END
GO

IF OBJECT_ID(N'dbo.XuLyViPham', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.XuLyViPham (
        maXuLy          VARCHAR(30)    NOT NULL CONSTRAINT PK_XuLyViPham PRIMARY KEY,
        maKhoaHoc       VARCHAR(20)    NOT NULL,
        -- N'Bác bỏ' | N'Tạm khóa' | N'Mở lại' | N'Khóa vĩnh viễn'
        hanhDong        NVARCHAR(50)   NOT NULL,
        lyDo            NVARCHAR(2000) NOT NULL,
        hanPhanHoi      DATETIME       NULL,      -- chỉ dùng cho Tạm khóa
        trangThaiTruoc  NVARCHAR(100)  NULL,      -- trạng thái khóa học trước khi khóa, để khôi phục khi mở lại
        phanHoiGiaoVien NVARCHAR(2000) NULL,
        ngayPhanHoi     DATETIME       NULL,
        maAdmin         INT            NOT NULL,
        ngayXuLy        DATETIME       NOT NULL CONSTRAINT DF_XuLyViPham_NgayXuLy DEFAULT (GETDATE()),

        CONSTRAINT FK_XuLyViPham_KhoaHoc FOREIGN KEY (maKhoaHoc) REFERENCES dbo.KhoaHoc(maKhoaHoc),
        CONSTRAINT FK_XuLyViPham_Admin   FOREIGN KEY (maAdmin)   REFERENCES dbo.NguoiDung(maNguoiDung),
        CONSTRAINT CHK_XuLyViPham_HanhDong CHECK (hanhDong IN (N'Bác bỏ', N'Tạm khóa', N'Mở lại', N'Khóa vĩnh viễn'))
    );

    CREATE INDEX IX_XuLyViPham_KhoaHoc ON dbo.XuLyViPham (maKhoaHoc, ngayXuLy DESC);
END
GO
