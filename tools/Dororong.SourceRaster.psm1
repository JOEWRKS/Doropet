$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

function Remove-DororongBoundaryBackground([System.Drawing.Bitmap]$Bitmap)
{
    if ($Bitmap.Width -ne 225 -or $Bitmap.Height -ne 225)
    {
        throw "The canonical source must be 225x225; observed $($Bitmap.Width)x$($Bitmap.Height)."
    }

    $nearWhiteFloor = 225
    $boundaryBackground = [bool[,]]::new($Bitmap.Width, $Bitmap.Height)
    $queue = [Collections.Generic.Queue[System.Drawing.Point]]::new()

    function Add-DororongNearWhiteBoundaryPoint([int]$X, [int]$Y)
    {
        if ($X -lt 0 -or $Y -lt 0 -or $X -ge $Bitmap.Width -or
            $Y -ge $Bitmap.Height -or $boundaryBackground[$X, $Y])
        { return }

        $pixel = $Bitmap.GetPixel($X, $Y)
        if ($pixel.R -ge $nearWhiteFloor -and $pixel.G -ge $nearWhiteFloor -and
            $pixel.B -ge $nearWhiteFloor)
        {
            $boundaryBackground[$X, $Y] = $true
            $queue.Enqueue([System.Drawing.Point]::new($X, $Y))
        }
    }

    for ($x = 0; $x -lt $Bitmap.Width; $x++)
    {
        Add-DororongNearWhiteBoundaryPoint $x 0
        Add-DororongNearWhiteBoundaryPoint $x ($Bitmap.Height - 1)
    }
    for ($y = 0; $y -lt $Bitmap.Height; $y++)
    {
        Add-DororongNearWhiteBoundaryPoint 0 $y
        Add-DororongNearWhiteBoundaryPoint ($Bitmap.Width - 1) $y
    }

    while ($queue.Count -gt 0)
    {
        $point = $queue.Dequeue()
        Add-DororongNearWhiteBoundaryPoint ($point.X - 1) $point.Y
        Add-DororongNearWhiteBoundaryPoint ($point.X + 1) $point.Y
        Add-DororongNearWhiteBoundaryPoint $point.X ($point.Y - 1)
        Add-DororongNearWhiteBoundaryPoint $point.X ($point.Y + 1)
    }

    $result = [System.Drawing.Bitmap]::new(
        $Bitmap.Width,
        $Bitmap.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $Bitmap.Height; $y++)
    {
        for ($x = 0; $x -lt $Bitmap.Width; $x++)
        {
            $pixel = $Bitmap.GetPixel($x, $y)
            if ($boundaryBackground[$x, $y])
            { $result.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0)) }
            else
            {
                $result.SetPixel(
                    $x,
                    $y,
                    [System.Drawing.Color]::FromArgb(255, $pixel.R, $pixel.G, $pixel.B))
            }
        }
    }
    return $result
}

function Resize-DororongPremultiplied96([System.Drawing.Bitmap]$Bitmap)
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
            finally { $attributes.Dispose() }
        }
        finally { $graphics.Dispose() }

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
                { $result.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0)) }
                else
                {
                    $result.SetPixel(
                        $x,
                        $y,
                        [System.Drawing.Color]::FromArgb(
                            $pixel.A,
                            $pixel.R,
                            $pixel.G,
                            $pixel.B))
                }
            }
        }
        return $result
    }
    finally { $premultiplied.Dispose() }
}

Export-ModuleMember -Function Remove-DororongBoundaryBackground, Resize-DororongPremultiplied96
