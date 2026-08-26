param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [string]$BaselineOutputDirectory
)

$ErrorActionPreference = 'Stop'
$nearWhiteFloor = 225
$expectedHash = 'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504'

if ((Get-FileHash -Algorithm SHA256 -LiteralPath $SourcePath).Hash -ne $expectedHash)
{
    throw 'The input is not the approved canonical Dororong source.'
}

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$leftEyeStencilRuns = @(
    '114:43-59', '115:44-60', '116:44-62', '117:43-63',
    '118:43-64', '119:43-64', '120:43-65', '121:43-64', '122:43-64',
    '123:43-64', '124:43-64', '125:43-64', '126:43-64', '127:43-64',
    '128:43-64', '129:43-64', '130:43-64', '131:44-63', '132:45-62',
    '133:46-61', '134:48-59', '135:51-57'
)
$rightEyeStencilRuns = @(
    '114:92-100', '115:89-103', '116:86-105', '117:86-106',
    '118:86-106', '119:86-106', '120:86-106', '121:86-106', '122:86-106',
    '123:86-106', '124:86-106', '125:86-106', '126:86-106', '127:86-106',
    '128:86-106', '129:86-106', '130:86-106', '131:86-106', '132:86-106',
    '133:87-105', '134:88-104', '135:90-103', '136:93-103',
    '137:86-103', '138:86-102', '139:88-102', '140:89-101',
    '141:99-101', '142:99-100', '143:99-100'
)
$leftLidRuns = @(
    '121:50-58', '122:48-60', '123:47-49', '123:59-61',
    '124:46-48', '124:60-62', '125:46-47', '125:61-62'
)
$rightLidRuns = @(
    '121:92-100', '122:90-102', '123:89-91', '123:101-103',
    '124:88-90', '124:102-104', '125:88-89', '125:103-104'
)

function ConvertFrom-CoordinateRun([string]$Run)
{
    if ($Run -notmatch '^(?<Y>\d+):(?<StartX>\d+)-(?<EndX>\d+)(:(?<SampleSide>[LR]))?$')
    {
        throw "Invalid coordinate run '$Run'."
    }

    return @{
        Y = [int]$Matches.Y
        StartX = [int]$Matches.StartX
        EndX = [int]$Matches.EndX
        SampleSide = if ($Matches.SampleSide) { $Matches.SampleSide } else { 'L' }
    }
}

function Get-BilinearFaceColor(
    [System.Drawing.Bitmap]$Bitmap,
    [int]$X,
    [int]$Y,
    [hashtable]$Eye)
{
    $topLeft = $Bitmap.GetPixel($Eye.TopLeftX, $Eye.TopLeftY)
    $topRight = $Bitmap.GetPixel($Eye.TopRightX, $Eye.TopRightY)
    $bottomLeft = $Bitmap.GetPixel($Eye.BottomLeftX, $Eye.BottomLeftY)
    $bottomRight = $Bitmap.GetPixel($Eye.BottomRightX, $Eye.BottomRightY)
    $xRatio = ($X - $Eye.MinimumX) / ($Eye.MaximumX - $Eye.MinimumX)
    $yRatio = ($Y - $Eye.MinimumY) / ($Eye.MaximumY - $Eye.MinimumY)

    $channels = foreach ($channel in @('R', 'G', 'B'))
    {
        $top = $topLeft.$channel + (($topRight.$channel - $topLeft.$channel) * $xRatio)
        $bottom = $bottomLeft.$channel + (($bottomRight.$channel - $bottomLeft.$channel) * $xRatio)
        [Math]::Round($top + (($bottom - $top) * $yRatio))
    }

    return [System.Drawing.Color]::FromArgb(255, $channels[0], $channels[1], $channels[2])
}

