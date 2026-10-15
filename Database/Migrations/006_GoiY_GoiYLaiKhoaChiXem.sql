-- =============================================================================
-- 006_GoiY_GoiYLaiKhoaChiXem.sql  (chạy SAU 005_GoiY_LocCongTac.sql)
--
-- Sửa quy tắc "khóa nào không gợi ý lại" của lọc cộng tác:
--   Trước: loại MỌI khóa người dùng đã tương tác, kể cả khóa chỉ mở trang chi tiết xem qua.
--          -> Người xem nhiều khóa (chưa mua) bị hết khóa để gợi ý, trang "Khóa học gợi ý" trống.
--   Sau  : chỉ loại khóa người dùng đã SỞ HỮU / ĐÃ HỌC: đã đăng ký, có tiến độ học, đã đánh giá.
--          Khóa chỉ mới xem qua vẫn được gợi ý lại (giống "Vì bạn đã xem" của Udemy).
--
-- Lượt xem / click / thích VẪN là tín hiệu của điểm tương tác và ma trận độ tương đồng; chỉ đổi
-- bước lọc kết quả. Các stored procedure của 005 (sp_GoiY_KhoaHocChoNguoiDung,
-- sp_GoiY_TinhGoiYChoNguoiDung) dùng hàm này nên tự áp dụng quy tắc mới, không cần sửa.
-- Job Python (Recommender/build_recommendations.py) dùng cùng quy tắc (load_consumed_courses).
--
-- Chỉ thay định nghĩa một hàm, không đổi dữ liệu. Chạy lại nhiều lần vẫn an toàn.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

CREATE OR ALTER FUNCTION dbo.fn_GoiY_KhoaHocDuocGoiY (@maHocVien INT)
RETURNS TABLE
AS
RETURN
(
    SELECT kh.maKhoaHoc,
           ISNULL(p.soNguoi, 0)  AS soNguoi,
           ISNULL(p.ratingTB, 0) AS ratingTB
    FROM dbo.KhoaHoc kh
    LEFT JOIN (
        SELECT course_id, COUNT(*) AS soNguoi,
               SUM(rating) / NULLIF(SUM(CASE WHEN rating > 0 THEN 1 ELSE 0 END), 0) AS ratingTB
        FROM dbo.vw_GoiY_TuongTac
        GROUP BY course_id
    ) p ON p.course_id = kh.maKhoaHoc
    WHERE kh.trangThai IN (N'Đã xuất bản', N'Đã duyệt')
      AND kh.maGiaoVien <> @maHocVien
      AND NOT EXISTS (SELECT 1 FROM dbo.DangKyKhoaHoc d  WHERE d.maHocVien = @maHocVien AND d.maKhoaHoc = kh.maKhoaHoc)
      AND NOT EXISTS (SELECT 1 FROM dbo.TienDoHocTap td  WHERE td.maHocVien = @maHocVien AND td.maKhoaHoc = kh.maKhoaHoc)
      AND NOT EXISTS (SELECT 1 FROM dbo.DanhGiaKhoaHoc g WHERE g.maHocVien = @maHocVien AND g.maKhoaHoc = kh.maKhoaHoc)
);
GO

PRINT N'006: fn_GoiY_KhoaHocDuocGoiY chỉ loại khóa đã đăng ký / đã học / đã đánh giá.';
GO
