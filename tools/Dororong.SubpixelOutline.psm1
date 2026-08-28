Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$script:OutlineConstants = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'Dororong.SubpixelOutline.Constants.psd1')

if (-not ('DororongSubpixelKernel' -as [type]))
{
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;

public sealed class DororongFillKernelResult
{
    public int[] Argb;
    public int[] EligibleIndices;
}

public static class DororongSubpixelKernel
{
    private static double DistanceSquaredToSegment(
        double x, double y, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        double lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared == 0.0)
        {
            double pointDx = x - x1;
            double pointDy = y - y1;
            return (pointDx * pointDx) + (pointDy * pointDy);
        }
        double t = (((x - x1) * dx) + ((y - y1) * dy)) / lengthSquared;
        if (t < 0.0) t = 0.0;
        else if (t > 1.0) t = 1.0;
        double nearestX = x1 + (t * dx);
        double nearestY = y1 + (t * dy);
        double resultX = x - nearestX;
        double resultY = y - nearestY;
        return (resultX * resultX) + (resultY * resultY);
    }

    public static double[] BuildCenterDistances(
        int width, int height, byte[] finalMask,
        double[] x1, double[] y1, double[] x2, double[] y2)
    {
        double[] distances = new double[width * height];
        for (int index = 0; index < distances.Length; index++)
            distances[index] = Double.NaN;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = (y * width) + x;
                if (finalMask[pixelIndex] != 255) continue;
                double minimumSquared = Double.PositiveInfinity;
                for (int segment = 0; segment < x1.Length; segment++)
                {
                    double candidate = DistanceSquaredToSegment(
                        x, y, x1[segment], y1[segment], x2[segment], y2[segment]);
                    if (candidate < minimumSquared) minimumSquared = candidate;
                }
                distances[pixelIndex] = Math.Sqrt(minimumSquared);
            }
        }
        return distances;
    }

    public static double[] BuildSampleDistances(
        int width, int height, byte[] finalMask, int factor,
        double[] x1, double[] y1, double[] x2, double[] y2,
        double[] distanceMultiplier)
    {
        int samplesPerPixel = factor * factor;
        double[] distances = new double[width * height * samplesPerPixel];
        for (int index = 0; index < distances.Length; index++)
            distances[index] = Double.NaN;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = (y * width) + x;
                if (finalMask[pixelIndex] != 255) continue;
                int sampleIndex = 0;
                for (int j = 0; j < factor; j++)
                {
                    double sampleY = y - 0.5 + ((j + 0.5) / factor);
                    for (int i = 0; i < factor; i++, sampleIndex++)
                    {
                        double sampleX = x - 0.5 + ((i + 0.5) / factor);
                        double minimumSquared = Double.PositiveInfinity;
                        for (int segment = 0; segment < x1.Length; segment++)
                        {
                            double candidate = DistanceSquaredToSegment(
                                sampleX, sampleY, x1[segment], y1[segment], x2[segment], y2[segment]);
                            double multiplier = distanceMultiplier[segment];
                            candidate *= multiplier * multiplier;
                            if (candidate < minimumSquared) minimumSquared = candidate;
                        }
                        distances[(pixelIndex * samplesPerPixel) + sampleIndex] = Math.Sqrt(minimumSquared);
                    }
                }
            }
        }
        return distances;
    }

    private static bool ComesBefore(double distanceSquared, int index,
        double otherDistanceSquared, int otherIndex)
    {
        return distanceSquared < otherDistanceSquared ||
            (distanceSquared == otherDistanceSquared && index < otherIndex);
    }

    public static DororongFillKernelResult BuildFill(
        int width, int height, int[] sourceArgb, byte[] seedMask, byte[] finalMask,
        double[] centerDistances, int floor, int maximumChroma,
        double minimumDistance, int neighborCount)
    {
        var eligible = new List<int>();
        for (int index = 0; index < sourceArgb.Length; index++)
        {
            if (finalMask[index] != 255 || seedMask[index] != 255 ||
                Double.IsNaN(centerDistances[index]) || centerDistances[index] <= minimumDistance)
                continue;
            int argb = sourceArgb[index];
            int alpha = (argb >> 24) & 255;
            int red = (argb >> 16) & 255;
            int green = (argb >> 8) & 255;
            int blue = argb & 255;
            int maximum = Math.Max(red, Math.Max(green, blue));
            int minimum = Math.Min(red, Math.Min(green, blue));
            if (alpha == 255 && red >= floor && green >= floor && blue >= floor &&
                (maximum - minimum) <= maximumChroma)
                eligible.Add(index);
        }
        if (eligible.Count < neighborCount)
            throw new InvalidOperationException(
                "Fill reconstruction requires at least " + neighborCount +
                " eligible seeds; observed " + eligible.Count + ".");

        int[] result = new int[sourceArgb.Length];
        for (int index = 0; index < sourceArgb.Length; index++)
        {
            if (finalMask[index] != 255) continue;
            if (eligible.BinarySearch(index) >= 0)
            {
                result[index] = sourceArgb[index];
                continue;
            }
            int x = index % width;
            int y = index / width;
            double[] nearestDistances = new double[neighborCount];
            int[] nearestIndices = new int[neighborCount];
            for (int slot = 0; slot < neighborCount; slot++)
            {
                nearestDistances[slot] = Double.PositiveInfinity;
                nearestIndices[slot] = Int32.MaxValue;
            }
            foreach (int seedIndex in eligible)
            {
                int seedX = seedIndex % width;
                int seedY = seedIndex / width;
                double dx = seedX - x;
                double dy = seedY - y;
                double distanceSquared = (dx * dx) + (dy * dy);
                int insertion = neighborCount;
                for (int slot = 0; slot < neighborCount; slot++)
                {
                    if (ComesBefore(distanceSquared, seedIndex,
                        nearestDistances[slot], nearestIndices[slot]))
                    { insertion = slot; break; }
                }
                if (insertion == neighborCount) continue;
                for (int slot = neighborCount - 1; slot > insertion; slot--)
                {
                    nearestDistances[slot] = nearestDistances[slot - 1];
                    nearestIndices[slot] = nearestIndices[slot - 1];
                }
                nearestDistances[insertion] = distanceSquared;
                nearestIndices[insertion] = seedIndex;
            }
            double weightTotal = 0.0;
            double redTotal = 0.0;
            double greenTotal = 0.0;
            double blueTotal = 0.0;
            for (int slot = 0; slot < neighborCount; slot++)
            {
                if (nearestIndices[slot] == Int32.MaxValue)
                    throw new InvalidOperationException(
                        "Writable non-seed cannot be assigned eight eligible seeds.");
                double weight = 1.0 / (1.0 + nearestDistances[slot]);
                int argb = sourceArgb[nearestIndices[slot]];
                weightTotal += weight;
                redTotal += weight * ((argb >> 16) & 255);
                greenTotal += weight * ((argb >> 8) & 255);
                blueTotal += weight * (argb & 255);
            }
            int red = (int)Math.Round(redTotal / weightTotal, MidpointRounding.ToEven);
            int green = (int)Math.Round(greenTotal / weightTotal, MidpointRounding.ToEven);
            int blue = (int)Math.Round(blueTotal / weightTotal, MidpointRounding.ToEven);
            result[index] = unchecked((int)0xFF000000) | (red << 16) | (green << 8) | blue;
        }
        return new DororongFillKernelResult {
            Argb = result,
            EligibleIndices = eligible.ToArray()
        };
    }

    public static int[] BuildRasterColors(
        int[] fillArgb, byte[] finalMask, double[] distances,
        int samplesPerPixel, int outlineArgb, double width)
    {
        int[] result = new int[fillArgb.Length];
        int outlineRed = (outlineArgb >> 16) & 255;
        int outlineGreen = (outlineArgb >> 8) & 255;
        int outlineBlue = outlineArgb & 255;
        for (int pixelIndex = 0; pixelIndex < fillArgb.Length; pixelIndex++)
        {
            if (finalMask[pixelIndex] != 255) continue;
            int count = 0;
            int offset = pixelIndex * samplesPerPixel;
            for (int sample = 0; sample < samplesPerPixel; sample++)
                if (distances[offset + sample] <= width) count++;
            double coverage = (double)count / samplesPerPixel;
            int fill = fillArgb[pixelIndex];
            int fillRed = (fill >> 16) & 255;
            int fillGreen = (fill >> 8) & 255;
            int fillBlue = fill & 255;
            int red = (int)Math.Round(
                (fillRed * (1.0 - coverage)) + (outlineRed * coverage),
                MidpointRounding.ToEven);
            int green = (int)Math.Round(
                (fillGreen * (1.0 - coverage)) + (outlineGreen * coverage),
                MidpointRounding.ToEven);
            int blue = (int)Math.Round(
                (fillBlue * (1.0 - coverage)) + (outlineBlue * coverage),
                MidpointRounding.ToEven);
            result[pixelIndex] = unchecked((int)0xFF000000) |
                (red << 16) | (green << 8) | blue;
        }
        return result;
    }
}
'@
}

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
                if ($neighborX -lt 0 -or $neighborY -lt 0 -or
                    $neighborX -ge $Source.Width -or $neighborY -ge $Source.Height)
                { continue }
                $isTransparent = $Source.GetPixel($neighborX, $neighborY).A -eq 0
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

