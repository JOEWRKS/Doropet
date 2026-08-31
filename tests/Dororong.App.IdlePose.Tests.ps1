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
$render = @($presenter.GetType().GetMethods([Reflection.BindingFlags]'Instance,NonPublic') |
    Where-Object { $_.Name -eq 'Render' -and $_.GetParameters().Count -eq 2 })[0]
if ($null -eq $render) { throw 'The presenter combined Render contract was not found.' }
$noDirectInteraction = $render.GetParameters()[1].ParameterType.GetProperty(
    'None', [Reflection.BindingFlags]'Static,Public,NonPublic').GetValue($null)
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
        throw 'The rendered presenter contained no visible alpha for geometry measurement.'
    }

    return [pscustomobject]@{
        Width = $maximumX - $minimumX + 1
        Height = $maximumY - $minimumY + 1
        MaximumY = $maximumY
    }
}

$failures = [Collections.Generic.List[string]]::new()

if ([Math]::Abs([double]$image.RenderTransformOrigin.X - 0.428987) -gt 0.0000001 -or
    [Math]::Abs([double]$image.RenderTransformOrigin.Y - 0.916667) -gt 0.0000001)
{
    $failures.Add(
        "IDLE breathing origin must use the visible horizontal center and foot baseline '0.428987,0.916667'; observed '$($image.RenderTransformOrigin.X),$($image.RenderTransformOrigin.Y)'.")
}

$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Idle 0.0), $noDirectInteraction)) | Out-Null
$restingGeometry = Get-RenderedVisibleGeometry $presenter
$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Idle 0.405), $noDirectInteraction)) | Out-Null
$peakGeometry = Get-RenderedVisibleGeometry $presenter
$widthGrowth = $peakGeometry.Width - $restingGeometry.Width
$heightGrowth = $peakGeometry.Height - $restingGeometry.Height
$footBaselineDelta = $peakGeometry.MaximumY - $restingGeometry.MaximumY
if ($widthGrowth -le 0)
{
    $failures.Add("Peak IDLE rendered width did not grow. Rest='$($restingGeometry.Width)', peak='$($peakGeometry.Width)'.")
}
if ($heightGrowth -le 0)
{
    $failures.Add("Peak IDLE rendered height did not grow. Rest='$($restingGeometry.Height)', peak='$($peakGeometry.Height)'.")
}
if ($widthGrowth -le $heightGrowth)
{
    $failures.Add("Peak IDLE directional breathing did not grow more in width than height. Width growth='$widthGrowth' px, height growth='$heightGrowth' px.")
}
if ([Math]::Abs($footBaselineDelta) -gt 1)
{
    $failures.Add("Peak IDLE directional breathing moved the visible foot baseline by '$footBaselineDelta' px; expected at most one raster pixel.")
}

$idleCases = @(
    @{ Name = 'start'; Phase = 0.0; ScaleX = 1.0; ScaleY = 1.0; Frame = 'dororong-canonical.png' },
    @{ Name = 'inhale midpoint'; Phase = 0.19; ScaleX = 1.012; ScaleY = 1.006; Frame = 'dororong-canonical.png' },
    @{ Name = 'hold entry'; Phase = 0.38; ScaleX = 1.024; ScaleY = 1.012; Frame = 'dororong-canonical.png' },
    @{ Name = 'hold midpoint'; Phase = 0.405; ScaleX = 1.024; ScaleY = 1.012; Frame = 'dororong-canonical.png' },
    @{ Name = 'hold exit'; Phase = 0.43; ScaleX = 1.024; ScaleY = 1.012; Frame = 'dororong-canonical.png' },
    @{ Name = 'exhale midpoint'; Phase = 0.715; ScaleX = 1.012; ScaleY = 1.006; Frame = 'dororong-closed-eyes.png' },
    @{ Name = 'return'; Phase = 1.0; ScaleX = 1.0; ScaleY = 1.0; Frame = 'dororong-canonical.png' }
)

foreach ($idleCase in $idleCases)
{
    $render.Invoke($presenter, [object[]]@((New-Snapshot $state::Idle $idleCase.Phase), $noDirectInteraction)) | Out-Null
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
    if ([Math]::Abs([double]$breathingScale.ScaleX - $idleCase.ScaleX) -gt 0.000001 -or
        [Math]::Abs([double]$breathingScale.ScaleY - $idleCase.ScaleY) -gt 0.000001)
    {
        $failures.Add("IDLE $($idleCase.Name) phase expected directional image scale '$($idleCase.ScaleX),$($idleCase.ScaleY)'; observed '$([double]$breathingScale.ScaleX),$([double]$breathingScale.ScaleY)'.")
    }
}

