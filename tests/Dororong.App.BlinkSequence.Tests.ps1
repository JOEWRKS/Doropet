param(
    [string]$Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -ne $Actual)
    {
        throw "$Message Expected '$Expected', observed '$Actual'."
    }
}

function Assert-Frame(
    [System.Windows.Controls.Image]$Image,
    [string]$ExpectedFileName,
    [string]$Label)
{
    $actual = $Image.Source.ToString()
    if (-not $actual.EndsWith($ExpectedFileName, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "$Label did not use $ExpectedFileName. Observed '$actual'."
    }
}

$currentThread = [Threading.Thread]::CurrentThread
Assert-Equal 'STA' $currentThread.GetApartmentState().ToString() `
    'The focused WPF blink-sequence test must run in an STA apartment.'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$coreAssemblyPath = Join-Path $repositoryRoot "src/Dororong.Core/bin/$Configuration/net8.0/Dororong.Core.dll"
$appAssemblyPath = Join-Path $repositoryRoot "src/Dororong.App/bin/$Configuration/net8.0-windows/Dororong.App.dll"

Add-Type -AssemblyName PresentationFramework
Add-Type -Path $coreAssemblyPath
Add-Type -Path $appAssemblyPath

$presenter = [Dororong.App.Controls.DororongPresenter]::new()
$image = [System.Windows.Controls.Image]$presenter.FindName('DororongImage')
$state = [Dororong.Core.Behavior.PetState]
$facing = [Dororong.Core.Behavior.FacingDirection]::Right
$origin = [Dororong.Core.Geometry.PointD]::new(0, 0)

function Render-State(
    [Dororong.Core.Behavior.PetState]$State,
    [double]$Phase)
{
    $presenter.Render([Dororong.Core.Behavior.PetSnapshot]::new(
        $State,$origin,$facing,$Phase,$false,$null))
}

# The 0.65..0.74 phase window is 0.18 seconds in the existing two-second IDLE phase.
# Mutating either edge or omitting either half frame must fail one of these literal probes.
$idleCases = @(
    @{ Phase = 0.649999; Frame = 'dororong-canonical.png'; Label = 'before blink' },
    @{ Phase = 0.650000; Frame = 'dororong-half-closed-eyes.png'; Label = 'first half-frame entry' },
    @{ Phase = 0.674999; Frame = 'dororong-half-closed-eyes.png'; Label = 'first half-frame exit probe' },
    @{ Phase = 0.675000; Frame = 'dororong-closed-eyes.png'; Label = 'full-close entry' },
    @{ Phase = 0.695000; Frame = 'dororong-closed-eyes.png'; Label = 'full-close middle' },
    @{ Phase = 0.715000; Frame = 'dororong-closed-eyes.png'; Label = 'full-close exit probe' },
    @{ Phase = 0.715001; Frame = 'dororong-half-closed-eyes.png'; Label = 'second half-frame entry' },
    @{ Phase = 0.740000; Frame = 'dororong-half-closed-eyes.png'; Label = 'second half-frame exit probe' },
    @{ Phase = 0.740001; Frame = 'dororong-canonical.png'; Label = 'after blink' })

foreach ($case in $idleCases)
{
    Render-State $state::Idle $case.Phase
    Assert-Frame $image $case.Frame "IDLE $($case.Label) phase=$($case.Phase)"
}

foreach ($phase in @(0.25, 0.75))
{
    Render-State $state::Sleep $phase
    Assert-Frame $image 'dororong-closed-eyes.png' "SLEEP phase=$phase"
}

foreach ($otherState in @(
    $state::Walk,$state::Curious,$state::Startled,$state::ClickReaction,$state::Dragged))
{
    Render-State $otherState 0.695
    Assert-Frame $image 'dororong-canonical.png' "$otherState reset at blink phase"
}

Write-Output 'BLINK SEQUENCE PASS: IDLE maps open/half/closed/half/open across 0.65..0.74; SLEEP stays closed and every other state resets to canonical.'
