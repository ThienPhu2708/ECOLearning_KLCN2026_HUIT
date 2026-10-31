$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:exe /out:bin\QuizWorkbookIntegration.exe /reference:bin\ECOLearning.dll /reference:bin\EntityFramework.dll /reference:bin\Newtonsoft.Json.dll /reference:System.ComponentModel.DataAnnotations.dll /reference:System.Web.dll /reference:System.Xml.Linq.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll .\Tests\QuizWorkbookIntegration.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test runner compilation failed.' }
    Copy-Item Web.config bin/QuizWorkbookIntegration.exe.config
    & ./bin/QuizWorkbookIntegration.exe
    if ($LASTEXITCODE -ne 0) { throw 'Workbook integration checks failed.' }
} finally {
    Remove-Item -LiteralPath bin\QuizWorkbookIntegration.exe,bin\QuizWorkbookIntegration.exe.config -ErrorAction SilentlyContinue
    Pop-Location
}
