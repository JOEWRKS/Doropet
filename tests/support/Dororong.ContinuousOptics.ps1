function Get-DororongBilinearPremultipliedSample(
    [Drawing.Bitmap]$Bitmap, [double]$X, [double]$Y)
{
    $x0 = [Math]::Floor($X); $y0 = [Math]::Floor($Y)
    $fx = $X - $x0; $fy = $Y - $y0
    $weights = [Collections.Generic.List[object]]::new()
    $weights.Add(@($x0,     $y0,     ((1-$fx)*(1-$fy)))) | Out-Null
    $weights.Add(@(($x0+1), $y0,     ($fx*(1-$fy)))) | Out-Null
    $weights.Add(@($x0,     ($y0+1), ((1-$fx)*$fy))) | Out-Null
    $weights.Add(@(($x0+1), ($y0+1), ($fx*$fy))) | Out-Null
    $a = 0.0; $pr = 0.0; $pg = 0.0; $pb = 0.0
    foreach ($entry in $weights)
    {
        $px = [int]$entry[0]; $py = [int]$entry[1]; $weight = [double]$entry[2]
        if ($px -lt 0 -or $py -lt 0 -or $px -ge $Bitmap.Width -or $py -ge $Bitmap.Height) { continue }
        $pixel = $Bitmap.GetPixel($px,$py); $alpha = $pixel.A / 255.0
        $a += $weight * $alpha
        $pr += $weight * $alpha * ($pixel.R / 255.0)
        $pg += $weight * $alpha * ($pixel.G / 255.0)
        $pb += $weight * $alpha * ($pixel.B / 255.0)
    }
    if ($a -le 0.0) { return [pscustomobject]@{ A=0.0; R=0.0; G=0.0; B=0.0 } }
    return [pscustomobject]@{ A=$a; R=$pr/$a; G=$pg/$a; B=$pb/$a }
}

function Measure-DororongContinuousCoverage(
    [Drawing.Bitmap]$Bitmap,
    [hashtable]$Normal,
    [Drawing.Color]$Fill,
    [Drawing.Color]$Ink,
    [double]$Spacing = 0.125)
{
    $x1=$Normal.X1Eighth/8.0; $y1=$Normal.Y1Eighth/8.0
    $x2=$Normal.X2Eighth/8.0; $y2=$Normal.Y2Eighth/8.0
    $length=[Math]::Sqrt(($x2-$x1)*($x2-$x1)+($y2-$y1)*($y2-$y1))
    if ($length -le 0.0) { throw 'Continuous normal has zero length.' }
    $fillL=(0.2126*$Fill.R)+(0.7152*$Fill.G)+(0.0722*$Fill.B)
    $inkL=(0.2126*$Ink.R)+(0.7152*$Ink.G)+(0.0722*$Ink.B)
    if ($fillL -le $inkL) { throw 'Fill luminance must exceed ink luminance.' }
    $positions=[Collections.Generic.List[double]]::new()
    for ($distance=0.0; $distance -lt $length; $distance += $Spacing) { $positions.Add($distance) }
    $positions.Add($length)
    $values=@()
    foreach ($distance in $positions)
    {
        $t=$distance/$length
        $sample=Get-DororongBilinearPremultipliedSample $Bitmap ($x1+(($x2-$x1)*$t)) ($y1+(($y2-$y1)*$t))
        $luminance=255.0*((0.2126*$sample.R)+(0.7152*$sample.G)+(0.0722*$sample.B))
        $darkness=[Math]::Clamp(($fillL-$luminance)/($fillL-$inkL),0.0,1.0)
        $values += $sample.A*$darkness
    }
    $integral=0.0
    for ($index=0; $index -lt $positions.Count-1; $index++)
    { $integral += 0.5*($values[$index]+$values[$index+1])*($positions[$index+1]-$positions[$index]) }
    return $integral
}
