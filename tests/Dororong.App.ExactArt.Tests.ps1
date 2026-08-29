param(
    [string]$Configuration = 'Debug',
    [string]$EyeOnlyOpenPath,
    [string]$EyeOnlyClosedPath,
    [string]$EyeOnlyNativeOpenPath,
    [string]$EyeOnlyNativeClosedPath,
    [string]$EvidenceOnlyDirectory,
    [switch]$FillFloorMutationOnly,
    [switch]$OracleOnly)

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

function Assert-Near([double]$Expected, [double]$Actual, [double]$Tolerance, [string]$Message)
{
    if ([Math]::Abs($Expected - $Actual) -gt $Tolerance)
    { throw "$Message Expected '$Expected' +/- '$Tolerance', observed '$Actual'." }
}

function Assert-Frame([System.Windows.Controls.Image]$Image, [string]$ExpectedFileName, [string]$State)
{
    Assert-True $Image.Source.ToString().EndsWith($ExpectedFileName, [StringComparison]::OrdinalIgnoreCase) `
        "$State did not use $ExpectedFileName."
}

function Assert-BitmapEqual([Drawing.Bitmap]$Expected,[Drawing.Bitmap]$Actual,[string]$Message)
{
    Assert-Equal $Expected.Width $Actual.Width "$Message Width differs."
    Assert-Equal $Expected.Height $Actual.Height "$Message Height differs."
    for ($y = 0; $y -lt $Expected.Height; $y++)
    {
        for ($x = 0; $x -lt $Expected.Width; $x++)
        {
            Assert-Equal $Expected.GetPixel($x,$y).ToArgb() $Actual.GetPixel($x,$y).ToArgb() `
                "$Message Pixel differs at ($x,$y)."
        }
    }
}

function Copy-Bitmap32([Drawing.Bitmap]$Bitmap)
{
    return $Bitmap.Clone(
        [Drawing.Rectangle]::new(0,0,$Bitmap.Width,$Bitmap.Height),
        [Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function Get-Median([int[]]$Values)
{
    $sorted = @($Values | Sort-Object)
    $middle = [int]($sorted.Count / 2)
    if (($sorted.Count % 2) -eq 0)
    {
        return [int][Math]::Round(
            ([double]$sorted[$middle - 1] + [double]$sorted[$middle]) / 2.0,
            0,[MidpointRounding]::ToEven)
    }
    return [int]$sorted[$middle]
}

function Get-OutlineMedianColor([Drawing.Bitmap]$Bitmap,[object[]]$Samples)
{
    $red = [Collections.Generic.List[int]]::new()
    $green = [Collections.Generic.List[int]]::new()
    $blue = [Collections.Generic.List[int]]::new()
    foreach ($sample in $Samples)
    {
        $pixel = $Bitmap.GetPixel([int]$sample[0],[int]$sample[1])
        $red.Add($pixel.R); $green.Add($pixel.G); $blue.Add($pixel.B)
    }
    return [Drawing.Color]::FromArgb(
        255,(Get-Median $red.ToArray()),(Get-Median $green.ToArray()),(Get-Median $blue.ToArray()))
}

function New-DirectCandidate(
    [Drawing.Bitmap]$Source,[Drawing.Bitmap]$Seed,[Drawing.Bitmap]$Mask,
    [hashtable]$Constants,[int]$Factor,[double]$Width)
{
    $endpoints = [Drawing.PointF[]]@($Constants.LegalEndpoints | ForEach-Object {
        [Drawing.PointF]::new([single]$_.X,[single]$_.Y)
    })
    $contour = New-DororongVisibleContour $Source $Mask $endpoints
    $fillField = New-DororongFillField $Source $Seed $Mask $contour
    $distanceMap = New-DororongSubpixelDistanceMap $Mask $contour $Factor
    $outlineColor = Get-OutlineMedianColor $Source @($Constants.OutlineSamples)
    $candidate = Invoke-DororongSubpixelOutline `
        $Source $Mask $fillField $distanceMap $outlineColor $Width
    return [pscustomobject]@{
        Candidate=$candidate; Contour=$contour; FillField=$fillField
        DistanceMap=$distanceMap; OutlineColor=$outlineColor
    }
}

function Assert-SourceCandidateContract(
    [Drawing.Bitmap]$Source,[Drawing.Bitmap]$Mask,[Drawing.Bitmap]$Candidate,[string]$Label,
    [switch]$AllowEyeChanges)
{
    Assert-Equal 225 $Candidate.Width "$Label width changed."
    Assert-Equal 225 $Candidate.Height "$Label height changed."
    for ($y = 0; $y -lt 225; $y++)
    {
        for ($x = 0; $x -lt 225; $x++)
        {
            $sourcePixel = $Source.GetPixel($x,$y)
            $candidatePixel = $Candidate.GetPixel($x,$y)
            Assert-Equal $sourcePixel.A $candidatePixel.A "$Label source alpha changed at ($x,$y)."
            if ($candidatePixel.A -eq 0)
            {
                Assert-Equal 0 ($candidatePixel.R + $candidatePixel.G + $candidatePixel.B) `
                    "$Label alpha-zero RGB hygiene failed at ($x,$y)."
            }
            if ($Mask.GetPixel($x,$y).R -eq 0 -and
                (-not $AllowEyeChanges -or -not (Test-InSourceStateRegion $x $y)))
            {
                Assert-Equal $sourcePixel.ToArgb() $candidatePixel.ToArgb() `
                    "$Label protected mask-zero artwork changed at ($x,$y)."
            }
        }
    }
}

function Assert-AlphaZeroRgb([Drawing.Bitmap]$Bitmap,[string]$Label)
{
    for ($y = 0; $y -lt $Bitmap.Height; $y++)
    {
        for ($x = 0; $x -lt $Bitmap.Width; $x++)
        {
            $pixel = $Bitmap.GetPixel($x,$y)
            if ($pixel.A -eq 0)
            {
                Assert-Equal 0 ($pixel.R + $pixel.G + $pixel.B) `
                    "$Label alpha-zero RGB hygiene failed at ($x,$y)."
            }
        }
    }
}

function Assert-FillSeedContract(
    [Drawing.Bitmap]$Source,[Drawing.Bitmap]$Seed,[Drawing.Color[,]]$FillField,[string]$Label)
{
    Assert-True ($FillField.EligibleSeedCount -ge 8) "$Label has fewer than eight eligible fill seeds."
    foreach ($coordinate in @($FillField.EligibleSeeds))
    {
        $pixel = $Source.GetPixel([int]$coordinate.X,[int]$coordinate.Y)
        Assert-Equal 255 $Seed.GetPixel([int]$coordinate.X,[int]$coordinate.Y).R `
            "$Label admitted newly owned fill seed at ($($coordinate.X),$($coordinate.Y))."
        Assert-Equal 255 $pixel.A "$Label admitted non-opaque fill seed at ($($coordinate.X),$($coordinate.Y))."
        Assert-True ($pixel.R -ge 225 -and $pixel.G -ge 225 -and $pixel.B -ge 225) `
            "$Label admitted dark fill seed at ($($coordinate.X),$($coordinate.Y)) RGB=$($pixel.R),$($pixel.G),$($pixel.B)."
        $minimum = [Math]::Min($pixel.R,[Math]::Min($pixel.G,$pixel.B))
        $maximum = [Math]::Max($pixel.R,[Math]::Max($pixel.G,$pixel.B))
        Assert-True (($maximum - $minimum) -le 8) `
            "$Label admitted chromatic fill seed at ($($coordinate.X),$($coordinate.Y))."
    }
}

function Get-IndependentPointSegmentDistanceSquared(
    [double]$X,[double]$Y,[object]$Segment)
{
    $dx=[double]$Segment.X2-[double]$Segment.X1
    $dy=[double]$Segment.Y2-[double]$Segment.Y1
    $lengthSquared=($dx*$dx)+($dy*$dy)
    if($lengthSquared-eq0.0)
    {
        $pointDx=$X-[double]$Segment.X1;$pointDy=$Y-[double]$Segment.Y1
        return ($pointDx*$pointDx)+($pointDy*$pointDy)
    }
    $t=((($X-[double]$Segment.X1)*$dx)+(($Y-[double]$Segment.Y1)*$dy))/$lengthSquared
    $t=[Math]::Clamp($t,0.0,1.0)
    $nearestX=[double]$Segment.X1+($t*$dx)
    $nearestY=[double]$Segment.Y1+($t*$dy)
    $resultX=$X-$nearestX;$resultY=$Y-$nearestY
    return ($resultX*$resultX)+($resultY*$resultY)
}

function Get-IndependentEligibleFillSamples(
    [Drawing.Bitmap]$Source,[Drawing.Bitmap]$Seed,[Drawing.Bitmap]$Mask,[object]$Contour)
{
    $segments=@($Contour.Segments)
    Assert-True ($segments.Count-gt0) 'Independent fill eligibility has no frozen E-union-C contour.'
    $samples=[Collections.Generic.List[object]]::new()
    for($y=0;$y-lt$Mask.Height;$y++)
    {
        for($x=0;$x-lt$Mask.Width;$x++)
        {
            if($Mask.GetPixel($x,$y).R-ne255-or$Seed.GetPixel($x,$y).R-ne255){continue}
            $pixel=$Source.GetPixel($x,$y)
            if($pixel.A-ne255-or$pixel.R-lt225-or$pixel.G-lt225-or$pixel.B-lt225){continue}
            $minimum=[Math]::Min($pixel.R,[Math]::Min($pixel.G,$pixel.B))
            $maximum=[Math]::Max($pixel.R,[Math]::Max($pixel.G,$pixel.B))
            if(($maximum-$minimum)-gt8){continue}
            $minimumDistanceSquared=[double]::PositiveInfinity
            foreach($segment in $segments)
            {
                $distanceSquared=Get-IndependentPointSegmentDistanceSquared $x $y $segment
                if($distanceSquared-lt$minimumDistanceSquared)
                {$minimumDistanceSquared=$distanceSquared}
            }
            if([Math]::Sqrt($minimumDistanceSquared)-le8.0){continue}
            $samples.Add([pscustomobject]@{X=$x;Y=$y;Color=$pixel})
        }
    }
    return @($samples)
}

function Get-BitmapPngHash([Drawing.Bitmap]$Bitmap)
{
    $stream=[IO.MemoryStream]::new()
    try
    {
        $Bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
        return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream.ToArray()))
    }
    finally{$stream.Dispose()}
}

function Get-IndependentSmoothColor([int]$X,[int]$Y,[object[]]$Samples)
{
    $nearest=@($Samples|Sort-Object `
        @{Expression={($_.X-$X)*($_.X-$X)+($_.Y-$Y)*($_.Y-$Y)}},Y,X|Select-Object -First 8)
    Assert-Equal 8 $nearest.Count "Independent smooth fill at ($X,$Y) did not select eight samples."
    $weightTotal=0.0;$red=0.0;$green=0.0;$blue=0.0
    foreach($sample in $nearest)
    {
        $d2=($sample.X-$X)*($sample.X-$X)+($sample.Y-$Y)*($sample.Y-$Y)
        $weight=1.0/(1.0+$d2);$weightTotal+=$weight
        $red+=$weight*$sample.Color.R;$green+=$weight*$sample.Color.G;$blue+=$weight*$sample.Color.B
    }
    return [Drawing.Color]::FromArgb(255,
        [int][Math]::Round($red/$weightTotal,0,[MidpointRounding]::ToEven),
        [int][Math]::Round($green/$weightTotal,0,[MidpointRounding]::ToEven),
        [int][Math]::Round($blue/$weightTotal,0,[MidpointRounding]::ToEven))
}

