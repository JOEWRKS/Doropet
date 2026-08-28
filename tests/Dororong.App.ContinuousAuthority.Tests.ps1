param(
    [string]$Configuration = 'Debug',
    [switch]$BodyNativeReferenceOnly)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -ne $Actual)
    { throw "$Message Expected '$Expected', observed '$Actual'." }
}

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition)
    { throw $Message }
}

function Assert-Near([double]$Expected, [double]$Actual, [double]$Tolerance, [string]$Message)
{
    if ([Math]::Abs($Expected - $Actual) -gt $Tolerance)
    { throw "$Message Expected '$Expected' +/- '$Tolerance', observed '$Actual'." }
}

function Assert-Point([object]$Point, [string]$Label)
{
    Assert-True ($null -ne $Point) "$Label is missing."
    $coordinates = @($Point)
    Assert-Equal 2 $coordinates.Count "$Label is not an X/Y pair."
    Assert-True ($coordinates[0] -is [int] -and $coordinates[1] -is [int]) `
        "$Label contains a non-integer coordinate."
}

function Assert-ReferencePoint(
    [hashtable]$Entry,
    [string]$Field,
    [Drawing.Bitmap]$Bitmap,
    [string]$Label)
{
    if (-not $Entry.ContainsKey($Field))
    { throw "$Label is missing $Field." }

    $point = $Entry[$Field]
    Assert-Point $point "$Label $Field"
    $x = [int]$point[0]
    $y = [int]$point[1]
    Assert-True ($x -ge 0 -and $x -lt $Bitmap.Width -and $y -ge 0 -and $y -lt $Bitmap.Height) `
        "$Label $Field coordinate ($x,$y) is outside the reference bitmap."
    Assert-Equal 255 $Bitmap.GetPixel($x,$y).A "$Label $Field coordinate ($x,$y) is not opaque."
}

