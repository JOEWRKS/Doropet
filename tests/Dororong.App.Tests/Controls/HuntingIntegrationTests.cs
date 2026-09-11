using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media;

namespace Dororong.App.Tests.Controls;

public sealed class HuntingIntegrationTests
{
    [Theory]
    [InlineData(16,1,17)]
    [InlineData(62.5,4,42)]
    public void Desktop_starts_lowering_next_tick_and_finishes_250ms_after_detection(double tickMs,int steps,int expectedFrame) => EdgePerchPresentationTests.Sta(() =>
    {
        var p=new DororongPresenter();
        var tick=TimeSpan.FromMilliseconds(tickMs);
        for(var i=0;i<=steps;i++)
        {
            p.UpdateHunting(Standing,DirectInteractionSnapshot.None,new(true,new(156,177)),tick,false);
            p.RenderDesktop(Standing,DirectInteractionSnapshot.None,tick);
        }
        // Frame17 visibly begins lowering; frame42 is fully crouched, zero sway.
        var expected=new byte[96*96*4];new HuntRenderer().Render(expectedFrame,default).CopyPixels(expected,384,0);
        var actual=new byte[expected.Length];
        ((BitmapSource)((Image)p.FindName("DororongImage")).Source).CopyPixels(actual,384,0);
        Assert.Equal(expected,actual);
    });
    [Fact]
    public void Finishing_hunt_does_not_restore_an_old_click_facing() => EdgePerchPresentationTests.Sta(() =>
    {
        var p=new DororongPresenter();
        p.RenderDesktop(Standing,DirectInteractionSnapshot.None,Tick);
        p.RenderDesktop(Standing with {State=PetState.ClickReaction,Phase=.8},DirectInteractionSnapshot.None,Tick);
        p.RenderDesktop(Standing,DirectInteractionSnapshot.None,Tick);
        var turned=Standing with {Facing=FacingDirection.Right};
        for(var i=0;i<12;i++)
        {
            p.UpdateHunting(turned,DirectInteractionSnapshot.None,new(true,new(230,181)),Tick,false);
            p.RenderDesktop(turned,DirectInteractionSnapshot.None,Tick);
        }
        for(var i=0;i<40;i++)
        {
            p.UpdateHunting(turned,DirectInteractionSnapshot.None,PointerSample.Unavailable,Tick,false);
            p.RenderDesktop(turned,DirectInteractionSnapshot.None,Tick);
        }
        Assert.Equal(-1,((ScaleTransform)p.FindName("BodyScaleTransform")).ScaleX);
    });
    [Fact]
    public void Tiny_motion_across_entry_boundary_does_not_release_hunt() => EdgePerchPresentationTests.Sta(() =>
    {
        var p=new DororongPresenter();
        Assert.True(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,new(true,new(248,181)),Tick,false));
        for(var i=0;i<50;i++)
            Assert.True(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,new(true,new(253,181)),Tick,false));
        for(var i=0;i<35;i++)p.UpdateHunting(Standing,DirectInteractionSnapshot.None,new(true,new(400,181)),Tick,false);
        Assert.False(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,new(true,new(400,181)),Tick,false));
    });
    [Fact]
    public void Lowered_visible_cheek_is_not_mistaken_for_a_front_paw() => EdgePerchPresentationTests.Sta(() =>
    {
        var p=new DororongPresenter();
        // Neutral gaze target: source (32,53), presenter (56,77).
        var pointer=new PointerSample(true,new(156,177));
        for(var i=0;i<11;i++)
        {
            p.UpdateHunting(Standing,DirectInteractionSnapshot.None,pointer,Tick,false);
            p.RenderDesktop(Standing,DirectInteractionSnapshot.None,Tick);
        }
        var cheek=HuntPose.At(1).Head(18,63);
        Assert.Equal(DirectInteractionTarget.RightCheek,p.ClassifyOpaqueSourcePoint(cheek,true));
        p.Measure(new Size(144,144));p.Arrange(new Rect(0,0,144,144));p.UpdateLayout();
        Assert.True(p.TryCreateCheekPullCapture(cheek,out var capture));
        var image=(Image)p.FindName("DororongImage");var before=new byte[96*96*4];
        new FormatConvertedBitmap(image.Source as BitmapSource,PixelFormats.Bgra32,null,0).CopyPixels(before,384,0);
        Assert.Equal(before,capture!.Render(0));
        var pulled=capture.Render(20);Assert.NotEqual(before,pulled);
        // Rump and rear foot must not enter the rotated head's cheek warp.
        for(var y=70;y<90;y++)for(var x=56;x<80;x++)for(var c=0;c<4;c++)
            Assert.Equal(before[(y*96+x)*4+c],pulled[(y*96+x)*4+c]);
        Assert.Equal(before,capture.Render(0));
    });
    private static readonly PetSnapshot Standing = new(PetState.Idle,new(100,100),FacingDirection.Left,0,false,null);
    private static readonly PointerSample Near = new(true,new(152,171));
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);

    [Fact]
    public void Nearby_mouse_activates_and_missing_pointer_eases_out() => EdgePerchPresentationTests.Sta(() =>
    {
        var p=new DororongPresenter();
        Assert.False(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,new(true,new(700,500)),Tick,false));
        for(var i=0;i<12;i++)
        {
            Assert.True(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,Near,Tick,false));
            p.RenderDesktop(Standing,DirectInteractionSnapshot.None,Tick);
        }
        Assert.IsNotType<BitmapImage>(((Image)p.FindName("DororongImage")).Source);
        Assert.True(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,PointerSample.Unavailable,Tick,false));
        for(var i=0;i<30;i++)p.UpdateHunting(Standing,DirectInteractionSnapshot.None,PointerSample.Unavailable,Tick,false);
        Assert.False(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,PointerSample.Unavailable,Tick,false));
        p.RenderDesktop(Standing,DirectInteractionSnapshot.None,Tick);
        Assert.IsType<BitmapImage>(((Image)p.FindName("DororongImage")).Source);
    });

    [Fact]
    public void Sit_press_platform_and_nonordinary_states_take_priority() => EdgePerchPresentationTests.Sta(() =>
    {
        var p=new DororongPresenter();
        Assert.True(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,Near,Tick,false));
        Assert.False(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,Near,Tick,true));
        p.SetSittingRequested(true);
        Assert.False(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,Near,Tick,false));
        p.SetSittingRequested(false);p.SetLocomotionBlocked(true);
        Assert.False(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,Near,Tick,false));
        p.SetLocomotionBlocked(false);
        Assert.False(p.UpdateHunting(Standing with {State=PetState.ClickReaction},DirectInteractionSnapshot.None,Near,Tick,false));
        Assert.True(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,Near,Tick,false));
    });
}
