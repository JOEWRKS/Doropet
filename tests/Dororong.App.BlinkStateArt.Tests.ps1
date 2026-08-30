Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -ne $Actual) { throw "$Message Expected '$Expected', observed '$Actual'." }
}

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition) { throw $Message }
}

function Test-Purple([Drawing.Color]$Pixel)
{
    return $Pixel.A -gt 0 -and ($Pixel.B - $Pixel.R) -ge 10
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $repositoryRoot 'src/Dororong.App/Assets'
$paths = [ordered]@{
    Open = Join-Path $assetRoot 'dororong-canonical.png'
    Half = Join-Path $assetRoot 'dororong-blink-squint.png'
    Closed = Join-Path $assetRoot 'dororong-closed-eyes.png'
}
$expectedHashes = [ordered]@{
    Open = '699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78'
    Half = '2A733093AC35B9678CBB90833272498C7BE755F77A574271DAE64FCE80FC90E2'
    Closed = '0D20EED5873A7E4277D6ED539474D875C9B8DF79998B1EFD74F46AA87662F488'
}

foreach ($name in $paths.Keys)
{
    Assert-True (Test-Path -LiteralPath $paths[$name] -PathType Leaf) `
        "The supplied $name runtime frame is missing."
    Assert-Equal $expectedHashes[$name] (Get-FileHash -Algorithm SHA256 -LiteralPath $paths[$name]).Hash `
        "The supplied $name runtime frame changed."
}
foreach ($obsolete in @(
    'dororong-eyes-70-open.png',
    'dororong-eyes-25-open.png',
    'dororong-half-closed-eyes.png'))
{
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $assetRoot $obsolete))) `
        "The obsolete blink frame '$obsolete' returned."
}

Add-Type -AssemblyName System.Drawing
$open=$null;$half=$null;$closed=$null
try
{
    $open=[Drawing.Bitmap]::new($paths.Open)
    $half=[Drawing.Bitmap]::new($paths.Half)
    $closed=[Drawing.Bitmap]::new($paths.Closed)
    foreach ($entry in ([ordered]@{Open=$open;Half=$half;Closed=$closed}).GetEnumerator())
    {
        Assert-Equal 96 $entry.Value.Width "$($entry.Key) width changed."
        Assert-Equal 96 $entry.Value.Height "$($entry.Key) height changed."
    }

    $eyeRegion = [pscustomobject]@{X0=16;X1=48;Y0=46;Y1=63}
    foreach ($entry in @(
        @{Name='half';Bitmap=$half},
        @{Name='closed';Bitmap=$closed}))
    {
        $insideChanges=0;$outsideChanges=0;$alphaChanges=0
        for ($y=0;$y-lt96;$y++)
        {
            for ($x=0;$x-lt96;$x++)
            {
                $before=$open.GetPixel($x,$y);$after=$entry.Bitmap.GetPixel($x,$y)
                if ($before.A-ne$after.A) { $alphaChanges++ }
                if ($before.ToArgb()-ne$after.ToArgb())
                {
                    if ($x-ge$eyeRegion.X0-and$x-le$eyeRegion.X1-and
                        $y-ge$eyeRegion.Y0-and$y-le$eyeRegion.Y1)
                    { $insideChanges++ }
                    else { $outsideChanges++ }
                }
            }
        }
        Assert-Equal 0 $alphaChanges "$($entry.Name) changed the supplied character alpha."
        Assert-Equal 0 $outsideChanges "$($entry.Name) changed pixels outside the supplied eye area."
        Assert-True ($insideChanges-gt0) "$($entry.Name) does not differ from the open frame."
    }

    $purpleCounts=[ordered]@{Open=0;Half=0;Closed=0}
    foreach ($entry in @(
        @{Name='Open';Bitmap=$open},
        @{Name='Half';Bitmap=$half},
        @{Name='Closed';Bitmap=$closed}))
    {
        foreach ($y in $eyeRegion.Y0..$eyeRegion.Y1)
        {
            foreach ($x in $eyeRegion.X0..$eyeRegion.X1)
            {
                if (Test-Purple $entry.Bitmap.GetPixel($x,$y)) { $purpleCounts[$entry.Name]++ }
            }
        }
    }
    Assert-True ($purpleCounts.Half-gt0-and$purpleCounts.Half-lt$purpleCounts.Open) `
        'The supplied half-close frame does not retain a smaller visible iris than open.'
    Assert-Equal 0 $purpleCounts.Closed 'The supplied full-close frame retains visible iris pixels.'
    Assert-True ($expectedHashes.Half-ne$expectedHashes.Closed) `
        'The supplied half-close and full-close frames collapsed into one state.'
}
finally
{
    foreach ($bitmap in @($closed,$half,$open)) { if ($null-ne$bitmap) { $bitmap.Dispose() } }
}

$runRoot = Join-Path $repositoryRoot ".superpowers/sdd/2026-08-30-dororong-user-authored-eye-frames/blink-state/$([Guid]::NewGuid().ToString('N'))"
$outputRoot = Join-Path $runRoot 'output'
$evidenceRoot = Join-Path $runRoot 'evidence'
$generatorOutput = & pwsh -NoProfile -File (Join-Path $repositoryRoot 'tools/Generate-CanonicalArt.ps1') `
    -SourcePath (Join-Path $assetRoot 'dororong-canonical-source.png') `
    -BodyMaskPath (Join-Path $assetRoot 'dororong-body-region-mask.png') `
    -OutputDirectory $outputRoot -EvidenceDirectory $evidenceRoot 2>&1
Assert-Equal 0 $LASTEXITCODE "Authored-frame generation failed: $($generatorOutput-join[Environment]::NewLine)"
Assert-True (($generatorOutput-join[Environment]::NewLine).Contains('authoredFrames=3',[StringComparison]::Ordinal)) `
    'The generator did not report all three supplied frames.'

foreach ($name in @('dororong-canonical.png','dororong-blink-squint.png','dororong-closed-eyes.png'))
{
    Assert-Equal (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $assetRoot $name)).Hash `
        (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $outputRoot $name)).Hash `
        "Regeneration changed supplied frame '$name'."
}

Write-Output 'BLINK STATE ART PASS: exact supplied open/half/full frames, eye-area-only state differences, shared alpha, distinct iris closure, and reproducible generation passed.'
