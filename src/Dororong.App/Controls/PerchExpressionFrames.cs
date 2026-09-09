using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Controls;

// Registration only operates on runtime copies. The source09 resource remains
// 100x100; cheek's approved raster field is expressed in canonical 96px space.
internal static class PerchExpressionFrames
{
    internal static readonly BitmapSource Open = Load("dororong-edge-perch-09.png");
    internal static readonly BitmapSource Squint = Compose("dororong-blink-squint.png");
    internal static readonly BitmapSource Closed = Compose("dororong-closed-eyes.png");

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
