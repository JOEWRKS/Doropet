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

function Get-RequiredType([Reflection.Assembly]$Assembly, [string]$Name) {
    $type = $Assembly.GetType($Name, $false)
    if ($null -eq $type) { throw "Required type '$Name' was not found." }
    return $type
}

function New-InternalInstance([Type]$Type, [object[]]$Arguments) {
    return [Activator]::CreateInstance(
        $Type,
        [Reflection.BindingFlags]'Instance,Public,NonPublic',
        $null,
        $Arguments,
        $null)
}

function Get-VisualDescendants([Windows.DependencyObject]$Root) {
    $results = [Collections.Generic.List[Windows.DependencyObject]]::new()
    $pending = [Collections.Generic.Queue[Windows.DependencyObject]]::new()
    $pending.Enqueue($Root)
    while ($pending.Count -gt 0) {
        $current = $pending.Dequeue()
        $childCount = [Windows.Media.VisualTreeHelper]::GetChildrenCount($current)
        for ($index = 0; $index -lt $childCount; $index++) {
            $child = [Windows.Media.VisualTreeHelper]::GetChild($current, $index)
            $results.Add($child)
            $pending.Enqueue($child)
        }
    }
    return $results
}

function Assert-ApprovedSource([Windows.Controls.Image]$Image, [string]$State) {
    $source = $Image.Source.ToString()
    $approved = $source.EndsWith('dororong-canonical.png', [StringComparison]::OrdinalIgnoreCase) -or
        $source.EndsWith('dororong-blink-squint.png', [StringComparison]::OrdinalIgnoreCase)
    Assert-True $approved "$State selected a non-approved body-click image: $source"
}

function Assert-SuppliedSource([Windows.Controls.Image]$Image, [string]$Asset, [int]$OffsetY, [int]$ForegroundCount) {
    $original = [Windows.Media.Imaging.BitmapImage]::new([Uri](Join-Path $assetRoot $Asset))
    $from = [Windows.Media.Imaging.FormatConvertedBitmap]::new($original, [Windows.Media.PixelFormats]::Pbgra32, $null, 0)
    $to = [Windows.Media.Imaging.FormatConvertedBitmap]::new($Image.Source, [Windows.Media.PixelFormats]::Pbgra32, $null, 0)
    Assert-Equal 96 $to.PixelWidth "Aligned $Asset changed width."
    Assert-Equal 96 $to.PixelHeight "Aligned $Asset changed height."
    $sourcePixels = [byte[]]::new(100 * 100 * 4)
    $actual = [byte[]]::new(96 * 96 * 4)
    $from.CopyPixels($sourcePixels, 400, 0)
    $to.CopyPixels($actual, 384, 0)
    $opaque = 0
    for ($index = 3; $index -lt $actual.Length; $index += 4) { if ($actual[$index] -ne 0) { $opaque++ } }
    Assert-Equal $ForegroundCount $opaque "Supplied $Asset foreground count changed."
    for ($y = 0; $y -lt 100; $y++) {
        for ($x = 0; $x -lt 100; $x++) {
            $s = ($y * 100 + $x) * 4
            if ([Math]::Min($sourcePixels[$s], [Math]::Min($sourcePixels[$s+1], $sourcePixels[$s+2])) -lt 240) {
                $targetY = $y + $OffsetY
                Assert-True ($targetY -ge 0 -and $targetY -lt 96 -and ($x + 3) -lt 96) "Supplied $Asset clipped source."
                $d = ($targetY * 96 + $x + 3) * 4
                $alpha = [int]$actual[$d+3]
                if ($alpha -lt 255) {
                    $boundary = $false
                    for ($dy = -1; $dy -le 1; $dy++) {
                        for ($dx = -1; $dx -le 1; $dx++) {
                            $nx = $x + 3 + $dx; $ny = $targetY + $dy
                            if ($nx -lt 0 -or $nx -ge 96 -or $ny -lt 0 -or $ny -ge 96 -or $actual[($ny*96+$nx)*4+3] -eq 0) { $boundary = $true }
                        }
                    }
                    Assert-True $boundary "Supplied $Asset changed an interior texel($x,$y)."
                    Assert-True ($alpha -gt 0) "Supplied $Asset erased an outline texel($x,$y)."
                    for ($channel = 0; $channel -lt 3; $channel++) {
                        Assert-True ($actual[$d+$channel] -le $alpha) "Invalid premultiplied edge in $Asset."
                        Assert-Equal $sourcePixels[$s+$channel] ($actual[$d+$channel]+255-$alpha) "Supplied $Asset changed white-composite texel($x,$y)."
                    }
                } else {
                    for ($channel = 0; $channel -lt 4; $channel++) {
                        Assert-Equal $sourcePixels[$s+$channel] $actual[$d+$channel] "Supplied $Asset changed interior texel($x,$y), channel$channel."
                    }
                }
            }
        }
    }
}

