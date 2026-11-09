$ErrorActionPreference = 'Stop'
$updateMutex = $null
$ownsMutex = $false
$copied = $false
$shortcutBackups = @()
$startupChanged = $false
try {
    # This updater is run explicitly from an extracted portable package. All writes
    # stay in this user's ShadeFlow installation/backup, existing shortcuts and Run value.
    $sourceDir = [IO.Path]::GetFullPath($PSScriptRoot)
    $installDir = Join-Path $env:LOCALAPPDATA 'Programs\ShadeFlow'
    $targetExe = Join-Path $installDir 'ShadeFlow.exe'
    if ($sourceDir.TrimEnd('\') -eq $installDir.TrimEnd('\')) { throw 'Extraia o pacote em outra pasta antes de atualizar.' }
    $payload = Join-Path $sourceDir 'ShadeFlow.exe'
    if (!(Test-Path $payload) -or (Get-Item $payload).VersionInfo.FileVersion -ne '1.2.1.0') { throw 'Pacote ShadeFlow 1.2.1 incompleto ou diferente do esperado.' }
    if (!(Test-Path (Join-Path $sourceDir 'ShadeFlow.runtimeconfig.json'))) { throw 'Extraia o ZIP completo antes de atualizar.' }
    if (Get-Process ShadeFlow -ErrorAction SilentlyContinue) { throw 'Escolha Sair no tray do ShadeFlow e execute novamente. Nenhum processo foi encerrado.' }
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $scope = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($sid))).Replace('-','').Substring(0,24) }
    finally { $sha.Dispose() }
    $created = $false
    $updateMutex = [Threading.Mutex]::new($true, ('Local\ShadeFlow.' + $scope), [ref]$created)
    $ownsMutex = $created
    if (!$ownsMutex) { throw 'ShadeFlow iniciou durante a verificacao. Escolha Sair e tente novamente.' }

    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $runValue = Get-ItemPropertyValue -Path $runKey -Name ShadeFlow -ErrorAction SilentlyContinue
    $backupDir = Join-Path $env:LOCALAPPDATA ('ShadeFlow\ApplicationBackups\' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    if (Test-Path $installDir) { Copy-Item -Path $installDir -Destination (Join-Path $backupDir 'installation') -Recurse }
    @{ startup = $runValue } | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $backupDir 'startup.json')
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    $copied = $true
    Get-ChildItem -LiteralPath $sourceDir | Where-Object { $_.Name -notlike 'Atualizar-ShadeFlow.*' } |
        Copy-Item -Destination $installDir -Recurse -Force

    # Preserve whether startup was enabled, changing only the existing ShadeFlow value.
    if (![string]::IsNullOrWhiteSpace($runValue)) {
        Set-ItemProperty -Path $runKey -Name ShadeFlow -Value ('"' + $targetExe + '" --startup')
        $startupChanged = $true
    }
    $shell = New-Object -ComObject WScript.Shell
    $shortcutRoots = @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'),
        (Join-Path $env:APPDATA 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar'))
    foreach ($root in $shortcutRoots) {
        if (!$root -or !(Test-Path $root)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $root -Filter '*.lnk' -Recurse) {
            $shortcut = $shell.CreateShortcut($file.FullName)
            if ([IO.Path]::GetFileName($shortcut.TargetPath) -ine 'ShadeFlow.exe') { continue }
            $backupName = [Guid]::NewGuid().ToString('N') + '.lnk'
            Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $backupDir $backupName)
            $shortcutBackups += @{ original = $file.FullName; backup = (Join-Path $backupDir $backupName) }
            ConvertTo-Json -InputObject $shortcutBackups | Set-Content -Encoding UTF8 (Join-Path $backupDir 'shortcuts.json')
            $shortcut.TargetPath = $targetExe
            $shortcut.WorkingDirectory = $installDir
            $shortcut.IconLocation = $targetExe + ',0'
            $shortcut.Save()
        }
    }
    $startLink = Join-Path ([Environment]::GetFolderPath('Programs')) 'ShadeFlow.lnk'
    if (!(Test-Path $startLink)) {
        $shortcut = $shell.CreateShortcut($startLink)
        $shortcut.TargetPath = $targetExe
        $shortcut.WorkingDirectory = $installDir
        $shortcut.Save()
    }
    Write-Host ('ShadeFlow atualizado em: ' + $installDir)
    Write-Host ('Backup da instalacao anterior: ' + $backupDir)
    Write-Host 'Aliases, presets, horarios e preferencias foram preservados.'
    $updateMutex.ReleaseMutex()
    $ownsMutex = $false
    $updateMutex.Dispose()
    $updateMutex = $null
    Start-Process -FilePath $targetExe
} catch {
    Write-Host ('Atualizacao interrompida: ' + $_.Exception.Message) -ForegroundColor Red
    # Restore overwritten installation files and machine references on a partial failure.
    try {
        if ($copied -and (Test-Path (Join-Path $backupDir 'installation'))) {
            Get-ChildItem -LiteralPath (Join-Path $backupDir 'installation') | Copy-Item -Destination $installDir -Recurse -Force
        }
        if ($startupChanged) { Set-ItemProperty -Path $runKey -Name ShadeFlow -Value $runValue }
        foreach ($entry in $shortcutBackups) { Copy-Item -LiteralPath $entry.backup -Destination $entry.original -Force }
    } catch { Write-Host ('Falha ao restaurar automaticamente o backup: ' + $_.Exception.Message) -ForegroundColor Red }
    Write-Host 'Os backups, quando criados, permanecem em %LOCALAPPDATA%\ShadeFlow\ApplicationBackups.'
    exit 1
} finally {
    if ($updateMutex) {
        if ($ownsMutex) { $updateMutex.ReleaseMutex() }
        $updateMutex.Dispose()
    }
}
