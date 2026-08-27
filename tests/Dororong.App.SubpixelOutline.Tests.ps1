param([switch]$GeometryOnly)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition) { throw $Message }
}

function Assert-Equal($Expected, $Actual, [string]$Message)
{
    if ($Expected -ne $Actual)
    { throw "$Message Expected '$Expected'; observed '$Actual'." }
}

function Assert-ThrowsLike([scriptblock]$Action, [string]$ExpectedPattern, [string]$Label)
{
    $caught = $null
    try { & $Action | Out-Null }
    catch { $caught = $_.Exception.Message }
    Assert-True ($null -ne $caught) "$Label did not fail."
    Assert-True ($caught -match $ExpectedPattern) `
        "$Label failed for the wrong reason. Expected '$ExpectedPattern'; observed '$caught'."
    return $caught
}

function Get-Sha256Text([string]$Text)
{
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text))) }
    finally { $sha.Dispose() }
}

function Get-BitmapPngBytes([Drawing.Bitmap]$Bitmap)
{
    $stream = [IO.MemoryStream]::new()
    try
    {
        $Bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally { $stream.Dispose() }
}

function Get-BytesSha256([byte[]]$Bytes)
{
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [Convert]::ToHexString($sha.ComputeHash($Bytes)) }
    finally { $sha.Dispose() }
}

function New-Segment([string]$Kind, [double]$X1, [double]$Y1, [double]$X2, [double]$Y2)
{
    return [pscustomobject]@{ Kind=$Kind; X1=$X1; Y1=$Y1; X2=$X2; Y2=$Y2 }
}

function Convert-CoordinateToDoubledInteger([double]$Value, [string]$Label)
{
    $doubled = $Value * 2.0
    $rounded = [Math]::Round($doubled, 0, [MidpointRounding]::ToEven)
    Assert-True ([Math]::Abs($doubled - $rounded) -le 0.000000000001) `
        "$Label is not an exact half-pixel coordinate: $Value."
    return [int]$rounded
}

function Get-IndependentCanonicalRecords([object[]]$Segments)
{
    $unique = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $records = [Collections.Generic.List[object]]::new()
    foreach ($segment in $Segments)
    {
        $kind = [string]$segment.Kind
        Assert-True ($kind -eq 'E' -or $kind -eq 'C') "Unknown contour kind '$kind'."
        $x1 = Convert-CoordinateToDoubledInteger ([double]$segment.X1) 'Segment X1'
        $y1 = Convert-CoordinateToDoubledInteger ([double]$segment.Y1) 'Segment Y1'
        $x2 = Convert-CoordinateToDoubledInteger ([double]$segment.X2) 'Segment X2'
        $y2 = Convert-CoordinateToDoubledInteger ([double]$segment.Y2) 'Segment Y2'
        if ($y2 -lt $y1 -or ($y2 -eq $y1 -and $x2 -lt $x1))
        {
            $temporaryX = $x1; $temporaryY = $y1
            $x1 = $x2; $y1 = $y2
            $x2 = $temporaryX; $y2 = $temporaryY
        }
        $identity = "$kind|$y1|$x1|$y2|$x2"
        if ($unique.Add($identity))
        {
            $records.Add([pscustomobject]@{
                Kind=$kind; StartY2=$y1; StartX2=$x1; EndY2=$y2; EndX2=$x2; Record=$identity
            })
        }
    }
    return @($records | Sort-Object Kind,StartY2,StartX2,EndY2,EndX2 | ForEach-Object Record)
}

function Assert-IndependentBitmapPair([Drawing.Bitmap]$Source, [Drawing.Bitmap]$Mask)
{
    Assert-True ($null -ne $Source -and $null -ne $Mask) 'Contour inputs are missing.'
    Assert-Equal $Source.Width $Mask.Width 'Contour bitmap widths differ.'
    Assert-Equal $Source.Height $Mask.Height 'Contour bitmap heights differ.'
    for ($y=0; $y -lt $Mask.Height; $y++)
    {
        for ($x=0; $x -lt $Mask.Width; $x++)
        {
            $maskPixel = $Mask.GetPixel($x,$y)
            Assert-True ($maskPixel.A -eq 255 -and $maskPixel.R -eq $maskPixel.G -and
                $maskPixel.R -eq $maskPixel.B -and ($maskPixel.R -eq 0 -or $maskPixel.R -eq 255)) `
                "Independent mask validation failed at ($x,$y)."
            $sourcePixel = $Source.GetPixel($x,$y)
            Assert-True ($sourcePixel.A -eq 0 -or $sourcePixel.A -eq 255) `
                "Independent source alpha validation failed at ($x,$y)."
            if ($maskPixel.R -eq 255)
            { Assert-Equal 255 $sourcePixel.A "Independent writable-alpha validation failed at ($x,$y)." }
        }
    }
}

