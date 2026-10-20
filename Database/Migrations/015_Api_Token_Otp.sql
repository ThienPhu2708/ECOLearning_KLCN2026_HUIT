/*
    015_Api_Token_Otp.sql
    API cho ứng dụng di động (Controllers/Api, đường dẫn /api/v1/...).

    App không dùng Session / cookie như website, nên cần:
      - ApiToken: token đăng nhập (gửi trong header Authorization: Bearer <token>). Chỉ lưu SHA-256 của token,
        lộ CSDL cũng không dùng lại được token. Hết hạn sau 30 ngày; đăng xuất thì ghi ngayThuHoi.
      - ApiOtp: mã OTP đăng ký / quên mật khẩu gửi qua email (website giữ OTP trong Session). Chỉ lưu SHA-256 của mã,
        hết hạn sau 5 phút, sai quá 5 lần thì mã bị vô hiệu (cùng quy tắc với website).

    Chạy lại nhiều lần không lỗi. Chạy bằng sqlcmd thì thêm tham số -I.
*/
USE QL_HETHONG_CUNGCAP_KHOAHOC;
GO
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.ApiToken', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApiToken
    (
        maToken         INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_ApiToken PRIMARY KEY,
        tokenHash       CHAR(64)       NOT NULL,
        maNguoiDung     INT            NOT NULL CONSTRAINT FK_ApiToken_NguoiDung REFERENCES dbo.NguoiDung (maNguoiDung) ON DELETE CASCADE,
        ngayTao         DATETIME       NOT NULL CONSTRAINT DF_ApiToken_ngayTao DEFAULT (GETDATE()),
        ngayHetHan      DATETIME       NOT NULL,
        lanDungGanNhat  DATETIME       NULL,
        ngayThuHoi      DATETIME       NULL,
        thietBi         NVARCHAR(200)  NULL
    );
    CREATE UNIQUE INDEX UX_ApiToken_tokenHash ON dbo.ApiToken (tokenHash);
    CREATE INDEX IX_ApiToken_maNguoiDung ON dbo.ApiToken (maNguoiDung);
END
GO

IF OBJECT_ID(N'dbo.ApiOtp', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApiOtp
    (
        maOtp        INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_ApiOtp PRIMARY KEY,
        email        NVARCHAR(150)  NOT NULL,
        mucDich      VARCHAR(20)    NOT NULL,   -- register | forgot
        hoTen        NVARCHAR(100)  NULL,
        maHash       CHAR(64)       NOT NULL,
        ngayTao      DATETIME       NOT NULL CONSTRAINT DF_ApiOtp_ngayTao DEFAULT (GETDATE()),
        ngayHetHan   DATETIME       NOT NULL,
        soLanSai     INT            NOT NULL CONSTRAINT DF_ApiOtp_soLanSai DEFAULT (0),
        ngayXacThuc  DATETIME       NULL,
        daDung       BIT            NOT NULL CONSTRAINT DF_ApiOtp_daDung DEFAULT (0)
    );
    CREATE INDEX IX_ApiOtp_email ON dbo.ApiOtp (email, mucDich, ngayTao DESC);
END
GO

-- Kiểm tra
SELECT
    CASE WHEN OBJECT_ID(N'dbo.ApiToken', N'U') IS NOT NULL THEN N'Có' ELSE N'Thiếu' END AS ApiToken,
    CASE WHEN OBJECT_ID(N'dbo.ApiOtp', N'U') IS NOT NULL THEN N'Có' ELSE N'Thiếu' END AS ApiOtp;
