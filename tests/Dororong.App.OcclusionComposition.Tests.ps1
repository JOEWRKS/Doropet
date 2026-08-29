Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected,[object]$Actual,[string]$Message)
{
    if ($Expected -ne $Actual) { throw "$Message Expected '$Expected', observed '$Actual'." }
}

function Get-PurpleEyePixelCount(
    [Drawing.Bitmap]$Bitmap,[int]$StartX,[int]$EndX,[int]$StartY,[int]$EndY)
{
    $count = 0
    for ($y = $StartY; $y -le $EndY; $y++)
    {
        for ($x = $StartX; $x -le $EndX; $x++)
        {
            $pixel = $Bitmap.GetPixel($x,$y)
            if ($pixel.A -gt 0 -and ($pixel.B - $pixel.R) -ge 10) { $count++ }
        }
    }
    return $count
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical-source.png'
$maskPath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-body-region-mask.png'
$generatorPath = Join-Path $repositoryRoot 'tools/Generate-CanonicalArt.ps1'
$runRoot = Join-Path $repositoryRoot ".superpowers/sdd/2026-08-29-dororong-stage-a-eye-geometry-blink-recovery/task-6-test-runs/$([Guid]::NewGuid().ToString('N'))"
$outputDirectory = Join-Path $runRoot 'output'
$evidenceDirectory = Join-Path $runRoot 'evidence'
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

$generatorOutput = & pwsh -NoProfile -File $generatorPath `
    -SourcePath $sourcePath -BodyMaskPath $maskPath `
    -OutputDirectory $outputDirectory -EvidenceDirectory $evidenceDirectory 2>&1
Assert-Equal 0 $LASTEXITCODE "The real generator failed: $($generatorOutput -join [Environment]::NewLine)"

Add-Type -AssemblyName System.Drawing
$frozenHairRuns = @(
    '114:97-100','115:98-103','116:101-105','117:103-106','118:104-106',
    '119:104-106','120:104-106','121:104-106','122:104-106','123:104-106',
    '124:104-106','125:104-106','126:104-106','127:104-106','128:104-106',
    '129:104-106','130:104-106','131:104-106','132:104-106','133:103-105',
    '134:103-104','135:103-103','136:102-103','137:102-103','138:101-102',
    '139:101-102','140:101-101','141:100-101','142:99-100','143:99-100')
$runPayload = [Text.Encoding]::UTF8.GetBytes($frozenHairRuns -join "`n")
$runHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($runPayload))
Assert-Equal '8655F6B4897707D4B79A3523116ADD3A392BD2EE0AFEA903D825424F21FE1987' `
    $runHash 'Frozen foreground-hair coordinate ownership changed.'

$sourceOpen = $null; $sourceHalf = $null; $sourceClosed = $null
$nativeOpen = $null; $nativeHalf = $null
try
{
    $sourceOpen = [Drawing.Bitmap]::new((Join-Path $evidenceDirectory 'source-open-candidate.png'))
    $sourceHalf = [Drawing.Bitmap]::new((Join-Path $evidenceDirectory 'source-half-closed-candidate.png'))
    $sourceClosed = [Drawing.Bitmap]::new((Join-Path $evidenceDirectory 'source-closed-candidate.png'))
    $nativeOpen = [Drawing.Bitmap]::new((Join-Path $evidenceDirectory 'native-open-candidate.png'))
    $nativeHalf = [Drawing.Bitmap]::new((Join-Path $evidenceDirectory 'native-half-closed-candidate.png'))

    $failures = [Collections.Generic.List[string]]::new()
    $frozenCoordinateCount = 0
    foreach ($encodedRun in $frozenHairRuns)
    {
        $parts = $encodedRun.Split(':')
        $y = [int]$parts[0]
        $bounds = $parts[1].Split('-')
        foreach ($x in ([int]$bounds[0])..([int]$bounds[1]))
        {
            $frozenCoordinateCount++
            $open = $sourceOpen.GetPixel($x,$y).ToArgb()
            if ($open -ne $sourceHalf.GetPixel($x,$y).ToArgb())
            { $failures.Add("Half-close changed frozen foreground hair at ($x,$y).") }
            if ($open -ne $sourceClosed.GetPixel($x,$y).ToArgb())
            { $failures.Add("Full-close changed frozen foreground hair at ($x,$y).") }
        }
    }
    Assert-Equal 85 $frozenCoordinateCount 'Frozen foreground-hair coordinate count changed.'

    $leftOpen = Get-PurpleEyePixelCount $nativeOpen 16 30 46 61
    $leftHalf = Get-PurpleEyePixelCount $nativeHalf 16 30 46 61
    $rightOpen = Get-PurpleEyePixelCount $nativeOpen 34 48 46 63
    $rightHalf = Get-PurpleEyePixelCount $nativeHalf 34 48 46 63
    Assert-Equal 24 $leftOpen 'Viewer-left canonical purple-eye count changed.'
    Assert-Equal 40 $rightOpen 'Viewer-right canonical purple-eye count changed.'
    if ($leftHalf -ne 8)
    { $failures.Add("Viewer-left half-close purple count is not candidate D. Expected '8', observed '$leftHalf'.") }
    if ($rightHalf -ne 14)
    { $failures.Add("Viewer-right half-close purple count is not candidate D. Expected '14', observed '$rightHalf'.") }
    $leftRatio = [double]$leftHalf / $leftOpen
    $rightRatio = [double]$rightHalf / $rightOpen
    if ($leftRatio -ne 0.3333333333333333)
    { $failures.Add("Viewer-left half-close ratio changed. Expected '0.3333333333333333', observed '$leftRatio'.") }
    if ($rightRatio -ne 0.35)
    { $failures.Add("Viewer-right half-close ratio changed. Expected '0.35', observed '$rightRatio'.") }
    if ([Math]::Abs($leftRatio - $rightRatio) -gt 0.08)
    { $failures.Add("Candidate D half-close imbalance exceeds 0.08: left=$leftRatio right=$rightRatio.") }
    if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
}
finally
{
    foreach ($bitmap in @($nativeHalf,$nativeOpen,$sourceClosed,$sourceHalf,$sourceOpen))
    { if ($null -ne $bitmap) { $bitmap.Dispose() } }
}

Write-Output 'OCCLUSION COMPOSITION PASS: 85 frozen foreground-hair source coordinates stay canonical in half/full close and candidate D ratios are 8/24 and 14/40.'
