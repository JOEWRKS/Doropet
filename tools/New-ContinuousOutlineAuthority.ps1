[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$MaskPath,

    [Parameter(Mandatory = $true)]
    [string]$NativePath,

    [Parameter(Mandatory = $true)]
    [string]$AuthorityPath,

    [Parameter(Mandatory = $true)]
    [string]$EvidenceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$expectedSourceHash = 'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504'
$expectedMaskHash = 'D08B3A941C662F1CBC55C486C13FD4C6CD8901DA9CD5CF8512509698219FE46F'
$expectedNativeHash = '238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511'
$expectedAuthorityHash = 'DDF749007995B3F03781A3A51467013F406C5F7A2AA0480212523A79EF31F17F'
$expectedHairBytesHash = '4942259408D151BABE64BB131334CD9E2F7111876B95C5C488667254315D7FD6'
$expectedContourHash = 'A29D007B699A16B555FE5133854E832FEFD8409EA85F2FECE3EEA97BF444FD65'

Import-Module -Force (Join-Path $PSScriptRoot 'Dororong.SubpixelOutline.psm1')

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition)
    { throw $Message }
}

function Assert-Hash([string]$Path, [string]$Expected, [string]$Label)
{
    Assert-True (Test-Path -LiteralPath $Path -PathType Leaf) "$Label is missing."
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
    if ($actual -ne $Expected)
    { throw "$Label hash changed. Expected '$Expected', observed '$actual'." }
}

function Get-Sha256Text([string]$Text)
{
    $sha=[Security.Cryptography.SHA256]::Create()
    try
    { return [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text))) }
    finally
    { $sha.Dispose() }
}

function Assert-HairAnchorBytes([string]$AuthorityPath)
{
    $text=[IO.File]::ReadAllText($AuthorityPath)
    $start=$text.IndexOf('    HairAnchors = @(',[StringComparison]::Ordinal)
    $end=$text.IndexOf('    BodyNormals = @(',[StringComparison]::Ordinal)
    Assert-True ($start -ge 0 -and $end -gt $start) `
        'Hair anchor byte range is missing.'
    $hash=Get-Sha256Text $text.Substring($start,$end-$start)
    Assert-True ($hash -eq $expectedHairBytesHash) `
        "Hair anchor bytes changed. Expected '$expectedHairBytesHash', observed '$hash'."
}

function New-CleanedSource([System.Drawing.Bitmap]$Source)
{
    $exterior=New-Object 'bool[,]' $Source.Width,$Source.Height
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
        if($exterior[$x,$y])
        { continue }
        $color=$Source.GetPixel($x,$y)
        if([Math]::Min($color.R,[Math]::Min($color.G,$color.B)) -lt 225)
        { continue }
        $exterior[$x,$y]=$true
        foreach($offset in @(@(-1,0),@(1,0),@(0,-1),@(0,1)))
        {
            $neighborX=$x+$offset[0]; $neighborY=$y+$offset[1]
            if($neighborX -ge 0 -and $neighborY -ge 0 -and `
                $neighborX -lt $Source.Width -and $neighborY -lt $Source.Height -and `
                -not $exterior[$neighborX,$neighborY])
            { $queue.Enqueue(@($neighborX,$neighborY)) }
        }
    }

    $cleaned=[System.Drawing.Bitmap]::new(
        $Source.Width,$Source.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        for($y=0;$y -lt $Source.Height;$y++)
        {
            for($x=0;$x -lt $Source.Width;$x++)
            {
                if($exterior[$x,$y])
                { $cleaned.SetPixel($x,$y,[System.Drawing.Color]::Transparent) }
                else
                { $cleaned.SetPixel($x,$y,$Source.GetPixel($x,$y)) }
            }
        }
        return $cleaned
    }
    catch
    {
        $cleaned.Dispose()
        throw
    }
}

