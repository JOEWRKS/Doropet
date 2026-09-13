using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Controls;

public class UprightRumpTests
{
    [Theory]
    [InlineData(0,0)] [InlineData(0,-.22)] [InlineData(0,.22)]
    [InlineData(156,0)] [InlineData(156,-.22)] [InlineData(156,.22)]
    [InlineData(0,-.34906585)] [InlineData(0,.34906585)]
    public void Tracking_keeps_authored_rump_pixels_below_the_rotating_ribbon(int frame,double roll) => EdgePerchPresentationTests.Sta(()=>
    {
        _=new DororongPresenter();
        var source=PerchExpressionTests.Pixels(new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png")));
        var actual=PerchExpressionTests.Pixels(new HuntRenderer().Render(frame,new(0,0,roll)));
        // This band is below the ribbon at both gaze extremes; its body edge
        // must retain source ink/coverage, not merely a similar bounding box.
        for(var y=roll>.3?65:62;y<70;y++)for(var x=71;x<82;x++)for(var c=0;c<4;c++)
            Assert.True(Math.Abs(source[(y*96+x)*4+c]-actual[(y*96+x)*4+c])<=2,$"rump changed at {x},{y} channel{c}");
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Standing_cleanup_preserves_every_connected_source_pixel(bool closed) => EdgePerchPresentationTests.Sta(()=>
    {
        _=new DororongPresenter();
        var name=closed?"dororong-closed-eyes.png":"dororong-canonical.png";
        var source=PerchExpressionTests.Pixels(new BitmapImage(new Uri($"pack://application:,,,/Dororong.App;component/Assets/{name}")));
        var actual=PerchExpressionTests.Pixels(UprightRumpSource.Standing(closed));
        for(var y=0;y<96;y++)for(var x=0;x<96;x++)
        {
            var i=(y*96+x)*4;
            if((x,y) is not ((74,70) or (74,71)))
                Assert.Equal(source.AsSpan(i,4).ToArray(),actual.AsSpan(i,4).ToArray());
            else Assert.Equal(new byte[4],actual.AsSpan(i,4).ToArray());
        }
    });

    internal static byte[] CleanFixture(byte[] pixels)
    {
        // Independently measured, disconnected texels. No production cleanup
        // logic or broad tolerance: every other fixture byte remains asserted.
        var result=(byte[])pixels.Clone();
        Array.Clear(result,(70*96+74)*4,4);Array.Clear(result,(71*96+74)*4,4);
        return result;
    }

    [Fact]
    public void Fresh_idle_and_blink_do_not_restore_disconnected_rump_fringe() => EdgePerchPresentationTests.Sta(()=>
    {
        var p=new DororongPresenter();
        var pose=new PetSnapshot(PetState.Idle,new(100,100),FacingDirection.Left,0,false,null);
        for(var t=0;t<2500;t+=16)
        {
            p.RenderDesktop(pose with{Phase=(t%1000)/1000d},DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(16));
            var pixels=PerchExpressionTests.Pixels((BitmapSource)((Image)p.FindName("DororongImage")).Source);
            Assert.InRange(pixels[(70*96+74)*4+3],0,2);
            Assert.InRange(pixels[(71*96+74)*4+3],0,2);
            Assert.True(pixels[(70*96+70)*4+3]>200,"Connected authored outline must survive cleanup");
        }
    });
}