function Assert-AllPixelSmoothFill(
    [Drawing.Bitmap]$Source,[Drawing.Bitmap]$Seed,[Drawing.Bitmap]$Mask,
    [object]$Contour,[Drawing.Color[,]]$FillField)
{
    $samples=@(Get-IndependentEligibleFillSamples $Source $Seed $Mask $Contour)
    Assert-Equal $samples.Count $FillField.EligibleSeedCount `
        'Independent/production eligible sample count changed.'
    Assert-Equal (@($samples|ForEach-Object{"$($_.X),$($_.Y)"})-join'|') `
        (@($FillField.EligibleSeeds|Sort-Object Y,X|ForEach-Object{"$($_.X),$($_.Y)"})-join'|') `
        'Independent/production eligible sample membership changed.'
    $sampleKeys=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($sample in $samples){$null=$sampleKeys.Add("$($sample.X),$($sample.Y)")}
    $changedSampleOutputs=0
    for($y=0;$y-lt$Mask.Height;$y++)
    {
        for($x=0;$x-lt$Mask.Width;$x++)
        {
            if($Mask.GetPixel($x,$y).R-ne255){continue}
            $expected=Get-IndependentSmoothColor $x $y $samples
            Assert-Equal $expected.ToArgb() $FillField[$x,$y].ToArgb() `
                "All-pixel nearest-eight smooth fill differs at ($x,$y)."
            if($sampleKeys.Contains("$x,$y")-and
                $Source.GetPixel($x,$y).ToArgb()-ne$FillField[$x,$y].ToArgb()){$changedSampleOutputs++}
        }
    }
    Assert-True ($changedSampleOutputs-gt0) `
        'Eligible sample coordinates retained exact source RGB instead of using the all-pixel smooth field.'
}

function Assert-ExactGeneratorEvidenceSet([string]$Directory,[Collections.IDictionary]$Hashes)
{
    $contracts=@(
        [pscustomobject]@{Name='native-closed-candidate.png';Width=96;Height=96;Hash=$Hashes.NativeClosed},
        [pscustomobject]@{Name='native-half-closed-candidate.png';Width=96;Height=96;Hash=$Hashes.NativeHalfClosed},
        [pscustomobject]@{Name='native-open-baseline.png';Width=96;Height=96;Hash=$Hashes.NativeOpenBaseline},
        [pscustomobject]@{Name='native-open-candidate.png';Width=96;Height=96;Hash=$Hashes.NativeOpen},
        [pscustomobject]@{Name='source-closed-candidate.png';Width=225;Height=225;Hash=$Hashes.SourceClosed},
        [pscustomobject]@{Name='source-half-closed-candidate.png';Width=225;Height=225;Hash=$Hashes.SourceHalfClosed},
        [pscustomobject]@{Name='source-open-baseline.png';Width=225;Height=225;Hash=$Hashes.SourceOpenBaseline},
        [pscustomobject]@{Name='source-open-candidate.png';Width=225;Height=225;Hash=$Hashes.SourceOpen})
    $expected=@($contracts|ForEach-Object Name|Sort-Object)
    Assert-True (Test-Path -LiteralPath $Directory -PathType Container) `
        "Generator evidence directory is missing: $Directory"
    $observed=@(Get-ChildItem -LiteralPath $Directory -File|Sort-Object Name|ForEach-Object Name)
    Assert-Equal ($expected-join'|') ($observed-join'|') `
        'Generator evidence surface is not exactly the eight contract PNGs.'
    foreach($contract in $contracts)
    {
        $path=Join-Path $Directory $contract.Name
        $bitmap=$null
        try
        {
            $bitmap=[Drawing.Bitmap]::new($path)
            Assert-Equal ([Drawing.Imaging.ImageFormat]::Png.Guid) $bitmap.RawFormat.Guid `
                "Generator evidence '$($contract.Name)' is not a decodable PNG."
            Assert-Equal $contract.Width $bitmap.Width `
                "Generator evidence '$($contract.Name)' width changed."
            Assert-Equal $contract.Height $bitmap.Height `
                "Generator evidence '$($contract.Name)' height changed."
        }
        finally{if($null-ne$bitmap){$bitmap.Dispose()}}
        Assert-Equal $contract.Hash (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash `
            "Generator evidence '$($contract.Name)' identity changed."
    }
}

function Get-IndependentProxyComponents(
    [Drawing.Bitmap]$Mask,[Drawing.Bitmap]$Source,[int]$MaximumSize=7,
    [int]$MaximumChroma=8,[switch]$EightConnected)
{
    $visited=[bool[]]::new($Mask.Width*$Mask.Height)
    $accepted=[Collections.Generic.List[object]]::new()
    $offsets=if($EightConnected){
        @(@(-1,-1),@(0,-1),@(1,-1),@(-1,0),@(1,0),@(-1,1),@(0,1),@(1,1))
    }else{@(@(-1,0),@(1,0),@(0,-1),@(0,1))}
    for($startY=0;$startY-lt$Mask.Height;$startY++)
    {
        for($startX=0;$startX-lt$Mask.Width;$startX++)
        {
            $startIndex=($startY*$Mask.Width)+$startX
            if($visited[$startIndex]-or$Mask.GetPixel($startX,$startY).R-ne0){continue}
            $queue=[Collections.Generic.Queue[int]]::new();$queue.Enqueue($startIndex)
            $visited[$startIndex]=$true;$indices=[Collections.Generic.List[int]]::new()
            $touchesFrame=$false;$maximumObservedChroma=0
            $minX=$startX;$maxX=$startX;$minY=$startY;$maxY=$startY
            while($queue.Count-gt0)
            {
                $index=$queue.Dequeue();$indices.Add($index)
                $x=$index%$Mask.Width;$y=[int][Math]::Floor($index/$Mask.Width)
                $minX=[Math]::Min($minX,$x);$maxX=[Math]::Max($maxX,$x)
                $minY=[Math]::Min($minY,$y);$maxY=[Math]::Max($maxY,$y)
                if($x-eq0-or$y-eq0-or$x-eq($Mask.Width-1)-or$y-eq($Mask.Height-1)){$touchesFrame=$true}
                $pixel=$Source.GetPixel($x,$y)
                $chroma=[Math]::Max($pixel.R,[Math]::Max($pixel.G,$pixel.B))-
                    [Math]::Min($pixel.R,[Math]::Min($pixel.G,$pixel.B))
                $maximumObservedChroma=[Math]::Max($maximumObservedChroma,$chroma)
                foreach($offset in $offsets)
                {
                    $nx=$x+$offset[0];$ny=$y+$offset[1]
                    if($nx-lt0-or$ny-lt0-or$nx-ge$Mask.Width-or$ny-ge$Mask.Height){continue}
                    $neighborIndex=($ny*$Mask.Width)+$nx
                    if(-not$visited[$neighborIndex]-and$Mask.GetPixel($nx,$ny).R-eq0)
                    {$visited[$neighborIndex]=$true;$queue.Enqueue($neighborIndex)}
                }
            }
            if(-not$touchesFrame-and$indices.Count-le$MaximumSize-and
                $maximumObservedChroma-le$MaximumChroma)
            {
                $accepted.Add([pscustomobject]@{
                    Indices=[int[]]$indices.ToArray();Size=$indices.Count;TouchesFrame=$touchesFrame
                    MinX=$minX;MinY=$minY;MaxX=$maxX;MaxY=$maxY
                    MaximumChroma=$maximumObservedChroma
                })
            }
        }
    }
    return @($accepted)
}

function Get-IndependentProxyMembership([object[]]$Components,[int]$Width)
{
    $records=@($Components|ForEach-Object{
        $coordinates=@($_.Indices|ForEach-Object{
            [pscustomobject]@{X=[int]($_%$Width);Y=[int][Math]::Floor($_/$Width)}
        }|Sort-Object Y,X)
        [pscustomobject]@{
            FirstY=$coordinates[0].Y;FirstX=$coordinates[0].X
            Record="C|$($coordinates.Count)|$(@($coordinates|ForEach-Object{"$($_.Y),$($_.X)"})-join ';')"
        }
    }|Sort-Object FirstY,FirstX|ForEach-Object Record)
    $bytes=[Text.Encoding]::UTF8.GetBytes($records-join"`n")
    return [pscustomobject]@{
        Records=$records
        Hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
    }
}

function New-IndependentResizeProxy(
    [Drawing.Bitmap]$Candidate,[Drawing.Color[,]]$FillField,[object[]]$Components)
{
    $proxy=Copy-Bitmap32 $Candidate
    try
    {
        foreach($component in $Components)
        {
            foreach($index in $component.Indices)
            {
                $x=$index%$Candidate.Width;$y=[int][Math]::Floor($index/$Candidate.Width)
                $fill=$FillField[$x,$y];$original=$proxy.GetPixel($x,$y)
                $proxy.SetPixel($x,$y,[Drawing.Color]::FromArgb($original.A,$fill.R,$fill.G,$fill.B))
            }
        }
        return $proxy
    }
    catch{$proxy.Dispose();throw}
}

function Assert-ProxyMembership(
    [object[]]$Components,[int]$Width,[string]$Label)
{
    Assert-Equal 0 @($Components|Where-Object TouchesFrame).Count `
        "$Label admitted a frame-touching component."
    Assert-Equal 6 $Components.Count "$Label component count changed."
    $pixelCount=(@($Components|ForEach-Object Size)|Measure-Object -Sum).Sum
    Assert-Equal 12 $pixelCount "$Label pixel count changed."
    $membership=Get-IndependentProxyMembership $Components $Width
    Assert-Equal '2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54' `
        $membership.Hash "$Label canonical membership changed."
    Assert-Equal 1 @($Components|Where-Object{
        $_.Size-eq7-and$_.MinX-eq144-and$_.MinY-eq159-and$_.MaxX-eq147-and$_.MaxY-eq160}).Count `
        "$Label diagnosed seven-pixel gray island changed."
    return $membership
}

function Assert-SourceBodyEquality(
    [Drawing.Bitmap]$Open,[Drawing.Bitmap]$Closed,[Drawing.Bitmap]$Mask,[string]$Label)
{
    for ($y = 0; $y -lt 225; $y++)
    {
        for ($x = 0; $x -lt 225; $x++)
        {
            if ($Mask.GetPixel($x,$y).R -eq 255)
            {
                Assert-Equal $Open.GetPixel($x,$y).ToArgb() $Closed.GetPixel($x,$y).ToArgb() `
                    "$Label open/closed body RGB changed at ($x,$y)."
            }
        }
    }
}

$script:FrozenForegroundHairRuns=@(
    '114:97-100','115:98-103','116:101-105','117:103-106','118:104-106',
    '119:104-106','120:104-106','121:104-106','122:104-106','123:104-106',
    '124:104-106','125:104-106','126:104-106','127:104-106','128:104-106',
    '129:104-106','130:104-106','131:104-106','132:104-106','133:103-105',
    '134:103-104','135:103-103','136:102-103','137:102-103','138:101-102',
    '139:101-102','140:101-101','141:100-101','142:99-100','143:99-100')
$script:ShiftedForegroundHairRuns=@(
    '125:83-83','126:83-84','127:83-85','128:83-85','129:84-85','130:85-85')

$script:ReviewedSourceEyeChangeKeys=[Collections.Generic.HashSet[string]]::new(
    [StringComparer]::Ordinal)
foreach($encodedRun in @(
    '114:43-59','115:44-60','116:44-62','117:43-63','118:43-64','119:43-64',
    '120:43-65','121:43-64','122:43-64','123:43-64','124:43-64','125:43-64',
    '126:43-64','127:43-64','128:43-64','129:43-64','130:43-64','131:44-63',
    '132:45-62','133:46-61','134:48-59','135:51-57',
    '114:92-100','115:89-103','116:86-105','117:86-106','118:86-106','119:86-106',
    '120:86-106','121:86-106','122:86-106','123:86-106','124:86-106','125:86-106',
    '126:86-106','127:86-106','128:86-106','129:86-106','130:86-106','131:86-106',
    '132:86-106','133:87-105','134:88-104','135:90-103','136:93-103','137:86-103',
    '138:86-102','139:88-102','140:89-101','141:99-101','142:99-100','143:99-100',
    '121:50-58','122:48-60','123:47-49','123:59-61','124:46-48','124:60-62',
    '125:46-47','125:61-62','121:92-100','122:90-102','123:89-91','123:101-103',
    '124:88-90','124:102-104','125:88-89','125:103-104'))
{
    $parts=$encodedRun.Split(':');$y=[int]$parts[0];$bounds=$parts[1].Split('-')
    foreach($x in ([int]$bounds[0])..([int]$bounds[1]))
    {$null=$script:ReviewedSourceEyeChangeKeys.Add("$x,$y")}
}

