Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal([object]$Expected, [object]$Actual, [string]$Message)
{
    if ($Expected -ne $Actual) { throw "$Message Expected '$Expected', observed '$Actual'." }
}

function Assert-BitmapEqual([Drawing.Bitmap]$Expected, [Drawing.Bitmap]$Actual, [string]$Label)
{
    Assert-Equal $Expected.Width $Actual.Width "$Label width changed."
    Assert-Equal $Expected.Height $Actual.Height "$Label height changed."
    for ($y = 0; $y -lt $Expected.Height; $y++)
    {
        for ($x = 0; $x -lt $Expected.Width; $x++)
        {
            Assert-Equal $Expected.GetPixel($x, $y).ToArgb() $Actual.GetPixel($x, $y).ToArgb() `
                "$Label differs from the user-authored frame at ($x,$y)."
        }
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $repositoryRoot 'src/Dororong.App/Assets'
$fixtureRoot = Join-Path $PSScriptRoot 'fixtures'
$cases = @(
    @{
        Label = 'Open'
        Asset = Join-Path $assetRoot 'dororong-canonical.png'
        Reference = Join-Path $fixtureRoot 'dororong-user-authored-open-reference.png'
        AssetHash = '699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78'
        ReferenceHash = '699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78'
    },
    @{
        Label = 'Half-close'
        Asset = Join-Path $assetRoot 'dororong-blink-squint.png'
        Reference = Join-Path $fixtureRoot 'dororong-user-authored-half-reference.png'
        AssetHash = '2A733093AC35B9678CBB90833272498C7BE755F77A574271DAE64FCE80FC90E2'
        ReferenceHash = '2A733093AC35B9678CBB90833272498C7BE755F77A574271DAE64FCE80FC90E2'
    },
    @{
        Label = 'Full-close'
        Asset = Join-Path $assetRoot 'dororong-closed-eyes.png'
        Reference = Join-Path $fixtureRoot 'dororong-user-authored-closed-reference.png'
        AssetHash = '0D20EED5873A7E4277D6ED539474D875C9B8DF79998B1EFD74F46AA87662F488'
        ReferenceHash = '0D20EED5873A7E4277D6ED539474D875C9B8DF79998B1EFD74F46AA87662F488'
    })

Add-Type -AssemblyName System.Drawing
$bitmaps = [Collections.Generic.List[Drawing.Bitmap]]::new()
try
{
    foreach ($case in $cases)
    {
        Assert-Equal $case.AssetHash (Get-FileHash -Algorithm SHA256 -LiteralPath $case.Asset).Hash `
            "$($case.Label) runtime frame does not have the supplied native-96 eye pixels."
        Assert-Equal $case.ReferenceHash (Get-FileHash -Algorithm SHA256 -LiteralPath $case.Reference).Hash `
            "$($case.Label) user-authored reference identity changed."
        $reference = [Drawing.Bitmap]::new($case.Reference); $bitmaps.Add($reference)
        $asset = [Drawing.Bitmap]::new($case.Asset); $bitmaps.Add($asset)
        Assert-Equal 96 $reference.Width "$($case.Label) reference is not native width."
        Assert-Equal 96 $reference.Height "$($case.Label) reference is not native height."
        Assert-BitmapEqual $reference $asset "$($case.Label) runtime asset"
    }
}
finally
{
    foreach ($bitmap in $bitmaps) { $bitmap.Dispose() }
}

Write-Output 'USER-AUTHORED EYE FRAMES PASS: runtime open/half/full frames exactly match the supplied native-96 references.'