function Resize-ToNative96([System.Drawing.Bitmap]$Bitmap)
{
    $premultiplied = [System.Drawing.Bitmap]::new(
        96,
        96,
        [System.Drawing.Imaging.PixelFormat]::Format32bppPArgb)
    try
    {
        $graphics = [System.Drawing.Graphics]::FromImage($premultiplied)
        try
        {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
            try
            {
                $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
                $graphics.DrawImage(
                    $Bitmap,
                    [System.Drawing.Rectangle]::new(0, 0, 96, 96),
                    0,
                    0,
                    $Bitmap.Width,
                    $Bitmap.Height,
                    [System.Drawing.GraphicsUnit]::Pixel,
                    $attributes)
            }
            finally
            {
                $attributes.Dispose()
            }
        }
        finally
        {
            $graphics.Dispose()
        }

        $result = [System.Drawing.Bitmap]::new(
            96,
            96,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        for ($y = 0; $y -lt 96; $y++)
        {
            for ($x = 0; $x -lt 96; $x++)
            {
                $pixel = $premultiplied.GetPixel($x, $y)
                if ($pixel.A -eq 0)
                {
                    $result.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
                }
                else
                {
                    $result.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($pixel.A, $pixel.R, $pixel.G, $pixel.B))
                }
            }
        }
        return $result
    }
    finally
    {
        $premultiplied.Dispose()
    }
}

function New-NativeBodyContour
{
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.StartFigure()
    $path.AddBezier(18.8, 70.7, 18.6, 74.5, 18.4, 78.1, 21.2, 80.1)
    $path.AddBezier(21.2, 80.1, 23.1, 81.5, 25.7, 80.5, 25.5, 77.3)
    $path.AddBezier(25.5, 77.3, 25.3, 75.0, 24.7, 72.7, 25.2, 71.6)
    $path.AddBezier(25.2, 71.6, 28.4, 74.2, 31.8, 75.4, 34.8, 76.5)
    $path.AddBezier(34.8, 76.5, 35.4, 80.0, 37.0, 83.4, 40.3, 85.0)
    $path.AddBezier(40.3, 85.0, 42.5, 86.3, 46.0, 85.1, 46.7, 82.2)
    $path.AddBezier(46.7, 82.2, 47.1, 79.9, 46.6, 77.8, 47.0, 76.6)
    $path.AddBezier(47.0, 76.6, 52.3, 76.3, 57.3, 74.3, 60.5, 71.3)
    $path.AddBezier(60.5, 71.3, 60.2, 75.6, 60.7, 79.7, 63.6, 82.5)
    $path.AddBezier(63.6, 82.5, 65.6, 84.5, 68.7, 83.8, 69.5, 81.2)
    $path.AddBezier(69.5, 81.2, 70.3, 78.5, 68.9, 76.6, 69.4, 74.7)
    $path.AddBezier(69.4, 74.7, 72.8, 69.0, 74.7, 64.4, 74.4, 59.7)
    return $path
}

function New-PathMask(
    [System.Drawing.Drawing2D.GraphicsPath]$Path,
    [float]$Width)
{
    $scale = 4
    $highResolutionMask = [System.Drawing.Bitmap]::new(
        96 * $scale,
        96 * $scale,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($highResolutionMask)
    try
    {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $scaledPath = $Path.Clone()
        try
        {
            $matrix = [System.Drawing.Drawing2D.Matrix]::new()
            try
            {
                $matrix.Scale($scale, $scale)
                $scaledPath.Transform($matrix)
            }
            finally
            {
                $matrix.Dispose()
            }
            $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, $Width * $scale)
            try
            {
                $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
                $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
                $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
                $graphics.DrawPath($pen, $scaledPath)
            }
            finally
            {
                $pen.Dispose()
            }
        }
        finally
        {
            $scaledPath.Dispose()
        }
    }
    finally
    {
        $graphics.Dispose()
    }

    $mask = [System.Drawing.Bitmap]::new(96, 96, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt 96; $y++)
    {
        for ($x = 0; $x -lt 96; $x++)
        {
            $alphaSum = 0
            for ($sampleY = 0; $sampleY -lt $scale; $sampleY++)
            {
                for ($sampleX = 0; $sampleX -lt $scale; $sampleX++)
                {
                    $alphaSum += $highResolutionMask.GetPixel(
                        ($x * $scale) + $sampleX,
                        ($y * $scale) + $sampleY).A
                }
            }
            $alpha = [Math]::Round($alphaSum / ($scale * $scale))
            $mask.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, 255, 255, 255))
        }
    }
    $highResolutionMask.Dispose()
    return $mask
}

