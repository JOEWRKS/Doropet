using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Dororong.App.Tests.Controls;

public sealed class PointerTrackingZoneTests
{
    private static readonly PetSnapshot Standing = new(PetState.Idle, new(100,100), FacingDirection.Left, 0, false, null);
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);

    [Theory]
    [InlineData(480,181)]
    [InlineData(-136,181)]
    [InlineData(172,-17)]
    [InlineData(172,379)]
    public void Outer_zone_tracks_upright_without_accumulating_a_jump(double x, double y) => EdgePerchPresentationTests.Sta(() =>
    {
        var p = new DororongPresenter();
        for (var i=0;i<40;i++)
        {
            Assert.True(p.UpdateHuntingWithPounce(Standing, DirectInteractionSnapshot.None, new(true,new(x,y)), Tick, false));
            p.RenderDesktop(Standing, DirectInteractionSnapshot.None, Tick);
            Assert.Equal(PouncePhase.Watch,p.Pounce.Phase);
            Assert.Equal(156,p.HuntingFrame);
        }
        var actual = new byte[96*96*4];
        ((BitmapSource)((Image)p.FindName("DororongImage")).Source).CopyPixels(actual,384,0);
        var neutral = new byte[actual.Length];
        new HuntRenderer().Render(156,default).CopyPixels(neutral,384,0);
        Assert.NotEqual(neutral,actual); // Head/whole-eye tracking is visibly active, not just a hold flag.
    });

    [Theory]
    [InlineData(2)]
    [InlineData(9)]
    [InlineData(14)]
    public void Departure_to_outer_zone_cancels_pose_next_tick_and_reentry_starts_fresh(int preparationTicks) => EdgePerchPresentationTests.Sta(() =>
    {
        var p=new DororongPresenter();
        void Step(double x)
        {
            p.UpdateHuntingWithPounce(Standing,DirectInteractionSnapshot.None,new(true,new(x,181)),Tick,false);
            p.RenderDesktop(Standing,DirectInteractionSnapshot.None,Tick);
        }
        for(var i=0;i<preparationTicks;i++)Step(230);
        Assert.InRange(p.HuntingFrame,15,99);
        Step(340);
        Assert.Equal(156,p.HuntingFrame);
        Assert.Equal(PouncePhase.Watch,p.Pounce.Phase);
        Step(230);
        Assert.Equal(15,p.HuntingFrame);
        for(var i=0;i<13;i++)Step(230);
        Assert.Equal(PouncePhase.Watch,p.Pounce.Phase);
        Step(230);
        Assert.Equal(PouncePhase.Flight,p.Pounce.Phase);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Far_or_unavailable_pointer_releases_preparation_immediately(bool unavailable) => EdgePerchPresentationTests.Sta(() =>
    {
        var p=new DororongPresenter();
        for(var i=0;i<12;i++)
        {
            p.UpdateHuntingWithPounce(Standing,DirectInteractionSnapshot.None,new(true,new(230,181)),Tick,false);
            p.RenderDesktop(Standing,DirectInteractionSnapshot.None,Tick);
        }
        var pointer=unavailable ? PointerSample.Unavailable : new(true,new(600,181));
        Assert.False(p.UpdateHuntingWithPounce(Standing,DirectInteractionSnapshot.None,pointer,Tick,false));
        p.RenderDesktop(Standing,DirectInteractionSnapshot.None,Tick);
        Assert.Equal(156,p.HuntingFrame); // Preparation ends now; gaze/source recover visually.
        Assert.Equal(PouncePhase.Watch,p.Pounce.Phase);
        for(var i=0;i<10;i++)
        {
            Assert.False(p.UpdateHuntingWithPounce(Standing,DirectInteractionSnapshot.None,pointer,Tick,false));
            p.RenderDesktop(Standing,DirectInteractionSnapshot.None,Tick);
            Assert.Equal(PouncePhase.Watch,p.Pounce.Phase);
        }
        // 12 approach + 1 departure + 10 recovery ticks = 2300ms: ordinary blink is closed.
        Assert.Same(UprightRumpSource.Standing(true),((Image)p.FindName("DororongImage")).Source);
    });

    [Theory]
    [InlineData(115,0,true,true)]
    [InlineData(-115,0,true,true)]
    [InlineData(0,74,true,true)]
    [InlineData(0,-74,true,true)]
    [InlineData(117,0,true,false)]
    [InlineData(0,76,true,false)]
    [InlineData(90,55,true,false)] // Outside inner ellipse despite both axes being inside.
    [InlineData(309,0,true,false)]
    [InlineData(0,199,true,false)]
    [InlineData(311,0,false,false)]
    [InlineData(0,201,false,false)]
    [InlineData(250,140,false,false)] // Outside outer ellipse, not its bounding rectangle.
    public void Expanded_zone_boundaries_gate_tracking_and_continuous_launch_dwell(
        double dx,double dy,bool tracking,bool preparing) => EdgePerchPresentationTests.Sta(() =>
    {
        var p=new DororongPresenter();
        var pointer=new PointerSample(true,new(172+dx,181+dy));
        for(var i=0;i<14;i++)
        {
            Assert.Equal(tracking,p.UpdateHuntingWithPounce(Standing,DirectInteractionSnapshot.None,pointer,Tick,false));
            p.RenderDesktop(Standing,DirectInteractionSnapshot.None,Tick);
            Assert.Equal(PouncePhase.Watch,p.Pounce.Phase);
            if(preparing) Assert.InRange(p.HuntingFrame,15,99);
            else if(tracking) Assert.Equal(156,p.HuntingFrame);
        }
        p.UpdateHuntingWithPounce(Standing,DirectInteractionSnapshot.None,pointer,Tick,false);
        Assert.Equal(preparing?PouncePhase.Flight:PouncePhase.Watch,p.Pounce.Phase);
    });
}
