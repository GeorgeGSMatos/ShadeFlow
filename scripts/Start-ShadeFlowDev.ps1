param()
$ErrorActionPreference = 'Stop'
$env:PATHEXT = '.COM;.EXE;.BAT;.CMD;' + $env:PATHEXT
$projectRoot = Split-Path $PSScriptRoot -Parent
if ($projectRoot -notmatch '^\\\\(?:wsl\.localhost|wsl\$)\\([^\\]+)(\\.*)$') {
    throw 'Abra este iniciador na pasta do projeto dentro do WSL.'
}
$distribution = $Matches[1]
$linuxRoot = $Matches[2].Replace('\', '/')
$arguments = @('--distribution', $distribution, '--cd', $linuxRoot, '--', 'bash', 'scripts/dev.sh')
& wsl.exe @arguments
exit $LASTEXITCODE