$translatedSampleCount = 0
$minimumSampleScaleX = [double]::PositiveInfinity
$maximumSampleScaleX = [double]::NegativeInfinity
$minimumSampleScaleY = [double]::PositiveInfinity
$maximumSampleScaleY = [double]::NegativeInfinity
$previousScaleX = $null
$previousScaleY = $null
$increasingSampleCount = 0
$decreasingSampleCount = 0
$peakHoldSampleCount = 0
$scaleXContinuityFailure = $false
$scaleYContinuityFailure = $false
for ($tick = 0; $tick -le 250; $tick++)
{
    $phase = ($tick * 0.016) / 4.0
    $render.Invoke($presenter, [object[]]@((New-Snapshot $state::Idle $phase), $noDirectInteraction)) | Out-Null
    if ([Math]::Abs([double]$translation.Y) -gt 0.000001)
    {
        $translatedSampleCount++
    }

    $currentScaleX = [double]$breathingScale.ScaleX
    $currentScaleY = [double]$breathingScale.ScaleY
    $minimumSampleScaleX = [Math]::Min($minimumSampleScaleX, $currentScaleX)
    $maximumSampleScaleX = [Math]::Max($maximumSampleScaleX, $currentScaleX)
    $minimumSampleScaleY = [Math]::Min($minimumSampleScaleY, $currentScaleY)
    $maximumSampleScaleY = [Math]::Max($maximumSampleScaleY, $currentScaleY)
    if ([Math]::Abs($currentScaleX - 1.024) -le 0.000000001 -and
        [Math]::Abs($currentScaleY - 1.012) -le 0.000000001)
    {
        $peakHoldSampleCount++
    }
    if ($null -ne $previousScaleX)
    {
        $scaleXDelta = $currentScaleX - [double]$previousScaleX
        $scaleYDelta = $currentScaleY - [double]$previousScaleY
        if ([Math]::Abs($scaleXDelta) -gt 0.000381 -and -not $scaleXContinuityFailure)
        {
            $failures.Add("IDLE tick $tick exceeded the 16 ms adjacent ScaleX continuity limit; observed '$([Math]::Abs($scaleXDelta))'.")
            $scaleXContinuityFailure = $true
        }
        if ([Math]::Abs($scaleYDelta) -gt 0.000191 -and -not $scaleYContinuityFailure)
        {
            $failures.Add("IDLE tick $tick exceeded the 16 ms adjacent ScaleY continuity limit; observed '$([Math]::Abs($scaleYDelta))'.")
            $scaleYContinuityFailure = $true
        }
        if ($scaleXDelta -gt 0.000000001) { $increasingSampleCount++ }
        elseif ($scaleXDelta -lt -0.000000001) { $decreasingSampleCount++ }
    }
    $previousScaleX = $currentScaleX
    $previousScaleY = $currentScaleY
}
if ($translatedSampleCount -gt 0)
{
    $failures.Add("IDLE whole-body Y translation was nonzero in '$translatedSampleCount' of 251 samples over four seconds.")
}
if ([Math]::Abs($minimumSampleScaleX - 1.0) -gt 0.000001 -or
    [Math]::Abs($maximumSampleScaleX - 1.024) -gt 0.000001 -or
    [Math]::Abs($minimumSampleScaleY - 1.0) -gt 0.000001 -or
    [Math]::Abs($maximumSampleScaleY - 1.012) -gt 0.000001)
{
    $failures.Add("IDLE sampled directional scale did not span X '1.0..1.024' and Y '1.0..1.012'; observed X '$minimumSampleScaleX..$maximumSampleScaleX', Y '$minimumSampleScaleY..$maximumSampleScaleY'.")
}
if ($peakHoldSampleCount -ne 13)
{
    $failures.Add("IDLE did not preserve the 0.38..0.43 full-inhale hold across the 16 ms samples; expected 13 peak samples, observed '$peakHoldSampleCount'.")
}
if ($increasingSampleCount -ge $decreasingSampleCount)
{
    $failures.Add("IDLE inhale was not shorter than exhale; observed '$increasingSampleCount' increasing samples and '$decreasingSampleCount' decreasing samples.")
}

$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Idle 0.405), $noDirectInteraction)) | Out-Null
if ([Math]::Abs([double]$breathingScale.ScaleX - 1.024) -gt 0.000001 -or
    [Math]::Abs([double]$breathingScale.ScaleY - 1.012) -gt 0.000001)
{
    $failures.Add("Curious reset precondition did not start from peak IDLE directional scale; observed '$([double]$breathingScale.ScaleX),$([double]$breathingScale.ScaleY)'.")
}
$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Curious 0.25), $noDirectInteraction)) | Out-Null
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

Write-Output ("IDLE POSE PASS: asymmetric foot-anchored directional breathing over four seconds, " +
    "rendered bounds=$($restingGeometry.Width)x$($restingGeometry.Height)->$($peakGeometry.Width)x$($peakGeometry.Height), " +
    "foot-baseline delta=$footBaselineDelta px, inhale/exhale samples=$increasingSampleCount/$decreasingSampleCount, " +
    'zero body Y across 251 samples, canonical idle frame, and non-IDLE pose reset passed.')
