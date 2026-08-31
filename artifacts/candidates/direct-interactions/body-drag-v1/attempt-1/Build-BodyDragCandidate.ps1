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

if ((Get-FileHash -LiteralPath $canonicalPath -Algorithm SHA256).Hash -ne $canonicalHash)
{
    throw 'Canonical runtime authority changed.'
}

$frameDirectory = Join-Path $candidate 'frames'
New-Item -ItemType Directory -Force -Path $frameDirectory,$verification,$sourceRoot | Out-Null

$entryNames = @(
    'body-drag-entry-00-press.png',
    'body-drag-entry-01-lengthen.png',
    'body-drag-entry-02-drop.png',
    'body-drag-entry-03-stretch.png',
    'body-drag-entry-04-dangle.png',
    'body-drag-entry-05-near-hang.png',
    'body-drag-entry-06-hang.png')
$settleNames = @(
    'body-drag-settle-00-hang.png',
    'body-drag-settle-01-lift.png',
    'body-drag-settle-02-gather.png',
    'body-drag-settle-03-land.png',
    'body-drag-settle-04-recover.png')
$names = @($entryNames + $settleNames)
$bottoms = @(88,89,90,92,94,95,95,95,94,92,90,88)
$shapeStages = @(0,1,2,3,4,5,6,6,5,3,1,0)

function New-TransparentBitmap([int]$Width,[int]$Height)
{
    $bitmap = [Drawing.Bitmap]::new($Width,$Height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try
    {
        $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.Clear([Drawing.Color]::FromArgb(0,0,0,0))
    }
    finally { $graphics.Dispose() }
    return $bitmap
}

function Clear-TransparentRgb([Drawing.Bitmap]$Bitmap)
{
    for ($y=0;$y-lt$Bitmap.Height;$y++)
    {
        for ($x=0;$x-lt$Bitmap.Width;$x++)
        {
            $pixel=$Bitmap.GetPixel($x,$y)
            if ($pixel.A-eq0-and($pixel.R-ne0-or$pixel.G-ne0-or$pixel.B-ne0))
            {
                $Bitmap.SetPixel($x,$y,[Drawing.Color]::FromArgb(0,0,0,0))
            }
        }
    }
}

function Add-Bezier(
    [Drawing.Drawing2D.GraphicsPath]$Path,[double]$Scale,
    [double]$X1,[double]$Y1,[double]$X2,[double]$Y2,
    [double]$X3,[double]$Y3,[double]$X4,[double]$Y4)
{
    $Path.AddBezier(
        [single]($X1*$Scale),[single]($Y1*$Scale),[single]($X2*$Scale),[single]($Y2*$Scale),
        [single]($X3*$Scale),[single]($Y3*$Scale),[single]($X4*$Scale),[single]($Y4*$Scale))
}

function New-HangingBody([int]$Bottom,[int]$ShapeStage)
{
    $scale=4.0
    $large=New-TransparentBitmap 384 384
    $graphics=[Drawing.Graphics]::FromImage($large)
    $path=[Drawing.Drawing2D.GraphicsPath]::new()
    $fill=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,250,250,250))
    $pen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,26,2,10),[single](1.25*$scale))
    $legTop=79-[Math]::Min(6,[Math]::Max(1,$ShapeStage))
    try
    {
        $graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
        $pen.LineJoin=[Drawing.Drawing2D.LineJoin]::Round
        $pen.StartCap=[Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap=[Drawing.Drawing2D.LineCap]::Round

        $path.StartFigure()
        Add-Bezier $path $scale 24 53 22 58 24 ($legTop-2) 25 $legTop

        Add-Bezier $path $scale 25 $legTop 25 ($Bottom-3) 27 $Bottom 31 $Bottom
        Add-Bezier $path $scale 31 $Bottom 35 $Bottom 36 ($Bottom-3) 36 ($Bottom-6)
        Add-Bezier $path $scale 36 ($Bottom-6) 36 ($legTop+2) 37 $legTop 38 $legTop

        Add-Bezier $path $scale 38 $legTop 39 ($legTop+1) 39 ($Bottom-5) 40 ($Bottom-2)
        Add-Bezier $path $scale 40 ($Bottom-2) 41 ($Bottom-1) 42 $Bottom 44 $Bottom
        Add-Bezier $path $scale 44 $Bottom 48 $Bottom 49 ($Bottom-3) 49 ($Bottom-6)
        Add-Bezier $path $scale 49 ($Bottom-6) 49 ($legTop+2) 50 $legTop 51 $legTop

        Add-Bezier $path $scale 51 $legTop 52 ($legTop+1) 52 ($Bottom-5) 53 ($Bottom-2)
        Add-Bezier $path $scale 53 ($Bottom-2) 54 ($Bottom-1) 55 $Bottom 57 $Bottom
        Add-Bezier $path $scale 57 $Bottom 61 $Bottom 62 ($Bottom-3) 62 ($Bottom-6)
        Add-Bezier $path $scale 62 ($Bottom-6) 62 ($legTop+2) 64 $legTop 65 $legTop

        Add-Bezier $path $scale 65 $legTop 67 ($legTop+1) 67 ($Bottom-5) 68 ($Bottom-2)
        Add-Bezier $path $scale 68 ($Bottom-2) 69 ($Bottom-1) 71 $Bottom 73 $Bottom
        Add-Bezier $path $scale 73 $Bottom 77 $Bottom 79 ($Bottom-3) 78 ($Bottom-7)
        Add-Bezier $path $scale 78 ($Bottom-7) 82 71 82 58 78 54
        Add-Bezier $path $scale 78 54 66 50 52 54 40 52
        Add-Bezier $path $scale 40 52 33 51 28 51 24 53
        $path.CloseFigure()

        $graphics.FillPath($fill,$path)
        $graphics.DrawPath($pen,$path)
    }
    finally { $pen.Dispose();$fill.Dispose();$path.Dispose();$graphics.Dispose() }

    $native=New-TransparentBitmap 96 96
    $g=[Drawing.Graphics]::FromImage($native)
    try
    {
        $g.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
        $g.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
        $g.DrawImage($large,[Drawing.Rectangle]::new(0,0,96,96),[Drawing.Rectangle]::new(0,0,384,384),[Drawing.GraphicsUnit]::Pixel)
    }
    finally { $g.Dispose();$large.Dispose() }
    Clear-TransparentRgb $native
    return $native
}

