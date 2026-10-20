-- 014: Luồng thanh toán và chi trả doanh thu ở chế độ THỬ NGHIỆM (chưa có pháp nhân nên chưa kết nối cổng thật).
-- Đơn hàng có trạng thái: Chờ thanh toán -> Đã thanh toán / Thanh toán thất bại / Hết hạn / Đã hủy.
-- Cổng thử nghiệm gửi kết quả (IPN) có chữ ký HMAC-SHA512 giống VNPay; dòng ThanhToan "Đã thanh toán" kích hoạt
-- trigger có sẵn trg_ThanhToan_TuDongDangKy để ghi danh học viên.
-- Yêu cầu rút tiền của giáo viên được Admin duyệt và "chuyển khoản thử nghiệm" (sinh mã giao dịch, không chuyển tiền thật).
-- Chỉ thêm cột / bảng, không sửa dữ liệu cũ. Chạy lại được.
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO

-- Đơn hàng: phương thức chọn ở giỏ hàng, hạn thanh toán, thời điểm thanh toán xong
IF COL_LENGTH('dbo.DonHang', 'phuongThuc') IS NULL
    ALTER TABLE dbo.DonHang ADD phuongThuc NVARCHAR(50) NULL;
IF COL_LENGTH('dbo.DonHang', 'ngayHetHan') IS NULL
    ALTER TABLE dbo.DonHang ADD ngayHetHan DATETIME NULL;
IF COL_LENGTH('dbo.DonHang', 'ngayThanhToan') IS NULL
    ALTER TABLE dbo.DonHang ADD ngayThanhToan DATETIME NULL;
GO

-- Giao dịch: cổng thanh toán, mã giao dịch phía cổng (mỗi giao dịch chỉ được ghi nhận một lần), thời điểm thanh toán
IF COL_LENGTH('dbo.ThanhToan', 'congThanhToan') IS NULL
    ALTER TABLE dbo.ThanhToan ADD congThanhToan NVARCHAR(50) NULL;
IF COL_LENGTH('dbo.ThanhToan', 'maGiaoDichCong') IS NULL
    ALTER TABLE dbo.ThanhToan ADD maGiaoDichCong VARCHAR(50) NULL;
IF COL_LENGTH('dbo.ThanhToan', 'ngayThanhToan') IS NULL
    ALTER TABLE dbo.ThanhToan ADD ngayThanhToan DATETIME NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ThanhToan_GiaoDichCong' AND object_id = OBJECT_ID('dbo.ThanhToan'))
    CREATE UNIQUE INDEX UX_ThanhToan_GiaoDichCong ON dbo.ThanhToan (congThanhToan, maGiaoDichCong) WHERE maGiaoDichCong IS NOT NULL;
GO

-- Nhật ký mọi lần cổng gọi về (IPN) và người dùng quay về (Return): phục vụ tra soát / đối soát
IF OBJECT_ID('dbo.NhatKyThanhToan') IS NULL
CREATE TABLE dbo.NhatKyThanhToan (
    maNhatKy INT IDENTITY(1, 1) PRIMARY KEY,
    maDonHang VARCHAR(20) NULL,
    loai VARCHAR(20) NOT NULL,              -- TAO_DON / IPN / RETURN / HET_HAN / HUY
    duLieu NVARCHAR(MAX) NULL,              -- tham số nhận được (đã bỏ chữ ký)
    chuKyHopLe BIT NULL,
    maPhanHoi VARCHAR(10) NULL,             -- mã trả về cho cổng: 00 thành công, 01 không thấy đơn, 02 đã xử lý, 04 sai số tiền, 97 sai chữ ký
    ghiChu NVARCHAR(500) NULL,
    thoiGian DATETIME NOT NULL CONSTRAINT DF_NhatKyThanhToan_ThoiGian DEFAULT GETDATE()
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NhatKyThanhToan_DonHang' AND object_id = OBJECT_ID('dbo.NhatKyThanhToan'))
    CREATE INDEX IX_NhatKyThanhToan_DonHang ON dbo.NhatKyThanhToan (maDonHang, thoiGian);
GO

-- Phiếu chi doanh thu giáo viên: Admin xử lý (chuyển khoản thử nghiệm / từ chối)
IF COL_LENGTH('dbo.NhanThanhToan', 'ngayXuLy') IS NULL
    ALTER TABLE dbo.NhanThanhToan ADD ngayXuLy DATETIME NULL;
IF COL_LENGTH('dbo.NhanThanhToan', 'maGiaoDichChi') IS NULL
    ALTER TABLE dbo.NhanThanhToan ADD maGiaoDichChi VARCHAR(50) NULL;
IF COL_LENGTH('dbo.NhanThanhToan', 'lyDoTuChoi') IS NULL
    ALTER TABLE dbo.NhanThanhToan ADD lyDoTuChoi NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.NhanThanhToan', 'maAdminXuLy') IS NULL
    ALTER TABLE dbo.NhanThanhToan ADD maAdminXuLy INT NULL CONSTRAINT FK_NhanThanhToan_AdminXuLy REFERENCES dbo.NguoiDung (maNguoiDung);
GO
