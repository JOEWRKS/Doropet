param(
    [string]$Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Near([double]$Expected, [double]$Actual, [double]$Tolerance, [string]$Message)
{
    if ([Math]::Abs($Expected - $Actual) -gt $Tolerance)
    {
        throw "$Message Expected '$Expected' +/- '$Tolerance', observed '$Actual'."
    }
}

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -ne $Actual)
    {
        throw "$Message Expected '$Expected', observed '$Actual'."
    }
}

function Assert-Frame([System.Windows.Controls.Image]$Image, [string]$ExpectedFileName, [string]$State)
{
    if (-not $Image.Source.ToString().EndsWith($ExpectedFileName, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "$State did not use $ExpectedFileName."
    }
}

$currentThread = [Threading.Thread]::CurrentThread
Assert-Equal 'STA' $currentThread.GetApartmentState().ToString() `
    'The focused WPF presenter test must run in an STA apartment.'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$coreAssemblyPath = Join-Path $repositoryRoot "src/Dororong.Core/bin/$Configuration/net8.0/Dororong.Core.dll"
$appAssemblyPath = Join-Path $repositoryRoot "src/Dororong.App/bin/$Configuration/net8.0-windows/Dororong.App.dll"

Add-Type -AssemblyName PresentationFramework
Add-Type -Path $coreAssemblyPath
Add-Type -Path $appAssemblyPath

$presenter = [Dororong.App.Controls.DororongPresenter]::new()
$scale = $presenter.FindName('BodyScaleTransform')
$rotation = $presenter.FindName('BodyRotateTransform')
$translation = $presenter.FindName('BodyTranslateTransform')
$image = [System.Windows.Controls.Image]$presenter.FindName('DororongImage')
$breathingScale = $presenter.FindName('ImageBreathingScaleTransform')

foreach ($namedPart in @(
    @{ Name = 'BodyScaleTransform'; Value = $scale },
    @{ Name = 'BodyRotateTransform'; Value = $rotation },
    @{ Name = 'BodyTranslateTransform'; Value = $translation },
    @{ Name = 'DororongImage'; Value = $image },
    @{ Name = 'ImageBreathingScaleTransform'; Value = $breathingScale }))
{
    if ($null -eq $namedPart.Value)
    {
        throw "$($namedPart.Name) was not found in the presenter namescope."
    }
}

Assert-Equal $true ([Object]::ReferenceEquals($image.RenderTransform, $breathingScale)) `
    'The dedicated breathing scale was not applied directly to the authored image.'

$state = [Dororong.Core.Behavior.PetState]
$facing = [Dororong.Core.Behavior.FacingDirection]::Right
$origin = [Dororong.Core.Geometry.PointD]::new(0, 0)

function New-Snapshot(
    [Dororong.Core.Behavior.PetState]$State,
    [double]$Phase,
    [Nullable[Dororong.Core.Geometry.PointD]]$GrabOffset = $null)
{
    return [Dororong.Core.Behavior.PetSnapshot]::new(
        $State,
        $origin,
        $facing,
        $Phase,
        $false,
        $GrabOffset)
}

function Get-RenderedVisibleGeometry(
    [Dororong.App.Controls.DororongPresenter]$Presenter)
{
    $size = [System.Windows.Size]::new(144, 144)
    $Presenter.Measure($size)
    $Presenter.Arrange([System.Windows.Rect]::new([System.Windows.Point]::new(0, 0), $size))
    $Presenter.UpdateLayout()

    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
        144,
        144,
        96,
        96,
        [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($Presenter)

    $stride = 144 * 4
    $pixels = [byte[]]::new($stride * 144)
    $bitmap.CopyPixels($pixels, $stride, 0)

    [double]$weight = 0
    $minimumX = 144
    $minimumY = 144
    $maximumX = -1
    $maximumY = -1
    for ($y = 0; $y -lt 144; $y++)
    {
        for ($x = 0; $x -lt 144; $x++)
        {
            $alpha = [double]$pixels[($y * $stride) + ($x * 4) + 3]
            if ($alpha -le 8)
            {
                continue
            }

            $minimumX = [Math]::Min($minimumX, $x)
            $minimumY = [Math]::Min($minimumY, $y)
            $maximumX = [Math]::Max($maximumX, $x)
            $maximumY = [Math]::Max($maximumY, $y)
            $weight += $alpha
        }
    }

    if ($weight -le 0)
    {
        throw 'The rendered presenter contained no visible alpha for centroid measurement.'
    }

    return [pscustomobject]@{
        Width = $maximumX - $minimumX + 1
        Height = $maximumY - $minimumY + 1
        MaximumY = $maximumY
    }
}

Assert-Near 0.428987 ([double]$image.RenderTransformOrigin.X) 0.0000001 `
    'SLEEP breathing origin did not use the visible horizontal center.'
Assert-Near 0.916667 ([double]$image.RenderTransformOrigin.Y) 0.0000001 `
    'SLEEP breathing origin did not use the visible foot baseline.'

$presenter.Render((New-Snapshot $state::Sleep 0.0))
$restingGeometry = Get-RenderedVisibleGeometry $presenter
$presenter.Render((New-Snapshot $state::Sleep 0.405))
$peakGeometry = Get-RenderedVisibleGeometry $presenter
$widthGrowth = $peakGeometry.Width - $restingGeometry.Width
$heightGrowth = $peakGeometry.Height - $restingGeometry.Height
$footBaselineDelta = $peakGeometry.MaximumY - $restingGeometry.MaximumY
if ($widthGrowth -le $heightGrowth)
{
    throw "Peak SLEEP directional breathing did not grow more in width than height. Width growth='$widthGrowth' px, height growth='$heightGrowth' px."
}
if ([Math]::Abs($footBaselineDelta) -gt 1)
{
    throw "Peak SLEEP directional breathing moved the visible foot baseline by '$footBaselineDelta' px; expected at most one raster pixel."
}

$sleepCases = @(
    @{ Name = 'start'; Phase = 0.0; ScaleX = 1.0; ScaleY = 1.0 },
    @{ Name = 'inhale midpoint'; Phase = 0.19; ScaleX = 1.012; ScaleY = 1.006 },
    @{ Name = 'hold entry'; Phase = 0.38; ScaleX = 1.024; ScaleY = 1.012 },
    @{ Name = 'hold midpoint'; Phase = 0.405; ScaleX = 1.024; ScaleY = 1.012 },
    @{ Name = 'hold exit'; Phase = 0.43; ScaleX = 1.024; ScaleY = 1.012 },
    @{ Name = 'exhale midpoint'; Phase = 0.715; ScaleX = 1.012; ScaleY = 1.006 },
    @{ Name = 'return'; Phase = 1.0; ScaleX = 1.0; ScaleY = 1.0 }
)

foreach ($sleepCase in $sleepCases)
{
    $presenter.Render((New-Snapshot $state::Sleep $sleepCase.Phase))
    Assert-Frame $image 'dororong-closed-eyes.png' "SLEEP $($sleepCase.Name) phase"
    Assert-Near 1.0 ([double]$scale.ScaleX) 0.000001 "SLEEP $($sleepCase.Name) phase changed body ScaleX."
    Assert-Near 1.0 ([double]$scale.ScaleY) 0.000001 "SLEEP $($sleepCase.Name) phase changed body ScaleY."
    Assert-Near 0.0 ([double]$translation.Y) 0.000001 "SLEEP $($sleepCase.Name) phase moved the whole body vertically."
    Assert-Near $sleepCase.ScaleX ([double]$breathingScale.ScaleX) 0.000001 `
        "SLEEP $($sleepCase.Name) phase breathing ScaleX changed."
    Assert-Near $sleepCase.ScaleY ([double]$breathingScale.ScaleY) 0.000001 `
        "SLEEP $($sleepCase.Name) phase breathing ScaleY changed."
}

$sleepBreathingScalesX = [System.Collections.Generic.List[double]]::new()
$sleepBreathingScalesY = [System.Collections.Generic.List[double]]::new()
$increasingSampleCount = 0
$decreasingSampleCount = 0
$peakHoldSampleCount = 0
for ($tick = 0; $tick -le 250; $tick++)
{
    $phase = ($tick * 0.016) / 4.0
    $presenter.Render((New-Snapshot $state::Sleep $phase))
    Assert-Equal $true $image.Source.ToString().EndsWith('dororong-closed-eyes.png', [StringComparison]::OrdinalIgnoreCase) `
        "SLEEP tick $tick did not use dororong-closed-eyes.png."
    Assert-Near 1.0 ([double]$scale.ScaleX) 0.000001 "SLEEP tick $tick changed body ScaleX."
    Assert-Near 1.0 ([double]$scale.ScaleY) 0.000001 "SLEEP tick $tick changed body ScaleY."
    Assert-Near 0.0 ([double]$translation.Y) 0.000001 "SLEEP tick $tick moved the whole body vertically."
    $sleepBreathingScalesX.Add([double]$breathingScale.ScaleX)
    $sleepBreathingScalesY.Add([double]$breathingScale.ScaleY)
    if ([Math]::Abs([double]$breathingScale.ScaleX - 1.024) -le 0.000000001 -and
        [Math]::Abs([double]$breathingScale.ScaleY - 1.012) -le 0.000000001)
    {
        $peakHoldSampleCount++
    }
    if ($sleepBreathingScalesX.Count -gt 1)
    {
        $scaleXDelta =
            $sleepBreathingScalesX[$sleepBreathingScalesX.Count - 1] -
            $sleepBreathingScalesX[$sleepBreathingScalesX.Count - 2]
        $scaleYDelta =
            $sleepBreathingScalesY[$sleepBreathingScalesY.Count - 1] -
            $sleepBreathingScalesY[$sleepBreathingScalesY.Count - 2]
        if ([Math]::Abs($scaleXDelta) -gt 0.000381)
        {
            throw "SLEEP tick $tick exceeded the 16 ms adjacent ScaleX continuity limit. Expected <= '0.000381', observed '$([Math]::Abs($scaleXDelta))'."
        }
        if ([Math]::Abs($scaleYDelta) -gt 0.000191)
        {
            throw "SLEEP tick $tick exceeded the 16 ms adjacent ScaleY continuity limit. Expected <= '0.000191', observed '$([Math]::Abs($scaleYDelta))'."
        }
        if ($scaleXDelta -gt 0.000000001) { $increasingSampleCount++ }
        elseif ($scaleXDelta -lt -0.000000001) { $decreasingSampleCount++ }
    }

    if ([double]$breathingScale.ScaleX -lt 1.0 -or [double]$breathingScale.ScaleX -gt 1.024 -or
        [double]$breathingScale.ScaleY -lt 1.0 -or [double]$breathingScale.ScaleY -gt 1.012)
    {
        throw "SLEEP tick $tick left the directional breathing-scale bounds. Expected X '1.0..1.024' and Y '1.0..1.012', observed '$($breathingScale.ScaleX),$($breathingScale.ScaleY)'."
    }
}
Assert-Equal 13 $peakHoldSampleCount `
    'SLEEP did not preserve the 0.38..0.43 full-inhale hold across the 16 ms samples.'
if ($increasingSampleCount -ge $decreasingSampleCount)
{
    throw "SLEEP inhale was not shorter than exhale; observed '$increasingSampleCount' increasing samples and '$decreasingSampleCount' decreasing samples."
}

$wakeCases = @(
    @{ State = $state::Walk; Phase = 0.25; GrabOffset = $null; ScaleX = 1.0; ScaleY = 1.0; Rotation = 0.0; TranslateY = -4.0 },
    @{ State = $state::Curious; Phase = 0.25; GrabOffset = $null; ScaleX = 1.0; ScaleY = 1.0; Rotation = 7.0; TranslateY = 0.0 },
    @{ State = $state::Startled; Phase = 0.25; GrabOffset = $null; ScaleX = 1.12727922061358; ScaleY = 0.901005050633883; Rotation = 0.0; TranslateY = 0.0 },
    @{ State = $state::ClickReaction; Phase = 0.5; GrabOffset = $null; ScaleX = 1.0; ScaleY = 1.0; Rotation = 0.0; TranslateY = -10.0 },
    @{ State = $state::Dragged; Phase = 0.25; GrabOffset = [Dororong.Core.Geometry.PointD]::new(76, 48); ScaleX = 1.0; ScaleY = 1.12; Rotation = 4.0; TranslateY = 0.0 }
)

foreach ($wakeCase in $wakeCases)
{
    $presenter.Render((New-Snapshot $state::Sleep 0.405))
    Assert-Near 1.024 ([double]$breathingScale.ScaleX) 0.000001 `
        "$($wakeCase.State) reset precondition did not start from peak SLEEP breathing ScaleX."
    Assert-Near 1.012 ([double]$breathingScale.ScaleY) 0.000001 `
        "$($wakeCase.State) reset precondition did not start from peak SLEEP breathing ScaleY."

    $presenter.Render((New-Snapshot $wakeCase.State $wakeCase.Phase $wakeCase.GrabOffset))
    Assert-Frame $image 'dororong-canonical.png' $wakeCase.State.ToString()
    Assert-Near $wakeCase.ScaleX ([double]$scale.ScaleX) 0.000001 "$($wakeCase.State) ScaleX retained a SLEEP transform."
    Assert-Near $wakeCase.ScaleY ([double]$scale.ScaleY) 0.000001 "$($wakeCase.State) ScaleY retained a SLEEP transform."
    Assert-Near $wakeCase.Rotation ([double]$rotation.Angle) 0.000001 "$($wakeCase.State) rotation changed."
    Assert-Near $wakeCase.TranslateY ([double]$translation.Y) 0.000001 "$($wakeCase.State) retained a SLEEP vertical offset."
    Assert-Near 1.0 ([double]$breathingScale.ScaleX) 0.000001 `
        "$($wakeCase.State) retained SLEEP breathing ScaleX."
    Assert-Near 1.0 ([double]$breathingScale.ScaleY) 0.000001 `
        "$($wakeCase.State) retained SLEEP breathing ScaleY."
}

Write-Output ("SLEEP POSE PASS: asymmetric foot-anchored directional breathing over four seconds, " +
    "rendered bounds=$($restingGeometry.Width)x$($restingGeometry.Height)->$($peakGeometry.Width)x$($peakGeometry.Height), " +
    "foot-baseline delta=$footBaselineDelta px, inhale/exhale samples=$increasingSampleCount/$decreasingSampleCount, " +
    'zero body Y, closed-frame selection, and every non-breathing pose reset passed.')
