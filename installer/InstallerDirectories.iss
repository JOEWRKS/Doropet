{ Directory ownership is independent of the replaceable file uninstall log.
  Only missing directories (or previously recorded ones) are claimed. Legacy
  installers have no directory record: existing directories stay unclaimed. }
function DirectoryListed(Paths: TArrayOfString; Value: String): Boolean;
var I: Integer;
begin
  Result := False;
  for I := 0 to GetArrayLength(Paths) - 1 do
    if CompareText(Paths[I], Value) = 0 then begin Result := True; Exit; end;
end;

function DecodeDirectoryOwnership(Value: String; var Paths: TArrayOfString;
  var RootOwned: Boolean): Boolean;
var Item: String; P, N: Integer;
begin
  Result := False; RootOwned := False; SetArrayLength(Paths, 0);
  if Value = '' then begin Result := True; Exit; end;
  if (Length(Value) > 65535) or (Copy(Value, 1, 2) <> '1|') then Exit;
  if (Length(Value) < 3) or ((Value[3] <> '0') and (Value[3] <> '1')) then Exit;
  RootOwned := Value[3] = '1';
  Delete(Value, 1, 3);
  while Value <> '' do begin
    if Value[1] <> '|' then Exit;
    Delete(Value, 1, 1);
    P := Pos('|', Value); if P = 0 then P := Length(Value) + 1;
    Item := Copy(Value, 1, P - 1); Delete(Value, 1, P - 1);
    if not SafeOwnedPath(Item) or DirectoryListed(Paths, Item) then Exit;
    N := GetArrayLength(Paths); if N >= 1000 then Exit;
    SetArrayLength(Paths, N + 1); Paths[N] := Item;
  end;
  Result := True;
end;

function EncodeDirectoryOwnership(Paths: TArrayOfString; RootOwned: Boolean): String;
var I: Integer;
begin
  Result := '1|0'; if RootOwned then Result := '1|1';
  for I := 0 to GetArrayLength(Paths) - 1 do Result := Result + '|' + Paths[I];
end;

function PreflightOwnedDirectories(Root: String; Paths: TArrayOfString; var Reason: String): Boolean;
var I: Integer; Path: String; Attr, Err: LongWord;
begin
  Result := False; Reason := 'Directory ownership path is unsafe or inaccessible.';
  if not NoReparseComponents(Root) then Exit;
  for I := 0 to GetArrayLength(Paths) - 1 do begin
    if not SafeOwnedPath(Paths[I]) then Exit;
    Path := AddBackslash(Root) + Paths[I];
    if not NoReparseComponents(Path) then Exit;
    Attr := PolicyAttributes(Path);
    if Attr = $FFFFFFFF then begin
      Err := PolicyLastError; if (Err <> 2) and (Err <> 3) then Exit;
    end else if (Attr and $10) = 0 then Exit;
  end;
  Reason := ''; Result := True;
end;

function CaptureDirectoryOwnership(Root: String; Files: TArrayOfString;
  var Paths: TArrayOfString; var RootOwned: Boolean; var Reason: String): Boolean;
var I, N: Integer; Parent: String; Checked: TArrayOfString; CheckedRoot: Boolean;
begin
  Result := False;
  if not PreflightOwnedDirectories(Root, Paths, Reason) then Exit;
  if FileExists(Root) then begin Reason := 'Application directory is occupied by a file.'; Exit; end;
  RootOwned := RootOwned or not DirExists(Root);
  for I := 0 to GetArrayLength(Files) - 1 do begin
    if not SafeOwnedPath(Files[I]) then begin Reason := 'Invalid incoming path.'; Exit; end;
    Parent := ExtractFileDir(Files[I]);
    while Parent <> '' do begin
      if not DirExists(AddBackslash(Root) + Parent) and not DirectoryListed(Paths, Parent) then begin
        N := GetArrayLength(Paths); SetArrayLength(Paths, N + 1); Paths[N] := Parent;
      end;
      Parent := ExtractFileDir(Parent);
    end;
  end;
  { Enforce the same bounds before saving as when restoring the record. }
  if not DecodeDirectoryOwnership(EncodeDirectoryOwnership(Paths, RootOwned), Checked, CheckedRoot) then begin
    Reason := 'Directory ownership exceeds supported limits.'; Exit;
  end;
  Result := PreflightOwnedDirectories(Root, Paths, Reason);
end;

function RemoveOwnedDirectories(Root: String; Paths: TArrayOfString; var Reason: String): Boolean;
var I, J: Integer; Path, Swap: String; Err: LongWord; Ordered: TArrayOfString;
begin
  Result := False;
  if not PreflightOwnedDirectories(Root, Paths, Reason) then Exit;
  SetArrayLength(Ordered, GetArrayLength(Paths));
  for I := 0 to GetArrayLength(Paths) - 1 do Ordered[I] := Paths[I];
  { Longest paths first ensures children precede parents, including old payload
    directories no longer referenced by the current version. Never enumerate. }
  for I := 0 to GetArrayLength(Ordered) - 1 do
    for J := I + 1 to GetArrayLength(Ordered) - 1 do
      if Length(Ordered[J]) > Length(Ordered[I]) then begin
        Swap := Ordered[I]; Ordered[I] := Ordered[J]; Ordered[J] := Swap;
      end;
  for I := 0 to GetArrayLength(Ordered) - 1 do begin
    Path := AddBackslash(Root) + Ordered[I];
    if not NoReparseComponents(Path) then begin Reason := 'Directory path changed during removal.'; Exit; end;
    { RemoveDir is nonrecursive. Nonempty directories (including user-created
      empty child folders) stay intact. No file or wildcard deletion here. }
    if not RemoveDir(Path) then begin
      Err := PolicyLastError;
      if (Err <> 2) and (Err <> 3) and (Err <> 145) then begin
        Reason := 'Empty directory removal failed: ' + Ordered[I]; Exit;
      end;
    end;
  end;
  Result := True;
end;
