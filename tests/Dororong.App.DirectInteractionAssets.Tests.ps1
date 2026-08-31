param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition) { throw $Message }
}

function Assert-Equal($Expected, $Actual, [string]$Message)
{
    if ($Expected -ne $Actual) { throw "$Message Expected <$Expected>; actual <$Actual>." }
}

function Get-AlphaBounds([System.Drawing.Bitmap]$Bitmap)
{
    $minX = $Bitmap.Width
    $minY = $Bitmap.Height
    $maxX = -1
    $maxY = -1
    $opaque = 0
    for ($y = 0; $y -lt $Bitmap.Height; $y++)
    {
        for ($x = 0; $x -lt $Bitmap.Width; $x++)
        {
            $pixel = $Bitmap.GetPixel($x, $y)
            if ($pixel.A -gt 0)
            {
                $minX = [Math]::Min($minX, $x)
                $minY = [Math]::Min($minY, $y)
                $maxX = [Math]::Max($maxX, $x)
                $maxY = [Math]::Max($maxY, $y)
                $opaque++
            }
            elseif (($pixel.R -ne 0) -or ($pixel.G -ne 0) -or ($pixel.B -ne 0))
            {
                throw "Transparent RGB fringe found at ($x,$y)."
            }
        }
    }

    return [pscustomobject]@{ MinX=$minX; MinY=$minY; MaxX=$maxX; MaxY=$maxY; Opaque=$opaque }
}

function Assert-ProtectedIdentity(
    [System.Drawing.Bitmap]$Canonical,
    [System.Drawing.Bitmap]$Frame,
    [string]$Name)
{
    # The full head/face and the low right-side ornament stack are outside body-drag deformation.
    for ($y = 0; $y -le 54; $y++)
    {
        for ($x = 0; $x -lt 96; $x++)
        {
            Assert-Equal $Canonical.GetPixel($x,$y).ToArgb() $Frame.GetPixel($x,$y).ToArgb() `
                "$Name changed the protected head/face pixel at ($x,$y)."
        }
    }
    for ($y = 55; $y -le 76; $y++)
    {
        for ($x = 64; $x -lt 96; $x++)
        {
            Assert-Equal $Canonical.GetPixel($x,$y).ToArgb() $Frame.GetPixel($x,$y).ToArgb() `
                "$Name changed the protected rose/bow/ribbon pixel at ($x,$y)."
        }
    }
}

function Assert-FourReadableLegs([System.Drawing.Bitmap]$Frame, [string]$Name, [int]$Bottom)
{
    $fourRunRow = -1
    for ($row = 72; $row -le $Bottom; $row++)
    {
        $runs = [Collections.Generic.List[object]]::new()
        $start = -1
        for ($x = 26; $x -le 79; $x++)
        {
            $on = $Frame.GetPixel($x,$row).A -ge 160
            if ($on -and $start -lt 0) { $start = $x }
            if ($start -ge 0 -and ((-not $on) -or $x -eq 79))
            {
                $end = if ($on -and $x -eq 79) { $x } else { $x - 1 }
                if (($end - $start + 1) -ge 2) { $runs.Add([pscustomobject]@{Start=$start;End=$end}) }
                $start = -1
            }
        }
        if ($runs.Count -eq 4) { $fourRunRow = $row; break }
    }
    Assert-True ($fourRunRow -ge 0) "$Name never exposes four separated, countable lower-body leg runs."
}

function Get-ChangedPixelCount([System.Drawing.Bitmap]$Left, [System.Drawing.Bitmap]$Right)
{
    $changed = 0
    for ($y = 0; $y -lt 96; $y++)
    {
        for ($x = 0; $x -lt 96; $x++)
        {
            if ($Left.GetPixel($x,$y).ToArgb() -ne $Right.GetPixel($x,$y).ToArgb()) { $changed++ }
        }
    }
    return $changed
}

function Get-ChangedSilhouettePixelCount([System.Drawing.Bitmap]$Left, [System.Drawing.Bitmap]$Right)
{
    $changed = 0
    for ($y = 0; $y -lt 96; $y++)
    {
        for ($x = 0; $x -lt 96; $x++)
        {
            if (($Left.GetPixel($x,$y).A -gt 32) -ne ($Right.GetPixel($x,$y).A -gt 32)) { $changed++ }
        }
    }
    return $changed
}

