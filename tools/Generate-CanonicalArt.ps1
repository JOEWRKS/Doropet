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
    Authored70=Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-eyes-70-open.png'
    Authored25=Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-eyes-25-open.png'
    AuthoredClosed=Join-Path $repositoryRoot 'src/Dororong.App/Assets/dororong-closed-eyes.png'
}
$expectedInputHashes=[ordered]@{
    Source='F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504'
    Seed='E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779'
    Mask='D08B3A941C662F1CBC55C486C13FD4C6CD8901DA9CD5CF8512509698219FE46F'
    Authored70='26300935CA7F86AC0B78A240C9B4F7818A2814E57A1041B25B324DF1F0B0CEAC'
    Authored25='9115B9AE003FD26E8191084C3232760985A9ED43E098C33C739B2A755857D99B'
    AuthoredClosed='4E12486A490EB134D8405259CB4DF6A847733559133CA9801DC506E292AEFD8E'
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

function Save-NearestNeighborEvidence([Drawing.Bitmap]$Bitmap,[string]$Path,[int]$Factor=8)
{
    $enlarged=[Drawing.Bitmap]::new(
        $Bitmap.Width*$Factor,$Bitmap.Height*$Factor,
        [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        foreach($y in 0..($Bitmap.Height-1)){foreach($x in 0..($Bitmap.Width-1))
        {
            $pixel=$Bitmap.GetPixel($x,$y)
            foreach($dy in 0..($Factor-1)){foreach($dx in 0..($Factor-1))
            {$enlarged.SetPixel(($x*$Factor)+$dx,($y*$Factor)+$dy,$pixel)}}
        }}
        Save-Png $enlarged $Path
    }
    finally{$enlarged.Dispose()}
}

function Copy-AuthoredAsset([string]$Source,[string]$Destination)
{
    if(Test-Path -LiteralPath $Destination)
    {throw "Refusing to overwrite generated output '$Destination'."}
    [IO.File]::Copy($Source,$Destination)
}

New-Item -ItemType Directory -Force -Path $OutputDirectory|Out-Null
if($EvidenceDirectory){New-Item -ItemType Directory -Force -Path $EvidenceDirectory|Out-Null}
$raw=$null;$source=$null;$seed=$null;$mask=$null;$open=$null;$openProxy=$null
$nativeOpen=$null;$nativeBaseline=$null;$authored70=$null;$authored25=$null;$authoredClosed=$null
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

    $proxyComponents=@(Get-DororongResizeProxyComponents $mask $source `
        ([int]$constants.ProxyMaximumSize) ([int]$constants.ProxyMaximumChroma))
    $proxyPixelCount=(@($proxyComponents|ForEach-Object Size)|Measure-Object -Sum).Sum
    $membership=Get-DororongResizeProxyMembership $proxyComponents $mask.Width
    if($proxyComponents.Count-ne[int]$constants.ExpectedProxyComponentCount-or
        $proxyPixelCount-ne[int]$constants.ExpectedProxyPixelCount-or
        $membership.Hash-ne[string]$constants.ExpectedProxyMembershipSha256)
    {throw "Resize-proxy identity changed: components=$($proxyComponents.Count), pixels=$proxyPixelCount, hash=$($membership.Hash)."}
    $openProxy=New-DororongResizeProxy $open $fill $proxyComponents
    $nativeOpen=Resize-DororongPremultiplied96 $openProxy
    Save-Png $nativeOpen (Join-Path $OutputDirectory 'dororong-canonical.png')
    Copy-AuthoredAsset $paths.Authored70 (Join-Path $OutputDirectory 'dororong-eyes-70-open.png')
    Copy-AuthoredAsset $paths.Authored25 (Join-Path $OutputDirectory 'dororong-eyes-25-open.png')
    Copy-AuthoredAsset $paths.AuthoredClosed (Join-Path $OutputDirectory 'dororong-closed-eyes.png')

    if($EvidenceDirectory)
    {
        $nativeBaseline=Resize-DororongPremultiplied96 $source
        Save-Png $source (Join-Path $EvidenceDirectory 'source-open-baseline.png')
        Save-Png $open (Join-Path $EvidenceDirectory 'source-open-candidate.png')
        Save-Png $nativeBaseline (Join-Path $EvidenceDirectory 'native-open-baseline.png')
        Save-Png $nativeOpen (Join-Path $EvidenceDirectory 'native-open-candidate.png')
        Copy-AuthoredAsset $paths.Authored70 (Join-Path $EvidenceDirectory 'native-eyes-70-open-candidate.png')
        Copy-AuthoredAsset $paths.Authored25 (Join-Path $EvidenceDirectory 'native-eyes-25-open-candidate.png')
        Copy-AuthoredAsset $paths.AuthoredClosed (Join-Path $EvidenceDirectory 'native-closed-candidate.png')
        Save-NearestNeighborEvidence $nativeOpen (Join-Path $EvidenceDirectory 'native-open-nearest-8x.png')
        $authored70=[Drawing.Bitmap]::new($paths.Authored70)
        $authored25=[Drawing.Bitmap]::new($paths.Authored25)
        $authoredClosed=[Drawing.Bitmap]::new($paths.AuthoredClosed)
        Save-NearestNeighborEvidence $authored70 (Join-Path $EvidenceDirectory 'native-eyes-70-open-nearest-8x.png')
        Save-NearestNeighborEvidence $authored25 (Join-Path $EvidenceDirectory 'native-eyes-25-open-nearest-8x.png')
        Save-NearestNeighborEvidence $authoredClosed (Join-Path $EvidenceDirectory 'native-closed-nearest-8x.png')
    }

    $widthText=([double]$constants.Width).ToString('R',[Globalization.CultureInfo]::InvariantCulture)
    $halfText=([double]$constants.Width/2.0).ToString('R',[Globalization.CultureInfo]::InvariantCulture)
    $endpointText=@($constants.LegalEndpoints|ForEach-Object{"$($_.X),$($_.Y)"})-join'|'
    Write-Output "GENERATOR INPUT source=$($preHashes.Source) seed=$($preHashes.Seed) mask=$($preHashes.Mask) ownership=$ownershipIdentity authority=$authorityIdentity constants=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.Constants).Hash) sourceRasterModule=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.SourceRasterModule).Hash) subpixelModule=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.SubpixelModule).Hash) ownershipConstants=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.OwnershipConstants).Hash) ownershipModule=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.OwnershipModule).Hash) subpixelTest=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.SubpixelTest).Hash) exactArtTest=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.ExactArtTest).Hash) generator=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.Generator).Hash)"
    Write-Output "GENERATOR GEOMETRY factor=$($constants.SubpixelFactor) width=$widthText halfWidth=$halfText eMultiplier=$($constants.ExposedCoverageMultiplier) cMultiplier=$($constants.ContinuationCoverageMultiplier) outlineRgb=$($outline.R),$($outline.G),$($outline.B) segments=$(@($contour.Segments).Count) exposed=$($contour.ExposedSegmentCount) continuations=$($contour.ContinuationSegmentCount) contourHash=$(Get-DororongCanonicalContourHash $contour) endpoints=$endpointText eligibleSeeds=$($fill.EligibleSeedCount)"
    Write-Output "GENERATOR PROXY proxyComponents=$($proxyComponents.Count) proxyPixels=$proxyPixelCount proxyHash=$($membership.Hash) proxyOpen=$(Get-BitmapPngHash $openProxy)"
    Write-Output "GENERATOR OUTPUT sourceOpen=$(Get-BitmapPngHash $open) nativeOpen=$(Get-BitmapPngHash $nativeOpen) authored70=$($preHashes.Authored70) authored25=$($preHashes.Authored25) authoredClosed=$($preHashes.AuthoredClosed) resizeOpen=1 authoredNativeCopies=3"
}
finally
{
    foreach($bitmap in @($authoredClosed,$authored25,$authored70,$nativeBaseline,
        $nativeOpen,$openProxy,$open,$mask,$seed,$source,$raw))
    {if($null-ne$bitmap){$bitmap.Dispose()}}
    foreach($name in $expectedInputHashes.Keys)
    {
        $post=(Get-FileHash -Algorithm SHA256 -LiteralPath $paths[$name]).Hash
        if($post-ne$preHashes[$name])
        {throw "Pinned $name was modified during generation: before=$($preHashes[$name]) after=$post."}
    }
}
