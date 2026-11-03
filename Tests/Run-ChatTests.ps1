$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & ./bin/roslyn/csc.exe /nologo /target:exe /out:bin/ChatIntegration.exe /reference:bin/ECOLearning.dll /reference:bin/EntityFramework.dll /reference:System.Web.dll /reference:System.ComponentModel.DataAnnotations.dll ./Tests/ChatIntegration.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    Copy-Item Web.config bin/ChatIntegration.exe.config
    & ./bin/ChatIntegration.exe
    if ($LASTEXITCODE -ne 0) { throw 'Chat checks failed' }
} finally {
    Remove-Item -LiteralPath bin/ChatIntegration.exe,bin/ChatIntegration.exe.config -ErrorAction SilentlyContinue
    Pop-Location
}