function New-DirectSnapshot([string]$Phase, [double]$Strength, [double]$ReleaseProgress) {
    return New-InternalInstance $directSnapshotType ([object[]]@(
        [Enum]::Parse($directTargetType, 'Body'),
        [Enum]::Parse($directPhaseType, $Phase),
        [Dororong.Core.Geometry.PointD]::new(48, 70),
        [Dororong.Core.Geometry.PointD]::new(64, 82),
        $Strength,
        $ReleaseProgress,
        ($Phase -ne 'BodyDragSettle')))
}

$apartmentState = [Threading.Thread]::CurrentThread.GetApartmentState().ToString()
Assert-Equal 'STA' $apartmentState 'The direct-interaction WPF render test must run in an STA apartment.'
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $repositoryRoot 'src/Dororong.App/Assets'
Assert-Equal '699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78' `
    (Get-FileHash -LiteralPath (Join-Path $assetRoot 'dororong-canonical.png') -Algorithm SHA256).Hash `
    'The accepted canonical source changed.'
Assert-Equal '2A733093AC35B9678CBB90833272498C7BE755F77A574271DAE64FCE80FC90E2' `
    (Get-FileHash -LiteralPath (Join-Path $assetRoot 'dororong-blink-squint.png') -Algorithm SHA256).Hash `
    'The accepted happy-squint source changed.'

Add-Type -Path (Join-Path $repositoryRoot "src/Dororong.Core/bin/$Configuration/net8.0/Dororong.Core.dll")
Add-Type -Path (Join-Path $repositoryRoot "src/Dororong.App/bin/$Configuration/net8.0-windows/Dororong.App.dll")
$appAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $repositoryRoot "src/Dororong.App/bin/$Configuration/net8.0-windows/Dororong.App.dll"))
$directSnapshotType = Get-RequiredType $appAssembly 'Dororong.App.Interaction.DirectInteractionSnapshot'
$directTargetType = Get-RequiredType $appAssembly 'Dororong.App.Interaction.DirectInteractionTarget'
$directPhaseType = Get-RequiredType $appAssembly 'Dororong.App.Interaction.DirectInteractionPhase'
$none = $directSnapshotType.GetProperty('None', [Reflection.BindingFlags]'Static,Public,NonPublic').GetValue($null)
$bodyPending = New-InternalInstance $directSnapshotType ([object[]]@(
    [Enum]::Parse($directTargetType, 'Body'),
    [Enum]::Parse($directPhaseType, 'BodyPending'),
    [Dororong.Core.Geometry.PointD]::new(48, 70),
    [Dororong.Core.Geometry.PointD]::new(48, 70),
    0.0,
    0.0,
    $true))
$origin = [Dororong.Core.Geometry.PointD]::new(640, 460)
$facing = [Dororong.Core.Behavior.FacingDirection]::Right
$state = [Dororong.Core.Behavior.PetState]
function New-Snapshot([Dororong.Core.Behavior.PetState]$State, [double]$Phase) {
    return [Dororong.Core.Behavior.PetSnapshot]::new($State, $origin, $facing, $Phase, $false, $null)
}

$presenter = [Dororong.App.Controls.DororongPresenter]::new()
$render = @($presenter.GetType().GetMethods([Reflection.BindingFlags]'Instance,NonPublic') |
    Where-Object { $_.Name -eq 'Render' -and $_.GetParameters().Count -eq 2 })[0]
$image = [Windows.Controls.Image]$presenter.FindName('DororongImage')
$bodyScale = $presenter.FindName('BodyScaleTransform')
$translation = $presenter.FindName('BodyTranslateTransform')
$imageScale = $presenter.FindName('ImageBreathingScaleTransform')
Assert-True ($null -ne $image) 'The one Dororong character surface was not found.'
$presenter.Measure([Windows.Size]::new(144, 144))
$presenter.Arrange([Windows.Rect]::new(0, 0, 144, 144))
$presenter.UpdateLayout()
$characterSurfaces = @(Get-VisualDescendants $presenter | Where-Object { $_.GetType().Name -eq 'AlphaHitTestImage' })
Assert-Equal 1 $characterSurfaces.Count 'The presenter must render exactly one character image surface.'

