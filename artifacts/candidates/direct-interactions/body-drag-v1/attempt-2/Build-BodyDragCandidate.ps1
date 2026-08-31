param(
    [Parameter(Mandatory = $true)][string]$RepositoryRoot,
    [Parameter(Mandatory = $true)][string]$CandidateDirectory,
    [Parameter(Mandatory = $true)][string]$VerificationDirectory)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName PresentationCore

$repo = [IO.Path]::GetFullPath($RepositoryRoot)
$candidate = [IO.Path]::GetFullPath($CandidateDirectory)
$verification = [IO.Path]::GetFullPath($VerificationDirectory)
$assetRoot = Join-Path $repo 'src\Dororong.App\Assets'
$sourceRoot = Join-Path $assetRoot 'frame-sources'
$canonicalPath = Join-Path $assetRoot 'dororong-canonical.png'
$canonicalHash = '699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78'
$sheetReferencePath = Join-Path $repo 'artifacts\candidates\direct-interactions\body-drag-v1\attempt-1\generated-sheet-attempt-2.png'
$sheetReferenceHash = 'A01188C35421680EF80FDCD14D36486FF36806B38E928FDEB7920B2FC88F445F'

if ((Get-FileHash -LiteralPath $canonicalPath -Algorithm SHA256).Hash -ne $canonicalHash) { throw 'Canonical runtime authority changed.' }
if ((Get-FileHash -LiteralPath $sheetReferencePath -Algorithm SHA256).Hash -ne $sheetReferenceHash) { throw 'Generated silhouette reference changed.' }

$frameDirectory = Join-Path $candidate 'frames'
New-Item -ItemType Directory -Force -Path $frameDirectory,$verification,$sourceRoot | Out-Null
if (@(Get-ChildItem -LiteralPath $frameDirectory -Force).Count -ne 0) { throw "Attempt-2 frame path is not fresh: $frameDirectory" }

$entryNames = @(
    'body-drag-entry-00-press.png','body-drag-entry-01-lengthen.png','body-drag-entry-02-drop.png',
    'body-drag-entry-03-stretch.png','body-drag-entry-04-dangle.png','body-drag-entry-05-near-hang.png',
    'body-drag-entry-06-hang.png')
$settleNames = @(
    'body-drag-settle-00-hang.png','body-drag-settle-01-lift.png','body-drag-settle-02-gather.png',
    'body-drag-settle-03-land.png','body-drag-settle-04-recover.png')
$names = @($entryNames + $settleNames)

# The body warp descends gradually from canonical into the hang, then retracts through the same contour family.
$progress = @(0.0,0.25,0.40,0.55,0.70,0.85,1.0,1.0,0.84,0.60,0.25,0.0)

