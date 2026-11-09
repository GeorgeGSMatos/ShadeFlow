param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$env:PATHEXT = '.COM;.EXE;.BAT;.CMD;' + $env:PATHEXT
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    if (!$SkipBuild) {
        & "$PSScriptRoot\Build-Release.ps1" -SkipPackage
        & dotnet build Tests/ShadeFlow.Checks.csproj -c Release -o artifacts/checks --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar checks de logica.' }
        & dotnet build scripts/VisualChecks/VisualChecks.csproj -c Release -o artifacts/visual-checks --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar checks WPF.' }
    }

    & dotnet artifacts/checks/ShadeFlow.Checks.dll
    if ($LASTEXITCODE -ne 0) { throw 'Falha nos checks de logica.' }
    & dotnet artifacts/checks/ShadeFlow.Checks.dll --development-preview
    if ($LASTEXITCODE -ne 0) { throw 'Falha na separacao da previa.' }
    & dotnet artifacts/visual-checks/VisualChecks.dll artifacts/validation/ui
    if ($LASTEXITCODE -ne 0) { throw 'Falha nos checks WPF.' }

    Write-Host 'Validacao obrigatoria concluida. Integracoes fisicas dependem de conferencia manual.'
} finally { Pop-Location }
