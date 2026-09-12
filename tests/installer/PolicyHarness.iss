[Setup]
AppName=Dororong Policy Test ONLY
AppVersion=0.0.0
PrivilegesRequired=lowest
SetupArchitecture=x64
DefaultDirName={#HarnessOutput}\must-never-install
OutputDir={#HarnessOutput}
OutputBaseFilename=PolicyHarness
Uninstallable=no
CreateUninstallRegKey=no
CloseApplications=no
RestartApplications=no
Compression=none

[Files]
Source: "{#SourcePath}PolicyHarness.iss"; DestDir: "{app}"

[Code]
#include "..\..\installer\InstallerPolicy.iss"

procedure Check(Value: Boolean; Description: String);
begin
  if not Value then RaiseException('POLICY FAIL: ' + Description);
end;

function InitializeSetup: Boolean;
var
  Root, Reason, RecordValue: String;
  Gate1, Gate2: THandle;
  Paths, Directories: TArrayOfString;
  RootOwned: Boolean;
begin
  Result := False;
  Check(VersionAllowed('0.1.10', '0.1.2'), 'numeric higher');
  Check(VersionAllowed('0.1.0', '0.1.0.0'), 'equal padding');
  Check(not VersionAllowed('0.1.2', '0.1.10'), 'downgrade');
  Check(not VersionAllowed('0.1.0', 'broken'), 'malformed installed');
  Check(not VersionAllowed('0.1.0', '0.1.'), 'empty component');
  Check(not VersionAllowed('0.1.0', '0.1.-1'), 'negative');
  Check(not VersionAllowed('0.1.0', '0.1.999999999999'), 'overflow');
  Check(not VersionAllowed('0.1.0', '0.1.0.0.0'), 'too many');
  Check(SafeOwnedPath('sub\owned.txt'), 'safe relative');
  Check(not SafeOwnedPath('..\user.txt'), 'traversal');
  Check(not SafeOwnedPath('C:\user.txt'), 'absolute');
  Check(not SafeOwnedPath('user.txt:stream'), 'ADS');
  Check(not SafeOwnedPath('sub\\file'), 'empty component');
  Check(not SafeOwnedPath('sub\file.'), 'trailing dot');
  Check(not SafeOwnedPath('NUL.txt'), 'device');
  Check(not SafeOwnedPath('sub\*'), 'wildcard');
  Root := ExpandConstant('{#HarnessOutput}\fixture');
  Check(NoReparseComponents(Root), 'normal root');
  Check(not NoReparseComponents(Root + '\linked\child'), 'reparse ancestor');
  Gate1 := 0; Gate2 := 0;
  Check(AcquireOperationGate(Root + '\operation.lock', Gate1), 'first gate');
  try
    Check(not AcquireOperationGate(Root + '\operation.lock', Gate2), 'exclusive gate');
  finally
    ReleaseOperationGate(Gate1);
    ReleaseOperationGate(Gate2);
  end;
  SetArrayLength(Paths, 1); Paths[0] := 'owned.txt';
  Check(not PreflightOwnedFiles(Root, Paths, Reason), 'locked file');
  Paths[0] := 'linked\file.txt';
  Check(not PreflightOwnedFiles(Root, Paths, Reason), 'reparse file');
  SetArrayLength(Paths, 1); Paths[0] := 'obsolete.txt';
  Check(RemoveOwnedFiles(Root, Paths, Reason), 'explicit removal');
  Check(not FileExists(Root + '\obsolete.txt'), 'obsolete removed');
  Check(FileExists(Root + '\user-added.txt'), 'user file preserved');
  Paths[0] := '..\user-added.txt';
  Check(not RemoveOwnedFiles(Root, Paths, Reason), 'malicious removal rejected');
  Paths[0] := 'preserved-payload.txt';
  Check(not RemoveProductFiles(Root, Root + '\programs-locked', Root + '\desktop-clear',
    'owned.lnk', Paths, False, Reason), 'locked start-menu shortcut refuses removal');
  Check(FileExists(Root + '\preserved-payload.txt'), 'start-menu refusal preserves payload');
  Check(FileExists(Root + '\programs-locked\owned.lnk'), 'locked start-menu shortcut preserved');
  Check(not RemoveProductFiles(Root, Root + '\programs-clear', Root + '\desktop-locked',
    'owned.lnk', Paths, True, Reason), 'locked owned desktop shortcut refuses removal');
  Check(FileExists(Root + '\preserved-payload.txt'), 'desktop refusal preserves payload');
  Check(FileExists(Root + '\programs-clear\owned.lnk'), 'desktop refusal preserves start-menu shortcut');
  Check(RemoveProductFiles(Root, Root + '\programs-clear', Root + '\desktop-locked',
    'owned.lnk', Paths, False, Reason), 'unowned desktop shortcut excluded');
  Check(FileExists(Root + '\desktop-locked\owned.lnk'), 'unowned desktop shortcut preserved');
  Check(not FileExists(Root + '\preserved-payload.txt'), 'unlocked payload removed');
  Paths[0] := 'second-payload.txt';
  Check(RemoveProductFiles(Root, Root + '\programs-clear', Root + '\desktop-clear',
    'owned.lnk', Paths, True, Reason), 'owned unlocked shortcut and payload removal');
  Check(not FileExists(Root + '\desktop-clear\owned.lnk'), 'owned desktop shortcut removed');
  { The normal-host candidate05 lifecycle test is the RED regression: its
    overwrite reinstall loses directory records. Exercise the new directory
    policy here without installing a product or touching user locations. }
  Check(DecodeDirectoryOwnership('', Directories, RootOwned), 'legacy ownership accepted conservatively');
  Check((GetArrayLength(Directories) = 0) and not RootOwned, 'legacy does not claim existing directories');
  ForceDirectories(Root + '\directory-case\preexisting');
  SetArrayLength(Paths, 3);
  Paths[0] := 'new\nested\payload.txt';
  Paths[1] := 'preexisting\payload.txt';
  Paths[2] := 'top.txt';
  Check(CaptureDirectoryOwnership(Root + '\directory-case', Paths, Directories, RootOwned, Reason), 'capture missing directories');
  Check(not RootOwned and (GetArrayLength(Directories) = 2), 'existing root and directory not claimed');
  RecordValue := EncodeDirectoryOwnership(Directories, RootOwned);
  ForceDirectories(Root + '\directory-case\new\nested');
  Check(DecodeDirectoryOwnership(RecordValue, Directories, RootOwned), 'ownership survives serialization');
  Check(CaptureDirectoryOwnership(Root + '\directory-case', Paths, Directories, RootOwned, Reason), 'reinstall preserves ownership');
  Check(GetArrayLength(Directories) = 2, 'reinstall retains two existing owned directories');
  ForceDirectories(Root + '\directory-case\new\user-empty');
  Check(RemoveOwnedDirectories(Root + '\directory-case', Directories, Reason), 'empty-only removal');
  Check(not DirExists(Root + '\directory-case\new\nested'), 'owned nested empty directory removed');
  Check(DirExists(Root + '\directory-case\new\user-empty'), 'user empty directory preserved');
  Check(DirExists(Root + '\directory-case\preexisting'), 'pre-existing empty directory preserved');
  Check(RemoveDir(Root + '\directory-case\new\user-empty'), 'fixture removes its own empty user directory');
  Check(RemoveOwnedDirectories(Root + '\directory-case', Directories, Reason), 'missing child accepted');
  Check(not DirExists(Root + '\directory-case\new'), 'owned parent removed bottom-up');
  Check(DecodeDirectoryOwnership('1|1|filled', Directories, RootOwned), 'root ownership roundtrip');
  Check(RootOwned, 'root creation ownership retained');
  ForceDirectories(Root + '\directory-case\filled');
  SaveStringToFile(Root + '\directory-case\filled\user.txt', 'preserve', False);
  Check(RemoveOwnedDirectories(Root + '\directory-case', Directories, Reason), 'nonempty directory is not an error');
  Check(FileExists(Root + '\directory-case\filled\user.txt'), 'user file retained');
  Check(not DecodeDirectoryOwnership('1|1|..\escape', Directories, RootOwned), 'directory traversal refused');
  Check(not DecodeDirectoryOwnership('1|0|one|ONE', Directories, RootOwned), 'duplicate ownership refused');
  Check(not DecodeDirectoryOwnership('2|1', Directories, RootOwned), 'unknown schema refused');
  Check(not DecodeDirectoryOwnership('1|yes', Directories, RootOwned), 'invalid root ownership refused');
  SetArrayLength(Directories, 2); Directories[0] := 'directory-case\preexisting'; Directories[1] := 'linked';
  Check(not RemoveOwnedDirectories(Root, Directories, Reason), 'reparse directory refused before cleanup');
  Check(DirExists(Root + '\directory-case\preexisting'), 'full directory preflight precedes removal');
  Check(DecodeDirectoryOwnership('', Directories, RootOwned), 'reset fresh fixture ownership');
  SetArrayLength(Paths, 1); Paths[0] := 'nested\payload.txt';
  Check(CaptureDirectoryOwnership(Root + '\fresh-directory-case', Paths, Directories, RootOwned, Reason), 'fresh absent root captured');
  Check(RootOwned and (GetArrayLength(Directories) = 1), 'fresh root and payload parent are owned');
  ForceDirectories(Root + '\fresh-directory-case\nested');
  Check(RemoveOwnedDirectories(Root + '\fresh-directory-case', Directories, Reason), 'fresh child cleanup');
  Check(DirExists(Root + '\fresh-directory-case'), 'policy leaves root for native uninstaller metadata cleanup');
  Log('POLICY PASS: directory lifecycle and existing file/shortcut checks; returning False before installation');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then begin
    Log('UNSAFE_INSTALL_TRANSITION');
    RaiseException('Harness must never install');
  end;
end;