function Assert-Normal(
    [hashtable]$Entry,
    [string]$Field,
    [Drawing.Bitmap]$Bitmap,
    [string]$Label)
{
    if (-not $Entry.ContainsKey($Field))
    { throw "$Label is missing $Field." }

    $normal = $Entry[$Field]
    Assert-True ($normal -is [hashtable]) "$Label $Field is not a literal hashtable."
    foreach ($coordinateName in @('X1Eighth','Y1Eighth','X2Eighth','Y2Eighth'))
    {
        Assert-True $normal.ContainsKey($coordinateName) "$Label $Field is missing $coordinateName."
        Assert-True ($normal[$coordinateName] -is [int]) `
            "$Label $Field $coordinateName is not an integer."
    }

    Assert-True (
        $normal.X1Eighth -ne $normal.X2Eighth -or $normal.Y1Eighth -ne $normal.Y2Eighth) `
        "$Label $Field has duplicate endpoints."

    $endpoints = @(
        [pscustomobject]@{ X=$normal.X1Eighth/8.0; Y=$normal.Y1Eighth/8.0; Name='first' }
        [pscustomobject]@{ X=$normal.X2Eighth/8.0; Y=$normal.Y2Eighth/8.0; Name='second' })
    foreach ($endpoint in $endpoints)
    {
        Assert-True (
            $endpoint.X -ge 0.0 -and $endpoint.X -lt $Bitmap.Width -and `
            $endpoint.Y -ge 0.0 -and $endpoint.Y -lt $Bitmap.Height) `
            "$Label $Field $($endpoint.Name) endpoint ($($endpoint.X),$($endpoint.Y)) is out of bounds."
    }
}

function Assert-PointEqual([object]$Expected, [object]$Actual, [string]$Message)
{
    Assert-Point $Expected "$Message expected point"
    Assert-Point $Actual "$Message actual point"
    Assert-Equal ([int]$Expected[0]) ([int]$Actual[0]) "$Message X changed."
    Assert-Equal ([int]$Expected[1]) ([int]$Actual[1]) "$Message Y changed."
}

function Get-Rec709Luminance([Drawing.Color]$Color)
{
    return (0.2126*$Color.R)+(0.7152*$Color.G)+(0.0722*$Color.B)
}

function Get-SelectedOpaquePoint(
    [Drawing.Bitmap]$Bitmap,
    [object]$Points,
    [bool]$Brightest,
    [string]$Label)
{
    $opaque = @()
    foreach ($point in @($Points))
    {
        $pixel = $Bitmap.GetPixel([int]$point[0],[int]$point[1])
        if ($pixel.A -ne 255)
        { continue }
        $opaque += [pscustomobject]@{
            X = [int]$point[0]
            Y = [int]$point[1]
            Luminance = Get-Rec709Luminance $pixel
        }
    }
    Assert-True ($opaque.Count -gt 0) "$Label has no opaque legacy native sample."
    if ($Brightest)
    { $selected = $opaque | Sort-Object @{Expression='Luminance';Descending=$true},Y,X | Select-Object -First 1 }
    else
    { $selected = $opaque | Sort-Object @{Expression='Luminance';Descending=$false},Y,X | Select-Object -First 1 }
    return @([int]$selected.X,[int]$selected.Y)
}

function Get-ContinuousProfile(
    [Drawing.Bitmap]$Bitmap,
    [hashtable]$Normal,
    [Drawing.Color]$Fill,
    [Drawing.Color]$Ink,
    [double]$Spacing = 0.125)
{
    $x1=$Normal.X1Eighth/8.0; $y1=$Normal.Y1Eighth/8.0
    $x2=$Normal.X2Eighth/8.0; $y2=$Normal.Y2Eighth/8.0
    $length=[Math]::Sqrt(($x2-$x1)*($x2-$x1)+($y2-$y1)*($y2-$y1))
    Assert-True ($length -gt 0.0) 'Cannot profile a zero-length continuous normal.'
    $fillL=Get-Rec709Luminance $Fill
    $inkL=Get-Rec709Luminance $Ink
    Assert-True ($fillL -gt $inkL) 'Cannot profile a normal whose fill is not brighter than its ink.'

    $positions=[Collections.Generic.List[double]]::new()
    for ($distance=0.0; $distance -lt $length; $distance += $Spacing)
    { $positions.Add($distance) }
    $positions.Add($length)
    $profile = @()
    foreach ($distance in $positions)
    {
        $t=$distance/$length
        $sample=Get-DororongBilinearPremultipliedSample $Bitmap `
            ($x1+(($x2-$x1)*$t)) ($y1+(($y2-$y1)*$t))
        $luminance=255.0*((0.2126*$sample.R)+(0.7152*$sample.G)+(0.0722*$sample.B))
        $darkness=[Math]::Clamp(($fillL-$luminance)/($fillL-$inkL),0.0,1.0)
        $profile += [pscustomobject]@{
            Distance = $distance
            Alpha = $sample.A
            Luminance = $luminance
            Darkness = $sample.A*$darkness
        }
    }
    return $profile
}

function Get-DarknessRunCount([object]$Profile, [double]$Threshold)
{
    $runCount = 0
    $insideRun = $false
    foreach ($sample in @($Profile))
    {
        if ($sample.Darkness -ge $Threshold)
        {
            if (-not $insideRun)
            { $runCount++ }
            $insideRun = $true
        }
        else
        { $insideRun = $false }
    }
    return $runCount
}

function Test-NormalIntersectsMask(
    [Drawing.Bitmap]$Mask,
    [hashtable]$Normal,
    [double]$Spacing = 0.125)
{
    $x1=$Normal.X1Eighth/8.0; $y1=$Normal.Y1Eighth/8.0
    $x2=$Normal.X2Eighth/8.0; $y2=$Normal.Y2Eighth/8.0
    $length=[Math]::Sqrt(($x2-$x1)*($x2-$x1)+($y2-$y1)*($y2-$y1))
    for ($distance=0.0; $distance -lt $length; $distance += $Spacing)
    {
        $t=$distance/$length
        $sample=Get-DororongBilinearPremultipliedSample $Mask `
            ($x1+(($x2-$x1)*$t)) ($y1+(($y2-$y1)*$t))
        if ($sample.R -gt 0.0)
        { return $true }
    }
    $last=Get-DororongBilinearPremultipliedSample $Mask $x2 $y2
    return $last.R -gt 0.0
}

function Test-NormalIntersectsAlpha(
    [Drawing.Bitmap]$Bitmap,
    [hashtable]$Normal,
    [double]$Spacing = 0.125)
{
    $x1=$Normal.X1Eighth/8.0; $y1=$Normal.Y1Eighth/8.0
    $x2=$Normal.X2Eighth/8.0; $y2=$Normal.Y2Eighth/8.0
    $length=[Math]::Sqrt(($x2-$x1)*($x2-$x1)+($y2-$y1)*($y2-$y1))
    for ($distance=0.0; $distance -lt $length; $distance += $Spacing)
    {
        $t=$distance/$length
        $sample=Get-DororongBilinearPremultipliedSample $Bitmap `
            ($x1+(($x2-$x1)*$t)) ($y1+(($y2-$y1)*$t))
        if ($sample.A -gt 0.0)
        { return $true }
    }
    $last=Get-DororongBilinearPremultipliedSample $Bitmap $x2 $y2
    return $last.A -gt 0.0
}

function Test-DororongNeutralContourPixel([Drawing.Color]$Color)
{
    $minimum=[Math]::Min($Color.R,[Math]::Min($Color.G,$Color.B))
    $maximum=[Math]::Max($Color.R,[Math]::Max($Color.G,$Color.B))
    return $Color.A -gt 0 -and ($maximum-$minimum) -le 8
}

function Get-DororongFinalSourceContour(
    [Drawing.Bitmap]$Source,
    [Drawing.Bitmap]$Mask,
    [object]$LegalEndpoints)
{
    Assert-Equal $Source.Width $Mask.Width `
        'Source/mask width differs during independent contour derivation.'
    Assert-Equal $Source.Height $Mask.Height `
        'Source/mask height differs during independent contour derivation.'

    $exteriorNearWhite=New-Object 'bool[,]' $Source.Width,$Source.Height
    $queue=[Collections.Generic.Queue[object]]::new()
    for($x=0;$x -lt $Source.Width;$x++)
    {
        $queue.Enqueue(@($x,0))
        $queue.Enqueue(@($x,($Source.Height-1)))
    }
    for($y=1;$y -lt ($Source.Height-1);$y++)
    {
        $queue.Enqueue(@(0,$y))
        $queue.Enqueue(@(($Source.Width-1),$y))
    }
    while($queue.Count -gt 0)
    {
        $point=$queue.Dequeue()
        $x=[int]$point[0]; $y=[int]$point[1]
        if($exteriorNearWhite[$x,$y])
        { continue }
        $color=$Source.GetPixel($x,$y)
        $minimum=[Math]::Min($color.R,[Math]::Min($color.G,$color.B))
        if($minimum -lt 225)
        { continue }
        $exteriorNearWhite[$x,$y]=$true
        foreach($offset in @(@(-1,0),@(1,0),@(0,-1),@(0,1)))
        {
            $neighborX=$x+$offset[0]; $neighborY=$y+$offset[1]
            if($neighborX -ge 0 -and $neighborY -ge 0 -and `
                $neighborX -lt $Source.Width -and $neighborY -lt $Source.Height -and `
                -not $exteriorNearWhite[$neighborX,$neighborY])
            { $queue.Enqueue(@($neighborX,$neighborY)) }
        }
    }

    $segments=[Collections.Generic.List[object]]::new()
    $directions=@(
        [pscustomobject]@{ DX=-1; DY=0; Side='Left' }
        [pscustomobject]@{ DX=1; DY=0; Side='Right' }
        [pscustomobject]@{ DX=0; DY=-1; Side='Top' }
        [pscustomobject]@{ DX=0; DY=1; Side='Bottom' })

    for($y=0;$y -lt $Mask.Height;$y++)
    {
        for($x=0;$x -lt $Mask.Width;$x++)
        {
            $maskValue=$Mask.GetPixel($x,$y).R
            Assert-True ($maskValue -eq 0 -or $maskValue -eq 255) `
                "Reviewed mask contains intermediate value '$maskValue' at ($x,$y)."
            if($maskValue -ne 255)
            { continue }

            foreach($direction in $directions)
            {
                $neighborX=$x+$direction.DX
                $neighborY=$y+$direction.DY
                if($neighborX -lt 0 -or $neighborY -lt 0 -or `
                    $neighborX -ge $Mask.Width -or $neighborY -ge $Mask.Height)
                { continue }

                if(-not $exteriorNearWhite[$neighborX,$neighborY])
                { continue }

                switch($direction.Side)
                {
                    'Left' {
                        $x1=$x-0.5; $y1=$y-0.5; $x2=$x-0.5; $y2=$y+0.5
                    }
                    'Right' {
                        $x1=$x+0.5; $y1=$y-0.5; $x2=$x+0.5; $y2=$y+0.5
                    }
                    'Top' {
                        $x1=$x-0.5; $y1=$y-0.5; $x2=$x+0.5; $y2=$y-0.5
                    }
                    'Bottom' {
                        $x1=$x-0.5; $y1=$y+0.5; $x2=$x+0.5; $y2=$y+0.5
                    }
                }
                $segments.Add([pscustomobject]@{
                    X1=[double]$x1; Y1=[double]$y1
                    X2=[double]$x2; Y2=[double]$y2
                    Kind='E'
                })
            }
        }
    }

    foreach($endpoint in @($LegalEndpoints))
    {
        Assert-Equal 255 $Mask.GetPixel([int]$endpoint.X,[int]$endpoint.Y).R `
            "Legal endpoint '$($endpoint.Name)' moved outside the final body mask."
        Assert-Equal 255 $Source.GetPixel([int]$endpoint.X,[int]$endpoint.Y).A `
            "Legal endpoint '$($endpoint.Name)' is no longer source-opaque."

        $nearest=$null
        $nearestDistance=[double]::PositiveInfinity
        foreach($segment in @($segments))
        {
            foreach($vertex in @(
                [pscustomobject]@{ X=$segment.X1; Y=$segment.Y1 }
                [pscustomobject]@{ X=$segment.X2; Y=$segment.Y2 }))
            {
                $distance=(($vertex.X-$endpoint.X)*($vertex.X-$endpoint.X))+`
                    (($vertex.Y-$endpoint.Y)*($vertex.Y-$endpoint.Y))
                $isOrdinallyEarlier=$null -eq $nearest -or `
                    $vertex.Y -lt $nearest.Y -or `
                    ($vertex.Y -eq $nearest.Y -and $vertex.X -lt $nearest.X)
                if($distance -lt ($nearestDistance-0.000000001) -or `
                    ([Math]::Abs($distance-$nearestDistance) -le 0.000000001 -and `
                    $isOrdinallyEarlier))
                {
                    $nearest=$vertex
                    $nearestDistance=$distance
                }
            }
        }
        Assert-True ($null -ne $nearest) `
            "Legal endpoint '$($endpoint.Name)' has no exposed contour vertex."
        $segments.Add([pscustomobject]@{
            X1=[double]$nearest.X; Y1=[double]$nearest.Y
            X2=[double]$endpoint.X; Y2=[double]$endpoint.Y
            Kind='C'
        })
    }
    return @($segments)
}

function Get-DororongIndependentContourHash([object]$Segments)
{
    $records=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $entries=[Collections.Generic.List[object]]::new()
    foreach($segment in @($Segments))
    {
        $x1=[int][Math]::Round(2.0*$segment.X1)
        $y1=[int][Math]::Round(2.0*$segment.Y1)
        $x2=[int][Math]::Round(2.0*$segment.X2)
        $y2=[int][Math]::Round(2.0*$segment.Y2)
        if($y2 -lt $y1 -or ($y2 -eq $y1 -and $x2 -lt $x1))
        {
            $temporaryX=$x1; $temporaryY=$y1
            $x1=$x2; $y1=$y2; $x2=$temporaryX; $y2=$temporaryY
        }
        $record="$($segment.Kind)|$y1|$x1|$y2|$x2"
        if($records.Add($record))
        {
            $entries.Add([pscustomobject]@{
                Kind=[string]$segment.Kind; StartY2=$y1; StartX2=$x1
                EndY2=$y2; EndX2=$x2; Record=$record })
        }
    }
    $text=@($entries|Sort-Object Kind,StartY2,StartX2,EndY2,EndX2|`
        ForEach-Object Record)|Join-String -Separator "`n"
    $sha=[Security.Cryptography.SHA256]::Create()
    try
    { return [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))) }
    finally
    { $sha.Dispose() }
}

