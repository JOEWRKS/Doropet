param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
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

        $productionPath = Join-Path $OutputDirectory 'dororong-canonical.png'
        $production.Save($productionPath, [System.Drawing.Imaging.ImageFormat]::Png)

        $closedEyes = $production.Clone()
        try
        {
            $graphics = [System.Drawing.Graphics]::FromImage($closedEyes)
            try
            {
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
                $faceBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 250, 220, 224))
                $eyePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 31, 12, 27), 3)
                try
                {
                    $graphics.FillEllipse($faceBrush, 44, 111, 27, 30)
                    $graphics.FillEllipse($faceBrush, 87, 110, 29, 31)
                    $graphics.DrawArc($eyePen, 48, 120, 19, 13, 15, 150)
                    $graphics.DrawArc($eyePen, 92, 119, 19, 13, 15, 150)
                }
                finally
                {
                    $faceBrush.Dispose()
                    $eyePen.Dispose()
                }
            }
            finally
            {
                $graphics.Dispose()
            }

            $closedEyes.Save(
                (Join-Path $OutputDirectory 'dororong-closed-eyes.png'),
                [System.Drawing.Imaging.ImageFormat]::Png)
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

Write-Output 'Generated canonical transparent and bounded closed-eye Dororong frames.'
