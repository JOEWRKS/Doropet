param(
    [ValidateSet('Boundary', 'Config', 'All')][string]$Phase = 'All',
    [string]$CandidatePath = 'artifacts/installer/candidate-20260912-task1002-05',
    [string]$PreparedToolchainPath = 'artifacts/installer/toolchain-inno-7.1.0-x64-20260912-02'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $repo
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function HostState {
    $paths = @(
        "$env:LOCALAPPDATA\Programs\JOEWRKS\Dororong",
        "$env:LOCALAPPDATA\Dororong-928A9BC7-operation.lock",
        "$env:LOCALAPPDATA\JOEWRKS\Dororong\logs",
        (Join-Path ([Environment]::GetFolderPath('Programs')) '도로롱.lnk'),
        (Join-Path ([Environment]::GetFolderPath('Desktop')) '도로롱.lnk')
    )
    $state = [ordered]@{}
    foreach ($path in $paths) {
        $state[$path] = if (Test-Path -LiteralPath $path) {
            $item = Get-Item -LiteralPath $path -Force
            if ($item.PSIsContainer) { @($item.FullName, $item.LastWriteTimeUtc.Ticks, @(Get-ChildItem -LiteralPath $path -Force | Select-Object Name, Length, LastWriteTimeUtc)) }
            else { (Get-FileHash -LiteralPath $path).Hash }
        } else { 'ABSENT' }
    }
    foreach ($hive in @('HKCU', 'HKLM')) {
        $key = "${hive}:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{928A9BC7-3A87-4C52-9161-1E5D08DB410A}_is1"
        $state[$key] = if (Test-Path $key) { Get-ItemProperty $key } else { 'ABSENT' }
    }
    $state | ConvertTo-Json -Depth 8 -Compress
}
$before = HostState
if ($Phase -in @('Boundary', 'All')) {
    $output = & powershell.exe -NoProfile -File tests/installer/Invoke-InstallerAcceptance.ps1 -CandidatePath $CandidatePath 2>&1 | Out-String
    Assert ($LASTEXITCODE -eq 2 -and $output -match 'UNVERIFIED: isolation') 'Host boundary must refuse with UNVERIFIED/exit 2 before work.'
    Assert ((HostState) -ceq $before) 'Ordinary-host invocation mutated protected product paths/registration.'
    Write-Output 'BOUNDARY PASS: ordinary host rejected with exit 2; protected host state unchanged.'
}
if ($Phase -in @('Config', 'All')) {
    $outputName = 'sandbox-test-' + [guid]::NewGuid().ToString('N')
    $destination = Join-Path $repo "artifacts/installer/$outputName"
    & tools/installer/New-InstallerSandbox.ps1 -CandidatePath $CandidatePath -PreparedToolchainPath $PreparedToolchainPath -OutputPath $destination
    Assert (Test-Path -LiteralPath "$destination/acceptance.wsb") 'Generator must create opt-in restricted Sandbox configuration.'
    [xml]$config = Get-Content -LiteralPath "$destination/acceptance.wsb" -Raw
    foreach ($setting in @('Networking', 'ClipboardRedirection', 'AudioInput', 'VideoInput', 'PrinterRedirection', 'VGpu')) {
        Assert ($config.Configuration.$setting -eq 'Disable') "$setting must be disabled in emitted configuration."
    }
    $mappings = @($config.Configuration.MappedFolders.MappedFolder)
    Assert ($mappings.Count -eq 4) 'Only input, candidate, toolchain and narrow results may be mapped.'
    $writes = @($mappings | Where-Object ReadOnly -eq 'false')
    Assert ($writes.Count -eq 1 -and $writes[0].HostFolder -eq "$destination\results") 'Exactly the fresh results directory may be writable.'
    Assert (@($mappings | Where-Object ReadOnly -eq 'true').Count -eq 3) 'Every input mapping must be read-only.'
    $handoff = Get-Content -LiteralPath "$destination/input/handoff.json" -Raw | ConvertFrom-Json
    Assert ($handoff.installerSha256 -eq 'AEDE2B7D36A10DEADA800832C99DBD134BC1F4DEEEEA39B48D1F51CE166D5D5D') 'Handoff must pin the reviewed candidate.'
    Assert ($handoff.configSha256 -eq (Get-FileHash "$destination/acceptance.wsb").Hash) 'Handoff must bind the generated configuration.'
    $result = & powershell.exe -NoProfile -File tests/installer/Invoke-InstallerAcceptance.ps1 -CandidatePath $CandidatePath -HandoffPath "$destination/input/handoff.json" -ResultPath "$destination/results" 2>&1 | Out-String
    Assert ($LASTEXITCODE -eq 2 -and $result -match 'UNVERIFIED: isolation') 'A genuine generated handoff must still refuse on its host.'
    Assert (@(Get-ChildItem "$destination/results" -Force).Count -eq 0) 'Host refusal must happen before output writes.'
    Assert ((HostState) -ceq $before) 'Generated-handoff host invocation mutated protected state.'
    # Compile the actual generated guest-only fixtures but NEVER execute them.
    # This catches broken shared-policy includes and Pascal/API syntax offline.
    $compileDir = Join-Path $destination 'fixture-compile'
    [void](New-Item -ItemType Directory -Path $compileDir)
    foreach ($version in @('1.0.0', '1.10.0', '1.2.0')) {
        [IO.File]::WriteAllText("$compileDir/value-$version.txt", "FIXTURE ONLY $version")
        & "$PreparedToolchainPath/ISCC.exe" "/DFixtureVersion=$version" "/DFixtureWorkRoot=$compileDir" "$destination/input/VersionFixture.iss" | Out-File "$compileDir/compile-$version.log"
        Assert ($LASTEXITCODE -eq 0 -and (Test-Path "$compileDir/Fixture-$version.exe")) "Generated guest fixture $version must compile."
    }
    Assert ((HostState) -ceq $before) 'Non-executing fixture compile changed host product state.'
    $oldHash = (Get-FileHash "$destination/acceptance.wsb").Hash
    $refused = $false
    try { & tools/installer/New-InstallerSandbox.ps1 -CandidatePath $CandidatePath -PreparedToolchainPath $PreparedToolchainPath -OutputPath $destination } catch { $refused = $true }
    Assert $refused 'Generator must refuse an existing output directory.'
    Assert ((Get-FileHash "$destination/acceptance.wsb").Hash -eq $oldHash) 'Refusal must preserve prior artifacts.'
    Write-Output "CONFIG PASS: restrictive .wsb, pinned handoff, host replay refusal, no host mutations, existing-output refusal; 3 separate fixture versions COMPILED ONLY. Evidence: $destination"
}
