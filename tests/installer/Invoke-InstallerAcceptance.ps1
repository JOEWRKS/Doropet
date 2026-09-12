param([string]$CandidatePath, [string]$HandoffPath, [string]$ResultPath, [string]$Nonce)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Require([bool]$Condition, [string]$Reason) { if (-not $Condition) { throw $Reason } }
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }

# This complete read-only gate precedes ALL output, fixture, installer and Shell writes.
# It prevents accidental host execution; it is not attestation against a hostile admin.
try {
    Require ($HandoffPath -eq 'C:\DororongAcceptance\Input\handoff.json') 'generated guest handoff path required'
    Require ($CandidatePath -eq 'C:\DororongAcceptance\Candidate') 'fixed read-only candidate mapping required'
    Require ($ResultPath -eq 'C:\DororongAcceptance\Results') 'narrow guest results mapping required'
    Require ($PSScriptRoot -eq 'C:\DororongAcceptance\Input') 'script must execute from generated input mapping'
    $handoff = Get-Content -LiteralPath $HandoffPath -Raw | ConvertFrom-Json
    Require ($handoff.schemaVersion -eq 1 -and $Nonce -match '^[a-f0-9]{32}$' -and $Nonce -ceq $handoff.nonce) 'generated handoff nonce required'
    Require ((Hash "$PSScriptRoot\acceptance.wsb") -eq $handoff.configSha256) 'configuration hash mismatch'
    [xml]$config = Get-Content -LiteralPath "$PSScriptRoot\acceptance.wsb" -Raw
    foreach ($setting in @('Networking', 'ClipboardRedirection', 'AudioInput', 'VideoInput', 'PrinterRedirection', 'VGpu')) {
        Require ($config.Configuration.$setting -eq 'Disable') "unsafe $setting configuration"
    }
    $maps = @($config.Configuration.MappedFolders.MappedFolder)
    Require ($maps.Count -eq 4 -and @($maps | Where-Object ReadOnly -eq 'false').Count -eq 1) 'mapping count/writable scope mismatch'
    foreach ($mapping in $handoff.mappings) {
        $match = @($maps | Where-Object { $_.SandboxFolder -ceq $mapping.guest -and $_.HostFolder -ceq $mapping.host -and $_.ReadOnly -ceq $mapping.readOnly })
        Require ($match.Count -eq 1 -and (Test-Path -LiteralPath $mapping.guest -PathType Container)) 'required generated mapping missing'
    }
    $machineGuid = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography').MachineGuid
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $computer = Get-CimInstance Win32_ComputerSystem
    $system = Get-CimInstance Win32_ComputerSystemProduct
    Require ($machineGuid -and $machineGuid -ne $handoff.hostMachineGuid) 'host MachineGuid matches or is unreadable'
    Require ($identity.User.Value -ne $handoff.hostUserSid) 'host user token matches'
    Require ($env:COMPUTERNAME -ne $handoff.hostComputerName -and $system.UUID -ne $handoff.hostSystemUuid) 'host computer identity matches'
    Require ($identity.Name -match '\\WDAGUtilityAccount$' -and $env:USERPROFILE -eq 'C:\Users\WDAGUtilityAccount') 'Sandbox account/profile evidence missing'
    Require ($computer.Manufacturer -eq 'Microsoft Corporation' -and $computer.Model -eq 'Virtual Machine' -and $computer.HypervisorPresent) 'Microsoft virtual guest evidence missing'
    Require (-not (Test-Path -LiteralPath $handoff.hostOutputPath)) 'host output path is directly accessible'
    foreach ($file in $handoff.inputFiles) { Require ((Hash (Join-Path $PSScriptRoot $file.name)) -eq $file.sha256) "input hash mismatch: $($file.name)" }
    Require ((Hash "$CandidatePath\Dororong-Setup-0.1.0-win-x64.exe") -eq 'AEDE2B7D36A10DEADA800832C99DBD134BC1F4DEEEEA39B48D1F51CE166D5D5D') 'reviewed setup hash mismatch'
    Require ((Hash 'C:\DororongAcceptance\Toolchain\ISCC.exe') -eq 'D06EBD38F38E3CEE60A3C50CC45BD449D77E0BC6A5CABC607EA9886808E4DE1A') 'compiler hash mismatch'
    Require (@(Get-ChildItem -LiteralPath $ResultPath -Force).Count -eq 0) 'results mapping must be empty; generate a fresh handoff for each run'
} catch {
    [Console]::Error.WriteLine('UNVERIFIED: isolation gate refused before mutation: ' + $_.Exception.Message)
    exit 2
}