function Get-LocalBodyFill(
    [System.Drawing.Bitmap]$Bitmap,
    [int]$X,
    [int]$Y)
{
    $best = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
    $bestLuminance = -1
    for ($radius = 1; $radius -le 4; $radius++)
    {
        for ($sampleY = [Math]::Max(0, $Y - $radius); $sampleY -le [Math]::Min(95, $Y + $radius); $sampleY++)
        {
            for ($sampleX = [Math]::Max(0, $X - $radius); $sampleX -le [Math]::Min(95, $X + $radius); $sampleX++)
            {
                $pixel = $Bitmap.GetPixel($sampleX, $sampleY)
                if ($pixel.A -lt 224)
                {
                    continue
                }
                $luminance = (0.2126 * $pixel.R) + (0.7152 * $pixel.G) + (0.0722 * $pixel.B)
                if ($luminance -gt $bestLuminance)
                {
                    $best = $pixel
                    $bestLuminance = $luminance
                }
            }
        }
        if ($bestLuminance -ge 245)
        {
            break
        }
    }
    return $best
}

function Apply-NativeBodyCorrection([System.Drawing.Bitmap]$Bitmap)
{
    $strokePath = New-NativeBodyContour
    $valleyClearPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $valleyClearPath.StartFigure()
    $valleyClearPath.AddLine(62.7, 67.4, 61.5, 70.0)
    $clearMask = New-PathMask $strokePath 3.5
    $valleyClearMask = New-PathMask $valleyClearPath 2.5
    $strokeMask = New-PathMask $strokePath 1.35
    $baseline = $Bitmap.Clone()
    try
    {
        $strokeColor = [System.Drawing.Color]::FromArgb(255, 31, 20, 25)
        for ($y = 0; $y -lt 96; $y++)
        {
            for ($x = 0; $x -lt 96; $x++)
            {
                $original = $Bitmap.GetPixel($x, $y)
                if ($original.A -eq 0)
                {
                    $Bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
                    continue
                }
                if ($clearMask.GetPixel($x, $y).A -eq 0 -and $valleyClearMask.GetPixel($x, $y).A -lt 128)
                {
                    continue
                }

                $fill = Get-LocalBodyFill $baseline $x $y
                $coverage = $strokeMask.GetPixel($x, $y).A / 255.0
                if (($coverage * ($original.A / 255.0)) -lt 0.16)
                {
                    $coverage = 0.0
                }
                $red = [Math]::Round(($fill.R * (1.0 - $coverage)) + ($strokeColor.R * $coverage))
                $green = [Math]::Round(($fill.G * (1.0 - $coverage)) + ($strokeColor.G * $coverage))
                $blue = [Math]::Round(($fill.B * (1.0 - $coverage)) + ($strokeColor.B * $coverage))
                $Bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($original.A, $red, $green, $blue))
            }
        }
    }
    finally
    {
        $strokePath.Dispose()
        $valleyClearPath.Dispose()
        $clearMask.Dispose()
        $valleyClearMask.Dispose()
        $strokeMask.Dispose()
        $baseline.Dispose()
    }
}

