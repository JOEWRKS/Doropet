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

function Test-LidInk([Drawing.Color]$Pixel)
{
    return $Pixel.ToArgb() -in @(
        [Drawing.Color]::FromArgb(255,50,42,48).ToArgb(),
        [Drawing.Color]::FromArgb(255,150,130,133).ToArgb())
}

function Get-ChangedLidInkCoordinates([Drawing.Bitmap]$Open,[Drawing.Bitmap]$State,[object]$Region)
{
    $coordinates = [Collections.Generic.List[string]]::new()
    foreach ($y in $Region.Y0..$Region.Y1)
    {
        foreach ($x in $Region.X0..$Region.X1)
        {
            $before = $Open.GetPixel($x,$y);$after = $State.GetPixel($x,$y)
            if ($before.ToArgb() -ne $after.ToArgb() -and (Test-LidInk $after) -and -not (Test-Purple $after))
            { $coordinates.Add("$x,$y") }
        }
    }
    return @($coordinates | Sort-Object)
}

function Get-RegionPixelHash([Drawing.Bitmap]$Bitmap,[object]$Region)
{
    $width = $Region.X1-$Region.X0+1;$height = $Region.Y1-$Region.Y0+1
    $bytes = [byte[]]::new($width*$height*4);$index=0
    foreach ($y in $Region.Y0..$Region.Y1)
    {
        foreach ($x in $Region.X0..$Region.X1)
        {
            $pixel=$Bitmap.GetPixel($x,$y)
            $bytes[$index++]=$pixel.A;$bytes[$index++]=$pixel.R
            $bytes[$index++]=$pixel.G;$bytes[$index++]=$pixel.B
        }
    }
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}

