param(
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9-]*$')][string]$RunName = 'final-verification-v1',
    [switch]$ProbeOnly,
    [switch]$WpfProbeOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repositoryRoot
$binaryRoot = Join-Path $repositoryRoot 'src/Dororong.App/bin/Release/net8.0-windows'

function Get-CaptureVerificationExit([object[]]$Samples, [object]$Scene) {
    # A stale successful scene must never mask a failed capture in this verification run.
    if ($Samples.Count -ne 21 -or @($Samples | Where-Object { !$_.success }).Count -gt 0) { return 1 }
    if ($null -eq $Scene -or $null -eq $Scene.Windows -or @($Scene.Monitors).Count -eq 0) { return 1 }
    foreach ($monitor in $Scene.Monitors) {
        $bounds=$monitor.Bounds
        if ($null -eq $bounds -or
            ![double]::IsFinite($bounds.X) -or ![double]::IsFinite($bounds.Y) -or
            ![double]::IsFinite($bounds.Width) -or ![double]::IsFinite($bounds.Height) -or
            $bounds.Width -le 0 -or $bounds.Height -le 0) { return 1 }
    }
    return 0
}

if ($ProbeOnly -or $WpfProbeOnly) {
    # Separate process releases assembly locks on exit. No pet/UI is created.
    Add-Type -Path (Join-Path $binaryRoot 'Dororong.Core.dll')
    $assembly = [Reflection.Assembly]::LoadFrom((Join-Path $binaryRoot 'Dororong.App.dll'))
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Instance'
    $type = $assembly.GetType('Dororong.App.Interop.DesktopMetadataReader', $true)
    $reader = $type.GetConstructors($flags)[0].Invoke(@([IntPtr]::Zero))
    $method = $type.GetMethod('Read', $flags)
    $samples = @(); $lastScene = $null
    foreach ($index in 0..20) {
        $timer = [Diagnostics.Stopwatch]::StartNew()
        try {
            $lastScene = $method.Invoke($reader, @())
            $timer.Stop()
            $samples += [ordered]@{ index=$index; milliseconds=$timer.Elapsed.TotalMilliseconds; success=($null -ne $lastScene); failure=$(if ($null -eq $lastScene) { 'CaptureUnavailable' } else { $null }) }
        } catch {
            $timer.Stop()
            $cause = $_.Exception.GetBaseException()
            $diagnostic = $cause.GetType().GetProperty('Diagnostic', $flags)
            $failure = if ($diagnostic) { $diagnostic.GetValue($cause) } else { $cause.GetType().FullName }
            $samples += [ordered]@{ index=$index; milliseconds=$timer.Elapsed.TotalMilliseconds; success=$false; failure=$failure }
        }
    }
    $captureExit = Get-CaptureVerificationExit $samples $lastScene
    $roundtrips = @()
    if ($lastScene) {
        $mapType = $assembly.GetType('Dororong.App.Runtime.DesktopCoordinateMap', $true)
        # Real monitor bounds; synthetic affine map. This is not WPF/physical agreement.
        $map = $mapType.GetConstructors($flags)[0].Invoke(@([Dororong.Core.Geometry.PointD]::new(0,0), [Dororong.Core.Geometry.PointD]::new(0,0), [double]1.25, [double]1.25))
        foreach ($monitor in $lastScene.Monitors) {
            $point = [Dororong.Core.Geometry.PointD]::new($monitor.Bounds.X, $monitor.Bounds.Y)
            $logical = $mapType.GetMethod('ToLogical',$flags).Invoke($map,@($point))
            $physical = $mapType.GetMethod('ToPhysical',$flags).Invoke($map,@($logical))
            $roundtrips += @{ monitorId=$monitor.Id; physicalX=$point.X; physicalY=$point.Y; errorX=($physical.X-$point.X); errorY=($physical.Y-$point.Y) }
        }
    }
    if ($WpfProbeOnly -and $captureExit -eq 0) {
        Add-Type -AssemblyName PresentationFramework
        Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CoordinateProbeNative {
 [StructLayout(LayoutKind.Sequential)] public struct Point { public int X; public int Y; }
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
 [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
 [DllImport("user32.dll")] public static extern bool GetPhysicalCursorPos(out Point point);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
}
'@
        if (!$lastScene) { throw 'No native monitor scene available for WPF probe.' }
        $agreements = @()
        foreach ($monitor in $lastScene.Monitors) {
            $window = $null
            try {
                $window = [Windows.Window]::new()
                $window.ShowActivated=$false; $window.ShowInTaskbar=$false
                $window.WindowStyle=[Windows.WindowStyle]::None
                $window.ResizeMode=[Windows.ResizeMode]::NoResize
                $window.AllowsTransparency=$true; $window.Opacity=0
                $window.Width=32; $window.Height=32
                $window.Content=[Windows.Controls.Grid]::new()
                $window.Show()
                $handle=[Windows.Interop.WindowInteropHelper]::new($window).Handle
                $previous=[CoordinateProbeNative]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))
                if ($previous -eq [IntPtr]::Zero) { throw 'DPI scope failed.' }
                try {
                    if (![CoordinateProbeNative]::SetWindowPos($handle,[IntPtr]::Zero,([int]$monitor.Bounds.X+40),([int]$monitor.Bounds.Y+40),32,32,0x14)) { throw 'Own window placement failed.' }
                } finally { if ([CoordinateProbeNative]::SetThreadDpiAwarenessContext($previous) -eq [IntPtr]::Zero) { throw 'DPI restore failed.' } }
                $window.UpdateLayout()
                $grid=$window.Content
                $origin=$grid.PointToScreen([Windows.Point]::new(0,0))
                $axisX=$grid.PointToScreen([Windows.Point]::new(1,0))
                $axisY=$grid.PointToScreen([Windows.Point]::new(0,1))
                $client=[CoordinateProbeNative+Point]::new()
                $cursor=[CoordinateProbeNative+Point]::new()
                $previous=[CoordinateProbeNative]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))
                if ($previous -eq [IntPtr]::Zero) { throw 'DPI scope failed.' }
                try {
                    if (![CoordinateProbeNative]::ClientToScreen($handle,[ref]$client)) { throw 'ClientToScreen failed.' }
                    if (![CoordinateProbeNative]::GetPhysicalCursorPos([ref]$cursor)) { throw 'GetPhysicalCursorPos failed.' }
                } finally { if ([CoordinateProbeNative]::SetThreadDpiAwarenessContext($previous) -eq [IntPtr]::Zero) { throw 'DPI restore failed.' } }
                $map=$mapType.GetMethod('FromPresenter',[Reflection.BindingFlags]'Static,NonPublic').Invoke($null,@($grid,[Dororong.Core.Geometry.PointD]::new(0,0)))
                $mapped=$mapType.GetMethod('ToLogical',$flags).Invoke($map,@([Dororong.Core.Geometry.PointD]::new($cursor.X,$cursor.Y)))
                $wpfCursor=$grid.PointFromScreen([Windows.Point]::new($cursor.X,$cursor.Y))
                $dpi=[CoordinateProbeNative]::GetDpiForWindow($handle)
                $dx=$origin.X-$client.X; $dy=$origin.Y-$client.Y
                $sx=$axisX.X-$origin.X; $sy=$axisY.Y-$origin.Y
                $cx=$mapped.X-$wpfCursor.X; $cy=$mapped.Y-$wpfCursor.Y
                $agreements += @{ monitorId=$monitor.Id; dpi=$dpi; originX=$origin.X; originY=$origin.Y; nativeClientX=$client.X; nativeClientY=$client.Y; originErrorX=$dx; originErrorY=$dy; scaleX=$sx; scaleY=$sy; cursorMapErrorX=$cx; cursorMapErrorY=$cy; passed=([Math]::Abs($dx)-lt 0.001 -and [Math]::Abs($dy)-lt 0.001 -and [Math]::Abs($sx-$dpi/96.0)-lt 0.001 -and [Math]::Abs($sy-$dpi/96.0)-lt 0.001 -and [Math]::Abs($cx)-lt 0.001 -and [Math]::Abs($cy)-lt 0.001) }
            } finally { if ($window) { $window.Close() } }
        }
        @{ scope='Invisible nonactivating own WPF window on each current monitor; no pet/user window changed. Actual PointToScreen vs PMv2 ClientToScreen, axes vs window DPI, production map cursor vs PointFromScreen. Current configuration only; mixed-DPI transition and live pet UNVERIFIED.'; agreements=$agreements } | ConvertTo-Json -Depth 8
        if (@($agreements | Where-Object { !$_.passed }).Count) { exit 1 }
        exit 0
    }
    [ordered]@{
        scope='Actual DesktopMetadataReader.Read, capture-only stopwatch excluding JSON; back-to-back samples; first sample includes JIT. No window titles/content or screenshots.'
        samples=$samples
        captureVerified=($captureExit -eq 0)
        captureExitCode=$captureExit
        successCriterion='Exactly 21 successful captures and a usable final scene with windows collection and finite positive monitor bounds; any failed sample rejects verification even if an older scene exists.'
        monitors= $(if ($lastScene) { @($lastScene.Monitors) } else { @() })
        windowCount= $(if ($lastScene) { $lastScene.Windows.Count } else { 0 })
        visibleWindowCount= $(if ($lastScene) { @($lastScene.Windows | Where-Object Visible).Count } else { 0 })
        taskbars= $(if ($lastScene) { @($lastScene.GetType().GetProperty('Taskbars',$flags).GetValue($lastScene)) } else { @() })
        roundtripScope='Native monitor origins through synthetic 1.25 affine map; actual WPF PointToScreen/native-pixel agreement UNVERIFIED'
        roundtrips=$roundtrips
    } | ConvertTo-Json -Depth 10
    exit $captureExit
}

