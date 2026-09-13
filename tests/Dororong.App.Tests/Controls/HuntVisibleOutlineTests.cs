using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public sealed class HuntVisibleOutlineTests
{
    [Fact]
    public void First_crouch_frame_does_not_snap_the_raised_neck_to_a_different_root() => EdgePerchPresentationTests.Sta(() =>
    {
        var before=Pixels(15,20,true);var after=Pixels(16,20,true);var difference=0;
        // One 60Hz entry step moves the pose by less than 0.1 native DIPs.
        // A multi-DIP endpoint switch is visible across this neck-only patch.
        for(var y=60;y<75;y++)for(var x=10;x<40;x++)for(var c=0;c<4;c++)
            difference+=Math.Abs(after[(y*96+x)*4+c]-before[(y*96+x)*4+c]);
        Assert.True(difference/(30d*15*4)<2,$"First crouch step abruptly changes the neck: channel delta {difference}.");
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Raised_head_keeps_an_inked_front_connection_during_crouch_entry(bool closed) => EdgePerchPresentationTests.Sta(() =>
    {
        var pixels=Pixels(28,20,closed);
        // Measured gap between the raised left hair tip and the existing far paw.
        // Every scanline must meet visible ink, rather than a white ownership cut.
        for(var y=65;y<=67;y++)
        {
            var ink=false;
            for(var x=14;x<=19;x++)
            {
                var i=(y*96+x)*4;
                if(pixels[i+3]>=128&&pixels[i+2]*255d/pixels[i+3]<175)ink=true;
            }
            Assert.True(ink,$"Front connection has no outline on row {y}.");
        }
    });

    [Theory]
    [InlineData(false,25,76)]
    [InlineData(true,25,76)]
    [InlineData(false,21,78)]
    [InlineData(true,21,78)]
    [InlineData(false,47,78)]
    [InlineData(true,47,78)]
    public void Lowered_head_backing_does_not_erase_the_visible_paw_boundary(bool closed,int x,int y) => EdgePerchPresentationTests.Sta(() =>
    {
        var pixels=Pixels(42,0,closed);var i=(y*96+x)*4;
        Assert.True(pixels[i+3]>=128,"The existing silhouette must remain covered.");
        var red=pixels[i+2]*255d/pixels[i+3];
        Assert.True(red<180,$"Exposed paw boundary ({x},{y}) is white: straight red {red:F1}.");
    });

    private static byte[] Pixels(int frame,double degrees,bool closed)
    {
        var pixels=new byte[96*96*4];
        new HuntRenderer().Render(frame,new(0,.65,degrees*Math.PI/180),closed).CopyPixels(pixels,384,0);
        return pixels;
    }
}