function Get-NamedContourSegments([object]$Contour,[string]$Name)
{
    $windows=@{
        FrontOuter=@{MinX=36.5;MaxX=39.5;MinY=160.5;MaxY=171.5}
        FrontFoot=@{MinX=48.5;MaxX=62.5;MinY=189.5;MaxY=193.5}
        FrontInner=@{MinX=62.5;MaxX=65.5;MinY=176.5;MaxY=186.5}
        FirstValley=@{MinX=66.5;MaxX=73.5;MinY=176.5;MaxY=181.5}
        FirstUnderside=@{MinX=73.5;MaxX=79.0;MinY=178.5;MaxY=184.5}
        CenterOuter=@{MinX=79.5;MaxX=83.5;MinY=181.5;MaxY=191.5}
        CenterFoot=@{MinX=96.5;MaxX=110.5;MinY=203.5;MaxY=206.5}
        CenterInner=@{MinX=112.5;MaxX=116.5;MinY=183.5;MaxY=191.5}
        SecondValley=@{MinX=138.5;MaxX=145.5;MinY=171.5;MaxY=178.5}
        SecondUnderside=@{MinX=123.5;MaxX=129.5;MinY=176.5;MaxY=181.5}
        RearOuter=@{MinX=164.5;MaxX=168.5;MinY=174.5;MaxY=185.5}
        RearFoot=@{MinX=149.5;MaxX=161.5;MinY=196.5;MaxY=200.5}
        RearInner=@{MinX=137.5;MaxX=141.5;MinY=176.5;MaxY=184.5}
        UpperRearRim=@{MinX=174.5;MaxX=178.5;MinY=122.5;MaxY=135.5}
        LowerRearRim=@{MinX=174.5;MaxX=178.5;MinY=148.5;MaxY=160.5}
    }
    Assert-True $windows.ContainsKey($Name) `
        "Body normal '$Name' has no named contour window."
    $window=$windows[$Name]
    return @($Contour.Segments|Where-Object {
        $midX=($_.X1+$_.X2)/2.0; $midY=($_.Y1+$_.Y2)/2.0
        $midX -ge $window.MinX -and $midX -le $window.MaxX -and `
        $midY -ge $window.MinY -and $midY -le $window.MaxY })
}

function Get-StrictIntersections([hashtable]$Normal,[object]$Segments)
{
    $px=$Normal.X1Eighth/8.0; $py=$Normal.Y1Eighth/8.0
    $rx=($Normal.X2Eighth-$Normal.X1Eighth)/8.0
    $ry=($Normal.Y2Eighth-$Normal.Y1Eighth)/8.0
    $epsilon=0.000000001
    $hits=@()
    foreach($segment in @($Segments))
    {
        $qx=[double]$segment.X1; $qy=[double]$segment.Y1
        $sx=[double]$segment.X2-$qx; $sy=[double]$segment.Y2-$qy
        $cross=($rx*$sy)-($ry*$sx)
        if([Math]::Abs($cross) -le $epsilon)
        { continue }
        $qpx=$qx-$px; $qpy=$qy-$py
        $t=(($qpx*$sy)-($qpy*$sx))/$cross
        $u=(($qpx*$ry)-($qpy*$rx))/$cross
        if($t -lt -$epsilon -or $t -gt (1.0+$epsilon) -or `
            $u -lt -$epsilon -or $u -gt (1.0+$epsilon))
        { continue }
        $hits += [pscustomobject]@{
            X=$px+($t*$rx); Y=$py+($t*$ry); T=$t; U=$u; Segment=$segment
            IsStrict=$t -gt $epsilon -and $t -lt (1.0-$epsilon) -and `
                $u -gt $epsilon -and $u -lt (1.0-$epsilon) }
    }
    return @($hits)
}

