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
    AuthoredOpen=Join-Path $repositoryRoot 'src/Dororong.App/Assets/frame-sources/dororong-canonical.png'
    AuthoredHalf=Join-Path $repositoryRoot 'src/Dororong.App/Assets/frame-sources/dororong-blink-squint.png'
    AuthoredClosed=Join-Path $repositoryRoot 'src/Dororong.App/Assets/frame-sources/dororong-closed-eyes.png'
}
$expectedInputHashes=[ordered]@{
    Source='F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504'
    Seed='E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779'
    Mask='D08B3A941C662F1CBC55C486C13FD4C6CD8901DA9CD5CF8512509698219FE46F'
    AuthoredOpen='699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78'
    AuthoredHalf='2A733093AC35B9678CBB90833272498C7BE755F77A574271DAE64FCE80FC90E2'
    AuthoredClosed='0D20EED5873A7E4277D6ED539474D875C9B8DF79998B1EFD74F46AA87662F488'
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
$nativeOpen=$null;$nativeBaseline=$null;$derivedSquint=$null;$derivedClosed=$null
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
    Copy-AuthoredAsset $paths.AuthoredOpen (Join-Path $OutputDirectory 'dororong-canonical.png')
    Copy-AuthoredAsset $paths.AuthoredHalf (Join-Path $OutputDirectory 'dororong-blink-squint.png')
    Copy-AuthoredAsset $paths.AuthoredClosed (Join-Path $OutputDirectory 'dororong-closed-eyes.png')

    if($EvidenceDirectory)
    {
        $nativeBaseline=Resize-DororongPremultiplied96 $source
        Save-Png $source (Join-Path $EvidenceDirectory 'source-open-baseline.png')
        Save-Png $open (Join-Path $EvidenceDirectory 'source-open-candidate.png')
        Save-Png $nativeBaseline (Join-Path $EvidenceDirectory 'native-open-baseline.png')
        Copy-AuthoredAsset $paths.AuthoredOpen (Join-Path $EvidenceDirectory 'native-open-candidate.png')
        Copy-AuthoredAsset $paths.AuthoredHalf (Join-Path $EvidenceDirectory 'native-squint-candidate.png')
        Copy-AuthoredAsset $paths.AuthoredClosed (Join-Path $EvidenceDirectory 'native-closed-candidate.png')
        $authoredOpen=[Drawing.Bitmap]::new($paths.AuthoredOpen)
        try { Save-NearestNeighborEvidence $authoredOpen (Join-Path $EvidenceDirectory 'native-open-nearest-8x.png') }
        finally { $authoredOpen.Dispose() }
        $derivedSquint=[Drawing.Bitmap]::new($paths.AuthoredHalf)
        $derivedClosed=[Drawing.Bitmap]::new($paths.AuthoredClosed)
        Save-NearestNeighborEvidence $derivedSquint (Join-Path $EvidenceDirectory 'native-squint-nearest-8x.png')
        Save-NearestNeighborEvidence $derivedClosed (Join-Path $EvidenceDirectory 'native-closed-nearest-8x.png')
    }

    $widthText=([double]$constants.Width).ToString('R',[Globalization.CultureInfo]::InvariantCulture)
    $halfText=([double]$constants.Width/2.0).ToString('R',[Globalization.CultureInfo]::InvariantCulture)
    $endpointText=@($constants.LegalEndpoints|ForEach-Object{"$($_.X),$($_.Y)"})-join'|'
    Write-Output "GENERATOR INPUT source=$($preHashes.Source) seed=$($preHashes.Seed) mask=$($preHashes.Mask) ownership=$ownershipIdentity authority=$authorityIdentity constants=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.Constants).Hash) sourceRasterModule=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.SourceRasterModule).Hash) subpixelModule=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.SubpixelModule).Hash) ownershipConstants=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.OwnershipConstants).Hash) ownershipModule=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.OwnershipModule).Hash) subpixelTest=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.SubpixelTest).Hash) exactArtTest=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.ExactArtTest).Hash) generator=$((Get-FileHash -Algorithm SHA256 -LiteralPath $paths.Generator).Hash)"
    Write-Output "GENERATOR GEOMETRY factor=$($constants.SubpixelFactor) width=$widthText halfWidth=$halfText eMultiplier=$($constants.ExposedCoverageMultiplier) cMultiplier=$($constants.ContinuationCoverageMultiplier) outlineRgb=$($outline.R),$($outline.G),$($outline.B) segments=$(@($contour.Segments).Count) exposed=$($contour.ExposedSegmentCount) continuations=$($contour.ContinuationSegmentCount) contourHash=$(Get-DororongCanonicalContourHash $contour) endpoints=$endpointText eligibleSeeds=$($fill.EligibleSeedCount)"
    Write-Output "GENERATOR PROXY proxyComponents=$($proxyComponents.Count) proxyPixels=$proxyPixelCount proxyHash=$($membership.Hash) proxyOpen=$(Get-BitmapPngHash $openProxy)"
    Write-Output "GENERATOR OUTPUT sourceOpen=$(Get-BitmapPngHash $open) generatedOpen=$(Get-BitmapPngHash $nativeOpen) authoredOpen=$($preHashes.AuthoredOpen) authoredHalf=$($preHashes.AuthoredHalf) authoredClosed=$($preHashes.AuthoredClosed) authoredFrames=3"
}
finally
{
    foreach($bitmap in @($derivedClosed,$derivedSquint,$nativeBaseline,
        $nativeOpen,$openProxy,$open,$mask,$seed,$source,$raw))
    {if($null-ne$bitmap){$bitmap.Dispose()}}
    foreach($name in $expectedInputHashes.Keys)
    {
        $post=(Get-FileHash -Algorithm SHA256 -LiteralPath $paths[$name]).Hash
        if($post-ne$preHashes[$name])
        {throw "Pinned $name was modified during generation: before=$($preHashes[$name]) after=$post."}
    }
}
