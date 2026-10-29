$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:exe /out:bin\TeacherSpecialtyIntegration.exe /reference:bin\ECOLearning.dll /reference:bin\EntityFramework.dll /reference:bin\Newtonsoft.Json.dll /reference:System.ComponentModel.DataAnnotations.dll /reference:System.Web.dll /reference:bin\System.Web.Mvc.dll .\Tests\TeacherSpecialtyIntegration.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test runner compilation failed.' }
    Copy-Item Web.config bin/TeacherSpecialtyIntegration.exe.config
    & ./bin/TeacherSpecialtyIntegration.exe
    if ($LASTEXITCODE -ne 0) { throw 'Course revision integration checks failed.' }
} finally {
    Remove-Item -LiteralPath bin\TeacherSpecialtyIntegration.exe,bin\TeacherSpecialtyIntegration.exe.config -ErrorAction SilentlyContinue
    Pop-Location
}
