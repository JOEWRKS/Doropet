param([string]$Configuration = 'Debug')

$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -ne $Actual) { throw "$Message Expected '$Expected', observed '$Actual'." }
}

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition) { throw $Message }
}

function Assert-Near([double]$Expected, [double]$Actual, [double]$Tolerance, [string]$Message)
{
    if ([Math]::Abs($Expected - $Actual) -gt $Tolerance)
    {
        throw "$Message Expected '$Expected' +/- '$Tolerance', observed '$Actual'."
    }
}

function Assert-Frame([System.Windows.Controls.Image]$Image, [string]$ExpectedFileName, [string]$State)
{
    Assert-True $Image.Source.ToString().EndsWith($ExpectedFileName, [StringComparison]::OrdinalIgnoreCase) `
        "$State did not use $ExpectedFileName."
}

function Test-InBodyProtectionBand([int]$X, [int]$Y)
{
    # Independent and deliberately broader than the generator's hand-owned band.
    return ($X -ge 16 -and $X -le 30 -and $Y -ge 68 -and $Y -le 83) -or
        ($X -ge 23 -and $X -le 40 -and $Y -ge 70 -and $Y -le 82) -or
        ($X -ge 32 -and $X -le 51 -and $Y -ge 73 -and $Y -le 88) -or
        ($X -ge 46 -and $X -le 64 -and $Y -ge 67 -and $Y -le 81) -or
        ($X -ge 57 -and $X -le 72 -and $Y -ge 70 -and $Y -le 87) -or
        ($X -ge 65 -and $X -le 78 -and $Y -ge 57 -and $Y -le 84)
}

function Get-OpticalInk([System.Drawing.Color]$Pixel, [int]$Background)
{
    $alpha = $Pixel.A / 255.0
    $sourceLuminance = (0.2126 * $Pixel.R) + (0.7152 * $Pixel.G) + (0.0722 * $Pixel.B)
    $compositeLuminance = ($alpha * $sourceLuminance) + ((1 - $alpha) * $Background)
    $bodyFillLuminance = ($alpha * 255.0) + ((1 - $alpha) * $Background)
    return [Math]::Max(0.0, ($bodyFillLuminance - $compositeLuminance) / 255.0)
}

function Get-Profile([System.Drawing.Bitmap]$Bitmap, [hashtable]$Definition, [int]$Background)
{
    $samples = @()
    foreach ($coordinate in $Definition.Samples)
    {
        $samples += Get-OpticalInk ($Bitmap.GetPixel([int]$coordinate[0], [int]$coordinate[1])) $Background
    }
    $support = @($samples | Where-Object { $_ -ge 0.10 }).Count
    $runs = 0
    $inRun = $false
    foreach ($sample in $samples)
    {
        if ($sample -ge 0.10 -and -not $inRun) { $runs++; $inRun = $true }
        elseif ($sample -lt 0.10) { $inRun = $false }
    }
    return [pscustomobject]@{
        Name = $Definition.Name
        Peak = ($samples | Measure-Object -Maximum).Maximum
        Width = ($samples | Measure-Object -Sum).Sum
        Support = $support
        Runs = $runs
        Joint = [bool]$Definition.Joint
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical-source.png'
$repositoryOpenPath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical.png'
$repositoryClosedPath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-closed-eyes.png'
$generatorPath = Join-Path $repositoryRoot 'tools/Generate-CanonicalArt.ps1'
Assert-Equal 'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash `
    'The exact approved 225px source changed.'

Add-Type -AssemblyName System.Drawing
$generatedDirectory = Join-Path ([IO.Path]::GetTempPath()) "dororong-native96-$([Guid]::NewGuid().ToString('N'))"
$baselineDirectory = Join-Path ([IO.Path]::GetTempPath()) "dororong-native96-baseline-$([Guid]::NewGuid().ToString('N'))"
try
{
    $generatorOutput = & pwsh -NoProfile -File $generatorPath -SourcePath $sourcePath `
        -OutputDirectory $generatedDirectory -BaselineOutputDirectory $baselineDirectory 2>&1
    Assert-Equal 0 $LASTEXITCODE "The real generator failed: $($generatorOutput -join [Environment]::NewLine)"

    $generatedOpenPath = Join-Path $generatedDirectory 'dororong-canonical.png'
    $generatedClosedPath = Join-Path $generatedDirectory 'dororong-closed-eyes.png'
    $baselineOpenPath = Join-Path $baselineDirectory 'dororong-canonical.png'
    $baselineClosedPath = Join-Path $baselineDirectory 'dororong-closed-eyes.png'
    Assert-Equal (Get-FileHash -Algorithm SHA256 -LiteralPath $repositoryOpenPath).Hash `
        (Get-FileHash -Algorithm SHA256 -LiteralPath $generatedOpenPath).Hash 'The committed open frame is stale.'
    Assert-Equal (Get-FileHash -Algorithm SHA256 -LiteralPath $repositoryClosedPath).Hash `
        (Get-FileHash -Algorithm SHA256 -LiteralPath $generatedClosedPath).Hash 'The committed closed frame is stale.'

    $source = [System.Drawing.Bitmap]::new($sourcePath)
    $open = [System.Drawing.Bitmap]::new($generatedOpenPath)
    $closed = [System.Drawing.Bitmap]::new($generatedClosedPath)
    $baselineOpen = [System.Drawing.Bitmap]::new($baselineOpenPath)
    $baselineClosed = [System.Drawing.Bitmap]::new($baselineClosedPath)
    try
    {
        Assert-Equal 225 $source.Width 'The source width changed.'
        Assert-Equal 225 $source.Height 'The source height changed.'
        foreach ($frame in @($open, $closed, $baselineOpen, $baselineClosed))
        {
            Assert-Equal 96 $frame.Width 'A runtime frame is not native 96px wide.'
            Assert-Equal 96 $frame.Height 'A runtime frame is not native 96px high.'
            Assert-Equal ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb) $frame.PixelFormat 'A runtime frame is not 32bpp ARGB.'
        }

        $openCorrection = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $closedCorrection = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $visibleBodySupport = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $eyeChanges = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $bounds = @{ MinX = 96; MinY = 96; MaxX = -1; MaxY = -1 }
        for ($y = 0; $y -lt 96; $y++)
        {
            for ($x = 0; $x -lt 96; $x++)
            {
                $coordinate = "$x,$y"
                $openPixel = $open.GetPixel($x, $y)
                $closedPixel = $closed.GetPixel($x, $y)
                $openBasePixel = $baselineOpen.GetPixel($x, $y)
                $closedBasePixel = $baselineClosed.GetPixel($x, $y)
                Assert-Equal $openBasePixel.A $openPixel.A "Open alpha changed at ($x,$y)."
                Assert-Equal $closedBasePixel.A $closedPixel.A "Closed alpha changed at ($x,$y)."
                Assert-Equal $openPixel.A $closedPixel.A "Open/closed alpha differs at ($x,$y)."
                if ($openPixel.A -eq 0)
                {
                    Assert-Equal 0 ($openPixel.R + $openPixel.G + $openPixel.B) "Alpha-zero open RGB contamination exists at ($x,$y)."
                    Assert-Equal 0 ($closedPixel.R + $closedPixel.G + $closedPixel.B) "Alpha-zero closed RGB contamination exists at ($x,$y)."
                }
                if ($openPixel.ToArgb() -ne $openBasePixel.ToArgb())
                {
                    Assert-True (Test-InBodyProtectionBand $x $y) "Open RGB changed outside the body protection band at ($x,$y)."
                    [void]$openCorrection.Add($coordinate)
                    if ((Get-OpticalInk $openPixel 255) -ge 0.10) { [void]$visibleBodySupport.Add($coordinate) }
                    $bounds.MinX = [Math]::Min($bounds.MinX, $x); $bounds.MinY = [Math]::Min($bounds.MinY, $y)
                    $bounds.MaxX = [Math]::Max($bounds.MaxX, $x); $bounds.MaxY = [Math]::Max($bounds.MaxY, $y)
                }
                if ($closedPixel.ToArgb() -ne $closedBasePixel.ToArgb())
                {
                    Assert-True (Test-InBodyProtectionBand $x $y) "Closed RGB changed outside the body protection band at ($x,$y)."
                    [void]$closedCorrection.Add($coordinate)
                }
                if ($openPixel.ToArgb() -ne $closedPixel.ToArgb())
                {
                    Assert-True (($x -ge 16 -and $x -le 30 -and $y -ge 46 -and $y -le 61) -or
                        ($x -ge 34 -and $x -le 48 -and $y -ge 46 -and $y -le 63)) `
                        "Closed-eye change escaped the scaled eye regions at ($x,$y)."
                    [void]$eyeChanges.Add($coordinate)
                }
                Assert-True ($closedPixel.R -ne 250 -or $closedPixel.G -ne 220 -or $closedPixel.B -ne 224) `
                    "Forbidden #FADCE0 remains at ($x,$y)."
            }
        }
        Assert-True ($openCorrection.Count -ge 120) 'The full exposed body contour was not replaced.'
        Assert-Equal $openCorrection.Count $closedCorrection.Count 'Open/closed body correction mask sizes differ.'
        foreach ($coordinate in $openCorrection)
        {
            Assert-True $closedCorrection.Contains($coordinate) "Closed correction omitted $coordinate."
            $parts = $coordinate.Split(',')
            Assert-Equal $open.GetPixel([int]$parts[0], [int]$parts[1]).ToArgb() `
                $closed.GetPixel([int]$parts[0], [int]$parts[1]).ToArgb() "Body correction differs by eye state at $coordinate."
        }
        $remainingSupport = [Collections.Generic.HashSet[string]]::new($visibleBodySupport, [StringComparer]::Ordinal)
        $componentSizes = [Collections.Generic.List[int]]::new()
        while ($remainingSupport.Count -gt 0)
        {
            $seed = $remainingSupport | Select-Object -First 1
            $queue = [Collections.Generic.Queue[string]]::new()
            $queue.Enqueue($seed); [void]$remainingSupport.Remove($seed); $componentSize = 0
            while ($queue.Count -gt 0)
            {
                $parts = $queue.Dequeue().Split(','); $componentSize++
                foreach ($offset in @(@(-1,-1),@(0,-1),@(1,-1),@(-1,0),@(1,0),@(-1,1),@(0,1),@(1,1)))
                {
                    $neighbor = "$([int]$parts[0] + $offset[0]),$([int]$parts[1] + $offset[1])"
                    if ($remainingSupport.Remove($neighbor)) { $queue.Enqueue($neighbor) }
                }
            }
            $componentSizes.Add($componentSize)
        }
        Assert-Equal 1 $componentSizes.Count 'The native body contour is broken or contains an isolated dark component/knot.'
        Assert-True ($componentSizes[0] -ge 80) 'The continuous visible body contour has implausibly little support.'
        Assert-True ($eyeChanges.Count -ge 80) 'Both eyes did not visibly close.'

        # Literal native-96 lid/lower-eye probes preserve the reviewed eye semantics.
        foreach ($eye in @(
            @{ Name='left'; StartX=18; EndX=29; LidY=52; LowerProbes=@('20,55','22,56','25,55') },
            @{ Name='right'; StartX=35; EndX=47; LidY=52; LowerProbes=@('42,56','43,58','42,60') }
        ))
        {
            $lidInk = 0
            for ($x = $eye.StartX; $x -le $eye.EndX; $x++)
            {
                if ((Get-OpticalInk ($closed.GetPixel($x, $eye.LidY)) 255) -ge 0.30) { $lidInk++ }
            }
            Assert-True ($lidInk -ge 4) "$($eye.Name) lid is not visible."
            foreach ($probe in $eye.LowerProbes)
            {
                $parts = $probe.Split(',')
                $openInk = Get-OpticalInk ($open.GetPixel([int]$parts[0], [int]$parts[1])) 255
                $closedInk = Get-OpticalInk ($closed.GetPixel([int]$parts[0], [int]$parts[1])) 255
                Assert-True ($closedInk -le 0.30 -and ($openInk - $closedInk) -ge 0.12) `
                    "$($eye.Name) lower open-eye oval/underline survived at $probe (open=$openInk, closed=$closedInk)."
            }
        }
        for ($y = 61; $y -le 66; $y++)
        {
            for ($x = 24; $x -le 33; $x++)
            {
                Assert-Equal $open.GetPixel($x, $y).ToArgb() $closed.GetPixel($x, $y).ToArgb() "Mouth changed at ($x,$y)."
            }
        }

        # Frozen after fixed clean-hair native profiles: peak >= .50, optical width .60..2.10.
        $hairProfiles = @(
            @{ Name='hair-top'; Samples=@(@(32,21),@(32,22),@(32,23),@(32,24),@(32,25),@(32,26),@(32,27)); Joint=$false },
            @{ Name='hair-left'; Samples=@(@(13,30),@(14,30),@(15,30),@(16,30),@(17,30),@(18,30),@(19,30),@(20,30),@(21,30)); Joint=$false }
        )
        foreach ($definition in $hairProfiles)
        {
            $metric = Get-Profile $baselineOpen $definition 255
            Assert-True ($metric.Peak -ge 0.50 -and $metric.Width -ge 0.60 -and $metric.Width -le 2.10) `
                "Fixed hair reference $($metric.Name) contradicts frozen profile limits: peak=$($metric.Peak), width=$($metric.Width)."
        }
        $bodyProfiles = @(
            @{ Name='rear-rim-upper'; Samples=@(@(71,62),@(72,62),@(73,62),@(74,62),@(75,62),@(76,62),@(77,62)); Joint=$false },
            @{ Name='rear-rim-lower'; Samples=@(@(67,72),@(68,72),@(69,72),@(70,72),@(71,72),@(72,72),@(73,72)); Joint=$false },
            @{ Name='front-outer'; Samples=@(@(16,75),@(17,75),@(18,75),@(19,75),@(20,75),@(21,75),@(22,75)); Joint=$false },
            @{ Name='front-inner'; Samples=@(@(23,76),@(24,76),@(25,76),@(26,76),@(27,76),@(28,76),@(29,76)); Joint=$false },
            @{ Name='front-foot'; Samples=@(@(23,78),@(23,79),@(23,80),@(23,81),@(23,82),@(23,83)); Joint=$false },
            @{ Name='front-valley'; Samples=@(@(28,72),@(28,73),@(28,74),@(28,75),@(28,76),@(28,77),@(28,78)); Joint=$true },
            @{ Name='center-outer'; Samples=@(@(34,79),@(35,79),@(36,79),@(37,79),@(38,79),@(39,79)); Joint=$false },
            @{ Name='center-foot'; Samples=@(@(42,82),@(42,83),@(42,84),@(42,85),@(42,86),@(42,87)); Joint=$false },
            @{ Name='center-inner'; Samples=@(@(44,80),@(45,80),@(46,80),@(47,80),@(48,80),@(49,80),@(50,80)); Joint=$false },
            @{ Name='center-valley'; Samples=@(@(52,71),@(52,72),@(52,73),@(52,74),@(52,75),@(52,76),@(52,77)); Joint=$true },
            @{ Name='rear-outer'; Samples=@(@(59,79),@(60,79),@(61,79),@(62,79),@(63,79),@(64,79)); Joint=$false },
            @{ Name='rear-foot'; Samples=@(@(66,81),@(66,82),@(66,83),@(66,84),@(66,85),@(66,86)); Joint=$false },
            @{ Name='rear-inner'; Samples=@(@(67,78),@(68,78),@(69,78),@(70,78),@(71,78),@(72,78),@(73,78)); Joint=$false }
        )
        $profileEvidence = [Collections.Generic.List[string]]::new()
        $lightBodyWidths = [Collections.Generic.List[double]]::new()
        $lightJointWidths = [Collections.Generic.List[double]]::new()
        foreach ($background in @(255, 24))
        {
            foreach ($definition in $bodyProfiles)
            {
                $metric = Get-Profile $open $definition $background
                $profileEvidence.Add("$($metric.Name):$([Math]::Round($metric.Width,3))/$([Math]::Round($metric.Peak,3))/$($metric.Support)")
                if ($background -eq 255)
                {
                    $lightBodyWidths.Add($metric.Width)
                    if ($metric.Joint) { $lightJointWidths.Add($metric.Width) }
                }
                Assert-True ($metric.Peak -ge 0.50) "$($metric.Name) is faint/vanished on background ${background}: peak=$($metric.Peak)."
                Assert-True ($metric.Width -ge 0.60 -and $metric.Width -le 2.10) "$($metric.Name) has wrong optical width: $($metric.Width)."
                Assert-Equal 1 $metric.Runs "$($metric.Name) has broken/dotted support."
                if (-not $metric.Joint)
                {
                    Assert-True ($metric.Support -le 2) "$($metric.Name) has a non-joint 3px knot."
                }
                else
                {
                    Assert-True ($metric.Support -le 3 -and $metric.Width -le 2.10) "$($metric.Name) joint is materially too heavy."
                }
            }
        }
        $orderedBodyWidths = @($lightBodyWidths | Sort-Object)
        $medianBodyWidth = $orderedBodyWidths[[Math]::Floor($orderedBodyWidths.Count / 2)]
        Assert-True ($medianBodyWidth -ge 0.95 -and $medianBodyWidth -le 1.60) `
            "The body stroke rhythm is materially lighter/heavier than fixed clean hair: median width=$medianBodyWidth."
        foreach ($jointWidth in $lightJointWidths)
        {
            Assert-True ($jointWidth -le ($medianBodyWidth * 1.35)) `
                "A joint mass is materially heavier than the clean body median: joint=$jointWidth, median=$medianBodyWidth."
        }
        Assert-Equal 0 $open.GetPixel(0,0).A 'Transparent margin was lost.'
        Assert-True ($open.GetPixel(43,75).A -ge 240) 'Opaque body hit probe was lost.'
        Write-Output "NATIVE96 PIXEL EVIDENCE: correction=$($openCorrection.Count), bounds=$($bounds.MinX),$($bounds.MinY)..$($bounds.MaxX),$($bounds.MaxY); profiles=$($profileEvidence -join ';')."
    }
    finally
    {
        $source.Dispose(); $open.Dispose(); $closed.Dispose(); $baselineOpen.Dispose(); $baselineClosed.Dispose()
    }
}
finally
{
    if ([IO.Directory]::Exists($generatedDirectory)) { [IO.Directory]::Delete($generatedDirectory, $true) }
    if ([IO.Directory]::Exists($baselineDirectory)) { [IO.Directory]::Delete($baselineDirectory, $true) }
}

