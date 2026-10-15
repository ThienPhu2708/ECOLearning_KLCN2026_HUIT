-- =============================================================================
-- 005_GoiY_LocCongTac.sql   (chạy SAU 001)
-- Mục đích: Gợi ý khóa học bằng lọc cộng tác dựa trên khóa học (Item-based CF)
--   1. TuongTacKhoaHoc       : lượt xem / lượt click / thích của người dùng với khóa học
--                              (ứng dụng ghi vào ở giai đoạn làm giao diện)
--   2. course_similarity     : ma trận độ tương đồng cosine giữa các khóa học,
--                              do job Python (Recommender/build_recommendations.py) tính định kỳ
--   3. GoiY_TrongSo          : trọng số + giá trị chuẩn hóa của từng tín hiệu mà job đã dùng
--   4. vw_GoiY_TuongTac      : dữ liệu tương tác cùng cấu trúc với file CSV thử nghiệm
--                              user_id, course_id, rating, views, clicks, likes, progress_percent
--   5. vw_GoiY_TinHieu       : vw_GoiY_TuongTac dạng dọc (user, course, tinHieu, giaTri)
--   6. fn_GoiY_KhoaHocDuocGoiY     : khóa được phép gợi ý cho một người + độ phổ biến
--   7. sp_GoiY_KhoaHocTuongTu      : top-K "Khóa học tương tự" (trang chi tiết)
--   8. sp_GoiY_KhoaHocChoNguoiDung : top-K "Khóa học dành cho bạn", đọc kết quả đã tính sẵn
--   9. sp_GoiY_TinhGoiYChoNguoiDung: nút "Gợi ý khóa học", tính lại gợi ý cho MỘT người
--                                    từ ma trận đã lưu (không tính lại cosine) và lưu kết quả
-- Không xóa / sửa dữ liệu cũ, chỉ bổ sung. Chạy lại nhiều lần vẫn an toàn.
-- =============================================================================
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

