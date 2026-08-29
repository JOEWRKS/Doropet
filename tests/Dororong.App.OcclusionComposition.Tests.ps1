Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$replacementTest = Join-Path $PSScriptRoot 'Dororong.App.BlinkStateArt.Tests.ps1'
$output = & pwsh -NoProfile -File $replacementTest 2>&1
if ($LASTEXITCODE -ne 0)
{
    throw "The authored native-state protected-art contract failed: $($output -join [Environment]::NewLine)"
}
$output
Write-Output 'OCCLUSION COMPOSITION PASS: the canonical-hair/protected-pixel suite delegates to the authored native face-state contract.'
