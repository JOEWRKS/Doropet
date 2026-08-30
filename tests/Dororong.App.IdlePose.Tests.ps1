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
$bodyScale = $presenter.FindName('BodyScaleTransform')
$rotation = $presenter.FindName('BodyRotateTransform')
$translation = $presenter.FindName('BodyTranslateTransform')
$image = [System.Windows.Controls.Image]$presenter.FindName('DororongImage')
$breathingScale = $presenter.FindName('ImageBreathingScaleTransform')

foreach ($namedPart in @(
    @{ Name = 'BodyScaleTransform'; Value = $bodyScale },
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
    [double]$Phase)
{
    return [Dororong.Core.Behavior.PetSnapshot]::new(
        $State,
        $origin,
        $facing,
        $Phase,
        $false,
        $null)
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
    [double]$weightedX = 0
    [double]$weightedY = 0
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
            $weightedX += ($x + 0.5) * $alpha
            $weightedY += ($y + 0.5) * $alpha
        }
    }

    if ($weight -le 0)
    {
        throw 'The rendered presenter contained no visible alpha for geometry measurement.'
    }

    return [pscustomobject]@{
        Width = $maximumX - $minimumX + 1
        Height = $maximumY - $minimumY + 1
        CentroidX = $weightedX / $weight
        CentroidY = $weightedY / $weight
    }
}

$failures = [Collections.Generic.List[string]]::new()

$presenter.Render((New-Snapshot $state::Idle 0.0))
$restingGeometry = Get-RenderedVisibleGeometry $presenter
$presenter.Render((New-Snapshot $state::Idle 0.5))
$peakGeometry = Get-RenderedVisibleGeometry $presenter
$centroidDeltaX = $peakGeometry.CentroidX - $restingGeometry.CentroidX
$centroidDeltaY = $peakGeometry.CentroidY - $restingGeometry.CentroidY
if ($peakGeometry.Width -le $restingGeometry.Width)
{
    $failures.Add("Peak IDLE rendered width did not grow. Rest='$($restingGeometry.Width)', peak='$($peakGeometry.Width)'.")
}
if ($peakGeometry.Height -le $restingGeometry.Height)
{
    $failures.Add("Peak IDLE rendered height did not grow. Rest='$($restingGeometry.Height)', peak='$($peakGeometry.Height)'.")
}
if ([Math]::Abs($centroidDeltaX) -gt 0.1)
{
    $failures.Add("Peak IDLE growth moved the rendered alpha-weighted visible centroid horizontally by '$centroidDeltaX' px.")
}
if ([Math]::Abs($centroidDeltaY) -gt 0.1)
{
    $failures.Add("Peak IDLE growth moved the rendered alpha-weighted visible centroid vertically by '$centroidDeltaY' px.")
}

$idleCases = @(
    @{ Name = 'start'; Phase = 0.0; BreathingScale = 1.0; Frame = 'dororong-canonical.png' },
    @{ Name = 'expansion'; Phase = 0.25; BreathingScale = 1.0282842712474618; Frame = 'dororong-canonical.png' },
    @{ Name = 'peak'; Phase = 0.5; BreathingScale = 1.04; Frame = 'dororong-canonical.png' },
    @{ Name = 'return'; Phase = 0.75; BreathingScale = 1.0282842712474618; Frame = 'dororong-blink-squint.png' },
    @{ Name = 'end'; Phase = 1.0; BreathingScale = 1.0; Frame = 'dororong-canonical.png' }
)

foreach ($idleCase in $idleCases)
{
    $presenter.Render((New-Snapshot $state::Idle $idleCase.Phase))
    if (-not $image.Source.ToString().EndsWith($idleCase.Frame, [StringComparison]::OrdinalIgnoreCase))
    {
        $failures.Add("IDLE $($idleCase.Name) phase did not use $($idleCase.Frame).")
    }
    if ([Math]::Abs([double]$bodyScale.ScaleX - 1.0) -gt 0.000001 -or
        [Math]::Abs([double]$bodyScale.ScaleY - 1.0) -gt 0.000001)
    {
        $failures.Add("IDLE $($idleCase.Name) phase changed the body scale.")
    }
    if ([Math]::Abs([double]$translation.Y) -gt 0.000001)
    {
        $failures.Add("IDLE $($idleCase.Name) phase moved the whole body vertically; observed '$([double]$translation.Y)'.")
    }
    if ([Math]::Abs([double]$breathingScale.ScaleX - $idleCase.BreathingScale) -gt 0.000001 -or
        [Math]::Abs([double]$breathingScale.ScaleY - $idleCase.BreathingScale) -gt 0.000001)
    {
        $failures.Add("IDLE $($idleCase.Name) phase expected uniform image scale '$($idleCase.BreathingScale)'; observed '$([double]$breathingScale.ScaleX),$([double]$breathingScale.ScaleY)'.")
    }
}

