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

function New-NativeBodyLightenRun(
    [int]$Y, [int]$StartX, [int]$EndX,
    [int]$FillX, [int]$FillY, [double]$Blend)
{
    if ($StartX -gt $EndX -or $Blend -le 0.0 -or $Blend -gt 1.0)
    {
        throw 'Invalid native body lighten run.'
    }
    return @{ Y=$Y; StartX=$StartX; EndX=$EndX; FillX=$FillX; FillY=$FillY; Blend=$Blend }
}

$nativeBodyLightenRuns = @(
    # Lower rear rim; the upper rim already matches the clean one-pixel hair reference.
    New-NativeBodyLightenRun 67 72 72 71 67 0.55
    New-NativeBodyLightenRun 68 71 71 70 68 0.55
    New-NativeBodyLightenRun 69 70 70 69 69 0.55
    New-NativeBodyLightenRun 70 70 70 69 70 0.55
    New-NativeBodyLightenRun 71 69 69 68 71 0.55
    New-NativeBodyLightenRun 72 69 69 68 72 0.55
    New-NativeBodyLightenRun 73 68 68 67 73 0.55
    New-NativeBodyLightenRun 74 68 68 67 74 0.55

    # Front outer edge, foot, and inner edge.
    New-NativeBodyLightenRun 72 18 18 19 72 0.55
    New-NativeBodyLightenRun 73 18 18 19 73 0.55
    New-NativeBodyLightenRun 74 19 19 20 74 0.55
    New-NativeBodyLightenRun 75 19 19 20 75 0.55
    New-NativeBodyLightenRun 77 20 20 21 77 0.55
    New-NativeBodyLightenRun 78 21 21 22 78 0.55
    New-NativeBodyLightenRun 79 22 22 23 79 0.55
    New-NativeBodyLightenRun 80 23 24 23 79 0.50
    New-NativeBodyLightenRun 76 25 25 24 76 0.55
    New-NativeBodyLightenRun 77 25 25 24 77 0.55
    New-NativeBodyLightenRun 78 25 25 24 78 0.55
    New-NativeBodyLightenRun 79 24 24 23 79 0.55

    # First valley and underside.
    New-NativeBodyLightenRun 74 28 28 28 73 0.55
    New-NativeBodyLightenRun 75 29 34 31 74 0.45

    # Center outer edge, foot, and inner edge.
    New-NativeBodyLightenRun 76 35 35 36 76 0.55
    New-NativeBodyLightenRun 77 35 35 36 77 0.55
    New-NativeBodyLightenRun 79 36 36 37 79 0.55
    New-NativeBodyLightenRun 80 36 36 37 80 0.55
    New-NativeBodyLightenRun 81 37 37 38 81 0.55
    New-NativeBodyLightenRun 82 38 38 39 82 0.55
    New-NativeBodyLightenRun 83 39 39 40 83 0.55
    New-NativeBodyLightenRun 84 41 41 42 84 0.55
    New-NativeBodyLightenRun 85 42 45 42 84 0.50
    New-NativeBodyLightenRun 77 46 46 45 77 0.55
    New-NativeBodyLightenRun 83 46 46 45 83 0.55
    New-NativeBodyLightenRun 84 46 46 45 84 0.55

    # Second valley and underside into the rear leg.
    New-NativeBodyLightenRun 75 52 52 52 74 0.55
    New-NativeBodyLightenRun 74 60 60 61 74 0.55
    New-NativeBodyLightenRun 75 60 60 61 75 0.55
    New-NativeBodyLightenRun 76 60 60 61 76 0.55
    New-NativeBodyLightenRun 78 61 61 62 78 0.55
    New-NativeBodyLightenRun 79 62 62 63 79 0.55
    New-NativeBodyLightenRun 80 62 62 63 80 0.55
    New-NativeBodyLightenRun 81 63 63 64 81 0.55

    # Rear foot and inner edge.
    New-NativeBodyLightenRun 82 64 67 65 81 0.50
    New-NativeBodyLightenRun 78 69 69 68 78 0.55
    New-NativeBodyLightenRun 79 69 69 68 79 0.55
    New-NativeBodyLightenRun 81 68 68 67 81 0.55
)

function Apply-SubtractiveNativeBodyCorrection([System.Drawing.Bitmap]$Bitmap)
{
    $baseline = $Bitmap.Clone()
    try
    {
        foreach ($run in $nativeBodyLightenRuns)
        {
            $fill = $baseline.GetPixel($run.FillX, $run.FillY)
            if ($fill.A -ne 255) { throw "Body fill sample is not opaque at ($($run.FillX),$($run.FillY))." }

            for ($x = $run.StartX; $x -le $run.EndX; $x++)
            {
                $original = $baseline.GetPixel($x, $run.Y)
                if ($original.A -ne 255) { throw "Body correction is not opaque at ($x,$($run.Y))." }
                if ($fill.R -lt $original.R -or $fill.G -lt $original.G -or $fill.B -lt $original.B)
                {
                    throw "Body fill sample darkens ($x,$($run.Y))."
                }

                $red = [Math]::Round($original.R + (($fill.R - $original.R) * $run.Blend))
                $green = [Math]::Round($original.G + (($fill.G - $original.G) * $run.Blend))
                $blue = [Math]::Round($original.B + (($fill.B - $original.B) * $run.Blend))
                $Bitmap.SetPixel($x, $run.Y, [System.Drawing.Color]::FromArgb(255, $red, $green, $blue))
            }
        }
    }
    finally
    {
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

                Apply-SubtractiveNativeBodyCorrection $openBaseline
                Apply-SubtractiveNativeBodyCorrection $closedBaseline
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
