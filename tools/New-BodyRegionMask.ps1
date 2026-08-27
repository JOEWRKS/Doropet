param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$SeedPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(Mandatory = $true)]
    [string]$EvidenceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$moduleRoot = $PSScriptRoot
Import-Module (Join-Path $moduleRoot 'Dororong.SourceRaster.psm1') -Force
Import-Module (Join-Path $moduleRoot 'Dororong.BodyOwnership.psm1') -Force
$configuration = Import-PowerShellDataFile -LiteralPath (
    Join-Path $moduleRoot 'Dororong.BodyOwnership.Constants.psd1')

function Get-PngBytes([System.Drawing.Bitmap]$Bitmap)
{
    $stream = [System.IO.MemoryStream]::new()
    try
    {
        $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally
    { $stream.Dispose() }
}

function Save-Png(
    [System.Drawing.Bitmap]$Bitmap,
    [string]$Path,
    [bool]$ProtectExisting)
{
    $bytes = Get-PngBytes $Bitmap
    if ([System.IO.File]::Exists($Path))
    {
        $existing = [System.IO.File]::ReadAllBytes($Path)
        $identical = $existing.Length -eq $bytes.Length
        for ($index = 0; $identical -and $index -lt $bytes.Length; $index++)
        {
            if ($existing[$index] -ne $bytes[$index])
            { $identical = $false }
        }
        if ($identical)
        { return }
        if ($ProtectExisting)
        { throw "Evidence path already contains different bytes: $Path" }
    }
    [System.IO.File]::WriteAllBytes($Path, $bytes)
}

function Save-NearestNeighbor(
    [System.Drawing.Bitmap]$Bitmap,
    [string]$Path,
    [int]$Scale,
    [bool]$ProtectExisting)
{
    $scaled = [System.Drawing.Bitmap]::new(
        $Bitmap.Width * $Scale,
        $Bitmap.Height * $Scale,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        $graphics = [System.Drawing.Graphics]::FromImage($scaled)
        try
        {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighSpeed
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
            $graphics.DrawImage(
                $Bitmap,
                [System.Drawing.Rectangle]::new(0, 0, $scaled.Width, $scaled.Height),
                0,
                0,
                $Bitmap.Width,
                $Bitmap.Height,
                [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally
        { $graphics.Dispose() }
        Save-Png $scaled $Path $ProtectExisting
    }
    finally
    { $scaled.Dispose() }
}

function Blend-Color(
    [System.Drawing.Color]$Base,
    [System.Drawing.Color]$Overlay,
    [int]$OverlayWeight)
{
    $baseWeight = 255 - $OverlayWeight
    return [System.Drawing.Color]::FromArgb(
        255,
        [int][Math]::Round((($Base.R * $baseWeight) + ($Overlay.R * $OverlayWeight)) / 255.0),
        [int][Math]::Round((($Base.G * $baseWeight) + ($Overlay.G * $OverlayWeight)) / 255.0),
        [int][Math]::Round((($Base.B * $baseWeight) + ($Overlay.B * $OverlayWeight)) / 255.0))
}

function New-OwnershipOverlay(
    [System.Drawing.Bitmap]$ProcessedSource,
    [object]$Ownership,
    [System.Drawing.Color]$Background)
{
    $size = $ProcessedSource.Width
    $selectedSet = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($component in @($Ownership.SelectedComponents))
    {
        foreach ($encoded in $component.Pixels)
        { [void]$selectedSet.Add([int]$encoded) }
    }
    $protectedSet = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($component in @($Ownership.ProtectedComponents))
    {
        foreach ($encoded in $component.Pixels)
        { [void]$protectedSet.Add([int]$encoded) }
    }

    $overlay = [System.Drawing.Bitmap]::new(
        $size,
        $size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $size; $y++)
    {
        for ($x = 0; $x -lt $size; $x++)
        {
            $encoded = ($y * $size) + $x
            $sourcePixel = $ProcessedSource.GetPixel($x, $y)
            if ($sourcePixel.A -eq 0)
            {
                $overlay.SetPixel($x, $y, $Background)
                continue
            }

            $base = [System.Drawing.Color]::FromArgb(255, $sourcePixel.R, $sourcePixel.G, $sourcePixel.B)
            if ($selectedSet.Contains($encoded))
            { $overlay.SetPixel($x, $y, (Blend-Color $base ([System.Drawing.Color]::FromArgb(255,255,112,0)) 205)) }
            elseif ($Ownership.CleanedSeed[$x,$y])
            { $overlay.SetPixel($x, $y, (Blend-Color $base ([System.Drawing.Color]::FromArgb(255,0,174,239)) 145)) }
            elseif ($protectedSet.Contains($encoded))
            { $overlay.SetPixel($x, $y, (Blend-Color $base ([System.Drawing.Color]::FromArgb(255,216,64,191)) 115)) }
            else
            { $overlay.SetPixel($x, $y, $base) }
        }
    }
    return $overlay
}

$fullSourcePath = [System.IO.Path]::GetFullPath($SourcePath)
$fullSeedPath = [System.IO.Path]::GetFullPath($SeedPath)
$fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$fullEvidenceDirectory = [System.IO.Path]::GetFullPath($EvidenceDirectory)

if (-not [System.IO.File]::Exists($fullSourcePath))
{ throw "Canonical source is missing: $fullSourcePath" }
if (-not [System.IO.File]::Exists($fullSeedPath))
{ throw "Immutable predecessor seed is missing: $fullSeedPath" }
if ($fullOutputPath -eq $fullSourcePath -or $fullOutputPath -eq $fullSeedPath)
{ throw 'OutputPath must not overwrite the canonical source or immutable predecessor seed.' }

$sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullSourcePath).Hash
$seedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullSeedPath).Hash
if ($sourceHash -ne [string]$configuration.SourceSha256)
{ throw "Canonical source hash changed. Expected $($configuration.SourceSha256), observed $sourceHash." }
if ($seedHash -ne [string]$configuration.SeedSha256)
{ throw "Immutable predecessor seed hash changed. Expected $($configuration.SeedSha256), observed $seedHash." }

[System.IO.Directory]::CreateDirectory((Split-Path -Parent $fullOutputPath)) | Out-Null
[System.IO.Directory]::CreateDirectory($fullEvidenceDirectory) | Out-Null

$rawSource = [System.Drawing.Bitmap]::new($fullSourcePath)
$processedSource = $null
$seed = $null
$ownership = $null
$whiteOverlay = $null
$darkOverlay = $null
try
{
    $processedSource = Remove-DororongBoundaryBackground $rawSource
    $seed = Import-DororongBinaryMask $fullSeedPath
    $ownership = Get-DororongBodyOwnership $processedSource $seed $configuration

    Save-Png $ownership.Mask $fullOutputPath $false
    $maskHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullOutputPath).Hash
    if (-not [string]::IsNullOrWhiteSpace([string]$configuration.ExpectedFinalMaskSha256) -and
        $maskHash -ne [string]$configuration.ExpectedFinalMaskSha256)
    { throw "Final mask hash changed. Expected $($configuration.ExpectedFinalMaskSha256), observed $maskHash." }

    $sourceEvidencePath = Join-Path $fullEvidenceDirectory 'dororong-canonical-source.png'
    if ([System.IO.File]::Exists($sourceEvidencePath))
    {
        $evidenceSourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceEvidencePath).Hash
        if ($evidenceSourceHash -ne $sourceHash)
        { throw "Evidence path already contains a different canonical source: $sourceEvidencePath" }
    }
    else
    { [System.IO.File]::Copy($fullSourcePath, $sourceEvidencePath, $false) }

    Save-Png $processedSource (Join-Path $fullEvidenceDirectory 'dororong-cleaned-source.png') $true
    Save-Png $ownership.Mask (Join-Path $fullEvidenceDirectory 'dororong-body-ownership-mask.png') $true
    Save-NearestNeighbor $rawSource `
        (Join-Path $fullEvidenceDirectory 'dororong-canonical-source-4x.png') 4 $true
    Save-NearestNeighbor $ownership.Mask `
        (Join-Path $fullEvidenceDirectory 'dororong-body-ownership-mask-4x.png') 4 $true

    $whiteOverlay = New-OwnershipOverlay $processedSource $ownership ([System.Drawing.Color]::White)
    $darkOverlay = New-OwnershipOverlay $processedSource $ownership (
        [System.Drawing.Color]::FromArgb(255,18,20,28))
    Save-Png $whiteOverlay `
        (Join-Path $fullEvidenceDirectory 'dororong-body-ownership-overlay-white.png') $true
    Save-NearestNeighbor $whiteOverlay `
        (Join-Path $fullEvidenceDirectory 'dororong-body-ownership-overlay-white-4x.png') 4 $true
    Save-Png $darkOverlay `
        (Join-Path $fullEvidenceDirectory 'dororong-body-ownership-overlay-rgb-18-20-28.png') $true
    Save-NearestNeighbor $darkOverlay `
        (Join-Path $fullEvidenceDirectory 'dororong-body-ownership-overlay-rgb-18-20-28-4x.png') 4 $true

    $writableCount = 0
    $minimumX = [int]$configuration.CanvasSize
    $minimumY = [int]$configuration.CanvasSize
    $maximumX = -1
    $maximumY = -1
    for ($y = 0; $y -lt [int]$configuration.CanvasSize; $y++)
    {
        for ($x = 0; $x -lt [int]$configuration.CanvasSize; $x++)
        {
            if ($ownership.Mask.GetPixel($x, $y).R -ne 255)
            { continue }
            $writableCount++
            if ($x -lt $minimumX) { $minimumX = $x }
            if ($y -lt $minimumY) { $minimumY = $y }
            if ($x -gt $maximumX) { $maximumX = $x }
            if ($y -gt $maximumY) { $maximumY = $y }
        }
    }

    Write-Output "SOURCE hash=$sourceHash"
    Write-Output "SEED hash=$seedHash"
    Write-Output "OWNERSHIP selectedComponents=$($ownership.SelectedComponents.Count) selectedPixels=$($ownership.SelectedPixelCount) membershipHash=$($ownership.MembershipHash) maximumSpread=$($ownership.MaximumSelectedChroma)"
    Write-Output "FINAL MASK hash=$maskHash count=$writableCount bounds=$minimumX,$minimumY-$maximumX,$maximumY"
    Write-Output "OWNERSHIP EVIDENCE directory=$fullEvidenceDirectory"
}
finally
{
    if ($null -ne $darkOverlay) { $darkOverlay.Dispose() }
    if ($null -ne $whiteOverlay) { $whiteOverlay.Dispose() }
    if ($null -ne $ownership -and $null -ne $ownership.Mask) { $ownership.Mask.Dispose() }
    if ($null -ne $seed) { $seed.Dispose() }
    if ($null -ne $processedSource) { $processedSource.Dispose() }
    $rawSource.Dispose()
}
