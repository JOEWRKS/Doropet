{ Shared by the real installer and a separate, non-installing compiled harness. }
function PolicyCreateFile(Name: String; Access, Share: LongWord; Security: NativeUInt;
  Creation, Flags: LongWord; Template: THandle): THandle;
  external 'CreateFileW@kernel32.dll stdcall';
function PolicyCloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';
function PolicyAttributes(Name: String): LongWord;
  external 'GetFileAttributesW@kernel32.dll stdcall';
function PolicyLastError: LongWord;
  external 'GetLastError@kernel32.dll stdcall';

function ReadNumericVersion(Value: String; var Parts: TArrayOfInteger): Boolean;
var I, Part, Number, Digit: Integer;
begin
  Result := False;
  SetArrayLength(Parts, 4);
  for I := 0 to 3 do Parts[I] := 0;
  Part := 0; Number := 0;
  if (Value = '') or (Value[Length(Value)] = '.') then Exit;
  for I := 1 to Length(Value) do begin
    if Value[I] = '.' then begin
      if (I = 1) or (Value[I-1] = '.') or (Part = 3) then Exit;
      Parts[Part] := Number; Part := Part + 1; Number := 0;
    end else begin
      if (Value[I] < '0') or (Value[I] > '9') then Exit;
      Digit := Ord(Value[I]) - Ord('0');
      if Number > 6553 then Exit;
      Number := Number * 10 + Digit;
      if Number > 65535 then Exit;
    end;
  end;
  if Part < 2 then Exit;
  Parts[Part] := Number;
  Result := True;
end;

function VersionAllowed(Incoming, Installed: String): Boolean;
var A, B: TArrayOfInteger; I: Integer;
begin
  Result := False;
  if not ReadNumericVersion(Incoming, A) then Exit;
  if not ReadNumericVersion(Installed, B) then Exit;
  for I := 0 to 3 do begin
    if A[I] > B[I] then begin Result := True; Exit; end;
    if A[I] < B[I] then Exit;
  end;
  Result := True;
end;