function Get-MaskSide([System.Drawing.Bitmap]$Mask,[double]$X,[double]$Y)
{
    $pixelX=[int][Math]::Floor($X+0.5)
    $pixelY=[int][Math]::Floor($Y+0.5)
    Assert-True ($pixelX -ge 0 -and $pixelY -ge 0 -and `
        $pixelX -lt $Mask.Width -and $pixelY -lt $Mask.Height) `
        "Ownership sample ($X,$Y) is outside the final mask."
    return $Mask.GetPixel($pixelX,$pixelY).R
}

function Assert-FinalContourNormal(
    [hashtable]$Entry,
    [System.Drawing.Bitmap]$Mask,
    [object]$Contour)
{
    $label="Body normal '$($Entry.Name)'"
    $namedHits=@(Get-StrictIntersections $Entry.SourceNormal `
        (Get-NamedContourSegments $Contour $Entry.Name))
    $strictNamed=@($namedHits|Where-Object IsStrict)
    Assert-True ($strictNamed.Count -eq 1) `
        "$label must cross exactly one named E/C segment strictly inside it."
    $allHits=@(Get-StrictIntersections $Entry.SourceNormal $Contour.Segments)
    Assert-True ($allHits.Count -eq 1) `
        "$label crosses a neighboring or second canonical segment."
    Assert-True $allHits[0].IsStrict `
        "$label crosses a contour vertex or junction."

    $normalX=($Entry.SourceNormal.X2Eighth-$Entry.SourceNormal.X1Eighth)/8.0
    $normalY=($Entry.SourceNormal.Y2Eighth-$Entry.SourceNormal.Y1Eighth)/8.0
    $segment=$strictNamed[0].Segment
    $tangentX=[double]$segment.X2-[double]$segment.X1
    $tangentY=[double]$segment.Y2-[double]$segment.Y1
    $dot=[Math]::Abs(($normalX*$tangentX)+($normalY*$tangentY))/`
        ([Math]::Sqrt(($normalX*$normalX)+($normalY*$normalY))*`
         [Math]::Sqrt(($tangentX*$tangentX)+($tangentY*$tangentY)))
    Assert-True ($dot -le 0.0871557427476582) `
        "$label is more than 5 degrees from perpendicular; absolute unit dot=$dot."

    Assert-True ((Get-MaskSide $Mask `
        ($Entry.SourceNormal.X1Eighth/8.0) ($Entry.SourceNormal.Y1Eighth/8.0)) -eq 255) `
        "$label is reversed: first endpoint is not on the body side."
    Assert-True ((Get-MaskSide $Mask `
        ($Entry.SourceNormal.X2Eighth/8.0) ($Entry.SourceNormal.Y2Eighth/8.0)) -eq 0) `
        "$label final endpoint is not on the non-body side."
    return $strictNamed[0]
}

function Get-Rec709Luminance([System.Drawing.Color]$Color)
{
    return (0.2126*$Color.R)+(0.7152*$Color.G)+(0.0722*$Color.B)
}

function Assert-Normal(
    [hashtable]$Entry,
    [string]$Field,
    [System.Drawing.Bitmap]$Bitmap,
    [string]$Label)
{
    Assert-True $Entry.ContainsKey($Field) "$Label is missing $Field."
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

function Assert-ReferencePoint(
    [hashtable]$Entry,
    [string]$Field,
    [System.Drawing.Bitmap]$Bitmap,
    [string]$Label)
{
    Assert-True $Entry.ContainsKey($Field) "$Label is missing $Field."
    $point = @($Entry[$Field])
    Assert-True ($point.Count -eq 2) "$Label $Field is not an X/Y pair."
    Assert-True ($point[0] -is [int] -and $point[1] -is [int]) `
        "$Label $Field contains a non-integer coordinate."
    $x=[int]$point[0]; $y=[int]$point[1]
    Assert-True ($x -ge 0 -and $x -lt $Bitmap.Width -and $y -ge 0 -and $y -lt $Bitmap.Height) `
        "$Label $Field coordinate ($x,$y) is outside the reference bitmap."
    Assert-True ($Bitmap.GetPixel($x,$y).A -eq 255) `
        "$Label $Field coordinate ($x,$y) is not opaque."
}

