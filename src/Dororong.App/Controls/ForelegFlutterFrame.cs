using System.Windows;

namespace Dororong.App.Controls;

// Readiness-only cutout animation. Only foreleg texels rotate; the torso plate
// is stationary. Polygons describe the existing art, not an image-wide warp.
internal static class ForelegFlutterFrame
{
    private sealed record Arm(Point Root, Point[] Mask);
    private static readonly Arm[] Hanging =
    [
        new(new(37,53), [new(31,52),new(45,52),new(46,57),new(45,62),new(42,66),new(39,68),new(35,67),new(32,64),new(31,59)]),
        new(new(53,55), [new(45,54),new(61,53),new(61,57),new(59,63),new(56,67),new(52,68),new(48,66),new(45,62)])
    ];
    private static readonly Arm[] Canonical =
    [
        new(new(22,71), [new(16,70),new(29,70),new(29,79),new(27,83),new(22,84),new(18,81),new(16,76)]),
        new(new(39,77), [new(32,76),new(50,76),new(50,85),new(47,89),new(41,90),new(35,86),new(32,81)])
    ];

    internal static byte[] Render(PremultipliedFrame frame, bool hanging, double phase, Point? pin, HuntPose? bodyPose = null)
    {
        var width = frame.Source.PixelWidth;
        var pad = width == 160 ? 32 : 0;
        var result = (byte[])frame.Pixels.Clone();
        var strippedTorsoInk = new bool[width * width];
        var armCoverage = new double[width * width];
        var arms = hanging ? Hanging : Canonical;
        if (bodyPose is { } pose)
        {
            Point Map(Point p) { var mapped = pose.Map(p.X,p.Y); return new(mapped.X,mapped.Y); }
            arms = Canonical.Select(a => new Arm(Map(a.Root),a.Mask.Select(Map).ToArray())).ToArray();
        }
        bool Foreground(int x,int y) => bodyPose is null && HeadForeground(hanging,x,y);
        var layers = new List<(Arm Arm, byte[] Pixels, double Angle)>();
        for (var armIndex = 0; armIndex < arms.Length; armIndex++)
        {
            var arm = arms[armIndex];
            // A directly held paw keeps its whole shape; the other can still flap.
            if (pin is { } p && Inside(arm.Mask, p.X - pad, p.Y - pad)) continue;
            var layer = new byte[frame.Pixels.Length];
            for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++)
            {
                if (Foreground(x,y) || !Inside(arm.Mask, x + .5, y + .5)) continue;
                var at = ((y + pad) * width + x + pad) * 4;
                // The first paw meets the static torso here. Keep its left
                // contour, but clear the old tip's inner antialias fringe.
                // Neither belongs in the rotating arm layer.
                if (hanging && armIndex == 0 && y >= 66)
                {
                    if (y == 66 && x >= 39)
                    {
                        var white = ((70 + pad) * width + 44 + pad) * 4;
                        for (var c = 0; c < 3; c++)
                            result[at+c] = (byte)((frame.Pixels[white+c] * result[at+3] + 127) / 255);
                    }
                    continue;
                }
                Array.Copy(frame.Pixels, at, layer, at, 4);
                if (hanging)
                {
                    // Extend the existing white torso/left contour only into
                    // the vacated arm mask. This plate never follows the wave.
                    Array.Copy(frame.Pixels, ((70 + pad) * width + x + pad) * 4, result, at, 4);
                    // The donor's vertical torso ink is not an arm crease. Above
                    // the real torso join it becomes a floating mark between the
                    // rotated paws; retain its coverage, but use interior white.
                    if (y < 66)
                    {
                        strippedTorsoInk[at/4] = result[at]+result[at+1]+result[at+2] < result[at+3]*2.5;
                        var white = ((70 + pad) * width + 44 + pad) * 4;
                        for (var c = 0; c < 3; c++)
                            result[at+c] = (byte)((frame.Pixels[white+c] * result[at+3] + 127) / 255);
                    }
                }
                else if (y > arm.Root.Y + 2)
                    Array.Clear(result, at, 4);
            }
            // Native art faces left; the existing presenter mirror handles right.
            // Half a beat apart: one paw reaches up while the other comes down.
            // Both keep the same four-Hz cycle and approved forward angle range.
            var angle = (52 + 13 * Math.Sin(phase * Math.PI * 2 - armIndex * Math.PI)) * Math.PI / 180;
            layers.Add((arm, layer, angle));
        }
        // The head is a foreground cutout, not an opaque rectangle. Remove its
        // base copy before drawing paws, then composite its actual coverage once.
        for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++)
            if (Foreground(x,y)) Array.Clear(result, ((y + pad) * width + x + pad) * 4, 4);
        foreach (var (arm, layer, angle) in layers)
        {
            var cos = Math.Cos(angle); var sin = Math.Sin(angle);
            for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++)
            {
                if (bodyPose is null && (hanging ? y < 48 || y >= 69 || x >= 63 : y < 63 || y >= 91 || x >= 53)) continue;
                var dx = x - arm.Root.X; var dy = y - arm.Root.Y;
                var sx = arm.Root.X + cos * dx + sin * dy + pad;
                var sy = arm.Root.Y - sin * dx + cos * dy + pad;
                var ix = (int)Math.Floor(sx); var iy = (int)Math.Floor(sy);
                if (ix < 0 || iy < 0 || ix >= width - 1 || iy >= width - 1) continue;
                var fx = sx - ix; var fy = sy - iy;
                var at = ((y + pad) * width + x + pad) * 4;
                double Sample(int channel)
                {
                    var i = (iy * width + ix) * 4 + channel;
                    return layer[i]*(1-fx)*(1-fy) + layer[i+4]*fx*(1-fy) +
                        layer[i+width*4]*(1-fx)*fy + layer[i+width*4+4]*fx*fy;
                }
                var alpha = Sample(3);
                if (alpha <= 0) continue;
                armCoverage[at/4] = Math.Max(armCoverage[at/4], alpha);
                for(var c=0;c<4;c++) result[at+c]=(byte)Math.Clamp(Math.Round(Sample(c)+result[at+c]*(1-alpha/255)),0,255);
            }
        }
        // Put donor ink back only on a truly exposed torso silhouette, never
        // inside the white body or on top of a moving paw. Coverage is unchanged.
        if (hanging)
        {
            for (var y=52;y<66;y++) for (var x=2;x<63;x++)
            {
                var at=((y+pad)*width+x+pad)*4;
                if (!strippedTorsoInk[at/4] || armCoverage[at/4] >= 32 ||
                    result[at-4+3] >= 64 && result[at-8+3] >= 64) continue;
                var donor=((70+pad)*width+x+pad)*4;
                if(frame.Pixels[donor+3]==0)continue;
                for(var c=0;c<3;c++) result[at+c]=(byte)Math.Min(result[at+c],
                    (frame.Pixels[donor+c]*result[at+3]+frame.Pixels[donor+3]/2)/frame.Pixels[donor+3]);
            }
        }
        // Transparent space beside the hair must reveal the raised paw. Opaque
        // head texels remain exact; antialiased edges cover the paw only once.
        for(var y=0;y<96;y++) for(var x=0;x<96;x++)
        {
            var at = ((y + pad) * width + x + pad) * 4;
            if (Foreground(x,y))
            {
                var uncovered = 1 - frame.Pixels[at+3] / 255d;
                for (var c = 0; c < 4; c++)
                    result[at+c] = (byte)Math.Clamp(Math.Round(frame.Pixels[at+c] + result[at+c] * uncovered), 0, 255);
            }
            if(pin is { } p && Math.Abs(x+pad-p.X)<=3 && Math.Abs(y+pad-p.Y)<=3)
                Array.Copy(frame.Pixels,at,result,at,4);
        }
        return result;
    }

    private static bool HeadForeground(bool hanging,int x,int y) => hanging
        ? y < 52 || y <= 54 && x < 33 || y <= 55 && x >= 49 && x < 58
        : y < 67;

    private static bool Inside(Point[] polygon, double x, double y)
    {
        var inside = false;
        for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
        {
            var a=polygon[i];var b=polygon[j];
            if((a.Y>y)!=(b.Y>y) && x < (b.X-a.X)*(y-a.Y)/(b.Y-a.Y)+a.X) inside=!inside;
        }
        return inside;
    }
}
