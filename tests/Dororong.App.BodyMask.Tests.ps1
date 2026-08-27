Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -ne $Actual)
    { throw "$Message Expected '$Expected', observed '$Actual'." }
}

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition)
    { throw $Message }
}

function Get-Sha256Text([string]$Text)
{
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try
    {
        return [Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))
    }
    finally
    { $sha.Dispose() }
}

function Get-PngSha256([System.Drawing.Bitmap]$Bitmap)
{
    $stream = [System.IO.MemoryStream]::new()
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try
    {
        $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return [Convert]::ToHexString($sha.ComputeHash($stream.ToArray()))
    }
    finally
    {
        $sha.Dispose()
        $stream.Dispose()
    }
}

function Copy-OwnershipConfiguration([hashtable]$Configuration)
{
    $copy = @{}
    foreach ($key in $Configuration.Keys)
    { $copy[$key] = $Configuration[$key] }
    $copy.InvalidSeedCoordinate = @{
        X = [int]$Configuration.InvalidSeedCoordinate.X
        Y = [int]$Configuration.InvalidSeedCoordinate.Y
    }
    $copy.ContactOffsets = @($Configuration.ContactOffsets | ForEach-Object {
        @{ X = [int]$_.X; Y = [int]$_.Y }
    })
    $copy.ProtectedAnchors = @($Configuration.ProtectedAnchors | ForEach-Object {
        @{ Name = [string]$_.Name; X = [int]$_.X; Y = [int]$_.Y }
    })
    return $copy
}

function Assert-ThrowsLike(
    [scriptblock]$Action,
    [string]$ExpectedPattern,
    [string]$Label)
{
    try
    {
        & $Action | Out-Null
    }
    catch
    {
        $message = $_.Exception.Message
        if ($message -notmatch $ExpectedPattern)
        { throw "$Label reached the wrong assertion. Expected '$ExpectedPattern', observed '$message'." }
        return $message
    }
    throw "$Label did not reach an assertion."
}

