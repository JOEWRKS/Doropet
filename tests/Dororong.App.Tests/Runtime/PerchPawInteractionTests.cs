using System.Windows;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Tests.Controls;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public partial class PetLoopPlatformTests
{
    [Theory]
    [InlineData(FacingDirection.Left,35)] [InlineData(FacingDirection.Right,35)]
    [InlineData(FacingDirection.Left,49)] [InlineData(FacingDirection.Right,49)]
    public void Attached_paw_recovering_tip_can_be_grabbed_again(FacingDirection facing,double x) => CheekProductTests.Sta(()=>
    {
        using var h=PerchLocalAttach(facing);var p=h.Presenter!;var position=h.Position;
        var first=PressImage(h,p.EdgePerchImage,new Point(x,71));h.Tick();
        h.Pointer=new(true,h.Pointer.Position+new PointD(0,18));h.Tick();h.Down=false;h.Tick();h.Tick();
        var captured=h.Direct.PawPull;
        var second=PressImage(h,p.EdgePerchImage,new Point(x,85));
        Assert.Equal(first.Target,second.Target);h.Tick();
        Assert.Equal(DirectInteractionPhase.PawHold,h.Direct.Phase);
        Assert.Equal(captured,h.Direct.PawPull);
        Assert.Equal(position,h.Position);Assert.Equal(EdgePerchPhase.Attached,h.Platforms.PerchPhase);
    });

    [Theory]
    [InlineData(FacingDirection.Left,"head")] [InlineData(FacingDirection.Right,"head")]
    [InlineData(FacingDirection.Left,"cheek")] [InlineData(FacingDirection.Right,"cheek")]
    [InlineData(FacingDirection.Left,"paw")] [InlineData(FacingDirection.Right,"paw")]
    public void Attached_paw_wave_can_be_interrupted_by_a_new_local_or_head_press(FacingDirection facing,string next) => CheekProductTests.Sta(()=>
    {
        using var h=PerchLocalAttach(facing);var p=h.Presenter!;var position=h.Position;
        PressImage(h,p.EdgePerchImage,new Point(35,71));h.Tick();h.Down=false;h.Tick();h.Tick(50);
        Assert.Equal(DirectInteractionPhase.PawRelease,h.Direct.Phase);
        var source=next switch {"head"=>new Point(40,30),"cheek"=>new Point(24,52),_=>new Point(49,71)};
        var press=PressImage(h,p.EdgePerchImage,source);h.Tick();
        Assert.Equal(press.Target,h.Direct.Target);Assert.Equal(next!="head",h.Direct.RequiresCapture);
        if(next=="head")Assert.Equal(DirectInteractionPhase.BodyPending,h.Direct.Phase);
        Assert.Equal(position,h.Position);
        Assert.Equal(next=="head"?EdgePerchPhase.None:EdgePerchPhase.Attached,h.Platforms.PerchPhase);
        Assert.Single(VisibleImages(p));
    });

    [Theory]
    [InlineData(FacingDirection.Left,35)] [InlineData(FacingDirection.Right,35)]
    [InlineData(FacingDirection.Left,49)] [InlineData(FacingDirection.Right,49)]
    public void Attached_paw_tap_waves_only_clicked_limb_in_place(FacingDirection facing,double x) => CheekProductTests.Sta(()=>
    {
        using var h=PerchLocalAttach(facing);var p=h.Presenter!;var position=h.Position;
        var original=PerchExpressionTests.Pixels((System.Windows.Media.Imaging.BitmapSource)p.EdgePerchImage.Source);
        PressImage(h,p.EdgePerchImage,new Point(x,71));h.Tick();h.Down=false;h.Tick();h.Tick(50);
        Assert.NotEqual(0,h.Direct.PawPull!.Angle);
        var waved=PerchExpressionTests.Pixels((System.Windows.Media.Imaging.BitmapSource)p.EdgePerchImage.Source);
        Assert.False(original.SequenceEqual(waved));
        for(var y=0;y<100;y++)for(var px=0;px<100;px++)
            if(y<64 || ((x==35?px>=43&&px<58:px>=29&&px<43) && original[(y*100+px)*4+3]!=0))
                Assert.Equal(original.AsSpan((y*100+px)*4,4).ToArray(),waved.AsSpan((y*100+px)*4,4).ToArray());
        Assert.Equal(position,h.Position);Assert.Equal(EdgePerchPhase.Attached,h.Platforms.PerchPhase);
        for(var i=0;i<40;i++)h.Tick();
        Assert.Equal(DirectInteractionTarget.None,h.Direct.Target);
        Assert.Equal(position,h.Position);Assert.Equal(h.Captures,h.Releases);Assert.Single(VisibleImages(p));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Attached_paw_owner_loss_cancels_hold_or_wave_without_stale_capture(bool release) => CheekProductTests.Sta(()=>
    {
        using var h=PerchLocalAttach(FacingDirection.Right);var p=h.Presenter!;
        PressImage(h,p.EdgePerchImage,new Point(35,71));h.Tick();
        if(release){h.Down=false;h.Tick();h.Tick(50);}
        h.Native.Scene=Scene(windows:false);h.Tick(80);
        Assert.Null(h.Direct.PawPull);Assert.Equal(DirectInteractionTarget.None,h.Direct.Target);
        Assert.Equal(EdgePerchPhase.None,h.Platforms.PerchPhase);Assert.Equal(PlatformPhase.Falling,h.LastPose?.Phase);
        Assert.Equal(h.Captures,h.Releases);
        // The existing complementary-clipped perch-to-fall handoff lasts120ms.
        for(var i=0;i<8;i++)h.Tick();
        Assert.Single(VisibleImages(p));
    });

    [Theory]
    [InlineData(FacingDirection.Left,35)] [InlineData(FacingDirection.Right,35)]
    [InlineData(FacingDirection.Left,49)] [InlineData(FacingDirection.Right,49)]
    public void Attached_paw_drag_changes_only_the_selected_paw_and_keeps_owner(FacingDirection facing,double x) => CheekProductTests.Sta(()=>
    {
        using var h=PerchLocalAttach(facing);var p=h.Presenter!;var position=h.Position;
        var original=PerchExpressionTests.Pixels((System.Windows.Media.Imaging.BitmapSource)p.EdgePerchImage.Source);
        var press=PressImage(h,p.EdgePerchImage,new Point(x,71));
        Assert.NotEqual(DirectInteractionTarget.ClickOnly,press.Target);
        h.Tick();h.Pointer=new(true,h.Pointer.Position+new PointD(0,18));h.Tick();
        var stretched=PerchExpressionTests.Pixels((System.Windows.Media.Imaging.BitmapSource)p.EdgePerchImage.Source);
        Assert.False(original.SequenceEqual(stretched));
        for(var y=0;y<100;y++)for(var px=0;px<100;px++)
            if(y<63 || (x==35?px>=43:px<43))
                Assert.Equal(original.AsSpan((y*100+px)*4,4).ToArray(),stretched.AsSpan((y*100+px)*4,4).ToArray());
        Assert.Equal(EdgePerchPhase.Attached,h.Platforms.PerchPhase);Assert.Equal(position,h.Position);
        Assert.Single(VisibleImages(p));Assert.False(h.Direct.IsPerchReady);
        h.Down=false;h.Tick();for(var i=0;i<40;i++)h.Tick();
        Assert.Equal(EdgePerchPhase.Attached,h.Platforms.PerchPhase);Assert.Equal(position,h.Position);
        Assert.Equal(DirectInteractionTarget.None,h.Direct.Target);Assert.Equal(h.Captures,h.Releases);
    });
}
