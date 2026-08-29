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

foreach ($namedPart in @(
    @{ Name = 'BodyScaleTransform'; Value = $scale },
    @{ Name = 'BodyRotateTransform'; Value = $rotation },
    @{ Name = 'BodyTranslateTransform'; Value = $translation },
    @{ Name = 'DororongImage'; Value = $image }))
{
    if ($null -eq $namedPart.Value)
    {
        throw "$($namedPart.Name) was not found in the presenter namescope."
    }
}

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

$presenter.Render((New-Snapshot $state::Sleep 0.25))
$highScaleX = [double]$scale.ScaleX
$highScaleY = [double]$scale.ScaleY
$highTranslateY = [double]$translation.Y
$highFrame = $image.Source.ToString()

$presenter.Render((New-Snapshot $state::Sleep 0.75))
$lowScaleX = [double]$scale.ScaleX
$lowScaleY = [double]$scale.ScaleY
$lowTranslateY = [double]$translation.Y
$lowFrame = $image.Source.ToString()

Assert-Near 1.0 $highScaleX 0.000001 'SLEEP expansion phase ScaleX changed.'
Assert-Near 1.0 $highScaleY 0.000001 'SLEEP expansion phase ScaleY changed.'
Assert-Near 5.0 $highTranslateY 0.000001 'SLEEP expansion phase vertical position changed.'
Assert-Near 1.0 $lowScaleX 0.000001 'SLEEP contraction phase ScaleX changed.'
Assert-Near 1.0 $lowScaleY 0.000001 'SLEEP contraction phase ScaleY changed.'
Assert-Near 7.0 $lowTranslateY 0.000001 'SLEEP contraction phase vertical position changed.'
Assert-Equal $true $highFrame.EndsWith('dororong-closed-eyes.png', [StringComparison]::OrdinalIgnoreCase) `
    'SLEEP expansion phase did not use dororong-closed-eyes.png.'
Assert-Equal $true $lowFrame.EndsWith('dororong-closed-eyes.png', [StringComparison]::OrdinalIgnoreCase) `
    'SLEEP contraction phase did not use dororong-closed-eyes.png.'

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
}

Write-Output 'SLEEP POSE PASS: whole-pixel translation-only breathing, corrected closed-frame selection, and wake-state pose reset passed.'