$attempt = Join-Path $repositoryRoot 'artifacts/repro/window-taskbar-platforms-20260907-attempt-1'
$run = Join-Path $attempt $RunName
if (Test-Path -LiteralPath $run) { throw "Refusing to overwrite existing evidence directory: $run" }
[void](New-Item -ItemType Directory -Path $run)
$results = [Collections.Generic.List[object]]::new()
function Invoke-Recorded([string]$Name, [string]$Program, [string[]]$Arguments) {
    $started = [DateTime]::UtcNow
    & $Program @Arguments *> (Join-Path $run "$Name.log")
    $code = $LASTEXITCODE
    $result = [ordered]@{ name=$Name; program=$Program; arguments=$Arguments; exitCode=$code; startedUtc=$started; endedUtc=[DateTime]::UtcNow }
    $results.Add($result)
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run "$Name.exit.json")
    Write-Host "$Name exit=$code"
}

Invoke-Recorded 'head' 'git' @('rev-parse','HEAD')
Invoke-Recorded 'branch' 'git' @('branch','--show-current')
Invoke-Recorded 'status-before' 'git' @('status','--short')
Invoke-Recorded 'core' 'dotnet' @('test','tests/Dororong.Core.Tests','-c','Release','--logger','trx;LogFileName=core.trx','--results-directory',$run)
Invoke-Recorded 'app' 'dotnet' @('test','tests/Dororong.App.Tests','-c','Release','--logger','trx;LogFileName=app.trx','--results-directory',$run,'--','xUnit.MaxParallelThreads=1','xUnit.ParallelizeTestCollections=false')
Invoke-Recorded 'runtime-composition' 'pwsh' @('-NoProfile','-File','tests/Dororong.App.RuntimeComposition.Tests.ps1','-Configuration','Release')
Invoke-Recorded 'direct-interaction-render' 'pwsh' @('-NoProfile','-File','tests/Dororong.App.DirectInteractionRender.Tests.ps1','-Configuration','Release')
# ExactArt creates its own GUID directory under .superpowers; it never reuses old evidence.
Invoke-Recorded 'exact-art' 'pwsh' @('-NoProfile','-File','tests/Dororong.App.ExactArt.Tests.ps1','-Configuration','Release')
Invoke-Recorded 'approved-assets' 'pwsh' @('-NoProfile','-File','tests/Dororong.App.BodyDragApprovedAssets.Tests.ps1')
Invoke-Recorded 'native-capture' 'pwsh' @('-NoProfile','-File',$PSCommandPath,'-ProbeOnly')
Invoke-Recorded 'wpf-coordinate-agreement' 'pwsh' @('-NoProfile','-STA','-File',$PSCommandPath,'-WpfProbeOnly')
Invoke-Recorded 'diff-check' 'git' @('diff','--check')
Invoke-Recorded 'status-after' 'git' @('status','--short')

