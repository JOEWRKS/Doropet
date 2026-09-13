namespace Dororong.App.Controls;

// A hand-isolated copy of the ordinary ribbon, in canonical head coordinates.
// The tail sits on white torso in the reference, but over the desktop when
// hanging. Its outer texels need coverage, not that reference torso behind them.
internal static class AuthoredRibbonContour
{
    internal static void Apply(byte[] output, int width, int height, bool straight,
        byte[] reference, double offsetX, double offsetY)
    {
        for (var y = Math.Max(0, (int)Math.Floor(36 + offsetY)); y <= Math.Min(height-1, Math.Ceiling(61 + offsetY)); y++)
        for (var x = Math.Max(0, (int)Math.Floor(60 + offsetX)); x <= Math.Min(width-1, Math.Ceiling(70 + offsetX)); x++)
        {
            var sx=x-offsetX; var sy=y-offsetY;
            var ix=(int)Math.Floor(sx); var iy=(int)Math.Floor(sy);
            var fx=sx-ix; var fy=sy-iy;
            double coverage=0, alpha=0, blue=0, green=0, red=0;
            for(var dy=0;dy<2;dy++) for(var dx=0;dx<2;dx++)
            {
                var nx=ix+dx; var ny=iy+dy;
                var (left,right)=Span(ny);
                if(nx<left || nx>right) continue;
                var at=(ny*96+nx)*4;
                var a=(double)reference[at+3];
                var b=(double)reference[at]; var g=(double)reference[at+1]; var r=(double)reference[at+2];
                if(ny>=47 && (nx==right || ny==61 || ny==60 && nx==left))
                {
                    // Remove only the measured white backing of the outer tail
                    // edge. Ink70 matches the opaque ordinary ribbon tip (B71).
                    // White recomposition retains the original reference colour.
                    a=Math.Clamp(Math.Round(255*(255-Math.Min(b,Math.Min(g,r)))/185),0,255);
                    var white=255-a;
                    b-=white; g-=white; r-=white;
                }
                var weight=(dx==0?1-fx:fx)*(dy==0?1-fy:fy);
                alpha+=a/255*weight; blue+=b*weight; green+=g*weight; red+=r*weight;
                // Upper loops have no torso behind them: replace their alpha
                // as well. Tail edges retain the posed torso when it is behind.
                coverage+=(ny<=46?1:a/255)*weight;
            }
            if(coverage<=0) continue;
            var target=(y*width+x)*4;
            var oldAlpha=output[target+3]/255d;
            var newAlpha=alpha+oldAlpha*(1-coverage);
            if(newAlpha<=0) { Array.Clear(output,target,4); continue; }
            var oldWeight=(1-coverage)*(straight?oldAlpha:1);
            var divisor=straight?newAlpha:1;
            output[target]=(byte)Math.Clamp(Math.Round((blue+output[target]*oldWeight)/divisor),0,255);
            output[target+1]=(byte)Math.Clamp(Math.Round((green+output[target+1]*oldWeight)/divisor),0,255);
            output[target+2]=(byte)Math.Clamp(Math.Round((red+output[target+2]*oldWeight)/divisor),0,255);
            output[target+3]=(byte)Math.Clamp(Math.Round(255*newAlpha),0,255);
        }
    }

    // Row-by-row pixel ownership excludes the rose, hair and reference rump.
    private static (int Left,int Right) Span(int y) => y switch
    {
        >=36 and <=41 => (65,70),
        >=42 and <=46 => (64,70),
        47 => (60,67),
        >=48 and <=52 => (60,68),
        53 => (60,67),
        54 or 55 => (60,66),
        56 or 57 => (60,65),
        58 or 59 => (60,64),
        60 or 61 => (61,63),
        _ => (1,0)
    };
}