foreach($encodedRun in @(
    '124:81-81','125:80-83','126:80-84','127:80-85',
    '128:82-85','129:83-85','130:84-85'))
{
    $parts=$encodedRun.Split(':');$y=[int]$parts[0];$bounds=$parts[1].Split('-')
    foreach($x in ([int]$bounds[0])..([int]$bounds[1]))
    {$null=$script:ReviewedSourceEyeChangeKeys.Add("$x,$y")}
}

foreach($encodedRun in @($script:FrozenForegroundHairRuns)+@($script:ShiftedForegroundHairRuns))
{
    $parts=$encodedRun.Split(':');$y=[int]$parts[0];$bounds=$parts[1].Split('-')
    foreach($x in ([int]$bounds[0])..([int]$bounds[1]))
    {
        Assert-True $script:ReviewedSourceEyeChangeKeys.Remove("$x,$y") `
            "Foreground-hair coordinate ($x,$y) was not in the reviewed eye/lid support."
    }
}

function Test-InSourceEyeRegion([int]$X,[int]$Y)
{return $script:ReviewedSourceEyeChangeKeys.Contains("$X,$Y")}

$script:ReviewedSourceStateChangeKeys=[Collections.Generic.HashSet[string]]::new(
    $script:ReviewedSourceEyeChangeKeys,[StringComparer]::Ordinal)
foreach($encodedRun in @(
    '114:92-100','115:89-103','116:86-105','117:86-106','118:86-106','119:86-106',
    '120:86-106','121:86-106','122:86-106','123:86-106','124:86-106','125:86-106',
    '126:86-106','127:86-106','128:86-106','129:86-106','130:86-106','131:86-106',
    '132:86-106','133:87-105','134:88-104','135:90-103','136:93-103','137:86-103',
    '138:86-102','139:88-102','140:89-101','141:99-101','142:99-100','143:99-100'))
{
    $parts=$encodedRun.Split(':');$y=[int]$parts[0];$bounds=$parts[1].Split('-')
    foreach($x in ([int]$bounds[0])..([int]$bounds[1]))
    {$null=$script:ReviewedSourceStateChangeKeys.Add("$($x-4),$y")}
}

function Test-InSourceStateRegion([int]$X,[int]$Y)
{return $script:ReviewedSourceStateChangeKeys.Contains("$X,$Y")}

function Test-InNativeEyeFilterSupport([int]$X,[int]$Y)
{
    return ($X -ge 16 -and $X -le 30 -and $Y -ge 46 -and $Y -le 61) -or
        ($X -ge 31 -and $X -le 48 -and $Y -ge 46 -and $Y -le 63)
}

function Assert-ProxyInputEqualityOutsideEyes(
    [Drawing.Bitmap]$Open,[Drawing.Bitmap]$Closed,[string]$Label)
{
    for($y=0;$y-lt$Open.Height;$y++)
    {
        for($x=0;$x-lt$Open.Width;$x++)
        {
            if(Test-InSourceStateRegion $x $y){continue}
            Assert-Equal $Open.GetPixel($x,$y).ToArgb() $Closed.GetPixel($x,$y).ToArgb() `
                "$Label open/closed resize-proxy input differs outside eye regions at ($x,$y)."
        }
    }
}

function Get-OpticalInk([Drawing.Color]$Pixel)
{
    $alpha = $Pixel.A / 255.0
    $luminance = (0.2126 * $Pixel.R) + (0.7152 * $Pixel.G) + (0.0722 * $Pixel.B)
    return [Math]::Max(0.0,$alpha * (255.0 - $luminance) / 255.0)
}

function Get-BitmapPixelHash([Drawing.Bitmap]$Bitmap)
{
    $bytes=[Collections.Generic.List[byte]]::new($Bitmap.Width*$Bitmap.Height*4)
    for($y=0;$y-lt$Bitmap.Height;$y++)
    {
        for($x=0;$x-lt$Bitmap.Width;$x++)
        {
            $pixel=$Bitmap.GetPixel($x,$y)
            $bytes.Add($pixel.A);$bytes.Add($pixel.R);$bytes.Add($pixel.G);$bytes.Add($pixel.B)
        }
    }
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes.ToArray()))
}

$script:IndependentEyeBlankSpecs=@(
    @{
        Name='left'; Runs=@(
            '114:43-59','115:44-60','116:44-62','117:43-63','118:43-64','119:43-64',
            '120:43-65','121:43-64','122:43-64','123:43-64','124:43-64','125:43-64',
            '126:43-64','127:43-64','128:43-64','129:43-64','130:43-64','131:44-63',
            '132:45-62','133:46-61','134:48-59','135:51-57')
        MinimumX=43;MaximumX=64;MinimumY=114;MaximumY=135
        TopLeftX=42;TopLeftY=137;TopRightX=66;TopRightY=136
        BottomLeftX=48;BottomLeftY=143;BottomRightX=64;BottomRightY=143
    },
    @{
        Name='right'; Runs=@(
            '114:92-100','115:89-103','116:86-105','117:86-106','118:86-106','119:86-106',
            '120:86-106','121:86-106','122:86-106','123:86-106','124:86-106','125:86-106',
            '126:86-106','127:86-106','128:86-106','129:86-106','130:86-106','131:86-106',
            '132:86-106','133:87-105','134:88-104','135:90-103','136:93-103','137:86-103',
            '138:86-102','139:88-102','140:89-101','141:99-101','142:99-100','143:99-100')
        MinimumX=86;MaximumX=106;MinimumY=114;MaximumY=143
        TopLeftX=65;TopLeftY=134;TopRightX=106;TopRightY=142
        BottomLeftX=86;BottomLeftY=144;BottomRightX=106;BottomRightY=144
    })

function Get-IndependentEyeBlankColor(
    [Drawing.Bitmap]$FaceSource,[int]$X,[int]$Y,[hashtable]$Eye)
{
    $topLeft=$FaceSource.GetPixel($Eye.TopLeftX,$Eye.TopLeftY)
    $topRight=$FaceSource.GetPixel($Eye.TopRightX,$Eye.TopRightY)
    $bottomLeft=$FaceSource.GetPixel($Eye.BottomLeftX,$Eye.BottomLeftY)
    $bottomRight=$FaceSource.GetPixel($Eye.BottomRightX,$Eye.BottomRightY)
    $xRatio=($X-$Eye.MinimumX)/($Eye.MaximumX-$Eye.MinimumX)
    $yRatio=($Y-$Eye.MinimumY)/($Eye.MaximumY-$Eye.MinimumY)
    $channels=foreach($channel in @('R','G','B'))
    {
        $top=$topLeft.$channel+(($topRight.$channel-$topLeft.$channel)*$xRatio)
        $bottom=$bottomLeft.$channel+(($bottomRight.$channel-$bottomLeft.$channel)*$xRatio)
        [int][Math]::Round($top+(($bottom-$top)*$yRatio),0,[MidpointRounding]::ToEven)
    }
    return [Drawing.Color]::FromArgb(255,$channels[0],$channels[1],$channels[2])
}

function New-IndependentEyeBlank([Drawing.Bitmap]$Open,[Drawing.Bitmap]$FaceSource)
{
    $blank=Copy-Bitmap32 $Open
    try
    {
        foreach($eye in $script:IndependentEyeBlankSpecs)
        {
            foreach($encodedRun in $eye.Runs)
            {
                $parts=$encodedRun.Split(':');$y=[int]$parts[0];$bounds=$parts[1].Split('-')
                foreach($x in ([int]$bounds[0])..([int]$bounds[1]))
                {
                    $face=Get-IndependentEyeBlankColor $FaceSource $x $y $eye
                    $alpha=$blank.GetPixel($x,$y).A
                    $blank.SetPixel($x,$y,[Drawing.Color]::FromArgb(
                        $alpha,$face.R,$face.G,$face.B))
                }
            }
        }
        foreach($encodedRun in @($script:FrozenForegroundHairRuns)+@($script:ShiftedForegroundHairRuns))
        {
            $parts=$encodedRun.Split(':');$y=[int]$parts[0];$bounds=$parts[1].Split('-')
            foreach($x in ([int]$bounds[0])..([int]$bounds[1]))
            {$blank.SetPixel($x,$y,$Open.GetPixel($x,$y))}
        }
        return $blank
    }
    catch{$blank.Dispose();throw}
}

function Measure-NativeOpenEyeAperture(
    [Drawing.Bitmap]$Open,[string]$Name,[int]$StartX,[int]$EndX,[int]$StartY,[int]$EndY)
{
    $count=0;$sumX=0.0;$sumY=0.0;$minX=999;$maxX=-1;$minY=999;$maxY=-1
    for($y=$StartY;$y-le$EndY;$y++)
    {
        for($x=$StartX;$x-le$EndX;$x++)
        {
            $pixel=$Open.GetPixel($x,$y)
            if($pixel.A-eq0-or($pixel.B-$pixel.R)-lt10){continue}
            $count++;$sumX+=$x;$sumY+=$y
            $minX=[Math]::Min($minX,$x);$maxX=[Math]::Max($maxX,$x)
            $minY=[Math]::Min($minY,$y);$maxY=[Math]::Max($maxY,$y)
        }
    }
    Assert-True ($count-gt0) "$Name canonical open aperture has no visible color pixels."
    return [pscustomobject]@{
        Name=$Name;VisibleWidth=$maxX-$minX+1;Bounds="($minX,$minY)-($maxX,$maxY)"
        CenterX=$sumX/$count;CenterY=$sumY/$count
    }
}

