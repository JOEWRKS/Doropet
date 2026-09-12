param(
    [Parameter(Mandatory)][string]$CandidatePath,
    [Parameter(Mandatory)][string]$PreparedToolchainPath,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Require([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function NoReparse([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force
    while ($null -ne $item) {
        Require (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) "Reparse path refused: $($item.FullName)"
        $parent = Split-Path $item.FullName -Parent
        if (-not $parent -or $parent -eq $item.FullName) { break }
        $item = Get-Item -LiteralPath $parent -Force
    }
}
Require ($OutputPath -notmatch '(^|[\\/])\.\.?([\\/]|$)') 'Output traversal refused.'
$destination = [IO.Path]::GetFullPath($OutputPath)
$artifactRoot = Join-Path $repo 'artifacts\installer'
Require ((Split-Path $destination -Parent) -eq $artifactRoot -and (Split-Path $destination -Leaf) -match '^sandbox-[a-zA-Z0-9-]+$') 'Output must be a direct fresh sandbox-* child of artifacts/installer.'
Require (-not (Test-Path -LiteralPath $destination)) 'Output already exists; retained artifacts must not be overwritten.'
NoReparse $artifactRoot
$candidate = (Resolve-Path -LiteralPath $CandidatePath).Path
$toolchain = (Resolve-Path -LiteralPath $PreparedToolchainPath).Path
NoReparse $candidate
NoReparse $toolchain
$evidence = Get-Content -LiteralPath "$candidate/build-evidence.json" -Raw | ConvertFrom-Json
Require ($evidence.schemaVersion -eq 1 -and $evidence.version -eq '0.1.0' -and $evidence.validatorExitCode -eq 0 -and $evidence.compilerExitCode -eq 0 -and $evidence.payloadFileCount -eq 467) 'Candidate build evidence contract mismatch.'
Require ((Hash "$candidate/Dororong-Setup-0.1.0-win-x64.exe") -eq '5BEAA5294FC9B73C527CB06404425C87320B421BD225223DAA9B8D253B13E61E') 'Reviewed candidate hash mismatch.'
Require ((Hash "$toolchain/ISCC.exe") -eq 'D06EBD38F38E3CEE60A3C50CC45BD449D77E0BC6A5CABC607EA9886808E4DE1A') 'Pinned compiler hash mismatch.'
Require ((Hash $evidence.archivePath) -eq '7BB45700A174D98123383B52FB39C913CA2F9EDA924827CBD14D97C136E20BC6') 'Frozen archive hash mismatch.'
foreach ($source in $evidence.sourceFiles) { Require ((Hash (Join-Path $repo $source.path)) -eq $source.sha256) "Build source drift: $($source.path)" }
foreach ($inputFile in $evidence.generatedInputs) { Require ((Hash (Join-Path $candidate $inputFile.path)) -eq $inputFile.sha256) "Generated candidate input drift: $($inputFile.path)" }
$inventory = Get-Content -LiteralPath "$candidate/payload.json" -Raw | ConvertFrom-Json
Require ($inventory.Files.Count -eq 467) 'Payload count mismatch.'
foreach ($file in $inventory.Files) {
    Require ($file.RelativePath -notmatch '(^[\\/]|:|(^|[\\/])\.\.([\\/]|$))') 'Unsafe inventory path.'
    foreach ($root in @($evidence.packageRoot, "$candidate/payload")) {
        $path = Join-Path $root $file.RelativePath
        NoReparse $path
        Require ((Hash $path) -eq $file.Sha256) "Frozen payload drift: $($file.RelativePath)"
    }
}
# All validation above is read-only. This is the sole host mutation: fresh artifacts.
$inputDir = Join-Path $destination 'input'
$resultDir = Join-Path $destination 'results'
[void](New-Item -ItemType Directory -Path $inputDir)
[void](New-Item -ItemType Directory -Path $resultDir)
$utf8Bom = New-Object System.Text.UTF8Encoding($true)
[IO.File]::WriteAllText("$inputDir/Invoke-InstallerAcceptance.ps1", [IO.File]::ReadAllText("$repo/tests/installer/Invoke-InstallerAcceptance.ps1"), $utf8Bom)
[IO.File]::WriteAllText("$inputDir/InstallerAcceptanceGate.ps1", [IO.File]::ReadAllText("$repo/tests/installer/InstallerAcceptanceGate.ps1"), $utf8Bom)
Copy-Item -LiteralPath "$repo/installer/InstallerPolicy.iss" -Destination "$inputDir/InstallerPolicy.iss"
Copy-Item -LiteralPath "$repo/installer/InstallerDirectories.iss" -Destination "$inputDir/InstallerDirectories.iss"
Copy-Item -LiteralPath "$candidate/payload.json" -Destination "$inputDir/payload.json"
# This separately identified package tests shared policy through REAL guest installs.
# Its versions, target, registry identity and bytes never become product evidence.
$fixture = @'
#ifndef FixtureVersion
  #error FixtureVersion required for separate acceptance fixture
#endif
#ifndef FixtureWorkRoot
  #define FixtureWorkRoot "C:\DororongAcceptance\Results\fixtures"
#endif
[Setup]
AppId={{D9BAFA26-9406-49E2-8C81-C8F41DD7C033}
AppName=Dororong Acceptance Fixture ONLY
AppVersion={#FixtureVersion}
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DefaultDirName={localappdata}\DororongAcceptanceFixture
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousAppDir=no
UninstallLogMode=overwrite
CloseApplications=no
RestartApplications=no
OutputDir={#FixtureWorkRoot}
OutputBaseFilename=Fixture-{#FixtureVersion}
[Files]
Source: "{#FixtureWorkRoot}\value-{#FixtureVersion}.txt"; DestDir: "{app}"; DestName: "value.txt"; Flags: ignoreversion
[Code]
#include "InstallerPolicy.iss"
const Key = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{D9BAFA26-9406-49E2-8C81-C8F41DD7C033}_is1';
var Completed: Boolean;
function InitializeSetup: Boolean;
var V: String;
begin
  Result := False;
  if ExpandConstant('{param:ACCEPTANCE_NONCE|}') <> '__NONCE__' then Exit;
  if not FileExists('C:\DororongAcceptance\Results\guest-verified.json') then Exit;
  if RegQueryStringValue(HKCU64, Key, 'DisplayVersion', V) then
    if not VersionAllowed('{#FixtureVersion}', V) then begin Log('FIXTURE downgrade refused'); Exit; end;
  Result := True;
end;
procedure CurStepChanged(Step: TSetupStep);
begin
  if (Step = ssPostInstall) and (ExpandConstant('{param:INJECT_FAILURE|0}') = '1') then
    RaiseException('FIXTURE injected postinstall failure; repair required');
  if Step = ssPostInstall then Completed := True;
end;
function GetCustomSetupExitCode: Integer;
begin
  Result := 10;
  if Completed then Result := 0;
end;
'@
$nonce = [guid]::NewGuid().ToString('N')
[IO.File]::WriteAllText("$inputDir/VersionFixture.iss", $fixture.Replace('__NONCE__', $nonce), $utf8Bom)
$mappings = @(
    @{host=$inputDir; guest='C:\DororongAcceptance\Input'; readOnly='true'},
    @{host=$candidate; guest='C:\DororongAcceptance\Candidate'; readOnly='true'},
    @{host=$toolchain; guest='C:\DororongAcceptance\Toolchain'; readOnly='true'},
    @{host=$resultDir; guest='C:\DororongAcceptance\Results'; readOnly='false'}
)
$mappingXml = ($mappings | ForEach-Object { '<MappedFolder><HostFolder>' + [Security.SecurityElement]::Escape($_.host) + '</HostFolder><SandboxFolder>' + $_.guest + '</SandboxFolder><ReadOnly>' + $_.readOnly + '</ReadOnly></MappedFolder>' }) -join "`r`n"
$command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\DororongAcceptance\Input\Invoke-InstallerAcceptance.ps1 -CandidatePath C:\DororongAcceptance\Candidate -HandoffPath C:\DororongAcceptance\Input\handoff.json -ResultPath C:\DororongAcceptance\Results -Nonce $nonce"
$xml = @"
<Configuration>
<Networking>Disable</Networking><ClipboardRedirection>Disable</ClipboardRedirection>
<AudioInput>Disable</AudioInput><VideoInput>Disable</VideoInput><PrinterRedirection>Disable</PrinterRedirection><VGpu>Disable</VGpu>
<MappedFolders>$mappingXml</MappedFolders>
<LogonCommand><Command>$command</Command></LogonCommand>
</Configuration>
"@
[IO.File]::WriteAllText("$destination/acceptance.wsb", $xml, $utf8Bom)
Copy-Item -LiteralPath "$destination/acceptance.wsb" -Destination "$inputDir/acceptance.wsb"
$handoff = [ordered]@{
    schemaVersion=1; nonce=$nonce; createdUtc=[DateTime]::UtcNow.ToString('o')
    hostMachineGuid=(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography').MachineGuid
    hostUserSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    hostComputerName=$env:COMPUTERNAME
    hostSystemUuid=(Get-CimInstance Win32_ComputerSystemProduct).UUID
    hostOutputPath=$destination; mappings=$mappings
    configSha256=(Hash "$destination/acceptance.wsb")
    installerSha256=$evidence.installerSha256; archiveSha256=$evidence.archiveSha256
    fixtureId='{D9BAFA26-9406-49E2-8C81-C8F41DD7C033}'; fixtureVersions=@('1.0.0', '1.10.0', '1.2.0')
    inputFiles=@('Invoke-InstallerAcceptance.ps1', 'InstallerAcceptanceGate.ps1', 'InstallerPolicy.iss', 'InstallerDirectories.iss', 'payload.json', 'VersionFixture.iss' | ForEach-Object { @{name=$_; sha256=(Hash "$inputDir/$_")} })
}
[IO.File]::WriteAllText("$inputDir/handoff.json", ($handoff | ConvertTo-Json -Depth 8), $utf8Bom)
Write-Output "SANDBOX_CONFIG=$destination\acceptance.wsb"
Write-Output 'UNVERIFIED: opt-in configuration generated only; Sandbox was not enabled or launched.'
