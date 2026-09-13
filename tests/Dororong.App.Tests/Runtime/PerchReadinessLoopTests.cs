using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;
using Dororong.App.Tests.Controls;

namespace Dororong.App.Tests.Runtime;

public partial class PetLoopPlatformTests
{
    [Theory]
    [InlineData(FacingDirection.Left, -60)] [InlineData(FacingDirection.Left, 60)]
    [InlineData(FacingDirection.Right, -60)] [InlineData(FacingDirection.Right, 60)]
    public void Held_at_taskbar_bottom_stays_ready_through_the_complete_swing_decay(FacingDirection facing, double fling) => CheekProductTests.Sta(() =>
    {
        using var h = new Harness(realPresenter: true, perchEnabled: true);
        h.Native.Scene = new(2, TimeSpan.Zero, [new(1, new(0, 0, 800, 600))],
            [new(new(2, 2, 2), new(0, 560, 800, 40), 0, true, false, false, false, true, true, true, true)]);
        h.Start(); var p = h.Presenter!;
        p.Render(h.Core with { State = PetState.Idle, Facing = facing, Phase = 0 }, DirectInteractionSnapshot.None);
        PerchExpressionTests.Layout(p);
        PressImage(h, (AlphaHitTestImage)p.FindName("DororongImage"), new(25, 38)); h.Tick();
        h.Pointer = new(true, h.Pointer.Position + new PointD(100, 0)); h.Tick(); h.Tick();
        h.Pointer = new(true, h.Pointer.Position + new PointD(fling, 1000));
        for (var i = 0; i < 160; i++)
        {
            h.Tick();
            Assert.Equal(DirectInteractionPhase.BodyDragHold, h.Direct.Phase);
            Assert.True(h.Direct.IsPerchReady, $"A clamped stationary hold lost readiness during swing at tick {i}.");
        }
        h.Down = false; h.Tick();
        Assert.Equal(EdgePerchPhase.Entering, h.Platforms.PerchPhase);
    });

    [Theory]
    [InlineData(FacingDirection.Left)] [InlineData(FacingDirection.Right)]
    public void Bottom_taskbar_real_head_carry_cues_above_old_band_and_releases_to_registered_grip(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        using var h = new Harness(realPresenter: true, perchEnabled: true);
        h.Start(); var p = h.Presenter!;
        p.Render(h.Core with { State = PetState.Idle, Facing = facing, Phase = 0 }, DirectInteractionSnapshot.None);
        PerchExpressionTests.Layout(p);
        h.Native.Scene = new(2, TimeSpan.Zero, [new(1, new(0, 0, 800, 600))],
            [new(new(2, 2, 2), new(0, 560, 800, 40), 0, true, false, false, false, true, true, true, true)]);
        PressImage(h, (AlphaHitTestImage)p.FindName("DororongImage"), new(25, 38)); h.Tick(80);
        h.Pointer = new(true, h.Pointer.Position + new PointD(100, 0)); h.Tick(80);
        h.Pointer = new(true, h.Pointer.Position + new PointD(0, 463 - h.Position.Y));
        h.Tick(80); h.Tick(80);
        Assert.Equal(463, h.Position.Y, 5);
        Assert.Equal(DirectInteractionPhase.BodyDragHold, h.Direct.Phase);
        Assert.True(h.Direct.IsPerchReady);
        Assert.True(h.Position.Y + p.MeasurePlatformContact()!.Value.SoleY <= 600);
        h.Down = false; h.Tick();
        Assert.Equal(EdgePerchPhase.Entering, h.Platforms.PerchPhase);
        for (var i = 0; i < 10; i++) h.Tick();
        Assert.Equal(EdgePerchPhase.Attached, h.Platforms.PerchPhase);
        Assert.Equal(466, h.Position.Y, 5);
    });

    [Theory]
    [InlineData(FacingDirection.Left)] [InlineData(FacingDirection.Right)]
    public void PerchReadiness_actual_render_changes_forelegs_without_changing_position_or_head(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        using var cue=new Harness(realPresenter:true,perchEnabled:true);
        using var plain=new Harness(realPresenter:true,perchEnabled:false);
        foreach(var h in new[]{cue,plain})
        {
            h.Start();var p=h.Presenter!;
            p.Render(h.Core with{State=PetState.Walk,Facing=facing,Phase=0},DirectInteractionSnapshot.None);
            PerchExpressionTests.Layout(p);h.Native.Scene=Scene(x:0,y:186);
            PressImage(h,(AlphaHitTestImage)p.FindName("DororongImage"),new(40,30));h.Tick(80);
            h.Pointer=new(true,h.Pointer.Position+new PointD(100,0));h.Tick(80);
            h.Tick(80);
        }
        Assert.True(cue.Direct.IsPerchReady);Assert.False(plain.Direct.IsPerchReady);
        Assert.Equal(plain.Position,cue.Position);
        var a=(AlphaHitTestImage)cue.Presenter!.FindName("DororongImage");
        var b=(AlphaHitTestImage)plain.Presenter!.FindName("DororongImage");
        Assert.Equal(b.TranslatePoint(new(40,30),plain.Presenter),a.TranslatePoint(new(40,30),cue.Presenter));
        var actual=PerchExpressionTests.Pixels((BitmapSource)a.Source);
        var original=PerchExpressionTests.Pixels((BitmapSource)b.Source);
        Assert.False(original.SequenceEqual(actual));
        // Row51 includes the chin AA. Row52 is the previously over-protected
        // blank body strip where the raised arm contour now meets that chin.
        Assert.Equal(original.Take(52*96*4),actual.Take(52*96*4));
    });

    [Theory]
    [InlineData(FacingDirection.Left)] [InlineData(FacingDirection.Right)]
    public void PerchReadiness_only_eligible_held_carry_cues_then_same_release_attaches(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        using var h=new Harness(realPresenter:true,perchEnabled:true);h.Start();var p=h.Presenter!;
        p.Render(h.Core with {State=PetState.Walk,Facing=facing,Phase=0},DirectInteractionSnapshot.None);
        PerchExpressionTests.Layout(p);
        h.Native.Scene=Scene(x:0,y:186);
        var image=(AlphaHitTestImage)p.FindName("DororongImage");
        PressImage(h,image,new(40,30));h.Tick(80);
        Assert.False(h.Direct.IsPerchReady);
        h.Pointer=new(true,h.Pointer.Position+new PointD(100,0));h.Tick(80);
        Assert.Equal(DirectInteractionPhase.BodyDragHold,h.Direct.Phase);
        Assert.True(h.Direct.IsPerchReady,"Valid held carry must advertise before release.");
        Assert.Equal(EdgePerchPhase.None,h.Platforms.PerchPhase);
        var held=h.Position;
        h.Native.Scene=Scene(x:0,y:held.Y+95);h.Tick(80); // grip1DIP above edge
        Assert.False(h.Direct.IsPerchReady);Assert.Equal(held,h.Position);
        h.Native.Scene=Scene(x:0,y:held.Y+94-20.01);h.Tick(80);
        Assert.False(h.Direct.IsPerchReady);Assert.Equal(held,h.Position);
        h.Native.Scene=Scene(x:0,y:held.Y+94-10);h.Tick(80);
        Assert.True(h.Direct.IsPerchReady);Assert.Equal(held,h.Position);
        h.Down=false;h.Tick();
        Assert.False(h.Direct.IsPerchReady);
        Assert.Equal(EdgePerchPhase.Entering,h.Platforms.PerchPhase);
    });
}
