param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $repoRoot 'src/Dororong.App/Assets'
$sourceRoot = Join-Path $assetRoot 'frame-sources'
$approved = [ordered]@{
    'body-drag-entry-00-press.png' = '699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78'
    'body-drag-entry-01-release.png' = '67D032F0CD5B1B1110552FA103E9BB3972796A107756443D931FE9FE8165C167'
    'body-drag-entry-02-lengthen.png' = '361E2DFA57BADC62B904BACD255E0EB831E271EB9D7183D51EC4BFB0771D0185'
    'body-drag-entry-03-drop.png' = 'E9AD3012EA73FAA07DAA7DBF15AB16FB5C0956467D93E7F3D09E556492026D37'
    'body-drag-entry-04-stretch.png' = 'E6B6E15CA7FD1218431F90F08C4846DAB0266605664698E82F8D6C33F97F0BFA'
    'body-drag-entry-05-dangle.png' = '2C3518CAD331A37FB877FF42A8A09E83B93D8BBBA0B3240C52E5E0055FE0DB8A'
    'body-drag-entry-06-near-hang.png' = 'E8B17279C67821A5F31534675C4FF959C818B4CF56BAE54A2CBEB8C80CBC79A1'
    'body-drag-entry-07-hang.png' = '4FFE1250C599C199328622C99BE41697A0A8562058DB28C7AEEDD9E3CFDC383F'
    'body-drag-settle-00-hang.png' = '4FFE1250C599C199328622C99BE41697A0A8562058DB28C7AEEDD9E3CFDC383F'
    'body-drag-settle-01-lift.png' = 'B45EC1593A92E6592D19F9BAE4229E5FC299994A471049A87D2B2B938A641F69'
    'body-drag-settle-02-gather.png' = 'EC789221BA990E10EBB4C2C51A0BE2FE807CC50321D9D6BD2366A59B268070F2'
    'body-drag-settle-03-land.png' = '361E2DFA57BADC62B904BACD255E0EB831E271EB9D7183D51EC4BFB0771D0185'
    'body-drag-settle-04-recover.png' = '699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78'
}

if ($approved.Count -ne 13) { throw "Approved body-drag identity table contains $($approved.Count) files instead of 13." }

foreach ($name in $approved.Keys)
{
    $expected = $approved[$name]
    $runtimePath = Join-Path $assetRoot $name
    $sourcePath = Join-Path $sourceRoot $name
    if (-not (Test-Path -LiteralPath $runtimePath -PathType Leaf)) { throw "Missing approved runtime asset: $name" }
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Missing approved frame-source asset: $name" }

    $runtimeHash = (Get-FileHash -LiteralPath $runtimePath -Algorithm SHA256).Hash
    $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    if ($runtimeHash -ne $expected) { throw "$name runtime SHA-256 changed. Expected <$expected>; actual <$runtimeHash>." }
    if ($sourceHash -ne $expected) { throw "$name frame-source SHA-256 changed. Expected <$expected>; actual <$sourceHash>." }
}

Write-Output 'BODY DRAG APPROVED ASSET IDENTITY PASS: 13 exact paths and SHA-256 values match in runtime assets and frame-sources.'
