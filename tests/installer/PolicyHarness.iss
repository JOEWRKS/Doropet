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
  Root, Reason: String;
  Gate1, Gate2: THandle;
  Paths: TArrayOfString;
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
  Log('POLICY PASS: 26 checks; returning False before installation');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then begin
    Log('UNSAFE_INSTALL_TRANSITION');
    RaiseException('Harness must never install');
  end;
end;
