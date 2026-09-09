using Dororong.App.Runtime;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public class DesktopCoordinateMapTests
{
    [Fact]
    public void Nonfinite_off_axis_samples_and_origins_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>DesktopCoordinateMap.FromScreenSamples(new(0,0),new(1,double.NaN),new(0,1),new()));
        Assert.Throws<ArgumentOutOfRangeException>(()=>DesktopCoordinateMap.FromScreenSamples(new(0,0),new(1,0),new(double.NaN,1),new()));
        Assert.Throws<ArgumentOutOfRangeException>(()=>DesktopCoordinateMap.FromScreenSamples(new(0,0),new(1,0),new(0,1),new(double.NaN,0)));
    }
    [Fact]
    public void Presenter_samples_and_all_scene_contact_coordinates_share_one_map()
    {
        var map=DesktopCoordinateMap.FromScreenSamples(new(-1500,300),new(-1498.5,300),new(-1500,302),new(-1000,200));
        var scene=new DesktopScene(1,TimeSpan.Zero,new[] {new DesktopMonitor(2,new(-1500,300,1500,800))},
            new[] {new DesktopWindow(new(3,4,5),new(-1350,400,300,200),0,true,false,false,false,true,true,false,false)});
        var converted=map.ToLogicalScene(scene);
        Assert.Equal(new RectD(-1000,200,1000,400),converted.Monitors[0].Bounds);
        Assert.Equal(new RectD(-900,250,200,100),converted.Windows[0].Bounds);
        Assert.Equal(new FootContact(-900,-700,250,200),map.ToLogicalContact(new FootContact(-1350,-1050,400,300)));
        Assert.Equal(new SurfaceKey(3,4,5),converted.Windows[0].Key);
    }
    [Fact]
    public void Rotated_or_degenerate_presenter_samples_reject_affine_contract()
    {
        Assert.Throws<InvalidOperationException>(()=>DesktopCoordinateMap.FromScreenSamples(new(0,0),new(1,1),new(0,1),new()));
        Assert.Throws<ArgumentOutOfRangeException>(()=>DesktopCoordinateMap.FromScreenSamples(new(0,0),new(0,0),new(0,1),new()));
    }
    [Fact]
    public void Negative_origin_and_nonuniform_scale_keep_contact_in_pet_space()
    {
        var map = new DesktopCoordinateMap(new(-1500,300),new(-1000,200),1.5,2);
        Assert.Equal(new PointD(-900,250), map.ToLogical(new(-1350,400)));
        Assert.Equal(new PointD(-1350,400), map.ToPhysical(new(-900,250)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_scales_cannot_produce_contact(double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DesktopCoordinateMap(new(),new(),scale,1).ToLogical(new()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DesktopCoordinateMap(new(),new(),1,scale).ToPhysical(new()));
    }
}
