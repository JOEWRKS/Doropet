param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$dest = Join-Path $Root 'artifacts/repro/five-body-product-port-20260906-attempt-1'
[void](New-Item -ItemType Directory -Force -Path $dest)
$source = [System.Drawing.Bitmap]::FromFile((Join-Path $Root 'src/Dororong.App/Assets/dororong-canonical.png'))
$canvas = [System.Drawing.Bitmap]::new(1000,1000)
$g = [System.Drawing.Graphics]::FromImage($canvas)
$g.Clear([System.Drawing.Color]::FromArgb(220,230,240))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($source,[System.Drawing.Rectangle]::new(40,40,960,960),0,0,96,96,[System.Drawing.GraphicsUnit]::Pixel)
$font = [System.Drawing.Font]::new('Consolas',9)
for($i=0;$i -lt 96;$i+=4){
 $n=40+$i*10
 $g.DrawLine([System.Drawing.Pens]::LightSlateGray,$n,40,$n,1000)
 $g.DrawLine([System.Drawing.Pens]::LightSlateGray,40,$n,1000,$n)
 $g.DrawString([string]$i,$font,[System.Drawing.Brushes]::Black,$n,20)
 $g.DrawString([string]$i,$font,[System.Drawing.Brushes]::Black,15,$n)
}
$canvas.Save((Join-Path $dest 'source-coordinate-grid.png'))
$g.Dispose(); $canvas.Dispose(); $source.Dispose(); $font.Dispose()
