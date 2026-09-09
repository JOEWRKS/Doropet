using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public class FlutterOutlineTests
{
    [Fact]
    public void Old_wrist_antialias_fringe_is_cleared_without_erasing_the_torso_join() => CheekProductTests.Sta(() =>
    {
        var source=ForelegFlutterTests.Source();var before=PerchExpressionTests.Pixels(source);
        var image=new Image{Source=source};var cue=new PerchReadinessPresentation();
        cue.Apply(image,true,true,TimeSpan.FromMilliseconds(64));
        var after=PerchExpressionTests.Pixels((BitmapSource)image.Source);
        foreach(var x in new[]{39,40})
        {
            var i=(66*96+x)*4;
            Assert.True(after[i]>=245 && after[i+1]>=245 && after[i+2]>=245,"Old paw AA remains inside the white torso.");
            Assert.Equal(before[i+3],after[i+3]);
        }
        foreach(var x in new[]{37,38})
            Assert.Equal(before.AsSpan((66*96+x)*4,4).ToArray(),after.AsSpan((66*96+x)*4,4).ToArray());
    });

    [Fact]
    public void Torso_join_texel_is_not_carried_away_as_a_fingertip_stroke() => CheekProductTests.Sta(() =>
    {
        var source=ForelegFlutterTests.Source();var bytes=PremultipliedFrame.From(source).Pixels;
        var at=(67*96+37)*4;
        bytes[at]=0;bytes[at+1]=255;bytes[at+2]=0;bytes[at+3]=255;
        var marked=BitmapSource.Create(96,96,96,96,PixelFormats.Pbgra32,null,bytes,384);
        var image=new Image{Source=marked};var cue=new PerchReadinessPresentation();
        cue.Apply(image,true,true,TimeSpan.FromMilliseconds(64));
        var pixels=PremultipliedFrame.From((BitmapSource)image.Source).Pixels;
        Assert.Equal(bytes.AsSpan(at,4).ToArray(),pixels.AsSpan(at,4).ToArray());
        for(var y=52;y<66;y++)for(var x=22;x<63;x++)
        {
            var i=(y*96+x)*4;
            Assert.True(pixels[i+1]<=Math.Max(pixels[i],pixels[i+2])+16,"Static torso stroke was sampled into the moving fingertip.");
        }
    });

    [Fact]
    public void Exposed_torso_edge_keeps_ink_after_interior_donor_marks_are_removed() => CheekProductTests.Sta(() =>
    {
        var image=new Image{Source=ForelegFlutterTests.Source()};var cue=new PerchReadinessPresentation();
        cue.Apply(image,true,true,TimeSpan.FromMilliseconds(64));
        var pixels=PerchExpressionTests.Pixels((BitmapSource)image.Source);
        Assert.True(Dark(pixels,37,64),"Visible torso boundary under the raised wrist must not become an unoutlined white step.");
    });

    [Theory]
    [InlineData(16)] [InlineData(64)] [InlineData(180)] [InlineData(240)]
    public void Raised_arm_contour_reaches_the_chin_without_a_blank_two_row_strip(int milliseconds) => CheekProductTests.Sta(() =>
    {
        var source=ForelegFlutterTests.Source();var image=new Image{Source=source};var cue=new PerchReadinessPresentation();
        cue.Apply(image,true,true,TimeSpan.FromMilliseconds(milliseconds));
        var pixels=PerchExpressionTests.Pixels((BitmapSource)image.Source);
        var ink=0;
        for(var y=51;y<=52;y++)for(var x=38;x<=47;x++)if(Dark(pixels,x,y))ink++;
        Assert.True(ink>=1,"Raised arm line should meet the chin, not start after a preserved blank body strip.");
    });

    [Theory]
    [InlineData(16)] [InlineData(64)] [InlineData(180)] [InlineData(240)]
    public void Flutter_has_no_isolated_ink_fragment_inside_the_front_paw(int milliseconds) => CheekProductTests.Sta(() =>
    {
        var source=ForelegFlutterTests.Source();var image=new Image{Source=source};var cue=new PerchReadinessPresentation();
        cue.Apply(image,true,true,TimeSpan.FromMilliseconds(milliseconds));
        var pixels=PerchExpressionTests.Pixels((BitmapSource)image.Source);
        var visited=new HashSet<(int X,int Y)>();
        for(var y=52;y<=62;y++)for(var x=33;x<=46;x++)
        {
            if(!Dark(pixels,x,y)||!visited.Add((x,y)))continue;
            var queue=new Queue<(int X,int Y)>();queue.Enqueue((x,y));var count=0;var touchesBoundary=false;
            while(queue.TryDequeue(out var p))
            {
                count++;touchesBoundary|=p.X is 33 or 46 || p.Y is 52 or 62;
                for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++)
                {
                    var n=(X:p.X+dx,Y:p.Y+dy);
                    if(n.X<33||n.X>46||n.Y<52||n.Y>62||!Dark(pixels,n.X,n.Y)||!visited.Add(n))continue;
                    queue.Enqueue(n);
                }
            }
            Assert.True(touchesBoundary||count>=3,$"Stranded ink inside paw at{x},{y} ({count}pixels)");
        }
    });

    private static bool Dark(byte[] pixels,int x,int y)
    {
        var i=(y*96+x)*4;
        return pixels[i+3]>=128 && (pixels[i]+pixels[i+1]+pixels[i+2])/3<180;
    }
}