function Measure-NativeClosedEyeCurve(
    [Drawing.Bitmap]$Blank,[Drawing.Bitmap]$Closed,[string]$Name,
    [int]$StartX,[int]$EndX,[int]$StartY,[int]$EndY)
{
    $gain=[double[]]::new(96*96)
    $total=0.0;$weightedX=0.0;$weightedY=0.0
    $minX=999;$maxX=-1;$minY=999;$maxY=-1
    for($y=$StartY;$y-le$EndY;$y++)
    {
        for($x=$StartX;$x-le$EndX;$x++)
        {
            $value=[Math]::Max(0.0,
                (Get-OpticalInk $Closed.GetPixel($x,$y))-(Get-OpticalInk $Blank.GetPixel($x,$y)))
            if($value-lt0.01){continue}
            $gain[($y*96)+$x]=$value;$total+=$value;$weightedX+=($x*$value);$weightedY+=($y*$value)
            $minX=[Math]::Min($minX,$x);$maxX=[Math]::Max($maxX,$x)
            $minY=[Math]::Min($minY,$y);$maxY=[Math]::Max($maxY,$y)
        }
    }
    Assert-True ($total-gt0.0) "$Name closed curve has no measurable native ink gain."

    $columns=[Collections.Generic.List[object]]::new()
    for($x=$minX;$x-le$maxX;$x++)
    {
        $columnTotal=0.0;$columnWeightedY=0.0
        for($y=$minY;$y-le$maxY;$y++)
        {
            $value=$gain[($y*96)+$x]
            $columnTotal+=$value;$columnWeightedY+=($y*$value)
        }
        if($columnTotal-ge0.02)
        {$columns.Add([pscustomobject]@{X=$x;Y=$columnWeightedY/$columnTotal;Weight=$columnTotal})}
    }
    Assert-True ($columns.Count-ge5) "$Name closed curve has fewer than five visible native columns."
    $edgeCount=[Math]::Max(1,[int][Math]::Ceiling($columns.Count*0.20))
    $edgeColumns=@($columns|Select-Object -First $edgeCount)+@($columns|Select-Object -Last $edgeCount)
    $endpointY=($edgeColumns|Measure-Object Y -Average).Average
    $centerX=$weightedX/$total
    $centerColumns=@($columns|Sort-Object @{Expression={[Math]::Abs($_.X-$centerX)}}|Select-Object -First 3)
    $centerY=($centerColumns|Measure-Object Y -Average).Average

    $active=[bool[]]::new(96*96)
    for($y=$minY;$y-le$maxY;$y++)
    {for($x=$minX;$x-le$maxX;$x++){$active[($y*96)+$x]=$gain[($y*96)+$x]-ge0.04}}
    $components=0
    for($y=$minY;$y-le$maxY;$y++)
    {
        for($x=$minX;$x-le$maxX;$x++)
        {
            $index=($y*96)+$x
            if(-not$active[$index]){continue}
            $components++;$queue=[Collections.Generic.Queue[int]]::new();$queue.Enqueue($index);$active[$index]=$false
            while($queue.Count-gt0)
            {
                $current=$queue.Dequeue();$currentX=$current%96;$currentY=[int][Math]::Floor($current/96)
                foreach($dy in -1..1){foreach($dx in -1..1)
                {
                    if($dx-eq0-and$dy-eq0){continue}
                    $nextX=$currentX+$dx;$nextY=$currentY+$dy
                    if($nextX-lt$minX-or$nextX-gt$maxX-or$nextY-lt$minY-or$nextY-gt$maxY){continue}
                    $next=($nextY*96)+$nextX
                    if(-not$active[$next]){continue}
                    $active[$next]=$false;$queue.Enqueue($next)
                }}
            }
        }
    }
    return [pscustomobject]@{
        Name=$Name;VisibleWidth=$maxX-$minX+1;Bounds="($minX,$minY)-($maxX,$maxY)"
        CenterX=$centerX;CenterY=$weightedY/$total;EndpointY=$endpointY
        CurveCenterY=$centerY;Dip=$centerY-$endpointY;Components=$components;TotalInk=$total
    }
}

function Assert-SourceEyeChangeContract(
    [Drawing.Bitmap]$SourceOpen,[Drawing.Bitmap]$SourceClosed)
{
    $observedChanges=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for($y=0;$y-lt$SourceOpen.Height;$y++)
    {
        for($x=0;$x-lt$SourceOpen.Width;$x++)
        {
            $openPixel=$SourceOpen.GetPixel($x,$y)
            $closedPixel=$SourceClosed.GetPixel($x,$y)
            Assert-Equal $openPixel.A $closedPixel.A "Source open/closed alpha differs at ($x,$y)."
            if($openPixel.ToArgb()-ne$closedPixel.ToArgb())
            {
                Assert-True (Test-InSourceEyeRegion $x $y) `
                    "Closed-eye source change escaped exact reviewed source eye stencil/lid membership at ($x,$y)."
                $null=$observedChanges.Add("$x,$y")
            }
            Assert-True ($closedPixel.R-ne250-or$closedPixel.G-ne220-or$closedPixel.B-ne224) `
                "Detached forbidden #FADCE0 eye patch exists at source ($x,$y)."
        }
    }
    Assert-Equal 873 $script:ReviewedSourceEyeChangeKeys.Count `
        'Independent reviewed eye/lid membership count changed.'
    Assert-Equal 873 $observedChanges.Count `
        'Observed source open/closed eye/lid change count changed.'
    Assert-True $script:ReviewedSourceEyeChangeKeys.SetEquals($observedChanges) `
        'Observed source open/closed changes do not equal the independent reviewed eye/lid membership.'
    for($y=136;$y-le153;$y++)
    {
        for($x=55;$x-le82;$x++)
        {
            Assert-Equal $SourceOpen.GetPixel($x,$y).ToArgb() $SourceClosed.GetPixel($x,$y).ToArgb() `
                "Source mouth changed at ($x,$y)."
        }
    }
    return $observedChanges.Count
}

function Assert-EyeAndMouthContract(
    [Drawing.Bitmap]$SourceOpen,[Drawing.Bitmap]$SourceClosed,
    [Drawing.Bitmap]$NativeOpen,[Drawing.Bitmap]$NativeBlank,[Drawing.Bitmap]$NativeClosed)
{
    $sourceChanges=Assert-SourceEyeChangeContract $SourceOpen $SourceClosed

    $measurements=[Collections.Generic.List[object]]::new()
    foreach($eye in @(
        @{Name='viewer-left';StartX=16;EndX=30;StartY=46;EndY=61},
        @{Name='viewer-right';StartX=34;EndX=48;StartY=46;EndY=63}))
    {
        $aperture=Measure-NativeOpenEyeAperture $NativeOpen $eye.Name `
            $eye.StartX $eye.EndX $eye.StartY $eye.EndY
        $curve=Measure-NativeClosedEyeCurve $NativeBlank $NativeClosed $eye.Name `
            $eye.StartX $eye.EndX $eye.StartY $eye.EndY
        if($eye.Name-eq'viewer-right')
        {
            $requestedCenterX=36.8180538802584
            Assert-True ([Math]::Abs($curve.CenterX-$requestedCenterX)-le0.01) `
                "$($eye.Name) closed curve did not move exactly three native pixels left from the user-rejected 39.8180538802584 center (target=$requestedCenterX, curve=$($curve.CenterX))."
        }
        else
        {
            Assert-True ([Math]::Abs($curve.CenterX-$aperture.CenterX)-le0.60) `
                "$($eye.Name) closed curve is horizontally misaligned with its canonical aperture (aperture=$($aperture.CenterX), curve=$($curve.CenterX))."
        }
        Assert-True ([Math]::Abs($curve.CenterY-$aperture.CenterY)-le0.60) `
            "$($eye.Name) closed curve is vertically misaligned with its canonical aperture (aperture=$($aperture.CenterY), curve=$($curve.CenterY))."
        $minimumInsetWidth=[int][Math]::Ceiling($aperture.VisibleWidth*0.50)
        Assert-True ($curve.VisibleWidth-ge$minimumInsetWidth-and
            $curve.VisibleWidth-le$aperture.VisibleWidth) `
            "$($eye.Name) closed curve exceeds its hair-framed aperture after the source endpoint-clearance contract (aperture=$($aperture.VisibleWidth), curve=$($curve.VisibleWidth), bounds=$($curve.Bounds))."
        Assert-Equal 1 $curve.Components `
            "$($eye.Name) closed curve is not one connected native component."
        Assert-True ($curve.Dip-ge0.75-and$curve.Dip-le1.15) `
            "$($eye.Name) closed curve does not retain a gentle roughly one-pixel downward-center dip (dip=$($curve.Dip), endpointY=$($curve.EndpointY), centerY=$($curve.CurveCenterY))."
        Write-Output "CLOSED EYE METRICS name=$($eye.Name) apertureBounds=$($aperture.Bounds) apertureCenter=$($aperture.CenterX),$($aperture.CenterY) curveBounds=$($curve.Bounds) curveCenter=$($curve.CenterX),$($curve.CenterY) width=$($curve.VisibleWidth) components=$($curve.Components) dip=$($curve.Dip)"
        $measurements.Add([pscustomobject]@{Aperture=$aperture;Curve=$curve})
    }
    $apertureSeparation=$measurements[1].Aperture.CenterY-$measurements[0].Aperture.CenterY
    $curveSeparation=$measurements[1].Curve.CenterY-$measurements[0].Curve.CenterY
    Assert-True ($curveSeparation-ge2.40-and
        [Math]::Abs($curveSeparation-$apertureSeparation)-le0.50) `
        "Independent left/right closed-eye heights do not preserve canonical asymmetry (aperture=$apertureSeparation, curve=$curveSeparation)."

    $nativeChanges = 0
    for ($y = 0; $y -lt 96; $y++)
    {
        for ($x = 0; $x -lt 96; $x++)
        {
            $openPixel = $NativeOpen.GetPixel($x,$y)
            $closedPixel = $NativeClosed.GetPixel($x,$y)
            Assert-Equal $openPixel.A $closedPixel.A "Native open/closed alpha differs at ($x,$y)."
            if ($openPixel.ToArgb() -ne $closedPixel.ToArgb())
            {
                Assert-True (Test-InNativeEyeFilterSupport $x $y) `
                    "Closed-eye native change escaped scaled filter support at ($x,$y)."
                $nativeChanges++
            }
        }
    }
    Assert-True ($nativeChanges -ge 80) 'Both native eyes did not visibly close.'

    foreach ($eye in @(
        @{ Name='left'; Lower=@('20,55','22,56','25,55') },
        @{ Name='right'; Lower=@('38,59','40,59','41,59') }))
    {
        foreach ($probe in $eye.Lower)
        {
            $parts = $probe.Split(',')
            $openInk = Get-OpticalInk $NativeOpen.GetPixel([int]$parts[0],[int]$parts[1])
            $closedInk = Get-OpticalInk $NativeClosed.GetPixel([int]$parts[0],[int]$parts[1])
            Assert-True ($closedInk -le 0.30 -and ($openInk - $closedInk) -ge 0.12) `
                "$($eye.Name) lower open-eye oval/underline survived at $probe (open=$openInk, closed=$closedInk)."
        }
    }
    for ($y = 61; $y -le 66; $y++)
    {
        for ($x = 24; $x -le 33; $x++)
        {
            Assert-Equal $NativeOpen.GetPixel($x,$y).ToArgb() $NativeClosed.GetPixel($x,$y).ToArgb() `
                "Native mouth changed at ($x,$y)."
        }
    }
}

function Measure-NativeBodyMetrics(
    [Drawing.Bitmap]$NativeOpen,[hashtable]$Authority,[Drawing.Color]$OutlineColor)
{
    $frozenHairMedian = 2.009803921568627
    $values = [Collections.Generic.List[double]]::new()
    $records = [Collections.Generic.List[string]]::new()
    foreach ($normal in @($Authority.BodyNormals))
    {
        $fill = $NativeOpen.GetPixel([int]$normal.NativeFill[0],[int]$normal.NativeFill[1])
        $value = Measure-DororongContinuousCoverage `
            $NativeOpen $normal.NativeNormal $fill $OutlineColor
        $delta = [Math]::Abs($value - $frozenHairMedian)
        $values.Add($value)
        $records.Add("$($normal.Name)=$([string]::Format([Globalization.CultureInfo]::InvariantCulture,'{0:R}',$value));delta=$([string]::Format([Globalization.CultureInfo]::InvariantCulture,'{0:R}',$delta))")
    }
    $minimum = ($values | Measure-Object -Minimum).Minimum
    $maximum = ($values | Measure-Object -Maximum).Maximum
    $spread = $maximum - $minimum
    [Console]::WriteLine("NATIVE BODY METRICS diagnosticOnly=true hairMedian=$frozenHairMedian tolerance=0.35 spreadLimit=0.50 minimum=$minimum maximum=$maximum spread=$spread values=$($records -join '|')")
    return [pscustomobject]@{ HairMedian=$frozenHairMedian; Minimum=$minimum; Maximum=$maximum; Spread=$spread }
}

function Assert-MutationRejected([string]$Label,[string]$ExpectedSemantic,[scriptblock]$Mutation)
{
    try { $null = & $Mutation }
    catch
    {
        Assert-True $_.Exception.Message.Contains($ExpectedSemantic,[StringComparison]::Ordinal) `
            "Mutation '$Label' failed for the wrong semantic: $($_.Exception.Message)"
        Write-Output "MUTATION PASS label=$Label semantic=$ExpectedSemantic failure=$($_.Exception.Message)"
        return
    }
    throw "Mutation '$Label' survived the exact-art contract."
}