$source = [System.Drawing.Bitmap]::new($SourcePath)
try
{
    if ($source.Width -ne 225 -or $source.Height -ne 225)
    {
        throw "The canonical source must be 225x225; observed $($source.Width)x$($source.Height)."
    }

    $boundaryBackground = [bool[,]]::new($source.Width, $source.Height)
    $queue = [Collections.Generic.Queue[System.Drawing.Point]]::new()

    function Add-NearWhiteBoundaryPoint([int]$X, [int]$Y)
    {
        if ($X -lt 0 -or $Y -lt 0 -or $X -ge $source.Width -or $Y -ge $source.Height -or $boundaryBackground[$X, $Y])
        {
            return
        }

        $pixel = $source.GetPixel($X, $Y)
        if ($pixel.R -ge $nearWhiteFloor -and $pixel.G -ge $nearWhiteFloor -and $pixel.B -ge $nearWhiteFloor)
        {
            $boundaryBackground[$X, $Y] = $true
            $queue.Enqueue([System.Drawing.Point]::new($X, $Y))
        }
    }

    for ($x = 0; $x -lt $source.Width; $x++)
    {
        Add-NearWhiteBoundaryPoint $x 0
        Add-NearWhiteBoundaryPoint $x ($source.Height - 1)
    }

    for ($y = 0; $y -lt $source.Height; $y++)
    {
        Add-NearWhiteBoundaryPoint 0 $y
        Add-NearWhiteBoundaryPoint ($source.Width - 1) $y
    }

    while ($queue.Count -gt 0)
    {
        $point = $queue.Dequeue()
        Add-NearWhiteBoundaryPoint ($point.X - 1) $point.Y
        Add-NearWhiteBoundaryPoint ($point.X + 1) $point.Y
        Add-NearWhiteBoundaryPoint $point.X ($point.Y - 1)
        Add-NearWhiteBoundaryPoint $point.X ($point.Y + 1)
    }

    $production = [System.Drawing.Bitmap]::new(
        $source.Width,
        $source.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        for ($y = 0; $y -lt $source.Height; $y++)
        {
            for ($x = 0; $x -lt $source.Width; $x++)
            {
                $pixel = $source.GetPixel($x, $y)
                $alpha = if ($boundaryBackground[$x, $y]) { 0 } else { 255 }
                $production.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, $pixel.R, $pixel.G, $pixel.B))
            }
        }

        $closedEyes = $production.Clone()
        try
        {
            foreach ($eye in @(
                @{
                    Runs = $leftEyeStencilRuns; MinimumX = 43; MaximumX = 64; MinimumY = 114; MaximumY = 135
                    TopLeftX = 42; TopLeftY = 137; TopRightX = 66; TopRightY = 136
                    BottomLeftX = 48; BottomLeftY = 143; BottomRightX = 64; BottomRightY = 143
                },
                @{
                    Runs = $rightEyeStencilRuns; MinimumX = 86; MaximumX = 106; MinimumY = 114; MaximumY = 143
                    TopLeftX = 65; TopLeftY = 134; TopRightX = 106; TopRightY = 142
                    BottomLeftX = 86; BottomLeftY = 144; BottomRightX = 106; BottomRightY = 144
                }
            ))
            {
                foreach ($stencilRun in $eye.Runs)
                {
                    $run = ConvertFrom-CoordinateRun $stencilRun
                    for ($x = $run.StartX; $x -le $run.EndX; $x++)
                    {
                        $faceColor = Get-BilinearFaceColor $source $x $run.Y $eye
                        $alpha = $closedEyes.GetPixel($x, $run.Y).A
                        $closedEyes.SetPixel(
                            $x,
                            $run.Y,
                            [System.Drawing.Color]::FromArgb($alpha, $faceColor.R, $faceColor.G, $faceColor.B))
                    }
                }
            }

            $lidColor = $source.GetPixel(43, 118)
            foreach ($lidRun in @($leftLidRuns + $rightLidRuns))
            {
                $run = ConvertFrom-CoordinateRun $lidRun
                for ($x = $run.StartX; $x -le $run.EndX; $x++)
                {
                    $alpha = $closedEyes.GetPixel($x, $run.Y).A
                    $closedEyes.SetPixel(
                        $x,
                        $run.Y,
                        [System.Drawing.Color]::FromArgb($alpha, $lidColor.R, $lidColor.G, $lidColor.B))
                }
            }

            $openBaseline = Resize-ToNative96 $production
            $closedBaseline = Resize-ToNative96 $closedEyes
            try
            {
                if ($BaselineOutputDirectory)
                {
                    New-Item -ItemType Directory -Force -Path $BaselineOutputDirectory | Out-Null
                    $openBaseline.Save(
                        (Join-Path $BaselineOutputDirectory 'dororong-canonical.png'),
                        [System.Drawing.Imaging.ImageFormat]::Png)
                    $closedBaseline.Save(
                        (Join-Path $BaselineOutputDirectory 'dororong-closed-eyes.png'),
                        [System.Drawing.Imaging.ImageFormat]::Png)
                }

                Apply-NativeBodyCorrection $openBaseline
                Apply-NativeBodyCorrection $closedBaseline
                $openBaseline.Save(
                    (Join-Path $OutputDirectory 'dororong-canonical.png'),
                    [System.Drawing.Imaging.ImageFormat]::Png)
                $closedBaseline.Save(
                    (Join-Path $OutputDirectory 'dororong-closed-eyes.png'),
                    [System.Drawing.Imaging.ImageFormat]::Png)
            }
            finally
            {
                $openBaseline.Dispose()
                $closedBaseline.Dispose()
            }
        }
        finally
        {
            $closedEyes.Dispose()
        }
    }
    finally
    {
        $production.Dispose()
    }
}
finally
{
    $source.Dispose()
}

Write-Output 'Generated deterministic native-96 open and closed Dororong runtime frames.'