$coreAssemblyPath = Join-Path $repositoryRoot "src/Dororong.Core/bin/$Configuration/net8.0/Dororong.Core.dll"
$appAssemblyPath = Join-Path $repositoryRoot "src/Dororong.App/bin/$Configuration/net8.0-windows/Dororong.App.dll"
Add-Type -AssemblyName PresentationFramework
Add-Type -Path $coreAssemblyPath
Add-Type -Path $appAssemblyPath
$presenter = [Dororong.App.Controls.DororongPresenter]::new()
$bodyGroup = [System.Windows.Controls.Canvas]$presenter.FindName('BodyGroup')
$image = [System.Windows.Controls.Image]$presenter.FindName('DororongImage')
Assert-Equal 108.0 $bodyGroup.Width 'BodyGroup width changed.'
Assert-Equal 96.0 $bodyGroup.Height 'BodyGroup height changed.'
Assert-Equal 18.0 ([System.Windows.Controls.Canvas]::GetLeft($bodyGroup)) 'BodyGroup placement changed.'
Assert-Equal 24.0 ([System.Windows.Controls.Canvas]::GetTop($bodyGroup)) 'BodyGroup placement changed.'
Assert-Equal 96.0 $image.Width 'The presenter resamples the native bitmap horizontally.'
Assert-Equal 96.0 $image.Height 'The presenter resamples the native bitmap vertically.'
Assert-Equal 6.0 ([System.Windows.Controls.Canvas]::GetLeft($image)) 'The native bitmap is not centered in BodyGroup.'