# All following mutations are confined to the verified disposable guest, except
# evidence in its one generated writable mapping. Never invoke by bypassing gate.
$utf8 = New-Object System.Text.UTF8Encoding($true)
function SaveJson([string]$Name, $Value) { [IO.File]::WriteAllText((Join-Path $ResultPath $Name), ($Value | ConvertTo-Json -Depth 12), $utf8) }
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$administratorSidPresent = @($identity.Groups | Where-Object Value -eq 'S-1-5-32-544').Count -gt 0
$guestEvidence = [ordered]@{
    machineGuid=$machineGuid; user=$identity.Name; userSid=$identity.User.Value
    computer=$env:COMPUTERNAME; systemUuid=$system.UUID; manufacturer=$computer.Manufacturer; model=$computer.Model
    powershellVersion=$PSVersionTable.PSVersion.ToString(); isAdministrator=$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    administratorSidPresent=$administratorSidPresent
    standardUserConfirmed=(-not $administratorSidPresent -and -not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
    groups=@($identity.Groups | ForEach-Object Value); nonce=$Nonce; configSha256=$handoff.configSha256
}
SaveJson 'guest-verified.json' $guestEvidence
& whoami.exe /all | Out-File -LiteralPath "$ResultPath/token.txt" -Encoding utf8
$cases = New-Object 'Collections.Generic.List[object]'
$root = Join-Path $env:LOCALAPPDATA 'Programs\JOEWRKS\Dororong'
$startLink = Join-Path ([Environment]::GetFolderPath('Programs')) '도로롱.lnk'
$desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) '도로롱.lnk'
$registration = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{928A9BC7-3A87-4C52-9161-1E5D08DB410A}_is1'
$setup = "$CandidatePath\Dororong-Setup-0.1.0-win-x64.exe"
$logSentinel = Join-Path $env:LOCALAPPDATA 'JOEWRKS\Dororong\logs\acceptance-preserve.txt'
$extra = Join-Path $root 'user-added-preserve.txt'
$outside = Join-Path $env:USERPROFILE 'Downloads\dororong-acceptance-preserve.txt'
$inventory = Get-Content -LiteralPath "$PSScriptRoot/payload.json" -Raw | ConvertFrom-Json
$expectedOwned = @($inventory.Files | ForEach-Object { $_.RelativePath.Replace('/', '\') }) + @('dororong-owned-files.txt')

function RegistrySnapshot([Microsoft.Win32.RegistryHive]$Hive, [string]$Key) {
    $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey($Hive, [Microsoft.Win32.RegistryView]::Registry64)
    try {
        $reg = $baseKey.OpenSubKey($Key)
        if ($null -eq $reg) { return $null }
        try {
            $values = [ordered]@{}
            foreach ($name in @($reg.GetValueNames() | Sort-Object)) { $values[$name] = @{kind=$reg.GetValueKind($name).ToString(); value=$reg.GetValue($name)} }
            return $values
        } finally { $reg.Dispose() }
    } finally { $baseKey.Dispose() }
}
function Snapshot {
    $files = [ordered]@{}
    if (Test-Path -LiteralPath $root) {
        foreach ($item in @(Get-ChildItem -LiteralPath $root -File -Recurse | Sort-Object FullName)) {
            Require (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) 'Unexpected guest reparse file'
            $files[$item.FullName.Substring($root.Length + 1)] = Hash $item.FullName
        }
    }
    $links = [ordered]@{}
    foreach ($path in @($startLink, $desktopLink)) { $links[$path] = if (Test-Path -LiteralPath $path) { Hash $path } else { 'ABSENT' } }
    return [ordered]@{files=$files; registration=(RegistrySnapshot CurrentUser $registration); machineRegistration=(RegistrySnapshot LocalMachine $registration); links=$links}
}
function SameState($Before) { Require (($Before | ConvertTo-Json -Depth 10 -Compress) -ceq ((Snapshot) | ConvertTo-Json -Depth 10 -Compress)) 'Refusal/cancel changed payload, registration or shortcut bytes' }
function ProductProcesses { @(Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase) }) }
function CheckInstalled([bool]$Desktop) {
    foreach ($file in $inventory.Files) { Require ((Hash (Join-Path $root $file.RelativePath)) -eq $file.Sha256) "Installed bytes differ: $($file.RelativePath)" }
    $owned = @(Get-Content -LiteralPath "$root/dororong-owned-files.txt")
    Require ($owned.Count -eq 468 -and @(Compare-Object ($expectedOwned | Sort-Object) ($owned | Sort-Object)).Count -eq 0) 'Owned manifest differs from literal 467 payload plus self contract'
    $reg = RegistrySnapshot CurrentUser $registration
    Require ($null -ne $reg -and $reg['DisplayName'].value -eq '도로롱 (Dororong)' -and $reg['DisplayVersion'].value -eq '0.1.0' -and $reg['Publisher'].value -eq 'JOEWRKS') 'Per-user product registration mismatch'
    Require ($reg['InstallLocation'].value.TrimEnd('\') -eq $root) 'Registered installation location mismatch'
    Require ($reg['UninstallString'].value -match 'unins000\.exe') 'Uninstall command missing'
    $shell = New-Object -ComObject WScript.Shell
    try {
        Require (Test-Path -LiteralPath $startLink) 'Start menu shortcut missing'
        Require ($shell.CreateShortcut($startLink).TargetPath -eq "$root\Dororong.exe") 'Start menu shortcut target mismatch'
        Require ((Test-Path -LiteralPath $desktopLink) -eq $Desktop) 'Desktop task selection mismatch'
        if ($Desktop) { Require ($shell.CreateShortcut($desktopLink).TargetPath -eq "$root\Dororong.exe") 'Desktop shortcut target mismatch' }
    } finally { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell) }
    Require ((ProductProcesses).Count -eq 0) 'Silent installation launched a product process'
}
function RunProcess([string]$Name, [string]$Executable, [string[]]$Arguments, [bool]$Success) {
    $argsWithLog = $Arguments + @('/LOG="' + "$ResultPath\$Name.log" + '"')
    $process = Start-Process -FilePath $Executable -ArgumentList $argsWithLog -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(180000)) { throw "Timeout: $Name; process $($process.Id) remains in guest for inspection" }
    SaveJson "$Name-process.json" @{executable=$Executable; sha256=(Hash $Executable); arguments=$argsWithLog; pid=$process.Id; exitCode=$process.ExitCode}
    if ($Success) { Require ($process.ExitCode -eq 0) "$Name failed with $($process.ExitCode)" }
    else { Require ($process.ExitCode -ne 0) "$Name unexpectedly reported success" }
    return $process.ExitCode
}
function Case([string]$Name, [string]$Package, [scriptblock]$Action) {
    $entry = [ordered]@{name=$Name; package=$Package; status='FAIL'; startedUtc=[DateTime]::UtcNow.ToString('o')}
    try { & $Action; $entry.status='PASS' } catch { $entry.error=$_.Exception.Message; throw } finally { $cases.Add($entry); SaveJson 'cases.json' @($cases.ToArray()) }
}
function LockedRefusal([string]$Name, [string]$Path, [bool]$Uninstall, [bool]$Desktop) {
    $before = Snapshot
    SaveJson "$Name-before.json" $before
    # ReadWrite deliberately excludes Delete, reproducing actual owned-link lock.
    $handle = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        if ($Uninstall) { [void](RunProcess $Name "$root\unins000.exe" @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') $false) }
        else {
            $task = if ($Desktop) { '/TASKS="desktopicon"' } else { '/TASKS=""' }
            [void](RunProcess $Name $setup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', $task) $false)
        }
        SameState $before
    } finally { $handle.Dispose() }
}
$silent = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-')
$failure = $null
try {
    Require (-not (Test-Path -LiteralPath $root) -and -not (Test-Path -LiteralPath $startLink) -and -not (Test-Path -LiteralPath $desktopLink) -and $null -eq (RegistrySnapshot CurrentUser $registration)) 'Guest is not clean; use a fresh Sandbox'
    $machineBefore = RegistrySnapshot LocalMachine $registration
    [void](New-Item -ItemType Directory -Path (Split-Path $logSentinel -Parent) -Force)
    [void](New-Item -ItemType Directory -Path (Split-Path $outside -Parent) -Force)
    [IO.File]::WriteAllText($logSentinel, 'KEEP diagnostic sentinel', $utf8)
    [IO.File]::WriteAllText($outside, 'KEEP unrelated Downloads sentinel', $utf8)
    $logHash = Hash $logSentinel
    $outsideHash = Hash $outside

    Case 'first-install-default-no-desktop' 'product 0.1.0 reviewed candidate' {
        [void](RunProcess 'first-install' $setup $silent $true)
        CheckInstalled $false
    }
    [IO.File]::WriteAllText($extra, 'KEEP user-added file', $utf8)
    $extraHash = Hash $extra
    Case 'same-version-repair-and-desktop-selection' 'product 0.1.0 reviewed candidate' {
        # A real damaged owned file must return to its approved bytes.
        [IO.File]::WriteAllText("$root\Dororong.App.deps.json", 'repair sentinel', $utf8)
        [void](RunProcess 'repair' $setup ($silent + '/TASKS="desktopicon"') $true)
        CheckInstalled $true
        Require ((Hash $extra) -eq $extraHash) 'Repair changed user-added file'
    }
    foreach ($lock in @(@{name='start'; path=$startLink}, @{name='desktop'; path=$desktopLink})) {
        $label = $lock.name
        $lockPath = $lock.path
        Case "locked-$label-repair" 'product 0.1.0 reviewed candidate' { LockedRefusal "locked-$label-repair" $lockPath $false $true }
        Case "locked-$label-desktop-deselection" 'product 0.1.0 reviewed candidate' { LockedRefusal "locked-$label-deselect" $lockPath $false $false }
        Case "locked-$label-uninstall" 'product 0.1.0 reviewed candidate' { LockedRefusal "locked-$label-uninstall" $lockPath $true $true }
    }
    Case 'owned-executable-lock-install-and-remove-refusal' 'product 0.1.0 reviewed candidate' {
        LockedRefusal 'locked-executable-repair' "$root\Dororong.exe" $false $true
        LockedRefusal 'locked-executable-uninstall' "$root\Dororong.exe" $true $true
    }
    Case 'invalid-directory-preinstall-failure' 'product 0.1.0 reviewed candidate' {
        $before = Snapshot
        [void](RunProcess 'invalid-directory' $setup ($silent + '/DIR="C:\DororongAcceptanceWrongTarget"') $false)
        SameState $before
        Require (-not (Test-Path 'C:\DororongAcceptanceWrongTarget')) 'Refused directory was created'
    }
    Case 'wizard-user-cancel-before-install' 'product 0.1.0 reviewed candidate' {
        $before = Snapshot
        Add-Type -AssemblyName UIAutomationClient
        Add-Type -AssemblyName UIAutomationTypes
        $process = Start-Process -FilePath $setup -ArgumentList @('/SP-', '/LANG=english', '/NORESTART', ('/LOG="' + "$ResultPath\cancel.log" + '"')) -PassThru
        $cancelled = $false
        $deadline = [DateTime]::UtcNow.AddSeconds(45)
        while (-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline) {
            $all = @(Get-CimInstance Win32_Process)
            $ids = @($process.Id)
            for ($depth=0; $depth -lt 4; $depth++) { $ids += @($all | Where-Object { $_.ParentProcessId -in $ids } | ForEach-Object ProcessId); $ids = @($ids | Select-Object -Unique) }
            foreach ($id in $ids) {
                $condition = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ProcessIdProperty, [int]$id)
                $windows = [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children, $condition)
                foreach ($window in $windows) {
                    $buttons = $window.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition)
                    foreach ($button in $buttons) {
                        $name = $button.Current.Name.Replace('&', '')
                        if (($name -eq 'Cancel' -and -not $cancelled) -or ($name -eq 'Yes' -and $cancelled)) {
                            $pattern = $null
                            if ($button.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke(); $cancelled = $true }
                        }
                    }
                }
            }
            Start-Sleep -Milliseconds 250
            $process.Refresh()
        }
        Require ($cancelled -and $process.HasExited -and $process.ExitCode -ne 0) 'Wizard cancellation did not complete; inspect disposable guest'
        SaveJson 'cancel-process.json' @{pid=$process.Id; exitCode=$process.ExitCode; executable=$setup; sha256=(Hash $setup); action='UI Automation Cancel then confirmation Yes; no install button invoked'}
        SameState $before
    }
    Case 'desktop-deselection' 'product 0.1.0 reviewed candidate' {
        [void](RunProcess 'desktop-deselect' $setup ($silent + '/TASKS=""') $true)
        CheckInstalled $false
    }
    Case 'unlocked-remove-owned-links-payload-preserve-data' 'product 0.1.0 reviewed candidate' {
        [void](RunProcess 'desktop-reselect' $setup ($silent + '/TASKS="desktopicon"') $true)
        CheckInstalled $true
        # Capture executable hash BEFORE uninstaller removes itself.
        $uninstaller = "$root\unins000.exe"
        $uninstallHash = Hash $uninstaller
        $process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + "$ResultPath\uninstall.log" + '"')) -PassThru -WindowStyle Hidden
        Require ($process.WaitForExit(180000) -and $process.ExitCode -eq 0) 'Normal uninstall failed'
        SaveJson 'uninstall-process.json' @{executable=$uninstaller; sha256=$uninstallHash; pid=$process.Id; exitCode=$process.ExitCode}
        foreach ($owned in $expectedOwned) { Require (-not (Test-Path -LiteralPath (Join-Path $root $owned))) "Owned file remains: $owned" }
        Require (-not (Test-Path -LiteralPath $startLink) -and -not (Test-Path -LiteralPath $desktopLink)) 'Owned shortcut remains'
        Require ($null -eq (RegistrySnapshot CurrentUser $registration)) 'Uninstall registration remains'
        Require ((Hash $extra) -eq $extraHash -and (Hash $logSentinel) -eq $logHash -and (Hash $outside) -eq $outsideHash) 'Uninstall removed/changed user data'
        Require ((ProductProcesses).Count -eq 0) 'Product process remains'
        Require (($machineBefore | ConvertTo-Json -Depth 8 -Compress) -ceq ((RegistrySnapshot LocalMachine $registration) | ConvertTo-Json -Depth 8 -Compress)) 'Machine product registration changed'
    }

    Case 'fixture-numeric-upgrade-lower-refusal-and-postinstall-failure' 'SEPARATE fixture {D9BAFA26-9406-49E2-8C81-C8F41DD7C033}; NOT product upgrade acceptance' {
        $fixtureDir = Join-Path $ResultPath 'fixtures'
        [void](New-Item -ItemType Directory -Path $fixtureDir)
        foreach ($version in @('1.0.0', '1.10.0', '1.2.0')) {
            [IO.File]::WriteAllText("$fixtureDir\value-$version.txt", "FIXTURE ONLY $version", $utf8)
            & 'C:\DororongAcceptance\Toolchain\ISCC.exe' "/DFixtureVersion=$version" "$PSScriptRoot\VersionFixture.iss" | Out-File "$fixtureDir\compile-$version.log" -Encoding utf8
            Require ($LASTEXITCODE -eq 0) "Guest fixture compilation failed: $version"
        }
        $fixtureRoot = Join-Path $env:LOCALAPPDATA 'DororongAcceptanceFixture'
        $fixtureKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{D9BAFA26-9406-49E2-8C81-C8F41DD7C033}_is1'
        $fixtureArgs = $silent + "/ACCEPTANCE_NONCE=$Nonce"
        [void](RunProcess 'fixture-first' "$fixtureDir\Fixture-1.0.0.exe" $fixtureArgs $true)
        [void](RunProcess 'fixture-higher' "$fixtureDir\Fixture-1.10.0.exe" $fixtureArgs $true)
        Require ([IO.File]::ReadAllText("$fixtureRoot\value.txt") -eq 'FIXTURE ONLY 1.10.0') 'Fixture upgrade bytes incorrect'
        Require ((RegistrySnapshot CurrentUser $fixtureKey)['DisplayVersion'].value -eq '1.10.0') 'Fixture upgraded version not registered'
        $beforeFile = Hash "$fixtureRoot\value.txt"
        $beforeReg = (RegistrySnapshot CurrentUser $fixtureKey) | ConvertTo-Json -Depth 8 -Compress
        [void](RunProcess 'fixture-lower-refusal' "$fixtureDir\Fixture-1.2.0.exe" $fixtureArgs $false)
        Require ((Hash "$fixtureRoot\value.txt") -eq $beforeFile -and ((RegistrySnapshot CurrentUser $fixtureKey) | ConvertTo-Json -Depth 8 -Compress) -ceq $beforeReg) 'Fixture downgrade changed installed state'
        [void](RunProcess 'fixture-injected-failure' "$fixtureDir\Fixture-1.10.0.exe" ($fixtureArgs + '/INJECT_FAILURE=1') $false)
        [void](RunProcess 'fixture-repair-after-failure' "$fixtureDir\Fixture-1.10.0.exe" $fixtureArgs $true)
        $fixtureUninstall = Start-Process "$fixtureRoot\unins000.exe" -ArgumentList $silent -PassThru -WindowStyle Hidden
        Require ($fixtureUninstall.WaitForExit(180000) -and $fixtureUninstall.ExitCode -eq 0) 'Fixture uninstall failed'
        Require (-not (Test-Path "$fixtureRoot\value.txt") -and $null -eq (RegistrySnapshot CurrentUser $fixtureKey)) 'Fixture owned state remains'
    }
} catch { $failure = $_.Exception.Message }
SaveJson 'summary.json' ([ordered]@{
    status=$(if ($failure) { 'FAIL' } else { 'PARTIAL / UNVERIFIED remaining gates' }); failure=$failure
    installerSha256=$handoff.installerSha256; configSha256=$handoff.configSha256
    cases=@($cases.ToArray()); token=$guestEvidence
    unverified=@('Actual higher/lower PRODUCT packages do not exist; fixture results never prove product upgrade', 'Product running process and another login session file locks (file-handle case only)', 'Product mid-copy I/O/disk-full/power-loss recovery (postinstall failure is fixture only)', 'Standard-user lifecycle unless token evidence proves non-administrative account', 'Visual tray icon cleanup, wizard localization and interactive Windows behavior')
})
if ($failure) { [Console]::Error.WriteLine('FAIL: ' + $failure); exit 1 }
Write-Output 'PARTIAL: recorded guest cases; remaining product/version/user/visual gates are UNVERIFIED.'
exit 2
