using System.Windows.Media;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Interaction;

public sealed class CheekCarryTests
{
    private static readonly RectD Area = new(0,0,800,600);
    private static readonly SizeD Size = new(120,100);

    [Fact]
    public void Sub_deadband_tremor_keeps_the_accepted_target_until_a_larger_move()
    {
        var c=Begin();
        foreach(var p in new[]{new PointD(200.4,200),new PointD(199.3,200),new PointD(200,200.6)})
            for(var i=0;i<10;i++){Assert.Equal(new(100,100),Step(c,16,new(true,p)));Assert.Equal(0,c.Current.CheekPull!.PullDips);}
        for(var i=0;i<80;i++)Step(c,16,new(true,new(188,200)));
        Assert.Equal(12,c.Current.CheekPull!.PullDips);
        for(var i=0;i<30;i++)Step(c,16,new(true,new(187.5,200.4)));
        Assert.Equal(12,c.Current.CheekPull!.PullDips);
        for(var i=0;i<80;i++)Step(c,16,new(true,new(186,200)));
        Assert.Equal(14,c.Current.CheekPull!.PullDips);
    }

    [Theory]
    [InlineData(0,2,-2,0,0,-1,100,50)]
    [InlineData(1.5,0,0,1.5,-1,0,50,100)]
    [InlineData(0.8660254037844386,0.5,-0.5,0.8660254037844386,-0.8660254037844386,-0.5,56.69872981077807,75)]
    public void Carry_uses_the_captured_rotated_scaled_outward_axis(double a,double b,double c,double d,double x,double y,double wantX,double wantY)
    {
        var controller=new DirectInteractionController();var capture=new CheekPullCapture(new byte[96*96*4],new Matrix(a,b,c,d,24,24),FacingDirection.Right);
        controller.BeginCheekCarry(capture,new(100,100),new(200,200));PointD window=default;
        for(var i=0;i<100;i++)
        {
            window=Step(controller,16,new(true,new(200+50*x,200+50*y)));
            Assert.InRange(controller.Current.CheekPull!.PullDips,-10,20);
        }
        Assert.InRange(window.X,wantX-.001,wantX+.001);Assert.InRange(window.Y,wantY-.001,wantY+.001);
    }

    [Fact]
    public void Exactly_twenty_dips_enters_carry_after_filter_settles_but_nineteen_point_nine_does_not()
    {
        var below=Begin();var exact=Begin();PointD belowPosition=default,exactPosition=default;
        for(var i=0;i<150;i++)
        {
            belowPosition=Step(below,16,new(true,new(180.1,200)));
            exactPosition=Step(exact,16,new(true,new(180,200)));
        }
        Assert.Equal(new(100,100),belowPosition);
        Assert.InRange(exactPosition.X,79.99,80.01);
    }

    [Fact]
    public void Vertical_or_inward_only_input_does_not_start_carry()
    {
        foreach(var point in new[]{new PointD(200,350),new PointD(260,200)})
        {
            var c=Begin();PointD window=default;
            for(var i=0;i<80;i++)window=Step(c,16,new(true,point));
            Assert.Equal(new(100,100),window);Assert.InRange(c.Current.CheekPull!.PullDips,-10,0);
        }
    }

    [Fact]
    public void Constant_target_has_same_motion_at_different_tick_cadences()
    {
        var samples=new List<(PointD Window,double Pull)>();
        foreach(var milliseconds in new[]{4,16,33})
        {
            var c=Begin();PointD window=default;
            for(var elapsed=0;elapsed<1056;elapsed+=milliseconds)window=Step(c,milliseconds,new(true,new(140,240)));
            samples.Add((window,c.Current.CheekPull!.PullDips));
        }
        Assert.InRange(samples[0].Window.X,39.99,40.1);Assert.InRange(samples[0].Window.Y,139.9,140.01);
        foreach(var s in samples.Skip(1)){Assert.Equal(samples[0].Window.X,s.Window.X,8);Assert.Equal(samples[0].Window.Y,s.Window.Y,8);Assert.Equal(samples[0].Pull,s.Pull,8);}
    }

    [Fact]
    public void Invalid_samples_do_not_replace_the_finite_target_and_zero_time_does_not_advance()
    {
        var c=Begin();var control=Begin();var pointer=new PointerSample(true,new(140,200));
        var last=Step(c,16,pointer);Step(control,16,pointer);
        foreach(var invalid in new[]{PointerSample.Unavailable,new PointerSample(true,new(double.NaN,0)),new PointerSample(true,new(double.PositiveInfinity,0)),new PointerSample(true,new(double.MaxValue,double.MaxValue))})
        {
            Assert.Equal(last,Step(c,0,invalid));
            last=Step(c,16,invalid);var expected=Step(control,16,PointerSample.Unavailable);
            Assert.Equal(expected,last);Assert.Equal(control.Current.CheekPull!.PullDips,c.Current.CheekPull!.PullDips);
        }
    }

    [Fact]
    public void Release_is_timed_once_and_regrab_has_no_stale_carry_offset()
    {
        var c=Begin();PointD window=default;
        for(var i=0;i<15;i++)window=Step(c,16,new(true,new(140,200)));
        var held=c.Current.CheekPull!.PullDips;var released=Step(c,16,new(true,new(700,500)),false);
        Assert.Equal(window,released);Assert.Equal(held,c.Current.CheekPull!.PullDips);
        Step(c,110,PointerSample.Unavailable,false);
        Assert.Equal(held*.5,c.Current.CheekPull!.PullDips,8);
        var current=c.Current;
        c.Advance(TimeSpan.FromMilliseconds(110),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        Assert.Equal(current,c.Current);
        Assert.Equal(window,Step(c,110,PointerSample.Unavailable,false));Assert.False(c.HasCheekCarry);Assert.Equal(DirectInteractionSnapshot.None,c.Current);
        c.BeginCheekCarry(Capture(),window,new(400,300));Assert.Equal(window,Step(c,16,new(true,new(400,300))));Assert.Equal(0,c.Current.CheekPull!.PullDips);
        c.Cancel();Assert.False(c.HasCheekCarry);Assert.Equal(DirectInteractionSnapshot.None,c.Current);
    }

    private static CheekPullCapture Capture()=>new(new byte[96*96*4],Matrix.Identity,FacingDirection.Right);
    private static DirectInteractionController Begin(){var c=new DirectInteractionController();c.BeginCheekCarry(Capture(),new(100,100),new(200,200));return c;}
    private static PointD Step(DirectInteractionController c,double ms,PointerSample pointer,bool down=true)=>c.AdvanceCheekCarry(TimeSpan.FromMilliseconds(ms),pointer,down,Area,Size);
}