$window = [System.Windows.Window]::new()
$window.Width = 144; $window.Height = 144; $window.Left = -10000; $window.Top = -10000
$window.ShowActivated = $false; $window.ShowInTaskbar = $false; $window.WindowStyle = [System.Windows.WindowStyle]::None
$window.Content = $presenter
try
{
    $window.Show(); $presenter.UpdateLayout()
    $dpi = [System.Windows.Media.VisualTreeHelper]::GetDpi($image)
    Assert-Near 1.0 $dpi.DpiScaleX 0.000001 'Presenter target is not 96 DPI.'
    Assert-Near 96.0 $image.ActualWidth 0.000001 'Bitmap is not arranged at 96 DIPs.'
    Assert-Near 96.0 $image.ActualHeight 0.000001 'Bitmap is not arranged at 96 DIPs.'
    Assert-Equal 96 ([System.Windows.Media.Imaging.BitmapSource]$image.Source).PixelWidth 'Presented resource is not 96 pixels.'
    $opaquePoint = $image.TranslatePoint([System.Windows.Point]::new(43.5,75.5), $presenter)
    $hit = $presenter.InputHitTest($opaquePoint)
    Assert-True ($null -ne $hit -and $bodyGroup.IsAncestorOf($hit)) 'Opaque native body point did not alpha-hit-test.'
    $marginPoint = $image.TranslatePoint([System.Windows.Point]::new(0.25,0.25), $presenter)
    Assert-True ($null -eq $presenter.InputHitTest($marginPoint)) 'Transparent native margin hit-tested as opaque.'
}
finally { $window.Close() }

