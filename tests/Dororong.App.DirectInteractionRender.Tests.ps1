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
    'body-drag-entry-00-press.png',
    'body-drag-entry-01-lengthen.png',
    'body-drag-entry-02-drop.png',
    'body-drag-entry-03-stretch.png',
    'body-drag-entry-04-dangle.png',
    'body-drag-entry-05-near-hang.png',
    'body-drag-entry-06-hang.png')
for ($index = 0; $index -lt $entryKeys.Count; $index++) {
    $progress = $index / [double]($entryKeys.Count - 1)
    $direct = New-DirectSnapshot 'BodyDragEntry' $progress 0
    $render.Invoke($presenter, [object[]]@((New-Snapshot $state::Dragged 0), $direct)) | Out-Null
    Assert-True $image.Source.ToString().EndsWith($entryKeys[$index], [StringComparison]::OrdinalIgnoreCase) "Body drag entry progress $progress did not select $($entryKeys[$index])."
    Assert-Near 1.0 ([double]$image.Opacity) 0.000001 "Body drag entry key $index changed whole-character opacity."
    Assert-Near 1.0 ([double]$bodyScale.ScaleY) 0.000001 "Body drag entry key $index retained the old procedural stretch."
    Assert-Near 0.0 ([double]$translation.Y) 0.000001 "Body drag entry key $index moved the complete-character surface away from its fixed anchor."
    Assert-Near 1.0 ([double]$imageScale.ScaleX) 0.000001 "Body drag entry key $index retained breathing or click scale X."
    Assert-Near 1.0 ([double]$imageScale.ScaleY) 0.000001 "Body drag entry key $index retained breathing or click scale Y."
}

$midEntry = New-DirectSnapshot 'BodyDragEntry' (1.0 / 12.0) 0
$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Dragged 0), $midEntry)) | Out-Null
Assert-Equal ([Windows.Media.PixelFormats]::Pbgra32) $image.Source.Format 'Body drag entry interpolation did not use one premultiplied Pbgra32 surface.'
Assert-Near 1.0 ([double]$image.Opacity) 0.000001 'Body drag entry interpolation changed whole-character opacity.'

$hold = New-DirectSnapshot 'BodyDragHold' 1 0
$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Dragged 0), $hold)) | Out-Null
Assert-True $image.Source.ToString().EndsWith('body-drag-entry-06-hang.png', [StringComparison]::OrdinalIgnoreCase) 'Body drag hold did not retain the exact full-hang key.'

$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Idle 0), $none)) | Out-Null
$facing = [Dororong.Core.Behavior.FacingDirection]::Left
$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Walk 0), $none)) | Out-Null
$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Walk 0), $bodyPending)) | Out-Null
$leftEntry = New-DirectSnapshot 'BodyDragEntry' 0 0
$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Dragged 0), $leftEntry)) | Out-Null
Assert-Near -1.0 ([double]$bodyScale.ScaleX) 0.000001 'Body drag did not preserve the visible left facing.'
$facing = [Dororong.Core.Behavior.FacingDirection]::Right

$settleKeys = @(
    'body-drag-settle-00-hang.png',
    'body-drag-settle-01-lift.png',
    'body-drag-settle-02-gather.png',
    'body-drag-settle-03-land.png',
    'body-drag-settle-04-recover.png')
for ($index = 0; $index -lt $settleKeys.Count; $index++) {
    $progress = $index / [double]($settleKeys.Count - 1)
    $direct = New-DirectSnapshot 'BodyDragSettle' 1 $progress
    $render.Invoke($presenter, [object[]]@((New-Snapshot $state::Idle 0.2), $direct)) | Out-Null
    Assert-True $image.Source.ToString().EndsWith($settleKeys[$index], [StringComparison]::OrdinalIgnoreCase) "Body drag settle progress $progress did not select $($settleKeys[$index])."
    Assert-Near 1.0 ([double]$image.Opacity) 0.000001 "Body drag settle key $index changed whole-character opacity."
    Assert-Near 1.0 ([double]$imageScale.ScaleX) 0.000001 "Body drag settle key $index retained idle breathing scale X."
    Assert-Near 1.0 ([double]$imageScale.ScaleY) 0.000001 "Body drag settle key $index retained idle breathing scale Y."
}

$render.Invoke($presenter, [object[]]@((New-Snapshot $state::Idle 0.2), $none)) | Out-Null
Assert-True $image.Source.ToString().EndsWith('dororong-canonical.png', [StringComparison]::OrdinalIgnoreCase) 'Body drag completion did not recover the canonical source.'
Assert-True ([double]$imageScale.ScaleX -gt 1.0) 'Body drag completion did not restore ordinary idle breathing.'

Write-Output "DIRECT INTERACTION RENDER PASS: $script:assertionCount assertions covered exact accepted sources, one opaque surface, pending compression, continuous 500ms hop, seven-key drag entry, full-hang hold, five-key settle, facing, and exact recovery."
