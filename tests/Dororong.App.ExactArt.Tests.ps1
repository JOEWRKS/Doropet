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

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical-source.png'
$productionPath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical.png'
$closedEyesPath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-closed-eyes.png'

Assert-True (Test-Path -LiteralPath $sourcePath) 'The persisted canonical source asset is missing.'
Assert-True (Test-Path -LiteralPath $productionPath) 'The transparent canonical production asset is missing.'
Assert-True (Test-Path -LiteralPath $closedEyesPath) 'The bounded closed-eye frame is missing.'
Assert-Equal `
    'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash `
    'The repository source is not the exact user-supplied canonical PNG.'

Add-Type -AssemblyName System.Drawing
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

    $changedClosedEyePixels = 0
    for ($y = 0; $y -lt $source.Height; $y++)
    {
        for ($x = 0; $x -lt $source.Width; $x++)
        {
            $sourcePixel = $source.GetPixel($x, $y)
            $productionPixel = $production.GetPixel($x, $y)
            $expectedAlpha = if ($boundaryBackground[$x, $y]) { 0 } else { 255 }

            Assert-Equal $expectedAlpha $productionPixel.A "Production alpha differs from the boundary-connected background mask at ($x,$y)."
            Assert-Equal $sourcePixel.R $productionPixel.R "Production red changed at retained coordinate ($x,$y)."
            Assert-Equal $sourcePixel.G $productionPixel.G "Production green changed at retained coordinate ($x,$y)."
            Assert-Equal $sourcePixel.B $productionPixel.B "Production blue changed at retained coordinate ($x,$y)."

            $closedPixel = $closedEyes.GetPixel($x, $y)
            if ($closedPixel.ToArgb() -ne $productionPixel.ToArgb())
            {
                $insideLeftEye = $x -ge 43 -and $x -le 72 -and $y -ge 105 -and $y -le 143
                $insideRightEye = $x -ge 86 -and $x -le 118 -and $y -ge 105 -and $y -le 143
                Assert-True ($insideLeftEye -or $insideRightEye) "The closed-eye frame changed a pixel outside the bounded eye regions at ($x,$y)."
                $changedClosedEyePixels++
            }
        }
    }

    Assert-True ($changedClosedEyePixels -gt 0) 'The closed-eye frame contains no derived closed-eye marks.'
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

Write-Output 'EXACT ART PASS: source identity/dimensions, edge alpha and retained pixels, bounded eyes, presenter state mapping, BodyGroup placement, and alpha-aware body hit testing passed.'
