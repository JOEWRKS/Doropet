Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected,[object]$Actual,[string]$Message)
{ if ($Expected -ne $Actual) { throw "$Message Expected '$Expected', observed '$Actual'." } }

function Assert-True([bool]$Condition,[string]$Message)
{ if (-not $Condition) { throw $Message } }

function Test-Purple([Drawing.Color]$Pixel)
{ return $Pixel.A -gt 0 -and ($Pixel.B - $Pixel.R) -ge 10 }

function Test-Dark([Drawing.Color]$Pixel)
{ return $Pixel.A -gt 0 -and (($Pixel.R + $Pixel.G + $Pixel.B) / 3.0) -lt 200 }

function Get-ChangedDarkCoordinates([Drawing.Bitmap]$Open,[Drawing.Bitmap]$State,[object]$Region)
{
    $coordinates = [Collections.Generic.List[string]]::new()
    foreach ($y in $Region.Y0..$Region.Y1)
    {
        foreach ($x in $Region.X0..$Region.X1)
        {
            $before = $Open.GetPixel($x,$y);$after = $State.GetPixel($x,$y)
            if ($before.ToArgb() -ne $after.ToArgb() -and (Test-Dark $after) -and -not (Test-Purple $after))
            { $coordinates.Add("$x,$y") }
        }
    }
    return @($coordinates | Sort-Object)
}

function Get-Center([string[]]$Coordinates)
{
    $xs = @($Coordinates | ForEach-Object { [int]$_.Split(',')[0] })
    $ys = @($Coordinates | ForEach-Object { [int]$_.Split(',')[1] })
    return [pscustomobject]@{
        X = ($xs | Measure-Object -Average).Average
        Y = ($ys | Measure-Object -Average).Average
        MinY = ($ys | Measure-Object -Minimum).Minimum
        MaxY = ($ys | Measure-Object -Maximum).Maximum
    }
}

