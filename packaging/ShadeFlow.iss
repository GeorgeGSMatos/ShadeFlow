#ifndef ReleaseDir
  #define ReleaseDir "..\artifacts\release-1.2.1"
#endif
#define AppVersion "1.2.1"

[Setup]
AppId={{77C13C2E-7FA8-4B5E-A9E7-83F36C4764F2}
AppName=ShadeFlow
AppVersion={#AppVersion}
AppVerName=ShadeFlow {#AppVersion}
VersionInfoVersion=1.2.1.0
AppPublisher=George GS Matos
VersionInfoCompany=George GS Matos
VersionInfoCopyright=Copyright © 2026 George GS Matos. All rights reserved.
DefaultDirName={localappdata}\Programs\ShadeFlow
DefaultGroupName=ShadeFlow
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\ShadeFlow.exe
SetupIconFile=..\Assets\ShadeFlow.ico
OutputDir={#ReleaseDir}
OutputBaseFilename=ShadeFlow-1.2.1-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; Flags: unchecked

[Files]
Source: "{#ReleaseDir}\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{userprograms}\ShadeFlow"; Filename: "{app}\ShadeFlow.exe"; AppUserModelID: "ShadeFlow.App"
Name: "{userdesktop}\ShadeFlow"; Filename: "{app}\ShadeFlow.exe"; Tasks: desktopicon; AppUserModelID: "ShadeFlow.App"

[Run]
Filename: "{app}\ShadeFlow.exe"; Description: "Abrir ShadeFlow"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

// Query the existing per-user mutex without activating or terminating ShadeFlow.
// A failure is closed: never replace application files while ownership is uncertain.
function ApplicationStopped(): Boolean;
var
  Script, Params: String;
  ResultCode: Integer;
begin
  Script := 'try { $sid=[System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value; ' +
    '$sha=[System.Security.Cryptography.SHA256]::Create(); ' +
    '$hash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($sid))).Replace(''-'','''').Substring(0,24); ' +
    '$sha.Dispose(); try { $m=[Threading.Mutex]::OpenExisting(''Local\ShadeFlow.''+$hash); $m.Dispose(); exit 10 } ' +
    'catch [Threading.WaitHandleCannotBeOpenedException] { exit 0 } } catch { exit 20 }';
  Params := '-NoProfile -NonInteractive -Command "' + Script + '"';
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not ApplicationStopped() then
    Result := 'Escolha Sair no ícone do ShadeFlow na bandeja e tente novamente. Se já estiver fechado, verifique se o PowerShell do Windows está disponível.';
end;

function InitializeUninstall(): Boolean;
begin
  Result := ApplicationStopped();
  if not Result then
    SuppressibleMsgBox('Escolha Sair no ShadeFlow antes de desinstalar. Nenhum processo será encerrado à força.', mbError, MB_OK, IDOK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Value, Expected: String;
begin
  if CurUninstallStep = usUninstall then begin
    Expected := '"' + ExpandConstant('{app}\ShadeFlow.exe') + '" --startup';
    if RegQueryStringValue(HKCU, RunKey, 'ShadeFlow', Value) and
      (CompareText(Value, Expected) = 0) then
      RegDeleteValue(HKCU, RunKey, 'ShadeFlow');
  end;
  // Settings and logs live outside {app}; preserve them during uninstall/update.
end;