-- 1. Hành vi xem / click / thích (rating lấy từ DanhGiaKhoaHoc, tiến độ từ TienDoHocTap)
IF OBJECT_ID(N'dbo.TuongTacKhoaHoc', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TuongTacKhoaHoc
    (
        maHocVien   INT          NOT NULL,
        maKhoaHoc   VARCHAR(20)  NOT NULL,
        luotXem     INT          NOT NULL CONSTRAINT DF_TuongTac_LuotXem   DEFAULT (0),  -- số lần mở trang chi tiết
        luotClick   INT          NOT NULL CONSTRAINT DF_TuongTac_LuotClick DEFAULT (0),  -- xem thử, thêm giỏ, mở bài học
        daThich     BIT          NOT NULL CONSTRAINT DF_TuongTac_DaThich   DEFAULT (0),
        lanCapNhat  DATETIME     NOT NULL CONSTRAINT DF_TuongTac_LanCapNhat DEFAULT (GETDATE()),
        CONSTRAINT PK_TuongTacKhoaHoc PRIMARY KEY (maHocVien, maKhoaHoc),
        CONSTRAINT FK_TuongTac_NguoiDung FOREIGN KEY (maHocVien) REFERENCES dbo.NguoiDung (maNguoiDung),
        CONSTRAINT FK_TuongTac_KhoaHoc   FOREIGN KEY (maKhoaHoc) REFERENCES dbo.KhoaHoc (maKhoaHoc) ON DELETE CASCADE,
        CONSTRAINT CHK_TuongTac_KhongAm  CHECK (luotXem >= 0 AND luotClick >= 0)
    );
END
GO

-- 2. Độ tương đồng giữa các khóa học (mỗi cặp lưu 2 chiều a->b và b->a, chỉ lưu similarity > 0)
--    Không đặt khóa ngoại: bảng được job xóa và ghi lại toàn bộ; khi đọc luôn JOIN KhoaHoc.
IF OBJECT_ID(N'dbo.course_similarity', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.course_similarity
    (
        course_id_a VARCHAR(20) NOT NULL,
        course_id_b VARCHAR(20) NOT NULL,
        similarity  FLOAT       NOT NULL,
        computed_at DATETIME    NOT NULL CONSTRAINT DF_CourseSimilarity_ComputedAt DEFAULT (GETDATE()),
        CONSTRAINT PK_course_similarity PRIMARY KEY (course_id_a, course_id_b),
        CONSTRAINT CHK_course_similarity_KhoangGiaTri CHECK (similarity > 0 AND similarity <= 1.000001),
        CONSTRAINT CHK_course_similarity_KhacNhau CHECK (course_id_a <> course_id_b)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_course_similarity_TopK'
               AND object_id = OBJECT_ID(N'dbo.course_similarity'))
BEGIN
    CREATE INDEX IX_course_similarity_TopK ON dbo.course_similarity (course_id_a, similarity DESC)
        INCLUDE (course_id_b);
END
GO

-- 3. Trọng số tín hiệu. Nguồn gốc là SIGNAL_WEIGHTS trong Recommender/recommender/config.py;
--    mỗi lần chạy, job ghi đè bảng này cùng lúc với course_similarity để SQL tính điểm giống Python.
--    giaTriChuanHoa NULL = lấy max thực tế trong dữ liệu (chỉ xảy ra trước lần chạy job đầu tiên).
IF OBJECT_ID(N'dbo.GoiY_TrongSo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GoiY_TrongSo
    (
        tinHieu        VARCHAR(50) NOT NULL CONSTRAINT PK_GoiY_TrongSo PRIMARY KEY,
        trongSo        FLOAT       NOT NULL,
        giaTriChuanHoa FLOAT       NULL,
        capNhatLuc     DATETIME    NOT NULL CONSTRAINT DF_GoiY_TrongSo_CapNhatLuc DEFAULT (GETDATE()),
        CONSTRAINT CHK_GoiY_TrongSo_TrongSo CHECK (trongSo >= 0 AND trongSo <= 1),
        CONSTRAINT CHK_GoiY_TrongSo_ChuanHoa CHECK (giaTriChuanHoa IS NULL OR giaTriChuanHoa > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.GoiY_TrongSo)
BEGIN
    INSERT INTO dbo.GoiY_TrongSo (tinHieu, trongSo, giaTriChuanHoa)
    VALUES ('rating', 0.4, 5), ('views', 0.2, NULL), ('clicks', 0.1, NULL),
           ('likes', 0.1, 1), ('progress_percent', 0.2, 100);
END
GO

-- 4. Dữ liệu tương tác cho thuật toán: một dòng cho mỗi cặp (người dùng, khóa học) có bất kỳ tương tác nào.
--    Thêm bình luận sau này: thêm cột comments ở đây, một dòng ở vw_GoiY_TinHieu và một dòng trong SIGNAL_WEIGHTS.
CREATE OR ALTER VIEW dbo.vw_GoiY_TuongTac
AS
WITH Cap AS
(
    SELECT maHocVien, maKhoaHoc FROM dbo.DangKyKhoaHoc
    UNION SELECT maHocVien, maKhoaHoc FROM dbo.TienDoHocTap
    UNION SELECT maHocVien, maKhoaHoc FROM dbo.DanhGiaKhoaHoc
    UNION SELECT maHocVien, maKhoaHoc FROM dbo.TuongTacKhoaHoc
),
DanhGia AS
(
    SELECT maHocVien, maKhoaHoc, AVG(CAST(soSao AS FLOAT)) AS soSao
    FROM dbo.DanhGiaKhoaHoc
    GROUP BY maHocVien, maKhoaHoc
),
TienDo AS
(
    SELECT maHocVien, maKhoaHoc, MAX(phanTramHoanThanh) AS phanTram
    FROM dbo.TienDoHocTap
    GROUP BY maHocVien, maKhoaHoc
)
SELECT
    c.maHocVien                                AS user_id,
    c.maKhoaHoc                                AS course_id,
    ISNULL(dg.soSao, 0)                        AS rating,
    ISNULL(tt.luotXem, 0)                      AS views,
    ISNULL(tt.luotClick, 0)                    AS clicks,
    CAST(ISNULL(tt.daThich, 0) AS INT)         AS likes,
    CAST(ISNULL(td.phanTram, 0) AS FLOAT)      AS progress_percent
FROM Cap c
LEFT JOIN DanhGia dg            ON dg.maHocVien = c.maHocVien AND dg.maKhoaHoc = c.maKhoaHoc
LEFT JOIN TienDo td             ON td.maHocVien = c.maHocVien AND td.maKhoaHoc = c.maKhoaHoc
LEFT JOIN dbo.TuongTacKhoaHoc tt ON tt.maHocVien = c.maHocVien AND tt.maKhoaHoc = c.maKhoaHoc;
GO

-- 5. Tín hiệu dạng dọc để nhân với GoiY_TrongSo (tên tín hiệu trùng khóa trong SIGNAL_WEIGHTS)
CREATE OR ALTER VIEW dbo.vw_GoiY_TinHieu
AS
SELECT t.user_id, t.course_id, v.tinHieu, v.giaTri
FROM dbo.vw_GoiY_TuongTac t
CROSS APPLY (VALUES
    ('rating',           CAST(t.rating AS FLOAT)),
    ('views',            CAST(t.views AS FLOAT)),
    ('clicks',           CAST(t.clicks AS FLOAT)),
    ('likes',            CAST(t.likes AS FLOAT)),
    ('progress_percent', CAST(t.progress_percent AS FLOAT))
) v (tinHieu, giaTri);
GO

-- 6. Khóa học được phép gợi ý cho một người: đang mở bán, không do chính người đó dạy,
--    người đó chưa tương tác. Kèm độ phổ biến (số người tương tác, rating TB của lượt đã đánh giá).
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
      AND NOT EXISTS (SELECT 1 FROM dbo.vw_GoiY_TuongTac t
                      WHERE t.user_id = @maHocVien AND t.course_id = kh.maKhoaHoc)
);
GO

-- 7. Khóa học tương tự (trang chi tiết khóa học)
CREATE OR ALTER PROCEDURE dbo.sp_GoiY_KhoaHocTuongTu
    @maKhoaHoc VARCHAR(20),
    @topK      INT = 6
AS
BEGIN
    SET NOCOUNT ON;

    IF @topK <= 0
    BEGIN
        RAISERROR(N'Top-K phải lớn hơn 0.', 16, 1);
        RETURN;
    END;

    SELECT TOP (@topK)
        kh.maKhoaHoc,
        kh.tenKhoaHoc,
        kh.moTaNgan,
        kh.anhBia,
        kh.capDo,
        kh.giaBan,
        kh.giaKhuyenMai,
        nd.hoTen        AS tenGiaoVien,
        s.similarity    AS doTuongDong
    FROM dbo.course_similarity s
    INNER JOIN dbo.KhoaHoc kh   ON kh.maKhoaHoc = s.course_id_b
    INNER JOIN dbo.NguoiDung nd ON nd.maNguoiDung = kh.maGiaoVien
    WHERE s.course_id_a = @maKhoaHoc
      AND kh.trangThai IN (N'Đã xuất bản', N'Đã duyệt')
    ORDER BY s.similarity DESC, kh.maKhoaHoc;
END;
GO

-- 8. Khóa học dành cho bạn (hiển thị tự động, đọc kết quả đã tính sẵn)
--    Nguồn 1: KhoaHocKhuyenNghi (job hằng đêm hoặc lần bấm nút gần nhất), lọc lại bằng
--             fn_GoiY_KhoaHocDuocGoiY để bỏ khóa đã tương tác / không còn mở bán.
--    Nguồn 2: khóa phổ biến nhất để bù cho đủ K hoặc cho người dùng mới (cold-start).
CREATE OR ALTER PROCEDURE dbo.sp_GoiY_KhoaHocChoNguoiDung
    @maHocVien INT,
    @topK      INT = 8
AS
BEGIN
    SET NOCOUNT ON;

    IF @topK <= 0
    BEGIN
        RAISERROR(N'Top-K phải lớn hơn 0.', 16, 1);
        RETURN;
    END;

    DECLARE @KetQua TABLE
    (
        thuTu      INT IDENTITY(1, 1) PRIMARY KEY,
        maKhoaHoc  VARCHAR(20) NOT NULL UNIQUE,
        diemDuDoan FLOAT       NOT NULL,
        nguon      NVARCHAR(20) NOT NULL
    );

    INSERT INTO @KetQua (maKhoaHoc, diemDuDoan, nguon)
    SELECT TOP (@topK) g.maKhoaHoc, g.diemDuDoan, N'LocCongTac'
    FROM dbo.KhoaHocKhuyenNghi g
    INNER JOIN dbo.fn_GoiY_KhoaHocDuocGoiY(@maHocVien) c ON c.maKhoaHoc = g.maKhoaHoc
    WHERE g.maHocVien = @maHocVien
    ORDER BY g.thuTuUuTien, g.diemDuDoan DESC;

    DECLARE @conThieu INT = @topK - (SELECT COUNT(*) FROM @KetQua);
    IF @conThieu > 0
    BEGIN
        INSERT INTO @KetQua (maKhoaHoc, diemDuDoan, nguon)
        SELECT TOP (@conThieu) c.maKhoaHoc, 0, N'PhoBien'
        FROM dbo.fn_GoiY_KhoaHocDuocGoiY(@maHocVien) c
        WHERE NOT EXISTS (SELECT 1 FROM @KetQua k WHERE k.maKhoaHoc = c.maKhoaHoc)
        ORDER BY c.soNguoi DESC, c.ratingTB DESC, c.maKhoaHoc;
    END;

    SELECT
        k.thuTu,
        kh.maKhoaHoc,
        kh.tenKhoaHoc,
        kh.moTaNgan,
        kh.anhBia,
        kh.capDo,
        kh.giaBan,
        kh.giaKhuyenMai,
        nd.hoTen     AS tenGiaoVien,
        k.diemDuDoan,
        k.nguon
    FROM @KetQua k
    INNER JOIN dbo.KhoaHoc kh   ON kh.maKhoaHoc = k.maKhoaHoc
    INNER JOIN dbo.NguoiDung nd ON nd.maNguoiDung = kh.maGiaoVien
    ORDER BY k.thuTu;
END;
GO

-- 9. Nút "Gợi ý khóa học": tính lại gợi ý cho MỘT người từ ma trận course_similarity đã lưu
--    (không tính lại cosine; ma trận do job hằng đêm cập nhật).
--    Bước 1: vector điểm tương tác MỚI NHẤT của người dùng (cùng công thức/trọng số với job)
--    Bước 2: dùng ma trận tương đồng đã tính sẵn
--    Bước 3: diemDuDoan(j) = Σ sim(i, j) * diem(i) trên các khóa i đã tương tác, sắp giảm dần
--    Bước 4: lấy top-K; thiếu thì bù khóa phổ biến. Lưu phần lọc cộng tác vào KhoaHocKhuyenNghi.
CREATE OR ALTER PROCEDURE dbo.sp_GoiY_TinhGoiYChoNguoiDung
    @maHocVien INT,
    @topK      INT = 8
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @topK <= 0
    BEGIN
        RAISERROR(N'Top-K phải lớn hơn 0.', 16, 1);
        RETURN;
    END;

    -- Trọng số và giá trị chuẩn hóa job đã dùng; NULL/0 thì lấy max thực tế (như max=None ở Python)
    DECLARE @TrongSo TABLE (tinHieu VARCHAR(50) PRIMARY KEY, trongSo FLOAT NOT NULL, giaTriMax FLOAT NULL);
    INSERT INTO @TrongSo (tinHieu, trongSo, giaTriMax)
    SELECT tinHieu, trongSo, giaTriChuanHoa FROM dbo.GoiY_TrongSo;

    IF EXISTS (SELECT 1 FROM @TrongSo WHERE giaTriMax IS NULL OR giaTriMax <= 0)
    BEGIN
        UPDATE w
        SET giaTriMax = ISNULL(NULLIF(m.giaTriMax, 0), 1)
        FROM @TrongSo w
        LEFT JOIN (SELECT tinHieu, MAX(giaTri) AS giaTriMax FROM dbo.vw_GoiY_TinHieu GROUP BY tinHieu) m
            ON m.tinHieu = w.tinHieu
        WHERE w.giaTriMax IS NULL OR w.giaTriMax <= 0;
    END;

    -- Bước 1: điểm = Σ trongSo * min(giaTri / giaTriMax, 1)
    DECLARE @DaHoc TABLE (maKhoaHoc VARCHAR(20) PRIMARY KEY, diem FLOAT NOT NULL);
    INSERT INTO @DaHoc (maKhoaHoc, diem)
    SELECT t.course_id,
           SUM(w.trongSo * CASE WHEN t.giaTri <= 0 THEN 0
                                WHEN t.giaTri >= w.giaTriMax THEN 1
                                ELSE t.giaTri / w.giaTriMax END)
    FROM dbo.vw_GoiY_TinHieu t
    INNER JOIN @TrongSo w ON w.tinHieu = t.tinHieu
    WHERE t.user_id = @maHocVien
    GROUP BY t.course_id;

    DECLARE @KetQua TABLE
    (
        thuTu         INT IDENTITY(1, 1) PRIMARY KEY,
        maKhoaHoc     VARCHAR(20)  NOT NULL UNIQUE,
        diemDuDoan    FLOAT        NOT NULL,
        nguon         NVARCHAR(20) NOT NULL,
        maKhoaHocLyDo VARCHAR(20)  NULL   -- khóa đã học đóng góp nhiều nhất ("Vì bạn đã học ...")
    );

    -- Bước 2-4
    WITH DongGop AS
    (
        SELECT s.course_id_b AS maKhoaHoc, d.maKhoaHoc AS khoaDaHoc, s.similarity * d.diem AS dongGop
        FROM @DaHoc d
        INNER JOIN dbo.course_similarity s ON s.course_id_a = d.maKhoaHoc
    ),
    XepHang AS
    (
        SELECT maKhoaHoc, khoaDaHoc,
               SUM(dongGop) OVER (PARTITION BY maKhoaHoc) AS diem,
               ROW_NUMBER() OVER (PARTITION BY maKhoaHoc ORDER BY dongGop DESC, khoaDaHoc) AS hang
        FROM DongGop
    )
    INSERT INTO @KetQua (maKhoaHoc, diemDuDoan, nguon, maKhoaHocLyDo)
    SELECT TOP (@topK) x.maKhoaHoc, x.diem, N'LocCongTac', x.khoaDaHoc
    FROM XepHang x
    INNER JOIN dbo.fn_GoiY_KhoaHocDuocGoiY(@maHocVien) c ON c.maKhoaHoc = x.maKhoaHoc
    WHERE x.hang = 1 AND x.diem > 0
    ORDER BY x.diem DESC, x.maKhoaHoc;

    DECLARE @conThieu INT = @topK - (SELECT COUNT(*) FROM @KetQua);
    IF @conThieu > 0
    BEGIN
        INSERT INTO @KetQua (maKhoaHoc, diemDuDoan, nguon)
        SELECT TOP (@conThieu) c.maKhoaHoc, 0, N'PhoBien'
        FROM dbo.fn_GoiY_KhoaHocDuocGoiY(@maHocVien) c
        WHERE NOT EXISTS (SELECT 1 FROM @KetQua k WHERE k.maKhoaHoc = c.maKhoaHoc)
        ORDER BY c.soNguoi DESC, c.ratingTB DESC, c.maKhoaHoc;
    END;

    -- Lưu phần lọc cộng tác (chỉ người học, chỉ khóa 'Đã xuất bản' theo trigger của KhoaHocKhuyenNghi)
    IF EXISTS (SELECT 1 FROM dbo.NguoiDung
               WHERE maNguoiDung = @maHocVien AND vaiTro IN (N'HocVien', N'GiaoVien'))
    BEGIN
        DECLARE @khoa NVARCHAR(255) = CONCAT(N'GoiY_', @maHocVien);
        BEGIN TRANSACTION;
        -- Hai lần bấm cùng lúc của một người không ghi chồng lên nhau
        EXEC sp_getapplock @Resource = @khoa, @LockMode = 'Exclusive', @LockOwner = 'Transaction';

        DELETE FROM dbo.KhoaHocKhuyenNghi WHERE maHocVien = @maHocVien;

        INSERT INTO dbo.KhoaHocKhuyenNghi (maGoiY, diemDuDoan, thuTuUuTien, ngayGoiY, maHocVien, maKhoaHoc)
        SELECT CONCAT('GY', @maHocVien, '_', r.hang), r.diemDuDoan, r.hang, GETDATE(), @maHocVien, r.maKhoaHoc
        FROM (
            SELECT k.maKhoaHoc, k.diemDuDoan, ROW_NUMBER() OVER (ORDER BY k.thuTu) AS hang
            FROM @KetQua k
            INNER JOIN dbo.KhoaHoc kh ON kh.maKhoaHoc = k.maKhoaHoc
            WHERE k.nguon = N'LocCongTac' AND kh.trangThai = N'Đã xuất bản'
        ) r;

        COMMIT TRANSACTION;
    END;

    SELECT
        k.thuTu,
        kh.maKhoaHoc,
        kh.tenKhoaHoc,
        kh.moTaNgan,
        kh.anhBia,
        kh.capDo,
        kh.giaBan,
        kh.giaKhuyenMai,
        nd.hoTen        AS tenGiaoVien,
        k.diemDuDoan,
        k.nguon,
        k.maKhoaHocLyDo,
        ld.tenKhoaHoc   AS tenKhoaHocLyDo,
        (SELECT MAX(computed_at) FROM dbo.course_similarity) AS maTranCapNhatLuc
    FROM @KetQua k
    INNER JOIN dbo.KhoaHoc kh   ON kh.maKhoaHoc = k.maKhoaHoc
    INNER JOIN dbo.NguoiDung nd ON nd.maNguoiDung = kh.maGiaoVien
    LEFT JOIN dbo.KhoaHoc ld    ON ld.maKhoaHoc = k.maKhoaHocLyDo
    ORDER BY k.thuTu;
END;
GO
