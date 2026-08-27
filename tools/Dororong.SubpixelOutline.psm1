Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$script:ExpectedBodyMaskSha256 = 'D08B3A941C662F1CBC55C486C13FD4C6CD8901DA9CD5CF8512509698219FE46F'

function Assert-DororongContourBitmapPair(
    [System.Drawing.Bitmap]$Source,
    [System.Drawing.Bitmap]$Mask)
{
    if ($null -eq $Source -or $null -eq $Mask)
    { throw 'Contour source and mask are required.' }
    if ($Source.Width -ne $Mask.Width -or $Source.Height -ne $Mask.Height)
    {
        throw "Contour source and mask dimensions differ: source $($Source.Width)x$($Source.Height), mask $($Mask.Width)x$($Mask.Height)."
    }
    if ($Source.Width -le 0 -or $Source.Height -le 0)
    { throw 'Contour source and mask dimensions must be positive.' }

    for ($y = 0; $y -lt $Mask.Height; $y++)
    {
        for ($x = 0; $x -lt $Mask.Width; $x++)
        {
            $maskPixel = $Mask.GetPixel($x, $y)
            if ($maskPixel.A -ne 255 -or $maskPixel.R -ne $maskPixel.G -or
                $maskPixel.R -ne $maskPixel.B -or
                ($maskPixel.R -ne 0 -and $maskPixel.R -ne 255))
            { throw "Mask pixel ($x,$y) is not binary opaque grayscale." }

            $sourcePixel = $Source.GetPixel($x, $y)
            if ($sourcePixel.A -ne 0 -and $sourcePixel.A -ne 255)
            { throw "Processed-source alpha must be 0 or 255 at ($x,$y); observed $($sourcePixel.A)." }
            if ($maskPixel.R -eq 255 -and $sourcePixel.A -ne 255)
            { throw "Writable-alpha failure at ($x,$y): mask 255 requires processed-source alpha 255." }
        }
    }
}

function New-DororongContourSegment(
    [ValidateSet('E','C')][string]$Kind,
    [double]$X1,
    [double]$Y1,
    [double]$X2,
    [double]$Y2)
{
    return [pscustomobject]@{
        Kind = $Kind
        X1 = $X1
        Y1 = $Y1
        X2 = $X2
        Y2 = $Y2
    }
}

function Convert-DororongDoubledCoordinate([double]$Value, [string]$Label)
{
    if ([double]::IsNaN($Value) -or [double]::IsInfinity($Value))
    { throw "$Label must be finite." }
    $doubled = $Value * 2.0
    $rounded = [Math]::Round($doubled, 0, [MidpointRounding]::ToEven)
    if ([Math]::Abs($doubled - $rounded) -gt 0.000000000001)
    { throw "$Label must be an exact half-pixel coordinate; observed $Value." }
    if ($rounded -lt [int]::MinValue -or $rounded -gt [int]::MaxValue)
    { throw "$Label is outside the supported coordinate range." }
    return [int]$rounded
}

function Import-DororongBodyMask([string]$Path)
{
    if ([string]::IsNullOrWhiteSpace($Path))
    { throw 'Body mask path is required.' }
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not [IO.File]::Exists($fullPath))
    { throw "Body mask is missing: $fullPath" }
    $observedHash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
    if ($observedHash -ne $script:ExpectedBodyMaskSha256)
    {
        throw "Body mask hash mismatch: expected $script:ExpectedBodyMaskSha256; observed $observedHash."
    }

    $loaded = [Drawing.Bitmap]::new($fullPath)
    try
    {
        if ($loaded.Width -ne 225 -or $loaded.Height -ne 225)
        { throw "Body mask dimensions must be 225x225; observed $($loaded.Width)x$($loaded.Height)." }
        $result = [Drawing.Bitmap]::new(225, 225, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try
        {
            for ($y = 0; $y -lt 225; $y++)
            {
                for ($x = 0; $x -lt 225; $x++)
                {
                    $pixel = $loaded.GetPixel($x, $y)
                    if ($pixel.A -ne 255 -or $pixel.R -ne $pixel.G -or
                        $pixel.R -ne $pixel.B -or ($pixel.R -ne 0 -and $pixel.R -ne 255))
                    { throw "Body mask pixel ($x,$y) is not binary opaque grayscale." }
                    $result.SetPixel($x, $y, [Drawing.Color]::FromArgb(255, $pixel.R, $pixel.R, $pixel.R))
                }
            }
            return $result
        }
        catch
        {
            $result.Dispose()
            throw
        }
    }
    finally
    { $loaded.Dispose() }
}

