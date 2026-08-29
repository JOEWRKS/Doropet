param([string]$Configuration = 'Debug')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-Purple([Drawing.Color]$Pixel)
{
    return $Pixel.A -gt 0 -and ($Pixel.B - $Pixel.R) -ge 10
}

function Get-PurpleCount([Drawing.Bitmap]$Bitmap,[object]$Region)
{
    $count = 0
    foreach ($y in $Region.Y0..$Region.Y1)
    {
        foreach ($x in $Region.X0..$Region.X1)
        {
            if (Test-Purple $Bitmap.GetPixel($x,$y)) { $count++ }
        }
    }
    return $count
}

function Get-LidCenterY([Drawing.Bitmap]$Bitmap,[object[]]$Coordinates)
{
    $weightedY = 0.0
    $weight = 0.0
    foreach ($coordinate in $Coordinates)
    {
        $pixel = $Bitmap.GetPixel([int]$coordinate[0],[int]$coordinate[1])
        if ($pixel.A -eq 0) { continue }
        $darkness = 255.0 - (($pixel.R + $pixel.G + $pixel.B) / 3.0)
        if ($darkness -le 0) { continue }
        $weightedY += [double]$coordinate[1] * $darkness
        $weight += $darkness
    }
    if ($weight -eq 0) { throw 'The expected lid coordinates contain no visible ink.' }
    return $weightedY / $weight
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $repositoryRoot 'src/Dororong.App/Assets'
$old70Path = Join-Path $assetRoot 'dororong-eyes-70-open.png'
$old25Path = Join-Path $assetRoot 'dororong-eyes-25-open.png'
$squintPath = Join-Path $assetRoot 'dororong-blink-squint.png'
$closedPath = Join-Path $assetRoot 'dororong-closed-eyes.png'
$failures = [Collections.Generic.List[string]]::new()

Add-Type -AssemblyName System.Drawing
$regions = @(
    [pscustomobject]@{Name='viewer-left';X0=16;X1=30;Y0=46;Y1=61},
    [pscustomobject]@{Name='viewer-right';X0=31;X1=48;Y0=46;Y1=63})

foreach ($entry in @(
    @{Label='rejected 70-percent';Path=$old70Path},
    @{Label='rejected 25-percent';Path=$old25Path}))
{
    if (-not (Test-Path -LiteralPath $entry.Path -PathType Leaf)) { continue }
    $bitmap = [Drawing.Bitmap]::new($entry.Path)
    try
    {
        $counts = @($regions | ForEach-Object { Get-PurpleCount $bitmap $_ })
        if (($counts | Measure-Object -Sum).Sum -gt 0)
        {
            $failures.Add("$($entry.Label) runtime resource retains purple eye content (left=$($counts[0]) right=$($counts[1])).")
        }
    }
    finally { $bitmap.Dispose() }
}

if (-not (Test-Path -LiteralPath $squintPath -PathType Leaf))
{
    $failures.Add('The single lid-only squint runtime resource is missing.')
}

$closed = [Drawing.Bitmap]::new($closedPath)
try
{
    if ((Test-Path -LiteralPath $old70Path) -or (Test-Path -LiteralPath $old25Path))
    {
        # Complete rejected Task 15 lid memberships. This branch makes the RED
        # run measure the actual failed asset rather than an expected constant.
        $leftClosed = @(@(19,51),@(20,52),@(21,53),@(22,53),@(23,53),@(24,52),@(25,51))
        $rightClosed = @(@(38,55),@(39,56),@(40,57),@(41,58),@(42,58),@(43,57),@(44,56))
    }
    else
    {
        $leftClosed = @(@(20,53),@(21,54),@(22,54),@(23,54),@(24,54),@(25,53))
        $rightClosed = @(@(37,55),@(38,56),@(39,56),@(40,56),@(41,55))
    }
    $leftCenter = Get-LidCenterY $closed $leftClosed
    $rightCenter = Get-LidCenterY $closed $rightClosed
    $difference = [Math]::Abs($leftCenter-$rightCenter)
    if ($difference -gt 2.0)
    {
        $failures.Add("The corrected closed lid centers remain more than two native rows apart (left=$leftCenter right=$rightCenter difference=$difference).")
    }
}
finally { $closed.Dispose() }

if ((Test-Path -LiteralPath $squintPath) -and (Get-FileHash -LiteralPath $squintPath -Algorithm SHA256).Hash -ne
    'CE77E5AEA5AEB4EAEFEBE28ABE1FEBEC51729546FD71C8B9FA713F809B05FFA5')
{ $failures.Add('The runtime squint is not the exact approved iris-free Task 18 input.') }
if ((Get-FileHash -LiteralPath $closedPath -Algorithm SHA256).Hash -ne
    'DE4D8DAD77521C1F720D0A25984897AA1B4FFF5CB6F5E427627DBCB2263E68DD')
{ $failures.Add('The runtime closed face is not the exact approved Task 18 input.') }

$coreAssemblyPath = Join-Path $repositoryRoot "src/Dororong.Core/bin/$Configuration/net8.0/Dororong.Core.dll"
$appAssemblyPath = Join-Path $repositoryRoot "src/Dororong.App/bin/$Configuration/net8.0-windows/Dororong.App.dll"
Add-Type -AssemblyName PresentationFramework
Add-Type -Path $coreAssemblyPath
Add-Type -Path $appAssemblyPath
$presenter = [Dororong.App.Controls.DororongPresenter]::new()
$image = [Windows.Controls.Image]$presenter.FindName('DororongImage')
$state = [Dororong.Core.Behavior.PetState]
$facing = [Dororong.Core.Behavior.FacingDirection]::Right
$origin = [Dororong.Core.Geometry.PointD]::new(0,0)
$observed = [Collections.Generic.List[string]]::new()
foreach ($phase in @(0.649999,0.650000,0.666500,0.690000,0.706500,0.730000,0.746500,0.770000))
{
    $presenter.Render([Dororong.Core.Behavior.PetSnapshot]::new(
        $state::Idle,$origin,$facing,$phase,$false,$null))
    $observed.Add([IO.Path]::GetFileName($image.Source.ToString()))
}
$oldRuntimeResources = @($observed | Where-Object {
    $_ -in @('dororong-eyes-70-open.png','dororong-eyes-25-open.png') } | Select-Object -Unique)
if ($oldRuntimeResources.Count -ne 0)
{
    $failures.Add("IDLE playback still requires two iris-bearing intermediate resources: $($oldRuntimeResources -join ', ').")
}
$expected = @(
    'dororong-canonical.png','dororong-blink-squint.png','dororong-blink-squint.png',
    'dororong-closed-eyes.png','dororong-closed-eyes.png',
    'dororong-blink-squint.png','dororong-blink-squint.png','dororong-canonical.png')
if (($expected -join '|') -ne (@($observed) -join '|'))
{
    $failures.Add("The 33ms probe sequence is not open/squint/squint/closed/closed/squint/squint/open; observed $(@($observed) -join ', ').")
}

if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
Write-Output 'BLINK RECOVERY PASS: rejected iris resources are absent, closed lid centers are within two rows, and IDLE playback does not require iris-bearing intermediates.'
