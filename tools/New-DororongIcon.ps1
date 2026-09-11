param(
    [string]$Source = (Join-Path $PSScriptRoot '..\src\Dororong.App\Assets\dororong-canonical.png'),
    [string]$Output = (Join-Path $PSScriptRoot '..\src\Dororong.App\Assets\dororong.ico')
)

$ErrorActionPreference = 'Stop'
$png = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Source))
if ($png.Length -lt 24 -or [Convert]::ToHexString($png[0..7]) -ne '89504E470D0A1A0A')
{
    throw 'The icon source must be a PNG file.'
}

function Read-BigEndianUInt32([byte[]]$Bytes, [int]$Offset)
{
    return ([uint32]$Bytes[$Offset] -shl 24) -bor
        ([uint32]$Bytes[$Offset + 1] -shl 16) -bor
        ([uint32]$Bytes[$Offset + 2] -shl 8) -bor
        [uint32]$Bytes[$Offset + 3]
}

$width = Read-BigEndianUInt32 $png 16
$height = Read-BigEndianUInt32 $png 20
if ($width -lt 1 -or $width -gt 256 -or $height -lt 1 -or $height -gt 256)
{
    throw "PNG dimensions must be between 1 and 256 pixels; observed ${width}x${height}."
}

$memory = [IO.MemoryStream]::new()
try
{
    $writer = [IO.BinaryWriter]::new($memory)
    try
    {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]1)
        $writer.Write([byte]$(if ($width -eq 256) { 0 } else { $width }))
        $writer.Write([byte]$(if ($height -eq 256) { 0 } else { $height }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$png.Length)
        $writer.Write([uint32]22)
        $writer.Write($png)
        $writer.Flush()
        [IO.File]::WriteAllBytes($Output, $memory.ToArray())
    }
    finally
    {
        $writer.Dispose()
    }
}
finally
{
    $memory.Dispose()
}

Write-Output "Wrote deterministic ${width}x${height} product icon to '$Output'."
