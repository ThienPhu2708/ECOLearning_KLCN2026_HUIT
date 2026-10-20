@echo off
chcp 65001 >nul
REM ======================================================================
REM  Mở địa chỉ public tạm thời cho API ứng dụng di động (P-2, Tuan10/Task_Tuan10.docx mục 5.2)
REM  - Cần chạy website trước (Visual Studio / IIS Express: https://localhost:44350)
REM  - Cần cloudflared.exe: https://github.com/cloudflare/cloudflared/releases (bản windows-amd64)
REM    đặt cùng thư mục với file này hoặc trong PATH
REM  - Sau khi chạy, cửa sổ in ra dòng https://xxxx.trycloudflare.com -> gửi cho Thư, Ngọc:
REM      BASE_URL của app = https://xxxx.trycloudflare.com/api/v1
REM  - Kiểm tra: mở https://xxxx.trycloudflare.com/api/v1/ping, trường publicBaseUrl phải là
REM    https://xxxx.trycloudflare.com. Nếu vẫn là https://localhost:44350 thì thêm vào Secrets.config:
REM      <add key="Api:PublicBaseUrl" value="https://xxxx.trycloudflare.com" />
REM    rồi khởi động lại website.
REM  - Tắt cửa sổ này là đóng địa chỉ public. Mỗi lần chạy lại, địa chỉ đổi.
REM ======================================================================
cd /d "%~dp0"
cloudflared tunnel --url https://localhost:44350 --no-tls-verify --http-host-header localhost:44350
pause
