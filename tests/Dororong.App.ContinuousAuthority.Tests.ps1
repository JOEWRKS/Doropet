Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -ne $Actual)
    { throw "$Message Expected '$Expected', observed '$Actual'." }
}

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition)
    { throw $Message }
}

function Assert-Near([double]$Expected, [double]$Actual, [double]$Tolerance, [string]$Message)
{
    if ([Math]::Abs($Expected - $Actual) -gt $Tolerance)
    { throw "$Message Expected '$Expected' +/- '$Tolerance', observed '$Actual'." }
}

function Assert-Point([object]$Point, [string]$Label)
{
    Assert-True ($null -ne $Point) "$Label is missing."
    $coordinates = @($Point)
    Assert-Equal 2 $coordinates.Count "$Label is not an X/Y pair."
    Assert-True ($coordinates[0] -is [int] -and $coordinates[1] -is [int]) `
        "$Label contains a non-integer coordinate."
}

function Assert-ReferencePoint(
    [hashtable]$Entry,
    [string]$Field,
    [Drawing.Bitmap]$Bitmap,
    [string]$Label)
{
    if (-not $Entry.ContainsKey($Field))
    { throw "$Label is missing $Field." }

    $point = $Entry[$Field]
    Assert-Point $point "$Label $Field"
    $x = [int]$point[0]
    $y = [int]$point[1]
    Assert-True ($x -ge 0 -and $x -lt $Bitmap.Width -and $y -ge 0 -and $y -lt $Bitmap.Height) `
        "$Label $Field coordinate ($x,$y) is outside the reference bitmap."
    Assert-Equal 255 $Bitmap.GetPixel($x,$y).A "$Label $Field coordinate ($x,$y) is not opaque."
}

