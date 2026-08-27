param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(Mandatory = $true)]
    [string]$EvidenceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$expectedSourceHash = 'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504'
$canvasSize = 225
$seedX = 160
$seedY = 114
$nearWhiteFloor = 225
$maximumContourDistance = 4
$maximumContourChroma = 12

function ConvertFrom-MaskRun([string]$Run)
{
    if ($Run -notmatch '^(?<Y>\d+):(?<StartX>\d+)-(?<EndX>\d+)$')
    { throw "Invalid body-mask run '$Run'." }
    return @{ Y=[int]$Matches.Y; StartX=[int]$Matches.StartX; EndX=[int]$Matches.EndX }
}

function Add-BoundarySeed(
    [int]$X,
    [int]$Y,
    [bool[,]]$NearWhite,
    [bool[,]]$Visited,
    [System.Collections.Generic.Queue[int]]$Queue)
{
    if ($NearWhite[$X,$Y] -and -not $Visited[$X,$Y])
    {
        $Visited[$X,$Y] = $true
        $Queue.Enqueue(($Y * $canvasSize) + $X)
    }
}

function Save-NearestNeighbor([System.Drawing.Bitmap]$Bitmap, [string]$Path, [int]$Scale)
{
    $scaled = [System.Drawing.Bitmap]::new(
        $Bitmap.Width * $Scale,
        $Bitmap.Height * $Scale,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        $graphics = [System.Drawing.Graphics]::FromImage($scaled)
        try
        {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighSpeed
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
            $graphics.DrawImage(
                $Bitmap,
                [System.Drawing.Rectangle]::new(0, 0, $scaled.Width, $scaled.Height),
                0,
                0,
                $Bitmap.Width,
                $Bitmap.Height,
                [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally
        { $graphics.Dispose() }

        if ([System.IO.File]::Exists($Path))
        { [System.IO.File]::Delete($Path) }
        $scaled.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally
    { $scaled.Dispose() }
}

function Get-BlendedColor(
    [System.Drawing.Color]$Source,
    [int]$OverlayRed,
    [int]$OverlayGreen,
    [int]$OverlayBlue,
    [int]$OverlayWeight)
{
    $sourceWeight = 255 - $OverlayWeight
    $red = [int][Math]::Round((($Source.R * $sourceWeight) + ($OverlayRed * $OverlayWeight)) / 255.0)
    $green = [int][Math]::Round((($Source.G * $sourceWeight) + ($OverlayGreen * $OverlayWeight)) / 255.0)
    $blue = [int][Math]::Round((($Source.B * $sourceWeight) + ($OverlayBlue * $OverlayWeight)) / 255.0)
    return [System.Drawing.Color]::FromArgb(255, $red, $green, $blue)
}

# Reviewed source-only mask: exposed contour pixels are retained, while grayscale
# occluder-side pixels along the hair and ribbon contacts are removed row by row.
$bodyMaskRuns = @(
    '110:160-161'
    '111:159-162'
    '112:159-163'
    '113:160-164'
    '114:160-165'
    '115:160-166'
    '116:161-167'
    '117:161-168'
    '118:161-169'
    '119:161-170'
    '120:160-171'
    '121:160-172'
    '122:159-173'
    '123:159-173'
    '124:158-174'
    '125:158-174'
    '126:137-137'
    '126:157-174'
    '127:137-138'
    '127:157-175'
    '128:136-138'
    '128:156-175'
    '129:136-138'
    '129:156-176'
    '130:136-138'
    '130:155-155'
    '130:157-176'
    '131:135-139'
    '131:154-176'
    '132:135-139'
    '132:153-176'
    '133:134-139'
    '133:153-176'
    '134:134-139'
    '134:152-176'
    '135:133-140'
    '135:151-176'
    '136:132-140'
    '136:151-177'
    '137:132-140'
    '137:151-177'
    '138:131-141'
    '138:150-177'
    '139:130-141'
    '139:150-177'
    '140:130-142'
    '140:150-177'
    '141:129-142'
    '141:149-177'
    '142:130-143'
    '142:149-177'
    '143:127-127'
    '143:129-144'
    '143:148-177'
    '144:126-145'
    '144:147-176'
    '145:125-176'
    '146:125-176'
    '147:123-176'
    '148:122-176'
    '149:120-176'
    '150:119-124'
    '150:126-176'
    '151:118-118'
    '151:120-176'
    '152:101-102'
    '152:118-176'
    '153:49-52'
    '153:99-102'
    '153:118-175'
    '154:48-57'
    '154:98-102'
    '154:117-175'
    '155:48-69'
    '155:72-72'
    '155:78-83'
    '155:85-87'
    '155:94-101'
    '155:116-174'
    '156:49-99'
    '156:101-101'
    '156:116-144'
    '156:146-174'
    '157:50-101'
    '157:115-173'
    '158:50-100'
    '158:114-172'
    '159:51-99'
    '159:113-144'
    '159:148-172'
    '160:39-39'
    '160:51-99'
    '160:112-143'
    '160:148-171'
    '161:38-98'
    '161:111-170'
    '162:38-98'
    '162:110-169'
    '163:38-98'
    '163:109-169'
    '164:38-98'
    '164:109-168'
    '165:38-98'
    '165:107-167'
    '166:38-99'
    '166:107-167'
    '167:38-99'
    '167:104-166'
    '168:39-165'
    '169:39-165'
    '170:39-165'
    '171:40-136'
    '171:139-164'
    '172:40-135'
    '172:139-164'
    '173:40-134'
    '173:139-164'
    '174:41-133'
    '174:138-164'
    '175:41-132'
    '175:138-164'
    '176:42-62'
    '176:64-130'
    '176:138-164'
    '177:42-62'
    '177:66-128'
    '177:138-164'
    '178:43-62'
    '178:68-125'
    '178:138-164'
    '179:43-63'
    '179:71-124'
    '179:138-164'
    '180:44-63'
    '180:77-121'
    '180:139-165'
    '181:44-63'
    '181:79-118'
    '181:139-165'
    '182:45-63'
    '182:80-113'
    '182:140-165'
    '183:45-62'
    '183:80-113'
    '183:140-165'
    '184:46-62'
    '184:80-113'
    '184:141-165'
    '185:47-62'
    '185:81-113'
    '185:141-165'
    '186:48-61'
    '186:81-113'
    '186:142-165'
    '187:49-60'
    '187:82-113'
    '187:143-165'
    '188:50-59'
    '188:82-113'
    '188:143-165'
    '189:51-58'
    '189:83-113'
    '189:144-165'
    '190:53-57'
    '190:84-113'
    '190:145-164'
    '191:85-113'
    '191:146-163'
    '192:86-113'
    '192:147-162'
    '193:86-113'
    '193:148-161'
    '194:87-113'
    '194:149-160'
    '195:88-113'
    '195:150-159'
    '196:89-112'
    '196:151-158'
    '197:90-112'
    '197:152-156'
    '198:91-111'
    '199:93-110'
    '200:94-109'
    '201:95-108'
    '202:98-107'
    '203:100-106'
    '204:103-104'
)

$fullSourcePath = [System.IO.Path]::GetFullPath($SourcePath)
$fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$fullEvidenceDirectory = [System.IO.Path]::GetFullPath($EvidenceDirectory)

if (-not [System.IO.File]::Exists($fullSourcePath))
{ throw "Canonical source is missing: $fullSourcePath" }
if ($fullSourcePath -eq $fullOutputPath)
{ throw 'OutputPath must not overwrite the canonical source.' }

$sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullSourcePath).Hash
if ($sourceHash -ne $expectedSourceHash)
{ throw "Canonical source hash changed. Expected $expectedSourceHash, observed $sourceHash." }

$outputParent = Split-Path -Parent $fullOutputPath
[System.IO.Directory]::CreateDirectory($outputParent) | Out-Null
[System.IO.Directory]::CreateDirectory($fullEvidenceDirectory) | Out-Null

$source = [System.Drawing.Bitmap]::new($fullSourcePath)
try
{
    if ($source.Width -ne $canvasSize -or $source.Height -ne $canvasSize)
    { throw "Canonical source dimensions changed. Expected 225x225, observed $($source.Width)x$($source.Height)." }

    $nearWhite = [bool[,]]::new($canvasSize, $canvasSize)
    for ($y = 0; $y -lt $canvasSize; $y++)
    {
        for ($x = 0; $x -lt $canvasSize; $x++)
        {
            $pixel = $source.GetPixel($x, $y)
            $nearWhite[$x,$y] = $pixel.R -ge $nearWhiteFloor -and `
                $pixel.G -ge $nearWhiteFloor -and $pixel.B -ge $nearWhiteFloor
        }
    }

    $boundaryBackground = [bool[,]]::new($canvasSize, $canvasSize)
    $queue = [System.Collections.Generic.Queue[int]]::new()
    for ($index = 0; $index -lt $canvasSize; $index++)
    {
        Add-BoundarySeed $index 0 $nearWhite $boundaryBackground $queue
        Add-BoundarySeed $index ($canvasSize - 1) $nearWhite $boundaryBackground $queue
        Add-BoundarySeed 0 $index $nearWhite $boundaryBackground $queue
        Add-BoundarySeed ($canvasSize - 1) $index $nearWhite $boundaryBackground $queue
    }

    while ($queue.Count -gt 0)
    {
        $encoded = $queue.Dequeue()
        $currentX = $encoded % $canvasSize
        $currentY = [int][Math]::Floor($encoded / $canvasSize)
        foreach ($offset in @(@(-1,0), @(1,0), @(0,-1), @(0,1)))
        {
            $nextX = $currentX + $offset[0]
            $nextY = $currentY + $offset[1]
            if ($nextX -lt 0 -or $nextX -ge $canvasSize -or $nextY -lt 0 -or $nextY -ge $canvasSize)
            { continue }
            if ($nearWhite[$nextX,$nextY] -and -not $boundaryBackground[$nextX,$nextY])
            {
                $boundaryBackground[$nextX,$nextY] = $true
                $queue.Enqueue(($nextY * $canvasSize) + $nextX)
            }
        }
    }

    if (-not $nearWhite[$seedX,$seedY] -or $boundaryBackground[$seedX,$seedY])
    { throw "Fixed body seed ($seedX,$seedY) is not in an internal near-white component." }

    $bodyInterior = [bool[,]]::new($canvasSize, $canvasSize)
    $queue.Clear()
    $bodyInterior[$seedX,$seedY] = $true
    $queue.Enqueue(($seedY * $canvasSize) + $seedX)
    $bodyInteriorCount = 0
    while ($queue.Count -gt 0)
    {
        $encoded = $queue.Dequeue()
        $currentX = $encoded % $canvasSize
        $currentY = [int][Math]::Floor($encoded / $canvasSize)
        $bodyInteriorCount++
        foreach ($offset in @(@(-1,0), @(1,0), @(0,-1), @(0,1)))
        {
            $nextX = $currentX + $offset[0]
            $nextY = $currentY + $offset[1]
            if ($nextX -lt 0 -or $nextX -ge $canvasSize -or $nextY -lt 0 -or $nextY -ge $canvasSize)
            { continue }
            if ($nearWhite[$nextX,$nextY] -and -not $boundaryBackground[$nextX,$nextY] -and `
                -not $bodyInterior[$nextX,$nextY])
            {
                $bodyInterior[$nextX,$nextY] = $true
                $queue.Enqueue(($nextY * $canvasSize) + $nextX)
            }
        }
    }

    $componentVisited = [bool[,]]::new($canvasSize, $canvasSize)
    for ($y = 0; $y -lt $canvasSize; $y++)
    {
        for ($x = 0; $x -lt $canvasSize; $x++)
        {
            if ($bodyInterior[$x,$y])
            { $componentVisited[$x,$y] = $true }
        }
    }

    $largestOtherComponent = 0
    for ($y = 0; $y -lt $canvasSize; $y++)
    {
        for ($x = 0; $x -lt $canvasSize; $x++)
        {
            if (-not $nearWhite[$x,$y] -or $boundaryBackground[$x,$y] -or $componentVisited[$x,$y])
            { continue }

            $componentSize = 0
            $queue.Clear()
            $componentVisited[$x,$y] = $true
            $queue.Enqueue(($y * $canvasSize) + $x)
            while ($queue.Count -gt 0)
            {
                $encoded = $queue.Dequeue()
                $currentX = $encoded % $canvasSize
                $currentY = [int][Math]::Floor($encoded / $canvasSize)
                $componentSize++
                foreach ($offset in @(@(-1,0), @(1,0), @(0,-1), @(0,1)))
                {
                    $nextX = $currentX + $offset[0]
                    $nextY = $currentY + $offset[1]
                    if ($nextX -lt 0 -or $nextX -ge $canvasSize -or $nextY -lt 0 -or $nextY -ge $canvasSize)
                    { continue }
                    if ($nearWhite[$nextX,$nextY] -and -not $boundaryBackground[$nextX,$nextY] -and `
                        -not $componentVisited[$nextX,$nextY])
                    {
                        $componentVisited[$nextX,$nextY] = $true
                        $queue.Enqueue(($nextY * $canvasSize) + $nextX)
                    }
                }
            }
            if ($componentSize -gt $largestOtherComponent)
            { $largestOtherComponent = $componentSize }
        }
    }

    if ($bodyInteriorCount -le $largestOtherComponent)
    {
        throw "Fixed-seed component is not the largest internal near-white component. " +
            "Seed count $bodyInteriorCount; largest other count $largestOtherComponent."
    }

    $distance = [int[,]]::new($canvasSize, $canvasSize)
    $queue.Clear()
    for ($y = 0; $y -lt $canvasSize; $y++)
    {
        for ($x = 0; $x -lt $canvasSize; $x++)
        {
            $distance[$x,$y] = [int]::MaxValue
            if ($bodyInterior[$x,$y])
            {
                $distance[$x,$y] = 0
                $queue.Enqueue(($y * $canvasSize) + $x)
            }
        }
    }

    while ($queue.Count -gt 0)
    {
        $encoded = $queue.Dequeue()
        $currentX = $encoded % $canvasSize
        $currentY = [int][Math]::Floor($encoded / $canvasSize)
        $nextDistance = $distance[$currentX,$currentY] + 1
        if ($nextDistance -gt $maximumContourDistance)
        { continue }
        foreach ($offset in @(@(-1,0), @(1,0), @(0,-1), @(0,1)))
        {
            $nextX = $currentX + $offset[0]
            $nextY = $currentY + $offset[1]
            if ($nextX -lt 0 -or $nextX -ge $canvasSize -or $nextY -lt 0 -or $nextY -ge $canvasSize)
            { continue }
            if ($nextDistance -lt $distance[$nextX,$nextY])
            {
                $distance[$nextX,$nextY] = $nextDistance
                $queue.Enqueue(($nextY * $canvasSize) + $nextX)
            }
        }
    }

    $mask = [Drawing.Bitmap]::new(225,225,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        for ($y=0; $y -lt 225; $y++)
        {
            for ($x=0; $x -lt 225; $x++)
            { $mask.SetPixel($x,$y,[Drawing.Color]::FromArgb(255,0,0,0)) }
        }
        foreach ($encodedRun in $bodyMaskRuns)
        {
            $run = ConvertFrom-MaskRun $encodedRun
            if ($run.Y -lt 0 -or $run.Y -ge $canvasSize -or $run.StartX -lt 0 -or `
                $run.EndX -ge $canvasSize -or $run.StartX -gt $run.EndX)
            { throw "Body-mask run is out of bounds: $encodedRun" }
            for ($x=$run.StartX; $x -le $run.EndX; $x++)
            {
                if ($mask.GetPixel($x,$run.Y).R -eq 255)
                { throw "Body-mask runs overlap at ($x,$($run.Y))." }
                $mask.SetPixel($x,$run.Y,[Drawing.Color]::FromArgb(255,255,255,255))
            }
        }

        $writableCount = 0
        $minimumX = $canvasSize
        $minimumY = $canvasSize
        $maximumX = -1
        $maximumY = -1
        for ($y = 0; $y -lt $canvasSize; $y++)
        {
            for ($x = 0; $x -lt $canvasSize; $x++)
            {
                $isWritable = $mask.GetPixel($x,$y).R -eq 255
                if ($bodyInterior[$x,$y] -and -not $isWritable)
                { throw "Literal mask omits fixed-seed body fill at ($x,$y)." }
                if (-not $isWritable)
                { continue }

                $writableCount++
                if ($x -lt $minimumX) { $minimumX = $x }
                if ($y -lt $minimumY) { $minimumY = $y }
                if ($x -gt $maximumX) { $maximumX = $x }
                if ($y -gt $maximumY) { $maximumY = $y }

                if ($bodyInterior[$x,$y])
                { continue }
                $pixel = $source.GetPixel($x,$y)
                $maximumChannel = [Math]::Max($pixel.R, [Math]::Max($pixel.G, $pixel.B))
                $minimumChannel = [Math]::Min($pixel.R, [Math]::Min($pixel.G, $pixel.B))
                if ($distance[$x,$y] -gt $maximumContourDistance -or `
                    ($maximumChannel - $minimumChannel) -gt $maximumContourChroma -or `
                    ($pixel.R -eq 255 -and $pixel.G -eq 255 -and $pixel.B -eq 255))
                { throw "Literal mask includes a pixel outside the source-derived contour capture at ($x,$y)." }
            }
        }

        $visited = [bool[,]]::new($canvasSize, $canvasSize)
        $componentCount = 0
        for ($y = 0; $y -lt $canvasSize; $y++)
        {
            for ($x = 0; $x -lt $canvasSize; $x++)
            {
                if ($mask.GetPixel($x,$y).R -ne 255 -or $visited[$x,$y])
                { continue }
                $componentCount++
                $visited[$x,$y] = $true
                $queue.Clear()
                $queue.Enqueue(($y * $canvasSize) + $x)
                while ($queue.Count -gt 0)
                {
                    $encoded = $queue.Dequeue()
                    $currentX = $encoded % $canvasSize
                    $currentY = [int][Math]::Floor($encoded / $canvasSize)
                    foreach ($offset in @(@(-1,0), @(1,0), @(0,-1), @(0,1)))
                    {
                        $nextX = $currentX + $offset[0]
                        $nextY = $currentY + $offset[1]
                        if ($nextX -lt 0 -or $nextX -ge $canvasSize -or `
                            $nextY -lt 0 -or $nextY -ge $canvasSize)
                        { continue }
                        if (-not $visited[$nextX,$nextY] -and $mask.GetPixel($nextX,$nextY).R -eq 255)
                        {
                            $visited[$nextX,$nextY] = $true
                            $queue.Enqueue(($nextY * $canvasSize) + $nextX)
                        }
                    }
                }
            }
        }
        if ($componentCount -ne 1)
        { throw "Literal mask has $componentCount writable components; expected one." }

        if ([System.IO.File]::Exists($fullOutputPath))
        { [System.IO.File]::Delete($fullOutputPath) }
        $mask.Save($fullOutputPath, [System.Drawing.Imaging.ImageFormat]::Png)

        $evidenceSourcePath = Join-Path $fullEvidenceDirectory 'dororong-canonical-source.png'
        [System.IO.File]::Copy($fullSourcePath, $evidenceSourcePath, $true)

        $evidenceMaskPath = Join-Path $fullEvidenceDirectory 'dororong-body-region-mask.png'
        if ([System.IO.File]::Exists($evidenceMaskPath))
        { [System.IO.File]::Delete($evidenceMaskPath) }
        $mask.Save($evidenceMaskPath, [System.Drawing.Imaging.ImageFormat]::Png)

        $redOverlay = [System.Drawing.Bitmap]::new(
            $canvasSize,
            $canvasSize,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $contourOverlay = [System.Drawing.Bitmap]::new(
            $canvasSize,
            $canvasSize,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try
        {
            for ($y = 0; $y -lt $canvasSize; $y++)
            {
                for ($x = 0; $x -lt $canvasSize; $x++)
                {
                    $sourcePixel = $source.GetPixel($x,$y)
                    if ($mask.GetPixel($x,$y).R -eq 255)
                    { $redOverlay.SetPixel($x,$y,(Get-BlendedColor $sourcePixel 255 0 0 115)) }
                    else
                    { $redOverlay.SetPixel($x,$y,[System.Drawing.Color]::FromArgb(255,$sourcePixel.R,$sourcePixel.G,$sourcePixel.B)) }

                    if ($bodyInterior[$x,$y])
                    { $contourOverlay.SetPixel($x,$y,(Get-BlendedColor $sourcePixel 0 150 255 90)) }
                    elseif ($mask.GetPixel($x,$y).R -eq 255)
                    { $contourOverlay.SetPixel($x,$y,(Get-BlendedColor $sourcePixel 255 128 0 170)) }
                    else
                    { $contourOverlay.SetPixel($x,$y,[System.Drawing.Color]::FromArgb(255,$sourcePixel.R,$sourcePixel.G,$sourcePixel.B)) }
                }
            }
            $contourOverlay.SetPixel($seedX,$seedY,[System.Drawing.Color]::FromArgb(255,255,0,255))

            $redOverlayPath = Join-Path $fullEvidenceDirectory 'dororong-body-region-mask-overlay.png'
            $contourOverlayPath = Join-Path $fullEvidenceDirectory 'dororong-source-body-contour-capture.png'
            if ([System.IO.File]::Exists($redOverlayPath))
            { [System.IO.File]::Delete($redOverlayPath) }
            if ([System.IO.File]::Exists($contourOverlayPath))
            { [System.IO.File]::Delete($contourOverlayPath) }
            $redOverlay.Save($redOverlayPath, [System.Drawing.Imaging.ImageFormat]::Png)
            $contourOverlay.Save($contourOverlayPath, [System.Drawing.Imaging.ImageFormat]::Png)
            Save-NearestNeighbor $redOverlay `
                (Join-Path $fullEvidenceDirectory 'dororong-body-region-mask-overlay-4x.png') 4
            Save-NearestNeighbor $mask `
                (Join-Path $fullEvidenceDirectory 'dororong-body-region-mask-4x.png') 4
            Save-NearestNeighbor $contourOverlay `
                (Join-Path $fullEvidenceDirectory 'dororong-source-body-contour-capture-4x.png') 4
        }
        finally
        {
            $contourOverlay.Dispose()
            $redOverlay.Dispose()
        }

        $maskHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullOutputPath).Hash
        Write-Output "SOURCE BODY COMPONENT hash=$sourceHash seed=$seedX,$seedY count=$bodyInteriorCount largestOther=$largestOtherComponent"
        Write-Output "BODY MASK GENERATED hash=$maskHash count=$writableCount bounds=$minimumX,$minimumY-$maximumX,$maximumY"
        Write-Output "SOURCE-ONLY EVIDENCE directory=$fullEvidenceDirectory"
    }
    finally
    { $mask.Dispose() }
}
finally
{ $source.Dispose() }