function Get-LegBottoms([System.Drawing.Bitmap]$Frame)
{
    $bottoms = [Collections.Generic.List[int]]::new()
    foreach ($band in @(@(26,36),@(39,49),@(52,62),@(67,79)))
    {
        $bottom = -1
        for ($y = 55; $y -lt 96; $y++)
        {
            for ($x = $band[0]; $x -le $band[1]; $x++)
            {
                if ($Frame.GetPixel($x,$y).A -ge 160) { $bottom = [Math]::Max($bottom,$y) }
            }
        }
        $bottoms.Add($bottom)
    }
    return [int[]]$bottoms.ToArray()
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $repoRoot 'src/Dororong.App/Assets'
$sourceRoot = Join-Path $assetRoot 'frame-sources'
$canonicalPath = Join-Path $assetRoot 'dororong-canonical.png'
$projectPath = Join-Path $repoRoot 'src/Dororong.App/Dororong.App.csproj'
$entryNames = @(
    'body-drag-entry-00-press.png',
    'body-drag-entry-01-lengthen.png',
    'body-drag-entry-02-drop.png',
    'body-drag-entry-03-stretch.png',
    'body-drag-entry-04-dangle.png',
    'body-drag-entry-05-near-hang.png',
    'body-drag-entry-06-hang.png')
$settleNames = @(
    'body-drag-settle-00-hang.png',
    'body-drag-settle-01-lift.png',
    'body-drag-settle-02-gather.png',
    'body-drag-settle-03-land.png',
    'body-drag-settle-04-recover.png')
$names = @($entryNames + $settleNames)

Add-Type -AssemblyName System.Drawing
$canonical = [System.Drawing.Bitmap]::new($canonicalPath)
$bitmaps = [Collections.Generic.List[System.Drawing.Bitmap]]::new()
try
{
    Assert-Equal 12 $names.Count 'Body-drag contract must contain exactly twelve ordered keys.'
    $canonicalBounds = Get-AlphaBounds $canonical
    $project = [xml](Get-Content -Raw $projectPath)
    $resourceIncludes = @($project.SelectNodes('//Resource') | ForEach-Object { $_.Include })
    $boundsByName = @{}
    $bitmapByName = @{}

    foreach ($name in $names)
    {
        $runtimePath = Join-Path $assetRoot $name
        $sourcePath = Join-Path $sourceRoot $name
        Assert-True (Test-Path -LiteralPath $runtimePath -PathType Leaf) "Missing runtime body-drag key: $name"
        Assert-True (Test-Path -LiteralPath $sourcePath -PathType Leaf) "Missing source body-drag key: $name"
        Assert-Equal (Get-FileHash $sourcePath -Algorithm SHA256).Hash `
            (Get-FileHash $runtimePath -Algorithm SHA256).Hash "$name source/runtime bytes differ."
        Assert-True ($resourceIncludes -contains "Assets\$name") "$name is not packaged as a WPF Resource."

        $bitmap = [System.Drawing.Bitmap]::new($runtimePath)
        $bitmaps.Add($bitmap)
        $bitmapByName[$name] = $bitmap
        Assert-Equal 96 $bitmap.Width "$name width changed."
        Assert-Equal 96 $bitmap.Height "$name height changed."
        Assert-ProtectedIdentity $canonical $bitmap $name
        $bounds = Get-AlphaBounds $bitmap
        $boundsByName[$name] = $bounds
        Assert-Equal $canonicalBounds.MinY $bounds.MinY "$name moved the fixed top/grab anchor."
        Assert-True ($bounds.Opaque -ge 1800) "$name is not a complete character image."
        Assert-True ($bounds.MaxX -le 84) "$name adds a right-side protrusion/tail outside the accepted silhouette."
    }

    $entryBottoms = @($entryNames | ForEach-Object { $boundsByName[$_].MaxY })
    for ($i = 1; $i -lt $entryBottoms.Count; $i++)
    {
        Assert-True ($entryBottoms[$i] -ge $entryBottoms[$i-1]) `
            "Entry torso shortens between $($entryNames[$i-1]) and $($entryNames[$i])."
        Assert-True (($entryBottoms[$i] - $entryBottoms[$i-1]) -le 4) `
            "Entry torso jumps more than four pixels between adjacent keys."
    }
    Assert-True (($entryBottoms[-1] - $entryBottoms[0]) -ge 7) 'Entry does not visibly extend the torso to the available 96px frame edge.'

    $settleBottoms = @($settleNames | ForEach-Object { $boundsByName[$_].MaxY })
    for ($i = 1; $i -lt $settleBottoms.Count; $i++)
    {
        Assert-True ($settleBottoms[$i] -le $settleBottoms[$i-1]) `
            "Settle extends downward between $($settleNames[$i-1]) and $($settleNames[$i])."
        Assert-True (($settleBottoms[$i-1] - $settleBottoms[$i]) -le 4) `
            "Settle torso jumps more than four pixels between adjacent keys."
    }

    foreach ($name in @($entryNames[2..6] + $settleNames[0..2]))
    {
        Assert-FourReadableLegs $bitmapByName[$name] $name $boundsByName[$name].MaxY
    }

    $entryEndpointChanged = Get-ChangedPixelCount $bitmapByName[$entryNames[0]] $bitmapByName[$entryNames[1]]
    $entryEndpointSilhouetteChanged = Get-ChangedSilhouettePixelCount $bitmapByName[$entryNames[0]] $bitmapByName[$entryNames[1]]
    Assert-True ($entryEndpointChanged -le 750) `
        "First changed entry key replaces too much of the rounded canonical body: $entryEndpointChanged pixels."
    Assert-True ($entryEndpointSilhouetteChanged -le 300) `
        "First changed entry key jumps its silhouette across $entryEndpointSilhouetteChanged pixels."

    $settleEndpointChanged = Get-ChangedPixelCount $bitmapByName[$settleNames[3]] $bitmapByName[$settleNames[4]]
    $settleEndpointSilhouetteChanged = Get-ChangedSilhouettePixelCount $bitmapByName[$settleNames[3]] $bitmapByName[$settleNames[4]]
    Assert-True ($settleEndpointChanged -le 750) `
        "Final land key is not close enough to canonical recovery: $settleEndpointChanged pixels change."
    Assert-True ($settleEndpointSilhouetteChanged -le 300) `
        "Final land key jumps its silhouette into canonical across $settleEndpointSilhouetteChanged pixels."

    $timeline = @($entryNames + $settleNames[1..4])
    for ($i = 1; $i -lt $timeline.Count; $i++)
    {
        $changed = Get-ChangedPixelCount $bitmapByName[$timeline[$i-1]] $bitmapByName[$timeline[$i]]
        Assert-True ($changed -gt 0) "Adjacent keys $($timeline[$i-1]) and $($timeline[$i]) are identical."
        Assert-True ($changed -le 850) `
            "Adjacent keys $($timeline[$i-1]) and $($timeline[$i]) jump across $changed pixels."
    }

    $hangLegBottoms = Get-LegBottoms $bitmapByName[$entryNames[6]]
    $distinctHangLegBottoms = @($hangLegBottoms | Sort-Object -Unique)
    Assert-True ($distinctHangLegBottoms.Count -ge 3) `
        "Full hang uses parallel comb-like leg tips: $($hangLegBottoms -join ',')."
    Assert-True ((($hangLegBottoms | Measure-Object -Maximum).Maximum - ($hangLegBottoms | Measure-Object -Minimum).Minimum) -le 4) `
        "Full hang leg-tip variation is unstable: $($hangLegBottoms -join ',')."

    $legTimeline = @($entryNames[2..6] + $settleNames[1..2])
    for ($i = 1; $i -lt $legTimeline.Count; $i++)
    {
        $before = Get-LegBottoms $bitmapByName[$legTimeline[$i-1]]
        $after = Get-LegBottoms $bitmapByName[$legTimeline[$i]]
        for ($leg = 0; $leg -lt 4; $leg++)
        {
            Assert-True ([Math]::Abs($after[$leg] - $before[$leg]) -le 3) `
                "Leg $($leg+1) jumps between $($legTimeline[$i-1]) and $($legTimeline[$i]): $($before[$leg]) to $($after[$leg])."
        }
    }

    Assert-Equal (Get-FileHash $canonicalPath -Algorithm SHA256).Hash `
        (Get-FileHash (Join-Path $assetRoot $settleNames[-1]) -Algorithm SHA256).Hash `
        'Final settle key does not return exactly to the canonical wakeful frame.'

    Write-Output 'DIRECT INTERACTION ASSETS PASS: 12 complete 96x96 transparent body-drag keys, fixed protected identity, continuous extension/recovery, four separated hang legs, no tail protrusion, clean alpha, source/runtime parity, and exact canonical recovery passed.'
}
finally
{
    foreach ($bitmap in $bitmaps) { $bitmap.Dispose() }
    $canonical.Dispose()
}
