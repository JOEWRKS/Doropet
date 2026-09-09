using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public class AlternatingForelegTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Paws_travel_in_opposite_directions_during_the_same_flutter_beat(bool padded) => CheekProductTests.Sta(() =>
    {
        var source=ForelegFlutterTests.Source();
        var input=PremultipliedFrame.From(source).Pixels;
        var width=padded?160:96;var pad=padded?32:0;
        var pixels=new byte[width*width*4];
        for(var y=0;y<96;y++)Array.Copy(input,y*384,pixels,((y+pad)*width+pad)*4,384);
        // Label a small interior patch of each real cutout, so their movement
        // can be measured independently even where the rendered paws overlap.
        void Mark(int cx,int cy,int channel)
        {
            for(var y=cy-1;y<=cy+1;y++)for(var x=cx-1;x<=cx+1;x++)
            {
                var at=((y+pad)*width+x+pad)*4;
                pixels[at]=pixels[at+1]=pixels[at+2]=0;
                pixels[at+channel]=255;pixels[at+3]=255;
            }
        }
        Mark(38,59,1);Mark(53,61,0);
        var marked=BitmapSource.Create(width,width,96,96,PixelFormats.Pbgra32,null,pixels,width*4);
        var frame=PremultipliedFrame.From(marked);
        double CenterY(byte[] rendered,int channel)
        {
            double sum=0,weight=0;
            for(var y=52+pad;y<69+pad;y++)for(var x=pad;x<63+pad;x++)
            {
                var at=(y*width+x)*4;
                var other=Math.Max(rendered[at+(channel==0?1:0)],rendered[at+2]);
                var amount=Math.Max(0,rendered[at+channel]-other-40);
                sum+=y*amount;weight+=amount;
            }
            Assert.True(weight>100,"Both independently moving paw labels must remain visible.");
            return sum/weight;
        }
        var start=ForelegFlutterFrame.Render(frame,true,0,null);
        var next=ForelegFlutterFrame.Render(frame,true,.125,null);
        Assert.True(CenterY(next,1)<CenterY(start,1)-.1,"First paw should rise during this beat.");
        Assert.True(CenterY(next,0)>CenterY(start,0)+.1,"Second paw should fall, not rise together with the first.");
    });
}