function Invoke-FillFloorMutationContract(
    [Drawing.Bitmap]$Source,[Drawing.Bitmap]$Seed,[Drawing.Bitmap]$Mask,
    [object]$Contour,[Collections.IDictionary]$Constants)
{
    $targetX=158;$targetY=124;$targetKey="$targetX,$targetY"
    $baseline=$Source.GetPixel($targetX,$targetY)
    Assert-Equal '227,228,232' "$($baseline.R),$($baseline.G),$($baseline.B)" `
        'Fill-floor mutation baseline RGB changed.'
    Assert-Equal 255 $baseline.A 'Fill-floor mutation baseline alpha changed.'
    Assert-Equal 255 $Mask.GetPixel($targetX,$targetY).R `
        'Fill-floor mutation target left the final mask.'
    Assert-Equal 255 $Seed.GetPixel($targetX,$targetY).R `
        'Fill-floor mutation target left the seed mask.'
    $minimum=[Math]::Min($baseline.R,[Math]::Min($baseline.G,$baseline.B))
    $maximum=[Math]::Max($baseline.R,[Math]::Max($baseline.G,$baseline.B))
    Assert-True ($minimum-ge225-and($maximum-$minimum)-le8) `
        'Fill-floor mutation target is not independently floor/chroma eligible at baseline.'
    $minimumDistanceSquared=[double]::PositiveInfinity
    foreach($segment in @($Contour.Segments))
    {
        $distanceSquared=Get-IndependentPointSegmentDistanceSquared $targetX $targetY $segment
        if($distanceSquared-lt$minimumDistanceSquared){$minimumDistanceSquared=$distanceSquared}
    }
    $independentDistance=[Math]::Sqrt($minimumDistanceSquared)
    Assert-True ($independentDistance-gt8.0) `
        'Fill-floor mutation target is not independently contour-distance eligible.'
    Assert-Equal 'A29D007B699A16B555FE5133854E832FEFD8409EA85F2FECE3EEA97BF444FD65' `
        (Get-DororongCanonicalContourHash $Contour) `
        'Fill-floor mutation baseline contour changed.'

    $mutatedSource=$null;$floor225Fill=$null;$floor224Fill=$null
    $module=Get-Module Dororong.SubpixelOutline
    try
    {
        $mutatedSource=Copy-Bitmap32 $Source
        $mutatedSource.SetPixel($targetX,$targetY,
            [Drawing.Color]::FromArgb($baseline.A,224,224,224))
        Assert-Equal $baseline.A $mutatedSource.GetPixel($targetX,$targetY).A `
            'Fill-floor mutation changed source alpha.'
        Assert-Equal '227,228,232' `
            "$($Source.GetPixel($targetX,$targetY).R),$($Source.GetPixel($targetX,$targetY).G),$($Source.GetPixel($targetX,$targetY).B)" `
            'Fill-floor mutation changed the protected processed source.'

        $endpoints=[Drawing.PointF[]]@($Constants.LegalEndpoints|ForEach-Object{
            [Drawing.PointF]::new([single]$_.X,[single]$_.Y)})
        $mutatedContour=New-DororongVisibleContour $mutatedSource $Mask $endpoints
        Assert-Equal (Get-DororongCanonicalContourHash $Contour) `
            (Get-DororongCanonicalContourHash $mutatedContour) `
            'Fill-floor mutation changed canonical contour geometry.'

        & $module {$script:OutlineConstants.FillFloor=225}
        $floor225Fill=New-DororongFillField $mutatedSource $Seed $Mask $mutatedContour
        Assert-Equal 2769 $floor225Fill.EligibleSeedCount `
            'Floor-225 source-clone eligible sample count changed.'
        Assert-Equal 0 @($floor225Fill.EligibleSeeds|Where-Object{
            "$($_.X),$($_.Y)"-eq$targetKey}).Count `
            'Floor 225 admitted the RGB-224 mutation target.'

        & $module {$script:OutlineConstants.FillFloor=224}
        $floor224Fill=New-DororongFillField $mutatedSource $Seed $Mask $mutatedContour
        Assert-Equal 2770 $floor224Fill.EligibleSeedCount `
            'Floor-224 source-clone eligible sample count changed.'
        Assert-Equal 1 @($floor224Fill.EligibleSeeds|Where-Object{
            "$($_.X),$($_.Y)"-eq$targetKey}).Count `
            'Floor 224 did not admit the RGB-224 mutation target.'
        Assert-MutationRejected 'fill-floor-225-to-224' `
            'admitted dark fill seed at (158,124)' {
            Assert-FillSeedContract $mutatedSource $Seed $floor224Fill 'Fill-floor mutation'
        }
        Write-Output "FILL-FLOOR MUTATION PASS coordinate=$targetKey baselineRgb=227,228,232 mutatedRgb=224,224,224 floor225Seeds=$($floor225Fill.EligibleSeedCount) floor224Seeds=$($floor224Fill.EligibleSeedCount) independentDistance=$independentDistance"
    }
    finally
    {
        & $module {$script:OutlineConstants.FillFloor=225}
        if($floor224Fill-is[IDisposable]){$floor224Fill.Dispose()}
        if($floor225Fill-is[IDisposable]){$floor225Fill.Dispose()}
        if($null-ne$mutatedSource){$mutatedSource.Dispose()}
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$paths = [ordered]@{
    Source = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical-source.png'
    Seed = Join-Path $repositoryRoot 'tests/fixtures/dororong-body-region-seed.png'
    Mask = Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-body-region-mask.png'
    Authority = Join-Path $repositoryRoot 'tests/fixtures/dororong-body-outline-authority.psd1'
    Constants = Join-Path $repositoryRoot 'tools/Dororong.SubpixelOutline.Constants.psd1'
    SourceRasterModule = Join-Path $repositoryRoot 'tools/Dororong.SourceRaster.psm1'
    SubpixelModule = Join-Path $repositoryRoot 'tools/Dororong.SubpixelOutline.psm1'
    Generator = Join-Path $repositoryRoot 'tools/Generate-CanonicalArt.ps1'
}
$expectedHashes = [ordered]@{
    Source = 'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504'
    Seed = 'E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779'
    Mask = 'D08B3A941C662F1CBC55C486C13FD4C6CD8901DA9CD5CF8512509698219FE46F'
    Authority = 'DDF749007995B3F03781A3A51467013F406C5F7A2AA0480212523A79EF31F17F'
    SourceOpen = 'D1F0770CBCA95FC79B5E68642D78A5A48077834495C9ECDBCD73B34545AC94FF'
    SourceOpenBaseline = '8F542A4F1B2671789BD7CD4980D890BF96CFCC9DADBC9D4E61963CCE2D384CCB'
    SourceClosed = '842035A880C30B79694AEC0D481374293A9DA4722410642EFD171ACD490099BD'
    SourceHalfClosed = 'CD7F8C114CAE3B303AC9D986122AF61DCD1AFCF0DC624FA3D819950052B67B0D'
    NativeOpen = '238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511'
    NativeOpenBaseline = '3B3D171D2C62134284915D7D162D43F36263761D4EA6344F4AC8BCEA730C5B59'
    NativeClosed = '1F8A50A5907D6BD926ECC4F93CD83064D8F1C862FCA457773E48FB3AFE172331'
    NativeHalfClosed = '44339755E917FED67A41F8D6D2D9116EA8367106FA2E664BBC38F2E421AC0682'
    Contour = 'A29D007B699A16B555FE5133854E832FEFD8409EA85F2FECE3EEA97BF444FD65'
}
foreach ($name in @('Source','Seed','Mask','Authority'))
{
    Assert-Equal $expectedHashes[$name] (Get-FileHash -Algorithm SHA256 -LiteralPath $paths[$name]).Hash `
        "Pinned $name identity changed."
}

