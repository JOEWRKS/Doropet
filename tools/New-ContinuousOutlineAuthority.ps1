[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$MaskPath,

    [Parameter(Mandatory = $true)]
    [string]$NativePath,

    [Parameter(Mandatory = $true)]
    [string]$AuthorityPath,

    [Parameter(Mandatory = $true)]
    [string]$EvidenceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$expectedSourceHash = 'F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504'
$expectedMaskHash = 'E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779'
$expectedNativeHash = '611A1367E92C37659CF63A549656BCE01EEDEF5DE3CA348C6FADFB98A5D88DC3'
$expectedAuthorityHash = 'D7F947518118805862404EE47459E9533D2DCCBB98B841C8B106F3EE7694D639'

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition)
    { throw $Message }
}

function Assert-Hash([string]$Path, [string]$Expected, [string]$Label)
{
    Assert-True (Test-Path -LiteralPath $Path -PathType Leaf) "$Label is missing."
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
    if ($actual -ne $Expected)
    { throw "$Label hash changed. Expected '$Expected', observed '$actual'." }
}

function Get-Rec709Luminance([System.Drawing.Color]$Color)
{
    return (0.2126*$Color.R)+(0.7152*$Color.G)+(0.0722*$Color.B)
}

function Assert-Normal(
    [hashtable]$Entry,
    [string]$Field,
    [System.Drawing.Bitmap]$Bitmap,
    [string]$Label)
{
    Assert-True $Entry.ContainsKey($Field) "$Label is missing $Field."
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

function Assert-ReferencePoint(
    [hashtable]$Entry,
    [string]$Field,
    [System.Drawing.Bitmap]$Bitmap,
    [string]$Label)
{
    Assert-True $Entry.ContainsKey($Field) "$Label is missing $Field."
    $point = @($Entry[$Field])
    Assert-True ($point.Count -eq 2) "$Label $Field is not an X/Y pair."
    Assert-True ($point[0] -is [int] -and $point[1] -is [int]) `
        "$Label $Field contains a non-integer coordinate."
    $x=[int]$point[0]; $y=[int]$point[1]
    Assert-True ($x -ge 0 -and $x -lt $Bitmap.Width -and $y -ge 0 -and $y -lt $Bitmap.Height) `
        "$Label $Field coordinate ($x,$y) is outside the reference bitmap."
    Assert-True ($Bitmap.GetPixel($x,$y).A -eq 255) `
        "$Label $Field coordinate ($x,$y) is not opaque."
}

function Assert-FillExceedsInk(
    [hashtable]$Entry,
    [string]$FillField,
    [string]$InkField,
    [System.Drawing.Bitmap]$Bitmap,
    [string]$Label,
    [string]$Surface)
{
    $fillPoint=@($Entry[$FillField]); $inkPoint=@($Entry[$InkField])
    $fill=$Bitmap.GetPixel([int]$fillPoint[0],[int]$fillPoint[1])
    $ink=$Bitmap.GetPixel([int]$inkPoint[0],[int]$inkPoint[1])
    Assert-True ((Get-Rec709Luminance $fill) -gt (Get-Rec709Luminance $ink)) `
        "$Label $Surface fill luminance must exceed ink luminance."
}

function New-OverlayBitmap([System.Drawing.Bitmap]$Reference)
{
    $overlay=[System.Drawing.Bitmap]::new(
        $Reference.Width,$Reference.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics=[System.Drawing.Graphics]::FromImage($overlay)
    try
    {
        $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.DrawImageUnscaled($Reference,0,0)
    }
    finally
    { $graphics.Dispose() }
    return $overlay
}

function Draw-EndpointLabel(
    [System.Drawing.Graphics]$Graphics,
    [string]$Text,
    [single]$X,
    [single]$Y,
    [System.Drawing.Font]$Font,
    [System.Drawing.Brush]$Brush)
{
    $textSize=$Graphics.MeasureString($Text,$Font)
    $labelX=[Math]::Clamp($X+1.5,0.0,[double]($Graphics.VisibleClipBounds.Width-$textSize.Width))
    $labelY=[Math]::Clamp($Y-$textSize.Height-1.0,0.0,[double]($Graphics.VisibleClipBounds.Height-$textSize.Height))
    $background=[System.Drawing.RectangleF]::new(
        [single]$labelX,[single]$labelY,[single]$textSize.Width,[single]$textSize.Height)
    $Graphics.FillRectangle([System.Drawing.Brushes]::White,$background)
    $Graphics.DrawString($Text,$Font,$Brush,[single]$labelX,[single]$labelY)
}

function Draw-Normal(
    [System.Drawing.Graphics]$Graphics,
    [hashtable]$Normal,
    [string]$Label,
    [System.Drawing.Pen]$Pen,
    [System.Drawing.Brush]$Brush,
    [System.Drawing.Font]$Font,
    [single]$MarkerRadius)
{
    $x1=[single]($Normal.X1Eighth/8.0); $y1=[single]($Normal.Y1Eighth/8.0)
    $x2=[single]($Normal.X2Eighth/8.0); $y2=[single]($Normal.Y2Eighth/8.0)
    $Graphics.DrawLine($Pen,$x1,$y1,$x2,$y2)
    $diameter=2.0*$MarkerRadius
    $Graphics.FillEllipse($Brush,$x1-$MarkerRadius,$y1-$MarkerRadius,$diameter,$diameter)
    $Graphics.FillEllipse($Brush,$x2-$MarkerRadius,$y2-$MarkerRadius,$diameter,$diameter)
    Draw-EndpointLabel $Graphics "${Label}a" $x1 $y1 $Font $Brush
    Draw-EndpointLabel $Graphics "${Label}b" $x2 $y2 $Font $Brush
}

function Draw-ReferencePoint(
    [System.Drawing.Graphics]$Graphics,
    [object]$Point,
    [System.Drawing.Brush]$Brush,
    [single]$Radius)
{
    $coordinates=@($Point)
    $x=[single]$coordinates[0]; $y=[single]$coordinates[1]
    $diameter=2.0*$Radius
    $Graphics.FillEllipse([System.Drawing.Brushes]::White,
        $x-$Radius-0.5,$y-$Radius-0.5,$diameter+1.0,$diameter+1.0)
    $Graphics.FillEllipse($Brush,$x-$Radius,$y-$Radius,$diameter,$diameter)
}

function Add-AuthorityDrawing(
    [System.Drawing.Bitmap]$Overlay,
    [object[]]$HairAnchors,
    [object[]]$BodyNormals,
    [string]$NormalPrefix,
    [string]$FillPrefix,
    [string]$InkPrefix,
    [bool]$NativeScale)
{
    $graphics=[System.Drawing.Graphics]::FromImage($Overlay)
    $hairPen=$null; $bodyPen=$null; $font=$null
    try
    {
        $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $graphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
        $lineWidth=if($NativeScale){0.8}else{1.2}
        $markerRadius=if($NativeScale){0.8}else{1.2}
        $pointRadius=if($NativeScale){0.9}else{1.4}
        $fontSize=if($NativeScale){3.2}else{5.0}
        $hairColor=[System.Drawing.Color]::FromArgb(255,0,90,255)
        $bodyColor=[System.Drawing.Color]::FromArgb(255,230,25,35)
        $hairBrush=[System.Drawing.SolidBrush]::new($hairColor)
        $bodyBrush=[System.Drawing.SolidBrush]::new($bodyColor)
        try
        {
            $hairPen=[System.Drawing.Pen]::new($hairColor,[single]$lineWidth)
            $bodyPen=[System.Drawing.Pen]::new($bodyColor,[single]$lineWidth)
            $font=[System.Drawing.Font]::new(
                [System.Drawing.FontFamily]::GenericSansSerif,[single]$fontSize,
                [System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
            for($index=0;$index -lt $HairAnchors.Count;$index++)
            {
                $entry=$HairAnchors[$index]
                Draw-Normal $graphics $entry[$NormalPrefix] "H$($index+1)" `
                    $hairPen $hairBrush $font $markerRadius
                Draw-ReferencePoint $graphics $entry[$FillPrefix] `
                    ([System.Drawing.Brushes]::LimeGreen) $pointRadius
                Draw-ReferencePoint $graphics $entry[$InkPrefix] `
                    ([System.Drawing.Brushes]::Black) $pointRadius
            }
            for($index=0;$index -lt $BodyNormals.Count;$index++)
            {
                $entry=$BodyNormals[$index]
                Draw-Normal $graphics $entry[$NormalPrefix] "B$($index+1)" `
                    $bodyPen $bodyBrush $font $markerRadius
                Draw-ReferencePoint $graphics $entry[$FillPrefix] `
                    ([System.Drawing.Brushes]::LimeGreen) $pointRadius
            }
        }
        finally
        {
            if($null-ne$font){$font.Dispose()}
            if($null-ne$bodyPen){$bodyPen.Dispose()}
            if($null-ne$hairPen){$hairPen.Dispose()}
            $bodyBrush.Dispose()
            $hairBrush.Dispose()
        }
    }
    finally
    { $graphics.Dispose() }
}

function Save-NearestNeighbor(
    [System.Drawing.Bitmap]$Bitmap,
    [string]$Path,
    [int]$Scale)
{
    $scaled=[System.Drawing.Bitmap]::new(
        $Bitmap.Width*$Scale,$Bitmap.Height*$Scale,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try
    {
        $graphics=[System.Drawing.Graphics]::FromImage($scaled)
        try
        {
            $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.DrawImage(
                $Bitmap,
                [System.Drawing.Rectangle]::new(0,0,$scaled.Width,$scaled.Height),
                [System.Drawing.Rectangle]::new(0,0,$Bitmap.Width,$Bitmap.Height),
                [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally
        { $graphics.Dispose() }
        $scaled.Save($Path,[System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally
    { $scaled.Dispose() }
}

$fullSourcePath=[System.IO.Path]::GetFullPath($SourcePath)
$fullMaskPath=[System.IO.Path]::GetFullPath($MaskPath)
$fullNativePath=[System.IO.Path]::GetFullPath($NativePath)
$fullAuthorityPath=[System.IO.Path]::GetFullPath($AuthorityPath)
$fullEvidenceDirectory=[System.IO.Path]::GetFullPath($EvidenceDirectory)

Assert-Hash $fullSourcePath $expectedSourceHash 'Canonical source'
Assert-Hash $fullMaskPath $expectedMaskHash 'Reviewed body-region mask'
Assert-Hash $fullNativePath $expectedNativeHash 'Committed native-open authority'
Assert-True (Test-Path -LiteralPath $fullAuthorityPath -PathType Leaf) `
    'Continuous authority fixture is missing.'

$authority=Import-PowerShellDataFile -LiteralPath $fullAuthorityPath
$source=[System.Drawing.Bitmap]::new($fullSourcePath)
$native=[System.Drawing.Bitmap]::new($fullNativePath)
$mask=[System.Drawing.Bitmap]::new($fullMaskPath)
try
{
    Assert-True ($source.Width-eq225-and$source.Height-eq225) `
        'Canonical source dimensions changed.'
    Assert-True ($native.Width-eq96-and$native.Height-eq96) `
        'Committed native-open dimensions changed.'
    Assert-True ($mask.Width-eq225-and$mask.Height-eq225) `
        'Reviewed body-region mask dimensions changed.'

    $hairAnchors=@($authority.HairAnchors)
    $bodyNormals=@($authority.BodyNormals)
    Assert-True ($hairAnchors.Count -eq 6) 'Hair anchor count changed.'
    Assert-True ($bodyNormals.Count -eq 15) 'Body normal count changed.'

    foreach($entry in $hairAnchors)
    {
        $label="Hair anchor '$($entry.Name)'"
        Assert-Normal $entry 'SourceNormal' $source $label
        Assert-Normal $entry 'NativeNormal' $native $label
        Assert-ReferencePoint $entry 'SourceFill' $source $label
        Assert-ReferencePoint $entry 'SourceInk' $source $label
        Assert-ReferencePoint $entry 'NativeFill' $native $label
        Assert-ReferencePoint $entry 'NativeInk' $native $label
        Assert-FillExceedsInk $entry 'SourceFill' 'SourceInk' $source $label 'source'
        Assert-FillExceedsInk $entry 'NativeFill' 'NativeInk' $native $label 'native'
    }

    foreach($entry in $bodyNormals)
    {
        $label="Body normal '$($entry.Name)'"
        Assert-Normal $entry 'SourceNormal' $source $label
        Assert-Normal $entry 'NativeNormal' $native $label
        Assert-ReferencePoint $entry 'SourceFill' $source $label
        Assert-ReferencePoint $entry 'NativeFill' $native $label
    }

    foreach($point in @($authority.ProtectedPoints))
    {
        $x=[int]$point.X; $y=[int]$point.Y
        Assert-True ($x-ge0-and$x-lt$mask.Width-and$y-ge0-and$y-lt$mask.Height) `
            "Protected point '$($point.Name)' is outside the reviewed mask."
        Assert-True ($mask.GetPixel($x,$y).R -eq 0) `
            "Protected point '$($point.Name)' is writable in the approved mask."
    }

    $authorityHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $fullAuthorityPath).Hash
    if($authorityHash -ne $expectedAuthorityHash)
    {
        throw "Continuous authority hash changed. Expected '$expectedAuthorityHash', observed '$authorityHash'."
    }

    $outputPaths=@(
        (Join-Path $fullEvidenceDirectory 'dororong-continuous-authority-source.png')
        (Join-Path $fullEvidenceDirectory 'dororong-continuous-authority-source-4x.png')
        (Join-Path $fullEvidenceDirectory 'dororong-continuous-authority-native.png')
        (Join-Path $fullEvidenceDirectory 'dororong-continuous-authority-native-4x.png'))
    foreach($outputPath in $outputPaths)
    {
        Assert-True (-not(Test-Path -LiteralPath $outputPath)) `
            "Authority evidence output already exists: $outputPath"
    }
    $sourceOverlay=New-OverlayBitmap $source
    $nativeOverlay=New-OverlayBitmap $native
    $evidenceDirectoryCreated=-not(Test-Path -LiteralPath $fullEvidenceDirectory)
    try
    {
        Add-AuthorityDrawing $sourceOverlay $hairAnchors $bodyNormals `
            'SourceNormal' 'SourceFill' 'SourceInk' $false
        Add-AuthorityDrawing $nativeOverlay $hairAnchors $bodyNormals `
            'NativeNormal' 'NativeFill' 'NativeInk' $true
        [System.IO.Directory]::CreateDirectory($fullEvidenceDirectory)|Out-Null
        $sourceOverlay.Save($outputPaths[0],[System.Drawing.Imaging.ImageFormat]::Png)
        Save-NearestNeighbor $sourceOverlay $outputPaths[1] 4
        $nativeOverlay.Save($outputPaths[2],[System.Drawing.Imaging.ImageFormat]::Png)
        Save-NearestNeighbor $nativeOverlay $outputPaths[3] 4
    }
    catch
    {
        foreach($outputPath in $outputPaths)
        {
            if([System.IO.File]::Exists($outputPath))
            { [System.IO.File]::Delete($outputPath) }
        }
        if($evidenceDirectoryCreated -and [System.IO.Directory]::Exists($fullEvidenceDirectory) -and `
            @(Get-ChildItem -LiteralPath $fullEvidenceDirectory -Force).Count -eq 0)
        { [System.IO.Directory]::Delete($fullEvidenceDirectory,$false) }
        throw
    }
    finally
    {
        $nativeOverlay.Dispose()
        $sourceOverlay.Dispose()
    }

    Write-Output "CONTINUOUS AUTHORITY OVERLAYS hash=$authorityHash"
    foreach($outputPath in $outputPaths)
    {
        $hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $outputPath).Hash
        Write-Output "OVERLAY path=$outputPath sha256=$hash"
    }
}
finally
{
    $mask.Dispose()
    $native.Dispose()
    $source.Dispose()
}