function Get-DororongMaskBytes([Drawing.Bitmap]$Mask,[string]$Label)
{
    if($null-eq$Mask){throw "$Label is required."}
    $bytes=[byte[]]::new($Mask.Width*$Mask.Height)
    for($y=0;$y-lt$Mask.Height;$y++)
    {
        for($x=0;$x-lt$Mask.Width;$x++)
        {
            $pixel=$Mask.GetPixel($x,$y)
            if($pixel.A-ne255-or$pixel.R-ne$pixel.G-or$pixel.R-ne$pixel.B-or
                ($pixel.R-ne0-and$pixel.R-ne255))
            {throw "$Label pixel ($x,$y) is not binary opaque grayscale."}
            $bytes[($y*$Mask.Width)+$x]=[byte]$pixel.R
        }
    }
    return $bytes
}

function Get-DororongContourCoordinateArrays([object]$Contour)
{
    $null=@(Get-DororongCanonicalContourRecords $Contour)
    $segments=@($Contour.Segments)
    if($segments.Count-eq0){throw 'Contour must contain at least one segment.'}
    $x1=[double[]]::new($segments.Count);$y1=[double[]]::new($segments.Count)
    $x2=[double[]]::new($segments.Count);$y2=[double[]]::new($segments.Count)
    $distanceMultipliers=[double[]]::new($segments.Count)
    for($index=0;$index-lt$segments.Count;$index++)
    {
        $x1[$index]=[double]$segments[$index].X1;$y1[$index]=[double]$segments[$index].Y1
        $x2[$index]=[double]$segments[$index].X2;$y2[$index]=[double]$segments[$index].Y2
        $distanceMultipliers[$index]=if([string]$segments[$index].Kind-eq'C'){2.0}else{1.0}
    }
    return [pscustomobject]@{
        X1=$x1;Y1=$y1;X2=$x2;Y2=$y2;DistanceMultipliers=$distanceMultipliers
    }
}