function New-DororongVisibleContour(
    [System.Drawing.Bitmap]$Source,
    [System.Drawing.Bitmap]$Mask,
    [System.Drawing.PointF[]]$LegalEndpoints)
{
    Assert-DororongContourBitmapPair $Source $Mask
    if ($null -eq $LegalEndpoints)
    { $LegalEndpoints = [Drawing.PointF[]]@() }

    $exposedSegments = [Collections.Generic.List[object]]::new()
    $continuationSegments = [Collections.Generic.List[object]]::new()
    $vertices = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    $directions = @(
        @{ Dx = -1; Dy = 0; X1 = -1; Y1 = -1; X2 = -1; Y2 = 1 },
        @{ Dx = 1; Dy = 0; X1 = 1; Y1 = -1; X2 = 1; Y2 = 1 },
        @{ Dx = 0; Dy = -1; X1 = -1; Y1 = -1; X2 = 1; Y2 = -1 },
        @{ Dx = 0; Dy = 1; X1 = -1; Y1 = 1; X2 = 1; Y2 = 1 }
    )

    for ($y = 0; $y -lt $Mask.Height; $y++)
    {
        for ($x = 0; $x -lt $Mask.Width; $x++)
        {
            if ($Mask.GetPixel($x, $y).R -ne 255)
            { continue }
            foreach ($direction in $directions)
            {
                $neighborX = $x + [int]$direction.Dx
                $neighborY = $y + [int]$direction.Dy
                $isTransparent = $neighborX -lt 0 -or $neighborY -lt 0 -or
                    $neighborX -ge $Source.Width -or $neighborY -ge $Source.Height
                if (-not $isTransparent)
                { $isTransparent = $Source.GetPixel($neighborX, $neighborY).A -eq 0 }
                if (-not $isTransparent)
                { continue }

                $x12 = (2 * $x) + [int]$direction.X1
                $y12 = (2 * $y) + [int]$direction.Y1
                $x22 = (2 * $x) + [int]$direction.X2
                $y22 = (2 * $y) + [int]$direction.Y2
                $segment = New-DororongContourSegment 'E' `
                    ($x12 / 2.0) ($y12 / 2.0) ($x22 / 2.0) ($y22 / 2.0)
                $exposedSegments.Add($segment)
                foreach ($vertex in @(@($x12,$y12), @($x22,$y22)))
                {
                    $key = "$($vertex[0]),$($vertex[1])"
                    if (-not $vertices.ContainsKey($key))
                    {
                        $vertices[$key] = [pscustomobject]@{
                            X2 = [int]$vertex[0]
                            Y2 = [int]$vertex[1]
                        }
                    }
                }
            }
        }
    }

    foreach ($endpoint in $LegalEndpoints)
    {
        $endpointX = [int]$endpoint.X
        $endpointY = [int]$endpoint.Y
        if ($endpoint.X -ne $endpointX -or $endpoint.Y -ne $endpointY)
        { throw "Legal endpoint ($($endpoint.X),$($endpoint.Y)) must use integer pixel coordinates." }
        if ($endpointX -lt 0 -or $endpointY -lt 0 -or
            $endpointX -ge $Mask.Width -or $endpointY -ge $Mask.Height)
        { throw "Legal endpoint ($endpointX,$endpointY) is out of bounds." }
        if ($Source.GetPixel($endpointX, $endpointY).A -ne 255)
        { throw "Legal endpoint ($endpointX,$endpointY) is not source opaque." }
        if ($Mask.GetPixel($endpointX, $endpointY).R -ne 255)
        { throw "Legal endpoint ($endpointX,$endpointY) is not writable in the final mask." }

        $isBoundary = $false
        foreach ($offset in @(@(-1,0), @(1,0), @(0,-1), @(0,1)))
        {
            $neighborX = $endpointX + $offset[0]
            $neighborY = $endpointY + $offset[1]
            if ($neighborX -lt 0 -or $neighborY -lt 0 -or
                $neighborX -ge $Mask.Width -or $neighborY -ge $Mask.Height -or
                $Mask.GetPixel($neighborX, $neighborY).R -eq 0)
            { $isBoundary = $true; break }
        }
        if (-not $isBoundary)
        { throw "Legal endpoint ($endpointX,$endpointY) is not on the final-mask boundary." }
        if ($vertices.Count -eq 0)
        { throw "Legal endpoint ($endpointX,$endpointY) cannot attach because no exposed contour vertex exists." }

        $nearest = @($vertices.Values | Sort-Object `
            @{ Expression = {
                $dx = ([double]$_.X2 / 2.0) - $endpointX
                $dy = ([double]$_.Y2 / 2.0) - $endpointY
                ($dx * $dx) + ($dy * $dy)
            } },
            @{ Expression = { [int]$_.Y2 } },
            @{ Expression = { [int]$_.X2 } } | Select-Object -First 1)
        $continuationSegments.Add((New-DororongContourSegment 'C' `
            ([double]$nearest[0].X2 / 2.0) ([double]$nearest[0].Y2 / 2.0) `
            $endpointX $endpointY))
    }

    $allSegments = @($exposedSegments) + @($continuationSegments)
    return [pscustomobject]@{
        Segments = $allSegments
        ExposedSegments = @($exposedSegments)
        ContinuationSegments = @($continuationSegments)
        ExposedSegmentCount = $exposedSegments.Count
        ContinuationSegmentCount = $continuationSegments.Count
    }
}

function Get-DororongCanonicalContourRecords([object]$Contour)
{
    if ($null -eq $Contour -or $null -eq $Contour.PSObject.Properties['Segments'])
    { throw 'Contour must provide a Segments collection.' }
    $unique = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $entries = [Collections.Generic.List[object]]::new()
    foreach ($segment in @($Contour.Segments))
    {
        if ($null -eq $segment)
        { throw 'Contour contains a null segment.' }
        foreach ($property in @('Kind','X1','Y1','X2','Y2'))
        {
            if ($null -eq $segment.PSObject.Properties[$property])
            { throw "Contour segment is missing '$property'." }
        }
        $kind = [string]$segment.Kind
        if ($kind -ne 'E' -and $kind -ne 'C')
        { throw "Contour segment has invalid kind '$kind'." }
        $x1 = Convert-DororongDoubledCoordinate ([double]$segment.X1) 'Contour segment X1'
        $y1 = Convert-DororongDoubledCoordinate ([double]$segment.Y1) 'Contour segment Y1'
        $x2 = Convert-DororongDoubledCoordinate ([double]$segment.X2) 'Contour segment X2'
        $y2 = Convert-DororongDoubledCoordinate ([double]$segment.Y2) 'Contour segment Y2'
        if ($y2 -lt $y1 -or ($y2 -eq $y1 -and $x2 -lt $x1))
        {
            $temporaryX = $x1; $temporaryY = $y1
            $x1 = $x2; $y1 = $y2
            $x2 = $temporaryX; $y2 = $temporaryY
        }
        $record = "$kind|$y1|$x1|$y2|$x2"
        if ($unique.Add($record))
        {
            $entries.Add([pscustomobject]@{
                Kind = $kind
                StartY2 = $y1
                StartX2 = $x1
                EndY2 = $y2
                EndX2 = $x2
                Record = $record
            })
        }
    }
    return @($entries | Sort-Object Kind, StartY2, StartX2, EndY2, EndX2 | ForEach-Object Record)
}

function Get-DororongCanonicalContourHash([object]$Contour)
{
    $records = @(Get-DororongCanonicalContourRecords $Contour)
    $sha = [Security.Cryptography.SHA256]::Create()
    try
    {
        $bytes = [Text.Encoding]::UTF8.GetBytes($records -join "`n")
        return [Convert]::ToHexString($sha.ComputeHash($bytes))
    }
    finally
    { $sha.Dispose() }
}

Export-ModuleMember -Function `
    Import-DororongBodyMask, `
    New-DororongVisibleContour, `
    Get-DororongCanonicalContourRecords, `
    Get-DororongCanonicalContourHash
