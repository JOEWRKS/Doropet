using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Controls;

// Preserve authored ink, rather than fitting a second rear silhouette.
internal static class UprightRumpSource
{
    private static readonly BitmapSource Open = Load("dororong-canonical.png");
    private static readonly BitmapSource Closed = Load("dororong-closed-eyes.png");
    private static readonly byte[] Pixels = PremultipliedFrame.From(Open).Pixels;
    internal static BitmapSource Standing(bool closed) => closed ? Closed : Open;
    internal static bool Contains(object? source) => ReferenceEquals(source,Open)||ReferenceEquals(source,Closed);

    private static BitmapSource Load(string name)
    {
        var source=new BitmapImage(new Uri($"pack://application:,,,/Dororong.App;component/Assets/{name}"));
        var original=new byte[96*96*4];
        new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0).CopyPixels(original,384,0);
        var clean=(byte[])original.Clone();
        // Same bounded, disconnected low-alpha cleanup as the locomotion bank.
        // Connected antialiasing and every other authored pixel remain intact.
        for(var y=60;y<96;y++)for(var x=60;x<96;x++)
        {
            var i=(y*96+x)*4; if(original[i+3] is 0 or >=40)continue;
            var attached=false;
            for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++)
                if(x+dx>=0&&x+dx<96&&y+dy>=0&&y+dy<96&&original[((y+dy)*96+x+dx)*4+3]>=40)attached=true;
            if(!attached)Array.Clear(clean,i,4);
        }
        var frame=BitmapSource.Create(96,96,96,96,PixelFormats.Bgra32,null,clean,384);frame.Freeze();return frame;
    }

    internal static void Apply(byte[] composite,double amount)
    {
        var upright=1-Smooth(amount/.2);if(upright==0)return;
        var head=HuntFrames.Instance.Head(0).Span;
        // Supersampled texels use exact 2x replication; the final native reduction
        // must not blur the authored rump through an extra bilinear round trip.
        // The narrow upper join still follows the ribbon, not the rump below it.
        for(var yy=130;yy<184;yy++)for(var xx=158;xx<224;xx++)
        {
            var x=(xx-32)/2;var y=(yy-32)/2;
            var weight=upright*Smooth((xx-158)/8d)*Smooth((yy-130)/8d)*(1-Smooth((yy-178)/6d));
            var source=(y*96+x)*4;var target=(yy*256+xx)*4;
            for(var c=0;c<4;c++)
            {
                // The isolated head owns these texels. Supply only hidden white
                // torso backing, never duplicate the old hair/ribbon ink.
                var value=head[source+3]>0 ? (x<=69?255:0) : Pixels[source+c];
                composite[target+c]=(byte)Math.Round(composite[target+c]+(value-composite[target+c])*weight);
            }
        }
    }

    private static double Smooth(double value){var t=Math.Clamp(value,0,1);return t*t*(3-2*t);}
}
