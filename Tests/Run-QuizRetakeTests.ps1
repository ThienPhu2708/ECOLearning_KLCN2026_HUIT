$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:exe /out:bin\QuizRetakeIntegration.exe /reference:bin\ECOLearning.dll /reference:bin\EntityFramework.dll /reference:bin\Newtonsoft.Json.dll /reference:System.ComponentModel.DataAnnotations.dll /reference:System.Web.dll .\Tests\QuizRetakeIntegration.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test runner compilation failed.' }
    Copy-Item Web.config bin/QuizRetakeIntegration.exe.config
    & ./bin/QuizRetakeIntegration.exe
    if ($LASTEXITCODE -ne 0) { throw 'Retake integration checks failed.' }
} finally {
    Remove-Item -LiteralPath bin\QuizRetakeIntegration.exe,bin\QuizRetakeIntegration.exe.config -ErrorAction SilentlyContinue
    Pop-Location
}
