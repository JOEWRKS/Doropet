param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$coreAssemblyPath = Join-Path $repositoryRoot "src/Dororong.Core/bin/$Configuration/net8.0/Dororong.Core.dll"
$appAssemblyPath = Join-Path $repositoryRoot "src/Dororong.App/bin/$Configuration/net8.0-windows/Dororong.App.dll"

Add-Type -AssemblyName PresentationFramework
Add-Type -Path $coreAssemblyPath
Add-Type -Path $appAssemblyPath

$presenter = [Dororong.App.Controls.DororongPresenter]::new()
$rotation = $presenter.FindName('BodyRotateTransform')
if ($null -eq $rotation)
{
    throw 'BodyRotateTransform was not found in the presenter namescope.'
}

$cases = @(
    @{ Name = 'center'; GrabX = 72.0; Expected = 0.0 },
    @{ Name = 'left symmetric point'; GrabX = 68.0; Expected = -4.0 },
    @{ Name = 'right symmetric point'; GrabX = 76.0; Expected = 4.0 },
    @{ Name = 'left clamp'; GrabX = 0.0; Expected = -8.0 },
    @{ Name = 'right clamp'; GrabX = 144.0; Expected = 8.0 }
)

foreach ($case in $cases)
{
    $grabOffset = [Dororong.Core.Geometry.PointD]::new($case.GrabX, 48)
    $snapshot = [Dororong.Core.Behavior.PetSnapshot]::new(
        [Dororong.Core.Behavior.PetState]::Dragged,
        [Dororong.Core.Geometry.PointD]::new(0, 0),
        [Dororong.Core.Behavior.FacingDirection]::Right,
        0,
        $true,
        $grabOffset)

    $presenter.Render($snapshot)

    $actualAngle = [double]$rotation.Angle
    if ([double]::IsNaN($actualAngle) -or [double]::IsInfinity($actualAngle))
    {
        throw "$($case.Name): angle was not finite ($actualAngle)."
    }

    if ([Math]::Abs($actualAngle - $case.Expected) -gt 0.000001)
    {
        throw "$($case.Name): angle was $actualAngle, expected $($case.Expected)."
    }
}

Write-Output 'DRAGGED ANGLE PASS: center=0; symmetric points=-4/+4; clamps=-8/+8.'
