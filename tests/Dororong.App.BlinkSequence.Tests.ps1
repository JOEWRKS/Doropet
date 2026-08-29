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
$scale = $presenter.FindName('BodyScaleTransform')
$translation = $presenter.FindName('BodyTranslateTransform')
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

# Each intermediate occupies 0.04 seconds in the existing two-second IDLE phase,
# so it is observable for at least one 33ms render tick. Full close occupies 0.08
# seconds, which covers two 33ms ticks.
$idleCases = @(
    @{ Phase = 0.649999; Frame = 'dororong-canonical.png'; Label = 'before blink' },
    @{ Phase = 0.650000; Frame = 'dororong-eyes-70-open.png'; Label = 'first 70-percent entry' },
    @{ Phase = 0.666500; Frame = 'dororong-eyes-70-open.png'; Label = 'first 70-percent 33ms probe' },
    @{ Phase = 0.670000; Frame = 'dororong-eyes-25-open.png'; Label = 'first 25-percent entry' },
    @{ Phase = 0.686500; Frame = 'dororong-eyes-25-open.png'; Label = 'first 25-percent 33ms probe' },
    @{ Phase = 0.690000; Frame = 'dororong-closed-eyes.png'; Label = 'full-close entry' },
    @{ Phase = 0.706500; Frame = 'dororong-closed-eyes.png'; Label = 'full-close first 33ms probe' },
    @{ Phase = 0.723000; Frame = 'dororong-closed-eyes.png'; Label = 'full-close second 33ms probe' },
    @{ Phase = 0.730000; Frame = 'dororong-eyes-25-open.png'; Label = 'second 25-percent entry' },
    @{ Phase = 0.746500; Frame = 'dororong-eyes-25-open.png'; Label = 'second 25-percent 33ms probe' },
    @{ Phase = 0.750000; Frame = 'dororong-eyes-70-open.png'; Label = 'second 70-percent entry' },
    @{ Phase = 0.766500; Frame = 'dororong-eyes-70-open.png'; Label = 'second 70-percent 33ms probe' },
    @{ Phase = 0.770000; Frame = 'dororong-canonical.png'; Label = 'after blink' })

$idleFailures = [Collections.Generic.List[string]]::new()
foreach ($case in $idleCases)
{
    Render-State $state::Idle $case.Phase
    $actualFrame = $image.Source.ToString()
    if (-not $actualFrame.EndsWith($case.Frame, [StringComparison]::OrdinalIgnoreCase))
    {
        $idleFailures.Add("IDLE $($case.Label) phase=$($case.Phase) did not use $($case.Frame); observed '$actualFrame'.")
    }
    if ([double]$scale.ScaleX -ne 1.0)
    { $idleFailures.Add("IDLE $($case.Label) scaled horizontally; observed '$([double]$scale.ScaleX)'.") }
    if ([double]$scale.ScaleY -ne 1.0)
    { $idleFailures.Add("IDLE $($case.Label) scaled vertically; observed '$([double]$scale.ScaleY)'.") }
    $translateY = [double]$translation.Y
    if ($translateY -lt -1.0 -or $translateY -gt 1.0 -or $translateY -ne [Math]::Round($translateY))
    {
        $idleFailures.Add("IDLE $($case.Label) breathing is not bounded to whole-pixel vertical translation; observed '$translateY'.")
    }
}
if ($idleFailures.Count -gt 0) { throw ($idleFailures -join [Environment]::NewLine) }

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

Write-Output 'BLINK SEQUENCE PASS: IDLE maps open/70%/25%/closed/25%/70%/open with one 33ms tick per intermediate and two closed ticks; IDLE uses whole-pixel translation without scaling, SLEEP stays closed, and other states reset to canonical.'
