param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$SourceRoot,
    [Parameter(Mandatory=$true)][string]$ControlDirectory,
    [Parameter(Mandatory=$true)][string]$Revision,
    [switch]$Hidden
)
$ErrorActionPreference = 'Stop'
# WSL can inherit an incomplete PATHEXT; keep native executables synchronous.
$env:PATHEXT = '.COM;.EXE;.BAT;.CMD;' + $env:PATHEXT
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { throw 'O runtime .NET Desktop 8 do Windows não foi encontrado.' }
$arguments = @($AssemblyPath, '--settings', '--development', $SourceRoot, '--dev-control', $ControlDirectory, '--dev-revision', $Revision)
if ($Hidden) { $arguments += '--dev-hidden' }
# Let the supervisor wait for the actual app shutdown. No Start-Process or force termination.
& $dotnet @arguments | Out-Default
exit $LASTEXITCODE
