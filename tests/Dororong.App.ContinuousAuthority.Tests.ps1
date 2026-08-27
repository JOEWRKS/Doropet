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

function Test-DororongNeutralContourPixel([Drawing.Color]$Color)
{
    $minimum=[Math]::Min($Color.R,[Math]::Min($Color.G,$Color.B))
    $maximum=[Math]::Max($Color.R,[Math]::Max($Color.G,$Color.B))
    return $Color.A -gt 0 -and ($maximum-$minimum) -le 8
}

function Get-DororongApprovedSourceContour(
    [Drawing.Bitmap]$Source,
    [Drawing.Bitmap]$Mask)
{
    Assert-Equal $Source.Width $Mask.Width `
        'Source/mask width differs during independent contour derivation.'
    Assert-Equal $Source.Height $Mask.Height `
        'Source/mask height differs during independent contour derivation.'

    $segments=[Collections.Generic.List[object]]::new()
    $directions=@(
        [pscustomobject]@{ DX=-1; DY=0; Side='Left' }
        [pscustomobject]@{ DX=1; DY=0; Side='Right' }
        [pscustomobject]@{ DX=0; DY=-1; Side='Top' }
        [pscustomobject]@{ DX=0; DY=1; Side='Bottom' })

    for($y=0;$y -lt $Mask.Height;$y++)
    {
        for($x=0;$x -lt $Mask.Width;$x++)
        {
            $maskValue=$Mask.GetPixel($x,$y).R
            Assert-True ($maskValue -eq 0 -or $maskValue -eq 255) `
                "Reviewed mask contains intermediate value '$maskValue' at ($x,$y)."
            if($maskValue -ne 255)
            { continue }

            $insideColor=$Source.GetPixel($x,$y)
            foreach($direction in $directions)
            {
                $neighborX=$x+$direction.DX
                $neighborY=$y+$direction.DY
                if($neighborX -lt 0 -or $neighborY -lt 0 -or `
                    $neighborX -ge $Mask.Width -or $neighborY -ge $Mask.Height)
                { continue }

                $neighborMaskValue=$Mask.GetPixel($neighborX,$neighborY).R
                if($neighborMaskValue -eq 255)
                { continue }
                Assert-Equal 0 $neighborMaskValue `
                    "Reviewed mask contains an intermediate neighbor at ($neighborX,$neighborY)."

                $outsideColor=$Source.GetPixel($neighborX,$neighborY)
                if(-not(Test-DororongNeutralContourPixel $insideColor) -or `
                    -not(Test-DororongNeutralContourPixel $outsideColor))
                { continue }

                switch($direction.Side)
                {
                    'Left' {
                        $x1=$x-0.5; $y1=$y-0.5; $x2=$x-0.5; $y2=$y+0.5
                    }
                    'Right' {
                        $x1=$x+0.5; $y1=$y-0.5; $x2=$x+0.5; $y2=$y+0.5
                    }
                    'Top' {
                        $x1=$x-0.5; $y1=$y-0.5; $x2=$x+0.5; $y2=$y-0.5
                    }
                    'Bottom' {
                        $x1=$x-0.5; $y1=$y+0.5; $x2=$x+0.5; $y2=$y+0.5
                    }
                }
                $segments.Add([pscustomobject]@{
                    X1=[double]$x1; Y1=[double]$y1
                    X2=[double]$x2; Y2=[double]$y2
                    Kind='Exposed'
                })
            }
        }
    }

    $legalEndpoints=@(
        [pscustomobject]@{ Name='FrontOcclusion'; X=112.0; Y=151.0 }
        [pscustomobject]@{ Name='RearOcclusion'; X=157.0; Y=116.0 })
    foreach($endpoint in $legalEndpoints)
    {
        Assert-Equal 0 $Mask.GetPixel([int]$endpoint.X,[int]$endpoint.Y).R `
            "Legal endpoint '$($endpoint.Name)' moved into the writable mask."
        Assert-Equal 255 $Source.GetPixel([int]$endpoint.X,[int]$endpoint.Y).A `
            "Legal endpoint '$($endpoint.Name)' is no longer source-opaque."

        $nearest=$null
        $nearestDistance=[double]::PositiveInfinity
        foreach($segment in @($segments))
        {
            foreach($vertex in @(
                [pscustomobject]@{ X=$segment.X1; Y=$segment.Y1 }
                [pscustomobject]@{ X=$segment.X2; Y=$segment.Y2 }))
            {
                $distance=(($vertex.X-$endpoint.X)*($vertex.X-$endpoint.X))+`
                    (($vertex.Y-$endpoint.Y)*($vertex.Y-$endpoint.Y))
                $isOrdinallyEarlier=$null -eq $nearest -or `
                    $vertex.Y -lt $nearest.Y -or `
                    ($vertex.Y -eq $nearest.Y -and $vertex.X -lt $nearest.X)
                if($distance -lt ($nearestDistance-0.000000001) -or `
                    ([Math]::Abs($distance-$nearestDistance) -le 0.000000001 -and `
                    $isOrdinallyEarlier))
                {
                    $nearest=$vertex
                    $nearestDistance=$distance
                }
            }
        }
        Assert-True ($null -ne $nearest) `
            "Legal endpoint '$($endpoint.Name)' has no exposed contour vertex."
        $segments.Add([pscustomobject]@{
            X1=[double]$nearest.X; Y1=[double]$nearest.Y
            X2=[double]$endpoint.X; Y2=[double]$endpoint.Y
            Kind='LegalContinuation'
        })
    }
    return @($segments)
}

function Get-DororongNamedContourSegments(
    [object]$ApprovedContour,
    [string]$Name)
{
    $windows=@{
        FrontOuter=@{ MinX=36.5; MaxX=39.5; MinY=160.5; MaxY=171.5 }
        FrontFoot=@{ MinX=48.5; MaxX=62.5; MinY=189.5; MaxY=193.5 }
        FrontInner=@{ MinX=62.5; MaxX=65.5; MinY=176.5; MaxY=186.5 }
        FirstValley=@{ MinX=66.5; MaxX=73.5; MinY=176.5; MaxY=181.5 }
        FirstUnderside=@{ MinX=72.5; MaxX=79.5; MinY=178.5; MaxY=184.5 }
        CenterOuter=@{ MinX=79.5; MaxX=83.5; MinY=181.5; MaxY=191.5 }
        CenterFoot=@{ MinX=96.5; MaxX=110.5; MinY=203.5; MaxY=206.5 }
        CenterInner=@{ MinX=112.5; MaxX=116.5; MinY=183.5; MaxY=191.5 }
        SecondValley=@{ MinX=138.5; MaxX=145.5; MinY=171.5; MaxY=178.5 }
        SecondUnderside=@{ MinX=123.5; MaxX=129.5; MinY=176.5; MaxY=181.5 }
        RearOuter=@{ MinX=164.5; MaxX=168.5; MinY=174.5; MaxY=185.5 }
        RearFoot=@{ MinX=149.5; MaxX=161.5; MinY=196.5; MaxY=200.5 }
        RearInner=@{ MinX=137.5; MaxX=141.5; MinY=176.5; MaxY=184.5 }
        UpperRearRim=@{ MinX=174.5; MaxX=178.5; MinY=122.5; MaxY=135.5 }
        LowerRearRim=@{ MinX=174.5; MaxX=178.5; MinY=148.5; MaxY=160.5 }
    }
    Assert-True $windows.ContainsKey($Name) `
        "Body normal '$Name' has no independently reviewed contour window."
    $window=$windows[$Name]
    return @($ApprovedContour | Where-Object {
        $midX=($_.X1+$_.X2)/2.0
        $midY=($_.Y1+$_.Y2)/2.0
        $midX -ge $window.MinX -and $midX -le $window.MaxX -and `
        $midY -ge $window.MinY -and $midY -le $window.MaxY
    })
}

