param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Equal($Expected, $Actual, [string]$Message)
{
    if ($Expected -ne $Actual) { throw "$Message Expected <$Expected>; actual <$Actual>." }
}

$approvedAssetNames = @(
    'body-drag-entry-00-press.png',
    'body-drag-entry-01-release.png',
    'body-drag-entry-02-lengthen.png',
    'body-drag-entry-03-drop.png',
    'body-drag-entry-04-stretch.png',
    'body-drag-entry-05-dangle.png',
    'body-drag-entry-06-near-hang.png',
    'body-drag-entry-07-hang.png',
    'body-drag-settle-00-hang.png',
    'body-drag-settle-01-lift.png',
    'body-drag-settle-02-gather.png',
    'body-drag-settle-03-land.png',
    'body-drag-settle-04-recover.png')
& (Join-Path $PSScriptRoot 'Dororong.App.BodyDragApprovedAssets.Tests.ps1')

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src/Dororong.App/Dororong.App.csproj'
$project = [xml](Get-Content -LiteralPath $projectPath -Raw)
$protectedResources = @(
    $project.SelectNodes('//Resource') |
        ForEach-Object { $_.Include } |
        Where-Object { $_ -match '^Assets\\body-drag-(entry|settle)-' })
$forbiddenResources = @(
    $project.SelectNodes('//Resource') |
        ForEach-Object { $_.Include } |
        Where-Object {
            $_ -like 'Assets\body-drag-v2-*.png' -or
            $_ -in @('Assets\dororong-body-drag-head.png', 'Assets\dororong-body-drag-hang-body.png')
        })

Assert-Equal 13 $protectedResources.Count 'The packaged protected body-drag asset set must contain exactly 13 files.'
for ($index = 0; $index -lt $approvedAssetNames.Count; $index++)
{
    Assert-Equal "Assets\$($approvedAssetNames[$index])" $protectedResources[$index] "Packaged protected body-drag asset path at index $index changed."
}
Assert-Equal 0 $forbiddenResources.Count 'The package still contains unauthorized v2 or split-layer body-drag resources.'

$forbiddenProductFiles = @()
foreach ($root in @((Join-Path $repoRoot 'src/Dororong.App/Assets'), (Join-Path $repoRoot 'src/Dororong.App/Assets/frame-sources')))
{
    $forbiddenProductFiles += @(Get-ChildItem -LiteralPath $root -File | Where-Object {
        $_.Name -like 'body-drag-v2-*.png' -or
        $_.Name -in @('dororong-body-drag-head.png', 'dororong-body-drag-hang-body.png')
    })
}
Assert-Equal 0 $forbiddenProductFiles.Count 'Unauthorized v2 or split-layer body-drag files remain in product asset paths.'

Write-Output 'DIRECT INTERACTION ASSETS PASS: exactly 13 protected attempt-40 assets are packaged and no unauthorized v2 or split-layer body-drag product files remain.'