function Get-IndependentVisibleSegments(
    [Drawing.Bitmap]$Source,
    [Drawing.Bitmap]$Mask,
    [Drawing.PointF[]]$Endpoints)
{
    Assert-IndependentBitmapPair $Source $Mask
    $segments = [Collections.Generic.List[object]]::new()
    $vertices = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    $directions = @(
        @{ Dx=-1; Dy=0; X1=-1; Y1=-1; X2=-1; Y2=1 },
        @{ Dx=1; Dy=0; X1=1; Y1=-1; X2=1; Y2=1 },
        @{ Dx=0; Dy=-1; X1=-1; Y1=-1; X2=1; Y2=-1 },
        @{ Dx=0; Dy=1; X1=-1; Y1=1; X2=1; Y2=1 }
    )
    for ($y=0; $y -lt $Mask.Height; $y++)
    {
        for ($x=0; $x -lt $Mask.Width; $x++)
        {
            if ($Mask.GetPixel($x,$y).R -ne 255) { continue }
            foreach ($direction in $directions)
            {
                $neighborX = $x + [int]$direction.Dx
                $neighborY = $y + [int]$direction.Dy
                if ($neighborX -lt 0 -or $neighborY -lt 0 -or
                    $neighborX -ge $Source.Width -or $neighborY -ge $Source.Height)
                { continue }
                $transparent = $Source.GetPixel($neighborX,$neighborY).A -eq 0
                if (-not $transparent) { continue }
                $x12=(2*$x)+[int]$direction.X1; $y12=(2*$y)+[int]$direction.Y1
                $x22=(2*$x)+[int]$direction.X2; $y22=(2*$y)+[int]$direction.Y2
                $segments.Add((New-Segment 'E' ($x12/2.0) ($y12/2.0) ($x22/2.0) ($y22/2.0)))
                foreach ($vertex in @(@($x12,$y12),@($x22,$y22)))
                {
                    $key="$($vertex[0]),$($vertex[1])"
                    if (-not $vertices.ContainsKey($key))
                    { $vertices[$key]=[pscustomobject]@{ X2=[int]$vertex[0]; Y2=[int]$vertex[1] } }
                }
            }
        }
    }
    foreach ($endpoint in $Endpoints)
    {
        $endpointX=[int]$endpoint.X; $endpointY=[int]$endpoint.Y
        Assert-True ($endpoint.X -eq $endpointX -and $endpoint.Y -eq $endpointY) `
            "Independent legal endpoint ($($endpoint.X),$($endpoint.Y)) is not integral."
        Assert-True ($endpointX -ge 0 -and $endpointY -ge 0 -and
            $endpointX -lt $Mask.Width -and $endpointY -lt $Mask.Height) `
            "Independent legal endpoint ($endpointX,$endpointY) is out of bounds."
        Assert-Equal 255 $Source.GetPixel($endpointX,$endpointY).A `
            "Independent legal endpoint ($endpointX,$endpointY) is not source opaque."
        Assert-Equal 255 $Mask.GetPixel($endpointX,$endpointY).R `
            "Independent legal endpoint ($endpointX,$endpointY) is not writable."
        $isBoundary=$false
        foreach ($offset in @(@(-1,0),@(1,0),@(0,-1),@(0,1)))
        {
            $neighborX=$endpointX+$offset[0]; $neighborY=$endpointY+$offset[1]
            if ($neighborX -lt 0 -or $neighborY -lt 0 -or
                $neighborX -ge $Mask.Width -or $neighborY -ge $Mask.Height -or
                $Mask.GetPixel($neighborX,$neighborY).R -eq 0)
            { $isBoundary=$true; break }
        }
        Assert-True $isBoundary `
            "Independent legal endpoint ($endpointX,$endpointY) is not on the final-mask boundary."
        $nearest=@($vertices.Values | Sort-Object `
            @{Expression={
                $dx=([double]$_.X2/2.0)-$endpointX; $dy=([double]$_.Y2/2.0)-$endpointY
                ($dx*$dx)+($dy*$dy)
            }},@{Expression={[int]$_.Y2}},@{Expression={[int]$_.X2}} | Select-Object -First 1)
        Assert-Equal 1 $nearest.Count 'Independent contour has no exposed vertex for a continuation.'
        $segments.Add((New-Segment 'C' ([double]$nearest[0].X2/2.0) `
            ([double]$nearest[0].Y2/2.0) $endpointX $endpointY))
    }
    return @($segments)
}

function New-SyntheticPair([int]$Width=9,[int]$Height=9)
{
    $source=[Drawing.Bitmap]::new($Width,$Height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $mask=[Drawing.Bitmap]::new($Width,$Height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $sourceGraphics=[Drawing.Graphics]::FromImage($source)
    $maskGraphics=[Drawing.Graphics]::FromImage($mask)
    try
    {
        $sourceGraphics.Clear([Drawing.Color]::Transparent)
        $maskGraphics.Clear([Drawing.Color]::FromArgb(255,0,0,0))
    }
    finally { $sourceGraphics.Dispose(); $maskGraphics.Dispose() }
    return [pscustomobject]@{Source=$source;Mask=$mask}
}

function Set-WritablePixel([object]$Pair,[int]$X,[int]$Y)
{
    $Pair.Source.SetPixel($X,$Y,[Drawing.Color]::FromArgb(255,245,246,247))
    $Pair.Mask.SetPixel($X,$Y,[Drawing.Color]::FromArgb(255,255,255,255))
}

function Assert-ProductionMatchesIndependent(
    [object]$ProductionContour,[object[]]$IndependentSegments,[string]$Label)
{
    $expectedRecords=@(Get-IndependentCanonicalRecords $IndependentSegments)
    $actualRecords=@(Get-DororongCanonicalContourRecords $ProductionContour)
    Assert-Equal ($expectedRecords -join "`n") ($actualRecords -join "`n") `
        "$Label production/independent canonical records differ."
    $expectedHash=Get-Sha256Text ($expectedRecords -join "`n")
    Assert-Equal $expectedHash (Get-DororongCanonicalContourHash $ProductionContour) `
        "$Label production/independent contour hash differs."
    return $expectedHash
}

function Save-ImmutableEvidence([Drawing.Bitmap]$Bitmap,[string]$Path)
{
    $bytes=Get-BitmapPngBytes $Bitmap
    $hash=Get-BytesSha256 $bytes
    if ([IO.File]::Exists($Path))
    {
        Assert-Equal $hash (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash `
            "Existing durable evidence differs at '$Path'."
    }
    else { [IO.File]::WriteAllBytes($Path,$bytes) }
    return $hash
}

function New-ContourOverlay([Drawing.Bitmap]$Source,[object]$Contour,[Drawing.PointF[]]$Endpoints)
{
    $overlay=$Source.Clone(); $graphics=[Drawing.Graphics]::FromImage($overlay)
    $exposedPen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,0,92,255),1.5)
    $frontPen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,0,180,70),2.0)
    $rearPen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,255,128,0),2.0)
    $frontBrush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,0,130,45))
    $rearBrush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,190,80,0))
    $font=[Drawing.Font]::new([Drawing.FontFamily]::GenericSansSerif,7.0,[Drawing.FontStyle]::Bold)
    try
    {
        $graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::None
        $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
        foreach ($segment in @($Contour.ExposedSegments))
        { $graphics.DrawLine($exposedPen,[single]$segment.X1,[single]$segment.Y1,[single]$segment.X2,[single]$segment.Y2) }
        foreach ($segment in @($Contour.ContinuationSegments))
        {
            $isFront=([double]$segment.X1 -eq $Endpoints[0].X -and [double]$segment.Y1 -eq $Endpoints[0].Y) -or
                ([double]$segment.X2 -eq $Endpoints[0].X -and [double]$segment.Y2 -eq $Endpoints[0].Y)
            $pen=if($isFront){$frontPen}else{$rearPen}
            $graphics.DrawLine($pen,[single]$segment.X1,[single]$segment.Y1,[single]$segment.X2,[single]$segment.Y2)
        }
        for($index=0;$index -lt $Endpoints.Count;$index++)
        {
            $endpoint=$Endpoints[$index]; $brush=if($index -eq 0){$frontBrush}else{$rearBrush}
            $graphics.FillEllipse($brush,$endpoint.X-2.0,$endpoint.Y-2.0,4.0,4.0)
            $label=if($index -eq 0){'front 118,151'}else{'rear 161,116'}
            $graphics.DrawString($label,$font,$brush,$endpoint.X+3.0,$endpoint.Y-10.0)
        }
    }
    finally
    {
        $font.Dispose();$frontBrush.Dispose();$rearBrush.Dispose()
        $exposedPen.Dispose();$frontPen.Dispose();$rearPen.Dispose();$graphics.Dispose()
    }
    return $overlay
}

