using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public sealed class PerchReadinessHandoffTests
{
    [Theory]
    [InlineData(1,0.5,true)]
    [InlineData(1,1.01,false)]
    [InlineData(1.5,0.4,true)]
    [InlineData(1.5,0.7,false)]
    public void Readback_allowance_is_one_physical_pixel_not_an_expanded_grip_band(double scale,double offset,bool expected)
    {
        var sole=127d;var runtime=new EdgePerchRuntime(_=>new(44,74,94,48),()=>sole);
        Assert.True(runtime.CanBegin(Position,FacingDirection.Left,[new(Bar,0)],[Monitor],advertise:true));
        sole=135;
        Assert.Equal(expected,runtime.TryBegin(Position+new PointD(0,offset),FacingDirection.Left,
            [new(Bar,0)],[Monitor],true,true,true,new(1/scale,1/scale)));
        if(expected)Assert.Equal(Position,runtime.Current.Position);
    }

    private static readonly PointD Position=new(100,473);
    private static readonly DesktopMonitor Monitor=new(1,new(0,0,800,600));
    private static readonly PlatformSurface Bar=new(new(2,2,2),1,PlatformKind.Taskbar,0,800,560,new(0,560)){Bottom=600};

    [Fact]
    public void Published_geometry_survives_pure_queries_but_is_consumed_by_release_attempt()
    {
        var sole=127d;var runtime=new EdgePerchRuntime(_=>new(44,74,94,48),()=>sole);
        Assert.True(runtime.CanBegin(Position,FacingDirection.Left,[new(Bar,0)],[Monitor],advertise:true));
        sole=135;
        Assert.False(runtime.CanBegin(Position,FacingDirection.Left,[new(Bar,0)],[Monitor]));
        Assert.True(runtime.TryBegin(Position,FacingDirection.Left,[new(Bar,0)],[Monitor],true,true,true));
        runtime.Release(Position);
        Assert.False(runtime.TryBegin(Position,FacingDirection.Left,[new(Bar,0)],[Monitor],true,true,true));
    }

    [Theory]
    [InlineData("moved")]
    [InlineData("facing")]
    [InlineData("missing")]
    [InlineData("replaced")]
    [InlineData("covered")]
    [InlineData("monitor")]
    [InlineData("pointer")]
    [InlineData("scene")]
    [InlineData("carry")]
    [InlineData("cleared")]
    [InlineData("new-unready-frame")]
    public void Advertised_cue_does_not_override_changed_context_or_reliability(string change)
    {
        var sole=127d;var runtime=new EdgePerchRuntime(_=>new(44,74,94,48),()=>sole);
        Assert.True(runtime.CanBegin(Position,FacingDirection.Left,[new(Bar,0)],[Monitor],advertise:true));
        sole=135;
        if(change=="cleared")runtime.ClearReadiness();
        if(change=="new-unready-frame")
            Assert.False(runtime.CanBegin(Position,FacingDirection.Left,[new(Bar,0)],[Monitor],advertise:true));
        PerchSurface[] surfaces=change switch
        {
            "missing"=>[],
            "replaced"=>[new(Bar with{Key=new(3,3,3)},0)],
            "covered"=>[new(Bar with{Left=200},0)],
            _=>[new(Bar,0)]
        };
        var monitor=change=="monitor" ? Monitor with{Bounds=new(0,0,800,601)} : Monitor;
        var position=change=="moved" ? Position+new PointD(0,40) : Position;
        var facing=change=="facing" ? FacingDirection.Right : FacingDirection.Left;
        Assert.False(runtime.TryBegin(position,facing,surfaces,[monitor],change!="carry",change!="pointer",change!="scene"));
        Assert.Equal(EdgePerchPhase.None,runtime.Current.Phase);
        Assert.False(runtime.TryBegin(Position,FacingDirection.Left,[new(Bar,0)],[Monitor],true,true,true));
    }
}