function Get-IndependentBodyOwnership(
    [System.Drawing.Bitmap]$Source,
    [System.Drawing.Bitmap]$Seed,
    [object[]]$Anchors)
{
    $width = $Source.Width
    $height = $Source.Height
    $nearWhite = [bool[,]]::new($width, $height)
    $boundaryBackground = [bool[,]]::new($width, $height)
    $queue = [System.Collections.Generic.Queue[int]]::new()

    for ($y = 0; $y -lt $height; $y++)
    {
        for ($x = 0; $x -lt $width; $x++)
        {
            $pixel = $Source.GetPixel($x, $y)
            $nearWhite[$x,$y] = $pixel.R -ge 225 -and $pixel.G -ge 225 -and $pixel.B -ge 225
        }
    }

    foreach ($x in 0..($width - 1))
    {
        foreach ($y in @(0, ($height - 1)))
        {
            if ($nearWhite[$x,$y] -and -not $boundaryBackground[$x,$y])
            {
                $boundaryBackground[$x,$y] = $true
                $queue.Enqueue(($y * $width) + $x)
            }
        }
    }
    foreach ($y in 0..($height - 1))
    {
        foreach ($x in @(0, ($width - 1)))
        {
            if ($nearWhite[$x,$y] -and -not $boundaryBackground[$x,$y])
            {
                $boundaryBackground[$x,$y] = $true
                $queue.Enqueue(($y * $width) + $x)
            }
        }
    }

    $neighbors = @(@(-1,0), @(1,0), @(0,-1), @(0,1))
    while ($queue.Count -gt 0)
    {
        $encoded = $queue.Dequeue()
        $currentX = $encoded % $width
        $currentY = [int][Math]::Floor($encoded / $width)
        foreach ($offset in $neighbors)
        {
            $nextX = $currentX + $offset[0]
            $nextY = $currentY + $offset[1]
            if ($nextX -lt 0 -or $nextX -ge $width -or $nextY -lt 0 -or $nextY -ge $height)
            { continue }
            if ($nearWhite[$nextX,$nextY] -and -not $boundaryBackground[$nextX,$nextY])
            {
                $boundaryBackground[$nextX,$nextY] = $true
                $queue.Enqueue(($nextY * $width) + $nextX)
            }
        }
    }

    $opaque = [bool[,]]::new($width, $height)
    $cleanedSeed = [bool[,]]::new($width, $height)
    for ($y = 0; $y -lt $height; $y++)
    {
        for ($x = 0; $x -lt $width; $x++)
        {
            $opaque[$x,$y] = -not $boundaryBackground[$x,$y]
            $cleanedSeed[$x,$y] = $Seed.GetPixel($x, $y).R -eq 255
        }
    }
    $cleanedSeed[138,174] = $false

    $anchorSet = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($anchor in $Anchors)
    { [void]$anchorSet.Add(([int]$anchor[1] * $width) + [int]$anchor[0]) }

    $visited = [bool[,]]::new($width, $height)
    $selected = [System.Collections.Generic.List[object]]::new()
    $protected = [System.Collections.Generic.List[object]]::new()
    for ($y = 0; $y -lt $height; $y++)
    {
        for ($x = 0; $x -lt $width; $x++)
        {
            if (-not $opaque[$x,$y] -or $cleanedSeed[$x,$y] -or $visited[$x,$y])
            { continue }

            $pixels = [System.Collections.Generic.List[int]]::new()
            $touchesSeed = $false
            $touchesExterior = $false
            $containsAnchor = $false
            $visited[$x,$y] = $true
            $queue.Enqueue(($y * $width) + $x)
            while ($queue.Count -gt 0)
            {
                $encoded = $queue.Dequeue()
                $pixels.Add($encoded)
                if ($anchorSet.Contains($encoded))
                { $containsAnchor = $true }

                $currentX = $encoded % $width
                $currentY = [int][Math]::Floor($encoded / $width)
                foreach ($offset in $neighbors)
                {
                    $nextX = $currentX + $offset[0]
                    $nextY = $currentY + $offset[1]
                    if ($nextX -lt 0 -or $nextX -ge $width -or $nextY -lt 0 -or $nextY -ge $height)
                    { continue }
                    if ($cleanedSeed[$nextX,$nextY])
                    { $touchesSeed = $true }
                    if (-not $opaque[$nextX,$nextY])
                    { $touchesExterior = $true }
                    if ($opaque[$nextX,$nextY] -and -not $cleanedSeed[$nextX,$nextY] -and -not $visited[$nextX,$nextY])
                    {
                        $visited[$nextX,$nextY] = $true
                        $queue.Enqueue(($nextY * $width) + $nextX)
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
            $pixel = $Source.GetPixel($encoded % $width, [int][Math]::Floor($encoded / $width))
            $spread = [Math]::Max($pixel.R, [Math]::Max($pixel.G, $pixel.B)) -
                [Math]::Min($pixel.R, [Math]::Min($pixel.G, $pixel.B))
            if ($spread -gt $maximumSpread)
            { $maximumSpread = $spread }
        }
    }

    $membershipRecords = @(
        $selected |
            Sort-Object { $_.Pixels[0] } |
            ForEach-Object {
                $coordinates = @($_.Pixels | ForEach-Object {
                    $pixelX = $_ % $width
                    $pixelY = [int][Math]::Floor($_ / $width)
                    "$pixelY,$pixelX"
                }) -join ';'
                "C|$($_.Pixels.Count)|$coordinates"
            })
    $membershipText = $membershipRecords -join "`n"

    $expected = [bool[,]]::new($width, $height)
    for ($y = 0; $y -lt $height; $y++)
    {
        for ($x = 0; $x -lt $width; $x++)
        { $expected[$x,$y] = $cleanedSeed[$x,$y] }
    }
    foreach ($component in $selected)
    {
        foreach ($encoded in $component.Pixels)
        {
            $pixelX = $encoded % $width
            $pixelY = [int][Math]::Floor($encoded / $width)
            $expected[$pixelX,$pixelY] = $true
        }
    }

    return [pscustomobject]@{
        ExpectedMask = $expected
        SelectedComponents = @($selected)
        SelectedPixelCount = [int](@($selected | ForEach-Object Pixels).Count)
        MaximumSpread = $maximumSpread
        ProtectedComponents = @($protected)
        MembershipRecords = $membershipRecords
        MembershipHash = Get-Sha256Text $membershipText
    }
}

function Assert-PointArray([object]$Points, [string]$Label)
{
    Assert-True ($null -ne $Points) "$Label is missing."
    $pointList = @($Points)
    Assert-True ($pointList.Count -ge 5) "$Label has fewer than five samples."

    $coordinates = @()

    foreach ($point in $pointList)
    {
        Assert-True ($point -is [System.Collections.IList]) "$Label contains a non-array point."
        Assert-Equal 2 $point.Count "$Label contains a point that is not an X/Y pair."
        Assert-True ($point[0] -is [int] -and $point[1] -is [int]) "$Label contains a non-integer coordinate."
        $coordinates += "$($point[0]),$($point[1])"
    }

    Assert-Equal $coordinates.Count @($coordinates | Sort-Object -Unique).Count `
        "$Label contains duplicate coordinates."
}

function Assert-Point([object]$Point, [string]$Label)
{
    Assert-True ($null -ne $Point) "$Label is missing."
    $coordinate = @($Point)
    Assert-Equal 2 $coordinate.Count "$Label is not an X/Y pair."
    Assert-True ($coordinate[0] -is [int] -and $coordinate[1] -is [int]) `
        "$Label contains a non-integer coordinate."
}

function Get-OpticalInk([System.Drawing.Color]$Pixel)
{
    $alpha = $Pixel.A / 255.0
    $luma = ($Pixel.R + $Pixel.G + $Pixel.B) / (3.0 * 255.0)
    return $alpha * (1.0 - $luma)
}

function Assert-ScanCrossesOutline(
    [System.Drawing.Bitmap]$Bitmap,
    [object]$Points,
    [string]$Label)
{
    $inkSamples = @()
    foreach ($point in @($Points))
    {
        $x = [int]$point[0]
        $y = [int]$point[1]
        Assert-True ($x -ge 0 -and $x -lt $Bitmap.Width -and $y -ge 0 -and $y -lt $Bitmap.Height) `
            "$Label coordinate ($x,$y) is outside the sampled bitmap."
        $inkSamples += Get-OpticalInk $Bitmap.GetPixel($x, $y)
    }

    Assert-True (($inkSamples | Measure-Object -Maximum).Maximum -ge 0.30) `
        "$Label does not cross a dark outline."
    Assert-True (($inkSamples | Measure-Object -Minimum).Minimum -le 0.10) `
        "$Label does not include a light side of the outline."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical-source.png'
$nativePath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical.png'
$maskPath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-body-region-mask.png'
$seedPath = Join-Path $repositoryRoot 'tests/fixtures/dororong-body-region-seed.png'
$authorityPath = Join-Path $repositoryRoot 'tests/fixtures/dororong-body-outline-authority.psd1'
$sourceRasterModulePath = Join-Path $repositoryRoot 'tools/Dororong.SourceRaster.psm1'
$ownershipModulePath = Join-Path $repositoryRoot 'tools/Dororong.BodyOwnership.psm1'
$ownershipConstantsPath = Join-Path $repositoryRoot 'tools/Dororong.BodyOwnership.Constants.psd1'
$expectedAnchors = @(
    @(52,68), @(99,72), @(23,116), @(106,139),
    @(39,132), @(84,145), @(63,142), @(78,140),
    @(52,122), @(92,124), @(137,84), @(143,89),
    @(135,105), @(150,108), @(159,98), @(151,128),
    @(181,127), @(180,163)
)

Assert-Equal 'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash 'Canonical source changed.'
Assert-True (Test-Path -LiteralPath $nativePath) 'Canonical native authority is missing.'
Assert-True (Test-Path -LiteralPath $maskPath) 'Reviewed body-region mask is missing.'
Assert-True (Test-Path -LiteralPath $seedPath) 'Immutable predecessor body-region seed is missing.'
Assert-True (Test-Path -LiteralPath $authorityPath) 'Independent body-outline authority is missing.'
Assert-Equal 'E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $seedPath).Hash 'Immutable predecessor body-region seed changed.'

Add-Type -AssemblyName System.Drawing
Import-Module $sourceRasterModulePath -Force
Import-Module $ownershipModulePath -Force

$authority = Import-PowerShellDataFile -LiteralPath $authorityPath
$ownershipConfiguration = Import-PowerShellDataFile -LiteralPath $ownershipConstantsPath
$source = [System.Drawing.Bitmap]::new($sourcePath)
$native = [System.Drawing.Bitmap]::new($nativePath)
$mask = [System.Drawing.Bitmap]::new($maskPath)
$seed = [System.Drawing.Bitmap]::new($seedPath)
$processedSource = $null
$resizedSource = $null
$productionSeed = $null
$productionOwnership = $null
$mutationDirectories = [System.Collections.Generic.List[string]]::new()

try
{
    Assert-Equal 225 $mask.Width 'Body-region mask width changed.'
    Assert-Equal 225 $mask.Height 'Body-region mask height changed.'
    Assert-Equal 96 $native.Width 'Canonical native authority width changed.'
    Assert-Equal 96 $native.Height 'Canonical native authority height changed.'
    Assert-Equal ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb) $mask.PixelFormat `
        'Body-region mask is not 32bpp ARGB.'

    $writable = [System.Collections.Generic.HashSet[string]]::new()
    $writableCount = 0
    $minimumX = 225
    $minimumY = 225
    $maximumX = -1
    $maximumY = -1

    for ($y = 0; $y -lt $mask.Height; $y++)
    {
        for ($x = 0; $x -lt $mask.Width; $x++)
        {
            $pixel = $mask.GetPixel($x, $y)
            Assert-Equal 255 $pixel.A "Mask alpha is not opaque at ($x,$y)."
            Assert-Equal $pixel.R $pixel.G "Mask red/green channels differ at ($x,$y)."
            Assert-Equal $pixel.R $pixel.B "Mask red/blue channels differ at ($x,$y)."
            Assert-True ($pixel.R -eq 0 -or $pixel.R -eq 255) `
                "Mask channel is not binary at ($x,$y)."

            if ($pixel.R -eq 255)
            {
                $sourcePixel = $source.GetPixel($x, $y)
                Assert-True ($sourcePixel.A -gt 0) "Mask writes transparent exterior at ($x,$y)."
                [void]$writable.Add("$x,$y")
                $writableCount++
                if ($x -lt $minimumX) { $minimumX = $x }
                if ($y -lt $minimumY) { $minimumY = $y }
                if ($x -gt $maximumX) { $maximumX = $x }
                if ($y -gt $maximumY) { $maximumY = $y }
            }
        }
    }

    Assert-True ($writableCount -gt 0) 'Body-region mask contains no writable pixels.'

    $ownership = Get-IndependentBodyOwnership $source $seed $expectedAnchors
    Assert-Equal 82 $ownership.SelectedComponents.Count `
        'Independent ownership selected-component count changed.'
    Assert-Equal 167 $ownership.SelectedPixelCount `
        'Independent ownership selected-pixel count changed.'
    Assert-Equal 4 $ownership.MaximumSpread `
        'Independent ownership maximum selected channel spread changed.'
    Assert-Equal 1 $ownership.ProtectedComponents.Count `
        'Independent ownership anchored protected-component count changed.'
    Assert-Equal 11988 $ownership.ProtectedComponents[0].Pixels.Count `
        'Independent ownership anchored protected-component size changed.'
    Assert-Equal '30945B766547723F9860941DEB70C74465912E2604E5EDADBBB69E91C78D708D' `
        $ownership.MembershipHash 'Independent ownership membership hash changed.'

    $processedSource = Remove-DororongBoundaryBackground $source
    $resizedSource = Resize-DororongPremultiplied96 $processedSource
    Assert-Equal ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb) `
        $resizedSource.PixelFormat 'Premultiplied native resize pixel format changed.'
    Assert-Equal 96 $resizedSource.Width 'Premultiplied native resize width changed.'
    Assert-Equal 96 $resizedSource.Height 'Premultiplied native resize height changed.'
    Assert-Equal '3B3D171D2C62134284915D7D162D43F36263761D4EA6344F4AC8BCEA730C5B59' `
        (Get-PngSha256 $resizedSource) `
        'Premultiplied native resize differs from the pre-refactor generator baseline.'
    $productionSeed = Import-DororongBinaryMask $seedPath
    $productionOwnership = Get-DororongBodyOwnership `
        $processedSource $productionSeed $ownershipConfiguration
    Assert-Equal $ownership.MembershipHash $productionOwnership.MembershipHash `
        'Production ownership membership differs from the independent membership.'
    Assert-Equal ($ownership.MembershipRecords -join "`n") `
        ($productionOwnership.MembershipRecords -join "`n") `
        'Production ownership records differ from the independent records.'
    for ($y = 0; $y -lt $mask.Height; $y++)
    {
        for ($x = 0; $x -lt $mask.Width; $x++)
        {
            Assert-Equal $ownership.ExpectedMask[$x,$y] `
                ($productionOwnership.Mask.GetPixel($x, $y).R -eq 255) `
                "Production ownership mask differs from the independent union at ($x,$y)."
        }
    }

    $mutationMessages = [System.Collections.Generic.List[string]]::new()
    $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())

    $mutationDirectory = Join-Path $temporaryRoot ("dororong-ownership-writable-alpha-" + [guid]::NewGuid())
    [System.IO.Directory]::CreateDirectory($mutationDirectory) | Out-Null
    $mutationDirectories.Add($mutationDirectory)
    $mutationConfiguration = Copy-OwnershipConfiguration $ownershipConfiguration
    $mutationConfiguration.InvalidSeedCoordinate = @{ X = 137; Y = 174 }
    $mutationMessage = Assert-ThrowsLike {
        Get-DororongBodyOwnership $processedSource $productionSeed $mutationConfiguration
    } 'Writable-alpha failure' 'Restore invalid seed bit mutation'
    $mutationMessages.Add("restore-seed-bit => $mutationMessage")

    $mutationDirectory = Join-Path $temporaryRoot ("dororong-ownership-remove-component-" + [guid]::NewGuid())
    [System.IO.Directory]::CreateDirectory($mutationDirectory) | Out-Null
    $mutationDirectories.Add($mutationDirectory)
    $mutationSeedPath = Join-Path $mutationDirectory 'mutation-seed.png'
    $mutationSeed = $productionSeed.Clone()
    try
    {
        foreach ($encoded in $ownership.SelectedComponents[0].Pixels)
        {
            $pixelX = $encoded % 225
            $pixelY = [int][Math]::Floor($encoded / 225)
            $mutationSeed.SetPixel($pixelX, $pixelY, [System.Drawing.Color]::FromArgb(255,255,255,255))
        }
        $mutationSeed.Save($mutationSeedPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally
    { $mutationSeed.Dispose() }
    $importedMutationSeed = Import-DororongBinaryMask $mutationSeedPath
    try
    {
        $mutationMessage = Assert-ThrowsLike {
            Get-DororongBodyOwnership $processedSource $importedMutationSeed $ownershipConfiguration
        } 'Component membership failure' 'Remove selected component mutation'
        $mutationMessages.Add("remove-selected-component => $mutationMessage")
    }
    finally
    { $importedMutationSeed.Dispose() }

    $mutationDirectory = Join-Path $temporaryRoot ("dororong-ownership-protected-anchor-" + [guid]::NewGuid())
    [System.IO.Directory]::CreateDirectory($mutationDirectory) | Out-Null
    $mutationDirectories.Add($mutationDirectory)
    $mutationSeedPath = Join-Path $mutationDirectory 'mutation-seed.png'
    $mutationSeed = $productionSeed.Clone()
    try
    {
        $mutationSeed.SetPixel(52, 68, [System.Drawing.Color]::FromArgb(255,255,255,255))
        $mutationSeed.Save($mutationSeedPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally
    { $mutationSeed.Dispose() }
    $importedMutationSeed = Import-DororongBinaryMask $mutationSeedPath
    try
    {
        $mutationMessage = Assert-ThrowsLike {
            Get-DororongBodyOwnership $processedSource $importedMutationSeed $ownershipConfiguration
        } 'Protected-anchor failure' 'Add anchored-component pixel mutation'
        $mutationMessages.Add("add-anchored-pixel => $mutationMessage")
    }
    finally
    { $importedMutationSeed.Dispose() }

    $mutationDirectory = Join-Path $temporaryRoot ("dororong-ownership-diagonal-contact-" + [guid]::NewGuid())
    [System.IO.Directory]::CreateDirectory($mutationDirectory) | Out-Null
    $mutationDirectories.Add($mutationDirectory)
    $mutationConfiguration = Copy-OwnershipConfiguration $ownershipConfiguration
    $mutationConfiguration.ContactOffsets = @(
        @{ X = -1; Y = -1 }, @{ X = 1; Y = -1 },
        @{ X = -1; Y = 1 }, @{ X = 1; Y = 1 }
    )
    $mutationMessage = Assert-ThrowsLike {
        Get-DororongBodyOwnership $processedSource $productionSeed $mutationConfiguration
    } 'Four-neighbor contact assertion failed' 'Diagonal-only contact mutation'
    $mutationMessages.Add("diagonal-only-contact => $mutationMessage")

    $mutationDirectory = Join-Path $temporaryRoot ("dororong-ownership-anchor-set-" + [guid]::NewGuid())
    [System.IO.Directory]::CreateDirectory($mutationDirectory) | Out-Null
    $mutationDirectories.Add($mutationDirectory)
    $mutationConfiguration = Copy-OwnershipConfiguration $ownershipConfiguration
    $mutationConfiguration.ProtectedAnchors[0].X = 53
    $mutationMessage = Assert-ThrowsLike {
        Get-DororongBodyOwnership $processedSource $productionSeed $mutationConfiguration
    } 'Protected anchor-set failure' 'Changed anchor-coordinate mutation'
    $mutationMessages.Add("change-anchor-coordinate => $mutationMessage")

    $firstMismatch = $null
    for ($y = 0; $y -lt $mask.Height -and $null -eq $firstMismatch; $y++)
    {
        for ($x = 0; $x -lt $mask.Width; $x++)
        {
            $actualWritable = $mask.GetPixel($x, $y).R -eq 255
            if ($actualWritable -ne $ownership.ExpectedMask[$x,$y])
            {
                $firstMismatch = "$x,$y"
                break
            }
        }
    }
    Assert-True ($null -eq $firstMismatch) `
        "Final body-region mask differs from the independent ownership union at ($firstMismatch); observed final count $writableCount."

    $unvisited = [System.Collections.Generic.HashSet[string]]::new($writable)
    $componentCount = 0
    while ($unvisited.Count -gt 0)
    {
        $componentCount++
        $first = $unvisited.GetEnumerator()
        [void]$first.MoveNext()
        $queue = [System.Collections.Generic.Queue[string]]::new()
        $queue.Enqueue($first.Current)
        [void]$unvisited.Remove($first.Current)

        while ($queue.Count -gt 0)
        {
            $coordinate = $queue.Dequeue().Split(',')
            $currentX = [int]$coordinate[0]
            $currentY = [int]$coordinate[1]
            foreach ($offset in @(@(-1,0), @(1,0), @(0,-1), @(0,1)))
            {
                $neighbor = "$($currentX + $offset[0]),$($currentY + $offset[1])"
                if ($unvisited.Remove($neighbor))
                { $queue.Enqueue($neighbor) }
            }
        }
    }
    Assert-Equal 1 $componentCount 'Body-region mask does not contain exactly one writable component.'

    foreach ($fieldName in @('HairAnchors', 'BodyNormals', 'ProtectedPoints', 'FillSamples'))
    {
        Assert-True $authority.ContainsKey($fieldName) "Authority fixture is missing '$fieldName'."
        Assert-True (@($authority[$fieldName]).Count -gt 0) "Authority fixture '$fieldName' is empty."
    }

    $hairAnchors = @($authority.HairAnchors)
    Assert-True ($hairAnchors.Count -ge 6) 'Authority fixture has fewer than six hair anchors.'
    foreach ($kind in @('Straight', 'Diagonal', 'Curve'))
    {
        Assert-True (@($hairAnchors | Where-Object Kind -eq $kind).Count -ge 2) `
            "Authority fixture has fewer than two '$kind' hair anchors."
    }
    foreach ($anchor in $hairAnchors)
    {
        Assert-True (-not [string]::IsNullOrWhiteSpace([string]$anchor.Name)) 'A hair anchor has no name.'
        Assert-True ($anchor.Kind -in @('Straight', 'Diagonal', 'Curve')) `
            "Hair anchor '$($anchor.Name)' has an invalid kind."
        Assert-PointArray $anchor.SourceSamples "Hair anchor '$($anchor.Name)' SourceSamples"
        Assert-PointArray $anchor.NativeSamples "Hair anchor '$($anchor.Name)' NativeSamples"
        Assert-Point $anchor.Fill "Hair anchor '$($anchor.Name)' Fill"
        Assert-Point $anchor.Ink "Hair anchor '$($anchor.Name)' Ink"
    }

    $requiredBodyNormals = @(
        'FrontOuter', 'FrontFoot', 'FrontInner',
        'FirstValley', 'FirstUnderside',
        'CenterOuter', 'CenterFoot', 'CenterInner',
        'SecondValley', 'SecondUnderside',
        'RearOuter', 'RearFoot', 'RearInner',
        'UpperRearRim', 'LowerRearRim'
    )
    $bodyNormals = @($authority.BodyNormals)
    $bodyNormalNames = @($bodyNormals | ForEach-Object { [string]$_.Name })
    foreach ($requiredName in $requiredBodyNormals)
    {
        Assert-True ($bodyNormalNames -contains $requiredName) `
            "Authority fixture is missing body normal '$requiredName'."
    }
    Assert-Equal $bodyNormalNames.Count @($bodyNormalNames | Sort-Object -Unique).Count `
        'Authority fixture contains duplicate body-normal names.'
    foreach ($normal in $bodyNormals)
    {
        Assert-PointArray $normal.SourceSamples "Body normal '$($normal.Name)' SourceSamples"
        Assert-PointArray $normal.NativeSamples "Body normal '$($normal.Name)' NativeSamples"
        Assert-Point $normal.Fill "Body normal '$($normal.Name)' Fill"
    }

    $secondUnderside = @($bodyNormals | Where-Object Name -eq 'SecondUnderside')[0]
    Assert-ScanCrossesOutline $source $secondUnderside.SourceSamples 'SecondUnderside source scan'
    Assert-ScanCrossesOutline $native $secondUnderside.NativeSamples 'SecondUnderside native scan'

    $historicalEndpointNames = @(
        'LegalEndpoint-FrontOcclusion',
        'LegalEndpoint-RearOcclusion'
    )
    $protectedPoints = @($authority.ProtectedPoints | Where-Object {
        [string]$_.Name -notin $historicalEndpointNames
    })
    $protectedNames = @($protectedPoints | ForEach-Object { [string]$_.Name })
    foreach ($category in @('Head', 'Hair', 'Face', 'Mouth', 'Eyes', 'Rose', 'Bow', 'Ribbons'))
    {
        Assert-True (@($protectedPoints | Where-Object { $_.Name -like "$category-*" }).Count -ge 2) `
            "Authority fixture has fewer than two '$category' protected points."
    }
    foreach ($requiredName in @('NoTailRear-Upper', 'NoTailRear-Lower'))
    {
        Assert-True ($protectedNames -contains $requiredName) `
            "Authority fixture is missing protected point '$requiredName'."
    }
    foreach ($protected in $protectedPoints)
    {
        $protectedX = [int]$protected.X
        $protectedY = [int]$protected.Y
        Assert-True ($protectedX -ge 0 -and $protectedX -lt 225 -and $protectedY -ge 0 -and $protectedY -lt 225) `
            "Protected point '$($protected.Name)' is out of bounds."
        Assert-Equal 0 $mask.GetPixel($protectedX, $protectedY).R `
            "Protected point '$($protected.Name)' is writable."
    }

    foreach ($occlusionSide in @(
        @{ Name = 'Front'; Body = @(118,151); Protected = @(119,151) },
        @{ Name = 'Rear'; Body = @(161,116); Protected = @(160,116) }
    ))
    {
        $bodyX = [int]$occlusionSide.Body[0]
        $bodyY = [int]$occlusionSide.Body[1]
        $protectedX = [int]$occlusionSide.Protected[0]
        $protectedY = [int]$occlusionSide.Protected[1]
        Assert-Equal 255 $source.GetPixel($bodyX, $bodyY).A `
            "$($occlusionSide.Name) occlusion body side is not source-opaque."
        Assert-Equal 255 $source.GetPixel($protectedX, $protectedY).A `
            "$($occlusionSide.Name) occlusion protected side is not source-opaque."
        Assert-Equal 255 $mask.GetPixel($bodyX, $bodyY).R `
            "$($occlusionSide.Name) occlusion body side is not writable."
        Assert-Equal 0 $mask.GetPixel($protectedX, $protectedY).R `
            "$($occlusionSide.Name) occlusion protected side is writable."
    }

    foreach ($sample in @($authority.FillSamples))
    {
        $sampleX = [int]$sample.X
        $sampleY = [int]$sample.Y
        $region = @($sample.Region)
        Assert-Equal 4 $region.Count "Fill sample '$($sample.Name)' does not have four region bounds."
        Assert-True ($region[0] -le $region[2] -and $region[1] -le $region[3]) `
            "Fill sample '$($sample.Name)' region bounds are not ordered."
        Assert-True ($sampleX -ge $region[0] -and $sampleX -le $region[2] -and `
            $sampleY -ge $region[1] -and $sampleY -le $region[3]) `
            "Fill sample '$($sample.Name)' is outside its region."
        Assert-True ($sampleX -ge 0 -and $sampleX -lt 225 -and $sampleY -ge 0 -and $sampleY -lt 225) `
            "Fill sample '$($sample.Name)' is out of bounds."

        $sourcePixel = $source.GetPixel($sampleX, $sampleY)
        Assert-Equal 255 $sourcePixel.A "Fill sample '$($sample.Name)' is not opaque in the source."
        Assert-True ($sourcePixel.R -ge 225 -and $sourcePixel.G -ge 225 -and $sourcePixel.B -ge 225) `
            "Fill sample '$($sample.Name)' is not near-white in the source."
        Assert-Equal 255 $mask.GetPixel($sampleX, $sampleY).R `
            "Fill sample '$($sample.Name)' is not writable."
    }

    $maskHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $maskPath).Hash
    Assert-Equal 'D08B3A941C662F1CBC55C486C13FD4C6CD8901DA9CD5CF8512509698219FE46F' `
        $maskHash 'Reviewed complete body-ownership mask changed.'
    foreach ($mutationMessage in $mutationMessages)
    { Write-Output "OWNERSHIP MUTATION PASS $mutationMessage" }
    Write-Output "BODY MASK PASS hash=$maskHash count=$writableCount bounds=$minimumX,$minimumY-$maximumX,$maximumY"
}
finally
{
    foreach ($mutationDirectory in $mutationDirectories)
    {
        $resolvedMutationDirectory = [System.IO.Path]::GetFullPath($mutationDirectory)
        $resolvedTemporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if ($resolvedMutationDirectory.StartsWith($resolvedTemporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and
            [System.IO.Directory]::Exists($resolvedMutationDirectory))
        { Remove-Item -LiteralPath $resolvedMutationDirectory -Recurse -Force }
        Assert-True (-not [System.IO.Directory]::Exists($resolvedMutationDirectory)) `
            "Mutation cleanup readback failure: directory remains at '$resolvedMutationDirectory'."
    }
    if ($null -ne $productionOwnership -and $null -ne $productionOwnership.Mask)
    { $productionOwnership.Mask.Dispose() }
    if ($null -ne $productionSeed) { $productionSeed.Dispose() }
    if ($null -ne $resizedSource) { $resizedSource.Dispose() }
    if ($null -ne $processedSource) { $processedSource.Dispose() }
    $seed.Dispose()
    $mask.Dispose()
    $native.Dispose()
    $source.Dispose()
}