Add-Type -AssemblyName System.Drawing
if(-not[string]::IsNullOrWhiteSpace($EvidenceOnlyDirectory))
{
    Assert-ExactGeneratorEvidenceSet ([IO.Path]::GetFullPath($EvidenceOnlyDirectory)) $expectedHashes
    Write-Output 'EVIDENCE-ONLY PASS files=8 decoded=8 sourceDimensions=225x225 nativeDimensions=96x96 hashes=reviewed-exact contract=exact'
    return
}
if(-not[string]::IsNullOrWhiteSpace($EyeOnlyOpenPath)-or
    -not[string]::IsNullOrWhiteSpace($EyeOnlyClosedPath))
{
    Assert-True (-not[string]::IsNullOrWhiteSpace($EyeOnlyOpenPath)-and
        -not[string]::IsNullOrWhiteSpace($EyeOnlyClosedPath)-and
        -not[string]::IsNullOrWhiteSpace($EyeOnlyNativeOpenPath)-and
        -not[string]::IsNullOrWhiteSpace($EyeOnlyNativeClosedPath)) `
        'Eye-only mode requires open/closed source and native candidate paths.'
    $eyeOpen=$null;$eyeClosed=$null;$eyeNativeOpen=$null;$eyeNativeClosed=$null
    $eyeRaw=$null;$eyeSource=$null;$eyeSeed=$null;$eyeMask=$null
    $eyeBlank=$null;$eyeBlankProxy=$null;$eyeNativeBlank=$null
    $eyeOpenProxy=$null;$eyeClosedProxy=$null;$eyeDirectNativeOpen=$null;$eyeDirectNativeClosed=$null
    try
    {
        $eyeOpen=[Drawing.Bitmap]::new([IO.Path]::GetFullPath($EyeOnlyOpenPath))
        $eyeClosed=[Drawing.Bitmap]::new([IO.Path]::GetFullPath($EyeOnlyClosedPath))
        $eyeNativeOpen=[Drawing.Bitmap]::new([IO.Path]::GetFullPath($EyeOnlyNativeOpenPath))
        $eyeNativeClosed=[Drawing.Bitmap]::new([IO.Path]::GetFullPath($EyeOnlyNativeClosedPath))
        Assert-True (Test-InSourceEyeRegion 65 120) `
            'Reviewed left-eye stencil boundary (65,120) is missing from exact membership.'
        Assert-True ($eyeOpen.GetPixel(65,120).ToArgb()-ne$eyeClosed.GetPixel(65,120).ToArgb()) `
            'Eye-only fixture does not exercise reviewed left-eye stencil boundary (65,120).'
        $sourceChanges=Assert-SourceEyeChangeContract $eyeOpen $eyeClosed
        Assert-AlphaZeroRgb $eyeNativeOpen 'Eye-only native-open candidate'
        Assert-AlphaZeroRgb $eyeNativeClosed 'Eye-only native-closed candidate'
        Assert-MutationRejected 'source-eye-outside-membership' `
            'escaped exact reviewed source eye stencil/lid membership' {
            $mutated=Copy-Bitmap32 $eyeClosed
            try
            {
                $pixel=$mutated.GetPixel(42,114)
                $mutated.SetPixel(42,114,[Drawing.Color]::FromArgb(
                    $pixel.A,($pixel.R-bxor1),$pixel.G,$pixel.B))
                $null=Assert-SourceEyeChangeContract $eyeOpen $mutated
            }
            finally{$mutated.Dispose()}
        }

        Import-Module $paths.SourceRasterModule -Force
        Import-Module $paths.SubpixelModule -Force
        $eyeRaw=[Drawing.Bitmap]::new($paths.Source)
        $eyeSource=Remove-DororongBoundaryBackground $eyeRaw
        $eyeSeed=[Drawing.Bitmap]::new($paths.Seed)
        $eyeMask=Import-DororongBodyMask $paths.Mask
        Assert-SourceCandidateContract $eyeSource $eyeMask $eyeOpen 'Eye-only source-open candidate'
        Assert-SourceCandidateContract $eyeSource $eyeMask $eyeClosed `
            'Eye-only source-closed candidate' -AllowEyeChanges
        Assert-SourceBodyEquality $eyeOpen $eyeClosed $eyeMask 'Eye-only source body'
        $eyeConstants=Import-PowerShellDataFile -LiteralPath $paths.Constants
        $eyeEndpoints=[Drawing.PointF[]]@($eyeConstants.LegalEndpoints|ForEach-Object{
            [Drawing.PointF]::new([single]$_.X,[single]$_.Y)})
        $eyeContour=New-DororongVisibleContour $eyeSource $eyeMask $eyeEndpoints
        $eyeFill=New-DororongFillField $eyeSource $eyeSeed $eyeMask $eyeContour
        $eyeComponents=@(Get-IndependentProxyComponents $eyeMask $eyeSource 7 8)
        $null=Assert-ProxyMembership $eyeComponents $eyeMask.Width 'Eye-only independent proxy'
        $eyeOpenProxy=New-IndependentResizeProxy $eyeOpen $eyeFill $eyeComponents
        $eyeClosedProxy=New-IndependentResizeProxy $eyeClosed $eyeFill $eyeComponents
        $eyeBlank=New-IndependentEyeBlank $eyeOpen $eyeSource
        Assert-Equal '7B101AAAD51934C16894271EB63BBF5D930EA858C99D98A2E127C920FF95C173' `
            (Get-BitmapPixelHash $eyeBlank) 'Independent eye-only source blank pixels changed.'
        $eyeBlankProxy=New-IndependentResizeProxy $eyeBlank $eyeFill $eyeComponents
        $eyeNativeBlank=Resize-DororongPremultiplied96 $eyeBlankProxy
        Assert-Equal 'F00905180F90166A6A061A8290E6DB8F4BD3EDB9DE0535BC5B3A7549F071C6AF' `
            (Get-BitmapPixelHash $eyeNativeBlank) 'Independent eye-only native blank pixels changed.'
        Assert-ProxyInputEqualityOutsideEyes $eyeOpenProxy $eyeClosedProxy 'Eye-only independent proxy'
        $eyeDirectNativeOpen=Resize-DororongPremultiplied96 $eyeOpenProxy
        $eyeDirectNativeClosed=Resize-DororongPremultiplied96 $eyeClosedProxy
        Assert-BitmapEqual $eyeDirectNativeOpen $eyeNativeOpen `
            'Eye-only native-open differs from one independent proxy resize.'
        Assert-BitmapEqual $eyeDirectNativeClosed $eyeNativeClosed `
            'Eye-only native-closed differs from one independent proxy resize.'
        Assert-EyeAndMouthContract $eyeOpen $eyeClosed $eyeNativeOpen $eyeNativeBlank $eyeNativeClosed
        . (Join-Path $repositoryRoot 'tests/support/Dororong.ContinuousOptics.ps1')
        $eyeAuthority=Import-PowerShellDataFile -LiteralPath $paths.Authority
        $eyeOutline=Get-OutlineMedianColor $eyeSource @($eyeConstants.OutlineSamples)
        $eyeMetrics=Measure-NativeBodyMetrics $eyeNativeOpen $eyeAuthority $eyeOutline
        Write-Output "EYE-ONLY PASS permissibleCoordinates=$($script:ReviewedSourceEyeChangeKeys.Count) observedChanges=$sourceChanges boundary=65,120 proxyComponents=$($eyeComponents.Count) resizeOpen=1 resizeClosed=1 nativeMinimum=$($eyeMetrics.Minimum) nativeMaximum=$($eyeMetrics.Maximum) nativeSpread=$($eyeMetrics.Spread)"
        return
    }
    finally
    {
        foreach($bitmap in @($eyeDirectNativeClosed,$eyeDirectNativeOpen,$eyeNativeBlank,$eyeBlankProxy,$eyeBlank,
            $eyeClosedProxy,$eyeOpenProxy,
            $eyeMask,$eyeSeed,$eyeSource,$eyeRaw,$eyeNativeClosed,$eyeNativeOpen,$eyeClosed,$eyeOpen))
        {if($null-ne$bitmap){$bitmap.Dispose()}}
    }
}
if($FillFloorMutationOnly)
{
    Import-Module $paths.SourceRasterModule -Force
    Import-Module $paths.SubpixelModule -Force
    $floorConstants=Import-PowerShellDataFile -LiteralPath $paths.Constants
    $floorRaw=$null;$floorSource=$null;$floorSeed=$null;$floorMask=$null
    try
    {
        $floorRaw=[Drawing.Bitmap]::new($paths.Source)
        $floorSource=Remove-DororongBoundaryBackground $floorRaw
        $floorSeed=[Drawing.Bitmap]::new($paths.Seed)
        $floorMask=Import-DororongBodyMask $paths.Mask
        $floorEndpoints=[Drawing.PointF[]]@($floorConstants.LegalEndpoints|ForEach-Object{
            [Drawing.PointF]::new([single]$_.X,[single]$_.Y)})
        $floorContour=New-DororongVisibleContour $floorSource $floorMask $floorEndpoints
        Invoke-FillFloorMutationContract `
            $floorSource $floorSeed $floorMask $floorContour $floorConstants
        Write-Output 'FILL-FLOOR-MUTATION-ONLY PASS'
        return
    }
    finally
    {
        foreach($bitmap in @($floorMask,$floorSeed,$floorSource,$floorRaw))
        {if($null-ne$bitmap){$bitmap.Dispose()}}
    }
}
Import-Module $paths.SourceRasterModule -Force
Import-Module $paths.SubpixelModule -Force
. (Join-Path $repositoryRoot 'tests/support/Dororong.ContinuousOptics.ps1')
$constants = Import-PowerShellDataFile -LiteralPath $paths.Constants
$authority = Import-PowerShellDataFile -LiteralPath $paths.Authority
Assert-Equal 8 ([int]$constants.SubpixelFactor) 'The exact raster factor changed.'
Assert-Equal 1.5 ([double]$constants.Width) 'The exact outline width changed.'

# Test-first direct behavioral gate. This runs before the real generator so the
# pre-production RED is an exact E-only raster mismatch, never parameter binding.
$preRaw=$null;$preSource=$null;$preSeed=$null;$preMask=$null;$preDirect=$null
$preProxy=$null;$preNative=$null
try
{
    $preRaw=[Drawing.Bitmap]::new($paths.Source)
    $preSource=Remove-DororongBoundaryBackground $preRaw
    $preSeed=[Drawing.Bitmap]::new($paths.Seed)
    $preMask=Import-DororongBodyMask $paths.Mask
    $preDirect=New-DirectCandidate $preSource $preSeed $preMask $constants `
        ([int]$constants.SubpixelFactor) ([double]$constants.Width)
    Assert-FillSeedContract $preSource $preSeed $preDirect.FillField 'Direct E-only fill samples'
    Assert-Equal $expectedHashes.Contour (Get-DororongCanonicalContourHash $preDirect.Contour) `
        'Oracle-only frozen E-union-C contour changed.'
    Assert-AllPixelSmoothFill $preSource $preSeed $preMask $preDirect.Contour $preDirect.FillField
    $preComponents=@(Get-IndependentProxyComponents $preMask $preSource 7 8)
    $null=Assert-ProxyMembership $preComponents $preMask.Width 'Direct E-only resize proxy'
    Assert-Equal $expectedHashes.SourceOpen (Get-BitmapPngHash $preDirect.Candidate) `
        'Direct production pipeline did not reproduce exact source-225 E-only candidate.'
    $preProxy=New-IndependentResizeProxy $preDirect.Candidate $preDirect.FillField $preComponents
    $preNative=Resize-DororongPremultiplied96 $preProxy
    Assert-Equal $expectedHashes.NativeOpen (Get-BitmapPngHash $preNative) `
        'Direct production pipeline did not reproduce exact native-96 E-only candidate through one proxy resize.'
}
finally
{
    foreach($bitmap in @($preNative,$preProxy))
    {if($null-ne$bitmap){$bitmap.Dispose()}}
    if($null-ne$preDirect-and$null-ne$preDirect.Candidate){$preDirect.Candidate.Dispose()}
    foreach($bitmap in @($preMask,$preSeed,$preSource,$preRaw))
    {if($null-ne$bitmap){$bitmap.Dispose()}}
}

if($OracleOnly)
{
    Write-Output "ORACLE-ONLY PASS sourceOpen=$($expectedHashes.SourceOpen) nativeOpen=$($expectedHashes.NativeOpen) eligibility=independent-final-mask-seed-alpha-floor-chroma-unweighted-E-union-C-distance-gt-8"
    return
}

Assert-Equal 2.5 ([double]$constants.ExposedCoverageMultiplier) 'The E-only exposed multiplier changed.'
Assert-Equal 0.0 ([double]$constants.ContinuationCoverageMultiplier) 'The E-only continuation multiplier changed.'
Assert-Equal 7 ([int]$constants.ProxyMaximumSize) 'The proxy maximum size changed.'
Assert-Equal 8 ([int]$constants.ProxyMaximumChroma) 'The proxy maximum chroma changed.'
Assert-Equal 6 ([int]$constants.ExpectedProxyComponentCount) 'The proxy component count changed.'
Assert-Equal 12 ([int]$constants.ExpectedProxyPixelCount) 'The proxy pixel count changed.'
Assert-Equal '2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54' `
    $constants.ExpectedProxyMembershipSha256 'The proxy membership identity changed.'

$candidateRoot = Join-Path $repositoryRoot '.superpowers/sdd/2026-08-28-dororong-e-only-thin-outline/runtime-integration'
$runRoot = Join-Path $candidateRoot "exact-art-$([Guid]::NewGuid().ToString('N'))"
$outputDirectory = Join-Path $runRoot 'output'
$evidenceDirectory = Join-Path $runRoot 'evidence'
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

$generatorOutput = & pwsh -NoProfile -File $paths.Generator `
    -SourcePath $paths.Source -BodyMaskPath $paths.Mask `
    -OutputDirectory $outputDirectory -EvidenceDirectory $evidenceDirectory 2>&1
Assert-Equal 0 $LASTEXITCODE "The real generator failed: $($generatorOutput -join [Environment]::NewLine)"
$generatorText=$generatorOutput-join[Environment]::NewLine
Assert-True $generatorText.Contains('proxyComponents=6',[StringComparison]::Ordinal) `
    'Generator diagnostics did not report six resize-proxy components.'
Assert-True $generatorText.Contains('proxyPixels=12',[StringComparison]::Ordinal) `
    'Generator diagnostics did not report twelve resize-proxy pixels.'
Assert-True $generatorText.Contains('proxyHash=2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54',[StringComparison]::Ordinal) `
    'Generator diagnostics did not report the canonical resize-proxy membership.'
Assert-True $generatorText.Contains('resizeOpen=1',[StringComparison]::Ordinal) `
    'Generator did not report exactly one open-frame resize.'
Assert-True $generatorText.Contains('resizeClosed=1',[StringComparison]::Ordinal) `
    'Generator did not report exactly one closed-frame resize.'