function Assert-Normal(
    [hashtable]$Entry,
    [string]$Field,
    [Drawing.Bitmap]$Bitmap,
    [string]$Label)
{
    if (-not $Entry.ContainsKey($Field))
    { throw "$Label is missing $Field." }

    $normal = $Entry[$Field]
    Assert-True ($normal -is [hashtable]) "$Label $Field is not a literal hashtable."
    foreach ($coordinateName in @('X1Eighth','Y1Eighth','X2Eighth','Y2Eighth'))
    {
        Assert-True $normal.ContainsKey($coordinateName) "$Label $Field is missing $coordinateName."
        Assert-True ($normal[$coordinateName] -is [int]) `
            "$Label $Field $coordinateName is not an integer."
    }

    Assert-True (
        $normal.X1Eighth -ne $normal.X2Eighth -or $normal.Y1Eighth -ne $normal.Y2Eighth) `
        "$Label $Field has duplicate endpoints."

    $endpoints = @(
        [pscustomobject]@{ X=$normal.X1Eighth/8.0; Y=$normal.Y1Eighth/8.0; Name='first' }
        [pscustomobject]@{ X=$normal.X2Eighth/8.0; Y=$normal.Y2Eighth/8.0; Name='second' })
    foreach ($endpoint in $endpoints)
    {
        Assert-True (
            $endpoint.X -ge 0.0 -and $endpoint.X -lt $Bitmap.Width -and `
            $endpoint.Y -ge 0.0 -and $endpoint.Y -lt $Bitmap.Height) `
            "$Label $Field $($endpoint.Name) endpoint ($($endpoint.X),$($endpoint.Y)) is out of bounds."
    }
}

function Assert-PointEqual([object]$Expected, [object]$Actual, [string]$Message)
{
    Assert-Point $Expected "$Message expected point"
    Assert-Point $Actual "$Message actual point"
    Assert-Equal ([int]$Expected[0]) ([int]$Actual[0]) "$Message X changed."
    Assert-Equal ([int]$Expected[1]) ([int]$Actual[1]) "$Message Y changed."
}

function Get-Rec709Luminance([Drawing.Color]$Color)
{
    return (0.2126*$Color.R)+(0.7152*$Color.G)+(0.0722*$Color.B)
}

function Get-SelectedOpaquePoint(
    [Drawing.Bitmap]$Bitmap,
    [object]$Points,
    [bool]$Brightest,
    [string]$Label)
{
    $opaque = @()
    foreach ($point in @($Points))
    {
        $pixel = $Bitmap.GetPixel([int]$point[0],[int]$point[1])
        if ($pixel.A -ne 255)
        { continue }
        $opaque += [pscustomobject]@{
            X = [int]$point[0]
            Y = [int]$point[1]
            Luminance = Get-Rec709Luminance $pixel
        }
    }
    Assert-True ($opaque.Count -gt 0) "$Label has no opaque legacy native sample."
    if ($Brightest)
    { $selected = $opaque | Sort-Object @{Expression='Luminance';Descending=$true},Y,X | Select-Object -First 1 }
    else
    { $selected = $opaque | Sort-Object @{Expression='Luminance';Descending=$false},Y,X | Select-Object -First 1 }
    return @([int]$selected.X,[int]$selected.Y)
}

function Get-ContinuousProfile(
    [Drawing.Bitmap]$Bitmap,
    [hashtable]$Normal,
    [Drawing.Color]$Fill,
    [Drawing.Color]$Ink,
    [double]$Spacing = 0.125)
{
    $x1=$Normal.X1Eighth/8.0; $y1=$Normal.Y1Eighth/8.0
    $x2=$Normal.X2Eighth/8.0; $y2=$Normal.Y2Eighth/8.0
    $length=[Math]::Sqrt(($x2-$x1)*($x2-$x1)+($y2-$y1)*($y2-$y1))
    Assert-True ($length -gt 0.0) 'Cannot profile a zero-length continuous normal.'
    $fillL=Get-Rec709Luminance $Fill
    $inkL=Get-Rec709Luminance $Ink
    Assert-True ($fillL -gt $inkL) 'Cannot profile a normal whose fill is not brighter than its ink.'

    $positions=[Collections.Generic.List[double]]::new()
    for ($distance=0.0; $distance -lt $length; $distance += $Spacing)
    { $positions.Add($distance) }
    $positions.Add($length)
    $profile = @()
    foreach ($distance in $positions)
    {
        $t=$distance/$length
        $sample=Get-DororongBilinearPremultipliedSample $Bitmap `
            ($x1+(($x2-$x1)*$t)) ($y1+(($y2-$y1)*$t))
        $luminance=255.0*((0.2126*$sample.R)+(0.7152*$sample.G)+(0.0722*$sample.B))
        $darkness=[Math]::Clamp(($fillL-$luminance)/($fillL-$inkL),0.0,1.0)
        $profile += [pscustomobject]@{
            Distance = $distance
            Alpha = $sample.A
            Luminance = $luminance
            Darkness = $sample.A*$darkness
        }
    }
    return $profile
}

function Get-DarknessRunCount([object]$Profile, [double]$Threshold)
{
    $runCount = 0
    $insideRun = $false
    foreach ($sample in @($Profile))
    {
        if ($sample.Darkness -ge $Threshold)
        {
            if (-not $insideRun)
            { $runCount++ }
            $insideRun = $true
        }
        else
        { $insideRun = $false }
    }
    return $runCount
}

function Test-NormalIntersectsMask(
    [Drawing.Bitmap]$Mask,
    [hashtable]$Normal,
    [double]$Spacing = 0.125)
{
    $x1=$Normal.X1Eighth/8.0; $y1=$Normal.Y1Eighth/8.0
    $x2=$Normal.X2Eighth/8.0; $y2=$Normal.Y2Eighth/8.0
    $length=[Math]::Sqrt(($x2-$x1)*($x2-$x1)+($y2-$y1)*($y2-$y1))
    for ($distance=0.0; $distance -lt $length; $distance += $Spacing)
    {
        $t=$distance/$length
        $sample=Get-DororongBilinearPremultipliedSample $Mask `
            ($x1+(($x2-$x1)*$t)) ($y1+(($y2-$y1)*$t))
        if ($sample.R -gt 0.0)
        { return $true }
    }
    $last=Get-DororongBilinearPremultipliedSample $Mask $x2 $y2
    return $last.R -gt 0.0
}

function Test-NormalIntersectsAlpha(
    [Drawing.Bitmap]$Bitmap,
    [hashtable]$Normal,
    [double]$Spacing = 0.125)
{
    $x1=$Normal.X1Eighth/8.0; $y1=$Normal.Y1Eighth/8.0
    $x2=$Normal.X2Eighth/8.0; $y2=$Normal.Y2Eighth/8.0
    $length=[Math]::Sqrt(($x2-$x1)*($x2-$x1)+($y2-$y1)*($y2-$y1))
    for ($distance=0.0; $distance -lt $length; $distance += $Spacing)
    {
        $t=$distance/$length
        $sample=Get-DororongBilinearPremultipliedSample $Bitmap `
            ($x1+(($x2-$x1)*$t)) ($y1+(($y2-$y1)*$t))
        if ($sample.A -gt 0.0)
        { return $true }
    }
    $last=Get-DororongBilinearPremultipliedSample $Bitmap $x2 $y2
    return $last.A -gt 0.0
}

function Replace-FirstLiteral([string]$Text, [string]$Old, [string]$New, [string]$Label)
{
    $index = $Text.IndexOf($Old, [StringComparison]::Ordinal)
    Assert-True ($index -ge 0) "$Label mutation target was not found."
    return $Text.Substring(0,$index) + $New + $Text.Substring($index+$Old.Length)
}

function Invoke-AuthorityMutationFailure(
    [string]$FixtureText,
    [string]$ExpectedError,
    [string]$Label,
    [string]$ToolPath,
    [string]$SourcePath,
    [string]$MaskPath,
    [string]$NativePath,
    [string]$TemporaryRoot)
{
    $authorityCopy = Join-Path $TemporaryRoot "$Label.psd1"
    [IO.File]::WriteAllText($authorityCopy,$FixtureText,[Text.UTF8Encoding]::new($false))
    $caught = $null
    try
    {
        & $ToolPath -SourcePath $SourcePath -MaskPath $MaskPath -NativePath $NativePath `
            -AuthorityPath $authorityCopy -EvidenceDirectory (Join-Path $TemporaryRoot "$Label-evidence") | Out-Null
    }
    catch
    { $caught = $_.Exception.Message }
    Assert-True ($null -ne $caught) "$Label mutation was accepted."
    Assert-True $caught.Contains($ExpectedError) `
        "$Label mutation failed without named error '$ExpectedError'. Observed '$caught'."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical-source.png'
$nativePath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical.png'
$maskPath = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-body-region-mask.png'
$authorityPath = Join-Path $repositoryRoot 'tests/fixtures/dororong-body-outline-authority.psd1'
$supportPath = Join-Path $repositoryRoot 'tests/support/Dororong.ContinuousOptics.ps1'
$toolPath = Join-Path $repositoryRoot 'tools/New-ContinuousOutlineAuthority.ps1'

Assert-Equal 'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash 'Canonical source changed.'
Assert-Equal 'E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $maskPath).Hash 'Reviewed body-region mask changed.'
Assert-Equal '611A1367E92C37659CF63A549656BCE01EEDEF5DE3CA348C6FADFB98A5D88DC3' `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $nativePath).Hash 'Committed native-open authority changed.'
Assert-True (Test-Path -LiteralPath $authorityPath) 'Continuous authority fixture is missing.'
Assert-True (Test-Path -LiteralPath $supportPath) 'Continuous optics test helper is missing.'

Add-Type -AssemblyName System.Drawing
. $supportPath

$synthetic = [Drawing.Bitmap]::new(2,2,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
try
{
    $synthetic.SetPixel(0,0,[Drawing.Color]::FromArgb(255,255,0,0))
    $synthetic.SetPixel(1,0,[Drawing.Color]::FromArgb(128,0,255,0))
    $synthetic.SetPixel(0,1,[Drawing.Color]::FromArgb(0,255,255,255))
    $synthetic.SetPixel(1,1,[Drawing.Color]::FromArgb(255,0,0,255))
    $sample=Get-DororongBilinearPremultipliedSample $synthetic 0.5 0.5
    Assert-Near 0.625490196078431 $sample.A 0.000000000001 `
        'Bilinear sampler did not interpolate alpha.'
    Assert-Near 0.399686520376176 $sample.R 0.000000000001 `
        'Bilinear sampler did not unpremultiply red.'
    Assert-Near 0.200626959247649 $sample.G 0.000000000001 `
        'Bilinear sampler did not unpremultiply green.'
    Assert-Near 0.399686520376176 $sample.B 0.000000000001 `
        'Bilinear sampler did not unpremultiply blue.'
    $outside=Get-DororongBilinearPremultipliedSample $synthetic -2.0 -2.0
    Assert-Near 0.0 $outside.A 0.0 'Outside sample alpha is not transparent.'
    Assert-Near 0.0 $outside.R 0.0 'Outside sample red is not black.'
    Assert-Near 0.0 $outside.G 0.0 'Outside sample green is not black.'
    Assert-Near 0.0 $outside.B 0.0 'Outside sample blue is not black.'
}
finally
{ $synthetic.Dispose() }

$integration = [Drawing.Bitmap]::new(2,1,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
try
{
    $integration.SetPixel(0,0,[Drawing.Color]::FromArgb(255,0,0,0))
    $integration.SetPixel(1,0,[Drawing.Color]::FromArgb(255,255,255,255))
    $normal=@{X1Eighth=0;Y1Eighth=0;X2Eighth=8;Y2Eighth=0}
    $coverage=Measure-DororongContinuousCoverage $integration $normal `
        ([Drawing.Color]::White) ([Drawing.Color]::Black) 0.6
    Assert-Near 0.5 $coverage 0.000000000001 `
        'Continuous coverage did not trapezoid-integrate the final shorter interval.'
}
finally
{ $integration.Dispose() }

$authority = Import-PowerShellDataFile -LiteralPath $authorityPath
$source = [Drawing.Bitmap]::new($sourcePath)
$native = [Drawing.Bitmap]::new($nativePath)
$mask = [Drawing.Bitmap]::new($maskPath)
try
{
    Assert-Equal 225 $source.Width 'Canonical source width changed.'
    Assert-Equal 225 $source.Height 'Canonical source height changed.'
    Assert-Equal 96 $native.Width 'Committed native-open width changed.'
    Assert-Equal 96 $native.Height 'Committed native-open height changed.'
    Assert-Equal 225 $mask.Width 'Reviewed mask width changed.'
    Assert-Equal 225 $mask.Height 'Reviewed mask height changed.'

    $hairAnchors=@($authority.HairAnchors)
    $bodyNormals=@($authority.BodyNormals)
    Assert-Equal 6 $hairAnchors.Count 'Hair anchor count changed.'
    Assert-Equal 15 $bodyNormals.Count 'Body normal count changed.'

    foreach ($kind in @('Straight','Diagonal','Curve'))
    {
        Assert-True (@($hairAnchors | Where-Object Kind -eq $kind).Count -ge 2) `
            "Hair anchor category '$kind' has fewer than two entries."
    }

    $expectedBodyNames=@(
        'FrontOuter','FrontFoot','FrontInner','FirstValley','FirstUnderside',
        'CenterOuter','CenterFoot','CenterInner','SecondValley','SecondUnderside',
        'RearOuter','RearFoot','RearInner','UpperRearRim','LowerRearRim') | Sort-Object
    $actualBodyNames=@($bodyNormals.Name) | Sort-Object
    Assert-Equal ($expectedBodyNames -join ',') ($actualBodyNames -join ',') `
        'The fifteen named body normals changed.'

    foreach ($entry in $hairAnchors)
    {
        $label="Hair anchor '$($entry.Name)'"
        Assert-Normal $entry 'SourceNormal' $source $label
        Assert-Normal $entry 'NativeNormal' $native $label
        Assert-ReferencePoint $entry 'SourceFill' $source $label
        Assert-ReferencePoint $entry 'SourceInk' $source $label
        Assert-ReferencePoint $entry 'NativeFill' $native $label
        Assert-ReferencePoint $entry 'NativeInk' $native $label

        Assert-PointEqual $entry.Fill $entry.SourceFill "$label SourceFill provenance"
        Assert-PointEqual $entry.Ink $entry.SourceInk "$label SourceInk provenance"
        Assert-PointEqual (Get-SelectedOpaquePoint $native $entry.NativeSamples $true $label) `
            $entry.NativeFill "$label NativeFill selection"
        Assert-PointEqual (Get-SelectedOpaquePoint $native $entry.NativeSamples $false $label) `
            $entry.NativeInk "$label NativeInk selection"

        $sourceFill=$source.GetPixel([int]$entry.SourceFill[0],[int]$entry.SourceFill[1])
        $sourceInk=$source.GetPixel([int]$entry.SourceInk[0],[int]$entry.SourceInk[1])
        $nativeFill=$native.GetPixel([int]$entry.NativeFill[0],[int]$entry.NativeFill[1])
        $nativeInk=$native.GetPixel([int]$entry.NativeInk[0],[int]$entry.NativeInk[1])
        $surfaces = @(
            [pscustomobject]@{
                Name='source'; Bitmap=$source; Normal=$entry.SourceNormal; Fill=$sourceFill; Ink=$sourceInk }
            [pscustomobject]@{
                Name='native'; Bitmap=$native; Normal=$entry.NativeNormal; Fill=$nativeFill; Ink=$nativeInk })
        foreach ($surface in $surfaces)
        {
            $profile=@(Get-ContinuousProfile `
                $surface.Bitmap $surface.Normal $surface.Fill $surface.Ink)
            Assert-Equal 1 (Get-DarknessRunCount $profile 0.10) `
                "$label $($surface.Name) normal does not contain exactly one darkness run at threshold 0.10."
            Assert-True ($profile[0].Darkness -lt 0.10) `
                "$label $($surface.Name) first endpoint is not near-zero darkness."
            Assert-True ($profile[-1].Darkness -lt 0.10) `
                "$label $($surface.Name) second endpoint is not near-zero darkness."
            Assert-True ((Measure-DororongContinuousCoverage `
                $surface.Bitmap $surface.Normal $surface.Fill $surface.Ink) -gt 0.0) `
                "$label $($surface.Name) continuous integral is not positive."
        }
    }

    $bodyInk=[Drawing.Color]::FromArgb(255,0,0,0)
    foreach ($entry in $bodyNormals)
    {
        $label="Body normal '$($entry.Name)'"
        Assert-Normal $entry 'SourceNormal' $source $label
        Assert-Normal $entry 'NativeNormal' $native $label
        Assert-ReferencePoint $entry 'SourceFill' $source $label
        Assert-ReferencePoint $entry 'NativeFill' $native $label
        Assert-PointEqual $entry.Fill $entry.SourceFill "$label SourceFill provenance"
        Assert-PointEqual (Get-SelectedOpaquePoint $native $entry.NativeSamples $true $label) `
            $entry.NativeFill "$label NativeFill selection"

        $sourceFill=$source.GetPixel([int]$entry.SourceFill[0],[int]$entry.SourceFill[1])
        $profile=@(Get-ContinuousProfile $source $entry.SourceNormal $sourceFill $bodyInk)
        Assert-Equal 1 (Get-DarknessRunCount $profile 0.10) `
            "$label source normal does not contain exactly one original-body-ink run."
        $hasNearWhiteInterval=$false
        for ($index=0; $index -lt $profile.Count-1; $index++)
        {
            if ($profile[$index].Alpha -ge 0.999 -and $profile[$index+1].Alpha -ge 0.999 -and `
                $profile[$index].Luminance -ge 245.0 -and $profile[$index+1].Luminance -ge 245.0)
            { $hasNearWhiteInterval=$true; break }
        }
        Assert-True $hasNearWhiteInterval `
            "$label source normal has no near-white fill-side interval."
        Assert-True (Test-NormalIntersectsMask $mask $entry.SourceNormal) `
            "$label source normal does not intersect the approved body mask."
        Assert-True ((Measure-DororongContinuousCoverage `
            $source $entry.SourceNormal $sourceFill $bodyInk) -gt 0.0) `
            "$label source continuous integral is not positive."

        Assert-True (Test-NormalIntersectsAlpha $native $entry.NativeNormal) `
            "$label native location normal does not intersect committed body alpha."
    }

    foreach ($point in @($authority.ProtectedPoints))
    {
        Assert-Equal 0 $mask.GetPixel([int]$point.X,[int]$point.Y).R `
            "Protected point '$($point.Name)' is writable in the approved mask."
    }
}
finally
{
    $mask.Dispose()
    $native.Dispose()
    $source.Dispose()
}

$productionFiles=@(
    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Recurse -File |
        Where-Object Extension -in @('.cs','.ps1')
    Get-Item -LiteralPath (Join-Path $repositoryRoot 'tools/Generate-CanonicalArt.ps1'))
foreach ($file in $productionFiles)
{
    $text=[IO.File]::ReadAllText($file.FullName)
    Assert-True (-not $text.Contains('dororong-body-outline-authority.psd1')) `
        "Production generator/module '$($file.FullName)' imports the authority fixture."
    Assert-True (-not $text.Contains('Dororong.ContinuousOptics.ps1')) `
        "Production generator/module '$($file.FullName)' imports the continuous test helper."
    Assert-True (-not $text.Contains('Measure-DororongContinuousCoverage')) `
        "Production generator/module '$($file.FullName)' references test-only continuous coverage."
}

Assert-True (Test-Path -LiteralPath $toolPath) 'Continuous authority overlay tool is missing.'
$fixtureText=[IO.File]::ReadAllText($authorityPath)
$firstAnchor=@($authority.HairAnchors)[0]
$normal=$firstAnchor.SourceNormal
$normalLiteral="            SourceNormal = @{ X1Eighth = $($normal.X1Eighth); Y1Eighth = $($normal.Y1Eighth); X2Eighth = $($normal.X2Eighth); Y2Eighth = $($normal.Y2Eighth) }"
$movedLiteral="            SourceNormal = @{ X1Eighth = $($normal.X1Eighth+1); Y1Eighth = $($normal.Y1Eighth); X2Eighth = $($normal.X2Eighth); Y2Eighth = $($normal.Y2Eighth) }"
$duplicateLiteral="            SourceNormal = @{ X1Eighth = $($normal.X1Eighth); Y1Eighth = $($normal.Y1Eighth); X2Eighth = $($normal.X1Eighth); Y2Eighth = $($normal.Y1Eighth) }"
$fillLiteral="            SourceFill = @($($firstAnchor.SourceFill[0]), $($firstAnchor.SourceFill[1]))"
$inkLiteral="            SourceInk = @($($firstAnchor.SourceInk[0]), $($firstAnchor.SourceInk[1]))"
$swappedFillLiteral="            SourceFill = @($($firstAnchor.SourceInk[0]), $($firstAnchor.SourceInk[1]))"
$swappedInkLiteral="            SourceInk = @($($firstAnchor.SourceFill[0]), $($firstAnchor.SourceFill[1]))"
$temporaryRoot=Join-Path ([IO.Path]::GetTempPath()) ("dororong-continuous-authority-"+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
try
{
    $movedText=Replace-FirstLiteral $fixtureText $normalLiteral $movedLiteral 'Moved endpoint'
    Invoke-AuthorityMutationFailure $movedText 'Continuous authority hash changed.' 'moved-endpoint' `
        $toolPath $sourcePath $maskPath $nativePath $temporaryRoot

    $swappedText=Replace-FirstLiteral $fixtureText $fillLiteral $swappedFillLiteral 'Swapped fill'
    $swappedText=Replace-FirstLiteral $swappedText $inkLiteral $swappedInkLiteral 'Swapped ink'
    Invoke-AuthorityMutationFailure $swappedText 'fill luminance must exceed ink luminance.' 'swapped-fill-ink' `
        $toolPath $sourcePath $maskPath $nativePath $temporaryRoot

    $duplicateText=Replace-FirstLiteral $fixtureText $normalLiteral $duplicateLiteral 'Duplicate endpoint'
    Invoke-AuthorityMutationFailure $duplicateText 'has duplicate endpoints.' 'duplicate-endpoint' `
        $toolPath $sourcePath $maskPath $nativePath $temporaryRoot
}
finally
{
    if ([IO.Directory]::Exists($temporaryRoot))
    { [IO.Directory]::Delete($temporaryRoot,$true) }
}

$expectedAuthorityHash='D7F947518118805862404EE47459E9533D2DCCBB98B841C8B106F3EE7694D639'
$authorityHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $authorityPath).Hash
Assert-Equal $expectedAuthorityHash $authorityHash 'Continuous authority fixture changed.'
Write-Output "CONTINUOUS AUTHORITY PASS hash=$authorityHash hair=6 body=15"
