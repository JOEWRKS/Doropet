using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    [InlineData(FacingDirection.Left)] [InlineData(FacingDirection.Right)]
    public void Perched_body_press_cannot_detach_or_replace_the_visible_pose(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        using var h = PerchLocalAttach(facing); var p = h.Presenter!;
        var position = h.Position;
        var press = PressImage(h, p.EdgePerchImage, new(40, 70));
        Assert.Equal(DirectInteractionTarget.ClickOnly, press.Target);
        h.Tick(); h.Pointer = new(true, h.Pointer.Position + new PointD(80, -50)); h.Tick(80);
        Assert.Equal(position, h.Position);
        Assert.Equal(EdgePerchPhase.Attached, h.Platforms.PerchPhase);
        Assert.Same(p.EdgePerchImage, VisibleImages(p).Single());
        h.Down = false; h.Tick();
        Assert.Equal(EdgePerchPhase.Attached, h.Platforms.PerchPhase);
        Assert.Equal(h.Captures, h.Releases);
    });

    [Theory]
    [InlineData(FacingDirection.Left)] [InlineData(FacingDirection.Right)]
    public void Perch_regrab_displays_supplied_eight_before_any_pointer_movement(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        using var h=PerchLocalAttach(facing);var p=h.Presenter!;
        var expected=PerchExpressionTests.Pixels(ForelegFlutterTests.Source());
        var position=h.Position;
        var press=PressImage(h,p.EdgePerchImage,new(40,30));
        var anchor=HeadPullAnchoring.Capture(p.EdgePerchImage,p,press.WindowLocalPosition,facing);
        Assert.NotNull(anchor);h.Tick();
        Assert.Equal(DirectInteractionPhase.BodyPending,h.Direct.Phase);
        Assert.Equal(position,h.Position);
        var image=VisibleImages(p).Single();
        Assert.Equal(expected,PerchExpressionTests.Pixels((BitmapSource)image.Source));
        Assert.Equal(facing==FacingDirection.Left?-1:1,Orientation(image,p));
        var correction=HeadPullAnchoring.Correction(image,p,anchor!);
        Assert.InRange(Math.Abs(correction.X),0,.01);Assert.InRange(Math.Abs(correction.Y),0,.01);
        h.Pointer=new(true,h.Pointer.Position+new PointD(1,0));h.Tick();
        Assert.Equal(DirectInteractionPhase.BodyPending,h.Direct.Phase);
        Assert.False(h.Direct.IsPerchReady);
    });

    [Fact]
    public void Perch_regrab_context_does_not_leak_into_the_next_ground_head_drag()
    {
        var controller=new DirectInteractionController();
        controller.BeginDistanceBody(new(100,100),new(4,4));
        controller.SetPressContext(FacingDirection.Left,false,true);
        controller.Cancel();
        controller.BeginDistanceBody(new(100,100),new(4,4));
        controller.SetPressContext(FacingDirection.Right,false);
        var current=controller.Advance(TimeSpan.FromMilliseconds(16),new(true,new(100,88)),true,PetState.Idle,PetState.Dragged);
        Assert.Equal(DirectInteractionPhase.BodyDragEntry,current.Phase);
        Assert.InRange(current.Strength,0.01,.5);
        Assert.False(current.StartsHanging);
    }

    [Theory]
    [InlineData(FacingDirection.Left)] [InlineData(FacingDirection.Right)]
    public void Perch_regrab_short_drag_stays_fully_hanging_and_releases_from_that_pose(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        using var h=PerchLocalAttach(facing);var p=h.Presenter!;
        PressImage(h,p.EdgePerchImage,new(40,30));h.Tick();
        var origin=h.Pointer.Position;
        h.Pointer=new(true,origin+new PointD(0,-12));h.Tick();
        Assert.Equal(DirectInteractionPhase.BodyDragHold,h.Direct.Phase);
        Assert.Equal(1,h.Direct.Strength);
        Assert.Equal(facing,h.Direct.PressFacing);
        Assert.Equal(EdgePerchPhase.None,h.Platforms.PerchPhase);
        Assert.Single(VisibleImages(p));
        h.Pointer=new(true,origin);h.Tick();
        Assert.Equal(DirectInteractionPhase.BodyDragHold,h.Direct.Phase);
        h.Native.Scene=Scene(windows:false);h.Down=false;h.Tick(80);
        Assert.Equal(DirectInteractionPhase.BodyDragSettle,h.Direct.Phase);
        Assert.False(h.Direct.IsPartialDragSettle);
        Assert.Equal(1,h.Direct.Strength);
    });

    [Theory]
    [InlineData("cancel")] [InlineData("dispose")]
    public void PerchLocalInteraction_explicit_cleanup_clears_local_capture_and_owned_visibility(string action) => CheekProductTests.Sta(() =>
    {
        using var h=PerchLocalAttach(FacingDirection.Left);var p=h.Presenter!;
        PressImage(h,p.EdgePerchImage,new(24,52));h.Tick();
        Assert.True(h.Direct.IsAttachedCheek);
        if(action=="cancel")h.Loop.NotifyDirectInteractionCanceled();else h.Dispose();
        Assert.Equal(EdgePerchPhase.None,h.Platforms.PerchPhase);
        Assert.Equal(h.Captures,h.Releases);
        Assert.Same(p.FindName("DororongImage"),VisibleImages(p).Single());
        Assert.Equal(Visibility.Collapsed,p.EdgePerchImage.Visibility);
        Assert.Null(((AlphaHitTestImage)p.FindName("DororongImage")).Clip);
    });

    [Theory]
    [InlineData(FacingDirection.Left)]
    [InlineData(FacingDirection.Right)]
    public void PerchLocalInteraction_real_press_facing_survives_pending_hold_and_perch(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        using var h=PerchLocalAttach(facing);
        Assert.Equal(facing==FacingDirection.Left?-1:1,Orientation(h.Presenter!.EdgePerchImage,h.Presenter));
        Assert.Equal(DirectInteractionTarget.None,h.Direct.Target);
        Assert.Equal(EdgePerchPhase.Attached,h.Platforms.PerchPhase);
    });

    [Theory]
    [InlineData(FacingDirection.Left,20)] [InlineData(FacingDirection.Left,100)]
    [InlineData(FacingDirection.Right,20)] [InlineData(FacingDirection.Right,100)]
    public void PerchLocalInteraction_real_cheek_pull_keeps_owner_paws_and_one_visible_source(FacingDirection facing,double pull) => CheekProductTests.Sta(() =>
    {
        using var h=PerchLocalAttach(facing);var p=h.Presenter!;var attached=h.Position;
        var original=PerchExpressionTests.Pixels((BitmapSource)p.EdgePerchImage.Source);
        var grip=p.EdgePerchImage.TranslatePoint(new(28,64),p);
        var press=PressImage(h,p.EdgePerchImage,new(24,52));
        Assert.True(press.IsAttachedCheek);Assert.Equal(facing,press.PressFacing);
        h.Tick();
        h.Pointer=new(true,h.Pointer.Position+new PointD(facing==FacingDirection.Left?pull:-pull,0));h.Tick(80);
        Assert.Equal(EdgePerchPhase.Attached,h.Platforms.PerchPhase);
        Assert.True(h.Direct.IsAttachedCheek);Assert.Equal(attached,h.Position);Assert.Null(h.LastPose);
        Assert.Equal(20,h.Direct.CheekPull!.PullDips,6);
        var overlay=VisibleImages(p).Single();
        Assert.Equal(96,((BitmapSource)overlay.Source).PixelWidth);
        Assert.Equal(grip,overlay.TranslatePoint(new(20,70),p));
        var localPixels=PerchExpressionTests.Pixels((BitmapSource)overlay.Source);
        for(var y=64;y<90;y++)for(var x=8;x<100;x++)
            Assert.Equal(original.AsSpan((y*100+x)*4,4).ToArray(),localPixels.AsSpan(((y+6)*96+x-8)*4,4).ToArray());
        h.Native.Scene=Scene(x:30,y:176);h.Tick(80);
        Assert.Equal(attached+new PointD(30,-10),h.Position);h.Tick(80);
        Assert.Equal(attached+new PointD(30,-10),h.Position);Assert.Equal(1,h.WritesThisTick);
        h.Down=false;h.Tick();
        for(var i=0;i<16;i++)h.Tick();
        Assert.Equal(DirectInteractionTarget.None,h.Direct.Target);
        Assert.Equal(EdgePerchPhase.Attached,h.Platforms.PerchPhase);
        Assert.Same(p.EdgePerchImage,VisibleImages(p).Single());
        Assert.Same(PerchExpressionFrames.Open,p.EdgePerchImage.Source);
        Assert.Null(p.EdgePerchImage.Clip);
        Assert.Equal(1,h.Captures-1);Assert.Equal(h.Captures,h.Releases);
    });

    [Theory]
    [InlineData(FacingDirection.Left)] [InlineData(FacingDirection.Right)]
    public void PerchLocalInteraction_owner_loss_cancels_local_capture_and_falls_once(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        using var h=PerchLocalAttach(facing);var p=h.Presenter!;
        PressImage(h,p.EdgePerchImage,new(24,52));h.Tick();var start=h.Position;
        h.Pointer=new(true,h.Pointer.Position+new PointD(100,0));h.Tick();
        h.Native.Scene=Scene(windows:false);h.Tick(80);
        Assert.Equal(DirectInteractionTarget.None,h.Direct.Target);Assert.False(h.Direct.IsAttachedCheek);
        Assert.Equal(EdgePerchPhase.None,h.Platforms.PerchPhase);
        Assert.Equal(PlatformPhase.Falling,h.LastPose?.Phase);Assert.True(h.Position.Y>start.Y);
        Assert.Equal(h.Captures,h.Releases);
        h.Down=false;
        var landings=0;var previous=h.LastPose?.Phase;
        for(var i=0;i<150;i++)
        {
            h.Tick();if(h.LastPose?.Phase==PlatformPhase.Landing && previous!=PlatformPhase.Landing)landings++;
            previous=h.LastPose?.Phase;
        }
        Assert.Equal(1,landings);Assert.Single(VisibleImages(p));
    });

    [Theory]
    [InlineData(FacingDirection.Left)] [InlineData(FacingDirection.Right)]
    public void PerchLocalInteraction_noncheek_regrab_detaches_at_displayed_position(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        using var h=PerchLocalAttach(facing);var p=h.Presenter!;var position=h.Position;
        var press=PressImage(h,p.EdgePerchImage,new(40,30));Assert.False(press.IsAttachedCheek);
        h.Tick();Assert.Equal(position,h.Position);Assert.Equal(EdgePerchPhase.None,h.Platforms.PerchPhase);
        Assert.Equal(DirectInteractionPhase.BodyPending,h.Direct.Phase);
        Assert.Equal(facing,h.Direct.PressFacing);
    });

    private static Harness PerchLocalAttach(FacingDirection facing)
    {
        var h=new Harness(realPresenter:true,perchEnabled:true);h.Start();var p=h.Presenter!;
        p.Render(h.Core with { State=PetState.Walk,Facing=facing,Phase=0 },DirectInteractionSnapshot.None);
        PerchExpressionTests.Layout(p);
        h.Native.Scene=Scene(x:0,y:186);
        var image=(AlphaHitTestImage)p.FindName("DororongImage");
        PressImage(h,image,new(40,30));h.Tick(80);
        Assert.Equal(facing,h.Direct.PressFacing);
        Assert.Equal(facing==FacingDirection.Left?-1:1,Orientation(image,p));
        h.Pointer=new(true,h.Pointer.Position+new PointD(100,0));h.Tick(80);
        Assert.Equal(DirectInteractionPhase.BodyDragHold,h.Direct.Phase);
        Assert.Equal(facing==FacingDirection.Left?-1:1,Orientation(image,p));
        h.Down=false;h.Tick();for(var i=0;i<30;i++)h.Tick();
        Assert.Equal(EdgePerchPhase.Attached,h.Platforms.PerchPhase);return h;
    }
    private static DirectInteractionPressEventArgs PressImage(Harness h,AlphaHitTestImage image,Point source)
    {
        var bitmap=(BitmapSource)image.Source;
        var local=new Point(source.X*image.ActualWidth/bitmap.PixelWidth,source.Y*image.ActualHeight/bitmap.PixelHeight);
        var press=Assert.IsType<DirectInteractionPressEventArgs>(h.Presenter!.CreateDirectPress(image,local));
        h.Pointer=new(true,h.Position+press.WindowLocalPosition);h.Down=true;h.Loop.NotifyDirectInteractionPressed(press);return press;
    }
    private static int Orientation(Image image,FrameworkElement p)=>Math.Sign(image.TranslatePoint(new(60,40),p).X-image.TranslatePoint(new(20,40),p).X);
    private static IEnumerable<Image> VisibleImages(DependencyObject root)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);
            if(child is Image { Visibility:Visibility.Visible, Source:BitmapSource } image)yield return image;
            foreach(var descendant in VisibleImages(child))yield return descendant;
        }
    }
}
