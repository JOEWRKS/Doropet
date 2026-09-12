param(
    [ValidateSet('Refusal', 'Policy', 'Build', 'All')][string]$Phase = 'All',
    [string]$CompilerPath,
    [string]$CandidatePath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts/installer'
$runtime = Join-Path $root 'artifacts/product-shell/candidate-20260912-1751/runtime'
$archive = Join-Path $root 'artifacts/product-shell/candidate-20260912-1751/Dororong-win-x64.zip'
$builder = Join-Path $root 'tools/Build-Installer.ps1'
if (-not $CompilerPath) { $CompilerPath = Join-Path $artifacts 'toolchain-inno-7.1.0-x64-20260912-02/ISCC.exe' }
function Check([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Refuses([hashtable]$arguments, [string]$pattern) {
    try { & $builder @arguments | Out-Null } catch {
        Check ($_.Exception.Message -match $pattern) "Wrong refusal: $($_.Exception.Message)"
        return
    }
    throw 'Builder accepted unsafe input.'
}
$testRoot = Join-Path $artifacts ('.task-1002-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
if ($Phase -in 'Refusal','All') {
    Check (Test-Path -LiteralPath $builder) 'RED: Installer builder is missing; refusal contract is not implemented.'
    $output = Join-Path $artifacts ('candidate-refusal-' + [guid]::NewGuid().ToString('N'))
    $argsBase = @{ PackagePath=$runtime; ArchivePath=$archive; CompilerPath=$CompilerPath; OutputPath=$output }
    $argsCase = $argsBase.Clone(); $argsCase.PackagePath = Join-Path $testRoot 'missing'
    Refuses $argsCase 'missing|does not exist'
    Check (-not (Test-Path $output)) 'Missing input created output.'
    $argsCase = $argsBase.Clone(); $argsCase.CompilerPath = (Get-Command pwsh).Source
    Refuses $argsCase 'compiler|trust|hash'
    Check (-not (Test-Path $output)) 'Foreign compiler created output.'
    $mutated = Join-Path $testRoot 'mutated.zip'
    [IO.File]::WriteAllText($mutated, 'not the approved archive')
    $argsCase = $argsBase.Clone(); $argsCase.ArchivePath = $mutated
    Refuses $argsCase 'archive|hash'
    Check (-not (Test-Path $output)) 'Mutated input created output.'
    New-Item -ItemType Directory -Path $output | Out-Null
    [IO.File]::WriteAllText((Join-Path $output 'sentinel'), 'preserve')
    Refuses $argsBase 'exist'
    Check ((Get-Content (Join-Path $output 'sentinel') -Raw) -ceq 'preserve') 'Existing output modified.'
    Check (@(Get-ChildItem $output -Force).Count -eq 1) 'Existing output gained files.'
    Write-Output 'REFUSAL PASS: missing input, foreign compiler, mutated archive, existing output; no build artifacts created.'
}
if ($Phase -in 'Policy','All') {
    Check (Test-Path (Join-Path $root 'installer/InstallerPolicy.iss')) 'RED: Shared compiled installer policy is missing.'
    Check ((Get-FileHash $CompilerPath).Hash -ceq 'D06EBD38F38E3CEE60A3C50CC45BD449D77E0BC6A5CABC607EA9886808E4DE1A') 'Untrusted compiler.'
    Check ((Get-AuthenticodeSignature $CompilerPath).Status -eq 'Valid') 'Compiler signature invalid.'
    & $CompilerPath /Qp "/DHarnessOutput=$testRoot" (Join-Path $PSScriptRoot 'installer/PolicyHarness.iss')
    Check ($LASTEXITCODE -eq 0) 'Harness compilation failed.'
    $fixture = Join-Path $testRoot 'fixture'
    New-Item -ItemType Directory -Path $fixture | Out-Null
    [IO.File]::WriteAllText((Join-Path $fixture 'owned.txt'), 'owned')
    [IO.File]::WriteAllText((Join-Path $fixture 'obsolete.txt'), 'obsolete')
    [IO.File]::WriteAllText((Join-Path $fixture 'user-added.txt'), 'preserve')
    [IO.File]::WriteAllText((Join-Path $fixture 'preserved-payload.txt'), 'preserve until all owned shortcuts pass')
    [IO.File]::WriteAllText((Join-Path $fixture 'second-payload.txt'), 'owned')
    foreach ($shortcutFolder in @('programs-locked','programs-clear','desktop-locked','desktop-clear')) {
        $fixtureFolder = Join-Path $fixture $shortcutFolder
        New-Item -ItemType Directory -Path $fixtureFolder | Out-Null
        # Bytes with a .lnk extension exercise Windows file sharing, not Shell
        # shortcut creation; these fixtures stay inside the task artifact root.
        [IO.File]::WriteAllText((Join-Path $fixtureFolder 'owned.lnk'), 'task-owned shortcut fixture')
    }
    $junction = Join-Path $fixture 'linked'
    $junctionTarget = Join-Path $testRoot 'junction-target'
    New-Item -ItemType Directory -Path $junctionTarget | Out-Null
    New-Item -ItemType Junction -Path $junction -Target $junctionTarget | Out-Null
    $locked = [IO.File]::Open((Join-Path $fixture 'owned.txt'), 'Open', 'Read', 'None')
    $lockedStartMenu = [IO.File]::Open((Join-Path $fixture 'programs-locked/owned.lnk'), 'Open', 'Read', 'ReadWrite')
    $lockedDesktop = [IO.File]::Open((Join-Path $fixture 'desktop-locked/owned.lnk'), 'Open', 'Read', 'ReadWrite')
    try {
        $process = Start-Process -FilePath (Join-Path $testRoot 'PolicyHarness.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART', ('/LOG="' + (Join-Path $testRoot 'harness.log') + '"')) -PassThru -Wait -WindowStyle Hidden
    } finally {
        $locked.Dispose()
        $lockedStartMenu.Dispose()
        $lockedDesktop.Dispose()
        # Remove only the test-owned junction itself, never its target/tree.
        if ((Get-Item -LiteralPath $junction -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            Remove-Item -LiteralPath $junction -Force
        }
    }
    Check ($process.ExitCode -ne 0) 'Harness did not abort setup.'
    $log = Get-Content (Join-Path $testRoot 'harness.log') -Raw
    Check ($log.Contains('POLICY PASS: 37 checks; returning False before installation')) 'Compiled policy assertions failed.'
    Check (-not $log.Contains('UNSAFE_INSTALL_TRANSITION')) 'Harness entered installation.'
    Check (-not (Test-Path (Join-Path $testRoot 'must-never-install'))) 'Harness wrote installation payload.'
    Check (-not (Test-Path (Join-Path $fixture 'operation.lock'))) 'Gate did not clean up on close.'
    Write-Output "POLICY PASS: compiled shared policy, 37 checks, abort exit $($process.ExitCode); evidence $testRoot"
}
if ($Phase -in 'Build','All') {
    if (-not $CandidatePath) { $CandidatePath = Join-Path $artifacts ('candidate-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
    & $builder -PackagePath $runtime -ArchivePath $archive -CompilerPath $CompilerPath -OutputPath $CandidatePath
    $evidence = Get-Content (Join-Path $CandidatePath 'build-evidence.json') -Raw | ConvertFrom-Json
    Check ($evidence.payloadFileCount -eq @(Get-ChildItem $runtime -File -Recurse).Count) 'Payload inventory parity failed.'
    Check ($evidence.version -ceq '0.1.0') 'Product version changed.'
    Check ($evidence.signatureState -ceq 'NotSigned') 'Unexpected signing state.'
    $exe = Join-Path $CandidatePath 'Dororong-Setup-0.1.0-win-x64.exe'
    Check ((Get-Item $exe).VersionInfo.ProductVersion.Trim() -ceq '0.1.0') 'Installer PE version mismatch.'
    Check ((Get-Item $exe).VersionInfo.CompanyName.Trim() -ceq 'JOEWRKS') 'Installer publisher mismatch.'
    $entries = @(Get-Content (Join-Path $CandidatePath 'payload-files.iss'))
    Check ($entries.Count -eq $evidence.payloadFileCount + 1) 'Explicit compiled inventory is incomplete.'
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $entries) {
        Check ($entry -match '^Source: "([^"]+)"; DestDir: "([^"]+)"; Flags: ignoreversion$') 'Unsafe or non-explicit compiled file entry.'
        $compiledSource = $Matches[1].Replace('{{','{')
        Check ($compiledSource.IndexOfAny([char[]]'*?') -lt 0) 'Wildcard compiled source.'
        Check ([IO.Path]::GetFullPath($compiledSource).StartsWith([IO.Path]::GetFullPath($CandidatePath) + '\', [StringComparison]::OrdinalIgnoreCase)) 'Compiler read unstaged payload.'
        Check ($seen.Add($compiledSource)) 'Duplicate compiled file entry.'
    }
    Check ((Get-FileHash $exe).Hash -ceq $evidence.installerSha256) 'Installer evidence hash mismatch.'
    Write-Output "BUILD PASS: metadata, signature state, payload count and installer hash; $CandidatePath"
}
