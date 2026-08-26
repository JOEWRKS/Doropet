param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -ne $Actual)
    {
        throw "$Message Expected '$Expected', observed '$Actual'."
    }
}

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition)
    {
        throw $Message
    }
}

function Assert-Frame([System.Windows.Controls.Image]$Image, [string]$ExpectedFileName, [string]$State)
{
    $sourceUri = $Image.Source.ToString()
    Assert-True $sourceUri.EndsWith($ExpectedFileName, [StringComparison]::OrdinalIgnoreCase) `
        "$State used '$sourceUri' instead of $ExpectedFileName."
}

function New-CoordinateSet([string[]]$Runs)
{
    $coordinates = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($run in $Runs)
    {
        if ($run -notmatch '^(?<Y>\d+):(?<StartX>\d+)-(?<EndX>\d+)$')
        {
            throw "Invalid coordinate run '$run'."
        }

        $y = [int]$Matches.Y
        for ($x = [int]$Matches.StartX; $x -le [int]$Matches.EndX; $x++)
        {
            [void]$coordinates.Add("$x,$y")
        }
    }

    return $coordinates
}

function Test-DarkCore([System.Drawing.Color]$Pixel)
{
    return $Pixel.R -lt 100 -and $Pixel.G -lt 100 -and $Pixel.B -lt 100
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical-source.png'
$repositoryProductionPath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical.png'
$repositoryClosedEyesPath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-closed-eyes.png'
$generatorPath = Join-Path $repositoryRoot 'tools/Generate-CanonicalArt.ps1'

$bodyFixture = New-CoordinateSet @(
    '140:174-174', '141:174-174', '142:174-174', '143:174-174', '144:174-174',
    '145:174-174', '146:174-174', '147:174-174', '148:174-174', '149:174-174',
    '150:173-174', '151:173-173', '152:173-173', '153:172-173', '154:172-172',
    '155:172-172', '156:171-172', '157:170-171', '158:170-170', '159:169-170',
    '160:168-169', '161:168-169', '162:167-168', '163:166-167', '164:166-166',
    '165:165-166', '166:164-165', '167:164-164', '168:163-164', '169:163-163',
    '170:162-162', '171:162-162', '172:162-162', '173:162-162', '174:161-161',
    '175:161-162',
    '177:44-45', '178:45-45', '179:45-45', '180:45-46', '181:46-46',
    '182:46-47', '183:47-48', '184:48-48', '185:48-49',
    '177:60-60', '178:60-60', '179:60-60', '180:60-60', '181:60-60',
    '182:60-60', '183:60-60', '184:60-60', '185:59-60', '186:59-59', '187:58-59',
    '180:81-81', '181:81-82', '182:82-82', '183:82-82', '184:83-83',
    '185:83-83', '186:83-84', '187:84-84', '188:84-85', '189:85-86',
    '190:86-87', '191:86-87', '192:87-88', '193:88-89', '194:89-90', '195:90-91',
    '181:110-111', '183:110-111', '184:111-111', '185:111-111', '186:111-111',
    '187:111-111', '188:111-111', '189:111-111', '190:111-111', '191:111-111',
    '192:110-111', '193:111-111', '194:110-111', '195:110-110', '196:110-110',
    '197:109-110', '198:109-110', '199:108-109',
    '177:141-141', '178:141-141', '179:141-141', '180:141-141', '181:141-142',
    '182:142-142', '183:142-143', '184:143-143', '185:143-144', '186:144-144',
    '187:144-145', '188:145-146', '189:146-146', '190:146-147', '191:147-148',
    '192:148-149', '193:149-150', '194:150-152',
    '176:162-162', '177:162-162', '178:162-162', '179:162-162', '180:162-163',
    '181:162-163', '182:163-163', '183:163-163', '184:163-163', '185:163-163',
    '186:163-163', '187:163-163', '188:162-162', '189:162-162', '190:161-162',
    '191:161-161', '192:159-161', '193:158-160', '194:156-159'
)
$leftEyeStencil = New-CoordinateSet @(
    '114:43-59', '115:44-60', '116:44-62', '117:43-63',
    '118:43-64', '119:43-64', '120:43-65', '121:43-64', '122:43-64',
    '123:43-64', '124:43-64', '125:43-64', '126:43-64', '127:43-64',
    '128:43-64', '129:43-64', '130:43-64', '131:44-63', '132:45-62',
    '133:46-61', '134:48-59', '135:51-57'
)
$rightEyeStencil = New-CoordinateSet @(
    '114:92-100', '115:89-103', '116:86-105', '117:86-106',
    '118:86-106', '119:86-106', '120:86-106', '121:86-106', '122:86-106',
    '123:86-106', '124:86-106', '125:86-106', '126:86-106', '127:86-106',
    '128:86-106', '129:86-106', '130:86-106', '131:86-106', '132:86-106',
    '133:87-105', '134:88-104', '135:90-103', '136:93-103',
    '137:86-103', '138:86-102', '139:88-102', '140:89-101',
    '141:99-101', '142:99-100', '143:99-100'
)
$leftOpenEyeComponent = New-CoordinateSet @(
    '114:43-46', '114:56-59', '115:44-45', '115:58-60',
    '116:44-45', '116:59-61', '117:43-44', '117:60-62',
    '118:43-44', '118:61-63', '119:43-43', '119:62-64',
    '120:43-43', '120:62-65', '121:43-43', '121:62-63',
    '122:43-43', '122:62-63', '123:43-43', '123:62-62',
    '124:43-43', '124:62-62', '125:43-44', '125:61-62',
    '126:44-44', '126:61-62', '127:44-45', '127:60-61',
    '128:45-46', '128:59-60', '129:46-47', '129:57-59',
    '130:48-51', '130:53-57', '131:49-55'
)
$rightOpenEyeComponent = New-CoordinateSet @(
    '115:97-99', '116:86-100', '117:86-102', '118:97-106',
    '119:88-95', '119:99-106', '120:87-96', '120:101-103', '120:105-106',
    '121:86-90', '121:94-98', '121:105-106', '122:86-87', '122:96-99', '122:106-106',
    '123:86-86', '123:98-100', '123:105-106', '124:99-100', '124:105-105',
    '125:100-101', '125:105-105', '126:100-101', '126:105-105',
    '127:101-102', '127:105-105', '128:101-102', '128:104-105',
    '129:101-102', '129:104-105', '130:101-102', '130:104-105',
    '131:101-101', '131:104-104', '132:101-101', '132:104-104',
    '133:101-101', '133:103-104', '134:100-101', '134:103-103',
    '135:100-100', '135:103-103', '136:99-100', '136:103-103',
    '137:98-99', '137:102-103', '138:97-98', '138:102-102',
    '139:94-96', '139:101-102', '140:89-94', '140:101-101',
    '141:101-101', '142:100-100', '143:99-100'
)
$leftLidFixture = New-CoordinateSet @(
    '121:50-58', '122:48-60', '123:47-49', '123:59-61',
    '124:46-48', '124:60-62', '125:46-47', '125:61-62'
)
$rightLidFixture = New-CoordinateSet @(
    '121:92-100', '122:90-102', '123:89-91', '123:101-103',
    '124:88-90', '124:102-104', '125:88-89', '125:103-104'
)

Assert-True (Test-Path -LiteralPath $sourcePath) 'The persisted canonical source asset is missing.'
Assert-True (Test-Path -LiteralPath $repositoryProductionPath) 'The transparent canonical production asset is missing.'
Assert-True (Test-Path -LiteralPath $repositoryClosedEyesPath) 'The bounded closed-eye frame is missing.'
Assert-Equal `
    'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash `
    'The repository source is not the exact user-supplied canonical PNG.'

Add-Type -AssemblyName System.Drawing
$generatedOutputDirectory = Join-Path ([IO.Path]::GetTempPath()) "dororong-exact-art-$([Guid]::NewGuid().ToString('N'))"
try
{
    $generatorOutput = & pwsh -NoProfile -File $generatorPath -SourcePath $sourcePath -OutputDirectory $generatedOutputDirectory 2>&1
    Assert-Equal 0 $LASTEXITCODE "The canonical-art generator failed: $($generatorOutput -join [Environment]::NewLine)"

    $productionPath = Join-Path $generatedOutputDirectory 'dororong-canonical.png'
    $closedEyesPath = Join-Path $generatedOutputDirectory 'dororong-closed-eyes.png'
    Assert-True (Test-Path -LiteralPath $productionPath) 'The generator did not create the transparent production asset.'
    Assert-True (Test-Path -LiteralPath $closedEyesPath) 'The generator did not create the bounded closed-eye frame.'
    Assert-Equal `
        (Get-FileHash -Algorithm SHA256 -LiteralPath $repositoryProductionPath).Hash `
        (Get-FileHash -Algorithm SHA256 -LiteralPath $productionPath).Hash `
        'The committed production asset is stale relative to the generator.'
    Assert-Equal `
        (Get-FileHash -Algorithm SHA256 -LiteralPath $repositoryClosedEyesPath).Hash `
        (Get-FileHash -Algorithm SHA256 -LiteralPath $closedEyesPath).Hash `
        'The committed closed-eye asset is stale relative to the generator.'

    $source = [System.Drawing.Bitmap]::new($sourcePath)
    $production = [System.Drawing.Bitmap]::new($productionPath)
    $closedEyes = [System.Drawing.Bitmap]::new($closedEyesPath)
    try
    {
    Assert-Equal 225 $source.Width 'The canonical source width changed.'
    Assert-Equal 225 $source.Height 'The canonical source height changed.'
    Assert-Equal $source.Width $production.Width 'The production frame width changed.'
    Assert-Equal $source.Height $production.Height 'The production frame height changed.'
    Assert-Equal $source.Width $closedEyes.Width 'The closed-eye frame width changed.'
    Assert-Equal $source.Height $closedEyes.Height 'The closed-eye frame height changed.'

    $boundaryBackground = [bool[,]]::new($source.Width, $source.Height)
    $queue = [Collections.Generic.Queue[System.Drawing.Point]]::new()

    function Add-NearWhiteBoundaryPoint([int]$X, [int]$Y)
    {
        if ($X -lt 0 -or $Y -lt 0 -or $X -ge $source.Width -or $Y -ge $source.Height -or $boundaryBackground[$X, $Y])
        {
            return
        }

        $pixel = $source.GetPixel($X, $Y)
        if ($pixel.R -ge 225 -and $pixel.G -ge 225 -and $pixel.B -ge 225)
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

    Assert-Equal 185 $bodyFixture.Count 'The approved body-outline fixture coordinate count changed.'
    Assert-Equal 38 $leftLidFixture.Count 'The left two-pixel lid fixture coordinate count changed.'
    Assert-Equal 38 $rightLidFixture.Count 'The right two-pixel lid fixture coordinate count changed.'

    $changedBodyPixels = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $changedClosedEyePixels = 0
    $darkClosedEyePixels = 0
    $darkClosedEyeCoordinates = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($y = 0; $y -lt $source.Height; $y++)
    {
        for ($x = 0; $x -lt $source.Width; $x++)
        {
            $sourcePixel = $source.GetPixel($x, $y)
            $productionPixel = $production.GetPixel($x, $y)
            $expectedAlpha = if ($boundaryBackground[$x, $y]) { 0 } else { 255 }

            Assert-Equal $expectedAlpha $productionPixel.A "Production alpha differs from the boundary-connected background mask at ($x,$y)."
            $coordinate = "$x,$y"
            $productionRgbChanged = $sourcePixel.R -ne $productionPixel.R -or
                $sourcePixel.G -ne $productionPixel.G -or
                $sourcePixel.B -ne $productionPixel.B
            if ($productionRgbChanged)
            {
                Assert-True $bodyFixture.Contains($coordinate) "Production RGB changed outside the approved body-outline fixture at ($x,$y)."
                [void]$changedBodyPixels.Add($coordinate)
            }

            $closedPixel = $closedEyes.GetPixel($x, $y)
            Assert-Equal $productionPixel.A $closedPixel.A "Closed-eye alpha changed at ($x,$y)."
            if ($closedPixel.ToArgb() -ne $productionPixel.ToArgb())
            {
                Assert-True ($leftEyeStencil.Contains($coordinate) -or $rightEyeStencil.Contains($coordinate)) `
                    "The closed-eye frame changed a pixel outside the explicit eye stencils at ($x,$y)."
                $changedClosedEyePixels++
                if (Test-DarkCore $closedPixel)
                {
                    $darkClosedEyePixels++
                    [void]$darkClosedEyeCoordinates.Add($coordinate)
                }
            }
        }
    }

    Assert-Equal $bodyFixture.Count $changedBodyPixels.Count 'The generated body correction did not change the exact approved fixture.'
    foreach ($coordinate in $bodyFixture)
    {
        Assert-True $changedBodyPixels.Contains($coordinate) "The body correction omitted approved fixture coordinate $coordinate."
    }

    foreach ($probe in @(
        @{ Y = 155; StartX = 172; EndX = 174 },
        @{ Y = 160; StartX = 168; EndX = 171 },
        @{ Y = 165; StartX = 165; EndX = 168 },
        @{ Y = 170; StartX = 162; EndX = 164 },
        @{ Y = 175; StartX = 161; EndX = 164 },
        @{ Y = 180; StartX = 43; EndX = 46 },
        @{ Y = 180; StartX = 60; EndX = 62 },
        @{ Y = 181; StartX = 79; EndX = 82 },
        @{ Y = 181; StartX = 110; EndX = 113 },
        @{ Y = 181; StartX = 139; EndX = 142 },
        @{ Y = 181; StartX = 162; EndX = 165 }
    ))
    {
        $darkCoreCount = 0
        for ($x = $probe.StartX; $x -le $probe.EndX; $x++)
        {
            if (Test-DarkCore $production.GetPixel($x, $probe.Y))
            {
                $darkCoreCount++
            }
        }

        Assert-Equal 2 $darkCoreCount "The corrected body dark core is not two pixels at y=$($probe.Y),x=$($probe.StartX)..$($probe.EndX)."
    }

    foreach ($roi in @(
        @{ StartX = 43; EndX = 72; StartY = 105; EndY = 143 },
        @{ StartX = 86; EndX = 118; StartY = 105; EndY = 143 }
    ))
    {
        for ($y = $roi.StartY; $y -le $roi.EndY; $y++)
        {
            for ($x = $roi.StartX; $x -le $roi.EndX; $x++)
            {
                $pixel = $closedEyes.GetPixel($x, $y)
                Assert-True ($pixel.R -ne 250 -or $pixel.G -ne 220 -or $pixel.B -ne 224) `
                    "The forbidden #FADCE0 eye-patch color remains at ($x,$y)."
            }
        }
    }

    Assert-True ($changedClosedEyePixels -gt 0) 'The closed-eye frame contains no derived closed-eye marks.'
    Assert-Equal 76 $darkClosedEyePixels 'The closed-eye frame changed a dark pixel outside the exact two-pixel lid fixtures.'
    foreach ($coordinate in @($leftLidFixture) + @($rightLidFixture))
    {
        Assert-True $darkClosedEyeCoordinates.Contains($coordinate) "The exact lid fixture omitted $coordinate."
    }

    foreach ($coordinate in @('103,135', '103,136', '102,139', '101,141'))
    {
        $parts = $coordinate.Split(',')
        Assert-True `
            ($closedEyes.GetPixel([int]$parts[0], [int]$parts[1]).ToArgb() -ne $production.GetPixel([int]$parts[0], [int]$parts[1]).ToArgb()) `
            "The rejected viewer-right open-eye remnant remains unchanged at $coordinate."
    }

    foreach ($component in @($leftOpenEyeComponent, $rightOpenEyeComponent))
    {
        foreach ($coordinate in $component)
        {
            $parts = $coordinate.Split(',')
            Assert-True `
                ($closedEyes.GetPixel([int]$parts[0], [int]$parts[1]).ToArgb() -ne $production.GetPixel([int]$parts[0], [int]$parts[1]).ToArgb()) `
                "An original open-eye component pixel remains unchanged at $coordinate."
        }
    }

    foreach ($centerProbe in @(@{ X = 54; StartY = 120; EndY = 123 }, @{ X = 96; StartY = 120; EndY = 123 }))
    {
        $darkCoreCount = 0
        for ($y = $centerProbe.StartY; $y -le $centerProbe.EndY; $y++)
        {
            if (Test-DarkCore $closedEyes.GetPixel($centerProbe.X, $y))
            {
                $darkCoreCount++
            }
        }

        Assert-Equal 2 $darkCoreCount "The closed lid does not retain a two-pixel core at x=$($centerProbe.X)."
    }
    Assert-Equal 0 $production.GetPixel(0, 0).A 'The production frame lacks a true alpha-zero outer margin.'
    Assert-Equal 255 $production.GetPixel(112, 112).A 'An opaque canonical character point became transparent.'
    Assert-Equal 255 $production.GetPixel(100, 175).A 'The enclosed white body was incorrectly removed.'
    }
    finally
    {
        $source.Dispose()
        $production.Dispose()
        $closedEyes.Dispose()
    }
}
finally
{
    if ([IO.Directory]::Exists($generatedOutputDirectory))
    {
        [IO.Directory]::Delete($generatedOutputDirectory, $true)
    }
}

$coreAssemblyPath = Join-Path $repositoryRoot "src/Dororong.Core/bin/$Configuration/net8.0/Dororong.Core.dll"
$appAssemblyPath = Join-Path $repositoryRoot "src/Dororong.App/bin/$Configuration/net8.0-windows/Dororong.App.dll"
Add-Type -AssemblyName PresentationFramework
Add-Type -Path $coreAssemblyPath
Add-Type -Path $appAssemblyPath

$presenter = [Dororong.App.Controls.DororongPresenter]::new()
$bodyGroup = [System.Windows.Controls.Canvas]$presenter.FindName('BodyGroup')
$image = [System.Windows.Controls.Image]$presenter.FindName('DororongImage')
Assert-True ($null -ne $bodyGroup) 'The public BodyGroup presenter element is missing.'
Assert-True ($null -ne $image) 'The canonical raster presenter image is missing.'
Assert-Equal 108.0 $bodyGroup.Width 'BodyGroup width changed.'
Assert-Equal 96.0 $bodyGroup.Height 'BodyGroup height changed.'
Assert-Equal 18.0 ([System.Windows.Controls.Canvas]::GetLeft($bodyGroup)) 'BodyGroup horizontal placement changed.'
Assert-Equal 24.0 ([System.Windows.Controls.Canvas]::GetTop($bodyGroup)) 'BodyGroup vertical placement changed.'

$testWindow = [System.Windows.Window]::new()
$testWindow.Width = 144
$testWindow.Height = 144
$testWindow.Left = -10000
$testWindow.Top = -10000
$testWindow.ShowActivated = $false
$testWindow.ShowInTaskbar = $false
$testWindow.WindowStyle = [System.Windows.WindowStyle]::None
$testWindow.Content = $presenter
try
{
    $testWindow.Show()
    $presenter.UpdateLayout()

    # Canonical pixel (100,175) is opaque body. Translate through the image's
    # real arranged geometry rather than duplicating WPF's Stretch layout.
    $opaqueBodyImagePoint = [System.Windows.Point]::new(
        100 * $image.ActualWidth / 225,
        175 * $image.ActualHeight / 225)
    $opaqueBodyPoint = $image.TranslatePoint($opaqueBodyImagePoint, $presenter)
    $visibleBodyHit = $presenter.InputHitTest($opaqueBodyPoint)
    Assert-True ($null -ne $visibleBodyHit) 'The arranged presenter did not hit-test an opaque canonical body point, so BodyGroup mouse input cannot originate.'
    Assert-True ($bodyGroup.IsAncestorOf($visibleBodyHit)) 'The opaque canonical body hit is not a descendant of BodyGroup, so its mouse events cannot bubble to the presenter handlers.'

    $transparentMarginImagePoint = [System.Windows.Point]::new(0.25, 0.25)
    $transparentMarginPoint = $image.TranslatePoint($transparentMarginImagePoint, $presenter)
    $transparentMarginHit = $presenter.InputHitTest($transparentMarginPoint)
    Assert-True ($null -eq $transparentMarginHit) 'The arranged presenter hit-tested an alpha-zero canonical margin as a filled rectangle.'
}
finally
{
    $testWindow.Close()
}

$stateType = [Dororong.Core.Behavior.PetState]
$facing = [Dororong.Core.Behavior.FacingDirection]::Right
foreach ($state in @($stateType::Idle, $stateType::Walk, $stateType::Curious, $stateType::Startled, $stateType::ClickReaction, $stateType::Dragged))
{
    $snapshot = [Dororong.Core.Behavior.PetSnapshot]::new(
        $state,
        [Dororong.Core.Geometry.PointD]::new(0, 0),
        $facing,
        0.1,
        $false,
        $null)
    $presenter.Render($snapshot)
    Assert-Frame $image 'dororong-canonical.png' $state.ToString()
}

$sleep = [Dororong.Core.Behavior.PetSnapshot]::new(
    $stateType::Sleep,
    [Dororong.Core.Geometry.PointD]::new(0, 0),
    $facing,
    0.1,
    $false,
    $null)
$presenter.Render($sleep)
Assert-Frame $image 'dororong-closed-eyes.png' 'Sleep'

$blink = [Dororong.Core.Behavior.PetSnapshot]::new(
    $stateType::Idle,
    [Dororong.Core.Geometry.PointD]::new(0, 0),
    $facing,
    0.68,
    $false,
    $null)
$presenter.Render($blink)
Assert-Frame $image 'dororong-closed-eyes.png' 'Idle blink'

Write-Output 'EXACT ART PASS: source identity/dimensions, edge alpha, explicit body/eye fixtures, two-pixel body probes, closed-lid marks, presenter state mapping, BodyGroup placement, and alpha-aware body hit testing passed.'