$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Idle 0.2), $bodyPending)) | Out-Null
Assert-True $image.Source.ToString().EndsWith('dororong-canonical.png', [StringComparison]::OrdinalIgnoreCase) 'Pending body press replaced the canonical source image.'
Assert-Near 1.012 ([double]$imageScale.ScaleX) 0.000001 'Pending body press lost its subtle horizontal compression response.'
Assert-Near 0.975 ([double]$imageScale.ScaleY) 0.000001 'Pending body press lost its subtle foot-anchored compression.'
Assert-Near 0.0 ([double]$translation.Y) 0.000001 'Pending body press moved the character or showed a pre-threshold hanging pose.'
Assert-Near 0.428987 ([double]$image.RenderTransformOrigin.X) 0.000001 'Pending body press changed the accepted foot anchor X.'
Assert-Near 0.916667 ([double]$image.RenderTransformOrigin.Y) 0.000001 'Pending body press changed the accepted foot anchor Y.'

$samples = [Collections.Generic.List[object]]::new()
foreach ($milliseconds in (0..31 | ForEach-Object { $_ * 16 }) + 500) {
    $phase = [Math]::Min($milliseconds / 500.0, 1.0)
    $render.Invoke($presenter, [object[]]@((New-Snapshot $state::ClickReaction $phase), $none)) | Out-Null
    Assert-ApprovedSource $image "CLICK_REACTION ${milliseconds}ms"
    Assert-Near 1.0 ([double]$image.Opacity) 0.000001 "CLICK_REACTION ${milliseconds}ms changed whole-character opacity."
    Assert-Near 0.0 ([double]$bodyScale.ScaleY - 1.0) 0.000001 "CLICK_REACTION ${milliseconds}ms used the old body/hanging scale channel."
    $samples.Add([pscustomobject]@{
        Milliseconds = $milliseconds
        ScaleX = [double]$imageScale.ScaleX
        ScaleY = [double]$imageScale.ScaleY
        TranslationY = [double]$translation.Y
        Source = $image.Source.ToString()
    })
}

$lift = @($samples | Where-Object { $_.Milliseconds -ge 80 -and $_.Milliseconds -le 224 })
for ($index = 1; $index -lt $lift.Count; $index++) {
    Assert-True ($lift[$index].TranslationY -lt $lift[$index - 1].TranslationY) "Lift stepped or reversed at $($lift[$index].Milliseconds)ms."
}
$apex = @($samples | Where-Object { $_.Milliseconds -ge 240 -and $_.Milliseconds -le 320 })
foreach ($sample in $apex) {
    Assert-Near -12.0 $sample.TranslationY 0.000001 "Apex at $($sample.Milliseconds)ms did not hold the exact airborne height."
}
$descent = @($samples | Where-Object { $_.Milliseconds -ge 336 -and $_.Milliseconds -le 416 })
for ($index = 1; $index -lt $descent.Count; $index++) {
    Assert-True ($descent[$index].TranslationY -gt $descent[$index - 1].TranslationY) "Descent stepped or reversed at $($descent[$index].Milliseconds)ms."
}
foreach ($pair in $samples[0..($samples.Count - 2)]) {
    $next = $samples[$samples.IndexOf($pair) + 1]
    Assert-True ([Math]::Abs($next.TranslationY - $pair.TranslationY) -le 3.6) "CLICK_REACTION Y jumped between $($pair.Milliseconds)ms and $($next.Milliseconds)ms."
}

$rest = $samples[-1]
Assert-Near 1.0 $rest.ScaleX 0.000001 'CLICK_REACTION completion did not restore exact ScaleX.'
Assert-Near 1.0 $rest.ScaleY 0.000001 'CLICK_REACTION completion did not restore exact ScaleY.'
Assert-Near 0.0 $rest.TranslationY 0.000001 'CLICK_REACTION completion did not restore exact TranslationY.'
Assert-True $rest.Source.EndsWith('dororong-canonical.png', [StringComparison]::OrdinalIgnoreCase) 'CLICK_REACTION completion did not restore the exact canonical source.'

$entryKeys = @(
    'user-body-drag/01.png', 'user-body-drag/02.png', 'user-body-drag/03.png', 'user-body-drag/04.png',
    'user-body-drag/05.png', 'user-body-drag/06.png', 'user-body-drag/07.png', 'user-body-drag/08.png')
