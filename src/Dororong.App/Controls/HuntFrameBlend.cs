using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Controls;

// A single premultiplied raster, not two overlapping image controls. Exact
// endpoints retain the original source identity and alpha-hit-test contract.
internal static class HuntFrameBlend
{
    internal static BitmapSource Apply(BitmapSource ordinary,BitmapSource tracking,double amount)
    {
        if(amount<=0)return ordinary;
        if(amount>=1)return tracking;
        var a=PremultipliedFrame.From(ordinary).Pixels;
        var b=PremultipliedFrame.From(tracking).Pixels;
        for(var i=0;i<a.Length;i++)a[i]=(byte)Math.Round(a[i]+(b[i]-a[i])*amount);
        var result=BitmapSource.Create(96,96,96,96,PixelFormats.Pbgra32,null,a,384);
        result.Freeze();return result;
    }
}