$translatedSampleCount = 0
$minimumSampleScale = [double]::PositiveInfinity
$maximumSampleScale = [double]::NegativeInfinity
$previousScale = $null
for ($tick = 0; $tick -le 250; $tick++)
{
    $phase = ($tick * 0.016) / 4.0
    $presenter.Render((New-Snapshot $state::Idle $phase))
    if ([Math]::Abs([double]$translation.Y) -gt 0.000001)
    {
        $translatedSampleCount++
    }

    $currentScale = [double]$breathingScale.ScaleX
    $minimumSampleScale = [Math]::Min($minimumSampleScale, $currentScale)
    $maximumSampleScale = [Math]::Max($maximumSampleScale, $currentScale)
    if ([Math]::Abs($currentScale - [double]$breathingScale.ScaleY) -gt 0.000001)
    {
        $failures.Add("IDLE tick $tick stretched the image non-uniformly.")
        break
    }
    if ($null -ne $previousScale -and [Math]::Abs($currentScale - [double]$previousScale) -gt 0.000503)
    {
        $failures.Add("IDLE tick $tick exceeded the 16 ms adjacent breathing-scale continuity limit; observed '$([Math]::Abs($currentScale - [double]$previousScale))'.")
        break
    }
    $previousScale = $currentScale
}
if ($translatedSampleCount -gt 0)
{
    $failures.Add("IDLE whole-body Y translation was nonzero in '$translatedSampleCount' of 251 samples over four seconds.")
}
if ([Math]::Abs($minimumSampleScale - 1.0) -gt 0.000001 -or
    [Math]::Abs($maximumSampleScale - 1.04) -gt 0.000001)
{
    $failures.Add("IDLE sampled image scale did not span '1.0..1.04'; observed '$minimumSampleScale..$maximumSampleScale'.")
}

$presenter.Render((New-Snapshot $state::Idle 0.5))
if ([Math]::Abs([double]$breathingScale.ScaleX - 1.04) -gt 0.000001)
{
    $failures.Add("Curious reset precondition did not start from peak IDLE image scale; observed '$([double]$breathingScale.ScaleX)'.")
}
$presenter.Render((New-Snapshot $state::Curious 0.25))
try
{
    Assert-Frame $image 'dororong-canonical.png' 'Curious reset'
    Assert-Near 1.0 ([double]$bodyScale.ScaleX) 0.000001 'Curious reset changed body ScaleX.'
    Assert-Near 1.0 ([double]$bodyScale.ScaleY) 0.000001 'Curious reset changed body ScaleY.'
    Assert-Near 7.0 ([double]$rotation.Angle) 0.000001 'Curious reset changed the Curious rotation.'
    Assert-Near 0.0 ([double]$translation.Y) 0.000001 'Curious reset retained an IDLE vertical offset.'
    Assert-Near 1.0 ([double]$breathingScale.ScaleX) 0.000001 'Curious reset retained IDLE breathing ScaleX.'
    Assert-Near 1.0 ([double]$breathingScale.ScaleY) 0.000001 'Curious reset retained IDLE breathing ScaleY.'
}
catch
{
    $failures.Add($_.Exception.Message)
}

if ($failures.Count -gt 0)
{
    throw ($failures -join [Environment]::NewLine)
}

Write-Output ("IDLE POSE PASS: visible-center continuous image growth over four seconds, " +
    "rendered bounds=$($restingGeometry.Width)x$($restingGeometry.Height)->$($peakGeometry.Width)x$($peakGeometry.Height), " +
    "centroid delta=($([Math]::Round($centroidDeltaX, 6)),$([Math]::Round($centroidDeltaY, 6))) px, " +
    'zero body Y across 251 samples, canonical idle frame, and non-IDLE pose reset passed.')