function New-DororongFillField(
    [Drawing.Bitmap]$Source,
    [Drawing.Bitmap]$SeedMask,
    [Drawing.Bitmap]$FinalMask,
    [object]$Contour)
{
    if($null-eq$Source-or$null-eq$SeedMask-or$null-eq$FinalMask)
    {throw 'Fill source, cleaned predecessor seed, and final mask are required.'}
    if($Source.Width-ne$SeedMask.Width-or$Source.Height-ne$SeedMask.Height-or
        $Source.Width-ne$FinalMask.Width-or$Source.Height-ne$FinalMask.Height)
    {throw 'Fill bitmap dimensions differ.'}
    $seedBytes=Get-DororongMaskBytes $SeedMask 'Cleaned predecessor seed'
    $finalBytes=Get-DororongMaskBytes $FinalMask 'Final mask'
    $sourceArgb=[int[]]::new($Source.Width*$Source.Height)
    for($y=0;$y-lt$Source.Height;$y++)
    {
        for($x=0;$x-lt$Source.Width;$x++)
        {
            $index=($y*$Source.Width)+$x
            $sourceArgb[$index]=$Source.GetPixel($x,$y).ToArgb()
            if($finalBytes[$index]-eq255-and(($sourceArgb[$index]-shr24)-band255)-ne255)
            {throw "Final-mask pixel ($x,$y) does not have source alpha 255."}
        }
    }
    $coordinates=Get-DororongContourCoordinateArrays $Contour
    $centerDistances=[DororongSubpixelKernel]::BuildCenterDistances(
        $Source.Width,$Source.Height,$finalBytes,
        $coordinates.X1,$coordinates.Y1,$coordinates.X2,$coordinates.Y2)
    $kernel=[DororongSubpixelKernel]::BuildFill(
        $Source.Width,$Source.Height,$sourceArgb,$seedBytes,$finalBytes,$centerDistances,
        [int]$script:OutlineConstants.FillFloor,[int]$script:OutlineConstants.MaximumChroma,
        [double]$script:OutlineConstants.FillDistance,[int]$script:OutlineConstants.FillNeighborCount)
    $colors=[Drawing.Color[,]]::new($Source.Width,$Source.Height)
    for($y=0;$y-lt$Source.Height;$y++)
    {
        for($x=0;$x-lt$Source.Width;$x++)
        {
            $index=($y*$Source.Width)+$x
            if($finalBytes[$index]-eq255)
            {$colors[$x,$y]=[Drawing.Color]::FromArgb($kernel.Argb[$index])}
        }
    }
    $eligibleSeeds=@($kernel.EligibleIndices|ForEach-Object{
        [pscustomobject]@{X=[int]($_%$Source.Width);Y=[int][Math]::Floor($_/$Source.Width)}
    })
    Add-Member -InputObject $colors -NotePropertyName EligibleSeeds `
        -NotePropertyValue $eligibleSeeds
    Add-Member -InputObject $colors -NotePropertyName EligibleSeedCount `
        -NotePropertyValue $eligibleSeeds.Count
    return ,$colors
}

