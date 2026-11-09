param([switch]$SkipPackage)
$ErrorActionPreference = 'Stop'
$env:PATHEXT = '.COM;.EXE;.BAT;.CMD;' + $env:PATHEXT
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $version = ([xml](Get-Content ShadeFlow.csproj -Raw)).Project.PropertyGroup.Version
    & dotnet publish ShadeFlow.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "artifacts/release-$version/portable" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Falha no publish do ShadeFlow.' }
    if (!$SkipPackage) {
        & python scripts/package-release.py
        if ($LASTEXITCODE -ne 0) { throw 'Falha no empacotamento.' }
    }
} finally { Pop-Location }
