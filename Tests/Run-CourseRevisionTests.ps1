$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:exe /out:bin\CourseRevisionIntegration.exe /reference:bin\ECOLearning.dll /reference:bin\EntityFramework.dll /reference:bin\Newtonsoft.Json.dll /reference:System.ComponentModel.DataAnnotations.dll /reference:System.Web.dll .\Tests\CourseRevisionIntegration.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test runner compilation failed.' }
    Copy-Item Web.config bin/CourseRevisionIntegration.exe.config
    & ./bin/CourseRevisionIntegration.exe
    if ($LASTEXITCODE -ne 0) { throw 'Course revision integration checks failed.' }
} finally {
    Remove-Item -LiteralPath bin\CourseRevisionIntegration.exe,bin\CourseRevisionIntegration.exe.config -ErrorAction SilentlyContinue
    Pop-Location
}
