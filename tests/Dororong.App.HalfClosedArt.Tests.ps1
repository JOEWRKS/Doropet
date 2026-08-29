Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -ne $Actual)
    {
        throw "$Message Expected '$Expected', observed '$Actual'."
    }
}

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition) { throw $Message }
}

function Get-PurpleEyePixelCount(
    [Drawing.Bitmap]$Bitmap,
    [int]$StartX,[int]$EndX,[int]$StartY,[int]$EndY)
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
$runRoot = Join-Path $repositoryRoot ".superpowers/sdd/2026-08-29-dororong-stage-a-half-closed-art/$([Guid]::NewGuid().ToString('N'))"
$outputDirectory = Join-Path $runRoot 'output'
$evidenceDirectory = Join-Path $runRoot 'evidence'
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

$generatorOutput = & pwsh -NoProfile -File $generatorPath `
    -SourcePath $sourcePath -BodyMaskPath $maskPath `
    -OutputDirectory $outputDirectory -EvidenceDirectory $evidenceDirectory 2>&1
Assert-Equal 0 $LASTEXITCODE "The real generator failed: $($generatorOutput -join [Environment]::NewLine)"

$runtimeHalfPath = Join-Path $outputDirectory 'dororong-half-closed-eyes.png'
$sourceOpenPath = Join-Path $evidenceDirectory 'source-open-candidate.png'
$sourceClosedPath = Join-Path $evidenceDirectory 'source-closed-candidate.png'
$sourceHalfPath = Join-Path $evidenceDirectory 'source-half-closed-candidate.png'
$nativeOpenPath = Join-Path $evidenceDirectory 'native-open-candidate.png'
$nativeClosedPath = Join-Path $evidenceDirectory 'native-closed-candidate.png'
$nativeHalfPath = Join-Path $evidenceDirectory 'native-half-closed-candidate.png'

