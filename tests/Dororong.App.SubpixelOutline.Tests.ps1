param([switch]$GeometryOnly,[switch]$SyntheticOnly,[switch]$ReloadOnly,[switch]$ThinOutlineOnly)

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

function Get-IndependentPointSegmentDistance(
    [double]$X,[double]$Y,[object]$Segment)
{
    $dx=[double]$Segment.X2-[double]$Segment.X1
    $dy=[double]$Segment.Y2-[double]$Segment.Y1
    $lengthSquared=($dx*$dx)+($dy*$dy)
    if($lengthSquared -eq 0.0)
    {
        $pointDx=$X-[double]$Segment.X1;$pointDy=$Y-[double]$Segment.Y1
        return [Math]::Sqrt(($pointDx*$pointDx)+($pointDy*$pointDy))
    }
    $t=((($X-[double]$Segment.X1)*$dx)+(($Y-[double]$Segment.Y1)*$dy))/$lengthSquared
    $t=[Math]::Clamp($t,0.0,1.0)
    $nearestX=[double]$Segment.X1+($t*$dx)
    $nearestY=[double]$Segment.Y1+($t*$dy)
    return [Math]::Sqrt((($X-$nearestX)*($X-$nearestX))+(($Y-$nearestY)*($Y-$nearestY)))
}

function Get-IndependentMinimumDistance([double]$X,[double]$Y,[object]$Contour,[switch]$ExposedOnly)
{
    $minimum=[double]::PositiveInfinity
    foreach($segment in @($Contour.Segments))
    {
        if($ExposedOnly -and [string]$segment.Kind -ne 'E'){continue}
        $distance=Get-IndependentPointSegmentDistance $X $Y $segment
        if($distance -lt $minimum){$minimum=$distance}
    }
    return $minimum
}

function Get-IndependentFillSeeds(
    [Drawing.Bitmap]$Source,[Drawing.Bitmap]$SeedMask,[Drawing.Bitmap]$FinalMask,
    [object]$Contour,[int]$Floor=225,[double]$MinimumDistance=8.0,[bool]$RequireSeedMask=$true)
{
    $seeds=[Collections.Generic.List[object]]::new()
    for($y=0;$y -lt $FinalMask.Height;$y++)
    {
        for($x=0;$x -lt $FinalMask.Width;$x++)
        {
            if($FinalMask.GetPixel($x,$y).R -ne 255){continue}
            if($RequireSeedMask -and $SeedMask.GetPixel($x,$y).R -ne 255){continue}
            $pixel=$Source.GetPixel($x,$y)
            if($pixel.A -ne 255 -or $pixel.R -lt $Floor -or $pixel.G -lt $Floor -or $pixel.B -lt $Floor){continue}
            $maximum=[Math]::Max($pixel.R,[Math]::Max($pixel.G,$pixel.B))
            $minimum=[Math]::Min($pixel.R,[Math]::Min($pixel.G,$pixel.B))
            if(($maximum-$minimum) -gt 8){continue}
            if((Get-IndependentMinimumDistance $x $y $Contour) -le $MinimumDistance){continue}
            $seeds.Add([pscustomobject]@{X=$x;Y=$y;Color=$pixel})
        }
    }
    return @($seeds)
}

