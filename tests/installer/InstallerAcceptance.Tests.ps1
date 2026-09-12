param(
    [ValidateSet('ProcessCount', 'Boundary', 'GatePredicates', 'Config', 'All')][string]$Phase = 'All',
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
if ($Phase -in @('ProcessCount', 'All')) {
    # Execute the real read-only query and both actual consumer expressions in
    # guest-version PowerShell. A zero-result query must stay a countable array.
    $tokens = $null
    $parseErrors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile("$repo/tests/installer/Invoke-InstallerAcceptance.ps1", [ref]$tokens, [ref]$parseErrors)
    Assert ($parseErrors.Count -eq 0) 'Acceptance script must parse.'
    $query = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'ProductProcesses' }, $true)
    $counts = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.MemberExpressionAst] -and $node.Member.Extent.Text -eq 'Count' -and $node.Expression.Extent.Text -match 'ProductProcesses' }, $true))
    Assert ($null -ne $query -and $counts.Count -eq 2) 'Both product-process count consumers must be covered.'
    $driver = @'
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = 'C:\DororongAcceptance-NoInstalledProduct-' + [guid]::NewGuid().ToString('N')
'@ + "`r`n" + $query.Extent.Text + "`r`n"
    foreach ($count in $counts) {
        $driver += '$observed = ' + $count.Extent.Text + "; if (`$observed -ne 0) { throw 'Expected zero product processes' }`r`n"
    }
    $driver += "Write-Output 'PS51 ZERO-PROCESS PASS: both consumers returned zero'"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($driver))
    $result = & powershell.exe -NoProfile -EncodedCommand $encoded 2>&1 | Out-String
    Assert ($LASTEXITCODE -eq 0 -and $result -match 'PS51 ZERO-PROCESS PASS') "Zero-result process consumers failed in Windows PowerShell: $result"
    Assert ((HostState) -ceq $before) 'Read-only process-count regression changed protected host state.'
    Write-Output 'PROCESS COUNT PASS: both actual consumers handle zero results in Windows PowerShell 5.1; host state unchanged.'
}
if ($Phase -in @('Boundary', 'All')) {
    $output = & powershell.exe -NoProfile -File tests/installer/Invoke-InstallerAcceptance.ps1 -CandidatePath $CandidatePath 2>&1 | Out-String
    Assert ($LASTEXITCODE -eq 2 -and $output -match 'UNVERIFIED: isolation') 'Host boundary must refuse with UNVERIFIED/exit 2 before work.'
    Assert ((HostState) -ceq $before) 'Ordinary-host invocation mutated protected product paths/registration.'
    Write-Output 'BOUNDARY PASS: ordinary host rejected with exit 2; protected host state unchanged.'
}
if ($Phase -in @('GatePredicates', 'All')) {
    $gatePath = "$repo/tests/installer/InstallerAcceptanceGate.ps1"
    Assert (Test-Path -LiteralPath $gatePath -PathType Leaf) 'The pure acceptance-gate predicate seam is missing.'
    . $gatePath
    $guestPathBefore = Test-Path -LiteralPath 'C:\DororongAcceptance'
    $nonce = '0123456789abcdef0123456789abcdef'
    $maps = @(
        [pscustomobject]@{ HostFolder='D:\input'; SandboxFolder='C:\DororongAcceptance\Input'; ReadOnly='true' },
        [pscustomobject]@{ HostFolder='D:\candidate'; SandboxFolder='C:\DororongAcceptance\Candidate'; ReadOnly='true' },
        [pscustomobject]@{ HostFolder='D:\toolchain'; SandboxFolder='C:\DororongAcceptance\Toolchain'; ReadOnly='true' },
        [pscustomobject]@{ HostFolder='D:\results'; SandboxFolder='C:\DororongAcceptance\Results'; ReadOnly='false' }
    )
    $mappingXml = ($maps | ForEach-Object { "<MappedFolder><HostFolder>$($_.HostFolder)</HostFolder><SandboxFolder>$($_.SandboxFolder)</SandboxFolder><ReadOnly>$($_.ReadOnly)</ReadOnly></MappedFolder>" }) -join ''
    [xml]$config = "<Configuration><Networking>Disable</Networking><ClipboardRedirection>Disable</ClipboardRedirection><AudioInput>Disable</AudioInput><VideoInput>Disable</VideoInput><PrinterRedirection>Disable</PrinterRedirection><VGpu>Disable</VGpu><MappedFolders>$mappingXml</MappedFolders></Configuration>"
    $handoff = [pscustomobject]@{
        schemaVersion=1; nonce=$nonce; hostMachineGuid='host-machine'; hostUserSid='S-1-5-21-host'
        hostComputerName='HOST'; hostSystemUuid='host-uuid'; configSha256='CONFIGHASH'
        mappings=@($maps | ForEach-Object { [pscustomobject]@{ host=$_.HostFolder; guest=$_.SandboxFolder; readOnly=$_.ReadOnly } })
        inputFiles=@([pscustomobject]@{ name='payload.json'; sha256='AAAAAAAA' })
    }
    $available = @{}
    foreach ($map in $maps) { $available[$map.SandboxFolder] = $true }
    $identity = @{
        machineGuid='guest-machine'; userSid='S-1-5-21-guest'; computerName='GUEST'; systemUuid='guest-uuid'
        identityName='WINDOWS\WDAGUtilityAccount'; userProfile='C:\Users\WDAGUtilityAccount'
        manufacturer='Microsoft Corporation'; model='Virtual Machine'; hypervisorPresent=$true
    }
    $hashes = @{ 'payload.json'='AAAAAAAA' }
    $common = @{
        Handoff=$handoff; Nonce=$nonce; Config=$config; Mappings=$maps; MappingAvailability=$available; ObservedConfigHash='CONFIGHASH'
        Identity=$identity; HostOutputAccessible=$false; ActualInputHashes=$hashes
        ReviewedSetupHash='AEDE2B7D36A10DEADA800832C99DBD134BC1F4DEEEEA39B48D1F51CE166D5D5D'
        CompilerHash='D06EBD38F38E3CEE60A3C50CC45BD449D77E0BC6A5CABC607EA9886808E4DE1A'; ResultCount=0
    }
    Assert-InstallerAcceptanceGatePredicates @common
    $cases = @(
        @{ name='nonce'; pattern='nonce'; mutate={ param($p) $p.Nonce='ffffffffffffffffffffffffffffffff' } },
        @{ name='configuration hash'; pattern='configuration hash'; mutate={ param($p) $p.ObservedConfigHash='WRONG' } },
        @{ name='mapping'; pattern='mapping'; mutate={ param($p) $p.Mappings[3].ReadOnly='true' } },
        @{ name='identity'; pattern='MachineGuid'; mutate={ param($p) $p.Identity.machineGuid='host-machine' } },
        @{ name='input hash'; pattern='input hash'; mutate={ param($p) $p.ActualInputHashes['payload.json']='BBBBBBBB' } },
        @{ name='setup hash'; pattern='setup hash'; mutate={ param($p) $p.ReviewedSetupHash='BAD' } },
        @{ name='compiler hash'; pattern='compiler hash'; mutate={ param($p) $p.CompilerHash='BAD' } }
    )
    foreach ($case in $cases) {
        $copy = @{} + $common
        $copy.Mappings = @($maps | ForEach-Object { [pscustomobject]@{ HostFolder=$_.HostFolder; SandboxFolder=$_.SandboxFolder; ReadOnly=$_.ReadOnly } })
        $copy.Identity = @{} + $identity
        $copy.ActualInputHashes = @{} + $hashes
        & $case.mutate $copy
        $refused = $false
        try { Assert-InstallerAcceptanceGatePredicates @copy } catch { $refused = $_.Exception.Message -match $case.pattern }
        Assert $refused "Synthetic $($case.name) mismatch did not fail closed with the expected reason."
    }
    Assert ((Test-Path -LiteralPath 'C:\DororongAcceptance') -eq $guestPathBefore) 'Pure gate checks created or removed the guest path.'
    Assert ((HostState) -ceq $before) 'Pure gate predicate checks changed protected host state.'
    Write-Output 'GATE PREDICATES PASS: nonce, configuration/input/setup/compiler hash, mapping and identity mismatches fail closed; host state unchanged.'
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
    Assert ($LASTEXITCODE -eq 2 -and $result -match 'UNVERIFIED: isolation') 'A host-path replay of generated artifacts must fail at the fixed guest-path guard.'
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
    Write-Output "CONFIG PASS: restrictive .wsb, pinned handoff, early fixed-path host replay refusal, no host mutations, existing-output refusal; 3 separate fixture versions COMPILED ONLY. Evidence: $destination"
}
