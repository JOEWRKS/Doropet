Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

function Get-DororongSha256Text([string]$Text)
{
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try
    { return [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text))) }
    finally
    { $sha.Dispose() }
}

function Assert-DororongBitmapShape(
    [System.Drawing.Bitmap]$Bitmap,
    [int]$CanvasSize,
    [string]$Label)
{
    if ($null -eq $Bitmap)
    { throw "$Label bitmap is missing." }
    if ($Bitmap.Width -ne $CanvasSize -or $Bitmap.Height -ne $CanvasSize)
    { throw "$Label dimensions must be ${CanvasSize}x${CanvasSize}; observed $($Bitmap.Width)x$($Bitmap.Height)." }
    if ($Bitmap.PixelFormat -ne [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    { throw "$Label pixel format must be Format32bppArgb; observed $($Bitmap.PixelFormat)." }
}

function Import-DororongBinaryMask([string]$Path)
{
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not [System.IO.File]::Exists($fullPath))
    { throw "Binary mask is missing: $fullPath" }

    $loaded = [System.Drawing.Bitmap]::new($fullPath)
    try
    {
        if ($loaded.Width -ne 225 -or $loaded.Height -ne 225)
        { throw "Binary mask dimensions must be 225x225; observed $($loaded.Width)x$($loaded.Height)." }
        $result = [System.Drawing.Bitmap]::new(
            225,
            225,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        for ($y = 0; $y -lt 225; $y++)
        {
            for ($x = 0; $x -lt 225; $x++)
            {
                $pixel = $loaded.GetPixel($x, $y)
                if ($pixel.A -ne 255)
                {
                    $result.Dispose()
                    throw "Binary mask alpha must be 255 at ($x,$y); observed $($pixel.A)."
                }
                if ($pixel.R -ne $pixel.G -or $pixel.R -ne $pixel.B -or
                    ($pixel.R -ne 0 -and $pixel.R -ne 255))
                {
                    $result.Dispose()
                    throw "Binary mask channels must be equal and binary at ($x,$y); observed $($pixel.R),$($pixel.G),$($pixel.B)."
                }
                $result.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $pixel.R, $pixel.R, $pixel.R))
            }
        }
        return $result
    }
    finally
    { $loaded.Dispose() }
}

function Assert-DororongOwnershipConfiguration([hashtable]$Configuration)
{
    foreach ($required in @(
        'CanvasSize', 'ExpectedSelectedComponentCount', 'ExpectedSelectedPixelCount',
        'MaximumSelectedChroma', 'InvalidSeedCoordinate', 'ContactOffsets',
        'ProtectedAnchors', 'AnchorSetSha256'))
    {
        if (-not $Configuration.ContainsKey($required))
        { throw "Ownership configuration is missing '$required'." }
    }

    $expectedOffsets = @('-1,0', '1,0', '0,-1', '0,1')
    $observedOffsets = @($Configuration.ContactOffsets | ForEach-Object { "$([int]$_.X),$([int]$_.Y)" })
    if ($observedOffsets.Count -ne 4 -or ($observedOffsets -join '|') -ne ($expectedOffsets -join '|'))
    {
        throw "Four-neighbor contact assertion failed. Expected $($expectedOffsets -join ';'), observed $($observedOffsets -join ';')."
    }

    $anchors = @($Configuration.ProtectedAnchors)
    if ($anchors.Count -ne 18)
    { throw "Protected anchor-set failure: expected 18 records, observed $($anchors.Count)." }
    $anchorCoordinates = [System.Collections.Generic.HashSet[string]]::new()
    $anchorRecords = [System.Collections.Generic.List[string]]::new()
    foreach ($anchor in $anchors)
    {
        if (-not $anchor.ContainsKey('Name') -or -not $anchor.ContainsKey('X') -or -not $anchor.ContainsKey('Y'))
        { throw 'Protected anchor-set failure: a record is incomplete.' }
        $x = [int]$anchor.X
        $y = [int]$anchor.Y
        if ($x -lt 0 -or $x -ge [int]$Configuration.CanvasSize -or
            $y -lt 0 -or $y -ge [int]$Configuration.CanvasSize)
        { throw "Protected anchor-set failure: '$($anchor.Name)' is out of bounds at ($x,$y)." }
        if (-not $anchorCoordinates.Add("$x,$y"))
        { throw "Protected anchor-set failure: duplicate coordinate ($x,$y)." }
        $anchorRecords.Add("$($anchor.Name)|$x|$y")
    }
    $anchorHash = Get-DororongSha256Text ($anchorRecords -join "`n")
    if (-not [string]::IsNullOrWhiteSpace([string]$Configuration.AnchorSetSha256) -and
        $anchorHash -ne [string]$Configuration.AnchorSetSha256)
    {
        throw "Protected anchor-set failure: expected hash $($Configuration.AnchorSetSha256), observed $anchorHash."
    }
    return $anchorHash
}