$stateType = [Dororong.Core.Behavior.PetState]
$facing = [Dororong.Core.Behavior.FacingDirection]::Right
foreach ($state in @($stateType::Idle,$stateType::Walk,$stateType::Curious,$stateType::Startled,$stateType::ClickReaction,$stateType::Dragged))
{
    $presenter.Render([Dororong.Core.Behavior.PetSnapshot]::new($state,[Dororong.Core.Geometry.PointD]::new(0,0),$facing,0.1,$false,$null))
    Assert-Frame $image 'dororong-canonical.png' $state.ToString()
}
$presenter.Render([Dororong.Core.Behavior.PetSnapshot]::new($stateType::Sleep,[Dororong.Core.Geometry.PointD]::new(0,0),$facing,0.1,$false,$null))
Assert-Frame $image 'dororong-closed-eyes.png' 'Sleep'
$presenter.Render([Dororong.Core.Behavior.PetSnapshot]::new($stateType::Idle,[Dororong.Core.Geometry.PointD]::new(0,0),$facing,0.68,$false,$null))
Assert-Frame $image 'dororong-closed-eyes.png' 'Idle blink'

Write-Output 'EXACT ART PASS: exact 225px authority, deterministic native-96 frames, independent body protection, identical eye-state correction, alpha hygiene, literal hair/body optical profiles, eye semantics, 96-DPI one-to-one presenter, native alpha hit testing, and state mapping passed.'
