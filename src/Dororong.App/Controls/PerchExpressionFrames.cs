using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Controls;

// Registration only operates on runtime copies. The source09 resource remains
// 100x100; cheek's approved raster field is expressed in canonical 96px space.
internal static class PerchExpressionFrames
{
    internal static readonly BitmapSource Open = CleanOpen();
    internal static readonly BitmapSource Closed = Compose("dororong-closed-eyes.png");

    private static BitmapSource CleanOpen()
    {
        var source = Read(Load("dororong-edge-perch-09.png"));
        var result = (byte[])source.Clone();
        // The partially transparent lower contour still contains white matte.
        // Recover its ink colour and coverage instead of blurring/thickening the
        // stroke. A white background retains the same luminance, while a black
        // background no longer reveals a pale second contour. Upper head and
        // accessory pixels are deliberately outside this bounded repair.
        for (var y = 57; y < 100; y++) for (var x = 0; x < 100; x++)
        {
            var at = (y * 100 + x) * 4;
            var alpha = source[at + 3];
            if (alpha == 0 || alpha == 255) continue;
            var light = (source[at] + source[at + 1] + source[at + 2]) / 3d;
            if (light > 235)
            {
                if (alpha < 128) Array.Clear(result, at, 4);
                continue; // Nearly opaque white is body, not exterior matte.
            }
            var donor = -1; var distance = int.MaxValue;
            for (var dy = -2; dy <= 2; dy++) for (var dx = -2; dx <= 2; dx++)
            {
                var nx = x + dx; var ny = y + dy;
                if (nx < 0 || nx >= 100 || ny < 57 || ny >= 100) continue;
                var i = (ny * 100 + nx) * 4;
                var value = (source[i] + source[i + 1] + source[i + 2]) / 3d;
                var d = dx * dx + dy * dy;
                if (source[i + 3] != 255 || value >= 160 || value >= light || d >= distance) continue;
                donor = i; distance = d;
            }
            if (donor < 0) continue;
            var ink = (source[donor] + source[donor + 1] + source[donor + 2]) / 3d;
            result[at + 3] = (byte)Math.Clamp(Math.Round(alpha * (255 - light) / (255 - ink)), 0, 255);
            for (var c = 0; c < 3; c++) result[at + c] = source[donor + c];
        }
        var frame = BitmapSource.Create(100, 100, 96, 96, PixelFormats.Bgra32, null, result, 400);
        frame.Freeze();
        return AuthoredHeadContour.Repair(frame, 8, -6);
    }

    internal static byte[] RegisteredOpen()
    {
        var source = Read(Open);
        var result = new byte[96 * 96 * 4];
        for(var y=0;y<100;y++) for(var x=0;x<100;x++)
        {
            var index=(y*100+x)*4; var nx=x-8;var ny=y+6;
            if(nx>=0 && nx<96 && ny>=0 && ny<96)
                Array.Copy(source,index,result,(ny*96+nx)*4,4);
            else if(source[index+3]!=0)
                throw new InvalidOperationException("Perch registration must retain all opaque pixels.");
        }
        return result;
    }

    private static BitmapSource Compose(string authored)
    {
        var canonical=Read(Load("dororong-canonical.png"));var expression=Read(Load(authored));
        var result=Read(Open);
        for(var y=0;y<96;y++) for(var x=0;x<96;x++)
        {
            var i=(y*96+x)*4;
            if(canonical.AsSpan(i,4).SequenceEqual(expression.AsSpan(i,4)))continue;
            // Apply only the authored difference mask, never a bounding box:
            // image09's similar head is not byte-identical to canonical.
            Array.Copy(expression,i,result,((y-6)*100+x+8)*4,4);
        }
        var frame=BitmapSource.Create(100,100,96,96,PixelFormats.Bgra32,null,result,400);frame.Freeze();return frame;
    }
    private static byte[] Read(BitmapSource source)
    {
        var b=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
        var pixels=new byte[b.PixelWidth*b.PixelHeight*4];b.CopyPixels(pixels,b.PixelWidth*4,0);return pixels;
    }
    private static BitmapSource Load(string name)
    {
        var b=new BitmapImage();b.BeginInit();b.UriSource=new Uri($"pack://application:,,,/Dororong.App;component/Assets/{name}");
        b.CacheOption=BitmapCacheOption.OnLoad;b.EndInit();b.Freeze();return b;
    }
}