function Get-DororongUniqueSegmentIntersections(
    [hashtable]$Normal,
    [object]$Segments)
{
    $px=$Normal.X1Eighth/8.0; $py=$Normal.Y1Eighth/8.0
    $rx=($Normal.X2Eighth-$Normal.X1Eighth)/8.0
    $ry=($Normal.Y2Eighth-$Normal.Y1Eighth)/8.0
    $epsilon=0.000000001
    $unique=@{}

    foreach($segment in @($Segments))
    {
        $qx=[double]$segment.X1; $qy=[double]$segment.Y1
        $sx=[double]$segment.X2-$qx; $sy=[double]$segment.Y2-$qy
        $crossRS=($rx*$sy)-($ry*$sx)
        $qpx=$qx-$px; $qpy=$qy-$py
        $crossQPR=($qpx*$ry)-($qpy*$rx)

        $points=@()
        if([Math]::Abs($crossRS) -le $epsilon)
        {
            if([Math]::Abs($crossQPR) -gt $epsilon)
            { continue }
            $normalLengthSquared=($rx*$rx)+($ry*$ry)
            $t1=(($qpx*$rx)+($qpy*$ry))/$normalLengthSquared
            $q2px=([double]$segment.X2)-$px
            $q2py=([double]$segment.Y2)-$py
            $t2=(($q2px*$rx)+($q2py*$ry))/$normalLengthSquared
            $overlapStart=[Math]::Max(0.0,[Math]::Min($t1,$t2))
            $overlapEnd=[Math]::Min(1.0,[Math]::Max($t1,$t2))
            if($overlapStart -gt ($overlapEnd+$epsilon))
            { continue }
            $points=@($overlapStart,$overlapEnd)
        }
        else
        {
            $t=(($qpx*$sy)-($qpy*$sx))/$crossRS
            $u=(($qpx*$ry)-($qpy*$rx))/$crossRS
            if($t -lt -$epsilon -or $t -gt (1.0+$epsilon) -or `
                $u -lt -$epsilon -or $u -gt (1.0+$epsilon))
            { continue }
            $points=@([Math]::Clamp($t,0.0,1.0))
        }

        foreach($t in $points)
        {
            $x=$px+($t*$rx); $y=$py+($t*$ry)
            $key=[String]::Format(
                [Globalization.CultureInfo]::InvariantCulture,
                '{0:F9},{1:F9}',$x,$y)
            $unique[$key]=[pscustomobject]@{ X=$x; Y=$y }
        }
    }
    return @($unique.Values)
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

$sharedVertexSegments=@(
    [pscustomobject]@{ X1=0.0; Y1=0.0; X2=1.0; Y2=0.0 }
    [pscustomobject]@{ X1=1.0; Y1=0.0; X2=1.0; Y2=1.0 })
$sharedVertexNormal=@{
    X1Eighth=4; Y1Eighth=-4; X2Eighth=12; Y2Eighth=4 }
Assert-Equal 1 `
    @(Get-DororongUniqueSegmentIntersections `
        $sharedVertexNormal $sharedVertexSegments).Count `
    'A shared stair-step contour vertex was counted as more than one crossing.'

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

    $approvedSourceContour=@(Get-DororongApprovedSourceContour $source $mask)
    Assert-True ($approvedSourceContour.Count -gt 0) `
        'Independent approved source contour derivation produced no segments.'

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

        $namedContour=@(Get-DororongNamedContourSegments `
            $approvedSourceContour $entry.Name)
        $contourIntersections=@(Get-DororongUniqueSegmentIntersections `
            $entry.SourceNormal $namedContour)
        Assert-True ($contourIntersections.Count -gt 0) `
            "$label SourceNormal does not intersect the approved contour."
        Assert-Equal 1 $contourIntersections.Count `
            "$label SourceNormal intersects the approved contour more than once."

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

$expectedAuthorityHash='87D0311B043368E0E21C2FD3E17EE7AC79FE8D217BEF5F231C4B3D1CD341490B'
$authorityHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $authorityPath).Hash
Assert-Equal $expectedAuthorityHash $authorityHash 'Continuous authority fixture changed.'
Write-Output "CONTINUOUS AUTHORITY PASS hash=$authorityHash hair=6 body=15"
