#ifndef BuildRoot
  #error Build only through tools/Build-Installer.ps1
#endif
#define ProductVersion "0.1.0"
#define ProductName "도로롱 (Dororong)"
#define ProductId "{928A9BC7-3A87-4C52-9161-1E5D08DB410A}"

[Setup]
AppId={{928A9BC7-3A87-4C52-9161-1E5D08DB410A}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppPublisher=JOEWRKS
VersionInfoVersion=0.1.0.0
VersionInfoProductVersion={#ProductVersion}
VersionInfoCompany=JOEWRKS
VersionInfoDescription=도로롱 사용자별 설치기
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DefaultDirName={localappdata}\Programs\JOEWRKS\Dororong
DisableDirPage=yes
UsePreviousAppDir=no
UsePreviousTasks=no
; A current explicit ownership manifest replaces historical file ownership.
; Appending would retain obsolete paths that users may subsequently reuse.
UninstallLogMode=overwrite
DisableProgramGroupPage=yes
UninstallDisplayName={#ProductName}
UninstallDisplayIcon={app}\Dororong.exe
SetupIconFile={#BuildRoot}\dororong.ico
CloseApplications=no
RestartApplications=no
RestartIfNeededByRun=no
AlwaysRestart=no
OutputDir={#BuildRoot}
OutputBaseFilename=Dororong-Setup-0.1.0-win-x64
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
#include BuildRoot + "\payload-files.iss"

[Icons]
Name: "{userprograms}\도로롱"; Filename: "{app}\Dororong.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\도로롱"; Filename: "{app}\Dororong.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Dororong.exe"; Description: "{cm:LaunchProgram,도로롱}"; Flags: nowait postinstall skipifsilent unchecked; Check: InstallationVerified

[Code]
#include "InstallerPolicy.iss"
#include BuildRoot + "\payload-policy.iss"

const
  RegistrationKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProductId}_is1';
  OwnershipFile = 'dororong-owned-files.txt';
  ShortcutFile = '도로롱.lnk';
var
  OperationGate: THandle;
  Incoming, IncomingHashes, Previous: TArrayOfString;
  InstallStarted, InstallVerified: Boolean;
  DesktopPreviouslyOwned, DesktopWillBeOwned: Boolean;

function FixedRoot: String;
begin
  Result := ExpandConstant('{localappdata}\Programs\JOEWRKS\Dororong');
end;

function SamePath(A, B: String): Boolean;
begin
  Result := CompareText(RemoveBackslashUnlessRoot(ExpandFileName(A)),
    RemoveBackslashUnlessRoot(ExpandFileName(B))) = 0;
end;

function ContainsPath(Paths: TArrayOfString; Value: String): Boolean;
var I: Integer;
begin
  Result := False;
  for I := 0 to GetArrayLength(Paths) - 1 do
    if CompareText(Paths[I], Value) = 0 then begin Result := True; Exit; end;
end;

function ReadOwnership(var Paths: TArrayOfString): Boolean;
var Lines: TArrayOfString; I, J: Integer; Size: Int64; Path: String;
begin
  Result := False;
  Path := AddBackslash(FixedRoot) + OwnershipFile;
  if not NoReparseComponents(Path) then Exit;
  if not FileSize64(Path, Size) then Exit;
  if (Size <= 0) or (Size > 1048576) then Exit;
  if not LoadStringsFromFile(Path, Lines) then Exit;
  if (GetArrayLength(Lines) = 0) or (GetArrayLength(Lines) > 10000) then Exit;
  for I := 0 to GetArrayLength(Lines) - 1 do begin
    if not SafeOwnedPath(Lines[I]) then Exit;
    for J := 0 to I - 1 do if CompareText(Lines[I], Lines[J]) = 0 then Exit;
  end;
  if not ContainsPath(Lines, 'Dororong.exe') or not ContainsPath(Lines, OwnershipFile) then Exit;
  Paths := Lines;
  Result := True;
end;

function ExistingIdentity(var Reason: String): Boolean;
var Version, Location, Name, Publisher, PEVersion: String; I: Integer; Registered: Boolean;
begin
  Result := False;
  Reason := 'Existing Dororong identity or ownership cannot be read safely. Repair or remove the existing product before retrying.';
  SetArrayLength(Previous, 0);
  Registered := RegKeyExists(HKCU64, RegistrationKey);
  if Registered then begin
    if not RegQueryStringValue(HKCU64, RegistrationKey, 'DisplayVersion', Version) then Exit;
    if not RegQueryStringValue(HKCU64, RegistrationKey, 'InstallLocation', Location) then Exit;
    if not RegQueryStringValue(HKCU64, RegistrationKey, 'DisplayName', Name) then Exit;
    if not RegQueryStringValue(HKCU64, RegistrationKey, 'Publisher', Publisher) then Exit;
    if not SamePath(Location, FixedRoot) or (Name <> '{#ProductName}') or (Publisher <> 'JOEWRKS') then Exit;
    if not VersionAllowed('{#ProductVersion}', Version) then begin
      Reason := 'Downgrade or malformed installed version refused. Uninstall the newer product first.'; Exit;
    end;
    if not ReadOwnership(Previous) then Exit;
    if FileExists(AddBackslash(FixedRoot) + 'Dororong.exe') then begin
      if not GetVersionNumbersString(AddBackslash(FixedRoot) + 'Dororong.exe', PEVersion) then Exit;
      if not VersionAllowed('{#ProductVersion}', PEVersion) then begin
        Reason := 'Downgrade refused by installed executable version.'; Exit;
      end;
      if not VersionAllowed(Version, PEVersion) or not VersionAllowed(PEVersion, Version) then Exit;
    end;
  end else begin
    { No portable import or collision overwrite in an unregistered folder. }
    for I := 0 to GetArrayLength(Incoming) - 1 do
      if FileExists(AddBackslash(FixedRoot) + Incoming[I]) or DirExists(AddBackslash(FixedRoot) + Incoming[I]) then Exit;
  end;
  Reason := ''; Result := True;
end;

function ReadShortcutOwnership(var Reason: String): Boolean;
var Value: String;
begin
  Result := False;
  DesktopPreviouslyOwned := False;
  if RegKeyExists(HKCU64, RegistrationKey) then begin
    { GetPreviousData is supported by both Setup and Uninstall in pinned Inno 7.1. }
    Value := GetPreviousData('DesktopIconOwned', '');
    if (Value <> '0') and (Value <> '1') then begin
      Reason := 'Shortcut ownership is unreadable. Repair Dororong before continuing.'; Exit;
    end;
    DesktopPreviouslyOwned := Value = '1';
  end;
  Result := True;
end;

function AllPreflight(var Reason: String): Boolean;
begin
  Result := False;
  if not NoReparseComponents(FixedRoot) then begin Reason := 'Installation path is unsafe or inaccessible.'; Exit; end;
  if not ExistingIdentity(Reason) then Exit;
  if not ReadShortcutOwnership(Reason) then Exit;
  if not PreflightOwnedFiles(FixedRoot, Previous, Reason) then Exit;
  if not PreflightOwnedFiles(FixedRoot, Incoming, Reason) then Exit;
  if not PreflightOwnedShortcuts(ExpandConstant('{userprograms}'), ExpandConstant('{userdesktop}'),
    ShortcutFile, DesktopPreviouslyOwned or DesktopWillBeOwned, Reason) then Exit;
  if not RegKeyExists(HKCU64, RegistrationKey) and
    FileExists(ExpandConstant('{userprograms}\') + ShortcutFile) then begin
    Reason := 'An unowned file occupies the Dororong Start menu shortcut path.'; Exit;
  end;
  if DesktopWillBeOwned and not DesktopPreviouslyOwned and
    FileExists(ExpandConstant('{userdesktop}\') + ShortcutFile) then begin
    Reason := 'An unowned file occupies the Dororong desktop shortcut path.'; Exit;
  end;
  Result := True;
end;

function BeginOperation(var Reason: String): Boolean;
begin
  Result := AcquireOperationGate(ExpandConstant('{localappdata}\Dororong-928A9BC7-operation.lock'), OperationGate);
  if not Result then Reason := 'Another Dororong setup or uninstall is active, or the operation lock is inaccessible.';
end;

function Refuse(Reason: String): Boolean;
begin
  Log('DORORONG REFUSED: ' + Reason);
  SuppressibleMsgBox(Reason, mbError, MB_OK, IDOK);
  Result := False;
end;

function InitializeSetup: Boolean;
var Reason, RequestedDirectory: String;
begin
  Result := False;
  LoadIncoming(Incoming, IncomingHashes);
  RequestedDirectory := ExpandConstant('{param:DIR|}');
  if (RequestedDirectory <> '') and not SamePath(RequestedDirectory, FixedRoot) then begin
    Refuse('Dororong must use its fixed per-user installation directory. /DIR overrides are refused.'); Exit;
  end;
  if not BeginOperation(Reason) then begin Refuse(Reason); Exit; end;
  if not AllPreflight(Reason) then begin Refuse(Reason); Exit; end;
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  DesktopWillBeOwned := WizardIsTaskSelected('desktopicon');
  if not SamePath(WizardDirValue, FixedRoot) then begin Result := 'Installation directory override refused.'; Exit; end;
  AllPreflight(Result);
end;

function InstallationVerified: Boolean;
begin
  Result := InstallVerified;
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  if WizardIsTaskSelected('desktopicon') then begin
    if not SetPreviousData(PreviousDataKey, 'DesktopIconOwned', '1') then
      RaiseException('Cannot record desktop shortcut ownership. Repair or reinstall is required.');
  end else begin
    if not SetPreviousData(PreviousDataKey, 'DesktopIconOwned', '0') then
      RaiseException('Cannot record desktop shortcut ownership. Repair or reinstall is required.');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Reason, Path: String; Obsolete: TArrayOfString; I, N: Integer;
begin
  if CurStep = ssInstall then begin
    DesktopWillBeOwned := WizardIsTaskSelected('desktopicon');
    if not SamePath(WizardDirValue, FixedRoot) then RaiseException('Installation directory override refused.');
    if not AllPreflight(Reason) then RaiseException(Reason);
    SetArrayLength(Obsolete, 0);
    for I := 0 to GetArrayLength(Previous) - 1 do
      if not ContainsPath(Incoming, Previous[I]) then begin
        N := GetArrayLength(Obsolete); SetArrayLength(Obsolete, N + 1); Obsolete[N] := Previous[I];
      end;
    InstallStarted := True;
    { Remove only previously owned shortcuts before payload mutation. Inno will
      recreate selected links; postinstall checks cannot mistake an old link for
      a successfully created link. A deselected desktop link stays removed. }
    if RegKeyExists(HKCU64, RegistrationKey) then begin
      if not RemoveOwnedShortcuts(ExpandConstant('{userprograms}'), ExpandConstant('{userdesktop}'),
        ShortcutFile, DesktopPreviouslyOwned, Reason) then RaiseException(Reason);
    end;
    if not RemoveOwnedFiles(FixedRoot, Obsolete, Reason) then RaiseException(Reason);
  end;
  if CurStep = ssPostInstall then begin
    InstallVerified := False;
    for I := 0 to GetArrayLength(Incoming) - 1 do begin
      Path := AddBackslash(FixedRoot) + Incoming[I];
      if not NoReparseComponents(Path) or not FileExists(Path) then RaiseException('Installed payload incomplete. Repair or reinstall is required.');
      if CompareText(GetSHA256OfFile(Path), IncomingHashes[I]) <> 0 then RaiseException('Installed payload verification failed. Repair or reinstall is required.');
    end;
    Path := ExpandConstant('{userprograms}\') + ShortcutFile;
    if not NoReparseComponents(Path) or not FileExists(Path) then
      RaiseException('Start menu shortcut creation failed. Repair or reinstall is required.');
    if DesktopWillBeOwned then begin
      Path := ExpandConstant('{userdesktop}\') + ShortcutFile;
      if not NoReparseComponents(Path) or not FileExists(Path) then
        RaiseException('Desktop shortcut creation failed. Repair or reinstall is required.');
    end;
    InstallVerified := True;
  end;
end;

function GetCustomSetupExitCode: Integer;
begin
  Result := 0;
  if not InstallVerified then Result := 10;
end;

procedure DeinitializeSetup;
begin
  if InstallStarted and not InstallVerified then
    SuppressibleMsgBox('Installation did not complete. Repair or reinstall Dororong before use.', mbError, MB_OK, IDOK);
  ReleaseOperationGate(OperationGate);
end;

function InitializeUninstall: Boolean;
var Reason: String;
begin
  Result := False;
  if not SamePath(ExpandConstant('{app}'), FixedRoot) then begin Refuse('Uninstall directory mismatch.'); Exit; end;
  if not BeginOperation(Reason) then begin Refuse(Reason); Exit; end;
  if not ReadOwnership(Previous) then begin Refuse('Ownership manifest unreadable. Repair Dororong before uninstalling.'); Exit; end;
  if not RegKeyExists(HKCU64, RegistrationKey) then begin Refuse('Product registration unreadable. Repair Dororong before uninstalling.'); Exit; end;
  if not ReadShortcutOwnership(Reason) then begin Refuse(Reason); Exit; end;
  if not PreflightProductFiles(FixedRoot, ExpandConstant('{userprograms}'), ExpandConstant('{userdesktop}'),
    ShortcutFile, Previous, DesktopPreviouslyOwned, Reason) then begin Refuse(Reason); Exit; end;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Reason: String;
begin
  if CurUninstallStep = usUninstall then begin
    { Recheck all locations before checked shortcut-then-payload removal. A
      failure here is fatal; Inno later processes registration and its metadata. }
    if not RemoveProductFiles(FixedRoot, ExpandConstant('{userprograms}'), ExpandConstant('{userdesktop}'),
      ShortcutFile, Previous, DesktopPreviouslyOwned, Reason) then RaiseException(Reason);
  end;
end;

procedure DeinitializeUninstall;
begin
  ReleaseOperationGate(OperationGate);
end;
