param([string]$Configuration = 'Debug')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected,[object]$Actual,[string]$Message)
{ if ($Expected -ne $Actual) { throw "$Message Expected '$Expected', observed '$Actual'." } }

function Assert-True([bool]$Condition,[string]$Message)
{ if (-not $Condition) { throw $Message } }

function Assert-Near([double]$Expected,[double]$Actual,[double]$Tolerance,[string]$Message)
{ if ([Math]::Abs($Expected-$Actual)-gt$Tolerance) { throw "$Message Expected '$Expected' +/- '$Tolerance', observed '$Actual'." } }

function Assert-BitmapEqual([Drawing.Bitmap]$Expected,[Drawing.Bitmap]$Actual,[string]$Message)
{
    Assert-Equal $Expected.Width $Actual.Width "$Message Width differs."
    Assert-Equal $Expected.Height $Actual.Height "$Message Height differs."
    foreach($y in 0..($Expected.Height-1)){foreach($x in 0..($Expected.Width-1))
    {Assert-Equal $Expected.GetPixel($x,$y).ToArgb() $Actual.GetPixel($x,$y).ToArgb() "$Message Pixel differs at ($x,$y)."}}
}

function Assert-Nearest([Drawing.Bitmap]$Native,[Drawing.Bitmap]$Enlarged,[string]$Label)
{
    Assert-Equal ($Native.Width*8) $Enlarged.Width "$Label enlarged width changed."
    Assert-Equal ($Native.Height*8) $Enlarged.Height "$Label enlarged height changed."
    foreach($y in 0..($Native.Height-1)){foreach($x in 0..($Native.Width-1))
    {
        $expected=$Native.GetPixel($x,$y).ToArgb()
        Assert-Equal $expected $Enlarged.GetPixel($x*8,$y*8).ToArgb() "$Label top-left nearest block changed at ($x,$y)."
        Assert-Equal $expected $Enlarged.GetPixel(($x*8)+7,($y*8)+7).ToArgb() "$Label bottom-right nearest block changed at ($x,$y)."
    }}
}

