param([string]$Configuration = 'Debug')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:assertionCount = 0

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message) {
    $script:assertionCount++
    if ($Expected -ne $Actual) { throw "$Message Expected '$Expected', observed '$Actual'." }
}
function Assert-Near([double]$Expected, [double]$Actual, [double]$Tolerance, [string]$Message) {
    $script:assertionCount++
    if ([Math]::Abs($Expected - $Actual) -gt $Tolerance) { throw "$Message Expected '$Expected' +/- '$Tolerance', observed '$Actual'." }
}
function Assert-True([bool]$Condition, [string]$Message) {
    $script:assertionCount++
    if (-not $Condition) { throw $Message }
}
function Assert-Frame([System.Windows.Controls.Image]$Image, [string]$ExpectedFileName, [string]$State) {
    Assert-True $Image.Source.ToString().EndsWith($ExpectedFileName, [StringComparison]::OrdinalIgnoreCase) "$State did not use $ExpectedFileName."
}
function Assert-Origin([System.Windows.Controls.Image]$Image, [double]$X, [double]$Y, [string]$State) {
    Assert-Near $X ([double]$Image.RenderTransformOrigin.X) 0.0000001 "$State used the wrong horizontal breathing origin."
    Assert-Near $Y ([double]$Image.RenderTransformOrigin.Y) 0.0000001 "$State used the wrong vertical breathing origin."
}
function Assert-Png([string]$Path, [string]$ExpectedHash, [string]$Label) {
    Assert-True (Test-Path -LiteralPath $Path -PathType Leaf) "$Label is missing: $Path"
    Assert-Equal $ExpectedHash (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash "$Label hash changed."
    $bytes = [IO.File]::ReadAllBytes($Path)
    Assert-True ($bytes.Length -ge 29) "$Label is not a complete PNG."
    Assert-Equal 137 $bytes[0] "$Label PNG signature changed."
    Assert-Equal 80 $bytes[1] "$Label PNG signature changed."
    Assert-Equal 78 $bytes[2] "$Label PNG signature changed."
    Assert-Equal 71 $bytes[3] "$Label PNG signature changed."
    Assert-Equal 'IHDR' ([Text.Encoding]::ASCII.GetString($bytes, 12, 4)) "$Label lacks an IHDR chunk."
    $width = ([int]$bytes[16] * 16777216) + ([int]$bytes[17] * 65536) + ([int]$bytes[18] * 256) + [int]$bytes[19]
    $height = ([int]$bytes[20] * 16777216) + ([int]$bytes[21] * 65536) + ([int]$bytes[22] * 256) + [int]$bytes[23]
    Assert-Equal 96 $width "$Label width changed."
    Assert-Equal 96 $height "$Label height changed."
    Assert-Equal 8 $bytes[24] "$Label PNG bit depth is not native 8-bit RGBA."
    Assert-Equal 6 $bytes[25] "$Label PNG color type is not native RGBA."
    $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create([Uri]::new($Path), [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat, [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    $converted = [System.Windows.Media.Imaging.FormatConvertedBitmap]::new($decoder.Frames[0], [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $pixels = [byte[]]::new(96 * 96 * 4)
    $converted.CopyPixels($pixels, 96 * 4, 0)
    $hasTransparentPixel = $false
    for ($index = 3; $index -lt $pixels.Length; $index += 4) { if ($pixels[$index] -eq 0) { $hasTransparentPixel = $true; break } }
    Assert-True $hasTransparentPixel "$Label lost transparent pixels."
}

$apartmentState = [Threading.Thread]::CurrentThread.GetApartmentState().ToString()
Assert-Equal 'STA' $apartmentState 'The focused WPF presenter test must run in an STA apartment.'
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $repositoryRoot 'src/Dororong.App/Assets'
$sourceRoot = Join-Path $assetRoot 'frame-sources'
$assetCases = @(
    @{ File = 'dororong-sleep.png'; Hash = '7173DCA4E04B1A890A2E64DB4C129FB909EB9E820838072AEB14428FD2A50368' },
    @{ File = 'dororong-sleep-crouch-closed.png'; Hash = '97D90DC5266BCB9C34E437A3E3ABA7415FFE59F205D9A27EEC568079BB5A4813' },
    @{ File = 'dororong-sleep-crouch-squint.png'; Hash = '75CD59819BE617ECDD38FBA2B0FC630BB497CE5BAA7B538B4D76A863C0D37C1A' },
    @{ File = 'dororong-sleep-tuck-closed.png'; Hash = '4ACB72875E6B88641772C6479207459405837FD7DC41455075D34D6EB6B5BD8C' },
    @{ File = 'dororong-sleep-tuck-squint.png'; Hash = '35F906DB46235FC8FEB0A946EE8793534B4129793DBF478427C5951BB61EAB66' }
)
[xml]$project = Get-Content -Raw (Join-Path $repositoryRoot 'src/Dororong.App/Dororong.App.csproj')
$resourceIncludes = @($project.SelectNodes('//Resource') | ForEach-Object { $_.Include })
foreach ($assetCase in $assetCases) {
    $runtimePath = Join-Path $assetRoot $assetCase.File
    $durablePath = Join-Path $sourceRoot $assetCase.File
    Assert-Png $runtimePath $assetCase.Hash "runtime $($assetCase.File)"
    Assert-Png $durablePath $assetCase.Hash "durable $($assetCase.File)"
    Assert-True ([System.Collections.StructuralComparisons]::StructuralEqualityComparer.Equals([IO.File]::ReadAllBytes($runtimePath), [IO.File]::ReadAllBytes($durablePath))) "$($assetCase.File) runtime and durable bytes differ."
    Assert-True ($resourceIncludes -contains "Assets\$($assetCase.File)") "$($assetCase.File) is not declared as a WPF Resource."
}

Add-Type -Path (Join-Path $repositoryRoot "src/Dororong.Core/bin/$Configuration/net8.0/Dororong.Core.dll")
Add-Type -Path (Join-Path $repositoryRoot "src/Dororong.App/bin/$Configuration/net8.0-windows/Dororong.App.dll")
$state = [Dororong.Core.Behavior.PetState]
$facing = [Dororong.Core.Behavior.FacingDirection]::Right
$origin = [Dororong.Core.Geometry.PointD]::new(0, 0)
function New-Snapshot([Dororong.Core.Behavior.PetState]$State, [double]$Phase, [Nullable[Dororong.Core.Geometry.PointD]]$GrabOffset = $null) {
    return [Dororong.Core.Behavior.PetSnapshot]::new($State, $origin, $facing, $Phase, $false, $GrabOffset)
}
function New-PresenterFixture() {
    $presenter = [Dororong.App.Controls.DororongPresenter]::new()
    return [pscustomobject]@{ Presenter = $presenter; Image = [System.Windows.Controls.Image]$presenter.FindName('DororongImage'); BodyScale = $presenter.FindName('BodyScaleTransform'); Rotation = $presenter.FindName('BodyRotateTransform'); Translation = $presenter.FindName('BodyTranslateTransform'); Breathing = $presenter.FindName('ImageBreathingScaleTransform') }
}

$fixture = New-PresenterFixture
Assert-True ($null -ne $fixture.Image) 'DororongImage was not found in the presenter namescope.'
Assert-True ([Object]::ReferenceEquals($fixture.Image.RenderTransform, $fixture.Breathing)) 'The dedicated breathing scale was not applied directly to the authored image.'
$entryCases = @(
    @{ Phase = 0.0; Frame = 'dororong-blink-squint.png'; Label = '0ms standing squint' },
    @{ Phase = 0.0225; Frame = 'dororong-closed-eyes.png'; Label = '90ms standing closed' },
    @{ Phase = 0.045; Frame = 'dororong-sleep-crouch-closed.png'; Label = '180ms crouch closed' },
    @{ Phase = 0.070; Frame = 'dororong-sleep-tuck-closed.png'; Label = '280ms tuck closed' },
    @{ Phase = 0.100; Frame = 'dororong-sleep.png'; Label = '400ms settled loaf' }
)
foreach ($entryCase in $entryCases) {
    $fixture.Presenter.Render((New-Snapshot $state::Sleep $entryCase.Phase))
    Assert-Frame $fixture.Image $entryCase.Frame "SLEEP entry $($entryCase.Label)"
    Assert-Near 1.0 ([double]$fixture.Breathing.ScaleX) 0.000001 "SLEEP entry $($entryCase.Label) began breathing early."
    Assert-Near 1.0 ([double]$fixture.Breathing.ScaleY) 0.000001 "SLEEP entry $($entryCase.Label) began breathing early."
    Assert-Origin $fixture.Image 0.428987 0.916667 "SLEEP entry $($entryCase.Label)"
}
$fixture.Presenter.Render((New-Snapshot $state::Sleep 0.135))
Assert-Frame $fixture.Image 'dororong-sleep.png' 'SLEEP settlement at 540ms'
Assert-Origin $fixture.Image 0.435630 0.854167 'SLEEP settlement at 540ms'
Assert-Near 1.0 ([double]$fixture.Breathing.ScaleX) 0.000001 'SLEEP breathing did not begin at rest after settlement.'
Assert-Near 1.0 ([double]$fixture.Breathing.ScaleY) 0.000001 'SLEEP breathing did not begin at rest after settlement.'
$fixture.Presenter.Render((New-Snapshot $state::Sleep 0.515))
Assert-Near 1.024 ([double]$fixture.Breathing.ScaleX) 0.000001 'Settled SLEEP breathing lost its horizontal peak.'
Assert-Near 1.012 ([double]$fixture.Breathing.ScaleY) 0.000001 'Settled SLEEP breathing lost its vertical peak.'
$fixture.Presenter.Render((New-Snapshot $state::Sleep 0.001))
Assert-Frame $fixture.Image 'dororong-sleep.png' 'SLEEP phase wrap after settlement'
Assert-Origin $fixture.Image 0.435630 0.854167 'SLEEP phase wrap after settlement'

$partialEntry = New-PresenterFixture
$partialEntry.Presenter.Render((New-Snapshot $state::Sleep 0.045))
Assert-Frame $partialEntry.Image 'dororong-sleep-crouch-closed.png' 'Partial SLEEP entry'
$partialEntry.Presenter.Render((New-Snapshot $state::Dragged 0.25 ([Dororong.Core.Geometry.PointD]::new(76, 48))))
Assert-Frame $partialEntry.Image 'dororong-canonical.png' 'Dragged SLEEP cancellation'
Assert-Near 1.12 ([double]$partialEntry.BodyScale.ScaleY) 0.000001 'Dragged SLEEP cancellation did not preserve the dragged pose.'
Assert-Origin $partialEntry.Image 0.428987 0.916667 'Dragged SLEEP cancellation'

$wakeCases = @(
    @{ State = $state::Curious; FirstLimit = 0.05625; SecondLimit = 0.1125; Transform = 'rotation' },
    @{ State = $state::Startled; FirstLimit = 0.06; SecondLimit = 0.12; Transform = 'startled scale' },
    @{ State = $state::ClickReaction; FirstLimit = 0.09; SecondLimit = 0.18; Transform = 'click bounce' }
)
foreach ($wakeCase in $wakeCases) {
    $wake = New-PresenterFixture
    $wake.Presenter.Render((New-Snapshot $state::Sleep 0.135))
    $wake.Presenter.Render((New-Snapshot $wakeCase.State 0.0))
    Assert-Frame $wake.Image 'dororong-sleep-tuck-squint.png' "$($wakeCase.State) wake bridge first segment"
    switch ($wakeCase.Transform) {
        'rotation' { Assert-Near 7.0 ([double]$wake.Rotation.Angle) 0.000001 'Curious rotation stopped during the wake bridge.' }
        'startled scale' { $wake.Presenter.Render((New-Snapshot $wakeCase.State 0.02)); Assert-True (([double]$wake.BodyScale.ScaleX) -gt 1.0) 'Startled scale stopped during the wake bridge.' }
        'click bounce' { $wake.Presenter.Render((New-Snapshot $wakeCase.State 0.04)); Assert-True (([double]$wake.Translation.Y) -lt 0.0) 'Click bounce stopped during the wake bridge.' }
    }
    $wake.Presenter.Render((New-Snapshot $wakeCase.State $wakeCase.FirstLimit))
    Assert-Frame $wake.Image 'dororong-sleep-crouch-squint.png' "$($wakeCase.State) wake bridge second segment"
    $wake.Presenter.Render((New-Snapshot $wakeCase.State $wakeCase.SecondLimit))
    Assert-Frame $wake.Image 'dororong-canonical.png' "$($wakeCase.State) wake bridge completion"
}
$oneShot = New-PresenterFixture
$oneShot.Presenter.Render((New-Snapshot $state::Sleep 0.135))
$oneShot.Presenter.Render((New-Snapshot $state::Curious 0.0))
Assert-Frame $oneShot.Image 'dororong-sleep-tuck-squint.png' 'Curious wake bridge one-shot precondition'
$oneShot.Presenter.Render((New-Snapshot $state::Curious 0.1125))
$oneShot.Presenter.Render((New-Snapshot $state::Curious 0.01))
Assert-Frame $oneShot.Image 'dororong-canonical.png' 'Curious wake bridge replay prevention'
$oneShot.Presenter.Render((New-Snapshot $state::Sleep 0.0))
Assert-Frame $oneShot.Image 'dororong-blink-squint.png' 'Fresh SLEEP entry after a wake'

$interruptedWake = New-PresenterFixture
$interruptedWake.Presenter.Render((New-Snapshot $state::Sleep 0.135))
$interruptedWake.Presenter.Render((New-Snapshot $state::Curious 0.0))
Assert-Frame $interruptedWake.Image 'dororong-sleep-tuck-squint.png' 'Interrupted Curious wake bridge precondition'
$interruptedWake.Presenter.Render((New-Snapshot $state::Idle 0.2))
$interruptedWake.Presenter.Render((New-Snapshot $state::Curious 0.0))
Assert-Frame $interruptedWake.Image 'dororong-canonical.png' 'Interrupted Curious wake bridge cancellation'

$idle = New-PresenterFixture
foreach ($idleCase in @(@{ Phase = 0.65; Frame = 'dororong-blink-squint.png' }, @{ Phase = 0.69; Frame = 'dororong-closed-eyes.png' }, @{ Phase = 0.73; Frame = 'dororong-blink-squint.png' }, @{ Phase = 0.77; Frame = 'dororong-canonical.png' })) {
    $idle.Presenter.Render((New-Snapshot $state::Idle $idleCase.Phase))
    Assert-Frame $idle.Image $idleCase.Frame "IDLE blink threshold $($idleCase.Phase)"
}
Write-Output "SLEEP POSE PASS: $script:assertionCount assertions covered approved native assets, entry latch, settled breathing, cancellation, one-shot wake bridges, and unchanged IDLE thresholds."