Assert-True $generatorText.Contains('resizeHalfClosed=1',[StringComparison]::Ordinal) `
    'Generator did not report exactly one half-closed-frame resize.'

$generatedOpenPath = Join-Path $outputDirectory 'dororong-canonical.png'
$generatedClosedPath = Join-Path $outputDirectory 'dororong-closed-eyes.png'
$generatedHalfClosedPath = Join-Path $outputDirectory 'dororong-half-closed-eyes.png'
$evidencePaths = [ordered]@{
    SourceOpenBaseline = Join-Path $evidenceDirectory 'source-open-baseline.png'
    SourceOpenCandidate = Join-Path $evidenceDirectory 'source-open-candidate.png'
    SourceClosedCandidate = Join-Path $evidenceDirectory 'source-closed-candidate.png'
    SourceHalfClosedCandidate = Join-Path $evidenceDirectory 'source-half-closed-candidate.png'
    NativeOpenBaseline = Join-Path $evidenceDirectory 'native-open-baseline.png'
    NativeOpenCandidate = Join-Path $evidenceDirectory 'native-open-candidate.png'
    NativeClosedCandidate = Join-Path $evidenceDirectory 'native-closed-candidate.png'
    NativeHalfClosedCandidate = Join-Path $evidenceDirectory 'native-half-closed-candidate.png'
}
Assert-ExactGeneratorEvidenceSet $evidenceDirectory $expectedHashes
foreach ($path in @($generatedOpenPath,$generatedClosedPath,$generatedHalfClosedPath) + @($evidencePaths.Values))
{ Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Generator output is missing: $path" }

Assert-Equal $expectedHashes.SourceOpen `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $evidencePaths.SourceOpenCandidate).Hash `
    'The representative source-open candidate changed.'
Assert-Equal $expectedHashes.NativeOpen `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $generatedOpenPath).Hash `
    'The representative native-open candidate changed.'
Assert-Equal $expectedHashes.NativeOpen `
    (Get-FileHash -Algorithm SHA256 -LiteralPath $evidencePaths.NativeOpenCandidate).Hash `
    'The native-open evidence differs from the runtime output.'

$raw=$null; $source=$null; $seed=$null; $mask=$null; $direct=$null
$directProxy=$null; $directNative=$null; $baselineNative=$null; $sourceBaseline=$null
$sourceOpen=$null; $sourceClosed=$null; $sourceHalfClosed=$null; $nativeBaseline=$null
$sourceOpenProxy=$null; $sourceClosedProxy=$null; $sourceHalfClosedProxy=$null; $sourceEyeBlank=$null
$sourceEyeBlankProxy=$null; $nativeEyeBlank=$null
$nativeOpen=$null; $nativeClosed=$null; $nativeHalfClosed=$null
$generatedOpen=$null; $generatedClosed=$null; $generatedHalfClosed=$null
try
{
    $raw = [Drawing.Bitmap]::new($paths.Source)
    $source = Remove-DororongBoundaryBackground $raw
    $seed = [Drawing.Bitmap]::new($paths.Seed)
    $mask = Import-DororongBodyMask $paths.Mask
    $direct = New-DirectCandidate $source $seed $mask $constants `
        ([int]$constants.SubpixelFactor) ([double]$constants.Width)
    Assert-Equal $expectedHashes.Contour (Get-DororongCanonicalContourHash $direct.Contour) `
        'The production contour geometry changed.'
    Assert-Equal '26,2,10' "$($direct.OutlineColor.R),$($direct.OutlineColor.G),$($direct.OutlineColor.B)" `
        'The median outline RGB changed.'

    Assert-AllPixelSmoothFill $source $seed $mask $direct.Contour $direct.FillField
    $proxyComponents=@(Get-IndependentProxyComponents $mask $source `
        ([int]$constants.ProxyMaximumSize) ([int]$constants.ProxyMaximumChroma))
    $null=Assert-ProxyMembership $proxyComponents $mask.Width 'Production resize proxy'
    $directProxy=New-IndependentResizeProxy $direct.Candidate $direct.FillField $proxyComponents
    $directNative = Resize-DororongPremultiplied96 $directProxy
    $baselineNative = Resize-DororongPremultiplied96 $source
    $sourceBaseline = [Drawing.Bitmap]::new($evidencePaths.SourceOpenBaseline)
    $sourceOpen = [Drawing.Bitmap]::new($evidencePaths.SourceOpenCandidate)
    $sourceClosed = [Drawing.Bitmap]::new($evidencePaths.SourceClosedCandidate)
    $sourceHalfClosed = [Drawing.Bitmap]::new($evidencePaths.SourceHalfClosedCandidate)
    $nativeBaseline = [Drawing.Bitmap]::new($evidencePaths.NativeOpenBaseline)
    $nativeOpen = [Drawing.Bitmap]::new($evidencePaths.NativeOpenCandidate)
    $nativeClosed = [Drawing.Bitmap]::new($evidencePaths.NativeClosedCandidate)
    $nativeHalfClosed = [Drawing.Bitmap]::new($evidencePaths.NativeHalfClosedCandidate)
    $generatedOpen = [Drawing.Bitmap]::new($generatedOpenPath)
    $generatedClosed = [Drawing.Bitmap]::new($generatedClosedPath)
    $generatedHalfClosed = [Drawing.Bitmap]::new($generatedHalfClosedPath)

    Assert-BitmapEqual $source $sourceBaseline 'Source-open baseline evidence differs from direct background removal.'
    Assert-BitmapEqual $direct.Candidate $sourceOpen 'Generated source-open differs from one direct production-module raster.'
    Assert-BitmapEqual $baselineNative $nativeBaseline 'Native-open baseline differs from one shared resize.'
    Assert-BitmapEqual $directNative $nativeOpen 'Generated native-open differs from one direct shared resize.'
    Assert-BitmapEqual $nativeOpen $generatedOpen 'Native-open output differs from its evidence file.'
    Assert-BitmapEqual $nativeClosed $generatedClosed 'Native-closed output differs from its evidence file.'
    Assert-BitmapEqual $nativeHalfClosed $generatedHalfClosed `
        'Native-half-closed output differs from its evidence file.'

    Assert-SourceCandidateContract $source $mask $sourceOpen 'Source-open candidate'
    Assert-SourceCandidateContract $source $mask $sourceClosed 'Source-closed candidate' -AllowEyeChanges
    Assert-SourceCandidateContract $source $mask $sourceHalfClosed 'Source-half-closed candidate' -AllowEyeChanges
    Assert-FillSeedContract $source $seed $direct.FillField 'Production fill field'
    Assert-SourceBodyEquality $sourceOpen $sourceClosed $mask 'Source candidate'
    Assert-SourceBodyEquality $sourceOpen $sourceHalfClosed $mask 'Source half-closed candidate'
    $sourceOpenProxy=New-IndependentResizeProxy $sourceOpen $direct.FillField $proxyComponents
    $sourceClosedProxy=New-IndependentResizeProxy $sourceClosed $direct.FillField $proxyComponents
    $sourceHalfClosedProxy=New-IndependentResizeProxy $sourceHalfClosed $direct.FillField $proxyComponents
    $sourceEyeBlank=New-IndependentEyeBlank $sourceOpen $source
    Assert-Equal '7B101AAAD51934C16894271EB63BBF5D930EA858C99D98A2E127C920FF95C173' `
        (Get-BitmapPixelHash $sourceEyeBlank) 'Independent source eye blank pixels changed.'
    $sourceEyeBlankProxy=New-IndependentResizeProxy $sourceEyeBlank $direct.FillField $proxyComponents
    $nativeEyeBlank=Resize-DororongPremultiplied96 $sourceEyeBlankProxy
    Assert-Equal 'F00905180F90166A6A061A8290E6DB8F4BD3EDB9DE0535BC5B3A7549F071C6AF' `
        (Get-BitmapPixelHash $nativeEyeBlank) 'Independent native eye blank pixels changed.'
    Assert-ProxyInputEqualityOutsideEyes $sourceOpenProxy $sourceClosedProxy 'Canonical resize proxy'
    Assert-ProxyInputEqualityOutsideEyes $sourceOpenProxy $sourceHalfClosedProxy `
        'Canonical half-closed resize proxy'
    Assert-BitmapEqual $directProxy $sourceOpenProxy 'Direct and generated source-open resize proxies differ.'
    Assert-AlphaZeroRgb $nativeOpen 'Native-open candidate'
    Assert-AlphaZeroRgb $nativeClosed 'Native-closed candidate'
    Assert-AlphaZeroRgb $nativeHalfClosed 'Native-half-closed candidate'
    Assert-EyeAndMouthContract $sourceOpen $sourceClosed $nativeOpen $nativeEyeBlank $nativeClosed
    $metrics = Measure-NativeBodyMetrics $nativeOpen $authority $direct.OutlineColor

    Assert-MutationRejected 'width-plus-1-over-64' 'Source-open fixed-width mutation' {
        $mutated = Invoke-DororongSubpixelOutline $source $mask $direct.FillField $direct.DistanceMap `
            $direct.OutlineColor ([double]$constants.Width + (1.0 / 64.0))
        try { Assert-BitmapEqual $direct.Candidate $mutated 'Source-open fixed-width mutation' }
        finally { $mutated.Dispose() }
    }
    Assert-MutationRejected 'factor-8-to-2' 'Source-open factor mutation' {
        $factorTwoMap = New-DororongSubpixelDistanceMap $mask $direct.Contour 2
        $mutated = Invoke-DororongSubpixelOutline $source $mask $direct.FillField $factorTwoMap `
            $direct.OutlineColor ([double]$constants.Width)
        try { Assert-BitmapEqual $direct.Candidate $mutated 'Source-open factor mutation' }
        finally { $mutated.Dispose() }
    }
    Assert-MutationRejected 'exposed-gain-2.5-to-1' 'Source-open exposed-gain mutation' {
        $module = Get-Module Dororong.SubpixelOutline
        & $module { $script:OutlineConstants.ExposedCoverageMultiplier = 1.0 }
        try
        {
            $mutated = Invoke-DororongSubpixelOutline $source $mask $direct.FillField `
                $direct.DistanceMap $direct.OutlineColor ([double]$constants.Width)
            try { Assert-BitmapEqual $direct.Candidate $mutated 'Source-open exposed-gain mutation' }
            finally { $mutated.Dispose() }
        }
        finally { & $module { $script:OutlineConstants.ExposedCoverageMultiplier = 2.5 } }
    }
    Assert-MutationRejected 'continuation-gain-0-to-1' 'Source-open continuation-gain mutation' {
        $module = Get-Module Dororong.SubpixelOutline
        & $module { $script:OutlineConstants.ContinuationCoverageMultiplier = 1.0 }
        try
        {
            $mutated = Invoke-DororongSubpixelOutline $source $mask $direct.FillField `
                $direct.DistanceMap $direct.OutlineColor ([double]$constants.Width)
            try { Assert-BitmapEqual $direct.Candidate $mutated 'Source-open continuation-gain mutation' }
            finally { $mutated.Dispose() }
        }
        finally { & $module { $script:OutlineConstants.ContinuationCoverageMultiplier = 0.0 } }
    }
    Assert-MutationRejected 'retain-eligible-seed-rgb' 'Source-open retained-seed mutation' {
        $retainedFill=[Drawing.Color[,]]$direct.FillField.Clone()
        $coordinate=@($direct.FillField.EligibleSeeds)[0]
        $retainedFill[[int]$coordinate.X,[int]$coordinate.Y]=
            $source.GetPixel([int]$coordinate.X,[int]$coordinate.Y)
        $mutated = Invoke-DororongSubpixelOutline $source $mask $retainedFill $direct.DistanceMap `
            $direct.OutlineColor ([double]$constants.Width)
        try { Assert-BitmapEqual $direct.Candidate $mutated 'Source-open retained-seed mutation' }
        finally { $mutated.Dispose() }
    }
    Invoke-FillFloorMutationContract $source $seed $mask $direct.Contour $constants
    Assert-MutationRejected 'final-mask-boundary-bit' 'Final-mask boundary mutation changed canonical contour geometry' {
        $mutatedMask = Copy-Bitmap32 $mask
        try
        {
            $mutatedMask.SetPixel(162,110,[Drawing.Color]::FromArgb(255,0,0,0))
            $endpoints = [Drawing.PointF[]]@($constants.LegalEndpoints | ForEach-Object {
                [Drawing.PointF]::new([single]$_.X,[single]$_.Y)
            })
            $mutatedContour = New-DororongVisibleContour $source $mutatedMask $endpoints
            Assert-Equal $expectedHashes.Contour (Get-DororongCanonicalContourHash $mutatedContour) `
                'Final-mask boundary mutation changed canonical contour geometry.'
        }
        finally { $mutatedMask.Dispose() }
    }
    Assert-MutationRejected 'proxy-eight-connected' 'component count changed' {
        $mutatedComponents=@(Get-IndependentProxyComponents $mask $source 7 8 -EightConnected)
        $null=Assert-ProxyMembership $mutatedComponents $mask.Width 'Eight-connected proxy mutation'
    }
    Assert-MutationRejected 'proxy-size-7-to-6' 'component count changed' {
        $mutatedComponents=@(Get-IndependentProxyComponents $mask $source 6 8)
        $null=Assert-ProxyMembership $mutatedComponents $mask.Width 'Size-six proxy mutation'
    }
    Assert-MutationRejected 'selected-proxy-pixel-chroma-to-9' 'component count changed' {
        $mutatedSource=Copy-Bitmap32 $source
        try
        {
            $index=[int]$proxyComponents[0].Indices[0]
            $x=$index%$source.Width;$y=[int][Math]::Floor($index/$source.Width)
            $alpha=$mutatedSource.GetPixel($x,$y).A
            $mutatedSource.SetPixel($x,$y,[Drawing.Color]::FromArgb($alpha,100,100,109))
            $mutatedComponents=@(Get-IndependentProxyComponents $mask $mutatedSource 7 8)
            $null=Assert-ProxyMembership $mutatedComponents $mask.Width 'Chroma-nine proxy mutation'
        }
        finally{$mutatedSource.Dispose()}
    }
    Assert-MutationRejected 'proxy-omit-component' 'component count changed' {
        $mutatedComponents=@($proxyComponents|Select-Object -Skip 1)
        $null=Assert-ProxyMembership $mutatedComponents $mask.Width 'Omitted-component proxy mutation'
    }
    Assert-MutationRejected 'proxy-membership-record' 'canonical membership changed' {
        $mutatedComponents=@($proxyComponents|ForEach-Object{
            [pscustomobject]@{
                Indices=[int[]]$_.Indices.Clone();Size=$_.Size;TouchesFrame=$_.TouchesFrame
                MinX=$_.MinX;MinY=$_.MinY;MaxX=$_.MaxX;MaxY=$_.MaxY
                MaximumChroma=$_.MaximumChroma
            }
        })
        $mutatedComponents[0].Indices[0]++
        $null=Assert-ProxyMembership $mutatedComponents $mask.Width 'Membership-record proxy mutation'
    }
    Assert-MutationRejected 'proxy-admit-frame-component' 'admitted a frame-touching component' {
        $frameComponent=[pscustomobject]@{
            Indices=[int[]]@(0);Size=1;TouchesFrame=$true
            MinX=0;MinY=0;MaxX=0;MaxY=0;MaximumChroma=0
        }
        $mutatedComponents=@($proxyComponents)+@($frameComponent)
        $null=Assert-ProxyMembership $mutatedComponents $mask.Width 'Frame-component proxy mutation'
    }
    Assert-MutationRejected 'proxy-only-open-eye-state' 'differs outside eye regions' {
        Assert-ProxyInputEqualityOutsideEyes $sourceOpenProxy $sourceClosed `
            'One-state-only proxy mutation'
    }
    Assert-MutationRejected 'second-native-resize' 'Second-resize mutation' {
        $twice=Resize-DororongPremultiplied96 $nativeOpen
        try { Assert-BitmapEqual $nativeOpen $twice 'Second-resize mutation' }
        finally { $twice.Dispose() }
    }
    Assert-MutationRejected 'protected-candidate-rgb' 'protected mask-zero artwork changed' {
        $mutated = Copy-Bitmap32 $sourceOpen
        try
        {
            $pixel = $mutated.GetPixel(52,68)
            $mutated.SetPixel(52,68,[Drawing.Color]::FromArgb($pixel.A,($pixel.R + 1),$pixel.G,$pixel.B))
            Assert-SourceCandidateContract $source $mask $mutated 'Protected RGB mutation'
        }
        finally { $mutated.Dispose() }
    }
    Assert-MutationRejected 'candidate-alpha' 'source alpha changed' {
        $mutated = Copy-Bitmap32 $sourceOpen
        try
        {
            $pixel = $mutated.GetPixel(100,180)
            $mutated.SetPixel(100,180,[Drawing.Color]::FromArgb(254,$pixel.R,$pixel.G,$pixel.B))
            Assert-SourceCandidateContract $source $mask $mutated 'Candidate alpha mutation'
        }
        finally { $mutated.Dispose() }
    }
    Assert-MutationRejected 'closed-frame-body-rgb' 'open/closed body RGB changed' {
        $mutated = Copy-Bitmap32 $sourceClosed
        try
        {
            $pixel = $mutated.GetPixel(100,180)
            $mutated.SetPixel(100,180,[Drawing.Color]::FromArgb($pixel.A,($pixel.R - 1),$pixel.G,$pixel.B))
            Assert-SourceBodyEquality $sourceOpen $mutated $mask 'Closed-frame mutation'
        }
        finally { $mutated.Dispose() }
    }

    Assert-Equal 0 $nativeOpen.GetPixel(0,0).A 'Transparent native margin was lost.'
    Assert-True ($nativeOpen.GetPixel(43,75).A -ge 240) 'Opaque native body hit probe was lost.'
    Write-Output "EXACT ART CANDIDATE sourceOpen=$($expectedHashes.SourceOpen) nativeOpen=$($expectedHashes.NativeOpen) fillSeeds=$($direct.FillField.EligibleSeedCount) nativeSpread=$($metrics.Spread) runRoot=$runRoot"
}
finally
{
    foreach ($bitmap in @($generatedHalfClosed,$generatedClosed,$generatedOpen,
        $nativeHalfClosed,$nativeClosed,$nativeOpen,$nativeEyeBlank,
        $sourceEyeBlankProxy,$sourceEyeBlank,$nativeBaseline,
        $sourceHalfClosedProxy,$sourceClosedProxy,$sourceOpenProxy,
        $sourceHalfClosed,$sourceClosed,$sourceOpen,$sourceBaseline,$baselineNative,
        $directNative,$directProxy))
    { if ($null -ne $bitmap) { $bitmap.Dispose() } }
    if ($null -ne $direct -and $null -ne $direct.Candidate) { $direct.Candidate.Dispose() }
    foreach ($bitmap in @($mask,$seed,$source,$raw))
    { if ($null -ne $bitmap) { $bitmap.Dispose() } }
}

$coreAssemblyPath = Join-Path $repositoryRoot "src/Dororong.Core/bin/$Configuration/net8.0/Dororong.Core.dll"
$appAssemblyPath = Join-Path $repositoryRoot "src/Dororong.App/bin/$Configuration/net8.0-windows/Dororong.App.dll"
Add-Type -AssemblyName PresentationFramework
Add-Type -Path $coreAssemblyPath
Add-Type -Path $appAssemblyPath
$presenter = [Dororong.App.Controls.DororongPresenter]::new()
$bodyGroup = [Windows.Controls.Canvas]$presenter.FindName('BodyGroup')
$image = [Windows.Controls.Image]$presenter.FindName('DororongImage')
Assert-Equal 108.0 $bodyGroup.Width 'BodyGroup width changed.'
Assert-Equal 96.0 $bodyGroup.Height 'BodyGroup height changed.'
Assert-Equal 18.0 ([Windows.Controls.Canvas]::GetLeft($bodyGroup)) 'BodyGroup placement changed.'
Assert-Equal 24.0 ([Windows.Controls.Canvas]::GetTop($bodyGroup)) 'BodyGroup placement changed.'
Assert-Equal 96.0 $image.Width 'The presenter resamples the native bitmap horizontally.'
Assert-Equal 96.0 $image.Height 'The presenter resamples the native bitmap vertically.'
Assert-Equal 6.0 ([Windows.Controls.Canvas]::GetLeft($image)) 'The native bitmap is not centered in BodyGroup.'

$window = [Windows.Window]::new()
$window.Width=144; $window.Height=144; $window.Left=-10000; $window.Top=-10000
$window.ShowActivated=$false; $window.ShowInTaskbar=$false
$window.WindowStyle=[Windows.WindowStyle]::None; $window.Content=$presenter
try
{
    $window.Show(); $presenter.UpdateLayout()
    $dpi = [Windows.Media.VisualTreeHelper]::GetDpi($image)
    Assert-Near 1.0 $dpi.DpiScaleX 0.000001 'Presenter target is not 96 DPI.'
    Assert-Near 96.0 $image.ActualWidth 0.000001 'Bitmap is not arranged at 96 DIPs.'
    Assert-Near 96.0 $image.ActualHeight 0.000001 'Bitmap is not arranged at 96 DIPs.'
    Assert-Equal 96 ([Windows.Media.Imaging.BitmapSource]$image.Source).PixelWidth `
        'Presented resource is not 96 pixels.'
    $opaquePoint = $image.TranslatePoint([Windows.Point]::new(43.5,75.5),$presenter)
    $hit = $presenter.InputHitTest($opaquePoint)
    Assert-True ($null -ne $hit -and $bodyGroup.IsAncestorOf($hit)) `
        'Opaque native body point did not alpha-hit-test.'
    $marginPoint = $image.TranslatePoint([Windows.Point]::new(0.25,0.25),$presenter)
    Assert-True ($null -eq $presenter.InputHitTest($marginPoint)) `
        'Transparent native margin hit-tested as opaque.'
}
finally { $window.Close() }

$stateType = [Dororong.Core.Behavior.PetState]
$facing = [Dororong.Core.Behavior.FacingDirection]::Right
foreach ($state in @($stateType::Idle,$stateType::Walk,$stateType::Curious,$stateType::Startled,
    $stateType::ClickReaction,$stateType::Dragged))
{
    $presenter.Render([Dororong.Core.Behavior.PetSnapshot]::new(
        $state,[Dororong.Core.Geometry.PointD]::new(0,0),$facing,0.1,$false,$null))
    Assert-Frame $image 'dororong-canonical.png' $state.ToString()
}
$presenter.Render([Dororong.Core.Behavior.PetSnapshot]::new(
    $stateType::Sleep,[Dororong.Core.Geometry.PointD]::new(0,0),$facing,0.1,$false,$null))
Assert-Frame $image 'dororong-closed-eyes.png' 'Sleep'
$presenter.Render([Dororong.Core.Behavior.PetSnapshot]::new(
    $stateType::Idle,[Dororong.Core.Geometry.PointD]::new(0,0),$facing,0.70,$false,$null))
Assert-Frame $image 'dororong-closed-eyes.png' 'Idle blink'
$presenter.Render([Dororong.Core.Behavior.PetSnapshot]::new(
    $stateType::Idle,[Dororong.Core.Geometry.PointD]::new(0,0),$facing,0.66,$false,$null))
Assert-Frame $image 'dororong-half-closed-eyes.png' 'Idle blink transition'

$runtimeOpenPath=Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-canonical.png'
$runtimeClosedPath=Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-closed-eyes.png'
$runtimeHalfClosedPath=Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-half-closed-eyes.png'
$runtimeOpenHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $runtimeOpenPath).Hash
$runtimeClosedHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $runtimeClosedPath).Hash
$runtimeHalfClosedHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $runtimeHalfClosedPath).Hash
$generatedClosedHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $generatedClosedPath).Hash
$generatedHalfClosedHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $generatedHalfClosedPath).Hash
if($runtimeOpenHash-ne$expectedHashes.NativeOpen-or$runtimeClosedHash-ne$generatedClosedHash-or
    $runtimeHalfClosedHash-ne$generatedHalfClosedHash)
{
    throw "Committed runtime assets are stale after all E-only candidate, invariant, mutation, and presenter checks: expectedOpen=$($expectedHashes.NativeOpen) observedOpen=$runtimeOpenHash expectedClosed=$generatedClosedHash observedClosed=$runtimeClosedHash expectedHalfClosed=$generatedHalfClosedHash observedHalfClosed=$runtimeHalfClosedHash."
}

Write-Output 'EXACT ART PASS: representative source/native reconstruction, fill provenance, source/protected/alpha invariants, open/closed body equality, reviewed eye semantics, causal mutations, 96-DPI presentation, alpha hit testing, and state mapping passed; native body optical diagnostics were recorded and did not gate PASS.'