function Assert-NoHairContact([Drawing.Bitmap]$Open,[Drawing.Bitmap]$State,[string[]]$Lid,[string]$Label)
{
    $lidSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($key in $Lid) { $null = $lidSet.Add($key) }
    foreach ($key in $Lid)
    {
        $parts = $key.Split(',');$x=[int]$parts[0];$y=[int]$parts[1]
        foreach ($dy in -1..1)
        {
            foreach ($dx in -1..1)
            {
                if ($dx -eq 0 -and $dy -eq 0) { continue }
                $neighborKey = "$($x+$dx),$($y+$dy)"
                if ($lidSet.Contains($neighborKey)) { continue }
                $before = $Open.GetPixel($x+$dx,$y+$dy);$after = $State.GetPixel($x+$dx,$y+$dy)
                Assert-True (-not ((Test-Dark $after) -and $after.ToArgb() -eq $before.ToArgb())) `
                    "$Label has 8-neighbor foreground-hair contact at $neighborKey."
            }
        }
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $repositoryRoot 'src/Dororong.App/Assets'
$paths = [ordered]@{
    Open = Join-Path $assetRoot 'dororong-canonical.png'
    Squint = Join-Path $assetRoot 'dororong-blink-squint.png'
    Closed = Join-Path $assetRoot 'dororong-closed-eyes.png'
    Old70 = Join-Path $assetRoot 'dororong-eyes-70-open.png'
    Old25 = Join-Path $assetRoot 'dororong-eyes-25-open.png'
    ObsoleteHalf = Join-Path $assetRoot 'dororong-half-closed-eyes.png'
}

foreach ($name in @('Open','Squint','Closed'))
{
    Assert-True (Test-Path -LiteralPath $paths[$name] -PathType Leaf) `
        "The authored $name face-state asset is missing."
}
foreach ($name in @('Old70','Old25','ObsoleteHalf'))
{
    Assert-True (-not (Test-Path -LiteralPath $paths[$name])) `
        "The obsolete iris-bearing or half-close asset '$($paths[$name])' still exists."
}

$expectedHashes = [ordered]@{
    Open = '238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511'
    Squint = 'CE77E5AEA5AEB4EAEFEBE28ABE1FEBEC51729546FD71C8B9FA713F809B05FFA5'
    Closed = 'DE4D8DAD77521C1F720D0A25984897AA1B4FFF5CB6F5E427627DBCB2263E68DD'
}
foreach ($name in @('Open','Squint','Closed'))
{
    Assert-Equal $expectedHashes[$name] (Get-FileHash -Algorithm SHA256 -LiteralPath $paths[$name]).Hash `
        "$name is not the exact approved Task 18 raster."
}

Add-Type -AssemblyName System.Drawing
$open=$null;$squint=$null;$closed=$null
try
{
    $open = [Drawing.Bitmap]::new($paths.Open);$squint = [Drawing.Bitmap]::new($paths.Squint);$closed = [Drawing.Bitmap]::new($paths.Closed)
    foreach ($entry in ([ordered]@{Open=$open;Squint=$squint;Closed=$closed}).GetEnumerator())
    {
        Assert-Equal 96 $entry.Value.Width "$($entry.Key) width changed."
        Assert-Equal 96 $entry.Value.Height "$($entry.Key) height changed."
    }

    $regions = @(
        [pscustomobject]@{Name='viewer-left';X0=16;X1=30;Y0=46;Y1=61},
        [pscustomobject]@{Name='viewer-right';X0=31;X1=48;Y0=46;Y1=63})
    $regionKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($region in $regions)
    {
        foreach ($y in $region.Y0..$region.Y1)
        { foreach ($x in $region.X0..$region.X1) { $null=$regionKeys.Add("$x,$y") } }
    }

    foreach ($entry in @(@{Name='squint';Bitmap=$squint},@{Name='closed';Bitmap=$closed}))
    {
        $outsideChanges=0;$alphaChanges=0
        foreach ($y in 0..95)
        {
            foreach ($x in 0..95)
            {
                $before=$open.GetPixel($x,$y);$after=$entry.Bitmap.GetPixel($x,$y)
                if ($before.A -ne $after.A) { $alphaChanges++ }
                if ($before.ToArgb() -ne $after.ToArgb() -and -not $regionKeys.Contains("$x,$y")) { $outsideChanges++ }
            }
        }
        Assert-Equal 0 $alphaChanges "$($entry.Name) changed alpha."
        Assert-Equal 0 $outsideChanges "$($entry.Name) changed protected pixels outside the eye regions."
    }

    $expectedLids = [ordered]@{
        SquintLeft = @('20,52','21,53','22,53','23,53','24,53','25,52') | Sort-Object
        SquintRight = @('37,54','38,55','39,55','40,55','41,54') | Sort-Object
        ClosedLeft = @('20,53','21,54','22,54','23,54','24,54','25,53') | Sort-Object
        ClosedRight = @('37,55','38,56','39,56','40,56','41,55') | Sort-Object
    }
    $observedLids = [ordered]@{
        SquintLeft = Get-ChangedDarkCoordinates $open $squint ([pscustomobject]@{X0=20;X1=25;Y0=52;Y1=53})
        SquintRight = Get-ChangedDarkCoordinates $open $squint ([pscustomobject]@{X0=37;X1=41;Y0=54;Y1=55})
        ClosedLeft = Get-ChangedDarkCoordinates $open $closed ([pscustomobject]@{X0=20;X1=25;Y0=53;Y1=54})
        ClosedRight = Get-ChangedDarkCoordinates $open $closed ([pscustomobject]@{X0=37;X1=41;Y0=55;Y1=56})
    }
    foreach ($name in $expectedLids.Keys)
    {
        Assert-Equal ($expectedLids[$name] -join '|') ($observedLids[$name] -join '|') `
            "$name does not contain exactly one approved shallow lid and no second dark mark."
    }

    foreach ($entry in @(
        @{Name='squint';Bitmap=$squint;Left=$observedLids.SquintLeft;Right=$observedLids.SquintRight},
        @{Name='closed';Bitmap=$closed;Left=$observedLids.ClosedLeft;Right=$observedLids.ClosedRight}))
    {
        foreach ($region in $regions)
        {
            $purple=0;$eyeWhite=0
            foreach ($y in $region.Y0..$region.Y1)
            {
                foreach ($x in $region.X0..$region.X1)
                {
                    $pixel=$entry.Bitmap.GetPixel($x,$y)
                    if (Test-Purple $pixel) { $purple++ }
                    if ($pixel.ToArgb() -eq [Drawing.Color]::FromArgb(255,247,244,242).ToArgb()) { $eyeWhite++ }
                }
            }
            Assert-Equal 0 $purple "$($entry.Name) $($region.Name) retained purple iris residue."
            Assert-Equal 0 $eyeWhite "$($entry.Name) $($region.Name) retained an eye-white island."
        }
        Assert-NoHairContact $open $entry.Bitmap $entry.Left "$($entry.Name) viewer-left lid"
        Assert-NoHairContact $open $entry.Bitmap $entry.Right "$($entry.Name) viewer-right lid"
    }

    $squintLeft=Get-Center $observedLids.SquintLeft;$squintRight=Get-Center $observedLids.SquintRight
    $closedLeft=Get-Center $observedLids.ClosedLeft;$closedRight=Get-Center $observedLids.ClosedRight
    Assert-Equal $closedLeft.X $squintLeft.X 'Viewer-left squint moved horizontally from closed.'
    Assert-Equal $closedRight.X $squintRight.X 'Viewer-right squint moved horizontally from closed.'
    Assert-Equal 1.0 ($closedLeft.Y-$squintLeft.Y) 'Viewer-left squint is not exactly one row above closed.'
    Assert-Equal 1.0 ($closedRight.Y-$squintRight.Y) 'Viewer-right squint is not exactly one row above closed.'
    Assert-True ([Math]::Abs($closedLeft.Y-$closedRight.Y) -le 2.0) 'Final left/right lid centers differ by more than two native rows.'
    foreach ($lid in @($squintLeft,$squintRight,$closedLeft,$closedRight))
    { Assert-Equal 1 ($lid.MaxY-$lid.MinY) 'A lid is not a one-row-deep downward-center curve.' }
}
finally
{
    foreach ($bitmap in @($closed,$squint,$open)) { if ($null -ne $bitmap) { $bitmap.Dispose() } }
}

$runRoot = Join-Path $repositoryRoot ".superpowers/sdd/2026-08-29-dororong-stage-a-eye-geometry-blink-recovery/task-18-test-runs/$([Guid]::NewGuid().ToString('N'))"
$outputDirectory = Join-Path $runRoot 'output';$evidenceDirectory = Join-Path $runRoot 'evidence'
$generatorOutput = & pwsh -NoProfile -File (Join-Path $repositoryRoot 'tools/Generate-CanonicalArt.ps1') `
    -SourcePath (Join-Path $assetRoot 'dororong-canonical-source.png') `
    -BodyMaskPath (Join-Path $assetRoot 'dororong-body-region-mask.png') `
    -OutputDirectory $outputDirectory -EvidenceDirectory $evidenceDirectory 2>&1
Assert-Equal 0 $LASTEXITCODE "The canonical/authored-state export failed: $($generatorOutput -join [Environment]::NewLine)"
Assert-True (($generatorOutput -join [Environment]::NewLine).Contains('resizeOpen=1 authoredNativeCopies=2',[StringComparison]::Ordinal)) `
    'The generator did not report one canonical resize plus exactly two authored native copies.'

$expectedOutputNames = @('dororong-blink-squint.png','dororong-canonical.png','dororong-closed-eyes.png') | Sort-Object
$observedOutputNames = @(Get-ChildItem -LiteralPath $outputDirectory -File | Sort-Object Name | ForEach-Object Name)
Assert-Equal ($expectedOutputNames -join '|') ($observedOutputNames -join '|') 'The export output is not exactly canonical open plus squint and closed.'

$expectedEvidenceNames = @(
    'native-closed-candidate.png','native-closed-nearest-8x.png',
    'native-open-baseline.png','native-open-candidate.png','native-open-nearest-8x.png',
    'native-squint-candidate.png','native-squint-nearest-8x.png',
    'source-open-baseline.png','source-open-candidate.png') | Sort-Object
$observedEvidenceNames = @(Get-ChildItem -LiteralPath $evidenceDirectory -File | Sort-Object Name | ForEach-Object Name)
Assert-Equal ($expectedEvidenceNames -join '|') ($observedEvidenceNames -join '|') 'The export evidence contains removed iris resources or misses the lid-only states.'

foreach ($mapping in @(
    @{Product=$paths.Open;Export=Join-Path $outputDirectory 'dororong-canonical.png'},
    @{Product=$paths.Squint;Export=Join-Path $outputDirectory 'dororong-blink-squint.png'},
    @{Product=$paths.Closed;Export=Join-Path $outputDirectory 'dororong-closed-eyes.png'}))
{
    Assert-Equal (Get-FileHash -Algorithm SHA256 -LiteralPath $mapping.Product).Hash `
        (Get-FileHash -Algorithm SHA256 -LiteralPath $mapping.Export).Hash "Exported asset differs from '$($mapping.Product)'."
}

Write-Output 'BLINK STATE ART PASS: exact open/squint/closed rasters, lid geometry, iris/white removal, protected pixels, hair clearance, and exact two-state export passed.'