function Get-DororongBodyOwnership(
    [System.Drawing.Bitmap]$Source,
    [System.Drawing.Bitmap]$Seed,
    [hashtable]$Configuration)
{
    $anchorHash = Assert-DororongOwnershipConfiguration $Configuration
    $size = [int]$Configuration.CanvasSize
    Assert-DororongBitmapShape $Source $size 'Processed source'
    Assert-DororongBitmapShape $Seed $size 'Seed'

    $opaque = [bool[,]]::new($size, $size)
    $cleanedSeed = [bool[,]]::new($size, $size)
    for ($y = 0; $y -lt $size; $y++)
    {
        for ($x = 0; $x -lt $size; $x++)
        {
            $sourcePixel = $Source.GetPixel($x, $y)
            if ($sourcePixel.A -ne 0 -and $sourcePixel.A -ne 255)
            { throw "Processed source alpha must be 0 or 255 at ($x,$y); observed $($sourcePixel.A)." }
            if ($sourcePixel.A -eq 0 -and
                ($sourcePixel.R -ne 0 -or $sourcePixel.G -ne 0 -or $sourcePixel.B -ne 0))
            { throw "Processed source alpha-zero RGB must be cleared at ($x,$y)." }
            $opaque[$x,$y] = $sourcePixel.A -eq 255

            $seedPixel = $Seed.GetPixel($x, $y)
            if ($seedPixel.A -ne 255)
            { throw "Seed alpha must be 255 at ($x,$y); observed $($seedPixel.A)." }
            if ($seedPixel.R -ne $seedPixel.G -or $seedPixel.R -ne $seedPixel.B -or
                ($seedPixel.R -ne 0 -and $seedPixel.R -ne 255))
            { throw "Seed channels must be equal and binary at ($x,$y)." }
            $cleanedSeed[$x,$y] = $seedPixel.R -eq 255
        }
    }

    $invalidX = [int]$Configuration.InvalidSeedCoordinate.X
    $invalidY = [int]$Configuration.InvalidSeedCoordinate.Y
    if ($invalidX -lt 0 -or $invalidX -ge $size -or $invalidY -lt 0 -or $invalidY -ge $size)
    { throw "Invalid-seed coordinate is out of bounds at ($invalidX,$invalidY)." }
    $cleanedSeed[$invalidX,$invalidY] = $false

    for ($y = 0; $y -lt $size; $y++)
    {
        for ($x = 0; $x -lt $size; $x++)
        {
            if ($cleanedSeed[$x,$y] -and -not $opaque[$x,$y])
            { throw "Writable-alpha failure: cleaned seed owns processed-source alpha zero at ($x,$y)." }
        }
    }

    $anchorSet = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($anchor in @($Configuration.ProtectedAnchors))
    {
        $encodedAnchor = ([int]$anchor.Y * $size) + [int]$anchor.X
        [void]$anchorSet.Add($encodedAnchor)
        if ($cleanedSeed[[int]$anchor.X,[int]$anchor.Y])
        { throw "Protected-anchor failure: cleaned seed owns '$($anchor.Name)' at ($($anchor.X),$($anchor.Y))." }
    }

    $neighbors = @(@(-1,0), @(1,0), @(0,-1), @(0,1))
    $queue = [System.Collections.Generic.Queue[int]]::new()
    $visited = [bool[,]]::new($size, $size)
    $selected = [System.Collections.Generic.List[object]]::new()
    $protected = [System.Collections.Generic.List[object]]::new()

    for ($y = 0; $y -lt $size; $y++)
    {
        for ($x = 0; $x -lt $size; $x++)
        {
            if (-not $opaque[$x,$y] -or $cleanedSeed[$x,$y] -or $visited[$x,$y])
            { continue }

            $pixels = [System.Collections.Generic.List[int]]::new()
            $touchesSeed = $false
            $touchesExterior = $false
            $containsAnchor = $false
            $visited[$x,$y] = $true
            $queue.Enqueue(($y * $size) + $x)
            while ($queue.Count -gt 0)
            {
                $encoded = $queue.Dequeue()
                $pixels.Add($encoded)
                if ($anchorSet.Contains($encoded))
                { $containsAnchor = $true }
                $currentX = $encoded % $size
                $currentY = [int][Math]::Floor($encoded / $size)

                foreach ($offset in $neighbors)
                {
                    $nextX = $currentX + $offset[0]
                    $nextY = $currentY + $offset[1]
                    if ($nextX -lt 0 -or $nextX -ge $size -or $nextY -lt 0 -or $nextY -ge $size)
                    { continue }
                    if ($cleanedSeed[$nextX,$nextY])
                    { $touchesSeed = $true }
                    if (-not $opaque[$nextX,$nextY])
                    { $touchesExterior = $true }
                    if ($opaque[$nextX,$nextY] -and -not $cleanedSeed[$nextX,$nextY] -and
                        -not $visited[$nextX,$nextY])
                    {
                        $visited[$nextX,$nextY] = $true
                        $queue.Enqueue(($nextY * $size) + $nextX)
                    }
                }
            }

            $component = [pscustomobject]@{
                Pixels = @($pixels | Sort-Object)
                TouchesSeed = $touchesSeed
                TouchesExterior = $touchesExterior
                ContainsAnchor = $containsAnchor
            }
            if ($containsAnchor)
            { $protected.Add($component) }
            elseif ($touchesSeed -and $touchesExterior)
            { $selected.Add($component) }
        }
    }

    $maximumSpread = 0
    foreach ($component in $selected)
    {
        foreach ($encoded in $component.Pixels)
        {
            $pixelX = $encoded % $size
            $pixelY = [int][Math]::Floor($encoded / $size)
            $pixel = $Source.GetPixel($pixelX, $pixelY)
            $spread = [Math]::Max($pixel.R, [Math]::Max($pixel.G, $pixel.B)) -
                [Math]::Min($pixel.R, [Math]::Min($pixel.G, $pixel.B))
            if ($spread -gt [int]$Configuration.MaximumSelectedChroma)
            {
                throw "Neutral qualification failure: selected pixel ($pixelX,$pixelY) has channel spread $spread; maximum is $($Configuration.MaximumSelectedChroma)."
            }
            if ($spread -gt $maximumSpread)
            { $maximumSpread = $spread }
        }
    }

    $selectedPixelCount = [int](@($selected | ForEach-Object Pixels).Count)
    if ($selected.Count -ne [int]$Configuration.ExpectedSelectedComponentCount -or
        $selectedPixelCount -ne [int]$Configuration.ExpectedSelectedPixelCount)
    {
        throw "Component membership failure: expected $($Configuration.ExpectedSelectedComponentCount) components and $($Configuration.ExpectedSelectedPixelCount) pixels; observed $($selected.Count) components and $selectedPixelCount pixels."
    }
    if ($Configuration.ContainsKey('ExpectedMaximumSelectedChroma') -and
        $maximumSpread -ne [int]$Configuration.ExpectedMaximumSelectedChroma)
    { throw "Component membership failure: expected maximum selected spread $($Configuration.ExpectedMaximumSelectedChroma), observed $maximumSpread." }

    if ($Configuration.ContainsKey('ExpectedProtectedComponentCount') -and
        $protected.Count -ne [int]$Configuration.ExpectedProtectedComponentCount)
    { throw "Protected-anchor failure: expected $($Configuration.ExpectedProtectedComponentCount) anchored component, observed $($protected.Count)." }
    if ($Configuration.ContainsKey('ExpectedProtectedPixelCount') -and
        ($protected.Count -ne 1 -or $protected[0].Pixels.Count -ne [int]$Configuration.ExpectedProtectedPixelCount))
    {
        $observedProtectedPixels = [int](@($protected | ForEach-Object Pixels).Count)
        throw "Protected-anchor failure: expected anchored component size $($Configuration.ExpectedProtectedPixelCount), observed $observedProtectedPixels."
    }

    $membershipRecords = @(
        $selected |
            Sort-Object { $_.Pixels[0] } |
            ForEach-Object {
                $coordinates = @($_.Pixels | ForEach-Object {
                    $pixelX = $_ % $size
                    $pixelY = [int][Math]::Floor($_ / $size)
                    "$pixelY,$pixelX"
                }) -join ';'
                "C|$($_.Pixels.Count)|$coordinates"
            })
    $membershipHash = Get-DororongSha256Text ($membershipRecords -join "`n")
    if ($Configuration.ContainsKey('ExpectedMembershipSha256') -and
        -not [string]::IsNullOrWhiteSpace([string]$Configuration.ExpectedMembershipSha256) -and
        $membershipHash -ne [string]$Configuration.ExpectedMembershipSha256)
    {
        throw "Component membership failure: expected hash $($Configuration.ExpectedMembershipSha256), observed $membershipHash."
    }

    $mask = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $size; $y++)
    {
        for ($x = 0; $x -lt $size; $x++)
        {
            $value = if ($cleanedSeed[$x,$y]) { 255 } else { 0 }
            $mask.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $value, $value, $value))
        }
    }
    foreach ($component in $selected)
    {
        foreach ($encoded in $component.Pixels)
        {
            $pixelX = $encoded % $size
            $pixelY = [int][Math]::Floor($encoded / $size)
            $mask.SetPixel($pixelX, $pixelY, [System.Drawing.Color]::FromArgb(255, 255, 255, 255))
        }
    }

    return [pscustomobject]@{
        Mask = $mask
        SelectedComponents = @($selected)
        SelectedPixelCount = $selectedPixelCount
        MaximumSelectedChroma = $maximumSpread
        MembershipRecords = $membershipRecords
        MembershipHash = $membershipHash
        ProtectedComponents = @($protected)
        AnchorSetHash = $anchorHash
        CleanedSeed = $cleanedSeed
    }
}

Export-ModuleMember -Function Import-DororongBinaryMask, Get-DororongBodyOwnership
