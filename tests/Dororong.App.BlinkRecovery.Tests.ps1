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

function Test-LidInk([Drawing.Color]$Pixel)
{
    return $Pixel.ToArgb() -in @(
        [Drawing.Color]::FromArgb(255,50,42,48).ToArgb(),
        [Drawing.Color]::FromArgb(255,150,130,133).ToArgb())
}

function Get-ChangedLidGeometry([Drawing.Bitmap]$Open,[Drawing.Bitmap]$State,[object]$Region)
{
    $coordinates = [Collections.Generic.List[object[]]]::new()
    foreach ($y in $Region.Y0..$Region.Y1)
    {
        foreach ($x in $Region.X0..$Region.X1)
        {
            $before=$Open.GetPixel($x,$y);$after=$State.GetPixel($x,$y)
            if ($before.ToArgb() -ne $after.ToArgb() -and (Test-LidInk $after) -and -not (Test-Purple $after))
            { $coordinates.Add(@($x,$y)) }
        }
    }
    if ($coordinates.Count -eq 0) { throw 'The reviewed eye region contains no changed lid ink.' }
    $xs=@($coordinates|ForEach-Object{[int]$_[0]});$ys=@($coordinates|ForEach-Object{[int]$_[1]})
    $minX=($xs|Measure-Object -Minimum).Minimum;$maxX=($xs|Measure-Object -Maximum).Maximum
    $minY=($ys|Measure-Object -Minimum).Minimum;$maxY=($ys|Measure-Object -Maximum).Maximum
    return [pscustomobject]@{
        Width=1+$maxX-$minX
        Depth=$maxY-$minY
        CenterY=($minY+$maxY)/2.0
    }
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

$openPath = Join-Path $assetRoot 'dororong-canonical.png'
$open = [Drawing.Bitmap]::new($openPath)
$closed = [Drawing.Bitmap]::new($closedPath)
try
{
    $leftClosed = Get-ChangedLidGeometry $open $closed $regions[0]
    $rightClosed = Get-ChangedLidGeometry $open $closed $regions[1]
    if ($leftClosed.CenterY -ne $rightClosed.CenterY)
    {
        $failures.Add("The final left/right native vertical centers are unequal (left=$($leftClosed.CenterY) right=$($rightClosed.CenterY)).")
    }
    foreach ($entry in @(
        @{Name='viewer-left';Geometry=$leftClosed},
        @{Name='viewer-right';Geometry=$rightClosed}))
    {
        if ($entry.Geometry.Width -ne 7)
        { $failures.Add("The $($entry.Name) final lid visible width is $($entry.Geometry.Width), not 7.") }
        if ($entry.Geometry.Depth -ne 2)
        { $failures.Add("The $($entry.Name) final lid depth is $($entry.Geometry.Depth), not 2.") }
    }
}
finally { $closed.Dispose();$open.Dispose() }

if ((Test-Path -LiteralPath $squintPath) -and (Get-FileHash -LiteralPath $squintPath -Algorithm SHA256).Hash -ne
    '615C758D82F745F41D22547B1B42DB16AAF6ACA900229F55823DFD82A6584721')
{ $failures.Add('The runtime squint is not the exact approved Task 19 attempt-2 pair-B input.') }
if ((Get-FileHash -LiteralPath $closedPath -Algorithm SHA256).Hash -ne
    '319C3E931C8D9D2D32CB172B1AB7617EB7362700FC832182F9B187B0BAB9DFBB')
{ $failures.Add('The runtime closed face is not the exact approved Task 19 attempt-2 pair-B input.') }

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
Write-Output 'BLINK RECOVERY PASS: rejected iris resources are absent, pair-B closed lids have equal centers, width 7, depth 2, and IDLE playback does not require iris-bearing intermediates.'