function Assert-FillExceedsInk(
    [hashtable]$Entry,
    [string]$FillField,
    [string]$InkField,
    [System.Drawing.Bitmap]$Bitmap,
    [string]$Label,
    [string]$Surface)
{
    $fillPoint=@($Entry[$FillField]); $inkPoint=@($Entry[$InkField])
    $fill=$Bitmap.GetPixel([int]$fillPoint[0],[int]$fillPoint[1])
    $ink=$Bitmap.GetPixel([int]$inkPoint[0],[int]$inkPoint[1])
    Assert-True ((Get-Rec709Luminance $fill) -gt (Get-Rec709Luminance $ink)) `
        "$Label $Surface fill luminance must exceed ink luminance."
}

function New-OverlayBitmap([System.Drawing.Bitmap]$Reference)
{
    $overlay=[System.Drawing.Bitmap]::new(
        $Reference.Width,$Reference.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics=[System.Drawing.Graphics]::FromImage($overlay)
    try
    {
        $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.DrawImageUnscaled($Reference,0,0)
    }
    finally
    { $graphics.Dispose() }
    return $overlay
}

function Draw-EndpointLabel(
    [System.Drawing.Graphics]$Graphics,
    [string]$Text,
    [single]$X,
    [single]$Y,
    [System.Drawing.Font]$Font,
    [System.Drawing.Brush]$Brush)
{
    $textSize=$Graphics.MeasureString($Text,$Font)
    $labelX=[Math]::Clamp($X+1.5,0.0,[double]($Graphics.VisibleClipBounds.Width-$textSize.Width))
    $labelY=[Math]::Clamp($Y-$textSize.Height-1.0,0.0,[double]($Graphics.VisibleClipBounds.Height-$textSize.Height))
    $background=[System.Drawing.RectangleF]::new(
        [single]$labelX,[single]$labelY,[single]$textSize.Width,[single]$textSize.Height)
    $Graphics.FillRectangle([System.Drawing.Brushes]::White,$background)
    $Graphics.DrawString($Text,$Font,$Brush,[single]$labelX,[single]$labelY)
}

function Draw-Normal(
    [System.Drawing.Graphics]$Graphics,
    [hashtable]$Normal,
    [string]$Label,
    [System.Drawing.Pen]$Pen,
    [System.Drawing.Brush]$Brush,
    [System.Drawing.Font]$Font,
    [single]$MarkerRadius)
{
    $x1=[single]($Normal.X1Eighth/8.0); $y1=[single]($Normal.Y1Eighth/8.0)
    $x2=[single]($Normal.X2Eighth/8.0); $y2=[single]($Normal.Y2Eighth/8.0)
    $Graphics.DrawLine($Pen,$x1,$y1,$x2,$y2)
    $diameter=2.0*$MarkerRadius
    $Graphics.FillEllipse($Brush,$x1-$MarkerRadius,$y1-$MarkerRadius,$diameter,$diameter)
    $Graphics.FillEllipse($Brush,$x2-$MarkerRadius,$y2-$MarkerRadius,$diameter,$diameter)
    Draw-EndpointLabel $Graphics "${Label}a" $x1 $y1 $Font $Brush
    Draw-EndpointLabel $Graphics "${Label}b" $x2 $y2 $Font $Brush
}

function Draw-ReferencePoint(
    [System.Drawing.Graphics]$Graphics,
    [object]$Point,
    [System.Drawing.Brush]$Brush,
    [single]$Radius)
{
    $coordinates=@($Point)
    $x=[single]$coordinates[0]; $y=[single]$coordinates[1]
    $diameter=2.0*$Radius
    $Graphics.FillEllipse([System.Drawing.Brushes]::White,
        $x-$Radius-0.5,$y-$Radius-0.5,$diameter+1.0,$diameter+1.0)
    $Graphics.FillEllipse($Brush,$x-$Radius,$y-$Radius,$diameter,$diameter)
}

function Draw-GeometryAnnotations(
    [System.Drawing.Graphics]$Graphics,
    [object]$Contour,
    [hashtable]$Crossings,
    [object]$LegalEndpoints,
    [object]$ProtectedPoints,
    [double]$Scale,
    [bool]$DrawCrossings)
{
    $continuationPen=[System.Drawing.Pen]::new(
        [System.Drawing.Color]::FromArgb(255,170,0,210),[single](1.4*$Scale))
    $tangentPen=[System.Drawing.Pen]::new(
        [System.Drawing.Color]::FromArgb(255,255,145,0),[single](1.2*$Scale))
    $crossingBrush=[System.Drawing.SolidBrush]::new(
        [System.Drawing.Color]::FromArgb(255,255,235,0))
    $protectedBrush=[System.Drawing.SolidBrush]::new(
        [System.Drawing.Color]::FromArgb(255,0,190,210))
    try
    {
        foreach($segment in @($Contour.ContinuationSegments))
        {
            $Graphics.DrawLine($continuationPen,
                [single]($segment.X1*$Scale),[single]($segment.Y1*$Scale),
                [single]($segment.X2*$Scale),[single]($segment.Y2*$Scale))
        }
        foreach($endpoint in @($LegalEndpoints))
        {
            $x=[single]($endpoint.X*$Scale); $y=[single]($endpoint.Y*$Scale)
            $radius=[single](1.8*$Scale)
            $Graphics.FillRectangle($crossingBrush,$x-$radius,$y-$radius,2*$radius,2*$radius)
        }
        foreach($point in @($ProtectedPoints))
        {
            $x=[single]($point.X*$Scale); $y=[single]($point.Y*$Scale)
            $radius=[single](1.2*$Scale)
            $Graphics.FillEllipse($protectedBrush,$x-$radius,$y-$radius,2*$radius,2*$radius)
        }
        if($DrawCrossings)
        {
            foreach($name in $Crossings.Keys)
            {
                $hit=$Crossings[$name]
                $x=[single]($hit.X*$Scale); $y=[single]($hit.Y*$Scale)
                $segment=$hit.Segment
                $tangentX=[double]$segment.X2-[double]$segment.X1
                $tangentY=[double]$segment.Y2-[double]$segment.Y1
                $length=[Math]::Sqrt(($tangentX*$tangentX)+($tangentY*$tangentY))
                $tangentX=2.5*$Scale*$tangentX/$length
                $tangentY=2.5*$Scale*$tangentY/$length
                $Graphics.DrawLine($tangentPen,
                    [single]($x-$tangentX),[single]($y-$tangentY),
                    [single]($x+$tangentX),[single]($y+$tangentY))
                $radius=[single](1.4*$Scale)
                $Graphics.FillEllipse($crossingBrush,
                    $x-$radius,$y-$radius,2*$radius,2*$radius)
            }
        }
    }
    finally
    {
        $protectedBrush.Dispose()
        $crossingBrush.Dispose()
        $tangentPen.Dispose()
        $continuationPen.Dispose()
    }
}

function Add-AuthorityDrawing(
    [System.Drawing.Bitmap]$Overlay,
    [object[]]$HairAnchors,
    [object[]]$BodyNormals,
    [string]$NormalPrefix,
    [string]$FillPrefix,
    [string]$InkPrefix,
    [bool]$NativeScale,
    [object]$Contour,
    [hashtable]$Crossings,
    [object]$LegalEndpoints,
    [object]$ProtectedPoints)
{
    $graphics=[System.Drawing.Graphics]::FromImage($Overlay)
    $hairPen=$null; $bodyPen=$null; $font=$null
    try
    {
        $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $graphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
        $lineWidth=if($NativeScale){0.8}else{1.2}
        $markerRadius=if($NativeScale){0.8}else{1.2}
        $pointRadius=if($NativeScale){0.9}else{1.4}
        $fontSize=if($NativeScale){3.2}else{5.0}
        $hairColor=[System.Drawing.Color]::FromArgb(255,0,90,255)
        $bodyColor=[System.Drawing.Color]::FromArgb(255,230,25,35)
        $hairBrush=[System.Drawing.SolidBrush]::new($hairColor)
        $bodyBrush=[System.Drawing.SolidBrush]::new($bodyColor)
        try
        {
            $geometryScale=if($NativeScale){96.0/225.0}else{1.0}
            Draw-GeometryAnnotations $graphics $Contour $Crossings `
                $LegalEndpoints $ProtectedPoints $geometryScale $true
            $hairPen=[System.Drawing.Pen]::new($hairColor,[single]$lineWidth)
            $bodyPen=[System.Drawing.Pen]::new($bodyColor,[single]$lineWidth)
            $font=[System.Drawing.Font]::new(
                [System.Drawing.FontFamily]::GenericSansSerif,[single]$fontSize,
                [System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
            for($index=0;$index -lt $HairAnchors.Count;$index++)
            {
                $entry=$HairAnchors[$index]
                Draw-Normal $graphics $entry[$NormalPrefix] "H$($index+1)" `
                    $hairPen $hairBrush $font $markerRadius
                Draw-ReferencePoint $graphics $entry[$FillPrefix] `
                    ([System.Drawing.Brushes]::LimeGreen) $pointRadius
                Draw-ReferencePoint $graphics $entry[$InkPrefix] `
                    ([System.Drawing.Brushes]::Black) $pointRadius
            }
            for($index=0;$index -lt $BodyNormals.Count;$index++)
            {
                $entry=$BodyNormals[$index]
                Draw-Normal $graphics $entry[$NormalPrefix] "B$($index+1)" `
                    $bodyPen $bodyBrush $font $markerRadius
                Draw-ReferencePoint $graphics $entry[$FillPrefix] `
                    ([System.Drawing.Brushes]::LimeGreen) $pointRadius
            }
        }
        finally
        {
            if($null-ne$font){$font.Dispose()}
            if($null-ne$bodyPen){$bodyPen.Dispose()}
            if($null-ne$hairPen){$hairPen.Dispose()}
            $bodyBrush.Dispose()
            $hairBrush.Dispose()
        }
    }
    finally
    { $graphics.Dispose() }
}

function Save-NearestNeighbor(
    [System.Drawing.Bitmap]$Bitmap,
    [string]$Path,
    [int]$Scale)
{
    $scaled=[System.Drawing.Bitmap]::new(
        $Bitmap.Width*$Scale,$Bitmap.Height*$Scale,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        $graphics=[System.Drawing.Graphics]::FromImage($scaled)
        try
        {
            $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.DrawImage(
                $Bitmap,
                [System.Drawing.Rectangle]::new(0,0,$scaled.Width,$scaled.Height),
                [System.Drawing.Rectangle]::new(0,0,$Bitmap.Width,$Bitmap.Height),
                [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally
        { $graphics.Dispose() }
        $scaled.Save($Path,[System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally
    { $scaled.Dispose() }
}

$fullSourcePath=[System.IO.Path]::GetFullPath($SourcePath)
$fullMaskPath=[System.IO.Path]::GetFullPath($MaskPath)
$fullNativePath=[System.IO.Path]::GetFullPath($NativePath)
$fullAuthorityPath=[System.IO.Path]::GetFullPath($AuthorityPath)
$fullEvidenceDirectory=[System.IO.Path]::GetFullPath($EvidenceDirectory)

Assert-Hash $fullSourcePath $expectedSourceHash 'Canonical source'
Assert-Hash $fullMaskPath $expectedMaskHash 'Final body-region mask'
Assert-Hash $fullNativePath $expectedNativeHash 'Committed native-open authority'
Assert-True (Test-Path -LiteralPath $fullAuthorityPath -PathType Leaf) `
    'Continuous authority fixture is missing.'
Assert-HairAnchorBytes $fullAuthorityPath

$authority=Import-PowerShellDataFile -LiteralPath $fullAuthorityPath
$source=[System.Drawing.Bitmap]::new($fullSourcePath)
$native=[System.Drawing.Bitmap]::new($fullNativePath)
$mask=[System.Drawing.Bitmap]::new($fullMaskPath)
$cleaned=$null
try
{
    Assert-True ($source.Width-eq225-and$source.Height-eq225) `
        'Canonical source dimensions changed.'
    Assert-True ($native.Width-eq96-and$native.Height-eq96) `
        'Committed native-open dimensions changed.'
    Assert-True ($mask.Width-eq225-and$mask.Height-eq225) `
        'Final body-region mask dimensions changed.'

    Assert-True $authority.ContainsKey('LegalEndpoints') `
        'Literal LegalEndpoints are missing.'
    Assert-True ((@($authority.LegalEndpoints|ForEach-Object{"$($_.X),$($_.Y)"})-join '|') `
        -eq '118,151|161,116') 'Literal LegalEndpoints changed.'
    Assert-True (@($authority.ProtectedPoints|Where-Object Name -like 'LegalEndpoint-*').Count -eq 0) `
        'Historical protected endpoint reintroduced.'

    $cleaned=New-CleanedSource $source
    $legalEndpointPoints=[System.Drawing.PointF[]]@($authority.LegalEndpoints|ForEach-Object {
        [System.Drawing.PointF]::new([single]$_.X,[single]$_.Y) })
    $contour=New-DororongVisibleContour $cleaned $mask $legalEndpointPoints
    $contourHash=Get-DororongCanonicalContourHash $contour
    Assert-True ($contourHash -eq $expectedContourHash) `
        "Final contour hash changed. Expected '$expectedContourHash', observed '$contourHash'."

    $hairAnchors=@($authority.HairAnchors)
    $bodyNormals=@($authority.BodyNormals)
    Assert-True ($hairAnchors.Count -eq 6) 'Hair anchor count changed.'
    Assert-True ($bodyNormals.Count -eq 15) 'Body normal count changed.'

    foreach($entry in $hairAnchors)
    {
        $label="Hair anchor '$($entry.Name)'"
        Assert-Normal $entry 'SourceNormal' $source $label
        Assert-Normal $entry 'NativeNormal' $native $label
        Assert-ReferencePoint $entry 'SourceFill' $source $label
        Assert-ReferencePoint $entry 'SourceInk' $source $label
        Assert-ReferencePoint $entry 'NativeFill' $native $label
        Assert-ReferencePoint $entry 'NativeInk' $native $label
        Assert-FillExceedsInk $entry 'SourceFill' 'SourceInk' $source $label 'source'
        Assert-FillExceedsInk $entry 'NativeFill' 'NativeInk' $native $label 'native'
    }

    foreach($entry in $bodyNormals)
    {
        $label="Body normal '$($entry.Name)'"
        Assert-Normal $entry 'SourceNormal' $source $label
        Assert-Normal $entry 'NativeNormal' $native $label
        Assert-ReferencePoint $entry 'SourceFill' $source $label
        Assert-ReferencePoint $entry 'NativeFill' $native $label
    }

    $crossings=@{}
    foreach($entry in $bodyNormals)
    { $crossings[$entry.Name]=Assert-FinalContourNormal $entry $mask $contour }

    foreach($point in @($authority.ProtectedPoints))
    {
        $x=[int]$point.X; $y=[int]$point.Y
        Assert-True ($x-ge0-and$x-lt$mask.Width-and$y-ge0-and$y-lt$mask.Height) `
            "Protected point '$($point.Name)' is outside the reviewed mask."
        Assert-True ($mask.GetPixel($x,$y).R -eq 0) `
            "Protected point '$($point.Name)' is writable in the approved mask."
    }

    $authorityHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $fullAuthorityPath).Hash
    if($authorityHash -ne $expectedAuthorityHash)
    {
        throw "Continuous authority hash changed. Expected '$expectedAuthorityHash', observed '$authorityHash'."
    }

    $outputPaths=@(
        (Join-Path $fullEvidenceDirectory 'dororong-continuous-authority-source.png')
        (Join-Path $fullEvidenceDirectory 'dororong-continuous-authority-source-4x.png')
        (Join-Path $fullEvidenceDirectory 'dororong-continuous-authority-native.png')
        (Join-Path $fullEvidenceDirectory 'dororong-continuous-authority-native-4x.png'))
    foreach($outputPath in $outputPaths)
    {
        Assert-True (-not(Test-Path -LiteralPath $outputPath)) `
            "Authority evidence output already exists: $outputPath"
    }
    $sourceOverlay=New-OverlayBitmap $source
    $nativeOverlay=New-OverlayBitmap $native
    $evidenceDirectoryCreated=-not(Test-Path -LiteralPath $fullEvidenceDirectory)
    try
    {
        Add-AuthorityDrawing $sourceOverlay $hairAnchors $bodyNormals `
            'SourceNormal' 'SourceFill' 'SourceInk' $false $contour $crossings `
            $authority.LegalEndpoints $authority.ProtectedPoints
        Add-AuthorityDrawing $nativeOverlay $hairAnchors $bodyNormals `
            'NativeNormal' 'NativeFill' 'NativeInk' $true $contour $crossings `
            $authority.LegalEndpoints $authority.ProtectedPoints
        [System.IO.Directory]::CreateDirectory($fullEvidenceDirectory)|Out-Null
        $sourceOverlay.Save($outputPaths[0],[System.Drawing.Imaging.ImageFormat]::Png)
        Save-NearestNeighbor $sourceOverlay $outputPaths[1] 4
        $nativeOverlay.Save($outputPaths[2],[System.Drawing.Imaging.ImageFormat]::Png)
        Save-NearestNeighbor $nativeOverlay $outputPaths[3] 4
    }
    catch
    {
        foreach($outputPath in $outputPaths)
        {
            if([System.IO.File]::Exists($outputPath))
            { [System.IO.File]::Delete($outputPath) }
        }
        if($evidenceDirectoryCreated -and [System.IO.Directory]::Exists($fullEvidenceDirectory) -and `
            @(Get-ChildItem -LiteralPath $fullEvidenceDirectory -Force).Count -eq 0)
        { [System.IO.Directory]::Delete($fullEvidenceDirectory,$false) }
        throw
    }
    finally
    {
        $nativeOverlay.Dispose()
        $sourceOverlay.Dispose()
    }

    Write-Output "CONTINUOUS AUTHORITY OVERLAYS hash=$authorityHash"
    foreach($outputPath in $outputPaths)
    {
        $hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $outputPath).Hash
        Write-Output "OVERLAY path=$outputPath sha256=$hash"
    }
}
finally
{
    if($null-ne$cleaned){$cleaned.Dispose()}
    $mask.Dispose()
    $native.Dispose()
    $source.Dispose()
}