function Assert-ThrowsLike([scriptblock]$Action,[string]$ExpectedPattern,[string]$Label)
{
    try { & $Action }
    catch
    {
        $message=$_.Exception.Message
        if ($message -notmatch $ExpectedPattern)
        { throw "$Label reached the wrong assertion. Expected '$ExpectedPattern', observed '$message'." }
        return $message
    }
    throw "$Label did not reach an assertion."
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

function Assert-StateRegionContract(
    [Drawing.Bitmap]$Open,[Drawing.Bitmap]$State,[object[]]$Regions,
    [string[]]$ExpectedLeft,[string[]]$ExpectedRight,
    [string[]]$ProtectedHair,[string[]]$ExpectedRegionHashes,[string]$Label)
{
    $observedLeft = Get-ChangedLidInkCoordinates $Open $State $Regions[0]
    $observedRight = Get-ChangedLidInkCoordinates $Open $State $Regions[1]
    Assert-Equal ($ExpectedLeft -join '|') ($observedLeft -join '|') `
        "$Label viewer-left full repair region does not contain exactly one approved shallow lid and no second dark mark."
    Assert-Equal ($ExpectedRight -join '|') ($observedRight -join '|') `
        "$Label viewer-right full repair region does not contain exactly one approved shallow lid and no second dark mark."

    foreach ($key in $ProtectedHair)
    {
        $parts=$key.Split(',');$x=[int]$parts[0];$y=[int]$parts[1]
        Assert-Equal ($Open.GetPixel($x,$y).ToArgb()) ($State.GetPixel($x,$y).ToArgb()) `
            "$Label canonical in-region foreground hair changed at $key."
    }

    foreach ($index in 0..1)
    {
        $actualHash=Get-RegionPixelHash $State $Regions[$index]
        Assert-Equal $ExpectedRegionHashes[$index] $actualHash `
            "$Label $($Regions[$index].Name) full allowed pixel membership changed; this includes every eye-white palette member."
        $purple=0
        foreach ($y in $Regions[$index].Y0..$Regions[$index].Y1)
        {
            foreach ($x in $Regions[$index].X0..$Regions[$index].X1)
            { if (Test-Purple $State.GetPixel($x,$y)) { $purple++ } }
        }
        Assert-Equal 0 $purple "$Label $($Regions[$index].Name) retained purple iris residue."
    }

    Assert-NoHairContact $Open $State $observedLeft "$Label viewer-left lid"
    Assert-NoHairContact $Open $State $observedRight "$Label viewer-right lid"
    return [pscustomobject]@{Left=$observedLeft;Right=$observedRight}
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
    $protectedHair = @(
        '40,47','40,48','40,49','40,61','41,47','41,48','41,49','41,59','41,60','41,61','41,62',
        '42,47','42,48','42,49','42,50','42,57','42,58','42,59','42,60','42,61','42,62',
        '43,48','43,49','43,50','43,51','43,52','43,56','43,57','43,58','43,59','43,60','43,61','43,62',
        '44,48','44,49','44,50','44,51','44,52','44,53','44,54','44,55','44,56','44,57','44,58','44,59','44,60','44,61','44,62',
        '45,48','45,49','45,50','45,51','45,52','45,53','45,54','45,55','45,56','45,57','45,58','45,59','45,60',
        '46,48','46,49','46,50','46,51','46,52','46,53','46,54','46,55','46,56','46,57','47,50') | Sort-Object
    Assert-Equal 72 $protectedHair.Count 'The independently approved in-region foreground-hair membership changed.'
    $hairMembershipBytes=[Text.Encoding]::UTF8.GetBytes($protectedHair -join '|')
    Assert-Equal '9420F22F10FAF71D8D750B4923A59937553CEF52BD1676161DCE38521020B655' `
        ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($hairMembershipBytes))) `
        'The independently approved in-region foreground-hair coordinate set changed.'

    $expectedRegionHashes = [ordered]@{
        Squint = @(
            'CDCF8F8DDDE9B5C309F506ED9E46B8D36E56D05B873354E330197A3BA431E354',
            '2D3576DEE824DDDA6350F28FB92CDD2AAB381BC10F6393B70490E2C82003CA52')
        Closed = @(
            '3D9C14827EEC541DE0A7165A530547BE8A99398E40B4A72A70134A3177D48E6F',
            '5529E7014B35249E4D401C551946FF145EC6247187B0FBE34F5D7F5CC39F9189')
    }
    $squintContract = Assert-StateRegionContract $open $squint $regions `
        $expectedLids.SquintLeft $expectedLids.SquintRight $protectedHair $expectedRegionHashes.Squint 'squint'
    $closedContract = Assert-StateRegionContract $open $closed $regions `
        $expectedLids.ClosedLeft $expectedLids.ClosedRight $protectedHair $expectedRegionHashes.Closed 'closed'
    $observedLids = [ordered]@{
        SquintLeft = $squintContract.Left
        SquintRight = $squintContract.Right
        ClosedLeft = $closedContract.Left
        ClosedRight = $closedContract.Right
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

    # Mutation contracts exercise cloned production rasters. Each test names
    # the product break that the full repair-region contract must reject.
    $extraMark = $squint.Clone()
    try
    {
        $extraMark.SetPixel(30,58,[Drawing.Color]::FromArgb(255,50,42,48))
        $message = Assert-ThrowsLike {
            Assert-StateRegionContract $open $extraMark $regions `
                $expectedLids.SquintLeft $expectedLids.SquintRight $protectedHair $expectedRegionHashes.Squint 'extra-dark-mark mutation'
        } 'full repair region does not contain exactly one approved shallow lid and no second dark mark' `
            'Production mutation extra changed non-purple dark mark at (30,58)'
        Write-Output "MUTATION PASS: extra changed non-purple dark mark rejected: $message"
    }
    finally { $extraMark.Dispose() }

    $alteredHair = $squint.Clone()
    try
    {
        $alteredHair.SetPixel(47,50,[Drawing.Color]::FromArgb(255,251,171,199))
        $message = Assert-ThrowsLike {
            Assert-StateRegionContract $open $alteredHair $regions `
                $expectedLids.SquintLeft $expectedLids.SquintRight $protectedHair $expectedRegionHashes.Squint 'altered-hair mutation'
        } 'canonical in-region foreground hair changed at 47,50' `
            'Production mutation altered canonical in-region foreground hair at (47,50)'
        Write-Output "MUTATION PASS: altered canonical in-region foreground hair rejected: $message"
    }
    finally { $alteredHair.Dispose() }

    $alternateEyeWhite = $squint.Clone()
    try
    {
        $alternateEyeWhite.SetPixel(30,59,[Drawing.Color]::FromArgb(255,255,255,255))
        $message = Assert-ThrowsLike {
            Assert-StateRegionContract $open $alternateEyeWhite $regions `
                $expectedLids.SquintLeft $expectedLids.SquintRight $protectedHair $expectedRegionHashes.Squint 'alternate-eye-white mutation'
        } 'full allowed pixel membership changed; this includes every eye-white palette member' `
            'Production mutation reintroduced alternate eye-white palette member 255,255,255 at (30,59)'
        Write-Output "MUTATION PASS: alternate eye-white palette member rejected: $message"
    }
    finally { $alternateEyeWhite.Dispose() }
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
