@echo off
rem Job tinh goi y khoa hoc hang dem (Task Scheduler goi file nay).
rem Tham so 1 (tuy chon): duong dan python.exe; mac dinh dung python trong PATH.
rem Log: output\job.log
setlocal
cd /d "%~dp0"
set "PYTHON=%~1"
if "%PYTHON%"=="" set "PYTHON=python"
if not exist output mkdir output

echo ===== %date% %time% >> output\job.log
"%PYTHON%" build_recommendations.py --source sql >> output\job.log 2>&1
set "RC=%errorlevel%"
echo ===== ket thuc, ma thoat %RC% >> output\job.log
exit /b %RC%