function Assert-Frame([Windows.Controls.Image]$Image,[string]$ExpectedFileName,[string]$Label)
{
    Assert-True $Image.Source.ToString().EndsWith($ExpectedFileName,[StringComparison]::OrdinalIgnoreCase) `
        "$Label did not use $ExpectedFileName."
}

$repositoryRoot=Split-Path -Parent $PSScriptRoot
$assetRoot=Join-Path $repositoryRoot 'src/Dororong.App/Assets'
$paths=[ordered]@{
    Source=Join-Path $assetRoot 'dororong-canonical-source.png'
    Mask=Join-Path $assetRoot 'dororong-body-region-mask.png'
    Open=Join-Path $assetRoot 'dororong-canonical.png'
    Squint=Join-Path $assetRoot 'dororong-blink-squint.png'
    Closed=Join-Path $assetRoot 'dororong-closed-eyes.png'
    Generator=Join-Path $repositoryRoot 'tools/Generate-CanonicalArt.ps1'
}
$expected=[ordered]@{
    Source='F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504'
    Mask='D08B3A941C662F1CBC55C486C13FD4C6CD8901DA9CD5CF8512509698219FE46F'
    Open='699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78'
    Squint='2A733093AC35B9678CBB90833272498C7BE755F77A574271DAE64FCE80FC90E2'
    Closed='0D20EED5873A7E4277D6ED539474D875C9B8DF79998B1EFD74F46AA87662F488'
    SourceOpen='D1F0770CBCA95FC79B5E68642D78A5A48077834495C9ECDBCD73B34545AC94FF'
    SourceBaseline='8F542A4F1B2671789BD7CD4980D890BF96CFCC9DADBC9D4E61963CCE2D384CCB'
    NativeBaseline='3B3D171D2C62134284915D7D162D43F36263761D4EA6344F4AC8BCEA730C5B59'
}
foreach($name in @('Source','Mask','Open','Squint','Closed'))
{Assert-Equal $expected[$name] (Get-FileHash -Algorithm SHA256 -LiteralPath $paths[$name]).Hash "Pinned $name identity changed."}
Assert-True (-not(Test-Path -LiteralPath (Join-Path $assetRoot 'dororong-half-closed-eyes.png'))) `
    'The obsolete half-closed runtime asset returned.'
Assert-True (-not(Test-Path -LiteralPath (Join-Path $assetRoot 'dororong-eyes-70-open.png'))) `
    'The obsolete 70-percent iris-bearing runtime asset returned.'
Assert-True (-not(Test-Path -LiteralPath (Join-Path $assetRoot 'dororong-eyes-25-open.png'))) `
    'The obsolete 25-percent iris-bearing runtime asset returned.'

$runRoot=Join-Path $repositoryRoot ".superpowers/sdd/2026-08-29-dororong-stage-a-eye-geometry-blink-recovery/task-20-exact-art/$([Guid]::NewGuid().ToString('N'))"
$outputDirectory=Join-Path $runRoot 'output'
$evidenceDirectory=Join-Path $runRoot 'evidence'
$generatorOutput=& pwsh -NoProfile -File $paths.Generator -SourcePath $paths.Source `
    -BodyMaskPath $paths.Mask -OutputDirectory $outputDirectory -EvidenceDirectory $evidenceDirectory 2>&1
Assert-Equal 0 $LASTEXITCODE "Canonical/authored-state export failed: $($generatorOutput-join[Environment]::NewLine)"
$generatorText=$generatorOutput-join[Environment]::NewLine
Assert-True $generatorText.Contains('authoredFrames=3',[StringComparison]::Ordinal) `
    'Generator did not report the three supplied authored frames.'

$expectedOutput=@('dororong-blink-squint.png','dororong-canonical.png','dororong-closed-eyes.png')|Sort-Object
$actualOutput=@(Get-ChildItem -LiteralPath $outputDirectory -File|Sort-Object Name|ForEach-Object Name)
Assert-Equal ($expectedOutput-join'|') ($actualOutput-join'|') 'Export output surface changed.'
$expectedEvidence=@(
    'native-closed-candidate.png','native-closed-nearest-8x.png',
    'native-squint-candidate.png','native-squint-nearest-8x.png',
    'native-open-baseline.png','native-open-candidate.png','native-open-nearest-8x.png',
    'source-open-baseline.png','source-open-candidate.png')|Sort-Object
$actualEvidence=@(Get-ChildItem -LiteralPath $evidenceDirectory -File|Sort-Object Name|ForEach-Object Name)
Assert-Equal ($expectedEvidence-join'|') ($actualEvidence-join'|') `
    'Evidence surface contains procedural expression-source remnants or misses authored-native evidence.'

foreach($mapping in @(
    @{Product=$paths.Open;Export=Join-Path $outputDirectory 'dororong-canonical.png';Evidence=Join-Path $evidenceDirectory 'native-open-candidate.png'},
    @{Product=$paths.Squint;Export=Join-Path $outputDirectory 'dororong-blink-squint.png';Evidence=Join-Path $evidenceDirectory 'native-squint-candidate.png'},
    @{Product=$paths.Closed;Export=Join-Path $outputDirectory 'dororong-closed-eyes.png';Evidence=Join-Path $evidenceDirectory 'native-closed-candidate.png'}))
{
    $hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $mapping.Product).Hash
    Assert-Equal $hash (Get-FileHash -Algorithm SHA256 -LiteralPath $mapping.Export).Hash 'Exported runtime identity changed.'
    Assert-Equal $hash (Get-FileHash -Algorithm SHA256 -LiteralPath $mapping.Evidence).Hash 'Evidence runtime identity changed.'
}
Assert-Equal $expected.SourceOpen (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectory 'source-open-candidate.png')).Hash `
    'Generated source-open identity changed.'
Assert-Equal $expected.SourceBaseline (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectory 'source-open-baseline.png')).Hash `
    'Source-open baseline identity changed.'
Assert-Equal $expected.NativeBaseline (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectory 'native-open-baseline.png')).Hash `
    'Native-open baseline identity changed.'

Add-Type -AssemblyName System.Drawing
$bitmaps=[Collections.Generic.List[Drawing.Bitmap]]::new()
try
{
    foreach($entry in @(
        @{Native='native-open-candidate.png';Nearest='native-open-nearest-8x.png';Label='open'},
        @{Native='native-squint-candidate.png';Nearest='native-squint-nearest-8x.png';Label='squint'},
        @{Native='native-closed-candidate.png';Nearest='native-closed-nearest-8x.png';Label='closed'}))
    {
        $native=[Drawing.Bitmap]::new((Join-Path $evidenceDirectory $entry.Native));$bitmaps.Add($native)
        $nearest=[Drawing.Bitmap]::new((Join-Path $evidenceDirectory $entry.Nearest));$bitmaps.Add($nearest)
        Assert-Equal 96 $native.Width "$($entry.Label) native width changed."
        Assert-Equal 96 $native.Height "$($entry.Label) native height changed."
        Assert-Nearest $native $nearest $entry.Label
    }
}
finally{foreach($bitmap in $bitmaps){$bitmap.Dispose()}}

