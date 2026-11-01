$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:exe /out:bin\QuizAssessmentIntegration.exe /reference:bin\ECOLearning.dll /reference:bin\EntityFramework.dll /reference:bin\Newtonsoft.Json.dll /reference:System.ComponentModel.DataAnnotations.dll /reference:System.Web.dll .\Tests\QuizAssessmentIntegration.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test runner compilation failed.' }
    Copy-Item Web.config bin/QuizAssessmentIntegration.exe.config
    & ./bin/QuizAssessmentIntegration.exe
    if ($LASTEXITCODE -ne 0) { throw 'Assessment integration checks failed.' }
} finally {
    Remove-Item -LiteralPath bin\QuizAssessmentIntegration.exe,bin\QuizAssessmentIntegration.exe.config -ErrorAction SilentlyContinue
    Pop-Location
}