function New-DororongSubpixelDistanceMap(
    [Drawing.Bitmap]$Mask,[object]$Contour,[int]$Factor)
{
    if($Factor-le0){throw 'Subpixel factor must be positive.'}
    $maskBytes=Get-DororongMaskBytes $Mask 'Final mask'
    $coordinates=Get-DororongContourCoordinateArrays $Contour
    $distances=[DororongSubpixelKernel]::BuildSampleDistances(
        $Mask.Width,$Mask.Height,$maskBytes,$Factor,
        $coordinates.X1,$coordinates.Y1,$coordinates.X2,$coordinates.Y2,
        $coordinates.DistanceMultipliers)
    return [pscustomobject]@{
        Width=$Mask.Width;Height=$Mask.Height;Factor=$Factor
        SamplesPerPixel=$Factor*$Factor;Mask=$maskBytes;Distances=$distances
    }
}

function Get-DororongOutlineCoverage(
    [object]$DistanceMap,[int]$X,[int]$Y,[double]$Width)
{
    if($null-eq$DistanceMap){throw 'Distance map is required.'}
    if([double]::IsNaN($Width)-or[double]::IsInfinity($Width)-or$Width-lt0.0)
    {throw 'Outline width must be finite and non-negative.'}
    if($X-lt0-or$Y-lt0-or$X-ge$DistanceMap.Width-or$Y-ge$DistanceMap.Height)
    {throw "Coverage coordinate ($X,$Y) is out of bounds."}
    $pixelIndex=($Y*$DistanceMap.Width)+$X
    if($DistanceMap.Mask[$pixelIndex]-ne255)
    {throw "Coverage coordinate ($X,$Y) is outside the final mask."}
    $count=0;$offset=$pixelIndex*$DistanceMap.SamplesPerPixel
    for($index=0;$index-lt$DistanceMap.SamplesPerPixel;$index++)
    {if($DistanceMap.Distances[$offset+$index]-le$Width){$count++}}
    return [double]$count/[double]$DistanceMap.SamplesPerPixel
}

function Invoke-DororongSubpixelOutline(
    [Drawing.Bitmap]$Source,[Drawing.Bitmap]$Mask,[Drawing.Color[,]]$FillField,
    [object]$DistanceMap,[Drawing.Color]$OutlineColor,[double]$Width)
{
    if($null-eq$Source-or$null-eq$Mask-or$null-eq$FillField-or$null-eq$DistanceMap)
    {throw 'Raster source, mask, fill field, and distance map are required.'}
    if($Source.Width-ne$Mask.Width-or$Source.Height-ne$Mask.Height-or
        $Source.Width-ne$DistanceMap.Width-or$Source.Height-ne$DistanceMap.Height-or
        $FillField.GetLength(0)-ne$Source.Width-or$FillField.GetLength(1)-ne$Source.Height)
    {throw 'Raster input dimensions differ.'}
    if([double]::IsNaN($Width)-or[double]::IsInfinity($Width)-or$Width-lt0.0)
    {throw 'Outline width must be finite and non-negative.'}
    $fillArgb=[int[]]::new($Source.Width*$Source.Height)
    for($y=0;$y-lt$Source.Height;$y++)
    {for($x=0;$x-lt$Source.Width;$x++){$fillArgb[($y*$Source.Width)+$x]=$FillField[$x,$y].ToArgb()}}
    $rasterArgb=[DororongSubpixelKernel]::BuildRasterColors(
        $fillArgb,$DistanceMap.Mask,$DistanceMap.Distances,
        $DistanceMap.SamplesPerPixel,$OutlineColor.ToArgb(),$Width)
    $result=$Source.Clone(
        [Drawing.Rectangle]::new(0,0,$Source.Width,$Source.Height),
        [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        for($y=0;$y-lt$Source.Height;$y++)
        {
            for($x=0;$x-lt$Source.Width;$x++)
            {
                if($Mask.GetPixel($x,$y).R-ne255)
                {continue}
                $sourceAlpha=$Source.GetPixel($x,$y).A
                $color=[Drawing.Color]::FromArgb($rasterArgb[($y*$Source.Width)+$x])
                $result.SetPixel($x,$y,[Drawing.Color]::FromArgb($sourceAlpha,$color.R,$color.G,$color.B))
            }
        }
        return $result
    }
    catch{$result.Dispose();throw}
}

Export-ModuleMember -Function `
    Import-DororongBodyMask, `
    New-DororongVisibleContour, `
    Get-DororongCanonicalContourRecords, `
    Get-DororongCanonicalContourHash, `
    New-DororongFillField, `
    New-DororongSubpixelDistanceMap, `
    Get-DororongOutlineCoverage, `
    Invoke-DororongSubpixelOutline
