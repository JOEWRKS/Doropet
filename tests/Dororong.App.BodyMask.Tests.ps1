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

function Assert-PointArray([object]$Points, [string]$Label)
{
    Assert-True ($null -ne $Points) "$Label is missing."
    $pointList = @($Points)
    Assert-True ($pointList.Count -gt 0) "$Label is empty."

    foreach ($point in $pointList)
    {
        Assert-True ($point -is [System.Collections.IList]) "$Label contains a non-array point."
        Assert-Equal 2 $point.Count "$Label contains a point that is not an X/Y pair."
        Assert-True ($point[0] -is [int] -and $point[1] -is [int]) "$Label contains a non-integer coordinate."
    }
}

function Assert-Point([object]$Point, [string]$Label)
{
    Assert-True ($null -ne $Point) "$Label is missing."
    $coordinate = @($Point)
    Assert-Equal 2 $coordinate.Count "$Label is not an X/Y pair."
    Assert-True ($coordinate[0] -is [int] -and $coordinate[1] -is [int]) `
        "$Label contains a non-integer coordinate."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical-source.png'
$maskPath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-body-region-mask.png'
$authorityPath = Join-Path $repositoryRoot 'tests/fixtures/dororong-body-outline-authority.psd1'

Assert-Equal 'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash 'Canonical source changed.'
Assert-True (Test-Path -LiteralPath $maskPath) 'Reviewed body-region mask is missing.'
Assert-True (Test-Path -LiteralPath $authorityPath) 'Independent body-outline authority is missing.'
Assert-Equal 'E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $maskPath).Hash 'Reviewed body-region mask changed.'

Add-Type -AssemblyName System.Drawing

$authority = Import-PowerShellDataFile -LiteralPath $authorityPath
$source = [System.Drawing.Bitmap]::new($sourcePath)
$mask = [System.Drawing.Bitmap]::new($maskPath)

try
{
    Assert-Equal 225 $mask.Width 'Body-region mask width changed.'
    Assert-Equal 225 $mask.Height 'Body-region mask height changed.'
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
        Assert-Equal @($anchor.SourceSamples).Count @($anchor.NativeSamples).Count `
            "Hair anchor '$($anchor.Name)' source/native sample counts differ."
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
        Assert-Equal @($normal.SourceSamples).Count @($normal.NativeSamples).Count `
            "Body normal '$($normal.Name)' source/native sample counts differ."
        Assert-Point $normal.Fill "Body normal '$($normal.Name)' Fill"
    }

    $protectedPoints = @($authority.ProtectedPoints)
    $protectedNames = @($protectedPoints | ForEach-Object { [string]$_.Name })
    foreach ($category in @('Head', 'Hair', 'Face', 'Mouth', 'Eyes', 'Rose', 'Bow', 'Ribbons'))
    {
        Assert-True (@($protectedPoints | Where-Object { $_.Name -like "$category-*" }).Count -ge 2) `
            "Authority fixture has fewer than two '$category' protected points."
    }
    foreach ($requiredName in @(
        'NoTailRear-Upper', 'NoTailRear-Lower',
        'LegalEndpoint-FrontOcclusion', 'LegalEndpoint-RearOcclusion'
    ))
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
    Write-Output "BODY MASK PASS hash=$maskHash count=$writableCount bounds=$minimumX,$minimumY-$maximumX,$maximumY"
}
finally
{
    $mask.Dispose()
    $source.Dispose()
}