$currentThread=[Threading.Thread]::CurrentThread
Assert-Equal 'STA' $currentThread.GetApartmentState().ToString() 'Exact WPF art test must run in STA.'
$coreAssemblyPath=Join-Path $repositoryRoot "src/Dororong.Core/bin/$Configuration/net8.0/Dororong.Core.dll"
$appAssemblyPath=Join-Path $repositoryRoot "src/Dororong.App/bin/$Configuration/net8.0-windows/Dororong.App.dll"
Add-Type -AssemblyName PresentationFramework
Add-Type -Path $coreAssemblyPath
Add-Type -Path $appAssemblyPath
$presenter=[Dororong.App.Controls.DororongPresenter]::new()
$bodyGroup=[Windows.Controls.Canvas]$presenter.FindName('BodyGroup')
$image=[Windows.Controls.Image]$presenter.FindName('DororongImage')
$scale=$presenter.FindName('BodyScaleTransform')
Assert-Equal 108.0 $bodyGroup.Width 'BodyGroup width changed.'
Assert-Equal 96.0 $bodyGroup.Height 'BodyGroup height changed.'
Assert-Equal 18.0 ([Windows.Controls.Canvas]::GetLeft($bodyGroup)) 'BodyGroup placement changed.'
Assert-Equal 24.0 ([Windows.Controls.Canvas]::GetTop($bodyGroup)) 'BodyGroup placement changed.'
Assert-Equal 96.0 $image.Width 'Presenter resamples native bitmap horizontally.'
Assert-Equal 96.0 $image.Height 'Presenter resamples native bitmap vertically.'
Assert-Equal 6.0 ([Windows.Controls.Canvas]::GetLeft($image)) 'Native bitmap is not centered in BodyGroup.'

$window=[Windows.Window]::new();$window.Width=144;$window.Height=144;$window.Left=-10000;$window.Top=-10000
$window.ShowActivated=$false;$window.ShowInTaskbar=$false;$window.WindowStyle=[Windows.WindowStyle]::None;$window.Content=$presenter
try
{
    $window.Show();$presenter.UpdateLayout();$dpi=[Windows.Media.VisualTreeHelper]::GetDpi($image)
    Assert-Near 1.0 $dpi.DpiScaleX 0.000001 'Presenter target is not 96 DPI.'
    Assert-Near 96.0 $image.ActualWidth 0.000001 'Bitmap is not arranged at 96 DIPs.'
    Assert-Near 96.0 $image.ActualHeight 0.000001 'Bitmap is not arranged at 96 DIPs.'
    Assert-Equal 96 ([Windows.Media.Imaging.BitmapSource]$image.Source).PixelWidth 'Presented resource is not 96 pixels.'
    $opaquePoint=$image.TranslatePoint([Windows.Point]::new(43.5,75.5),$presenter)
    $hit=$presenter.InputHitTest($opaquePoint)
    Assert-True ($null-ne$hit-and$bodyGroup.IsAncestorOf($hit)) 'Opaque native body point did not alpha-hit-test.'
    $marginPoint=$image.TranslatePoint([Windows.Point]::new(0.25,0.25),$presenter)
    Assert-True ($null-eq$presenter.InputHitTest($marginPoint)) 'Transparent native margin hit-tested as opaque.'
}
finally{$window.Close()}

$state=[Dororong.Core.Behavior.PetState];$facing=[Dororong.Core.Behavior.FacingDirection]::Right
function Render-State([Dororong.Core.Behavior.PetState]$State,[double]$Phase)
{
    $presenter.Render([Dororong.Core.Behavior.PetSnapshot]::new(
        $State,[Dororong.Core.Geometry.PointD]::new(0,0),$facing,$Phase,$false,$null))
}
foreach($case in @(
    @{State=$state::Idle;Phase=0.1;Frame='dororong-canonical.png';Label='Idle open'},
    @{State=$state::Idle;Phase=0.66;Frame='dororong-blink-squint.png';Label='Idle closing squint'},
    @{State=$state::Idle;Phase=0.70;Frame='dororong-closed-eyes.png';Label='Idle closed'},
    @{State=$state::Sleep;Phase=0.25;Frame='dororong-closed-eyes.png';Label='Sleep'}))
{
    Render-State $case.State $case.Phase;Assert-Frame $image $case.Frame $case.Label
    if($case.State-eq$state::Idle-or$case.State-eq$state::Sleep)
    {Assert-Equal 1.0 ([double]$scale.ScaleY) "$($case.Label) used fractional vertical scale."}
}

Write-Output "EXACT ART PASS: canonical=$($expected.Open) squint=$($expected.Squint) closed=$($expected.Closed); authored-native export/evidence, exact nearest-neighbor states, 96-DPI presentation, alpha hit testing, and state mapping passed."
