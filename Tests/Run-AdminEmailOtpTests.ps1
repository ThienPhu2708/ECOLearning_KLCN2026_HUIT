$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & ./bin/roslyn/csc.exe /nologo /target:exe /out:bin/AdminEmailOtpTests.exe /reference:bin/ECOLearning.dll /reference:System.Web.dll ./Tests/AdminEmailOtpTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    & ./bin/AdminEmailOtpTests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Admin email OTP checks failed' }
} finally {
    Remove-Item -LiteralPath bin/AdminEmailOtpTests.exe -ErrorAction SilentlyContinue
    Pop-Location
}
