$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & ./bin/roslyn/csc.exe /nologo /target:exe /out:bin/AdminActivityIntegration.exe /reference:bin/ECOLearning.dll /reference:bin/EntityFramework.dll /reference:System.ComponentModel.DataAnnotations.dll ./Tests/AdminActivityIntegration.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    Copy-Item Web.config bin/AdminActivityIntegration.exe.config
    & ./bin/AdminActivityIntegration.exe
    if ($LASTEXITCODE -ne 0) { throw 'Admin activity checks failed' }
} finally {
    Remove-Item -LiteralPath bin/AdminActivityIntegration.exe,bin/AdminActivityIntegration.exe.config -ErrorAction SilentlyContinue
    Pop-Location
}