$entryOffsets = @(-13, -9, -9, -10, -10, -10, -10, -10)
$entryForegroundCounts = @(3316, 3553, 3628, 3732, 3728, 3722, 3658, 3634)
for ($index = 0; $index -lt $entryKeys.Count; $index++) {
    $progress = $index / [double]($entryKeys.Count - 1)
    $direct = New-DirectSnapshot 'BodyDragEntry' $progress 0
    $render.Invoke($presenter, [object[]]@((New-Snapshot $state::Dragged 0), $direct)) | Out-Null
    Assert-SuppliedSource $image $entryKeys[$index] $entryOffsets[$index] $entryForegroundCounts[$index]
    Assert-Near 1.0 ([double]$image.Opacity) 0.000001 "Body drag entry key $index changed whole-character opacity."
    Assert-Near 1.0 ([double]$bodyScale.ScaleY) 0.000001 "Body drag entry key $index retained procedural body stretch."
    Assert-Near 0.0 ([double]$translation.Y) 0.000001 "Body drag entry key $index moved the whole-character surface away from its fixed anchor."
    Assert-Near 1.0 ([double]$imageScale.ScaleX) 0.000001 "Body drag entry key $index retained breathing or click scale X."
    Assert-Near 1.0 ([double]$imageScale.ScaleY) 0.000001 "Body drag entry key $index retained breathing or click scale Y."
}

$midEntry = New-DirectSnapshot 'BodyDragEntry' (1.0 / 14.0) 0
$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Dragged 0), $midEntry)) | Out-Null
Assert-Equal ([Windows.Media.PixelFormats]::Pbgra32) $image.Source.Format 'Body drag entry interpolation did not use one premultiplied Pbgra32 surface.'
Assert-Near 1.0 ([double]$image.Opacity) 0.000001 'Body drag entry interpolation changed whole-character opacity.'

$hold = New-DirectSnapshot 'BodyDragHold' 1 0
$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Dragged 0), $hold)) | Out-Null
Assert-SuppliedSource $image 'user-body-drag/08.png' -10 3634

$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Idle 0), $none)) | Out-Null
$facing = [Dororong.Core.Behavior.FacingDirection]::Left
$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Walk 0), $none)) | Out-Null
$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Walk 0), $bodyPending)) | Out-Null
$leftEntry = New-DirectSnapshot 'BodyDragEntry' 0 0
$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Dragged 0), $leftEntry)) | Out-Null
Assert-Near -1.0 ([double]$bodyScale.ScaleX) 0.000001 'Body drag did not preserve the visible left facing.'
$facing = [Dororong.Core.Behavior.FacingDirection]::Right

$settleKeys = @(
    'user-body-drag/08.png', 'user-body-drag/07.png', 'user-body-drag/06.png', 'user-body-drag/05.png',
    'user-body-drag/04.png', 'user-body-drag/03.png', 'user-body-drag/02.png', 'user-body-drag/01.png')
$settleOffsets = @(-10, -10, -10, -10, -10, -9, -9, -13)
$settleForegroundCounts = @(3634, 3658, 3722, 3728, 3732, 3628, 3553, 3316)
for ($index = 0; $index -lt $settleKeys.Count; $index++) {
    $progress = $index / [double]($settleKeys.Count - 1)
    $direct = New-DirectSnapshot 'BodyDragSettle' 1 $progress
    $render.Invoke($presenter, [object[]]@((New-Snapshot $state::Idle 0.2), $direct)) | Out-Null
    Assert-SuppliedSource $image $settleKeys[$index] $settleOffsets[$index] $settleForegroundCounts[$index]
    Assert-Near 1.0 ([double]$image.Opacity) 0.000001 "Body drag settle key $index changed whole-character opacity."
    Assert-Near 1.0 ([double]$imageScale.ScaleX) 0.000001 "Body drag settle key $index retained idle breathing scale X."
    Assert-Near 1.0 ([double]$imageScale.ScaleY) 0.000001 "Body drag settle key $index retained idle breathing scale Y."
}

$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Idle 0.2), $none)) | Out-Null
Assert-True $image.Source.ToString().EndsWith('dororong-canonical.png', [StringComparison]::OrdinalIgnoreCase) 'Body drag completion did not recover the canonical source.'
Assert-True ([double]$imageScale.ScaleX -gt 1.0) 'Body drag completion did not restore ordinary idle breathing.'

Write-Output "DIRECT INTERACTION RENDER PASS: $script:assertionCount assertions covered supplied8 interior/white-composite identity, exterior-only premultiplied alpha and silhouette counts, integer foot registration, one character surface, pending compression, continuous500ms hop,8-key entry, supplied8 hold, reverse8 settle, facing, and idle recovery."
