$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $compiler = Join-Path (Get-Location) 'bin\roslyn\csc.exe'
    & $compiler /nologo /target:exe /out:bin\QuizMobileApiIntegration.exe /reference:bin\ECOLearning.dll /reference:bin\EntityFramework.dll /reference:bin\Newtonsoft.Json.dll /reference:System.ComponentModel.DataAnnotations.dll /reference:System.Web.dll /reference:bin\System.Web.Mvc.dll .\Tests\QuizMobileApiIntegration.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test runner compilation failed.' }
    Copy-Item Web.config bin/QuizMobileApiIntegration.exe.config
    & ./bin/QuizMobileApiIntegration.exe
    if ($LASTEXITCODE -ne 0) { throw 'Mobile API integration checks failed.' }
} finally {
    Remove-Item -LiteralPath bin\QuizMobileApiIntegration.exe,bin\QuizMobileApiIntegration.exe.config -ErrorAction SilentlyContinue
    Pop-Location
}
