using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Controls;

// One lower-head texture owner for authored keys, dense transitions and perch.
// Repair copies never change source resources or the body's pose/registration.
internal static class AuthoredHeadContour
{
    private static readonly Lazy<byte[]> Canonical = new(() => PremultipliedFrame.From(
        new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"))).Pixels);
    // Integer head translations measured against the ordinary pink-hair texture.
    private static readonly (double X, double Y)[] PullOffsets =
        [(0,-1),(0,-3),(1,-7),(2,-11),(4,-13),(6,-15),(8,-15),(11,-16)];

    internal static BitmapSource Pull(BitmapSource source, double progress)
    {
        var position = Math.Clamp(progress, 0, 1) * 7;
        var index = Math.Min(6, (int)position);
        var t = position - index;
        var a = PullOffsets[index]; var b = PullOffsets[index + 1];
        return Repair(source, a.X + (b.X-a.X)*t, a.Y + (b.Y-a.Y)*t);
    }

    internal static BitmapSource Repair(BitmapSource source, double offsetX, double offsetY)
    {
        var straight = source.Format == PixelFormats.Bgra32;
        var stride = source.PixelWidth * 4;
        var output = new byte[stride * source.PixelHeight];
        if (straight) source.CopyPixels(output, stride, 0);
        else output = PremultipliedFrame.From(source).Pixels;
        var reference = Canonical.Value;
        for (var y = Math.Max(0,(int)Math.Floor(57+offsetY)); y <= Math.Min(source.PixelHeight-1,Math.Ceiling(72+offsetY)); y++)
        for (var x = Math.Max(0,(int)Math.Floor(19+offsetX)); x <= Math.Min(source.PixelWidth-1,Math.Ceiling(53+offsetX)); x++)
        {
            var sx=x-offsetX; var sy=y-offsetY;
            var ix=(int)Math.Floor(sx); var iy=(int)Math.Floor(sy);
            var fx=sx-ix; var fy=sy-iy;
            double coverage=0,blue=0,green=0,red=0;
            for(var dy=0;dy<2;dy++) for(var dx=0;dx<2;dx++)
            {
                var nx=ix+dx;var ny=iy+dy;
                if(!Owns(nx,ny))continue;
                var weight=(dx==0?1-fx:fx)*(dy==0?1-fy:fy);
                var at=(ny*96+nx)*4;
                var white=253*(1-reference[at+3]/255d);
                coverage+=weight;
                blue+=(reference[at]+white)*weight;
                green+=(reference[at+1]+white)*weight;
                red+=(reference[at+2]+white)*weight;
            }
            if(coverage==0)continue;
            var target=y*stride+x*4;
            // The authored lower head lies on the white chest. Replace old ink,
            // not source-over it; bilinear mask coverage makes subpixel shifts
            // continuous at the outer repair boundary without blurring the head.
            var oldAlpha = output[target+3] / 255d;
            var newAlpha = coverage + oldAlpha*(1-coverage);
            var oldWeight = (1-coverage)*(straight?oldAlpha:1);
            var divisor = straight?newAlpha:1;
            output[target]=(byte)Math.Clamp(Math.Round((blue+output[target]*oldWeight)/divisor),0,255);
            output[target+1]=(byte)Math.Clamp(Math.Round((green+output[target+1]*oldWeight)/divisor),0,255);
            output[target+2]=(byte)Math.Clamp(Math.Round((red+output[target+2]*oldWeight)/divisor),0,255);
            output[target+3]=(byte)Math.Clamp(Math.Round(255*newAlpha),0,255);
        }
        AuthoredRibbonContour.Apply(output, source.PixelWidth, source.PixelHeight, straight, reference, offsetX, offsetY);
        var frame=BitmapSource.Create(source.PixelWidth,source.PixelHeight,96,96,straight?PixelFormats.Bgra32:PixelFormats.Pbgra32,null,output,stride);
        frame.Freeze();return frame;
    }

    private static bool Owns(int x,int y) =>
        x is >=20 and <=41 && y is >=59 and <=67 ||
        y is >=58 and <=71 && x >= (y<66?41:39) && x <= (y<63?52:y<66?50:48);
}
