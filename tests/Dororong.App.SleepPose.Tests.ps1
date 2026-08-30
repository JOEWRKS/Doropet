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
Assert-Near 0.5 ([double]$image.RenderTransformOrigin.X) 0.000001 `
    'The image breathing scale was not horizontally centered.'
Assert-Near 1.0 ([double]$image.RenderTransformOrigin.Y) 0.000001 `
    'The image breathing scale was not anchored to the bottom edge.'

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

$sleepCases = @(
    @{ Name = 'start'; Phase = 0.0; BreathingScale = 1.0 },
    @{ Name = 'expansion'; Phase = 0.25; BreathingScale = 1.0084852813742386 },
    @{ Name = 'peak'; Phase = 0.5; BreathingScale = 1.012 },
    @{ Name = 'return'; Phase = 0.75; BreathingScale = 1.0084852813742386 },
    @{ Name = 'end'; Phase = 1.0; BreathingScale = 1.0 }
)

foreach ($sleepCase in $sleepCases)
{
    $presenter.Render((New-Snapshot $state::Sleep $sleepCase.Phase))
    Assert-Frame $image 'dororong-closed-eyes.png' "SLEEP $($sleepCase.Name) phase"
    Assert-Near 1.0 ([double]$scale.ScaleX) 0.000001 "SLEEP $($sleepCase.Name) phase changed body ScaleX."
    Assert-Near 1.0 ([double]$scale.ScaleY) 0.000001 "SLEEP $($sleepCase.Name) phase changed body ScaleY."
    Assert-Near 0.0 ([double]$translation.Y) 0.000001 "SLEEP $($sleepCase.Name) phase moved the whole body vertically."
    Assert-Near $sleepCase.BreathingScale ([double]$breathingScale.ScaleX) 0.000001 `
        "SLEEP $($sleepCase.Name) phase breathing ScaleX changed."
    Assert-Near $sleepCase.BreathingScale ([double]$breathingScale.ScaleY) 0.000001 `
        "SLEEP $($sleepCase.Name) phase breathing ScaleY changed."
}

$sleepBreathingScales = [System.Collections.Generic.List[double]]::new()
for ($tick = 0; $tick -le 150; $tick++)
{
    $phase = ($tick * 0.016) / 2.4
    $presenter.Render((New-Snapshot $state::Sleep $phase))
    Assert-Equal $true $image.Source.ToString().EndsWith('dororong-closed-eyes.png', [StringComparison]::OrdinalIgnoreCase) `
        "SLEEP tick $tick did not use dororong-closed-eyes.png."
    Assert-Near 1.0 ([double]$scale.ScaleX) 0.000001 "SLEEP tick $tick changed body ScaleX."
    Assert-Near 1.0 ([double]$scale.ScaleY) 0.000001 "SLEEP tick $tick changed body ScaleY."
    Assert-Near 0.0 ([double]$translation.Y) 0.000001 "SLEEP tick $tick moved the whole body vertically."
    Assert-Near ([double]$breathingScale.ScaleX) ([double]$breathingScale.ScaleY) 0.000001 `
        "SLEEP tick $tick stretched the image non-uniformly."

    $sleepBreathingScales.Add([double]$breathingScale.ScaleX)
    if ($sleepBreathingScales.Count -gt 1)
    {
        $adjacentDelta = [Math]::Abs(
            $sleepBreathingScales[$sleepBreathingScales.Count - 1] -
            $sleepBreathingScales[$sleepBreathingScales.Count - 2])
        if ($adjacentDelta -gt 0.000252)
        {
            throw "SLEEP tick $tick exceeded the 16 ms adjacent breathing-scale continuity limit. Expected <= '0.000252', observed '$adjacentDelta'."
        }
    }

    if ([double]$breathingScale.ScaleX -lt 1.0 -or [double]$breathingScale.ScaleX -gt 1.012)
    {
        throw "SLEEP tick $tick left the breathing-scale bounds. Expected '1.0..1.012', observed '$($breathingScale.ScaleX)'."
    }
}

$wakeCases = @(
    @{ State = $state::Curious; Phase = 0.25; GrabOffset = $null; ScaleX = 1.0; ScaleY = 1.0; Rotation = 7.0; TranslateY = 0.0 },
    @{ State = $state::Startled; Phase = 0.25; GrabOffset = $null; ScaleX = 1.12727922061358; ScaleY = 0.901005050633883; Rotation = 0.0; TranslateY = 0.0 },
    @{ State = $state::ClickReaction; Phase = 0.5; GrabOffset = $null; ScaleX = 1.0; ScaleY = 1.0; Rotation = 0.0; TranslateY = -10.0 },
    @{ State = $state::Dragged; Phase = 0.25; GrabOffset = [Dororong.Core.Geometry.PointD]::new(76, 48); ScaleX = 1.0; ScaleY = 1.12; Rotation = 4.0; TranslateY = 0.0 }
)

foreach ($wakeCase in $wakeCases)
{
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

Write-Output 'SLEEP POSE PASS: bottom-anchored continuous image breathing, fixed body baseline, closed-frame selection, and wake-state pose reset passed.'
