Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected,[object]$Actual,[string]$Message)
{
    if ($Expected -ne $Actual) { throw "$Message Expected '$Expected', observed '$Actual'." }
}

function Assert-True([bool]$Condition,[string]$Message)
{
    if (-not $Condition) { throw $Message }
}

function Test-Purple([Drawing.Color]$Pixel)
{
    return $Pixel.A -gt 0 -and ($Pixel.B - $Pixel.R) -ge 10
}

function Get-PurpleCoordinates([Drawing.Bitmap]$Bitmap,[object]$Region)
{
    $coordinates = [Collections.Generic.List[object]]::new()
    foreach ($y in $Region.Y0..$Region.Y1)
    {
        foreach ($x in $Region.X0..$Region.X1)
        {
            if (Test-Purple $Bitmap.GetPixel($x,$y))
            { $coordinates.Add([pscustomobject]@{X=$x;Y=$y}) }
        }
    }
    return $coordinates
}

function Assert-PurpleBandIsStationary(
    [Drawing.Bitmap]$Open,[Drawing.Bitmap]$State,[object]$Region,[string]$Label)
{
    $statePurple = @(Get-PurpleCoordinates $State $Region)
    Assert-True ($statePurple.Count -gt 0) "$Label retained no visible iris band."
    foreach ($coordinate in $statePurple)
    {
        Assert-True (Test-Purple $Open.GetPixel($coordinate.X,$coordinate.Y)) `
            "$Label introduced purple iris content at ($($coordinate.X),$($coordinate.Y))."
    }
    $minimumY = (@($statePurple | ForEach-Object Y) | Measure-Object -Minimum).Minimum
    $maximumY = (@($statePurple | ForEach-Object Y) | Measure-Object -Maximum).Maximum
    $openXs = [Collections.Generic.List[int]]::new()
    foreach ($y in $minimumY..$maximumY)
    {
        foreach ($x in $Region.X0..$Region.X1)
        {
            if (Test-Purple $Open.GetPixel($x,$y)) { $openXs.Add($x) }
        }
    }
    Assert-True ($openXs.Count -gt 0) "$Label has no canonical reference band."
    $stateCenter = (@($statePurple | ForEach-Object X) | Measure-Object -Average).Average
    $openCenter = ($openXs | Measure-Object -Average).Average
    Assert-True ([Math]::Abs($stateCenter-$openCenter) -le 0.50) `
        "$Label visible iris centroid moved horizontally (open=$openCenter state=$stateCenter)."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $repositoryRoot 'src/Dororong.App/Assets'
$paths = [ordered]@{
    Open = Join-Path $assetRoot 'dororong-canonical.png'
    Open70 = Join-Path $assetRoot 'dororong-eyes-70-open.png'
    Open25 = Join-Path $assetRoot 'dororong-eyes-25-open.png'
    Closed = Join-Path $assetRoot 'dororong-closed-eyes.png'
    ObsoleteHalf = Join-Path $assetRoot 'dororong-half-closed-eyes.png'
}

foreach ($name in @('Open','Open70','Open25','Closed'))
{
    Assert-True (Test-Path -LiteralPath $paths[$name] -PathType Leaf) `
        "The authored $name face-state asset is missing."
}
Assert-True (-not (Test-Path -LiteralPath $paths.ObsoleteHalf)) `
    'The obsolete half-closed asset still exists.'

$expectedOpenHash = '238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511'
$expectedSelectedClosedHash = '4E12486A490EB134D8405259CB4DF6A847733559133CA9801DC506E292AEFD8E'
Assert-Equal $expectedOpenHash (Get-FileHash -Algorithm SHA256 -LiteralPath $paths.Open).Hash `
    'The canonical open sprite changed.'
Assert-Equal $expectedSelectedClosedHash (Get-FileHash -Algorithm SHA256 -LiteralPath $paths.Closed).Hash `
    'The runtime closed face is not exact Task 14 candidate B.'

Add-Type -AssemblyName System.Drawing
$open=$null;$open70=$null;$open25=$null;$closed=$null
try
{
    $open = [Drawing.Bitmap]::new($paths.Open)
    $open70 = [Drawing.Bitmap]::new($paths.Open70)
    $open25 = [Drawing.Bitmap]::new($paths.Open25)
    $closed = [Drawing.Bitmap]::new($paths.Closed)
    $states = [ordered]@{ Open=$open; Open70=$open70; Open25=$open25; Closed=$closed }
    foreach ($entry in $states.GetEnumerator())
    {
        Assert-Equal 96 $entry.Value.Width "$($entry.Key) width changed."
        Assert-Equal 96 $entry.Value.Height "$($entry.Key) height changed."
    }

    $regions = @(
        [pscustomobject]@{Name='viewer-left';X0=16;X1=30;Y0=46;Y1=61;Expected70=17;Expected25=3},
        [pscustomobject]@{Name='viewer-right';X0=31;X1=48;Y0=46;Y1=63;Expected70=29;Expected25=6})
    $regionKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($region in $regions)
    {
        foreach ($y in $region.Y0..$region.Y1)
        { foreach ($x in $region.X0..$region.X1) { $null=$regionKeys.Add("$x,$y") } }
    }

    foreach ($entry in @(
        [pscustomobject]@{Name='70-percent';Bitmap=$open70},
        [pscustomobject]@{Name='25-percent';Bitmap=$open25},
        [pscustomobject]@{Name='closed';Bitmap=$closed}))
    {
        $outsideChanges=0;$alphaChanges=0
        foreach ($y in 0..95)
        {
            foreach ($x in 0..95)
            {
                $before=$open.GetPixel($x,$y);$after=$entry.Bitmap.GetPixel($x,$y)
                if ($before.A -ne $after.A) { $alphaChanges++ }
                if ($before.ToArgb() -ne $after.ToArgb() -and -not $regionKeys.Contains("$x,$y"))
                { $outsideChanges++ }
            }
        }
        Assert-Equal 0 $alphaChanges "$($entry.Name) changed alpha."
        Assert-Equal 0 $outsideChanges "$($entry.Name) changed protected pixels outside the two eye regions."
    }

    $hashes = @($paths.Open,$paths.Open70,$paths.Open25,$paths.Closed |
        ForEach-Object { (Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash })
    Assert-Equal 4 @($hashes | Select-Object -Unique).Count `
        'Open, 70%, 25%, and closed are not four distinct authored rasters.'

    foreach ($region in $regions)
    {
        $openCount = @(Get-PurpleCoordinates $open $region).Count
        $count70 = @(Get-PurpleCoordinates $open70 $region).Count
        $count25 = @(Get-PurpleCoordinates $open25 $region).Count
        $closedCount = @(Get-PurpleCoordinates $closed $region).Count
        Assert-Equal $region.Expected70 $count70 "$($region.Name) 70-percent iris mask changed."
        Assert-Equal $region.Expected25 $count25 "$($region.Name) 25-percent iris mask changed."
        Assert-Equal 0 $closedCount "$($region.Name) closed face retained purple eye pixels."
        Assert-True ($openCount -gt $count70 -and $count70 -gt $count25 -and $count25 -gt 0) `
            "$($region.Name) closure is not progressive (open=$openCount 70=$count70 25=$count25 closed=$closedCount)."
        Assert-PurpleBandIsStationary $open $open70 $region "$($region.Name) 70-percent"
        Assert-PurpleBandIsStationary $open $open25 $region "$($region.Name) 25-percent"
        Write-Output "BLINK NATIVE METRICS name=$($region.Name) purple=$openCount->$count70->$count25->$closedCount"
    }
    $gap70=[Math]::Abs((17.0/24.0)-(29.0/40.0))
    $gap25=[Math]::Abs((3.0/24.0)-(6.0/40.0))
    Assert-True ($gap70 -le 0.05 -and $gap25 -le 0.05) `
        "Paired normalized openness is unbalanced (70=$gap70 25=$gap25)."
}
finally
{
    foreach ($bitmap in @($closed,$open25,$open70,$open))
    { if ($null -ne $bitmap) { $bitmap.Dispose() } }
}

$runRoot = Join-Path $repositoryRoot ".superpowers/sdd/2026-08-29-dororong-stage-a-eye-geometry-blink-recovery/task-15-test-runs/$([Guid]::NewGuid().ToString('N'))"
$outputDirectory = Join-Path $runRoot 'output'
$evidenceDirectory = Join-Path $runRoot 'evidence'
$generatorOutput = & pwsh -NoProfile -File (Join-Path $repositoryRoot 'tools/Generate-CanonicalArt.ps1') `
    -SourcePath (Join-Path $assetRoot 'dororong-canonical-source.png') `
    -BodyMaskPath (Join-Path $assetRoot 'dororong-body-region-mask.png') `
    -OutputDirectory $outputDirectory -EvidenceDirectory $evidenceDirectory 2>&1
Assert-Equal 0 $LASTEXITCODE "The canonical/authored-state export failed: $($generatorOutput -join [Environment]::NewLine)"

$expectedOutputNames = @(
    'dororong-canonical.png','dororong-closed-eyes.png',
    'dororong-eyes-25-open.png','dororong-eyes-70-open.png') | Sort-Object
$observedOutputNames = @(Get-ChildItem -LiteralPath $outputDirectory -File | Sort-Object Name | ForEach-Object Name)
Assert-Equal ($expectedOutputNames -join '|') ($observedOutputNames -join '|') `
    'The export output is not exactly canonical open plus three authored native face states.'

$expectedEvidenceNames = @(
    'native-closed-candidate.png','native-closed-nearest-8x.png',
    'native-eyes-25-open-candidate.png','native-eyes-25-open-nearest-8x.png',
    'native-eyes-70-open-candidate.png','native-eyes-70-open-nearest-8x.png',
    'native-open-baseline.png','native-open-candidate.png','native-open-nearest-8x.png',
    'source-open-baseline.png','source-open-candidate.png') | Sort-Object
$observedEvidenceNames = @(Get-ChildItem -LiteralPath $evidenceDirectory -File | Sort-Object Name | ForEach-Object Name)
Assert-Equal ($expectedEvidenceNames -join '|') ($observedEvidenceNames -join '|') `
    'The export evidence still contains procedural expression-source remnants or misses authored native states.'

foreach ($mapping in @(
    @{Product=$paths.Open;Export=Join-Path $outputDirectory 'dororong-canonical.png'},
    @{Product=$paths.Open70;Export=Join-Path $outputDirectory 'dororong-eyes-70-open.png'},
    @{Product=$paths.Open25;Export=Join-Path $outputDirectory 'dororong-eyes-25-open.png'},
    @{Product=$paths.Closed;Export=Join-Path $outputDirectory 'dororong-closed-eyes.png'}))
{
    Assert-Equal (Get-FileHash -Algorithm SHA256 -LiteralPath $mapping.Product).Hash `
        (Get-FileHash -Algorithm SHA256 -LiteralPath $mapping.Export).Hash `
        "Exported asset differs from '$($mapping.Product)'."
}

Write-Output 'BLINK STATE ART PASS: exact selected closed face, authored progressive native transitions, protected pixels, stationary iris bands, and canonical-only plus authored-state export passed.'