function Get-IndependentInterpolatedColor([int]$X,[int]$Y,[object[]]$Seeds)
{
    $nearest=@($Seeds|Sort-Object `
        @{Expression={($_.X-$X)*($_.X-$X)+($_.Y-$Y)*($_.Y-$Y)}},Y,X|Select-Object -First 8)
    Assert-Equal 8 $nearest.Count "Independent fill at ($X,$Y) did not select eight seeds."
    $weightTotal=0.0;$red=0.0;$green=0.0;$blue=0.0
    foreach($seed in $nearest)
    {
        $distanceSquared=($seed.X-$X)*($seed.X-$X)+($seed.Y-$Y)*($seed.Y-$Y)
        $weight=1.0/(1.0+$distanceSquared)
        $weightTotal+=$weight
        $red+=$weight*$seed.Color.R;$green+=$weight*$seed.Color.G;$blue+=$weight*$seed.Color.B
    }
    return [Drawing.Color]::FromArgb(255,
        [int][Math]::Round($red/$weightTotal,0,[MidpointRounding]::ToEven),
        [int][Math]::Round($green/$weightTotal,0,[MidpointRounding]::ToEven),
        [int][Math]::Round($blue/$weightTotal,0,[MidpointRounding]::ToEven))
}

function Get-IndependentSampleDistances(
    [int]$X,[int]$Y,[object]$Contour,[int]$Factor=8,[double]$OriginShift=0.0,
    [switch]$ExposedOnly,[double]$ExposedMultiplier=1.0,[double]$ContinuationMultiplier=2.0)
{
    $distances=[Collections.Generic.List[double]]::new()
    for($j=0;$j -lt $Factor;$j++)
    {
        for($i=0;$i -lt $Factor;$i++)
        {
            $sampleX=$X-0.5+(($i+0.5)/$Factor)+$OriginShift
            $sampleY=$Y-0.5+(($j+0.5)/$Factor)+$OriginShift
            $minimum=[double]::PositiveInfinity
            foreach($segment in @($Contour.Segments))
            {
                if($ExposedOnly -and [string]$segment.Kind -ne 'E'){continue}
                $multiplier=if([string]$segment.Kind-eq'C'){$ContinuationMultiplier}else{$ExposedMultiplier}
                $distance=$multiplier*(Get-IndependentPointSegmentDistance $sampleX $sampleY $segment)
                if($distance-lt$minimum){$minimum=$distance}
            }
            $distances.Add($minimum)
        }
    }
    return @($distances)
}

function Get-IndependentCoverage([double[]]$Distances,[double]$Width)
{ return [double]@($Distances|Where-Object{$_ -le $Width}).Count/[double]$Distances.Count }

function Get-IndependentKindSampleDistances(
    [int]$X,[int]$Y,[object]$Contour,[string]$Kind,[int]$Factor=8,[double]$OriginShift=0.0)
{
    $distances=[Collections.Generic.List[double]]::new()
    for($j=0;$j-lt$Factor;$j++)
    {
        for($i=0;$i-lt$Factor;$i++)
        {
            $sampleX=$X-0.5+(($i+0.5)/$Factor)+$OriginShift
            $sampleY=$Y-0.5+(($j+0.5)/$Factor)+$OriginShift
            $minimum=[double]::PositiveInfinity
            foreach($segment in @($Contour.Segments|Where-Object Kind -eq $Kind))
            {
                $distance=Get-IndependentPointSegmentDistance $sampleX $sampleY $segment
                if($distance-lt$minimum){$minimum=$distance}
            }
            $distances.Add($minimum)
        }
    }
    return @($distances)
}

function Get-IndependentFCoverage(
    [double[]]$ExposedDistances,[double[]]$ContinuationDistances,[double]$Width,
    [double]$ExposedGain=2.5,[double]$ContinuationGain=0.0)
{
    $eRaw=Get-IndependentCoverage $ExposedDistances $Width
    $cRaw=Get-IndependentCoverage $ContinuationDistances ($Width/2.0)
    $e=[Math]::Clamp($eRaw*$ExposedGain,0.0,1.0)
    $c=[Math]::Clamp($cRaw*$ContinuationGain,0.0,1.0)
    return [Math]::Max($e,$c)
}

function Get-IndependentBlendedColor(
    [Drawing.Color]$Fill,[Drawing.Color]$Outline,[double]$Coverage,[int]$Alpha)
{
    return [Drawing.Color]::FromArgb($Alpha,
        [int][Math]::Round(($Fill.R*(1.0-$Coverage))+($Outline.R*$Coverage),0,
            [MidpointRounding]::ToEven),
        [int][Math]::Round(($Fill.G*(1.0-$Coverage))+($Outline.G*$Coverage),0,
            [MidpointRounding]::ToEven),
        [int][Math]::Round(($Fill.B*(1.0-$Coverage))+($Outline.B*$Coverage),0,
            [MidpointRounding]::ToEven))
}

function Assert-IndependentWritableBlend(
    [Drawing.Bitmap]$Source,[Drawing.Bitmap]$Mask,[Drawing.Color[,]]$FillField,
    [object]$Contour,[Drawing.Color]$Outline,[double]$Width,[Drawing.Bitmap]$Candidate)
{
    for($y=0;$y-lt$Mask.Height;$y++)
    {
        for($x=0;$x-lt$Mask.Width;$x++)
        {
            if($Mask.GetPixel($x,$y).R-ne255){continue}
            $eDistances=[double[]](Get-IndependentKindSampleDistances $x $y $Contour 'E')
            $cDistances=[double[]](Get-IndependentKindSampleDistances $x $y $Contour 'C')
            $coverage=Get-IndependentFCoverage $eDistances $cDistances $Width
            $expected=Get-IndependentBlendedColor `
                $FillField[$x,$y] $Outline $coverage $Source.GetPixel($x,$y).A
            $actual=$Candidate.GetPixel($x,$y)
            Assert-Equal $expected.ToArgb() $actual.ToArgb() `
                "Writable blend RGB changed at ($x,$y)."
        }
    }
}

function Assert-ColorEqual([Drawing.Color]$Expected,[Drawing.Color]$Actual,[string]$Message)
{ Assert-Equal $Expected.ToArgb() $Actual.ToArgb() $Message }

function Assert-RasterPreservesAuthority(
    [Drawing.Bitmap]$Source,[Drawing.Bitmap]$Mask,[Drawing.Bitmap]$Candidate)
{
    for($y=0;$y -lt $Source.Height;$y++)
    {
        for($x=0;$x -lt $Source.Width;$x++)
        {
            $before=$Source.GetPixel($x,$y);$after=$Candidate.GetPixel($x,$y)
            Assert-Equal $before.A $after.A "Source alpha changed at ($x,$y)."
            if($Mask.GetPixel($x,$y).R -eq 0)
            { Assert-Equal $before.ToArgb() $after.ToArgb() "Protected mask-zero RGB changed at ($x,$y)." }
        }
    }
}

function Get-IndependentResizeProxyComponents(
    [Drawing.Bitmap]$Mask,[Drawing.Bitmap]$Source,[int]$MaximumSize=7,
    [int]$MaximumChroma=8,[switch]$EightConnected)
{
    $visited=[bool[]]::new($Mask.Width*$Mask.Height)
    $accepted=[Collections.Generic.List[object]]::new()
    $offsets=if($EightConnected){
        @(@(-1,-1),@(0,-1),@(1,-1),@(-1,0),@(1,0),@(-1,1),@(0,1),@(1,1))
    }else{@(@(-1,0),@(1,0),@(0,-1),@(0,1))}
    for($startY=0;$startY-lt$Mask.Height;$startY++)
    {
        for($startX=0;$startX-lt$Mask.Width;$startX++)
        {
            $startIndex=($startY*$Mask.Width)+$startX
            if($visited[$startIndex]-or$Mask.GetPixel($startX,$startY).R-ne0){continue}
            $queue=[Collections.Generic.Queue[int]]::new();$queue.Enqueue($startIndex)
            $visited[$startIndex]=$true;$indices=[Collections.Generic.List[int]]::new()
            $touchesFrame=$false;$observedChroma=0
            $minX=$startX;$maxX=$startX;$minY=$startY;$maxY=$startY
            while($queue.Count-gt0)
            {
                $index=$queue.Dequeue();$indices.Add($index)
                $x=$index%$Mask.Width;$y=[int][Math]::Floor($index/$Mask.Width)
                $minX=[Math]::Min($minX,$x);$maxX=[Math]::Max($maxX,$x)
                $minY=[Math]::Min($minY,$y);$maxY=[Math]::Max($maxY,$y)
                if($x-eq0-or$y-eq0-or$x-eq($Mask.Width-1)-or$y-eq($Mask.Height-1)){$touchesFrame=$true}
                $pixel=$Source.GetPixel($x,$y)
                $chroma=[Math]::Max($pixel.R,[Math]::Max($pixel.G,$pixel.B))-
                    [Math]::Min($pixel.R,[Math]::Min($pixel.G,$pixel.B))
                $observedChroma=[Math]::Max($observedChroma,$chroma)
                foreach($offset in $offsets)
                {
                    $nx=$x+$offset[0];$ny=$y+$offset[1]
                    if($nx-lt0-or$ny-lt0-or$nx-ge$Mask.Width-or$ny-ge$Mask.Height){continue}
                    $neighborIndex=($ny*$Mask.Width)+$nx
                    if(-not$visited[$neighborIndex]-and$Mask.GetPixel($nx,$ny).R-eq0)
                    {$visited[$neighborIndex]=$true;$queue.Enqueue($neighborIndex)}
                }
            }
            if(-not$touchesFrame-and$indices.Count-le$MaximumSize-and$observedChroma-le$MaximumChroma)
            {
                $accepted.Add([pscustomobject]@{
                    Indices=[int[]]$indices.ToArray();Size=$indices.Count
                    MinX=$minX;MinY=$minY;MaxX=$maxX;MaxY=$maxY;MaximumChroma=$observedChroma
                })
            }
        }
    }
    return @($accepted)
}

function Get-IndependentResizeProxyMembership([object[]]$Components,[int]$Width)
{
    $records=@($Components|ForEach-Object{
        $coordinates=@($_.Indices|ForEach-Object{
            [pscustomobject]@{X=[int]($_%$Width);Y=[int][Math]::Floor($_/$Width)}
        }|Sort-Object Y,X)
        [pscustomobject]@{
            FirstY=$coordinates[0].Y;FirstX=$coordinates[0].X
            Record="C|$($coordinates.Count)|$(@($coordinates|ForEach-Object{"$($_.Y),$($_.X)"})-join ';')"
        }
    }|Sort-Object FirstY,FirstX|ForEach-Object Record)
    $text=$records-join"`n"
    return [pscustomobject]@{Records=$records;Hash=Get-Sha256Text $text}
}

function New-IndependentResizeProxy(
    [Drawing.Bitmap]$Candidate,[Drawing.Color[,]]$FillField,[object[]]$Components)
{
    $proxy=$Candidate.Clone(
        [Drawing.Rectangle]::new(0,0,$Candidate.Width,$Candidate.Height),
        [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        foreach($component in $Components)
        {
            foreach($index in $component.Indices)
            {
                $x=$index%$Candidate.Width;$y=[int][Math]::Floor($index/$Candidate.Width)
                $fill=$FillField[$x,$y];$original=$proxy.GetPixel($x,$y)
                $proxy.SetPixel($x,$y,[Drawing.Color]::FromArgb($original.A,$fill.R,$fill.G,$fill.B))
            }
        }
        return $proxy
    }
    catch{$proxy.Dispose();throw}
}

function Get-Median([double[]]$Values)
{
    Assert-True ($Values.Count -gt 0) 'Cannot calculate an empty median.'
    $sorted=@($Values|Sort-Object);$middle=[int][Math]::Floor($sorted.Count/2.0)
    if(($sorted.Count%2)-eq1){return [double]$sorted[$middle]}
    return ([double]$sorted[$middle-1]+[double]$sorted[$middle])/2.0
}

function Get-ComponentMedianColor([Drawing.Bitmap]$Source,[object[]]$Samples)
{
    $red=@();$green=@();$blue=@()
    foreach($sample in $Samples)
    {
        $pixel=$Source.GetPixel([int]$sample[0],[int]$sample[1])
        $red+=$pixel.R;$green+=$pixel.G;$blue+=$pixel.B
    }
    return [Drawing.Color]::FromArgb(255,
        [int][Math]::Round((Get-Median ([double[]]$red)),0,[MidpointRounding]::ToEven),
        [int][Math]::Round((Get-Median ([double[]]$green)),0,[MidpointRounding]::ToEven),
        [int][Math]::Round((Get-Median ([double[]]$blue)),0,[MidpointRounding]::ToEven))
}

function Get-ThinOutlineOpticalInk([Drawing.Bitmap]$Bitmap,[double]$X,[double]$Y)
{
    $sample=Get-DororongBilinearPremultipliedSample $Bitmap $X $Y
    $luminance=(0.2126*$sample.R)+(0.7152*$sample.G)+(0.0722*$sample.B)
    return [double]($sample.A*(1.0-$luminance))
}

function Get-ThinOutlineHighOpacityRun([Drawing.Bitmap]$Bitmap,[hashtable]$Normal)
{
    $x1=$Normal.X1Eighth/8.0;$y1=$Normal.Y1Eighth/8.0
    $x2=$Normal.X2Eighth/8.0;$y2=$Normal.Y2Eighth/8.0
    $length=[Math]::Sqrt(($x2-$x1)*($x2-$x1)+($y2-$y1)*($y2-$y1))
    $step=0.125;$runs=@();$start=$null;$last=0.0
    for($distance=0.0;$distance-le$length+0.000000001;$distance+=$step)
    {
        $t=[Math]::Min(1.0,$distance/$length)
        $ink=Get-ThinOutlineOpticalInk $Bitmap `
            ($x1+(($x2-$x1)*$t)) ($y1+(($y2-$y1)*$t))
        if($ink-ge0.50)
        {
            if($null-eq$start){$start=$distance}
            $last=$distance
        }
        elseif($null-ne$start)
        {$runs+=($last-$start+$step);$start=$null}
    }
    if($null-ne$start){$runs+=($last-$start+$step)}
    if($runs.Count-eq0){return 0.0}
    return [double](($runs|Measure-Object -Maximum).Maximum)
}

function Invoke-ThinOutlineDecisionContract(
    [string]$RepositoryRoot,[string]$SourcePath,[string]$SeedPath,[string]$MaskPath,
    [string]$AuthorityPath,[string]$SourceRasterModulePath,[string]$OutlineModulePath,
    [string]$ConstantsPath,[string]$GeneratorPath,[string]$ContinuousOpticsPath)
{
    $runRoot=Join-Path $RepositoryRoot (
        '.superpowers/sdd/2026-08-28-dororong-e-only-thin-outline/' +
        'task-5-tdd-'+[Guid]::NewGuid().ToString('N'))
    $outputDirectory=Join-Path $runRoot 'output'
    $evidenceDirectory=Join-Path $runRoot 'evidence'
    & $GeneratorPath -SourcePath $SourcePath -BodyMaskPath $MaskPath `
        -OutputDirectory $outputDirectory -EvidenceDirectory $evidenceDirectory | Out-Null

    Import-Module $SourceRasterModulePath -Force
    Import-Module $OutlineModulePath -Force
    . $ContinuousOpticsPath
    $constants=Import-PowerShellDataFile -LiteralPath $ConstantsPath
    $authority=Import-PowerShellDataFile -LiteralPath $AuthorityPath
    $native=$generatedSource=$raw=$processed=$seed=$mask=$direct=$cMutation=$null
    try
    {
        $native=[Drawing.Bitmap]::new((Join-Path $outputDirectory 'dororong-canonical.png'))
        $generatedSource=[Drawing.Bitmap]::new((Join-Path $evidenceDirectory 'source-open-candidate.png'))
        $bodyRuns=[double[]]@($authority.BodyNormals|ForEach-Object{
            Get-ThinOutlineHighOpacityRun $native $_.NativeNormal})
        $hairRuns=[double[]]@($authority.HairAnchors|ForEach-Object{
            Get-ThinOutlineHighOpacityRun $native $_.NativeNormal})
        $bodyMedian=Get-Median $bodyRuns
        $hairMedian=Get-Median $hairRuns

        $raw=[Drawing.Bitmap]::new($SourcePath)
        $processed=Remove-DororongBoundaryBackground $raw
        $seed=[Drawing.Bitmap]::new($SeedPath)
        $mask=Import-DororongBodyMask $MaskPath
        $endpoints=[Drawing.PointF[]]@($constants.LegalEndpoints|ForEach-Object{
            [Drawing.PointF]::new([single]$_.X,[single]$_.Y)})
        $contour=New-DororongVisibleContour $processed $mask $endpoints
        Assert-Equal 'A29D007B699A16B555FE5133854E832FEFD8409EA85F2FECE3EEA97BF444FD65' `
            (Get-DororongCanonicalContourHash $contour) 'Thin-outline contour changed.'
        $fill=New-DororongFillField $processed $seed $mask $contour
        $distanceMap=New-DororongSubpixelDistanceMap $mask $contour ([int]$constants.SubpixelFactor)
        $outline=Get-ComponentMedianColor $processed @($constants.OutlineSamples)
        $direct=Invoke-DororongSubpixelOutline `
            $processed $mask $fill $distanceMap $outline ([double]$constants.Width)
        $zeroContinuationDistances=[double[]]::new($distanceMap.ContinuationDistances.Length)
        $cMutationMap=[pscustomobject]@{
            Width=$distanceMap.Width;Height=$distanceMap.Height;Factor=$distanceMap.Factor
            SamplesPerPixel=$distanceMap.SamplesPerPixel;Mask=$distanceMap.Mask
            ExposedDistances=$distanceMap.ExposedDistances
            ContinuationDistances=$zeroContinuationDistances
        }
        $cMutation=Invoke-DororongSubpixelOutline `
            $processed $mask $fill $cMutationMap $outline ([double]$constants.Width)

        $directMismatch=$null;$cMismatch=$null
        for($y=0;$y-lt$generatedSource.Height;$y++)
        {
            for($x=0;$x-lt$generatedSource.Width;$x++)
            {
                $generatedArgb=$generatedSource.GetPixel($x,$y).ToArgb()
                if($null-eq$directMismatch-and$generatedArgb-ne$direct.GetPixel($x,$y).ToArgb())
                {$directMismatch="$x,$y"}
                if($null-eq$cMismatch-and$generatedArgb-ne$cMutation.GetPixel($x,$y).ToArgb())
                {$cMismatch="$x,$y"}
            }
        }
        Assert-True ($null-eq$directMismatch) `
            "Direct real pipeline differs from generator source output at $directMismatch."

        $failures=[Collections.Generic.List[string]]::new()
        if($bodyMedian-gt0.750000000001)
        {$failures.Add("native body high-opacity median expected <=0.75; observed $bodyMedian (hair=$hairMedian)")}
        if($null-ne$cMismatch)
        {$failures.Add("C-distance geometry mutation changed generator output at $cMismatch; expected byte-identical")}
        if($failures.Count-gt0)
        {throw "THIN OUTLINE DECISION FAIL: $($failures-join'; ') runRoot=$runRoot"}
        Write-Output "THIN OUTLINE PASS bodyMedian=$bodyMedian hairMedian=$hairMedian cMutation=byte-identical runRoot=$runRoot"
    }
    finally
    {
        foreach($bitmap in @($cMutation,$direct,$mask,$seed,$processed,$raw,$generatedSource,$native))
        {if($null-ne$bitmap){$bitmap.Dispose()}}
    }
}

function Invoke-SyntheticFillAndRasterContract
{
    $source=[Drawing.Bitmap]::new(25,25,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $seedMask=[Drawing.Bitmap]::new(25,25,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $finalMask=[Drawing.Bitmap]::new(25,25,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $sourceGraphics=[Drawing.Graphics]::FromImage($source)
    $seedGraphics=[Drawing.Graphics]::FromImage($seedMask)
    $finalGraphics=[Drawing.Graphics]::FromImage($finalMask)
    try
    {
        $sourceGraphics.Clear([Drawing.Color]::FromArgb(0,0,0,0))
        $seedGraphics.Clear([Drawing.Color]::FromArgb(255,0,0,0))
        $finalGraphics.Clear([Drawing.Color]::FromArgb(255,0,0,0))
    }
    finally{$finalGraphics.Dispose();$seedGraphics.Dispose();$sourceGraphics.Dispose()}
    try
    {
        for($y=1;$y -le23;$y++)
        {
            for($x=1;$x -le23;$x++)
            {
                $source.SetPixel($x,$y,[Drawing.Color]::FromArgb(255,200,201,202))
                $finalMask.SetPixel($x,$y,[Drawing.Color]::FromArgb(255,255,255,255))
                if($x-ge2-and$x-le22-and$y-ge2-and$y-le22)
                {$seedMask.SetPixel($x,$y,[Drawing.Color]::FromArgb(255,255,255,255))}
            }
        }
        $source.SetPixel(24,12,[Drawing.Color]::FromArgb(255,20,30,40))
        $seedCoordinates=@(@(11,11),@(12,11),@(13,11),@(11,12),@(13,12),@(11,13),@(12,13),@(10,12),@(14,12))
        for($index=0;$index-lt$seedCoordinates.Count;$index++)
        {
            $coordinate=$seedCoordinates[$index]
            $source.SetPixel($coordinate[0],$coordinate[1],[Drawing.Color]::FromArgb(
                255,225+($index%4),226+(($index+1)%4),227+(($index+2)%4)))
        }
        # Each candidate below violates exactly one seed rule.
        $source.SetPixel(12,12,[Drawing.Color]::FromArgb(255,240,241,242))
        $seedMask.SetPixel(12,12,[Drawing.Color]::FromArgb(255,0,0,0))
        $source.SetPixel(9,12,[Drawing.Color]::FromArgb(255,224,225,225))
        $source.SetPixel(8,12,[Drawing.Color]::FromArgb(255,240,241,242))

        $contour=New-DororongVisibleContour $source $finalMask ([Drawing.PointF[]]@())
        $constants=[ordered]@{
            FillDistance=8.0;FillFloor=225;MaximumChroma=8;FillNeighborCount=8
        }
        $independentSeeds=@(Get-IndependentFillSeeds $source $seedMask $finalMask $contour)
        Assert-Equal 9 $independentSeeds.Count 'Independent eligible seed count changed.'
        $fillField=New-DororongFillField $source $seedMask $finalMask $contour
        Assert-True ($fillField-is[Drawing.Color[,]]) `
            'New-DororongFillField did not return Color[,].'
        Assert-Equal 9 $fillField.EligibleSeedCount 'Production eligible seed count changed.'
        Assert-Equal 9 @($fillField.EligibleSeeds).Count 'Production eligible seed records changed.'
        Assert-Equal '10,12|11,11|11,12|11,13|12,11|12,13|13,11|13,12|14,12' `
            (@($fillField.EligibleSeeds|ForEach-Object{"$($_.X),$($_.Y)"}|Sort-Object)-join '|') `
            'Production eligible seed coordinates differ.'

        $independentKeys=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach($seed in $independentSeeds){$null=$independentKeys.Add("$($seed.X),$($seed.Y)")}
        $eligibleOutputChanged=0
        for($y=0;$y-lt25;$y++)
        {
            for($x=0;$x-lt25;$x++)
            {
                if($finalMask.GetPixel($x,$y).R-ne255){continue}
                $actual=$fillField[$x,$y]
                $key="$x,$y"
                $expected=Get-IndependentInterpolatedColor $x $y $independentSeeds
                Assert-ColorEqual $expected $actual "All-pixel smooth fill differs at ($x,$y)."
                Assert-Equal 255 $actual.A "Writable pixel was not reconstructed at ($x,$y)."
                if($independentKeys.Contains($key)-and
                    $source.GetPixel($x,$y).ToArgb()-ne$actual.ToArgb()){$eligibleOutputChanged++}
                $sourcePixel=$source.GetPixel($x,$y)
                if($sourcePixel.R-lt225-or$sourcePixel.G-lt225-or$sourcePixel.B-lt225)
                { Assert-True (-not $independentKeys.Contains($key)) "Dark source pixel seeded at ($x,$y)." }
            }
        }
        Assert-True (-not $independentKeys.Contains('12,12')) 'Newly owned pixel became an eligible seed.'
        Assert-True ($eligibleOutputChanged-gt0) `
            'All eligible sample coordinates retained exact source RGB instead of using the smooth field.'
        $targetExpected=Get-IndependentInterpolatedColor 12 12 $independentSeeds
        Assert-ColorEqual $targetExpected $fillField[12,12] `
            'Eight-nearest ordering, inverse-distance weights, or ties-to-even rounding changed.'
        Write-Output "SYNTHETIC FILL PASS eligibleSeeds=$($fillField.EligibleSeedCount) eligibleOutputsChanged=$eligibleOutputChanged targetArgb=$($targetExpected.ToArgb()) ordering=distance,Y,X weight=1/(1+d^2) rounding=ToEven"

        $coverageContour=[pscustomobject]@{Segments=@(
            (New-Segment 'E' 2.0 11.5 2.0 12.5),
            (New-Segment 'C' 12.0 11.5 12.0 12.5))}
        $distanceMap=New-DororongSubpixelDistanceMap $finalMask $coverageContour 8
        Assert-Equal 8 $distanceMap.Factor 'Production distance-map factor changed.'
        Assert-Equal (25*25*64) $distanceMap.Distances.Length 'Distance-map storage is not all-pixel 8x8.'
        for($y=0;$y-lt25;$y++)
        {
            for($x=0;$x-lt25;$x++)
            {
                if($finalMask.GetPixel($x,$y).R-ne255){continue}
                $expectedE=@(Get-IndependentKindSampleDistances $x $y $coverageContour 'E')
                $expectedC=@(Get-IndependentKindSampleDistances $x $y $coverageContour 'C')
                for($sampleIndex=0;$sampleIndex-lt64;$sampleIndex++)
                {
                    $flat=((($y*25)+$x)*64)+$sampleIndex
                    Assert-True ([Math]::Abs($expectedE[$sampleIndex]-$distanceMap.ExposedDistances[$flat])-le1e-12) `
                        "Independent/production E sample distance differs at ($x,$y) sample $sampleIndex."
                    Assert-True ([Math]::Abs($expectedC[$sampleIndex]-$distanceMap.ContinuationDistances[$flat])-le1e-12) `
                        "Independent/production C sample distance differs at ($x,$y) sample $sampleIndex."
                }
            }
        }
        $expectedCoverage=Get-IndependentFCoverage `
            ([double[]](Get-IndependentKindSampleDistances 2 12 $coverageContour 'E')) `
            ([double[]](Get-IndependentKindSampleDistances 2 12 $coverageContour 'C')) 0.16
        Assert-Equal 0.625 $expectedCoverage 'Independent E-only coverage fixture changed.'
        Assert-Equal $expectedCoverage (Get-DororongOutlineCoverage $distanceMap 2 12 0.16) `
            'Production coverage differs from independent clamp(E*2.5).'

        $sideCoverageContour=[pscustomobject]@{Segments=@(
            (New-Segment 'E' 1.5 11.5 1.5 12.5),
            (New-Segment 'C' 12.0 11.5 12.0 12.5))}
        $sideCoverageMap=New-DororongSubpixelDistanceMap $finalMask $sideCoverageContour 8
        Assert-Equal 0.625 (Get-DororongOutlineCoverage $sideCoverageMap 2 12 0.25) `
            'Exposed E support or fixed 2.5 optical multiplier changed.'
        Assert-Equal 0.0 (Get-DororongOutlineCoverage $sideCoverageMap 12 12 0.25) `
            'Internal continuation geometry rendered nonzero coverage.'

        $halfRadiusExposed=Get-IndependentCoverage ([double[]](
            Get-IndependentSampleDistances 2 12 $sideCoverageContour `
                -ExposedMultiplier 2.0)) 0.25
        $failure=Assert-ThrowsLike {
            Assert-Equal 0.25 $halfRadiusExposed `
                'Exposed half-radius mutation lost the required inward full thickness W.'
        } 'Exposed half-radius mutation lost the required inward full thickness W' `
            'E multiplier 2 mutation'
        Write-Output "MUTATION PASS label=exposed-half-radius-W-over-2 failure=$failure"

        $eRaw=Get-IndependentCoverage `
            ([double[]](Get-IndependentKindSampleDistances 2 12 $sideCoverageContour 'E')) 0.25
        $eGainOne=[Math]::Clamp($eRaw*1.0,0.0,1.0)
        Assert-True ($eGainOne-ne0.625) 'E multiplier 2.5 to 1.0 mutation survived.'
        Write-Output 'MUTATION PASS label=e-multiplier-2.5-to-1.0 failure=E-only-coverage'
        $cRaw=Get-IndependentCoverage `
            ([double[]](Get-IndependentKindSampleDistances 12 12 $sideCoverageContour 'C')) 0.125
        $cGainOne=[Math]::Clamp($cRaw*1.0,0.0,1.0)
        Assert-True ($cGainOne-gt0.0) 'C multiplier 0.0 to 1.0 mutation fixture produced no coverage.'
        Write-Output 'MUTATION PASS label=c-multiplier-0-to-1 failure=E-only-coverage'

        $retainedSeed=$fillField[10,12]
        $expectedSeed=Get-IndependentInterpolatedColor 10 12 $independentSeeds
        Assert-ColorEqual $expectedSeed $retainedSeed 'Eligible sample coordinate did not use smooth fill.'
        Assert-True ($source.GetPixel(10,12).ToArgb()-ne$expectedSeed.ToArgb()) `
            'Retain-seed mutation fixture does not distinguish source RGB from smooth output.'
        Write-Output 'MUTATION PASS label=retain-seed-output failure=eligible-sample-smooth-RGB'

        $factorFour=Get-IndependentCoverage `
            ([double[]](Get-IndependentKindSampleDistances 2 12 $coverageContour 'E' 4)) 0.16
        Assert-True ($factorFour-ne$expectedCoverage) 'Factor-8-to-4 mutation survived.'
        Write-Output 'MUTATION PASS label=factor-8-to-4 failure=coverage'
        $shiftedOrigin=Get-IndependentCoverage `
            ([double[]](Get-IndependentKindSampleDistances 2 12 $coverageContour 'E' 8 0.5)) 0.16
        Assert-True ($shiftedOrigin-ne$expectedCoverage) 'Sample-origin-plus-0.5 mutation survived.'
        Write-Output 'MUTATION PASS label=sample-origin-plus-0.5 failure=coverage'
        $exposedOnly=Get-IndependentCoverage `
            ([double[]](Get-IndependentSampleDistances 2 12 $coverageContour 8 0.0 -ExposedOnly)) 0.16
        Assert-Equal $expectedCoverage ([Math]::Clamp($exposedOnly*2.5,0.0,1.0)) `
            'E-only distance coverage changed when continuation geometry was omitted.'

        $admitted=@(Get-IndependentFillSeeds $source $seedMask $finalMask $contour 225 8.0 $false)
        Assert-Equal 10 $admitted.Count 'Newly-owned-seed mutation did not admit exactly one pixel.'
        Write-Output 'MUTATION PASS label=newly-owned-fill-seed-admitted failure=eligible-seed-count'
        $floor224=@(Get-IndependentFillSeeds $source $seedMask $finalMask $contour 224 8.0 $true)
        Assert-Equal 10 $floor224.Count 'Fill-floor-224 mutation did not admit exactly one pixel.'
        Write-Output 'MUTATION PASS label=fill-floor-225-to-224 failure=eligible-seed-count'
        $distanceSeven=@(Get-IndependentFillSeeds $source $seedMask $finalMask $contour 225 7.0 $true)
        Assert-Equal 10 $distanceSeven.Count 'Fill-distance-7 mutation did not admit exactly one pixel.'
        Write-Output 'MUTATION PASS label=fill-distance-8-to-7 failure=eligible-seed-count'

        $outline=[Drawing.Color]::FromArgb(255,12,23,34)
        $candidate=Invoke-DororongSubpixelOutline $source $finalMask $fillField $distanceMap $outline 0.16
        try
        {
            Assert-IndependentWritableBlend `
                $source $finalMask $fillField $coverageContour $outline 0.16 $candidate
            $corruptedBlend=$candidate.Clone()
            try
            {
                $pixel=$corruptedBlend.GetPixel(12,12)
                $corruptedBlend.SetPixel(12,12,[Drawing.Color]::FromArgb(
                    $pixel.A,($pixel.R+1),$pixel.G,$pixel.B))
                $failure=Assert-ThrowsLike {
                    Assert-IndependentWritableBlend `
                        $source $finalMask $fillField $coverageContour $outline 0.16 $corruptedBlend
                } 'Writable blend RGB changed at \(12,12\)' 'Writable no-blend/wrong-blend mutation'
                Write-Output "MUTATION PASS label=writable-no-blend-or-wrong-blend failure=$failure"
            }
            finally{$corruptedBlend.Dispose()}

            $tieFill=[Drawing.Color[,]]::new(25,25)
            for($tieY=1;$tieY-le23;$tieY++)
            {for($tieX=1;$tieX-le23;$tieX++)
                {$tieFill[$tieX,$tieY]=[Drawing.Color]::FromArgb(255,10,20,30)}}
            $tieCoverage=Get-IndependentFCoverage `
                ([double[]](Get-IndependentKindSampleDistances 2 12 $sideCoverageContour 'E')) `
                ([double[]](Get-IndependentKindSampleDistances 2 12 $sideCoverageContour 'C')) 0.25
            Assert-Equal 0.625 $tieCoverage 'Exact-half E-only blend coverage fixture changed.'
            $tieOutline=[Drawing.Color]::FromArgb(255,14,24,34)
            $tieCandidate=Invoke-DororongSubpixelOutline `
                $source $finalMask $tieFill $sideCoverageMap $tieOutline 0.25
            try
            {
                $toEvenLiteral=[Drawing.Color]::FromArgb(255,12,22,32)
                Assert-ColorEqual $toEvenLiteral $tieCandidate.GetPixel(2,12) `
                    'Exact-half writable blend did not round ties to even.'
                $awayRounded=$tieCandidate.Clone()
                try
                {
                    $awayRounded.SetPixel(2,12,[Drawing.Color]::FromArgb(255,13,23,33))
                    $failure=Assert-ThrowsLike {
                        Assert-ColorEqual $toEvenLiteral $awayRounded.GetPixel(2,12) `
                            'Exact-half writable blend changed from ToEven to AwayFromZero.'
                    } 'Exact-half writable blend changed from ToEven to AwayFromZero' `
                        'Away-from-zero blend-rounding mutation'
                    Write-Output "MUTATION PASS label=blend-away-from-zero failure=$failure"
                }
                finally{$awayRounded.Dispose()}
            }
            finally{$tieCandidate.Dispose()}
            Assert-RasterPreservesAuthority $source $finalMask $candidate
            $protectedMutation=$candidate.Clone()
            try
            {
                $protectedMutation.SetPixel(24,12,[Drawing.Color]::FromArgb(255,21,30,40))
                $failure=Assert-ThrowsLike `
                    {Assert-RasterPreservesAuthority $source $finalMask $protectedMutation} `
                    'Protected mask-zero RGB changed' 'Protected RGB mutation'
                Write-Output "MUTATION PASS label=protected-rgb-change failure=$failure"
            }
            finally{$protectedMutation.Dispose()}
            $alphaMutation=$candidate.Clone()
            try
            {
                $pixel=$alphaMutation.GetPixel(12,12)
                $alphaMutation.SetPixel(12,12,[Drawing.Color]::FromArgb(254,$pixel.R,$pixel.G,$pixel.B))
                $failure=Assert-ThrowsLike `
                    {Assert-RasterPreservesAuthority $source $finalMask $alphaMutation} `
                    'Source alpha changed' 'Source alpha mutation'
                Write-Output "MUTATION PASS label=source-alpha-change failure=$failure"
            }
            finally{$alphaMutation.Dispose()}
        }
        finally{$candidate.Dispose()}
        Write-Output 'SYNTHETIC RASTER PASS samples=64 coordinates=x-0.5+(i+0.5)/8 supports=E-W transfer=clamp(E*2.5) C=geometry-only-zero-render maskZero=byte-identical alpha=preserved'
    }
    finally{$finalMask.Dispose();$seedMask.Dispose();$source.Dispose()}
}

function Get-FillFieldHash([Drawing.Color[,]]$Colors,[Drawing.Bitmap]$Mask)
{
    $bytes=[Collections.Generic.List[byte]]::new()
    for($y=0;$y-lt$Mask.Height;$y++)
    {
        for($x=0;$x-lt$Mask.Width;$x++)
        {
            if($Mask.GetPixel($x,$y).R-ne255){continue}
            $color=$Colors[$x,$y]
            $bytes.Add([byte]$color.R);$bytes.Add([byte]$color.G);$bytes.Add([byte]$color.B)
        }
    }
    return Get-BytesSha256 $bytes.ToArray()
}

function Invoke-TerminalSourceGate(
    [string]$SourcePath,[string]$SeedPath,[string]$MaskPath,
    [string]$AuthorityPath,[string]$SourceRasterModulePath,
    [string]$OutlineModulePath,[string]$ConstantsPath)
{
    $expectedHashes=[ordered]@{
        Source='F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504'
        Seed='E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779'
        Mask='D08B3A941C662F1CBC55C486C13FD4C6CD8901DA9CD5CF8512509698219FE46F'
        Authority='DDF749007995B3F03781A3A51467013F406C5F7A2AA0480212523A79EF31F17F'
    }
    $paths=[ordered]@{Source=$SourcePath;Seed=$SeedPath;Mask=$MaskPath;Authority=$AuthorityPath}
    $preHashes=[ordered]@{}
    foreach($name in $paths.Keys)
    {
        $preHashes[$name]=(Get-FileHash -LiteralPath $paths[$name] -Algorithm SHA256).Hash
        Assert-Equal $expectedHashes[$name] $preHashes[$name] "Frozen $name changed before the gate."
    }
    $executingTestPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Dororong.App.SubpixelOutline.Tests.ps1'))
    $moduleHash=(Get-FileHash -LiteralPath $OutlineModulePath -Algorithm SHA256).Hash
    $executingTestHash=(Get-FileHash -LiteralPath $executingTestPath -Algorithm SHA256).Hash
    $constantsHash=(Get-FileHash -LiteralPath $ConstantsPath -Algorithm SHA256).Hash
    Write-Output "FIXED CANDIDATE TREE modulePath=$([IO.Path]::GetFullPath($OutlineModulePath)) moduleHash=$moduleHash testPath=$executingTestPath testHash=$executingTestHash constantsPath=$([IO.Path]::GetFullPath($ConstantsPath)) constantsHash=$constantsHash"
    $constants=Import-PowerShellDataFile -LiteralPath $ConstantsPath
    Assert-Equal 8 $constants.SubpixelFactor 'Terminal subpixel factor changed.'
    Assert-Equal 8.0 $constants.FillDistance 'Terminal fill distance changed.'
    Assert-Equal 225 $constants.FillFloor 'Terminal fill floor changed.'
    Assert-Equal 8 $constants.MaximumChroma 'Terminal maximum chroma changed.'
    Assert-Equal 8 $constants.FillNeighborCount 'Terminal fill neighbor count changed.'
    Assert-Equal 0.25 $constants.WidthSweepMinimum 'Terminal width minimum changed.'
    Assert-Equal 4.00 $constants.WidthSweepMaximum 'Terminal width maximum changed.'
    Assert-Equal 0.015625 $constants.WidthSweepStep 'Terminal width step changed.'
    Assert-Equal 1.5 ([double]$constants.Width) 'Fixed visible Width changed.'
    Assert-Equal 0.75 ([double]$constants.Width/2.0) 'Fixed continuation geometry radius W/2 changed.'
    Assert-Equal 2.5 ([double]$constants.ExposedCoverageMultiplier) 'Fixed E optical multiplier changed.'
    Assert-Equal 0.0 ([double]$constants.ContinuationCoverageMultiplier) 'Fixed C optical multiplier changed.'
    Assert-Equal 7 ([int]$constants.ProxyMaximumSize) 'Resize-proxy size limit changed.'
    Assert-Equal 8 ([int]$constants.ProxyMaximumChroma) 'Resize-proxy chroma limit changed.'
    Assert-Equal 6 ([int]$constants.ExpectedProxyComponentCount) 'Resize-proxy component count changed.'
    Assert-Equal 12 ([int]$constants.ExpectedProxyPixelCount) 'Resize-proxy pixel count changed.'
    Assert-Equal '2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54' `
        $constants.ExpectedProxyMembershipSha256 'Resize-proxy membership identity changed.'
    $authority=Import-PowerShellDataFile -LiteralPath $AuthorityPath
    Assert-Equal 6 @($authority.HairAnchors).Count 'Terminal hair anchor count changed.'
    Assert-Equal 15 @($authority.BodyNormals).Count 'Terminal body normal count changed.'

    Import-Module $SourceRasterModulePath -Force
    Import-Module $OutlineModulePath -Force
    . (Join-Path (Split-Path -Parent $AuthorityPath) '..\support\Dororong.ContinuousOptics.ps1')
    $rawSource=[Drawing.Bitmap]::new($SourcePath);$processedSource=$null
    $seed=[Drawing.Bitmap]::new($SeedPath);$mask=[Drawing.Bitmap]::new($MaskPath)
    try
    {
        $processedSource=Remove-DororongBoundaryBackground $rawSource
        $endpoints=[Drawing.PointF[]]@($constants.LegalEndpoints|ForEach-Object{
            [Drawing.PointF]::new([single]$_.X,[single]$_.Y)})
        $contour=New-DororongVisibleContour $processedSource $mask $endpoints
        $contourHash=Get-DororongCanonicalContourHash $contour
        Assert-Equal 'A29D007B699A16B555FE5133854E832FEFD8409EA85F2FECE3EEA97BF444FD65' `
            $contourHash 'Terminal contour changed.'
        Assert-Equal 382 $contour.ExposedSegmentCount 'Fixed candidate exposed count changed.'
        Assert-Equal 2 $contour.ContinuationSegmentCount 'Fixed candidate continuation count changed.'
        Assert-Equal 384 @($contour.Segments).Count 'Fixed candidate contour record count changed.'
        $fillField=New-DororongFillField $processedSource $seed $mask $contour
        $fillHash=Get-FillFieldHash $fillField $mask
        $distanceMap=New-DororongSubpixelDistanceMap $mask $contour ([int]$constants.SubpixelFactor)
        $outlineColor=Get-ComponentMedianColor $processedSource @($constants.OutlineSamples)
        Assert-Equal 26 $outlineColor.R 'Frozen median outline red changed.'
        Assert-Equal 2 $outlineColor.G 'Frozen median outline green changed.'
        Assert-Equal 10 $outlineColor.B 'Frozen median outline blue changed.'

        $candidate=Invoke-DororongSubpixelOutline `
            $processedSource $mask $fillField $distanceMap $outlineColor ([double]$constants.Width)
        $proxyCandidate=$null;$nativeCandidate=$null
        try
        {
            $sourceCandidateHash=Get-BytesSha256 (Get-BitmapPngBytes $candidate)
            Assert-Equal 'D1F0770CBCA95FC79B5E68642D78A5A48077834495C9ECDBCD73B34545AC94FF' `
                $sourceCandidateHash 'Fixed source225 candidate PNG changed.'
            $proxyComponents=@(Get-IndependentResizeProxyComponents `
                $mask $processedSource ([int]$constants.ProxyMaximumSize) ([int]$constants.ProxyMaximumChroma))
            $proxyPixels=(@($proxyComponents|ForEach-Object Size)|Measure-Object -Sum).Sum
            Assert-Equal 6 $proxyComponents.Count 'Independent resize-proxy component count changed.'
            Assert-Equal 12 $proxyPixels 'Independent resize-proxy pixel count changed.'
            $membership=Get-IndependentResizeProxyMembership $proxyComponents $mask.Width
            Assert-Equal '2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54' `
                $membership.Hash 'Independent resize-proxy membership changed.'
            $grayIsland=@($proxyComponents|Where-Object{
                $_.Size-eq7-and$_.MinX-eq144-and$_.MinY-eq159-and$_.MaxX-eq147-and$_.MaxY-eq160})
            Assert-Equal 1 $grayIsland.Count 'Diagnosed seven-pixel gray resize-proxy island changed.'
            $proxyCandidate=New-IndependentResizeProxy $candidate $fillField $proxyComponents
            $nativeCandidate=Resize-DororongPremultiplied96 $proxyCandidate
            $nativeCandidateHash=Get-BytesSha256 (Get-BitmapPngBytes $nativeCandidate)
            Assert-Equal '238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511' `
                $nativeCandidateHash 'Fixed native96 candidate PNG changed.'
            $endpointText=@($constants.LegalEndpoints|ForEach-Object{"$($_.X),$($_.Y)"})-join '|'
            $widthText=([double]$constants.Width).ToString('R',[Globalization.CultureInfo]::InvariantCulture)
            $halfWidthText=([double]$constants.Width/2.0).ToString('R',[Globalization.CultureInfo]::InvariantCulture)
            Write-Output "FIXED CANDIDATE INPUT moduleHash=$moduleHash testHash=$executingTestHash constantsHash=$constantsHash sourceHash=$($preHashes.Source) seedHash=$($preHashes.Seed) maskHash=$($preHashes.Mask) authorityHash=$($preHashes.Authority)"
            Write-Output "FIXED CANDIDATE GEOMETRY width=$widthText halfWidth=$halfWidthText eMultiplier=$($constants.ExposedCoverageMultiplier) cMultiplier=$($constants.ContinuationCoverageMultiplier) outlineRgb=$($outlineColor.R),$($outlineColor.G),$($outlineColor.B) factor=$($constants.SubpixelFactor) contourRecords=$(@($contour.Segments).Count) exposed=$($contour.ExposedSegmentCount) continuations=$($contour.ContinuationSegmentCount) contourHash=$contourHash endpoints=$endpointText eligibleFillSeeds=$($fillField.EligibleSeedCount) fillHash=$fillHash proxyComponents=$($proxyComponents.Count) proxyPixels=$proxyPixels proxyHash=$($membership.Hash)"
            Write-Output "FIXED CANDIDATE PASS source225Hash=$sourceCandidateHash native96Hash=$nativeCandidateHash generatedSourceCandidates=1 nativeResizes=1"
        }
        finally
        {
            if($null-ne$nativeCandidate){$nativeCandidate.Dispose()}
            if($null-ne$proxyCandidate){$proxyCandidate.Dispose()}
            $candidate.Dispose()
        }

        foreach($history in @(
            'FrontOuter min=2.0625 max=2.421875 count=24',
            'FrontFoot min=1.8125 max=2.125 count=21',
            'FrontInner min=1.8125 max=2.125 count=21',
            'FirstValley min=1.8125 max=2.125 count=21',
            'FirstUnderside min=1.8125 max=2.125 count=21',
            'CenterOuter min=1.203125 max=1.609375 count=27',
            'CenterFoot min=1.65625 max=1.953125 count=20',
            'CenterInner min=2.0625 max=2.546875 count=32',
            'SecondValley min=2.0625 max=2.546875 count=32',
            'SecondUnderside min=1.796875 max=2.125 count=22',
            'RearOuter min=1.8125 max=2.125 count=21',
            'RearFoot min=1.8125 max=2.125 count=21',
            'RearInner min=1.8125 max=2.125 count=21',
            'UpperRearRim min=1.9375 max=2.421875 count=32',
            'LowerRearRim min=2.0625 max=2.421875 count=24'))
        {Write-Output "HISTORICAL INTERVAL diagnosticOnly=true name=$history"}
        Write-Output 'HISTORICAL TERMINAL FEASIBILITY diagnosticOnly=true result=EMPTY widths=241 range=0.25..4 step=0.015625'
    }
    finally
    {
        if($null-ne$processedSource){$processedSource.Dispose()}
        $mask.Dispose();$seed.Dispose();$rawSource.Dispose()
        foreach($name in $paths.Keys)
        {
            $post=(Get-FileHash -LiteralPath $paths[$name] -Algorithm SHA256).Hash
            Assert-Equal $preHashes[$name] $post "Frozen $name changed during the gate."
            Write-Output "PROTECTED HASH name=$name pre=$($preHashes[$name]) post=$post"
        }
    }
}

$repositoryRoot=Split-Path -Parent $PSScriptRoot
$sourcePath=Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical-source.png'
$seedPath=Join-Path $repositoryRoot 'tests/fixtures/dororong-body-region-seed.png'
$maskPath=Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-body-region-mask.png'
$authorityPath=Join-Path $repositoryRoot 'tests/fixtures/dororong-body-outline-authority.psd1'
$sourceRasterModulePath=Join-Path $repositoryRoot 'tools/Dororong.SourceRaster.psm1'
$outlineModulePath=Join-Path $repositoryRoot 'tools/Dororong.SubpixelOutline.psm1'
$constantsPath=Join-Path $repositoryRoot 'tools/Dororong.SubpixelOutline.Constants.psd1'
$generatorPath=Join-Path $repositoryRoot 'tools/Generate-CanonicalArt.ps1'
$continuousOpticsPath=Join-Path $repositoryRoot 'tests/support/Dororong.ContinuousOptics.ps1'
$evidenceDirectory=Join-Path $repositoryRoot '.superpowers/sdd/2026-08-27-dororong-complete-body-ownership-outline/contour-evidence'
$legalEndpoints=[Drawing.PointF[]]@([Drawing.PointF]::new(118,151),[Drawing.PointF]::new(161,116))
$expectedModuleExports=@(
    'Import-DororongBodyMask','New-DororongVisibleContour',
    'Get-DororongCanonicalContourRecords','Get-DororongCanonicalContourHash',
    'Get-DororongResizeProxyComponents','Get-DororongResizeProxyMembership',
    'New-DororongResizeProxy','New-DororongFillField','New-DororongSubpixelDistanceMap',
    'Get-DororongOutlineCoverage','Invoke-DororongSubpixelOutline')

if($ThinOutlineOnly)
{
    Add-Type -AssemblyName System.Drawing
    Invoke-ThinOutlineDecisionContract $repositoryRoot $sourcePath $seedPath $maskPath `
        $authorityPath $sourceRasterModulePath $outlineModulePath $constantsPath `
        $generatorPath $continuousOpticsPath
    return
}

if($ReloadOnly)
{
    Add-Type -AssemblyName System.Drawing
    Import-Module $outlineModulePath -Force
    $firstExports=@(Get-Command -Module Dororong.SubpixelOutline|ForEach-Object Name|Sort-Object)
    Assert-Equal (($expectedModuleExports|Sort-Object)-join '|') ($firstExports-join '|') `
        'Initial reload-check module export identity changed.'
    Assert-True ($null-ne('DororongSubpixelKernelV2' -as [type])) `
        'Initial import did not publish the versioned CLR kernel V2.'

    Import-Module $outlineModulePath -Force
    $secondExports=@(Get-Command -Module Dororong.SubpixelOutline|ForEach-Object Name|Sort-Object)
    Assert-Equal (($expectedModuleExports|Sort-Object)-join '|') ($secondExports-join '|') `
        'Forced reimport module export identity changed.'
    Assert-True ($null-ne('DororongSubpixelKernelV2' -as [type])) `
        'Forced reimport lost the versioned CLR kernel V2.'
    Invoke-SyntheticFillAndRasterContract
    Write-Output "RELOAD PASS imports=2 kernel=DororongSubpixelKernelV2 exports=$($secondExports.Count) behavior=synthetic-fill-raster"
    return
}

if(-not $GeometryOnly)
{
    Import-Module $outlineModulePath -Force
    $requiredRasterCommands=@(
        'New-DororongFillField',
        'New-DororongSubpixelDistanceMap',
        'Get-DororongOutlineCoverage',
        'Invoke-DororongSubpixelOutline')
    foreach($command in $requiredRasterCommands)
    {
        Assert-True ($null -ne (Get-Command $command -ErrorAction SilentlyContinue)) `
            "Missing fill or raster interface '$command'."
    }
    Invoke-SyntheticFillAndRasterContract
    if($SyntheticOnly){return}
    Invoke-TerminalSourceGate $sourcePath $seedPath $maskPath $authorityPath `
        $sourceRasterModulePath $outlineModulePath $constantsPath
    return
}

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
    $requiredCommands=$expectedModuleExports
    foreach($command in $requiredCommands)
    {
        Assert-True ($null -ne (Get-Command $command -ErrorAction SilentlyContinue)) `
            "Missing canonical contour interface '$command'."
    }
    $exportedCommands=@(Get-Command -Module Dororong.SubpixelOutline|ForEach-Object Name|Sort-Object)
    Assert-Equal (($requiredCommands|Sort-Object)-join '|') ($exportedCommands-join '|') `
        'Geometry/raster module export identity changed.'

    $constants=Import-PowerShellDataFile -LiteralPath $constantsPath
    $expectedKeys=@('SubpixelFactor','FillDistance','FillFloor','MaximumChroma','FillNeighborCount','Width',
        'WidthSweepMinimum','WidthSweepMaximum','WidthSweepStep','LegalEndpoints','OutlineSamples',
        'ExposedCoverageMultiplier','ContinuationCoverageMultiplier','ProxyMaximumSize',
        'ProxyMaximumChroma','ExpectedProxyComponentCount','ExpectedProxyPixelCount',
        'ExpectedProxyMembershipSha256')|Sort-Object
    Assert-Equal ($expectedKeys-join '|') (@($constants.Keys|Sort-Object)-join '|') `
        'Geometry constants contain missing or extra fields.'
    Assert-Equal 1.5 ([double]$constants.Width) 'Fixed visible Width changed.'
    Assert-Equal 8 $constants.SubpixelFactor 'Subpixel factor changed.'
    Assert-Equal 8.0 $constants.FillDistance 'Fill distance changed.'
    Assert-Equal 225 $constants.FillFloor 'Fill floor changed.'
    Assert-Equal 8 $constants.MaximumChroma 'Maximum chroma changed.'
    Assert-Equal 8 $constants.FillNeighborCount 'Fill neighbor count changed.'
    Assert-Equal 0.25 $constants.WidthSweepMinimum 'Width sweep minimum changed.'
    Assert-Equal 4.00 $constants.WidthSweepMaximum 'Width sweep maximum changed.'
    Assert-Equal 0.015625 $constants.WidthSweepStep 'Width sweep step changed.'
    Assert-Equal 2.5 ([double]$constants.ExposedCoverageMultiplier) 'E optical multiplier changed.'
    Assert-Equal 0.0 ([double]$constants.ContinuationCoverageMultiplier) 'C optical multiplier changed.'
    Assert-Equal 7 ([int]$constants.ProxyMaximumSize) 'Proxy maximum size changed.'
    Assert-Equal 8 ([int]$constants.ProxyMaximumChroma) 'Proxy maximum chroma changed.'
    Assert-Equal 6 ([int]$constants.ExpectedProxyComponentCount) 'Expected proxy component count changed.'
    Assert-Equal 12 ([int]$constants.ExpectedProxyPixelCount) 'Expected proxy pixel count changed.'
    Assert-Equal '2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54' `
        $constants.ExpectedProxyMembershipSha256 'Expected proxy membership changed.'
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
