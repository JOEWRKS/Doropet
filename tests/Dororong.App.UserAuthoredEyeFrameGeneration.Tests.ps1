Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -ne $Actual) { throw "$Message Expected '$Expected', observed '$Actual'." }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $repositoryRoot 'src/Dororong.App/Assets'
$fixtureRoot = Join-Path $PSScriptRoot 'fixtures'
$generator = Join-Path $repositoryRoot 'tools/Generate-CanonicalArt.ps1'
$runRoot = Join-Path $repositoryRoot ".superpowers/sdd/2026-08-30-dororong-user-authored-eye-frames/$([Guid]::NewGuid().ToString('N'))"
$outputRoot = Join-Path $runRoot 'output'

# Break caught: regenerating canonical art silently replaces a user-authored
# open, half-close, or full-close runtime frame with an older derived frame.
$generatorOutput = & pwsh -NoProfile -File $generator `
    -SourcePath (Join-Path $assetRoot 'dororong-canonical-source.png') `
    -BodyMaskPath (Join-Path $assetRoot 'dororong-body-region-mask.png') `
    -OutputDirectory $outputRoot 2>&1
Assert-Equal 0 $LASTEXITCODE "Canonical generation failed: $($generatorOutput-join[Environment]::NewLine)"

foreach ($case in @(
    @{ Name = 'dororong-canonical.png'; Reference = 'dororong-user-authored-open-reference.png' },
    @{ Name = 'dororong-blink-squint.png'; Reference = 'dororong-user-authored-half-reference.png' },
    @{ Name = 'dororong-closed-eyes.png'; Reference = 'dororong-user-authored-closed-reference.png' }))
{
    $expected = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $fixtureRoot $case.Reference)).Hash
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $outputRoot $case.Name)).Hash
    Assert-Equal $expected $actual "Generated $($case.Name) diverged from the supplied frame."
}

Write-Output 'USER-AUTHORED EYE FRAME GENERATION PASS: regeneration preserves all three supplied frames exactly.'
