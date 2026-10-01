-- =============================================================================
-- 99_xoa_du_lieu_demo.sql — Xóa TOÀN BỘ dữ liệu demo do 01_du_lieu_demo.sql tạo ra
-- (kể cả dữ liệu phát sinh khi bạn thao tác trên web bằng các tài khoản / khóa học demo).
-- Chỉ xóa dữ liệu nhận diện được là demo:
--   người dùng @demo.ecolearning.vn, khóa học DMKH.., danh mục DM04–DM06 (đúng tên demo, không còn khóa học).
-- Chạy trong 1 transaction: lỗi ở đâu thì không xóa gì cả.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Users TABLE (id INT PRIMARY KEY);
INSERT INTO @Users SELECT maNguoiDung FROM NguoiDung WHERE email LIKE '%@demo.ecolearning.vn';

DECLARE @Courses TABLE (id VARCHAR(20) PRIMARY KEY);
INSERT INTO @Courses SELECT maKhoaHoc FROM KhoaHoc WHERE maKhoaHoc LIKE 'DMKH%' OR maGiaoVien IN (SELECT id FROM @Users);

DECLARE @Lessons TABLE (id VARCHAR(20) PRIMARY KEY);
INSERT INTO @Lessons
SELECT b.maBaiHoc FROM BaiHoc b LEFT JOIN ChuongHoc c ON c.maChuong = b.maChuong
WHERE c.maKhoaHoc IN (SELECT id FROM @Courses)
   OR EXISTS (SELECT 1 FROM @Courses k WHERE b.maChuong IS NULL AND b.maBaiHoc LIKE k.id + '\_%' ESCAPE '\');

DECLARE @Orders TABLE (id VARCHAR(20) PRIMARY KEY);
INSERT INTO @Orders
SELECT maDonHang FROM DonHang WHERE maDonHang LIKE 'DMDH%' OR maHocVien IN (SELECT id FROM @Users)
UNION
SELECT ct.maDonHang FROM ChiTietDonHang ct WHERE ct.maKhoaHoc IN (SELECT id FROM @Courses);

BEGIN TRANSACTION;

-- Báo cáo & xử lý vi phạm
IF OBJECT_ID(N'dbo.PhanHoiViPham', N'U') IS NOT NULL
    DELETE FROM PhanHoiViPham
    WHERE maNguoiGui IN (SELECT id FROM @Users)
       OR maXuLy IN (SELECT maXuLy FROM XuLyViPham WHERE maKhoaHoc IN (SELECT id FROM @Courses));
IF OBJECT_ID(N'dbo.XuLyViPham', N'U') IS NOT NULL
    DELETE FROM XuLyViPham WHERE maKhoaHoc IN (SELECT id FROM @Courses);
IF OBJECT_ID(N'dbo.BaoCaoViPham', N'U') IS NOT NULL
    DELETE FROM BaoCaoViPham WHERE maKhoaHoc IN (SELECT id FROM @Courses) OR maNguoiBaoCao IN (SELECT id FROM @Users);

-- Học tập
DELETE FROM DanhGiaKhoaHoc   WHERE maKhoaHoc IN (SELECT id FROM @Courses) OR maHocVien IN (SELECT id FROM @Users);
DELETE FROM TienDoBaiHoc     WHERE maBaiHoc IN (SELECT id FROM @Lessons) OR maHocVien IN (SELECT id FROM @Users);
DELETE FROM TienDoHocTap     WHERE maKhoaHoc IN (SELECT id FROM @Courses) OR maHocVien IN (SELECT id FROM @Users);
DELETE FROM KhoaHocKhuyenNghi WHERE maKhoaHoc IN (SELECT id FROM @Courses) OR maHocVien IN (SELECT id FROM @Users);
DELETE FROM DangKyKhoaHoc    WHERE maKhoaHoc IN (SELECT id FROM @Courses) OR maHocVien IN (SELECT id FROM @Users);

-- Đơn hàng
DELETE FROM ThanhToan      WHERE maDonHang IN (SELECT id FROM @Orders);
DELETE FROM ChiTietDonHang WHERE maDonHang IN (SELECT id FROM @Orders) OR maKhoaHoc IN (SELECT id FROM @Courses);
DELETE FROM DonHang        WHERE maDonHang IN (SELECT id FROM @Orders);

-- Giáo viên
DELETE FROM NhanThanhToan  WHERE maGiaoVien IN (SELECT id FROM @Users);
DELETE FROM DangKyGiaoVien WHERE maNguoiDung IN (SELECT id FROM @Users);
IF OBJECT_ID(N'dbo.KhuyenMaiKhoaHoc', N'U') IS NOT NULL
    DELETE FROM KhuyenMaiKhoaHoc WHERE maKhoaHoc IN (SELECT id FROM @Courses);

-- Nội dung khóa học
DELETE FROM KiemDuyetKhoaHoc WHERE maKhoaHoc IN (SELECT id FROM @Courses);
DELETE FROM TaiNguyen WHERE maBaiHoc IN (SELECT id FROM @Lessons);
DELETE FROM BaiHoc    WHERE maBaiHoc IN (SELECT id FROM @Lessons);
DELETE FROM ChuongHoc WHERE maKhoaHoc IN (SELECT id FROM @Courses);
-- Trigger chặn xóa khóa học "Chờ duyệt" -> chuyển về Bản nháp trước khi xóa
UPDATE KhoaHoc SET trangThai = N'Bản nháp'
WHERE maKhoaHoc IN (SELECT id FROM @Courses) AND trangThai IN (N'Chờ duyệt', N'Chờ xét duyệt');
DELETE FROM KhoaHoc WHERE maKhoaHoc IN (SELECT id FROM @Courses);

-- Danh mục demo (chỉ xóa khi đúng tên demo và không còn khóa học nào)
DELETE FROM DanhMuc
WHERE ((maDanhMuc = 'DM04' AND tenDanhMuc = N'Marketing & Kinh doanh')
    OR (maDanhMuc = 'DM05' AND tenDanhMuc = N'Ngoại ngữ')
    OR (maDanhMuc = 'DM06' AND tenDanhMuc = N'Nhiếp ảnh & Video'))
  AND NOT EXISTS (SELECT 1 FROM KhoaHoc k WHERE k.maDanhMuc = DanhMuc.maDanhMuc);

-- Người dùng demo
DELETE FROM BaoCaoThongKe WHERE maNguoiDung IN (SELECT id FROM @Users);
DELETE FROM NguoiDung WHERE maNguoiDung IN (SELECT id FROM @Users);

COMMIT TRANSACTION;

SELECT N'Đã xóa dữ liệu demo' AS [Kết quả],
       (SELECT COUNT(*) FROM NguoiDung WHERE email LIKE '%@demo.ecolearning.vn') AS [Người dùng demo còn lại],
       (SELECT COUNT(*) FROM KhoaHoc WHERE maKhoaHoc LIKE 'DMKH%') AS [Khóa học demo còn lại];
GO