function Copy-CanonicalIdentity([Drawing.Bitmap]$Canonical,[Drawing.Bitmap]$Frame)
{
    for ($y=0;$y-lt96;$y++)
    {
        for ($x=0;$x-lt96;$x++)
        {
            $protected = ($y-le58) -or
                ($y-le70-and$x-le29) -or
                ($y-le71-and$x-ge47-and$x-le64) -or
                ($y-le76-and$x-ge64)
            if ($protected) { $Frame.SetPixel($x,$y,$Canonical.GetPixel($x,$y)) }
        }
    }
    Clear-TransparentRgb $Frame
}

function Save-Png([Drawing.Bitmap]$Bitmap,[string]$Path)
{
    $Bitmap.Save($Path,[Drawing.Imaging.ImageFormat]::Png)
}

function New-Strip([Drawing.Bitmap[]]$Frames)
{
    $strip=New-TransparentBitmap (96*$Frames.Count) 96
    $graphics=[Drawing.Graphics]::FromImage($strip)
    try
    {
        $graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
        for ($i=0;$i-lt$Frames.Count;$i++) { $graphics.DrawImageUnscaled($Frames[$i],96*$i,0) }
    }
    finally { $graphics.Dispose() }
    return $strip
}

function New-Nearest4x([Drawing.Bitmap[]]$Frames)
{
    $strip=[Drawing.Bitmap]::new(384*$Frames.Count,384,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics=[Drawing.Graphics]::FromImage($strip)
    try
    {
        $graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.Clear([Drawing.Color]::FromArgb(255,18,20,28))
        $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
        for ($i=0;$i-lt$Frames.Count;$i++)
        {
            $graphics.DrawImage($Frames[$i],[Drawing.Rectangle]::new(384*$i,0,384,384),0,0,96,96,[Drawing.GraphicsUnit]::Pixel)
        }
    }
    finally { $graphics.Dispose() }
    return $strip
}

function New-OnionStrip([Drawing.Bitmap[]]$Frames)
{
    $strip=New-TransparentBitmap (96*($Frames.Count-1)) 96
    for ($i=0;$i-lt$Frames.Count-1;$i++)
    {
        for ($y=0;$y-lt96;$y++)
        {
            for ($x=0;$x-lt96;$x++)
            {
                $a=$Frames[$i].GetPixel($x,$y);$b=$Frames[$i+1].GetPixel($x,$y)
                $alpha=[Math]::Min(255,[int](($a.A+$b.A)/2))
                if ($alpha-gt0)
                {
                    $strip.SetPixel(96*$i+$x,$y,[Drawing.Color]::FromArgb($alpha,[int](($a.R+$b.R)/2),[int](($a.G+$b.G)/2),[int](($a.B+$b.B)/2)))
                }
            }
        }
    }
    Clear-TransparentRgb $strip
    return $strip
}

function New-DifferenceStrip([Drawing.Bitmap[]]$Frames)
{
    $strip=[Drawing.Bitmap]::new(96*($Frames.Count-1),96,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($i=0;$i-lt$Frames.Count-1;$i++)
    {
        for ($y=0;$y-lt96;$y++)
        {
            for ($x=0;$x-lt96;$x++)
            {
                $a=$Frames[$i].GetPixel($x,$y);$b=$Frames[$i+1].GetPixel($x,$y)
                $delta=[Math]::Max([Math]::Abs($a.A-$b.A),[Math]::Max([Math]::Abs($a.R-$b.R),[Math]::Max([Math]::Abs($a.G-$b.G),[Math]::Abs($a.B-$b.B))))
                $strip.SetPixel(96*$i+$x,$y,[Drawing.Color]::FromArgb(255,$delta,$delta,$delta))
            }
        }
    }
    return $strip
}

function New-TimedGif([string[]]$FramePaths,[int[]]$DurationsMs,[string]$OutputPath)
{
    $encoder=[Windows.Media.Imaging.GifBitmapEncoder]::new()
    foreach ($path in $FramePaths)
    {
        $stream=[IO.File]::OpenRead($path)
        try
        {
            $decoder=[Windows.Media.Imaging.PngBitmapDecoder]::new($stream,[Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,[Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
            $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($decoder.Frames[0]))
        }
        finally { $stream.Dispose() }
    }
    $output=[IO.File]::Create($OutputPath)
    try { $encoder.Save($output) } finally { $output.Dispose() }

    $bytes=[Collections.Generic.List[byte]]::new([IO.File]::ReadAllBytes($OutputPath))
    $packed=$bytes[10]
    $globalTableBytes=if(($packed-band0x80)-ne0){3*(1-shl(($packed-band0x07)+1))}else{0}
    $loop=[byte[]](0x21,0xFF,0x0B,0x4E,0x45,0x54,0x53,0x43,0x41,0x50,0x45,0x32,0x2E,0x30,0x03,0x01,0x00,0x00,0x00)
    $bytes.InsertRange(13+$globalTableBytes,$loop)
    $controls=[Collections.Generic.List[int]]::new()
    for($i=0;$i-le$bytes.Count-8;$i++)
    {
        if($bytes[$i]-eq0x21-and$bytes[$i+1]-eq0xF9-and$bytes[$i+2]-eq0x04){$controls.Add($i)}
    }
    if($controls.Count-ne$DurationsMs.Count){throw "GIF control count mismatch: $($controls.Count)"}
    for($i=0;$i-lt$controls.Count;$i++)
    {
        $delay=[int]([Math]::Max(2,[Math]::Round($DurationsMs[$i]/10.0)))
        $offset=$controls[$i]
        $bytes[$offset+3]=[byte](($bytes[$offset+3]-band0xE3)-bor0x09)
        $bytes[$offset+4]=[byte]($delay-band0xFF)
        $bytes[$offset+5]=[byte](($delay-shr8)-band0xFF)
    }
    [IO.File]::WriteAllBytes($OutputPath,$bytes.ToArray())
}

$canonical=[Drawing.Bitmap]::new($canonicalPath)
$frames=[Collections.Generic.List[Drawing.Bitmap]]::new()
try
{
    for ($i=0;$i-lt$names.Count;$i++)
    {
        $candidateFramePath=Join-Path $frameDirectory $names[$i]
        if ($i-eq0-or$i-eq11)
        {
            Copy-Item -LiteralPath $canonicalPath -Destination $candidateFramePath -Force
            $frame=[Drawing.Bitmap]::new($candidateFramePath)
        }
        else
        {
            $generated=New-HangingBody $bottoms[$i] $shapeStages[$i]
            try
            {
                Copy-CanonicalIdentity $canonical $generated
                Save-Png $generated $candidateFramePath
            }
            finally { $generated.Dispose() }
            $frame=[Drawing.Bitmap]::new($candidateFramePath)
        }
        $frames.Add($frame)
    }

    $framePaths=@($names|ForEach-Object{Join-Path $frameDirectory $_})
    for($i=0;$i-lt$names.Count;$i++)
    {
        Copy-Item -LiteralPath $framePaths[$i] -Destination (Join-Path $assetRoot $names[$i]) -Force
        Copy-Item -LiteralPath $framePaths[$i] -Destination (Join-Path $sourceRoot $names[$i]) -Force
    }

    $native=New-Strip $frames.ToArray();try{Save-Png $native (Join-Path $verification 'body-drag-native-strip.png')}finally{$native.Dispose()}
    $nearest=New-Nearest4x $frames.ToArray();try{Save-Png $nearest (Join-Path $verification 'body-drag-nearest-4x-strip.png')}finally{$nearest.Dispose()}
    $onion=New-OnionStrip $frames.ToArray();try{Save-Png $onion (Join-Path $verification 'body-drag-onion-skin-strip.png')}finally{$onion.Dispose()}
    $difference=New-DifferenceStrip $frames.ToArray();try{Save-Png $difference (Join-Path $verification 'body-drag-difference-strip.png')}finally{$difference.Dispose()}
    New-TimedGif $framePaths ([int[]]@(50,50,50,50,50,50,80,160,55,55,55,80)) (Join-Path $verification 'body-drag-entry-hold-release-800ms.gif')

    $records=for($i=0;$i-lt$names.Count;$i++)
    {
        [ordered]@{
            Index=$i;Name=$names[$i];IntendedBottomY=$bottoms[$i]
            Sha256=(Get-FileHash -LiteralPath $framePaths[$i] -Algorithm SHA256).Hash
        }
    }
    $metrics=[ordered]@{
        Candidate='body-drag-v1-attempt-1-provisional-unverified'
        FrameCount=12;FrameWidth=96;FrameHeight=96
        TimelineMs=[ordered]@{Entry=380;Hold=160;Settle=260;Total=800}
        CanonicalSha256=$canonicalHash
        Frames=$records
    }
    $metrics|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $verification 'body-drag-metrics.json') -Encoding utf8NoBOM
    'BODY DRAG ATTEMPT 1 BUILT: 12 provisional complete frames plus native/4x/onion/difference/GIF evidence.'
}
finally
{
    foreach($frame in $frames){$frame.Dispose()}
    $canonical.Dispose()
}