function Get-DororongStrictSegmentIntersections(
    [hashtable]$Normal,
    [object]$Segments)
{
    $px=$Normal.X1Eighth/8.0; $py=$Normal.Y1Eighth/8.0
    $rx=($Normal.X2Eighth-$Normal.X1Eighth)/8.0
    $ry=($Normal.Y2Eighth-$Normal.Y1Eighth)/8.0
    $epsilon=0.000000001
    $intersections=@()
    foreach($segment in @($Segments))
    {
        $qx=[double]$segment.X1; $qy=[double]$segment.Y1
        $sx=[double]$segment.X2-$qx; $sy=[double]$segment.Y2-$qy
        $crossRS=($rx*$sy)-($ry*$sx)
        if([Math]::Abs($crossRS) -le $epsilon)
        { continue }
        $qpx=$qx-$px; $qpy=$qy-$py
        $t=(($qpx*$sy)-($qpy*$sx))/$crossRS
        $u=(($qpx*$ry)-($qpy*$rx))/$crossRS
        if($t -lt -$epsilon -or $t -gt (1.0+$epsilon) -or `
            $u -lt -$epsilon -or $u -gt (1.0+$epsilon))
        { continue }
        $intersections += [pscustomobject]@{
            X=$px+($t*$rx); Y=$py+($t*$ry); T=$t; U=$u; Segment=$segment
            IsStrict=$t -gt $epsilon -and $t -lt (1.0-$epsilon) -and `
                $u -gt $epsilon -and $u -lt (1.0-$epsilon)
        }
    }
    return @($intersections)
}