function New-TransparentBitmap([int]$Width,[int]$Height)
{
    $bitmap=[Drawing.Bitmap]::new($Width,$Height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics=[Drawing.Graphics]::FromImage($bitmap)
    try{$graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy;$graphics.Clear([Drawing.Color]::FromArgb(0,0,0,0))}
    finally{$graphics.Dispose()}
    return $bitmap
}

function Clear-TransparentRgb([Drawing.Bitmap]$Bitmap)
{
    for($y=0;$y-lt$Bitmap.Height;$y++){for($x=0;$x-lt$Bitmap.Width;$x++)
    {
        $pixel=$Bitmap.GetPixel($x,$y)
        if($pixel.A-eq0-and($pixel.R-ne0-or$pixel.G-ne0-or$pixel.B-ne0)){$Bitmap.SetPixel($x,$y,[Drawing.Color]::FromArgb(0,0,0,0))}
    }}
}

function New-BodyAuthority([Drawing.Bitmap]$Canonical,[string]$MaskPath)
{
    $sourceMask=[Drawing.Bitmap]::new($MaskPath)
    $mask=New-TransparentBitmap 96 96
    $graphics=[Drawing.Graphics]::FromImage($mask)
    try
    {
        $graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
        $graphics.DrawImage($sourceMask,[Drawing.Rectangle]::new(0,0,96,96),[Drawing.Rectangle]::new(0,0,$sourceMask.Width,$sourceMask.Height),[Drawing.GraphicsUnit]::Pixel)
    }
    finally{$graphics.Dispose();$sourceMask.Dispose()}
    $layer=New-TransparentBitmap 96 96
    for($y=0;$y-lt96;$y++){for($x=0;$x-lt96;$x++)
    {
        if($mask.GetPixel($x,$y).R-gt4){$layer.SetPixel($x,$y,$Canonical.GetPixel($x,$y))}
    }}
    Clear-TransparentRgb $layer
    return [pscustomobject]@{Mask=$mask;Layer=$layer}
}

function Get-VerticalSample([Drawing.Bitmap]$Layer,[int]$X,[double]$SourceY)
{
    if($SourceY-lt0-or$SourceY-gt95){return [Drawing.Color]::FromArgb(0,0,0,0)}
    $y0=[Math]::Max(0,[Math]::Min(95,[int][Math]::Floor($SourceY)))
    $y1=[Math]::Max(0,[Math]::Min(95,$y0+1))
    $mix=$SourceY-$y0
    $a=$Layer.GetPixel($X,$y0);$b=$Layer.GetPixel($X,$y1)
    $alpha=[int][Math]::Round($a.A+(($b.A-$a.A)*$mix))
    if($alpha-le0){return [Drawing.Color]::FromArgb(0,0,0,0)}
    $premultipliedR=(($a.R*$a.A/255.0)*(1.0-$mix))+(($b.R*$b.A/255.0)*$mix)
    $premultipliedG=(($a.G*$a.A/255.0)*(1.0-$mix))+(($b.G*$b.A/255.0)*$mix)
    $premultipliedB=(($a.B*$a.A/255.0)*(1.0-$mix))+(($b.B*$b.A/255.0)*$mix)
    return [Drawing.Color]::FromArgb(
        $alpha,
        [int][Math]::Max(0,[Math]::Min(255,[Math]::Round($premultipliedR*255.0/$alpha))),
        [int][Math]::Max(0,[Math]::Min(255,[Math]::Round($premultipliedG*255.0/$alpha))),
        [int][Math]::Max(0,[Math]::Min(255,[Math]::Round($premultipliedB*255.0/$alpha))))
}

function Test-ProtectedPixel([int]$X,[int]$Y)
{
    return ($Y-le58)-or($Y-le66-and$X-ge30-and$X-le46)-or($Y-le70-and$X-le29)-or($Y-le71-and$X-ge47-and$X-le64)-or($Y-le76-and$X-ge64)
}

function New-ProgressiveFrame(
    [Drawing.Bitmap]$Canonical,[Drawing.Bitmap]$BodyLayer,[double]$Progress)
{
    $frame=New-TransparentBitmap 96 96
    $anchorY=54.0
    $sourceBottoms=@(83.0,87.0,84.0,84.0)
    $targetBottoms=@(92.0,95.0,93.0,92.0)
    $horizontalShifts=@(6.0,0.0,7.0,-8.0)
    for($y=0;$y-lt96;$y++)
    {
        for($x=0;$x-lt96;$x++)
        {
            $leg=if($x-le37){0}elseif($x-le50){1}elseif($x-le64){2}else{3}
            $legFullScale=($targetBottoms[$leg]-$anchorY)/($sourceBottoms[$leg]-$anchorY)
            $legScale=1.0+($Progress*($legFullScale-1.0))
            $torsoScale=1.0+(0.08*$Progress)
            $blend=[Math]::Max(0.0,[Math]::Min(1.0,($y-66.0)/14.0))
            $scale=$torsoScale+(($legScale-$torsoScale)*$blend)
            $sourceY=$anchorY+(($y-$anchorY)/$scale)
            $sourceX=[int][Math]::Max(0,[Math]::Min(95,[Math]::Round($x+($horizontalShifts[$leg]*$Progress*$blend))))
            $pixel=Get-VerticalSample $BodyLayer $sourceX $sourceY
            if($pixel.A-gt0){$frame.SetPixel($x,$y,$pixel)}
        }
    }

    for($y=0;$y-lt96;$y++){for($x=0;$x-lt96;$x++)
    {
        if(Test-ProtectedPixel $x $y){$frame.SetPixel($x,$y,$Canonical.GetPixel($x,$y))}
    }}

    $fullValleys=@(76,78,77)
    $gapXs=@(38,51,65)
    $ink=[Drawing.Color]::FromArgb(255,26,2,10)
    for($gap=0;$gap-lt3;$gap++)
    {
        $valley=[int][Math]::Round(86+(($fullValleys[$gap]-86)*$Progress))
        $gapX=$gapXs[$gap]
        for($clearY=$valley+1;$clearY-lt96;$clearY++)
        {
            for($clearX=$gapX-1;$clearX-le$gapX+1;$clearX++)
            {
                if(-not(Test-ProtectedPixel $clearX $clearY)){$frame.SetPixel($clearX,$clearY,[Drawing.Color]::FromArgb(0,0,0,0))}
            }
        }
        foreach($outlineX in @(($gapX-2),($gapX+2)))
        {
            for($outlineY=$valley+2;$outlineY-lt95;$outlineY++)
            {
                if($frame.GetPixel($outlineX,$outlineY).A-gt32-and-not(Test-ProtectedPixel $outlineX $outlineY)){$frame.SetPixel($outlineX,$outlineY,$ink)}
            }
        }
        foreach($point in @(@(($gapX-1),($valley+1)),@($gapX,$valley),@(($gapX+1),($valley+1))))
        {
            if($frame.GetPixel($point[0],$point[1]).A-gt32-and-not(Test-ProtectedPixel $point[0] $point[1])){$frame.SetPixel($point[0],$point[1],$ink)}
        }
    }
    Clear-TransparentRgb $frame
    return $frame
}

function Save-Png([Drawing.Bitmap]$Bitmap,[string]$Path){$Bitmap.Save($Path,[Drawing.Imaging.ImageFormat]::Png)}

function New-Strip([Drawing.Bitmap[]]$Frames)
{
    $strip=New-TransparentBitmap (96*$Frames.Count) 96;$g=[Drawing.Graphics]::FromImage($strip)
    try{$g.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy;for($i=0;$i-lt$Frames.Count;$i++){$g.DrawImageUnscaled($Frames[$i],96*$i,0)}}
    finally{$g.Dispose()};return $strip
}

function New-Nearest4x([Drawing.Bitmap[]]$Frames)
{
    $strip=[Drawing.Bitmap]::new(384*$Frames.Count,384,[Drawing.Imaging.PixelFormat]::Format32bppArgb);$g=[Drawing.Graphics]::FromImage($strip)
    try
    {
        $g.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy;$g.Clear([Drawing.Color]::FromArgb(255,18,20,28))
        $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
        for($i=0;$i-lt$Frames.Count;$i++){$g.DrawImage($Frames[$i],[Drawing.Rectangle]::new(384*$i,0,384,384),0,0,96,96,[Drawing.GraphicsUnit]::Pixel)}
    }
    finally{$g.Dispose()};return $strip
}

function New-OnionStrip([Drawing.Bitmap[]]$Frames)
{
    $strip=New-TransparentBitmap (96*($Frames.Count-1)) 96
    for($i=0;$i-lt$Frames.Count-1;$i++){for($y=0;$y-lt96;$y++){for($x=0;$x-lt96;$x++)
    {
        $a=$Frames[$i].GetPixel($x,$y);$b=$Frames[$i+1].GetPixel($x,$y);$alpha=[Math]::Min(255,[int](($a.A+$b.A)/2))
        if($alpha-gt0){$strip.SetPixel(96*$i+$x,$y,[Drawing.Color]::FromArgb($alpha,[int](($a.R+$b.R)/2),[int](($a.G+$b.G)/2),[int](($a.B+$b.B)/2)))}
    }}};Clear-TransparentRgb $strip;return $strip
}

function New-DifferenceStrip([Drawing.Bitmap[]]$Frames)
{
    $strip=[Drawing.Bitmap]::new(96*($Frames.Count-1),96,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for($i=0;$i-lt$Frames.Count-1;$i++){for($y=0;$y-lt96;$y++){for($x=0;$x-lt96;$x++)
    {
        $a=$Frames[$i].GetPixel($x,$y);$b=$Frames[$i+1].GetPixel($x,$y)
        $delta=[Math]::Max([Math]::Abs($a.A-$b.A),[Math]::Max([Math]::Abs($a.R-$b.R),[Math]::Max([Math]::Abs($a.G-$b.G),[Math]::Abs($a.B-$b.B))))
        $strip.SetPixel(96*$i+$x,$y,[Drawing.Color]::FromArgb(255,$delta,$delta,$delta))
    }}};return $strip
}

function New-TimedGif([string[]]$FramePaths,[int[]]$DurationsMs,[string]$OutputPath)
{
    $encoder=[Windows.Media.Imaging.GifBitmapEncoder]::new()
    foreach($path in $FramePaths)
    {
        $stream=[IO.File]::OpenRead($path)
        try{$decoder=[Windows.Media.Imaging.PngBitmapDecoder]::new($stream,[Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,[Windows.Media.Imaging.BitmapCacheOption]::OnLoad);$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($decoder.Frames[0]))}
        finally{$stream.Dispose()}
    }
    $output=[IO.File]::Create($OutputPath);try{$encoder.Save($output)}finally{$output.Dispose()}
    $bytes=[Collections.Generic.List[byte]]::new([IO.File]::ReadAllBytes($OutputPath));$packed=$bytes[10]
    $globalTableBytes=if(($packed-band0x80)-ne0){3*(1-shl(($packed-band0x07)+1))}else{0}
    $bytes.InsertRange(13+$globalTableBytes,[byte[]](0x21,0xFF,0x0B,0x4E,0x45,0x54,0x53,0x43,0x41,0x50,0x45,0x32,0x2E,0x30,0x03,0x01,0x00,0x00,0x00))
    $controls=[Collections.Generic.List[int]]::new();for($i=0;$i-le$bytes.Count-8;$i++){if($bytes[$i]-eq0x21-and$bytes[$i+1]-eq0xF9-and$bytes[$i+2]-eq0x04){$controls.Add($i)}}
    if($controls.Count-ne$DurationsMs.Count){throw "GIF control count mismatch: $($controls.Count)"}
    for($i=0;$i-lt$controls.Count;$i++)
    {
        $delay=[int]([Math]::Max(2,[Math]::Round($DurationsMs[$i]/10.0)));$offset=$controls[$i]
        $bytes[$offset+3]=[byte](($bytes[$offset+3]-band0xE3)-bor0x09);$bytes[$offset+4]=[byte]($delay-band0xFF);$bytes[$offset+5]=[byte](($delay-shr8)-band0xFF)
    }
    [IO.File]::WriteAllBytes($OutputPath,$bytes.ToArray())
}

$canonical=[Drawing.Bitmap]::new($canonicalPath)
$bodyAuthority=New-BodyAuthority $canonical (Join-Path $assetRoot 'dororong-body-region-mask.png')
$frames=[Collections.Generic.List[Drawing.Bitmap]]::new()
try
{
    for($i=0;$i-lt$names.Count;$i++)
    {
        $path=Join-Path $frameDirectory $names[$i]
        if($i-eq0-or$i-eq11){Copy-Item -LiteralPath $canonicalPath -Destination $path}
        elseif($i-eq7){Copy-Item -LiteralPath (Join-Path $frameDirectory $entryNames[6]) -Destination $path}
        else
        {
            $frame=New-ProgressiveFrame $canonical $bodyAuthority.Layer $progress[$i]
            try{Save-Png $frame $path}finally{$frame.Dispose()}
        }
        $frames.Add([Drawing.Bitmap]::new($path))
    }

    $framePaths=@($names|ForEach-Object{Join-Path $frameDirectory $_})
    $native=New-Strip $frames.ToArray();try{Save-Png $native (Join-Path $verification 'body-drag-native-strip.png')}finally{$native.Dispose()}
    $nearest=New-Nearest4x $frames.ToArray();try{Save-Png $nearest (Join-Path $verification 'body-drag-nearest-4x-strip.png')}finally{$nearest.Dispose()}
    $onion=New-OnionStrip $frames.ToArray();try{Save-Png $onion (Join-Path $verification 'body-drag-onion-skin-strip.png')}finally{$onion.Dispose()}
    $difference=New-DifferenceStrip $frames.ToArray();try{Save-Png $difference (Join-Path $verification 'body-drag-difference-strip.png')}finally{$difference.Dispose()}
    New-TimedGif $framePaths ([int[]]@(50,50,50,50,50,50,80,160,55,55,55,80)) (Join-Path $verification 'body-drag-entry-hold-release-800ms.gif')

    $records=for($i=0;$i-lt$names.Count;$i++)
    {
        [ordered]@{Index=$i;Name=$names[$i];WarpProgress=$progress[$i];Sha256=(Get-FileHash -LiteralPath $framePaths[$i] -Algorithm SHA256).Hash}
    }
    [ordered]@{
        Candidate='body-drag-v1-attempt-2-canonical-body-warp-provisional-unverified';FrameCount=12;FrameWidth=96;FrameHeight=96
        TimelineMs=[ordered]@{Entry=380;Hold=160;Settle=260;Total=800};CanonicalSha256=$canonicalHash
        GeneratedSilhouetteReferencePath=$sheetReferencePath;GeneratedSilhouetteReferenceSha256=$sheetReferenceHash;Frames=$records
    }|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $verification 'body-drag-metrics.json') -Encoding utf8NoBOM

    # Promote only after the complete attempt-2 candidate/evidence package exists.
    for($i=0;$i-lt$names.Count;$i++)
    {
        Copy-Item -LiteralPath $framePaths[$i] -Destination (Join-Path $assetRoot $names[$i]) -Force
        Copy-Item -LiteralPath $framePaths[$i] -Destination (Join-Path $sourceRoot $names[$i]) -Force
    }
    'BODY DRAG ATTEMPT 2 BUILT: canonical body-mask warp with rounded U valleys plus native/4x/onion/difference/800ms GIF evidence.'
}
finally{foreach($frame in $frames){$frame.Dispose()};$bodyAuthority.Layer.Dispose();$bodyAuthority.Mask.Dispose();$canonical.Dispose()}