$baseline = Get-Content -Raw -LiteralPath (Join-Path $attempt 'preservation-before.json') | ConvertFrom-Json
$hashChecks = @(foreach ($entry in $baseline) {
    $actual = if (Test-Path -LiteralPath $entry.Path -PathType Leaf) { (Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash } else { $null }
    [ordered]@{ path=$entry.Path; before=$entry.SHA256; after=$actual; unchanged=($entry.SHA256 -eq $actual) }
})
$hashChecks | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'preservation-after.json')
$intendedBaselineChanges = @(
    'src/Dororong.App/Controls/DororongPresenter.xaml.cs',
    'src/Dororong.App/PetLoop.cs', 'src/Dororong.App/Runtime/PetLoopRuntime.cs',
    'src/Dororong.Core/Behavior/PetBrain.cs', 'src/Dororong.Core/Behavior/PetInput.cs'
)
$unexpectedChanges = @($hashChecks | Where-Object { !$_.unchanged -and ($_.path.Replace('\','/') -notin $intendedBaselineChanges) })
$inventory = Get-Content -Raw -LiteralPath (Join-Path $attempt 'prior-file-inventory-before.json') | ConvertFrom-Json
$metadataChanges = @(foreach ($entry in $inventory) {
    $file = Get-Item -LiteralPath $entry.Path -ErrorAction SilentlyContinue
    if (!$file -or $file.Length -ne $entry.Length -or $file.LastWriteTimeUtc -ne [datetime]$entry.LastWriteTimeUtc) { $entry.Path }
})
$binaries = @(foreach ($name in @('Dororong.App.dll','Dororong.Core.dll')) {
    $path = Join-Path $binaryRoot $name
    @{ path=$path; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
})
$process = Get-Process -Id 52024 -ErrorAction SilentlyContinue
$counts = @(foreach ($name in @('core','app')) {
    $path = Join-Path $run "$name.trx"
    if (Test-Path -LiteralPath $path) { [xml]$trx=Get-Content -Raw -LiteralPath $path; @{ suite=$name; counters=$trx.TestRun.ResultSummary.Counters.OuterXml } }
})
$evidence = [ordered]@{
    createdUtc=[DateTime]::UtcNow; results=$results; testCounts=$counts; testedBinaries=$binaries
    baselineFiles=$hashChecks.Count; changedBaseline=@($hashChecks | Where-Object { !$_.unchanged })
    unexpectedBaselineChanges=$unexpectedChanges
    priorArtifactMetadata=@{ checked=$inventory.Count; changed=$metadataChanges; scope='Path, size and mtime only; not exhaustive byte identity' }
    oldProcess=@{ expectedPid=52024; running=($null -ne $process); observedPath=$(if ($process) { $process.Path } else { $null }) }
    runtimeApplied=$false; publication='WITHHELD pending final broad review'; actualDesktopAcceptance='UNVERIFIED'
}
$evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $run 'evidence.json')
if (@($results | Where-Object { $_.exitCode -ne 0 }).Count -gt 0) { exit 1 }
if ($metadataChanges.Count -gt 0) { exit 2 }
if ($unexpectedChanges.Count -gt 0) { exit 3 }
Write-Host "Evidence: $run"