function Get-DororongMaskSide(
    [Drawing.Bitmap]$Mask,
    [double]$X,
    [double]$Y)
{
    $pixelX=[int][Math]::Floor($X+0.5)
    $pixelY=[int][Math]::Floor($Y+0.5)
    Assert-True ($pixelX -ge 0 -and $pixelX -lt $Mask.Width -and `
        $pixelY -ge 0 -and $pixelY -lt $Mask.Height) `
        "Ownership sample ($X,$Y) is outside the final mask."
    return $Mask.GetPixel($pixelX,$pixelY).R
}

function Assert-DororongFinalContourNormal(
    [hashtable]$Entry,
    [Drawing.Bitmap]$Mask,
    [object]$FinalContour)
{
    $label="Body normal '$($Entry.Name)'"
    $namedContour=@(Get-DororongNamedContourSegments $FinalContour $Entry.Name)
    $namedHits=@(Get-DororongStrictSegmentIntersections $Entry.SourceNormal $namedContour)
    $strictNamedHits=@($namedHits|Where-Object IsStrict)
    Assert-Equal 1 $strictNamedHits.Count `
        "$label SourceNormal must cross exactly one named E/C segment strictly inside it."

    $allHits=@(Get-DororongStrictSegmentIntersections $Entry.SourceNormal $FinalContour)
    Assert-Equal 1 $allHits.Count `
        "$label SourceNormal crosses a neighboring or second canonical segment."
    Assert-True $allHits[0].IsStrict `
        "$label SourceNormal crosses a contour vertex or junction."

    $normal=$Entry.SourceNormal
    $normalX=($normal.X2Eighth-$normal.X1Eighth)/8.0
    $normalY=($normal.Y2Eighth-$normal.Y1Eighth)/8.0
    $segment=$strictNamedHits[0].Segment
    $tangentX=[double]$segment.X2-[double]$segment.X1
    $tangentY=[double]$segment.Y2-[double]$segment.Y1
    $absoluteUnitDot=[Math]::Abs(($normalX*$tangentX)+($normalY*$tangentY))/`
        ([Math]::Sqrt(($normalX*$normalX)+($normalY*$normalY))*`
         [Math]::Sqrt(($tangentX*$tangentX)+($tangentY*$tangentY)))
    Assert-True ($absoluteUnitDot -le 0.0871557427476582) `
        "$label SourceNormal is more than 5 degrees from perpendicular; absolute unit dot=$absoluteUnitDot."

    Assert-Equal 255 (Get-DororongMaskSide $Mask `
        ($normal.X1Eighth/8.0) ($normal.Y1Eighth/8.0)) `
        "$label SourceNormal first 1/8-pixel sample is not on the body side."
    Assert-Equal 0 (Get-DororongMaskSide $Mask `
        ($normal.X2Eighth/8.0) ($normal.Y2Eighth/8.0)) `
        "$label SourceNormal final 1/8-pixel sample is not on the non-body side."
    return $strictNamedHits[0]
}

function Get-Median([double[]]$Values)
{
    Assert-True ($Values.Count -gt 0) 'Cannot calculate an empty median.'
    $sorted=@($Values|Sort-Object)
    $middle=[int][Math]::Floor($sorted.Count/2.0)
    if(($sorted.Count%2) -eq 1)
    { return [double]$sorted[$middle] }
    return ([double]$sorted[$middle-1]+[double]$sorted[$middle])/2.0
}

function Get-DororongNamedContourSegments(
    [object]$ApprovedContour,
    [string]$Name)
{
    $windows=@{
        FrontOuter=@{ MinX=36.5; MaxX=39.5; MinY=160.5; MaxY=171.5 }
        FrontFoot=@{ MinX=48.5; MaxX=62.5; MinY=189.5; MaxY=193.5 }
        FrontInner=@{ MinX=62.5; MaxX=65.5; MinY=176.5; MaxY=186.5 }
        FirstValley=@{ MinX=66.5; MaxX=73.5; MinY=176.5; MaxY=181.5 }
        FirstUnderside=@{ MinX=73.5; MaxX=79.0; MinY=178.5; MaxY=184.5 }
        CenterOuter=@{ MinX=79.5; MaxX=83.5; MinY=181.5; MaxY=191.5 }
        CenterFoot=@{ MinX=96.5; MaxX=110.5; MinY=203.5; MaxY=206.5 }
        CenterInner=@{ MinX=112.5; MaxX=116.5; MinY=183.5; MaxY=191.5 }
        SecondValley=@{ MinX=138.5; MaxX=145.5; MinY=171.5; MaxY=178.5 }
        SecondUnderside=@{ MinX=123.5; MaxX=129.5; MinY=176.5; MaxY=181.5 }
        RearOuter=@{ MinX=164.5; MaxX=168.5; MinY=174.5; MaxY=185.5 }
        RearFoot=@{ MinX=149.5; MaxX=161.5; MinY=196.5; MaxY=200.5 }
        RearInner=@{ MinX=137.5; MaxX=141.5; MinY=176.5; MaxY=184.5 }
        UpperRearRim=@{ MinX=174.5; MaxX=178.5; MinY=122.5; MaxY=135.5 }
        LowerRearRim=@{ MinX=174.5; MaxX=178.5; MinY=148.5; MaxY=160.5 }
    }
    Assert-True $windows.ContainsKey($Name) `
        "Body normal '$Name' has no independently reviewed contour window."
    $window=$windows[$Name]
    return @($ApprovedContour | Where-Object {
        $midX=($_.X1+$_.X2)/2.0
        $midY=($_.Y1+$_.Y2)/2.0
        $midX -ge $window.MinX -and $midX -le $window.MaxX -and `
        $midY -ge $window.MinY -and $midY -le $window.MaxY
    })
}

function Get-DororongUniqueSegmentIntersections(
    [hashtable]$Normal,
    [object]$Segments)
{
    $px=$Normal.X1Eighth/8.0; $py=$Normal.Y1Eighth/8.0
    $rx=($Normal.X2Eighth-$Normal.X1Eighth)/8.0
    $ry=($Normal.Y2Eighth-$Normal.Y1Eighth)/8.0
    $epsilon=0.000000001
    $unique=@{}

    foreach($segment in @($Segments))
    {
        $qx=[double]$segment.X1; $qy=[double]$segment.Y1
        $sx=[double]$segment.X2-$qx; $sy=[double]$segment.Y2-$qy
        $crossRS=($rx*$sy)-($ry*$sx)
        $qpx=$qx-$px; $qpy=$qy-$py
        $crossQPR=($qpx*$ry)-($qpy*$rx)

        $points=@()
        if([Math]::Abs($crossRS) -le $epsilon)
        {
            if([Math]::Abs($crossQPR) -gt $epsilon)
            { continue }
            $normalLengthSquared=($rx*$rx)+($ry*$ry)
            $t1=(($qpx*$rx)+($qpy*$ry))/$normalLengthSquared
            $q2px=([double]$segment.X2)-$px
            $q2py=([double]$segment.Y2)-$py
            $t2=(($q2px*$rx)+($q2py*$ry))/$normalLengthSquared
            $overlapStart=[Math]::Max(0.0,[Math]::Min($t1,$t2))
            $overlapEnd=[Math]::Min(1.0,[Math]::Max($t1,$t2))
            if($overlapStart -gt ($overlapEnd+$epsilon))
            { continue }
            $points=@($overlapStart,$overlapEnd)
        }
        else
        {
            $t=(($qpx*$sy)-($qpy*$sx))/$crossRS
            $u=(($qpx*$ry)-($qpy*$rx))/$crossRS
            if($t -lt -$epsilon -or $t -gt (1.0+$epsilon) -or `
                $u -lt -$epsilon -or $u -gt (1.0+$epsilon))
            { continue }
            $points=@([Math]::Clamp($t,0.0,1.0))
        }

        foreach($t in $points)
        {
            $x=$px+($t*$rx); $y=$py+($t*$ry)
            $key=[String]::Format(
                [Globalization.CultureInfo]::InvariantCulture,
                '{0:F9},{1:F9}',$x,$y)
            $unique[$key]=[pscustomobject]@{ X=$x; Y=$y }
        }
    }
    return @($unique.Values)
}

function Replace-FirstLiteral([string]$Text, [string]$Old, [string]$New, [string]$Label)
{
    $index = $Text.IndexOf($Old, [StringComparison]::Ordinal)
    Assert-True ($index -ge 0) "$Label mutation target was not found."
    return $Text.Substring(0,$index) + $New + $Text.Substring($index+$Old.Length)
}

function Invoke-AuthorityMutationFailure(
    [string]$FixtureText,
    [string]$ExpectedError,
    [string]$Label,
    [string]$ToolPath,
    [string]$SourcePath,
    [string]$MaskPath,
    [string]$NativePath,
    [string]$TemporaryRoot)
{
    $authorityCopy = Join-Path $TemporaryRoot "$Label.psd1"
    [IO.File]::WriteAllText($authorityCopy,$FixtureText,[Text.UTF8Encoding]::new($false))
    $caught = $null
    try
    {
        & $ToolPath -SourcePath $SourcePath -MaskPath $MaskPath -NativePath $NativePath `
            -AuthorityPath $authorityCopy -EvidenceDirectory (Join-Path $TemporaryRoot "$Label-evidence") | Out-Null
    }
    catch
    { $caught = $_.Exception.Message }
    Assert-True ($null -ne $caught) "$Label mutation was accepted."
    Assert-True $caught.Contains($ExpectedError) `
        "$Label mutation failed without named error '$ExpectedError'. Observed '$caught'."
    Write-Output "CONTINUOUS MUTATION PASS label=$Label failure=$ExpectedError"
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical-source.png'
$nativePath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical.png'
$maskPath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-body-region-mask.png'
$authorityPath = Join-Path $repositoryRoot 'tests/fixtures/dororong-body-outline-authority.psd1'
$supportPath = Join-Path $repositoryRoot 'tests/support/Dororong.ContinuousOptics.ps1'
$toolPath = Join-Path $repositoryRoot 'tools/New-ContinuousOutlineAuthority.ps1'
$expectedAuthorityHash='DDF749007995B3F03781A3A51467013F406C5F7A2AA0480212523A79EF31F17F'

Assert-Equal 'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash 'Canonical source changed.'
Assert-Equal 'D08B3A941C662F1CBC55C486C13FD4C6CD8901DA9CD5CF8512509698219FE46F' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $maskPath).Hash 'Final body-region mask changed.'
Assert-Equal '238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $nativePath).Hash 'Committed native-open authority changed.'
Assert-True (Test-Path -LiteralPath $authorityPath) 'Continuous authority fixture is missing.'
Assert-True (Test-Path -LiteralPath $supportPath) 'Continuous optics test helper is missing.'

Add-Type -AssemblyName System.Drawing
. $supportPath

$synthetic = [Drawing.Bitmap]::new(2,2,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
try
{
    $synthetic.SetPixel(0,0,[Drawing.Color]::FromArgb(255,255,0,0))
    $synthetic.SetPixel(1,0,[Drawing.Color]::FromArgb(128,0,255,0))
    $synthetic.SetPixel(0,1,[Drawing.Color]::FromArgb(0,255,255,255))
    $synthetic.SetPixel(1,1,[Drawing.Color]::FromArgb(255,0,0,255))
    $sample=Get-DororongBilinearPremultipliedSample $synthetic 0.5 0.5
    Assert-Near 0.625490196078431 $sample.A 0.000000000001 `
        'Bilinear sampler did not interpolate alpha.'
    Assert-Near 0.399686520376176 $sample.R 0.000000000001 `
        'Bilinear sampler did not unpremultiply red.'
    Assert-Near 0.200626959247649 $sample.G 0.000000000001 `
        'Bilinear sampler did not unpremultiply green.'
    Assert-Near 0.399686520376176 $sample.B 0.000000000001 `
        'Bilinear sampler did not unpremultiply blue.'
    $outside=Get-DororongBilinearPremultipliedSample $synthetic -2.0 -2.0
    Assert-Near 0.0 $outside.A 0.0 'Outside sample alpha is not transparent.'
    Assert-Near 0.0 $outside.R 0.0 'Outside sample red is not black.'
    Assert-Near 0.0 $outside.G 0.0 'Outside sample green is not black.'
    Assert-Near 0.0 $outside.B 0.0 'Outside sample blue is not black.'
}
finally
{ $synthetic.Dispose() }

$integration = [Drawing.Bitmap]::new(2,1,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
try
{
    $integration.SetPixel(0,0,[Drawing.Color]::FromArgb(255,0,0,0))
    $integration.SetPixel(1,0,[Drawing.Color]::FromArgb(255,255,255,255))
    $normal=@{X1Eighth=0;Y1Eighth=0;X2Eighth=8;Y2Eighth=0}
    $coverage=Measure-DororongContinuousCoverage $integration $normal `
        ([Drawing.Color]::White) ([Drawing.Color]::Black) 0.6
    Assert-Near 0.5 $coverage 0.000000000001 `
        'Continuous coverage did not trapezoid-integrate the final shorter interval.'
}
finally
{ $integration.Dispose() }

$sharedVertexSegments=@(
    [pscustomobject]@{ X1=0.0; Y1=0.0; X2=1.0; Y2=0.0 }
    [pscustomobject]@{ X1=1.0; Y1=0.0; X2=1.0; Y2=1.0 })
$sharedVertexNormal=@{
    X1Eighth=4; Y1Eighth=-4; X2Eighth=12; Y2Eighth=4 }
Assert-Equal 1 `
    @(Get-DororongUniqueSegmentIntersections `
        $sharedVertexNormal $sharedVertexSegments).Count `
    'A shared stair-step contour vertex was counted as more than one crossing.'

$authority = Import-PowerShellDataFile -LiteralPath $authorityPath
$authorityText=[IO.File]::ReadAllText($authorityPath)
$hairStart=$authorityText.IndexOf('    HairAnchors = @(',[StringComparison]::Ordinal)
$hairEnd=$authorityText.IndexOf('    BodyNormals = @(',[StringComparison]::Ordinal)
Assert-True ($hairStart -ge 0 -and $hairEnd -gt $hairStart) `
    'Hair anchor byte range is missing.'
$sha=[Security.Cryptography.SHA256]::Create()
try
{
    $hairBytesHash=[Convert]::ToHexString($sha.ComputeHash(
        [Text.Encoding]::UTF8.GetBytes($authorityText.Substring(
            $hairStart,$hairEnd-$hairStart))))
}
finally
{ $sha.Dispose() }
Assert-Equal '4942259408D151BABE64BB131334CD9E2F7111876B95C5C488667254315D7FD6' `
    $hairBytesHash 'Hair anchor bytes changed.'
$expectedLegalEndpoints='118,151|161,116'
Assert-True $authority.ContainsKey('LegalEndpoints') `
    'Continuous authority still uses historical endpoint markers instead of literal LegalEndpoints.'
Assert-Equal $expectedLegalEndpoints `
    (@($authority.LegalEndpoints|ForEach-Object{"$($_.X),$($_.Y)"})-join '|') `
    'Continuous authority legal endpoints changed.'
Assert-True (@($authority.ProtectedPoints|Where-Object Name -like 'LegalEndpoint-*').Count -eq 0) `
    'Continuous authority still protects a historical endpoint marker.'
$source = [Drawing.Bitmap]::new($sourcePath)
$native = [Drawing.Bitmap]::new($nativePath)
$mask = [Drawing.Bitmap]::new($maskPath)
try
{
    Assert-Equal 225 $source.Width 'Canonical source width changed.'
    Assert-Equal 225 $source.Height 'Canonical source height changed.'
    Assert-Equal 96 $native.Width 'Committed native-open width changed.'
    Assert-Equal 96 $native.Height 'Committed native-open height changed.'
    Assert-Equal 225 $mask.Width 'Reviewed mask width changed.'
    Assert-Equal 225 $mask.Height 'Reviewed mask height changed.'

    $approvedSourceContour=@(Get-DororongFinalSourceContour `
        $source $mask $authority.LegalEndpoints)
    Assert-True ($approvedSourceContour.Count -gt 0) `
        'Independent final source contour derivation produced no segments.'
    Assert-Equal 'A29D007B699A16B555FE5133854E832FEFD8409EA85F2FECE3EEA97BF444FD65' `
        (Get-DororongIndependentContourHash $approvedSourceContour) `
        'Independent final source contour hash changed.'

    $hairAnchors=@($authority.HairAnchors)
    $bodyNormals=@($authority.BodyNormals)
    Assert-Equal 6 $hairAnchors.Count 'Hair anchor count changed.'
    Assert-Equal 15 $bodyNormals.Count 'Body normal count changed.'

    foreach ($kind in @('Straight','Diagonal','Curve'))
    {
        Assert-True (@($hairAnchors | Where-Object Kind -eq $kind).Count -ge 2) `
            "Hair anchor category '$kind' has fewer than two entries."
    }

    $expectedBodyNames=@(
        'FrontOuter','FrontFoot','FrontInner','FirstValley','FirstUnderside',
        'CenterOuter','CenterFoot','CenterInner','SecondValley','SecondUnderside',
        'RearOuter','RearFoot','RearInner','UpperRearRim','LowerRearRim') | Sort-Object
    $actualBodyNames=@($bodyNormals.Name) | Sort-Object
    Assert-Equal ($expectedBodyNames -join ',') ($actualBodyNames -join ',') `
        'The fifteen named body normals changed.'

    if ($BodyNativeReferenceOnly)
    {
        Assert-Equal $expectedAuthorityHash `
            (Get-FileHash -Algorithm SHA256 -LiteralPath $authorityPath).Hash `
            'Continuous authority fixture changed.'
        $selectionMismatchCount=0
        foreach ($entry in $bodyNormals)
        {
            $label="Body normal '$($entry.Name)'"
            Assert-Normal $entry 'SourceNormal' $source $label
            Assert-Normal $entry 'NativeNormal' $native $label
            Assert-ReferencePoint $entry 'SourceFill' $source $label
            Assert-ReferencePoint $entry 'NativeFill' $native $label
            Assert-PointEqual $entry.Fill $entry.SourceFill "$label SourceFill provenance"
            Assert-True (Test-NormalIntersectsMask $mask $entry.SourceNormal) `
                "$label source normal does not intersect the approved body mask."
            $null=Assert-DororongFinalContourNormal $entry $mask $approvedSourceContour
            Assert-True (Test-NormalIntersectsAlpha $native $entry.NativeNormal) `
                "$label native location normal does not intersect committed body alpha."

            $selectedNativeFill=@(Get-SelectedOpaquePoint `
                $native $entry.NativeSamples $true $label)
            $frozenNativeFill=@($entry.NativeFill)
            $selectionMatches=(
                [int]$selectedNativeFill[0] -eq [int]$frozenNativeFill[0] -and `
                [int]$selectedNativeFill[1] -eq [int]$frozenNativeFill[1])
            if (-not $selectionMatches)
            { $selectionMismatchCount++ }
            $selectedColor=$native.GetPixel(
                [int]$selectedNativeFill[0],[int]$selectedNativeFill[1])
            $frozenColor=$native.GetPixel(
                [int]$frozenNativeFill[0],[int]$frozenNativeFill[1])
            Write-Output (
                "BODY NATIVE DIAGNOSTIC name=$($entry.Name) " +
                "frozen=$($frozenNativeFill -join ',') " +
                "frozenLuminance=$(Get-Rec709Luminance $frozenColor) " +
                "brightest=$($selectedNativeFill -join ',') " +
                "brightestLuminance=$(Get-Rec709Luminance $selectedColor) " +
                "matches=$selectionMatches authority=false")
        }
        Write-Output (
            "BODY NATIVE REFERENCE PASS body=15 " +
            "authorityHash=$expectedAuthorityHash " +
            "contourHash=A29D007B699A16B555FE5133854E832FEFD8409EA85F2FECE3EEA97BF444FD65 " +
            "brightestSelection=diagnosticOnly mismatches=$selectionMismatchCount")
        return
    }

    $sourceHairCoverages=@()
    $nativeHairCoverages=@()
    foreach ($entry in $hairAnchors)
    {
        $label="Hair anchor '$($entry.Name)'"
        Assert-Normal $entry 'SourceNormal' $source $label
        Assert-Normal $entry 'NativeNormal' $native $label
        Assert-ReferencePoint $entry 'SourceFill' $source $label
        Assert-ReferencePoint $entry 'SourceInk' $source $label
        Assert-ReferencePoint $entry 'NativeFill' $native $label
        Assert-ReferencePoint $entry 'NativeInk' $native $label

        Assert-PointEqual $entry.Fill $entry.SourceFill "$label SourceFill provenance"
        Assert-PointEqual $entry.Ink $entry.SourceInk "$label SourceInk provenance"
        Assert-PointEqual (Get-SelectedOpaquePoint $native $entry.NativeSamples $true $label) `
            $entry.NativeFill "$label NativeFill selection"
        Assert-PointEqual (Get-SelectedOpaquePoint $native $entry.NativeSamples $false $label) `
            $entry.NativeInk "$label NativeInk selection"

        $sourceFill=$source.GetPixel([int]$entry.SourceFill[0],[int]$entry.SourceFill[1])
        $sourceInk=$source.GetPixel([int]$entry.SourceInk[0],[int]$entry.SourceInk[1])
        $nativeFill=$native.GetPixel([int]$entry.NativeFill[0],[int]$entry.NativeFill[1])
        $nativeInk=$native.GetPixel([int]$entry.NativeInk[0],[int]$entry.NativeInk[1])
        $surfaces = @(
            [pscustomobject]@{
                Name='source'; Bitmap=$source; Normal=$entry.SourceNormal; Fill=$sourceFill; Ink=$sourceInk }
            [pscustomobject]@{
                Name='native'; Bitmap=$native; Normal=$entry.NativeNormal; Fill=$nativeFill; Ink=$nativeInk })
        foreach ($surface in $surfaces)
        {
            $profile=@(Get-ContinuousProfile `
                $surface.Bitmap $surface.Normal $surface.Fill $surface.Ink)
            Assert-Equal 1 (Get-DarknessRunCount $profile 0.10) `
                "$label $($surface.Name) normal does not contain exactly one darkness run at threshold 0.10."
            Assert-True ($profile[0].Darkness -lt 0.10) `
                "$label $($surface.Name) first endpoint is not near-zero darkness."
            Assert-True ($profile[-1].Darkness -lt 0.10) `
                "$label $($surface.Name) second endpoint is not near-zero darkness."
            $coverage=Measure-DororongContinuousCoverage `
                $surface.Bitmap $surface.Normal $surface.Fill $surface.Ink
            Assert-True ($coverage -gt 0.0) `
                "$label $($surface.Name) continuous integral is not positive."
            if($surface.Name -eq 'source')
            { $sourceHairCoverages += $coverage }
            else
            { $nativeHairCoverages += $coverage }
        }
    }
    $sourceHairMedian=Get-Median $sourceHairCoverages
    $nativeHairMedian=Get-Median $nativeHairCoverages
    Assert-Near 2.208986702011595 $sourceHairMedian 0.000000000001 `
        'Source hair median changed.'
    Assert-Near 2.009803921568627 $nativeHairMedian 0.000000000001 `
        'Native hair median changed.'

    $bodyInk=[Drawing.Color]::FromArgb(255,0,0,0)
    foreach ($entry in $bodyNormals)
    {
        $label="Body normal '$($entry.Name)'"
        Assert-Normal $entry 'SourceNormal' $source $label
        Assert-Normal $entry 'NativeNormal' $native $label
        Assert-ReferencePoint $entry 'SourceFill' $source $label
        Assert-ReferencePoint $entry 'NativeFill' $native $label
        Assert-PointEqual $entry.Fill $entry.SourceFill "$label SourceFill provenance"
        $selectedNativeFill=@(Get-SelectedOpaquePoint `
            $native $entry.NativeSamples $true $label)
        Write-Output (
            "BODY NATIVE DIAGNOSTIC name=$($entry.Name) " +
            "frozen=$(@($entry.NativeFill) -join ',') " +
            "brightest=$($selectedNativeFill -join ',') authority=false")

        $sourceFill=$source.GetPixel([int]$entry.SourceFill[0],[int]$entry.SourceFill[1])
        $profile=@(Get-ContinuousProfile $source $entry.SourceNormal $sourceFill $bodyInk)
        Assert-Equal 1 (Get-DarknessRunCount $profile 0.10) `
            "$label source normal does not contain exactly one original-body-ink run."
        $hasNearWhiteInterval=$false
        for ($index=0; $index -lt $profile.Count-1; $index++)
        {
            if ($profile[$index].Alpha -ge 0.999 -and $profile[$index+1].Alpha -ge 0.999 -and `
                $profile[$index].Luminance -ge 245.0 -and $profile[$index+1].Luminance -ge 245.0)
            { $hasNearWhiteInterval=$true; break }
        }
        Assert-True $hasNearWhiteInterval `
            "$label source normal has no near-white fill-side interval."
        Assert-True (Test-NormalIntersectsMask $mask $entry.SourceNormal) `
            "$label source normal does not intersect the approved body mask."
        Assert-True ((Measure-DororongContinuousCoverage `
            $source $entry.SourceNormal $sourceFill $bodyInk) -gt 0.0) `
            "$label source continuous integral is not positive."

        $null=Assert-DororongFinalContourNormal $entry $mask $approvedSourceContour

        Assert-True (Test-NormalIntersectsAlpha $native $entry.NativeNormal) `
            "$label native location normal does not intersect committed body alpha."
    }

    foreach ($point in @($authority.ProtectedPoints))
    {
        Assert-Equal 0 $mask.GetPixel([int]$point.X,[int]$point.Y).R `
            "Protected point '$($point.Name)' is writable in the approved mask."
    }
}
finally
{
    $mask.Dispose()
    $native.Dispose()
    $source.Dispose()
}

$productionFiles=@(
    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Recurse -File |
        Where-Object Extension -in @('.cs','.ps1')
    Get-Item -LiteralPath (Join-Path $repositoryRoot 'tools/Generate-CanonicalArt.ps1'))
foreach ($file in $productionFiles)
{
    $text=[IO.File]::ReadAllText($file.FullName)
    Assert-True (-not $text.Contains('dororong-body-outline-authority.psd1')) `
        "Production generator/module '$($file.FullName)' imports the authority fixture."
    Assert-True (-not $text.Contains('Dororong.ContinuousOptics.ps1')) `
        "Production generator/module '$($file.FullName)' imports the continuous test helper."
    Assert-True (-not $text.Contains('Measure-DororongContinuousCoverage')) `
        "Production generator/module '$($file.FullName)' references test-only continuous coverage."
}

Assert-True (Test-Path -LiteralPath $toolPath) 'Continuous authority overlay tool is missing.'
$fixtureText=[IO.File]::ReadAllText($authorityPath)
$firstAnchor=@($authority.HairAnchors)[0]
$frontOuter=@($authority.BodyNormals|Where-Object Name -eq 'FrontOuter')[0]
$frontOuterNormal=$frontOuter.SourceNormal
$frontOuterLiteral="            SourceNormal = @{ X1Eighth = $($frontOuterNormal.X1Eighth); Y1Eighth = $($frontOuterNormal.Y1Eighth); X2Eighth = $($frontOuterNormal.X2Eighth); Y2Eighth = $($frontOuterNormal.Y2Eighth) }"
$vertexLiteral='            SourceNormal = @{ X1Eighth = 368; Y1Eighth = 1320; X2Eighth = 300; Y2Eighth = 1316 }'
$rotatedLiteral='            SourceNormal = @{ X1Eighth = 368; Y1Eighth = 1328; X2Eighth = 256; Y2Eighth = 1312 }'
$reversedLiteral="            SourceNormal = @{ X1Eighth = $($frontOuterNormal.X2Eighth); Y1Eighth = $($frontOuterNormal.Y2Eighth); X2Eighth = $($frontOuterNormal.X1Eighth); Y2Eighth = $($frontOuterNormal.Y1Eighth) }"
$secondValley=@($authority.BodyNormals|Where-Object Name -eq 'SecondValley')[0]
$secondValleyNormal=$secondValley.SourceNormal
$secondValleyLiteral="            SourceNormal = @{ X1Eighth = $($secondValleyNormal.X1Eighth); Y1Eighth = $($secondValleyNormal.Y1Eighth); X2Eighth = $($secondValleyNormal.X2Eighth); Y2Eighth = $($secondValleyNormal.Y2Eighth) }"
$neighborLiteral='            SourceNormal = @{ X1Eighth = 1152; Y1Eighth = 1384; X2Eighth = 1056; Y2Eighth = 1384 }'
$protectedClose="        @{ Name = 'NoTailRear-Lower'; X = 180; Y = 163 }`n    )"
$historicalProtectedClose="        @{ Name = 'NoTailRear-Lower'; X = 180; Y = 163 }`n        @{ Name = 'LegalEndpoint-FrontOcclusion'; X = 112; Y = 151 }`n    )"
$hairNormal=$firstAnchor.SourceNormal
$hairNormalLiteral="            SourceNormal = @{ X1Eighth = $($hairNormal.X1Eighth); Y1Eighth = $($hairNormal.Y1Eighth); X2Eighth = $($hairNormal.X2Eighth); Y2Eighth = $($hairNormal.Y2Eighth) }"
$hairChangedLiteral="            SourceNormal = @{ X1Eighth = $($hairNormal.X1Eighth+1); Y1Eighth = $($hairNormal.Y1Eighth); X2Eighth = $($hairNormal.X2Eighth); Y2Eighth = $($hairNormal.Y2Eighth) }"
$temporaryRoot=Join-Path ([IO.Path]::GetTempPath()) ("dororong-continuous-authority-"+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
try
{
    $vertexText=Replace-FirstLiteral $fixtureText $frontOuterLiteral $vertexLiteral 'Vertex endpoint'
    Invoke-AuthorityMutationFailure $vertexText `
        "Body normal 'FrontOuter' must cross exactly one named E/C segment strictly inside it." `
        'endpoint-at-vertex' `
        $toolPath $sourcePath $maskPath $nativePath $temporaryRoot

    $rotatedText=Replace-FirstLiteral $fixtureText $frontOuterLiteral $rotatedLiteral 'Rotated normal'
    Invoke-AuthorityMutationFailure $rotatedText `
        "Body normal 'FrontOuter' is more than 5 degrees from perpendicular" `
        'rotated-beyond-five-degrees' `
        $toolPath $sourcePath $maskPath $nativePath $temporaryRoot

    $reversedText=Replace-FirstLiteral $fixtureText $frontOuterLiteral $reversedLiteral 'Reversed normal'
    Invoke-AuthorityMutationFailure $reversedText `
        "Body normal 'FrontOuter' is reversed" 'reversed-normal' `
        $toolPath $sourcePath $maskPath $nativePath $temporaryRoot

    $neighborText=Replace-FirstLiteral $fixtureText $secondValleyLiteral $neighborLiteral 'Neighbor crossing'
    Invoke-AuthorityMutationFailure $neighborText `
        "Body normal 'SecondValley' crosses a neighboring or second canonical segment." `
        'extended-across-neighbor' `
        $toolPath $sourcePath $maskPath $nativePath $temporaryRoot

    $historicalText=Replace-FirstLiteral $fixtureText $protectedClose `
        $historicalProtectedClose 'Historical protected endpoint'
    Invoke-AuthorityMutationFailure $historicalText `
        'Historical protected endpoint reintroduced.' 'historical-protected-endpoint' `
        $toolPath $sourcePath $maskPath $nativePath $temporaryRoot

    $hairChangedText=Replace-FirstLiteral $fixtureText $hairNormalLiteral `
        $hairChangedLiteral 'Hair anchor byte'
    Invoke-AuthorityMutationFailure $hairChangedText `
        'Hair anchor bytes changed.' 'hair-anchor-byte' `
        $toolPath $sourcePath $maskPath $nativePath $temporaryRoot
}
finally
{
    if ([IO.Directory]::Exists($temporaryRoot))
    { [IO.Directory]::Delete($temporaryRoot,$true) }
}

$authorityHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $authorityPath).Hash
Assert-Equal $expectedAuthorityHash $authorityHash 'Continuous authority fixture changed.'
Write-Output "HAIR MEDIANS source=$sourceHairMedian native=$nativeHairMedian bytes=$hairBytesHash"
Write-Output "BODY NORMALS names=$(@($authority.BodyNormals.Name)-join ',')"
Write-Output "FINAL GEOMETRY maskHash=$((Get-FileHash -Algorithm SHA256 -LiteralPath $maskPath).Hash) contourHash=A29D007B699A16B555FE5133854E832FEFD8409EA85F2FECE3EEA97BF444FD65"
Write-Output "CONTINUOUS AUTHORITY PASS hash=$authorityHash hair=6 body=15"