foreach ($path in @(
    $runtimeHalfPath,$sourceOpenPath,$sourceClosedPath,$sourceHalfPath,
    $nativeOpenPath,$nativeClosedPath,$nativeHalfPath))
{
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) `
        "Half-closed generator contract is missing '$path'."
}

$expectedSourceHalfHash = 'DE6CFFA2B09629B53726673D0D800F1387276E8CA110A1024247FCC2942A584B'
$expectedNativeHalfHash = '6B3FF731B4AB6E3781AD10598AB3A7BAEDFEA2DB7D4D130357E8642C9FD9AFFC'
Assert-Equal $expectedSourceHalfHash `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceHalfPath).Hash `
    'The deterministic source half-closed frame changed.'
Assert-Equal $expectedNativeHalfHash `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $nativeHalfPath).Hash `
    'The deterministic native half-closed frame changed.'
Assert-Equal $expectedNativeHalfHash `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $runtimeHalfPath).Hash `
    'The runtime half-closed frame differs from its one-resize evidence.'

Add-Type -AssemblyName System.Drawing
$allowedSourceChanges = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($encodedRun in @(
    '114:43-59','115:44-60','116:44-62','117:43-63','118:43-64','119:43-64',
    '120:43-65','121:43-64','122:43-64','123:43-64','124:43-64','125:43-64',
    '126:43-64','127:43-64','128:43-64','129:43-64','130:43-64','131:44-63',
    '132:45-62','133:46-61','134:48-59','135:51-57',
    '114:92-100','115:89-103','116:86-105','117:86-106','118:86-106','119:86-106',
    '120:86-106','121:86-106','122:86-106','123:86-106','124:86-106','125:86-106',
    '126:86-106','127:86-106','128:86-106','129:86-106','130:86-106','131:86-106',
    '132:86-106','133:87-105','134:88-104','135:90-103','136:93-103','137:86-103',
    '138:86-102','139:88-102','140:89-101','141:99-101','142:99-100','143:99-100'))
{
    $parts = $encodedRun.Split(':')
    $y = [int]$parts[0]
    $bounds = $parts[1].Split('-')
    foreach ($x in ([int]$bounds[0])..([int]$bounds[1]))
    {
        $null = $allowedSourceChanges.Add("$x,$y")
    }
}

$sourceOpen=$null;$sourceClosed=$null;$sourceHalf=$null
$nativeOpen=$null;$nativeClosed=$null;$nativeHalf=$null
try
{
    $sourceOpen = [Drawing.Bitmap]::new($sourceOpenPath)
    $sourceClosed = [Drawing.Bitmap]::new($sourceClosedPath)
    $sourceHalf = [Drawing.Bitmap]::new($sourceHalfPath)
    $nativeOpen = [Drawing.Bitmap]::new($nativeOpenPath)
    $nativeClosed = [Drawing.Bitmap]::new($nativeClosedPath)
    $nativeHalf = [Drawing.Bitmap]::new($nativeHalfPath)

    foreach ($bitmap in @($sourceOpen,$sourceClosed,$sourceHalf))
    {
        Assert-Equal 225 $bitmap.Width 'A source frame width changed.'
        Assert-Equal 225 $bitmap.Height 'A source frame height changed.'
    }
    foreach ($bitmap in @($nativeOpen,$nativeClosed,$nativeHalf))
    {
        Assert-Equal 96 $bitmap.Width 'A native frame width changed.'
        Assert-Equal 96 $bitmap.Height 'A native frame height changed.'
    }

    $sourceHalfChanges=0;$sourceClosedChanges=0
    $halfEqualsOpenOnly=0;$halfEqualsClosedOnly=0
    for ($y=0;$y-lt225;$y++)
    {
        for ($x=0;$x-lt225;$x++)
        {
            $open=$sourceOpen.GetPixel($x,$y)
            $closed=$sourceClosed.GetPixel($x,$y)
            $half=$sourceHalf.GetPixel($x,$y)
            Assert-Equal $open.A $closed.A "Source full-close alpha changed at ($x,$y)."
            Assert-Equal $open.A $half.A "Source half-close alpha changed at ($x,$y)."
            if ($open.ToArgb() -ne $closed.ToArgb()) { $sourceClosedChanges++ }
            if ($open.ToArgb() -ne $half.ToArgb())
            {
                $sourceHalfChanges++
                Assert-True $allowedSourceChanges.Contains("$x,$y") `
                    "Source half-close change escaped the independent eye stencils at ($x,$y)."
            }
            if ($half.ToArgb() -eq $open.ToArgb() -and $half.ToArgb() -ne $closed.ToArgb())
            { $halfEqualsOpenOnly++ }
            if ($half.ToArgb() -eq $closed.ToArgb() -and $half.ToArgb() -ne $open.ToArgb())
            { $halfEqualsClosedOnly++ }
        }
    }
    Assert-True ($sourceHalfChanges -gt 0 -and $sourceHalfChanges -lt $sourceClosedChanges) `
        "Half-close is not a distinct intermediate source drawing (half=$sourceHalfChanges, closed=$sourceClosedChanges)."
    Assert-True ($halfEqualsOpenOnly -ge 100) `
        "Half-close did not retain a substantial exact-open lower-eye region (count=$halfEqualsOpenOnly)."
    Assert-True ($halfEqualsClosedOnly -ge 100) `
        "Half-close did not blank a substantial upper-eye region with exact face pixels (count=$halfEqualsClosedOnly)."

    $nativeHalfChanges=0
    for ($y=0;$y-lt96;$y++)
    {
        for ($x=0;$x-lt96;$x++)
        {
            $open=$nativeOpen.GetPixel($x,$y)
            $half=$nativeHalf.GetPixel($x,$y)
            Assert-Equal $open.A $half.A "Native half-close alpha changed at ($x,$y)."
            if ($open.ToArgb() -eq $half.ToArgb()) { continue }
            $nativeHalfChanges++
            $insideFilter = ($x-ge16-and$x-le30-and$y-ge46-and$y-le61) -or `
                ($x-ge34-and$x-le48-and$y-ge46-and$y-le63)
            Assert-True $insideFilter `
                "Native half-close change escaped scaled eye filter support at ($x,$y)."
        }
    }
    Assert-True ($nativeHalfChanges -ge 40) `
        "Native half-close did not visibly descend over both eyes (changes=$nativeHalfChanges)."

    foreach ($eye in @(
        @{Name='viewer-left';StartX=16;EndX=30;UpperStart=46;UpperEnd=51;LowerStart=52;LowerEnd=61},
        @{Name='viewer-right';StartX=34;EndX=48;UpperStart=46;UpperEnd=54;LowerStart=55;LowerEnd=63}))
    {
        $openUpper=Get-PurpleEyePixelCount $nativeOpen $eye.StartX $eye.EndX $eye.UpperStart $eye.UpperEnd
        $halfUpper=Get-PurpleEyePixelCount $nativeHalf $eye.StartX $eye.EndX $eye.UpperStart $eye.UpperEnd
        $openLower=Get-PurpleEyePixelCount $nativeOpen $eye.StartX $eye.EndX $eye.LowerStart $eye.LowerEnd
        $halfLower=Get-PurpleEyePixelCount $nativeHalf $eye.StartX $eye.EndX $eye.LowerStart $eye.LowerEnd
        $closedLower=Get-PurpleEyePixelCount $nativeClosed $eye.StartX $eye.EndX $eye.LowerStart $eye.LowerEnd
        $openTotal=Get-PurpleEyePixelCount $nativeOpen $eye.StartX $eye.EndX $eye.UpperStart $eye.LowerEnd
        $halfTotal=Get-PurpleEyePixelCount $nativeHalf $eye.StartX $eye.EndX $eye.UpperStart $eye.LowerEnd
        Assert-True (($openUpper-$halfUpper)-ge2) `
            "$($eye.Name) upper iris was not blanked by the descending lid (open=$openUpper, half=$halfUpper)."
        Assert-True ($halfLower-ge2-and$halfLower-le$openLower) `
            "$($eye.Name) did not retain a small lower iris crescent (open=$openLower, half=$halfLower)."
        Assert-True ($halfTotal-ge2-and$halfTotal-le($openTotal*0.60)) `
            "$($eye.Name) retained too much of the full open iris to read as a lower crescent (open=$openTotal, half=$halfTotal)."
        Assert-True ($halfLower-gt$closedLower) `
            "$($eye.Name) lower iris crescent is not distinct from full close (half=$halfLower, closed=$closedLower)."
        Write-Output "HALF EYE METRICS name=$($eye.Name) totalPurple=$openTotal->$halfTotal upperPurple=$openUpper->$halfUpper lowerPurple=$openLower->$halfLower->$closedLower"
    }
}
finally
{
    foreach ($bitmap in @($nativeHalf,$nativeClosed,$nativeOpen,$sourceHalf,$sourceClosed,$sourceOpen))
    {
        if ($null -ne $bitmap) { $bitmap.Dispose() }
    }
}

Write-Output 'HALF-CLOSED ART PASS: deterministic source/proxy/single-resize identity, protected non-eye/alpha pixels, descending upper lids, and retained lower iris crescents passed.'