function Resize-Nearest4x([Drawing.Bitmap]$Bitmap)
{
    $result=[Drawing.Bitmap]::new($Bitmap.Width*4,$Bitmap.Height*4,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics=[Drawing.Graphics]::FromImage($result)
    try
    {
        $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
        $graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.DrawImage($Bitmap,[Drawing.Rectangle]::new(0,0,$result.Width,$result.Height),
            0,0,$Bitmap.Width,$Bitmap.Height,[Drawing.GraphicsUnit]::Pixel)
    }
    finally{$graphics.Dispose()}
    return $result
}

function Crop-Bitmap([Drawing.Bitmap]$Bitmap,[Drawing.Rectangle]$Rectangle)
{ return $Bitmap.Clone($Rectangle,[Drawing.Imaging.PixelFormat]::Format32bppArgb) }

Assert-True $GeometryOnly 'This focused test must be run with -GeometryOnly.'
$repositoryRoot=Split-Path -Parent $PSScriptRoot
$sourcePath=Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical-source.png'
$maskPath=Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-body-region-mask.png'
$sourceRasterModulePath=Join-Path $repositoryRoot 'tools/Dororong.SourceRaster.psm1'
$outlineModulePath=Join-Path $repositoryRoot 'tools/Dororong.SubpixelOutline.psm1'
$constantsPath=Join-Path $repositoryRoot 'tools/Dororong.SubpixelOutline.Constants.psd1'
$evidenceDirectory=Join-Path $repositoryRoot '.superpowers/sdd/2026-08-27-dororong-complete-body-ownership-outline/contour-evidence'
$legalEndpoints=[Drawing.PointF[]]@([Drawing.PointF]::new(118,151),[Drawing.PointF]::new(161,116))

Assert-Equal 'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504' `
    (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash 'Canonical source changed.'
Assert-Equal 'D08B3A941C662F1CBC55C486C13FD4C6CD8901DA9CD5CF8512509698219FE46F' `
    (Get-FileHash -LiteralPath $maskPath -Algorithm SHA256).Hash 'Reviewed final mask changed.'

Add-Type -AssemblyName System.Drawing
Import-Module $sourceRasterModulePath -Force
$rawSource=[Drawing.Bitmap]::new($sourcePath);$processedSource=$null;$independentMask=$null
try
{
    $processedSource=Remove-DororongBoundaryBackground $rawSource
    $independentMask=[Drawing.Bitmap]::new($maskPath)
    $independentRealSegments=@(Get-IndependentVisibleSegments $processedSource $independentMask $legalEndpoints)
    $independentRealRecords=@(Get-IndependentCanonicalRecords $independentRealSegments)
    $independentRealHash=Get-Sha256Text ($independentRealRecords -join "`n")
    $independentExposedCount=@($independentRealSegments|Where-Object Kind -eq 'E').Count
    Assert-Equal 382 $independentExposedCount 'Independent real exposed count changed.'
    Assert-Equal 2 @($independentRealSegments|Where-Object Kind -eq 'C').Count `
        'Independent real continuation count changed.'
    Assert-Equal 384 $independentRealRecords.Count 'Independent canonical record count changed.'
    Assert-Equal 'A29D007B699A16B555FE5133854E832FEFD8409EA85F2FECE3EEA97BF444FD65' `
        $independentRealHash 'Independent canonical contour hash changed.'
    Write-Output "INDEPENDENT CONTOUR hash=$independentRealHash exposed=$independentExposedCount continuations=2 records=$($independentRealRecords.Count)"

    Import-Module $outlineModulePath -Force
    $requiredCommands=@('Import-DororongBodyMask','New-DororongVisibleContour',
        'Get-DororongCanonicalContourRecords','Get-DororongCanonicalContourHash')
    foreach($command in $requiredCommands)
    {
        Assert-True ($null -ne (Get-Command $command -ErrorAction SilentlyContinue)) `
            "Missing canonical contour interface '$command'."
    }
    $exportedCommands=@(Get-Command -Module Dororong.SubpixelOutline|ForEach-Object Name|Sort-Object)
    Assert-Equal (($requiredCommands|Sort-Object)-join '|') ($exportedCommands-join '|') `
        'Geometry module exports non-geometry interfaces.'

    $constants=Import-PowerShellDataFile -LiteralPath $constantsPath
    $expectedKeys=@('SubpixelFactor','FillDistance','FillFloor','MaximumChroma','FillNeighborCount',
        'WidthSweepMinimum','WidthSweepMaximum','WidthSweepStep','LegalEndpoints','OutlineSamples')|Sort-Object
    Assert-Equal ($expectedKeys-join '|') (@($constants.Keys|Sort-Object)-join '|') `
        'Geometry constants contain missing or extra fields.'
    Assert-True (-not $constants.ContainsKey('Width')) 'Geometry constants must not freeze Width.'
    Assert-Equal 8 $constants.SubpixelFactor 'Subpixel factor changed.'
    Assert-Equal 8.0 $constants.FillDistance 'Fill distance changed.'
    Assert-Equal 225 $constants.FillFloor 'Fill floor changed.'
    Assert-Equal 8 $constants.MaximumChroma 'Maximum chroma changed.'
    Assert-Equal 8 $constants.FillNeighborCount 'Fill neighbor count changed.'
    Assert-Equal 0.25 $constants.WidthSweepMinimum 'Width sweep minimum changed.'
    Assert-Equal 4.00 $constants.WidthSweepMaximum 'Width sweep maximum changed.'
    Assert-Equal 0.015625 $constants.WidthSweepStep 'Width sweep step changed.'
    Assert-Equal '118,151|161,116' `
        (@($constants.LegalEndpoints|ForEach-Object{"$($_.X),$($_.Y)"})-join '|') 'Legal endpoints changed.'
    Assert-Equal '20,125|104,131|24,95|109,64|54,62|24,143' `
        (@($constants.OutlineSamples|ForEach-Object{"$($_[0]),$($_[1])"})-join '|') 'Outline samples changed.'

    # Off-canvas space is not a processed-source pixel and therefore cannot
    # contribute an exposed edge. This catches treating canvas bounds as alpha zero.
    $canvasEdge=New-SyntheticPair 1 1
    try
    {
        Set-WritablePixel $canvasEdge 0 0
        $actualCanvasEdge=New-DororongVisibleContour `
            $canvasEdge.Source $canvasEdge.Mask ([Drawing.PointF[]]@())
        Assert-Equal 0 $actualCanvasEdge.ExposedSegmentCount `
            'Off-canvas neighbors emitted production exposed edges.'
        $independentCanvasEdge=@(Get-IndependentVisibleSegments `
            $canvasEdge.Source $canvasEdge.Mask ([Drawing.PointF[]]@()))
        Assert-Equal 0 @($independentCanvasEdge|Where-Object Kind -eq 'E').Count `
            'Off-canvas neighbors emitted independent exposed edges.'
        Write-Output 'SYNTHETIC EDGE PASS offCanvasExposed=0'
    }
    finally{$canvasEdge.Source.Dispose();$canvasEdge.Mask.Dispose()}

    $rectangle=New-SyntheticPair
    try
    {
        for($y=2;$y -le 6;$y++){for($x=2;$x -le 6;$x++){Set-WritablePixel $rectangle $x $y}}
        $expectedRectangle=@(Get-IndependentVisibleSegments $rectangle.Source $rectangle.Mask ([Drawing.PointF[]]@()))
        $actualRectangle=New-DororongVisibleContour $rectangle.Source $rectangle.Mask ([Drawing.PointF[]]@())
        Assert-Equal 20 $actualRectangle.ExposedSegmentCount 'Rectangle exposed count changed.'
        Assert-ProductionMatchesIndependent $actualRectangle $expectedRectangle 'Rectangle'|Out-Null
    }
    finally{$rectangle.Source.Dispose();$rectangle.Mask.Dispose()}

    $stair=New-SyntheticPair
    try
    {
        foreach($coordinate in @(@(2,2),@(3,2),@(3,3),@(4,3),@(4,4),@(5,4),@(5,5),@(6,5),@(6,6)))
        {Set-WritablePixel $stair $coordinate[0] $coordinate[1]}
        $expectedStair=@(Get-IndependentVisibleSegments $stair.Source $stair.Mask ([Drawing.PointF[]]@()))
        $actualStair=New-DororongVisibleContour $stair.Source $stair.Mask ([Drawing.PointF[]]@())
        Assert-Equal 20 $actualStair.ExposedSegmentCount 'Stair-step exposed count changed.'
        Assert-ProductionMatchesIndependent $actualStair $expectedStair 'Stair-step'|Out-Null
    }
    finally{$stair.Source.Dispose();$stair.Mask.Dispose()}

    $protected=New-SyntheticPair
    try
    {
        for($y=2;$y -le 4;$y++){for($x=2;$x -le 4;$x++){Set-WritablePixel $protected $x $y}}
        $protected.Source.SetPixel(5,3,[Drawing.Color]::FromArgb(255,20,30,40))
        $syntheticEndpoint=[Drawing.PointF[]]@([Drawing.PointF]::new(4,3))
        $expectedProtected=@(Get-IndependentVisibleSegments $protected.Source $protected.Mask $syntheticEndpoint)
        $actualProtected=New-DororongVisibleContour $protected.Source $protected.Mask $syntheticEndpoint
        Assert-Equal 11 $actualProtected.ExposedSegmentCount 'Opaque protected contact emitted a hidden edge.'
        Assert-Equal 1 $actualProtected.ContinuationSegmentCount 'Synthetic continuation count changed.'
        Assert-ProductionMatchesIndependent $actualProtected $expectedProtected 'Protected contact'|Out-Null
        $continuation=@($actualProtected.ContinuationSegments)[0]
        Assert-Equal '4.5,2.5->4,3' `
            "$($continuation.X1),$($continuation.Y1)->$($continuation.X2),$($continuation.Y2)" `
            'Nearest exposed vertex ordinal Y,X tie changed.'
    }
    finally{$protected.Source.Dispose();$protected.Mask.Dispose()}

    $productionMask=Import-DororongBodyMask $maskPath
    try
    {
        $productionContour=New-DororongVisibleContour $processedSource $productionMask $legalEndpoints
        $realHash=Assert-ProductionMatchesIndependent $productionContour $independentRealSegments 'Real contour'
        Assert-Equal $independentExposedCount $productionContour.ExposedSegmentCount 'Real exposed count changed.'
        Assert-Equal 2 $productionContour.ContinuationSegmentCount 'Real continuation count changed.'
        $productionRecords=@(Get-DororongCanonicalContourRecords $productionContour)

        $reversedAndPermuted=[Collections.Generic.List[object]]::new()
        for($index=@($productionContour.Segments).Count-1;$index -ge 0;$index--)
        {
            $segment=@($productionContour.Segments)[$index]
            $reversedAndPermuted.Add((New-Segment $segment.Kind $segment.X2 $segment.Y2 $segment.X1 $segment.Y1))
        }
        $reversedObject=[pscustomobject]@{Segments=@($reversedAndPermuted)}
        Assert-Equal $realHash (Get-DororongCanonicalContourHash $reversedObject) `
            'Raw reversal/permutation changed canonical hash.'
        $duplicateObject=[pscustomobject]@{Segments=@($productionContour.Segments)+@($productionContour.Segments)[0]}
        Assert-Equal $realHash (Get-DororongCanonicalContourHash $duplicateObject) `
            'Exact duplicate removal changed canonical hash.'
        Write-Output 'MUTATION PASS label=raw-reversal-permutation result=same-canonical-hash'
        Write-Output 'MUTATION PASS label=exact-duplicate result=same-canonical-hash'

        $endpointFailure=Assert-ThrowsLike {
            New-DororongVisibleContour $processedSource $productionMask ([Drawing.PointF[]]@(
                [Drawing.PointF]::new(119,151),[Drawing.PointF]::new(161,116)))
        } 'Legal endpoint.*119,151.*(writable|boundary|opaque)' 'Endpoint 118,151 to 119,151 mutation'
        Write-Output "MUTATION PASS label=endpoint-118-to-119 failure=$endpointFailure"

        $mutatedMask=$productionMask.Clone()
        try
        {
            $changed=$false
            for($y=0;$y -lt $mutatedMask.Height -and -not $changed;$y++)
            {
                for($x=0;$x -lt $mutatedMask.Width -and -not $changed;$x++)
                {
                    if($mutatedMask.GetPixel($x,$y).R -ne 255){continue}
                    foreach($offset in @(@(-1,0),@(1,0),@(0,-1),@(0,1)))
                    {
                        $neighborX=$x+$offset[0];$neighborY=$y+$offset[1]
                        if($neighborX -lt 0 -or $neighborY -lt 0 -or $neighborX -ge $mutatedMask.Width -or
                            $neighborY -ge $mutatedMask.Height){continue}
                        if($mutatedMask.GetPixel($neighborX,$neighborY).R -eq 0 -and
                            $processedSource.GetPixel($neighborX,$neighborY).A -eq 0)
                        {$mutatedMask.SetPixel($x,$y,[Drawing.Color]::FromArgb(255,0,0,0));$changed=$true;break}
                    }
                }
            }
            Assert-True $changed 'Could not identify an exposed writable boundary bit for mutation.'
            $mutatedContour=New-DororongVisibleContour $processedSource $mutatedMask $legalEndpoints
            Assert-True ((Get-DororongCanonicalContourHash $mutatedContour)-ne $realHash) `
                'Final-mask boundary-bit mutation did not change contour hash.'
            Write-Output 'MUTATION PASS label=final-mask-boundary-bit failure=contour-hash'
        }
        finally{$mutatedMask.Dispose()}

        $frontHidden=New-Segment 'E' 118.5 150.5 118.5 151.5
        $protectedContactObject=[pscustomobject]@{Segments=@($productionContour.Segments)+$frontHidden}
        Assert-True ((Get-DororongCanonicalContourHash $protectedContactObject)-ne $realHash) `
            'Protected-contact E mutation did not change contour hash.'
        Write-Output 'MUTATION PASS label=protected-contact-emitted-as-E failure=protected-contact/hash'

        $withoutContinuation=[pscustomobject]@{Segments=@($productionContour.Segments|Where-Object{
            -not($_.Kind -eq 'C' -and (($_.X1 -eq 118 -and $_.Y1 -eq 151)-or
                ($_.X2 -eq 118 -and $_.Y2 -eq 151)))})}
        Assert-Equal 1 @($withoutContinuation.Segments|Where-Object Kind -eq 'C').Count `
            'Continuation-removal mutation did not remove exactly one continuation.'
        Assert-True ((Get-DororongCanonicalContourHash $withoutContinuation)-ne $realHash) `
            'Continuation-removal mutation did not change contour hash.'
        Write-Output 'MUTATION PASS label=continuation-removed failure=continuation-count/hash'

        $geometrySegments=@($productionContour.Segments|ForEach-Object{New-Segment $_.Kind $_.X1 $_.Y1 $_.X2 $_.Y2})
        $geometrySegments[0].X2=[double]$geometrySegments[0].X2+0.5
        Assert-True ((Get-DororongCanonicalContourHash ([pscustomobject]@{Segments=$geometrySegments}))-ne $realHash) `
            'Geometry mutation did not change contour hash.'
        $kindSegments=@($productionContour.Segments|ForEach-Object{New-Segment $_.Kind $_.X1 $_.Y1 $_.X2 $_.Y2})
        $kindSegments[0].Kind='C'
        Assert-True ((Get-DororongCanonicalContourHash ([pscustomobject]@{Segments=$kindSegments}))-ne $realHash) `
            'Kind mutation did not change contour hash.'
        Write-Output 'MUTATION PASS label=geometry-or-kind-changed failure=canonical-hash'

        $mutationDirectory=Join-Path ([IO.Path]::GetTempPath()) ('dororong-contour-mask-hash-'+[Guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($mutationDirectory)|Out-Null
        $mutatedMaskPath=Join-Path $mutationDirectory 'mutated-mask.png';$hashMask=$productionMask.Clone()
        try
        {
            $hashMask.SetPixel(0,0,[Drawing.Color]::FromArgb(255,255,255,255))
            $hashMask.Save($mutatedMaskPath,[Drawing.Imaging.ImageFormat]::Png)
            $maskHashFailure=Assert-ThrowsLike {Import-DororongBodyMask $mutatedMaskPath} `
                'Body mask hash mismatch' 'Task 1 final-mask hash mutation'
            Write-Output "MUTATION PASS label=task1-mask-hash failure=$maskHashFailure"
        }
        finally
        {
            $hashMask.Dispose();$resolvedMutationDirectory=[IO.Path]::GetFullPath($mutationDirectory)
            $resolvedTemporaryRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
            Assert-True ($resolvedMutationDirectory.StartsWith($resolvedTemporaryRoot,[StringComparison]::OrdinalIgnoreCase)) `
                'Mutation cleanup path escaped the temporary root.'
            [IO.Directory]::Delete($resolvedMutationDirectory,$true)
            Assert-True (-not [IO.Directory]::Exists($resolvedMutationDirectory)) 'Mutation cleanup readback failed.'
        }

        [IO.Directory]::CreateDirectory($evidenceDirectory)|Out-Null
        $overlay=New-ContourOverlay $rawSource $productionContour $legalEndpoints
        $nearest=$null;$frontCrop=$null;$rearCrop=$null
        try
        {
            $nearest=Resize-Nearest4x $overlay
            $frontCrop=Crop-Bitmap $nearest ([Drawing.Rectangle]::new(392,524,176,176))
            $rearCrop=Crop-Bitmap $nearest ([Drawing.Rectangle]::new(556,376,176,176))
            $prefix=$realHash.Substring(0,16).ToLowerInvariant()
            $sourceEvidencePath=Join-Path $evidenceDirectory "$prefix-contour-source-225.png"
            $nearestEvidencePath=Join-Path $evidenceDirectory "$prefix-contour-nearest-4x-900.png"
            $frontEvidencePath=Join-Path $evidenceDirectory "$prefix-front-junction-nearest-4x.png"
            $rearEvidencePath=Join-Path $evidenceDirectory "$prefix-rear-junction-nearest-4x.png"
            $sourceEvidenceHash=Save-ImmutableEvidence $overlay $sourceEvidencePath
            $nearestEvidenceHash=Save-ImmutableEvidence $nearest $nearestEvidencePath
            $frontEvidenceHash=Save-ImmutableEvidence $frontCrop $frontEvidencePath
            $rearEvidenceHash=Save-ImmutableEvidence $rearCrop $rearEvidencePath
            Write-Output "EVIDENCE source=$sourceEvidencePath sha256=$sourceEvidenceHash"
            Write-Output "EVIDENCE nearest4x=$nearestEvidencePath sha256=$nearestEvidenceHash"
            Write-Output "EVIDENCE frontJunction=$frontEvidencePath sha256=$frontEvidenceHash"
            Write-Output "EVIDENCE rearJunction=$rearEvidencePath sha256=$rearEvidenceHash"
        }
        finally
        {
            if($null-ne$rearCrop){$rearCrop.Dispose()};if($null-ne$frontCrop){$frontCrop.Dispose()}
            if($null-ne$nearest){$nearest.Dispose()};$overlay.Dispose()
        }

        $endpointReadback=@($productionContour.ContinuationSegments|ForEach-Object{
            if(($_.X1-eq118-and$_.Y1-eq151)-or($_.X2-eq118-and$_.Y2-eq151)){'118,151'}
            elseif(($_.X1-eq161-and$_.Y1-eq116)-or($_.X2-eq161-and$_.Y2-eq116)){'161,116'}
        }|Sort-Object)
        Assert-Equal '118,151|161,116' ($endpointReadback-join '|') 'Production endpoint readback changed.'
        Write-Output "CANONICAL CONTOUR PASS maskHash=$((Get-FileHash -LiteralPath $maskPath -Algorithm SHA256).Hash) contourHash=$realHash records=$($productionRecords.Count) exposed=$($productionContour.ExposedSegmentCount) continuations=2 endpoints=$($endpointReadback-join ';')"
    }
    finally{$productionMask.Dispose()}
}
finally
{
    if($null-ne$independentMask){$independentMask.Dispose()}
    if($null-ne$processedSource){$processedSource.Dispose()}
    $rawSource.Dispose()
}
