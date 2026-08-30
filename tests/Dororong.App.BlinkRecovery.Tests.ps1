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

if ((Test-Path -LiteralPath $squintPath) -and (Get-FileHash -LiteralPath $squintPath -Algorithm SHA256).Hash -ne
    '2A733093AC35B9678CBB90833272498C7BE755F77A574271DAE64FCE80FC90E2')
{ $failures.Add('The runtime squint is not the supplied half-close frame.') }
if ((Get-FileHash -LiteralPath $closedPath -Algorithm SHA256).Hash -ne
    '0D20EED5873A7E4277D6ED539474D875C9B8DF79998B1EFD74F46AA87662F488')
{ $failures.Add('The runtime closed face is not the supplied full-close frame.') }

# Only the generated closed eyes belong to the expression state. The canonical
# mouth and lower face remain the positional authority, and the generated mouth
# above them must be removed instead of moving or replacing the canonical mouth.
$canonicalBitmap = [Drawing.Bitmap]::new((Join-Path $assetRoot 'dororong-canonical.png'))
$closedBitmap = [Drawing.Bitmap]::new($closedPath)
try
{
    $changedCanonicalLowerFacePixels = 0
    foreach ($y in 59..62)
    {
        foreach ($x in 22..34)
        {
            if ($canonicalBitmap.GetPixel($x,$y).ToArgb() -ne $closedBitmap.GetPixel($x,$y).ToArgb())
            {
                $changedCanonicalLowerFacePixels++
            }
        }
    }
    if ($changedCanonicalLowerFacePixels -ne 0)
    {
        $failures.Add("The closed expression moves the canonical mouth or imports a lower-face/chin line at $changedCanonicalLowerFacePixels pixels.")
    }

}
finally
{
    $canonicalBitmap.Dispose()
    $closedBitmap.Dispose()
}

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
Write-Output 'BLINK RECOVERY PASS: rejected iris resources are absent, the canonical mouth/lower face is exact, and IDLE playback does not require the rejected intermediate resources.'
