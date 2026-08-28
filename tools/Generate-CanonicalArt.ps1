param(
    [Parameter(Mandatory=$true)][string]$SourcePath,
    [Parameter(Mandatory=$true)][string]$BodyMaskPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$EvidenceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repositoryRoot=Split-Path -Parent $PSScriptRoot
$paths=[ordered]@{
    Source=[IO.Path]::GetFullPath($SourcePath)
    Seed=Join-Path $repositoryRoot 'tests/fixtures/dororong-body-region-seed.png'
    Mask=[IO.Path]::GetFullPath($BodyMaskPath)
    Constants=Join-Path $PSScriptRoot 'Dororong.SubpixelOutline.Constants.psd1'
    SourceRasterModule=Join-Path $PSScriptRoot 'Dororong.SourceRaster.psm1'
    SubpixelModule=Join-Path $PSScriptRoot 'Dororong.SubpixelOutline.psm1'
    OwnershipConstants=Join-Path $PSScriptRoot 'Dororong.BodyOwnership.Constants.psd1'
    OwnershipModule=Join-Path $PSScriptRoot 'Dororong.BodyOwnership.psm1'
    SubpixelTest=Join-Path $repositoryRoot 'tests/Dororong.App.SubpixelOutline.Tests.ps1'
    ExactArtTest=Join-Path $repositoryRoot 'tests/Dororong.App.ExactArt.Tests.ps1'
    Generator=$PSCommandPath
}
$expectedInputHashes=[ordered]@{
    Source='F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504'
    Seed='E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779'
    Mask='D08B3A941C662F1CBC55C486C13FD4C6CD8901DA9CD5CF8512509698219FE46F'
}
$authorityIdentity='DDF749007995B3F03781A3A51467013F406C5F7A2AA0480212523A79EF31F17F'
$ownershipIdentity='30945B766547723F9860941DEB70C74465912E2604E5EDADBBB69E91C78D708D'
$preHashes=[ordered]@{}
foreach($name in $expectedInputHashes.Keys)
{
    $preHashes[$name]=(Get-FileHash -Algorithm SHA256 -LiteralPath $paths[$name]).Hash
    if($preHashes[$name]-ne$expectedInputHashes[$name])
    {throw "Pinned $name identity changed: expected $($expectedInputHashes[$name]); observed $($preHashes[$name])."}
}

Add-Type -AssemblyName System.Drawing
Import-Module $paths.SourceRasterModule -Force
Import-Module $paths.SubpixelModule -Force
$constants=Import-PowerShellDataFile -LiteralPath $paths.Constants

$leftEyeStencilRuns=@(
    '114:43-59','115:44-60','116:44-62','117:43-63','118:43-64','119:43-64',
    '120:43-65','121:43-64','122:43-64','123:43-64','124:43-64','125:43-64',
    '126:43-64','127:43-64','128:43-64','129:43-64','130:43-64','131:44-63',
    '132:45-62','133:46-61','134:48-59','135:51-57')
$rightEyeStencilRuns=@(
    '114:92-100','115:89-103','116:86-105','117:86-106','118:86-106','119:86-106',
    '120:86-106','121:86-106','122:86-106','123:86-106','124:86-106','125:86-106',
    '126:86-106','127:86-106','128:86-106','129:86-106','130:86-106','131:86-106',
    '132:86-106','133:87-105','134:88-104','135:90-103','136:93-103','137:86-103',
    '138:86-102','139:88-102','140:89-101','141:99-101','142:99-100','143:99-100')
$closedEyeGeometry=@{
    StrokeWidth=2.50
    SubpixelFactor=8
    CurveSegments=256
    Left=@{P0=@(43.00,118.80);P1=@(54.440,124.40);P2=@(63.50,119.40)}
    Right=@{P0=@(85.75,125.60);P1=@(92.298,131.55);P2=@(103.75,126.50)}
}

function ConvertFrom-CoordinateRun([string]$Run)
{
    if($Run-notmatch'^(?<Y>\d+):(?<StartX>\d+)-(?<EndX>\d+)$')
    {throw "Invalid coordinate run '$Run'."}
    return [pscustomobject]@{Y=[int]$Matches.Y;StartX=[int]$Matches.StartX;EndX=[int]$Matches.EndX}
}

function New-CoordinateMask([string[]]$Runs,[int]$Width,[int]$Height)
{
    $mask=[bool[]]::new($Width*$Height)
    foreach($encodedRun in $Runs)
    {
        $run=ConvertFrom-CoordinateRun $encodedRun
        foreach($x in $run.StartX..$run.EndX){$mask[($run.Y*$Width)+$x]=$true}
    }
    return $mask
}

function Add-SubpixelQuadraticCurve(
    [Drawing.Bitmap]$Bitmap,[bool[]]$Membership,[hashtable]$Eye,
    [Drawing.Color]$LidColor,[double]$StrokeWidth,[int]$Factor,[int]$Segments)
{
    $p0x=[double]$Eye.P0[0];$p0y=[double]$Eye.P0[1]
    $p1x=[double]$Eye.P1[0];$p1y=[double]$Eye.P1[1]
    $p2x=[double]$Eye.P2[0];$p2y=[double]$Eye.P2[1]
    $points=[double[,]]::new($Segments+1,2)
    $minimumX=[double]::PositiveInfinity;$maximumX=[double]::NegativeInfinity
    $minimumY=[double]::PositiveInfinity;$maximumY=[double]::NegativeInfinity
    foreach($index in 0..$Segments)
    {
        $t=[double]$index/$Segments;$u=1.0-$t
        $x=($u*$u*$p0x)+(2.0*$u*$t*$p1x)+($t*$t*$p2x)
        $y=($u*$u*$p0y)+(2.0*$u*$t*$p1y)+($t*$t*$p2y)
        $points[$index,0]=$x;$points[$index,1]=$y
        $minimumX=[Math]::Min($minimumX,$x);$maximumX=[Math]::Max($maximumX,$x)
        $minimumY=[Math]::Min($minimumY,$y);$maximumY=[Math]::Max($maximumY,$y)
    }
    $radius=$StrokeWidth/2.0;$radiusSquared=$radius*$radius
    $startX=[Math]::Max(0,[int][Math]::Floor($minimumX-$radius-1.0))
    $endX=[Math]::Min($Bitmap.Width-1,[int][Math]::Ceiling($maximumX+$radius+1.0))
    $startY=[Math]::Max(0,[int][Math]::Floor($minimumY-$radius-1.0))
    $endY=[Math]::Min($Bitmap.Height-1,[int][Math]::Ceiling($maximumY+$radius+1.0))
    foreach($y in $startY..$endY)
    {
        foreach($x in $startX..$endX)
        {
            if(-not$Membership[($y*$Bitmap.Width)+$x]){continue}
            $covered=0
            foreach($sampleY in 0..($Factor-1))
            {
                $py=$y-0.5+(($sampleY+0.5)/$Factor)
                foreach($sampleX in 0..($Factor-1))
                {
                    $px=$x-0.5+(($sampleX+0.5)/$Factor)
                    $guess=[Math]::Max(0.0,[Math]::Min(1.0,($px-$p0x)/($p2x-$p0x)))
                    $centerIndex=[int][Math]::Floor($guess*$Segments)
                    $first=[Math]::Max(0,$centerIndex-7)
                    $last=[Math]::Min($Segments-1,$centerIndex+7)
                    $best=[double]::PositiveInfinity
                    foreach($segmentIndex in $first..$last)
                    {
                        $ax=$points[$segmentIndex,0];$ay=$points[$segmentIndex,1]
                        $bx=$points[($segmentIndex+1),0];$by=$points[($segmentIndex+1),1]
                        $dx=$bx-$ax;$dy=$by-$ay;$lengthSquared=($dx*$dx)+($dy*$dy)
                        $projection=if($lengthSquared-le0.0){0.0}else{
                            ((($px-$ax)*$dx)+(($py-$ay)*$dy))/$lengthSquared}
                        $projection=[Math]::Max(0.0,[Math]::Min(1.0,$projection))
                        $nearestX=$ax+($projection*$dx);$nearestY=$ay+($projection*$dy)
                        $distanceSquared=(($px-$nearestX)*($px-$nearestX))+(($py-$nearestY)*($py-$nearestY))
                        if($distanceSquared-lt$best){$best=$distanceSquared}
                    }
                    if($best-le$radiusSquared){$covered++}
                }
            }
            if($covered-eq0){continue}
            $coverage=[double]$covered/($Factor*$Factor)
            $base=$Bitmap.GetPixel($x,$y)
            $red=[int][Math]::Round($base.R+(($LidColor.R-$base.R)*$coverage),0,[MidpointRounding]::ToEven)
            $green=[int][Math]::Round($base.G+(($LidColor.G-$base.G)*$coverage),0,[MidpointRounding]::ToEven)
            $blue=[int][Math]::Round($base.B+(($LidColor.B-$base.B)*$coverage),0,[MidpointRounding]::ToEven)
            $Bitmap.SetPixel($x,$y,[Drawing.Color]::FromArgb($base.A,$red,$green,$blue))
        }
    }
}

function Get-BilinearFaceColor(
    [Drawing.Bitmap]$Bitmap,[int]$X,[int]$Y,[hashtable]$Eye)
{
    $topLeft=$Bitmap.GetPixel($Eye.TopLeftX,$Eye.TopLeftY)
    $topRight=$Bitmap.GetPixel($Eye.TopRightX,$Eye.TopRightY)
    $bottomLeft=$Bitmap.GetPixel($Eye.BottomLeftX,$Eye.BottomLeftY)
    $bottomRight=$Bitmap.GetPixel($Eye.BottomRightX,$Eye.BottomRightY)
    $xRatio=($X-$Eye.MinimumX)/($Eye.MaximumX-$Eye.MinimumX)
    $yRatio=($Y-$Eye.MinimumY)/($Eye.MaximumY-$Eye.MinimumY)
    $channels=foreach($channel in @('R','G','B'))
    {
        $top=$topLeft.$channel+(($topRight.$channel-$topLeft.$channel)*$xRatio)
        $bottom=$bottomLeft.$channel+(($bottomRight.$channel-$bottomLeft.$channel)*$xRatio)
        [Math]::Round($top+(($bottom-$top)*$yRatio),0,[MidpointRounding]::ToEven)
    }
    return [Drawing.Color]::FromArgb(255,$channels[0],$channels[1],$channels[2])
}

function New-ClosedEyeFrame([Drawing.Bitmap]$Open,[Drawing.Bitmap]$FaceSource)
{
    $closed=$Open.Clone(
        [Drawing.Rectangle]::new(0,0,$Open.Width,$Open.Height),
        [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        foreach($eye in @(
            @{Runs=$leftEyeStencilRuns;MinimumX=43;MaximumX=64;MinimumY=114;MaximumY=135
                TopLeftX=42;TopLeftY=137;TopRightX=66;TopRightY=136
                BottomLeftX=48;BottomLeftY=143;BottomRightX=64;BottomRightY=143},
            @{Runs=$rightEyeStencilRuns;MinimumX=86;MaximumX=106;MinimumY=114;MaximumY=143
                TopLeftX=65;TopLeftY=134;TopRightX=106;TopRightY=142
                BottomLeftX=86;BottomLeftY=144;BottomRightX=106;BottomRightY=144}))
        {
            foreach($encodedRun in $eye.Runs)
            {
                $run=ConvertFrom-CoordinateRun $encodedRun
                foreach($x in $run.StartX..$run.EndX)
                {
                    $face=Get-BilinearFaceColor $FaceSource $x $run.Y $eye
                    $alpha=$closed.GetPixel($x,$run.Y).A
                    $closed.SetPixel($x,$run.Y,[Drawing.Color]::FromArgb(
                        $alpha,$face.R,$face.G,$face.B))
                }
            }
        }
        $lidColor=$Open.GetPixel(43,118)
        $leftMask=New-CoordinateMask $leftEyeStencilRuns $closed.Width $closed.Height
        $rightMask=New-CoordinateMask $rightEyeStencilRuns $closed.Width $closed.Height
        Add-SubpixelQuadraticCurve $closed $leftMask $closedEyeGeometry.Left $lidColor `
            ([double]$closedEyeGeometry.StrokeWidth) ([int]$closedEyeGeometry.SubpixelFactor) `
            ([int]$closedEyeGeometry.CurveSegments)
        Add-SubpixelQuadraticCurve $closed $rightMask $closedEyeGeometry.Right $lidColor `
            ([double]$closedEyeGeometry.StrokeWidth) ([int]$closedEyeGeometry.SubpixelFactor) `
            ([int]$closedEyeGeometry.CurveSegments)
        return $closed
    }
    catch{$closed.Dispose();throw}
}

function Get-Median([int[]]$Values)
{
    $sorted=@($Values|Sort-Object);$middle=[int]($sorted.Count/2)
    if(($sorted.Count%2)-eq0)
    {return [int][Math]::Round(([double]$sorted[$middle-1]+$sorted[$middle])/2.0,0,[MidpointRounding]::ToEven)}
    return [int]$sorted[$middle]
}

function Get-OutlineColor([Drawing.Bitmap]$Bitmap,[object[]]$Samples)
{
    $red=[Collections.Generic.List[int]]::new();$green=[Collections.Generic.List[int]]::new()
    $blue=[Collections.Generic.List[int]]::new()
    foreach($sample in $Samples)
    {
        $pixel=$Bitmap.GetPixel([int]$sample[0],[int]$sample[1])
        $red.Add($pixel.R);$green.Add($pixel.G);$blue.Add($pixel.B)
    }
    return [Drawing.Color]::FromArgb(255,
        (Get-Median $red.ToArray()),(Get-Median $green.ToArray()),(Get-Median $blue.ToArray()))
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

function Save-Png([Drawing.Bitmap]$Bitmap,[string]$Path)
{$Bitmap.Save($Path,[Drawing.Imaging.ImageFormat]::Png)}

New-Item -ItemType Directory -Force -Path $OutputDirectory|Out-Null
if($EvidenceDirectory){New-Item -ItemType Directory -Force -Path $EvidenceDirectory|Out-Null}
$raw=$null;$source=$null;$seed=$null;$mask=$null;$open=$null;$closed=$null
$openProxy=$null;$closedProxy=$null;$nativeOpen=$null;$nativeClosed=$null;$nativeBaseline=$null
try
{
    $raw=[Drawing.Bitmap]::new($paths.Source)
    if($raw.Width-ne225-or$raw.Height-ne225)
    {throw "The canonical source must be 225x225; observed $($raw.Width)x$($raw.Height)."}
    $source=Remove-DororongBoundaryBackground $raw
    $seed=[Drawing.Bitmap]::new($paths.Seed)
    $mask=Import-DororongBodyMask $paths.Mask
    $endpoints=[Drawing.PointF[]]@($constants.LegalEndpoints|ForEach-Object{
        [Drawing.PointF]::new([single]$_.X,[single]$_.Y)})
    $contour=New-DororongVisibleContour $source $mask $endpoints
    $fill=New-DororongFillField $source $seed $mask $contour
    $distanceMap=New-DororongSubpixelDistanceMap $mask $contour ([int]$constants.SubpixelFactor)
    $outline=Get-OutlineColor $source @($constants.OutlineSamples)
    $open=Invoke-DororongSubpixelOutline $source $mask $fill $distanceMap $outline ([double]$constants.Width)
    $closed=New-ClosedEyeFrame $open $source

    $proxyComponents=@(Get-DororongResizeProxyComponents $mask $source `
        ([int]$constants.ProxyMaximumSize) ([int]$constants.ProxyMaximumChroma))
    $proxyPixelCount=(@($proxyComponents|ForEach-Object Size)|Measure-Object -Sum).Sum
    $membership=Get-DororongResizeProxyMembership $proxyComponents $mask.Width
    if($proxyComponents.Count-ne[int]$constants.ExpectedProxyComponentCount-or
        $proxyPixelCount-ne[int]$constants.ExpectedProxyPixelCount-or
        $membership.Hash-ne[string]$constants.ExpectedProxyMembershipSha256)
    {throw "Resize-proxy identity changed: components=$($proxyComponents.Count), pixels=$proxyPixelCount, hash=$($membership.Hash)."}
    $openProxy=New-DororongResizeProxy $open $fill $proxyComponents
    $closedProxy=New-DororongResizeProxy $closed $fill $proxyComponents
    $nativeOpen=Resize-DororongPremultiplied96 $openProxy
    $nativeClosed=Resize-DororongPremultiplied96 $closedProxy
    Save-Png $nativeOpen (Join-Path $OutputDirectory 'dororong-canonical.png')
    Save-Png $nativeClosed (Join-Path $OutputDirectory 'dororong-closed-eyes.png')

    if($EvidenceDirectory)
    {
        $nativeBaseline=Resize-DororongPremultiplied96 $source
        Save-Png $source (Join-Path $EvidenceDirectory 'source-open-baseline.png')
        Save-Png $open (Join-Path $EvidenceDirectory 'source-open-candidate.png')
        Save-Png $closed (Join-Path $EvidenceDirectory 'source-closed-candidate.png')
        Save-Png $nativeBaseline (Join-Path $EvidenceDirectory 'native-open-baseline.png')
        Save-Png $nativeOpen (Join-Path $EvidenceDirectory 'native-open-candidate.png')
        Save-Png $nativeClosed (Join-Path $EvidenceDirectory 'native-closed-candidate.png')
    }

    $widthText=([double]$constants.Width).ToString('R',[Globalization.CultureInfo]::InvariantCulture)
    $halfText=([double]$constants.Width/2.0).ToString('R',[Globalization.CultureInfo]::InvariantCulture)
    $endpointText=@($constants.LegalEndpoints|ForEach-Object{"$($_.X),$($_.Y)"})-join'|'
    Write-Output "GENERATOR INPUT source=$($preHashes.Source) seed=$($preHashes.Seed) mask=$($preHashes.Mask) ownership=$ownershipIdentity authority=$authorityIdentity constants=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.Constants).Hash) sourceRasterModule=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.SourceRasterModule).Hash) subpixelModule=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.SubpixelModule).Hash) ownershipConstants=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.OwnershipConstants).Hash) ownershipModule=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.OwnershipModule).Hash) subpixelTest=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.SubpixelTest).Hash) exactArtTest=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.ExactArtTest).Hash) generator=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.Generator).Hash)"
    Write-Output "GENERATOR GEOMETRY factor=$($constants.SubpixelFactor) width=$widthText halfWidth=$halfText eMultiplier=$($constants.ExposedCoverageMultiplier) cMultiplier=$($constants.ContinuationCoverageMultiplier) outlineRgb=$($outline.R),$($outline.G),$($outline.B) segments=$(@($contour.Segments).Count) exposed=$($contour.ExposedSegmentCount) continuations=$($contour.ContinuationSegmentCount) contourHash=$(Get-DororongCanonicalContourHash $contour) endpoints=$endpointText eligibleSeeds=$($fill.EligibleSeedCount)"
    Write-Output "GENERATOR PROXY proxyComponents=$($proxyComponents.Count) proxyPixels=$proxyPixelCount proxyHash=$($membership.Hash) proxyOpen=$(Get-BitmapPngHash $openProxy) proxyClosed=$(Get-BitmapPngHash $closedProxy)"
    Write-Output "GENERATOR OUTPUT sourceOpen=$(Get-BitmapPngHash $open) sourceClosed=$(Get-BitmapPngHash $closed) nativeOpen=$(Get-BitmapPngHash $nativeOpen) nativeClosed=$(Get-BitmapPngHash $nativeClosed) resizeOpen=1 resizeClosed=1"
}
finally
{
    foreach($bitmap in @($nativeBaseline,$nativeClosed,$nativeOpen,$closedProxy,$openProxy,$closed,$open,$mask,$seed,$source,$raw))
    {if($null-ne$bitmap){$bitmap.Dispose()}}
    foreach($name in $expectedInputHashes.Keys)
    {
        $post=(Get-FileHash -Algorithm SHA256 -LiteralPath $paths[$name]).Hash
        if($preHashes[$name]-ne$post){throw "Pinned $name changed during generation."}
        Write-Output "PROTECTED HASH name=$name pre=$($preHashes[$name]) post=$post"
    }
}