function SafeOwnedPath(Value: String): Boolean;
var I, Start, Dot: Integer; Component, Stem: String;
begin
  Result := False;
  if (Value = '') or (Length(Value) > 220) then Exit;
  Start := 1;
  for I := 1 to Length(Value) + 1 do begin
    if I <= Length(Value) then
      if (Ord(Value[I]) < 32) or (Pos(Value[I], '/:*?"<>|{};') > 0) then Exit;
    if (I > Length(Value)) or (Value[I] = '\') then begin
      Component := Copy(Value, Start, I - Start);
      if (Component = '') or (Component = '.') or (Component = '..') then Exit;
      if (Component[Length(Component)] = '.') or (Component[Length(Component)] = ' ') then Exit;
      Stem := Uppercase(Component); Dot := Pos('.', Stem);
      if Dot > 0 then Stem := Copy(Stem, 1, Dot - 1);
      if (Stem = 'CON') or (Stem = 'PRN') or (Stem = 'AUX') or (Stem = 'NUL') then Exit;
      if (Length(Stem) = 4) and ((Copy(Stem,1,3) = 'COM') or (Copy(Stem,1,3) = 'LPT')) then
        if (Stem[4] >= '0') and (Stem[4] <= '9') then Exit;
      Start := I + 1;
    end;
  end;
  Result := True;
end;

function NoReparseComponents(Path: String): Boolean;
var Attr, Err: LongWord; Parent: String;
begin
  Result := False;
  while Path <> '' do begin
    Attr := PolicyAttributes(Path);
    if Attr = $FFFFFFFF then begin
      Err := PolicyLastError;
      if (Err <> 2) and (Err <> 3) then Exit;
    end else if (Attr and $400) <> 0 then Exit;
    Parent := ExtractFileDir(Path);
    if Parent = Path then Break;
    Path := Parent;
  end;
  Result := True;
end;

#include "InstallerDirectories.iss"

function AcquireOperationGate(Path: String; var Handle: THandle): Boolean;
begin
  Result := False;
  Handle := 0;
  if not NoReparseComponents(Path) then Exit;
  { OPEN_ALWAYS, share=0, DELETE_ON_CLOSE: process/crash close releases the gate.
    The caller supplies a per-user path outside the installed payload. }
  Handle := PolicyCreateFile(Path, $C0010000, 0, 0, 4, $04000000, 0);
  if Handle = THandle(-1) then begin Handle := 0; Exit; end;
  Result := True;
end;

procedure ReleaseOperationGate(var Handle: THandle);
begin
  if Handle <> 0 then PolicyCloseHandle(Handle);
  Handle := 0;
end;

function PreflightOwnedFiles(Root: String; Paths: TArrayOfString; var Reason: String): Boolean;
var I: Integer; Path: String; Attr, Err: LongWord; Handle: THandle;
begin
  Result := False;
  Reason := 'Unsafe installation path, unreadable file, or file in use. Close Dororong in all sessions and retry.';
  if not NoReparseComponents(Root) then Exit;
  for I := 0 to GetArrayLength(Paths) - 1 do begin
    if not SafeOwnedPath(Paths[I]) then Exit;
    Path := AddBackslash(Root) + Paths[I];
    if not NoReparseComponents(Path) then Exit;
    Attr := PolicyAttributes(Path);
    if Attr = $FFFFFFFF then begin
      Err := PolicyLastError;
      if (Err <> 2) and (Err <> 3) then Exit;
    end else begin
      if (Attr and $10) <> 0 then Exit;
      { Request write/delete access without sharing. Mapped executables and
        sharing conflicts are refused across sessions; no process is killed. }
      Handle := PolicyCreateFile(Path, $C0010000, 0, 0, 3, $80, 0);
      if Handle = THandle(-1) then Exit;
      PolicyCloseHandle(Handle);
    end;
  end;
  Reason := '';
  Result := True;
end;

function RemoveOwnedFiles(Root: String; Paths: TArrayOfString; var Reason: String): Boolean;
var I: Integer; Path: String; Attr, Err: LongWord;
begin
  Result := False;
  { Validate the entire list before touching any entry. Never recurse. }
  if not PreflightOwnedFiles(Root, Paths, Reason) then Exit;
  for I := 0 to GetArrayLength(Paths) - 1 do begin
    Path := AddBackslash(Root) + Paths[I];
    if not NoReparseComponents(Path) then begin Reason := 'Unsafe path changed during removal.'; Exit; end;
    Attr := PolicyAttributes(Path);
    if Attr = $FFFFFFFF then begin
      Err := PolicyLastError;
      if (Err <> 2) and (Err <> 3) then begin
        Reason := 'File became inaccessible during removal. Repair or reinstall may be required.';
        Exit;
      end;
    end else begin
      if not DeleteFile(Path) then begin
        Reason := 'File removal failed. Close Dororong in all sessions. Repair or reinstall may be required: ' + Paths[I];
        Exit;
      end;
    end;
  end;
  Result := True;
end;

function PreflightOwnedShortcuts(ProgramsRoot, DesktopRoot, ShortcutName: String;
  DesktopOwned: Boolean; var Reason: String): Boolean;
var Shortcuts: TArrayOfString;
begin
  Result := False;
  SetArrayLength(Shortcuts, 1); Shortcuts[0] := ShortcutName;
  if not PreflightOwnedFiles(ProgramsRoot, Shortcuts, Reason) then Exit;
  if DesktopOwned then
    if not PreflightOwnedFiles(DesktopRoot, Shortcuts, Reason) then Exit;
  Result := True;
end;

function PreflightProductFiles(PayloadRoot, ProgramsRoot, DesktopRoot, ShortcutName: String;
  Paths: TArrayOfString; DesktopOwned: Boolean; var Reason: String): Boolean;
begin
  Result := False;
  if not PreflightOwnedFiles(PayloadRoot, Paths, Reason) then Exit;
  if not PreflightOwnedShortcuts(ProgramsRoot, DesktopRoot, ShortcutName, DesktopOwned, Reason) then Exit;
  Result := True;
end;

function RemoveOwnedShortcuts(ProgramsRoot, DesktopRoot, ShortcutName: String;
  DesktopOwned: Boolean; var Reason: String): Boolean;
var Shortcuts: TArrayOfString;
begin
  Result := False;
  if not PreflightOwnedShortcuts(ProgramsRoot, DesktopRoot, ShortcutName, DesktopOwned, Reason) then Exit;
  SetArrayLength(Shortcuts, 1); Shortcuts[0] := ShortcutName;
  if not RemoveOwnedFiles(ProgramsRoot, Shortcuts, Reason) then Exit;
  if DesktopOwned then
    if not RemoveOwnedFiles(DesktopRoot, Shortcuts, Reason) then Exit;
  Result := True;
end;

function RemoveProductFiles(PayloadRoot, ProgramsRoot, DesktopRoot, ShortcutName: String;
  Paths: TArrayOfString; DesktopOwned: Boolean; var Reason: String): Boolean;
begin
  Result := False;
  { All three locations must pass before the first removal. Actual shortcut
    deletion errors propagate before any payload deletion, rather than being
    left for Inno's non-fatal uninstall-log processing. }
  if not PreflightProductFiles(PayloadRoot, ProgramsRoot, DesktopRoot, ShortcutName, Paths, DesktopOwned, Reason) then Exit;
  if not RemoveOwnedShortcuts(ProgramsRoot, DesktopRoot, ShortcutName, DesktopOwned, Reason) then Exit;
  Result := RemoveOwnedFiles(PayloadRoot, Paths, Reason);
end;
