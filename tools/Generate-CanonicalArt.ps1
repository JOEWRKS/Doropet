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

$bodyFixtureRuns = @(
    '140:174-174', '141:174-174', '142:174-174', '143:174-174', '144:174-174',
    '145:174-174', '146:174-174', '147:174-174', '148:174-174', '149:174-174',
    '150:173-174', '151:173-173', '152:173-173', '153:172-173', '154:172-172',
    '155:172-172', '156:171-172', '157:170-171', '158:170-170', '159:169-170',
    '160:168-169', '161:168-169', '162:167-168', '163:166-167', '164:166-166',
    '165:165-166', '166:164-165', '167:164-164', '168:163-164', '169:163-163',
    '170:162-162', '171:162-162', '172:162-162', '173:162-162', '174:161-161',
    '175:161-162',
    '177:44-45:R', '178:45-45:R', '179:45-45:R', '180:45-46:R', '181:46-46:R',
    '182:46-47:R', '183:47-48:R', '184:48-48:R', '185:48-49:R',
    '177:60-60:L', '178:60-60:L', '179:60-60:L', '180:60-60:L', '181:60-60:L',
    '182:60-60:L', '183:60-60:L', '184:60-60:L', '185:59-60:L', '186:59-59:L', '187:58-59:L',
    '180:81-81:R', '181:81-82:R', '182:82-82:R', '183:82-82:R', '184:83-83:R',
    '185:83-83:R', '186:83-84:R', '187:84-84:R', '188:84-85:R', '189:85-86:R',
    '190:86-87:R', '191:86-87:R', '192:87-88:R', '193:88-89:R', '194:89-90:R', '195:90-91:R',
    '181:110-111:L', '183:110-111:L', '184:111-111:L', '185:111-111:L', '186:111-111:L',
    '187:111-111:L', '188:111-111:L', '189:111-111:L', '190:111-111:L', '191:111-111:L',
    '192:110-111:L', '193:111-111:L', '194:110-111:L', '195:110-110:L', '196:110-110:L',
    '197:109-110:L', '198:109-110:L', '199:108-109:L',
    '177:141-141:R', '178:141-141:R', '179:141-141:R', '180:141-141:R', '181:141-142:R',
    '182:142-142:R', '183:142-143:R', '184:143-143:R', '185:143-144:R', '186:144-144:R',
    '187:144-145:R', '188:145-146:R', '189:146-146:R', '190:146-147:R', '191:147-148:R',
    '192:148-149:R', '193:149-150:R', '194:150-152:R',
    '176:162-162:L', '177:162-162:L', '178:162-162:L', '179:162-162:L', '180:162-163:L',
    '181:162-163:L', '182:163-163:L', '183:163-163:L', '184:163-163:L', '185:163-163:L',
    '186:163-163:L', '187:163-163:L', '188:162-162:L', '189:162-162:L', '190:161-162:L',
    '191:161-161:L', '192:159-161:L', '193:158-160:L', '194:156-159:L'
)
$leftEyeStencilRuns = @(
    '114:49-57', '115:46-60', '116:44-62', '117:43-63',
    '118:43-64', '119:43-64', '120:43-64', '121:43-64', '122:43-64',
    '123:43-64', '124:43-64', '125:43-64', '126:43-64', '127:43-64',
    '128:43-64', '129:43-64', '130:43-64', '131:44-63', '132:45-62',
    '133:46-61', '134:48-59', '135:51-57'
)
$rightEyeStencilRuns = @(
    '114:92-100', '115:89-103', '116:87-105', '117:86-106',
    '118:86-106', '119:86-106', '120:86-106', '121:86-106', '122:86-106',
    '123:86-106', '124:86-106', '125:86-106', '126:86-106', '127:86-106',
    '128:86-106', '129:86-106', '130:86-106', '131:86-106', '132:86-106',
    '133:87-105', '134:88-104', '135:90-102', '136:93-99'
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

        foreach ($fixtureRun in $bodyFixtureRuns)
        {
            $run = ConvertFrom-CoordinateRun $fixtureRun
            $sampleX = if ($run.SampleSide -eq 'R') { $run.EndX + 1 } else { $run.StartX - 1 }
            $localBodyColor = $source.GetPixel($sampleX, $run.Y)
            for ($x = $run.StartX; $x -le $run.EndX; $x++)
            {
                $alpha = $production.GetPixel($x, $run.Y).A
                $production.SetPixel(
                    $x,
                    $run.Y,
                    [System.Drawing.Color]::FromArgb($alpha, $localBodyColor.R, $localBodyColor.G, $localBodyColor.B))
            }
        }

        $productionPath = Join-Path $OutputDirectory 'dororong-canonical.png'
        $production.Save($productionPath, [System.Drawing.Imaging.ImageFormat]::Png)

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
                    Runs = $rightEyeStencilRuns; MinimumX = 86; MaximumX = 106; MinimumY = 114; MaximumY = 136
                    TopLeftX = 65; TopLeftY = 134; TopRightX = 106; TopRightY = 142
                    BottomLeftX = 65; BottomLeftY = 136; BottomRightX = 106; BottomRightY = 144
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
