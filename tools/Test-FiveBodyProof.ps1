param([string]$ProofRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/repro/five-body-product-port-20260906-attempt-1'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$playback=Get-Content -LiteralPath (Join-Path $ProofRoot 'playback.json') -Raw | ConvertFrom-Json
if($playback[0].pull.Y -gt 1 -or $playback[0].pull.Y -lt -1) {throw 'Playback begins with a spurious clamping-induced vertical pull.'}
foreach($region in @('FrontPaw','MiddlePaw','RightPaw','Belly','Rump')) {
    $native=[System.Drawing.Bitmap]::FromFile((Join-Path $ProofRoot "$region-sheet-native.png"))
    $large=[System.Drawing.Bitmap]::FromFile((Join-Path $ProofRoot "$region-sheet-nearest3x.png"))
    try {
        if($large.Width -ne $native.Width*3 -or $large.Height -ne $native.Height*3) {throw 'Wrong enlargement dimensions'}
        for($y=0;$y -lt $large.Height;$y++) {for($x=0;$x -lt $large.Width;$x++) {
            if($large.GetPixel($x,$y).ToArgb() -ne $native.GetPixel([int][math]::Floor($x/3),[int][math]::Floor($y/3)).ToArgb()) {
                throw "Non-nearest enlargement for $region at $x,$y"
            }
        }}
    } finally {$native.Dispose();$large.Dispose()}
}
Write-Output 'Exact nearest replication PASS: all five sheets, every pixel of 3x output.'
