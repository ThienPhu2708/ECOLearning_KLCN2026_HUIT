-- =============================================================================
-- 001_KhuyenNghi_ChoPhepGiaoVien.sql
-- Mục đích: Giáo viên vẫn là người học (mua/học khóa học của giáo viên khác),
--           nên cũng phải nhận được khóa học khuyến nghị (lọc cộng tác).
-- Thay đổi: Điều kiện 3 của trigger trg_KhoaHocKhuyenNghi_KiemTraDuLieu
--           từ  vaiTro <> N'HocVien'
--           thành vaiTro NOT IN (N'HocVien', N'GiaoVien')
-- Các điều kiện 1, 2, 4 giữ nguyên như bản đang chạy trong CSDL.
-- Không thay đổi / xóa dữ liệu. Chạy lại nhiều lần vẫn an toàn (ALTER).
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

ALTER TRIGGER trg_KhoaHocKhuyenNghi_KiemTraDuLieu
ON KhoaHocKhuyenNghi
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. Điểm dự đoán không được âm
    IF EXISTS (
        SELECT 1
        FROM inserted
        WHERE diemDuDoan < 0
    )
    BEGIN
        RAISERROR(N'Điểm dự đoán không được nhỏ hơn 0.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    -- 2. Thứ tự ưu tiên phải lớn hơn 0
    IF EXISTS (
        SELECT 1
        FROM inserted
        WHERE thuTuUuTien <= 0
    )
    BEGIN
        RAISERROR(N'Thứ tự ưu tiên phải lớn hơn 0.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    -- 3. Người nhận gợi ý phải là người học: Học viên, hoặc Giáo viên (giáo viên vẫn mua/học khóa học khác)
    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN NguoiDung nd
            ON i.maHocVien = nd.maNguoiDung
        WHERE nd.vaiTro NOT IN (N'HocVien', N'GiaoVien')
    )
    BEGIN
        RAISERROR(N'Chỉ học viên hoặc giáo viên mới được nhận khóa học khuyến nghị.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    -- 4. Chỉ được khuyến nghị khóa học đã xuất bản
    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN KhoaHoc kh
            ON i.maKhoaHoc = kh.maKhoaHoc
        WHERE kh.trangThai <> N'Đã xuất bản'
    )
    BEGIN
        RAISERROR(N'Không được khuyến nghị khóa học chưa được xuất bản.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;
END;
GO
