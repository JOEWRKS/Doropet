using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class SurroundingPullProductTests
{
    [Fact]
    public void Carried_head_reversal_changes_lag_before_crossing_the_original_press()=>Sta(()=>
    {
        var p=new DororongPresenter();var origin=new PointD(100,100);
        p.Render(new(PetState.Idle,origin,FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None);Layout(p);
        var image=(Image)p.FindName("DororongImage");var original=(BitmapSource)image.Source;
        var grab=image.TranslatePoint(new Point(37.5,28.25),p);var press=origin+new PointD(grab.X,grab.Y);
        var direct=new DirectInteractionSnapshot(DirectInteractionTarget.Body,DirectInteractionPhase.BodyDragHold,press,press+new PointD(2000,0),1,0,true);
        var pet=new PetSnapshot(PetState.Dragged,origin,FacingDirection.Right,0,false,null);
        p.Render(pet,direct);
        var motion=new SurroundingPullMotion();motion.Step(new(14,0),16);
        for(var tick=1;tick<=100;tick++){p.Render(pet,direct with {PointerPosition=press+new PointD(2000-tick*4,0)});motion.Step(new(-14,0),16);}
        var key=new SuppliedBodyDragFrames(PremultipliedFrame.From(original)).Sample(1);
        Assert.True(motion.Current.X < -13.9 && motion.Far.X < -13.9);
        var expected=Crop(SurroundingPullRenderer.Head(PremultipliedFrame.From(key).Pixels,new(48.5,12.25),motion.Current,motion.Far));
        Assert.Equal(expected,Pixels(image));
        for(var tick=0;tick<250;tick++)p.Render(pet,direct with {PointerPosition=press+new PointD(1600,0)});
        Assert.Equal(PremultipliedFrame.From(key).Pixels,Pixels(image));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Head_secondary_screen_vector_accounts_for_the_actual_capped_rotation(bool mirror)=>Sta(()=>
    {
        var p=new DororongPresenter();var origin=new PointD(100,100);var facing=mirror?FacingDirection.Left:FacingDirection.Right;
        p.Render(new(PetState.Walk,origin,facing,0,false,null),DirectInteractionSnapshot.None);Layout(p);
        Assert.Equal(mirror?-1:1,Math.Sign(((ScaleTransform)p.FindName("BodyScaleTransform")).ScaleX));
        var image=(Image)p.FindName("DororongImage");var original=(BitmapSource)image.Source;
        var grab=image.TranslatePoint(new Point(37.5,28.25),p);var press=origin+new PointD(grab.X,grab.Y);
        var direct=new DirectInteractionSnapshot(DirectInteractionTarget.Body,DirectInteractionPhase.BodyDragHold,press,press+new PointD(80,0),1,0,true){HeadSwingDegrees=30};
        p.Render(new(PetState.Dragged,origin,facing,0,false,null),direct);Layout(p);
        var radians=((RotateTransform)p.FindName("BodyRotateTransform")).Angle*Math.PI/180;
        var motion=new SurroundingPullMotion();motion.Step(new(14,0),16);
        PointD Local(PointD q)=>new((q.X*Math.Cos(radians)+q.Y*Math.Sin(radians))*(mirror?-1:1),-q.X*Math.Sin(radians)+q.Y*Math.Cos(radians));
        var key=new SuppliedBodyDragFrames(PremultipliedFrame.From(original)).Sample(1);
        Assert.Equal(Crop(SurroundingPullRenderer.Head(PremultipliedFrame.From(key).Pixels,new(48.5,12.25),Local(motion.Current),Local(motion.Far))),Pixels(image));
    });

    [Fact]
    public void Body_secondary_release_consumes_the_first_controller_release_tick()=>Sta(()=>
    {
        var p=new DororongPresenter();var pet=new PetSnapshot(PetState.Idle,default,FacingDirection.Right,0,false,null);
        p.Render(pet,DirectInteractionSnapshot.None);Layout(p);Assert.True(p.TryCreateBodyPullCapture(new(23,79),out var captured));var capture=captured!;
        var body=new BodyPullSnapshot(capture.Region,BodyPullPhase.Pulling,default,new(-10,6),capture.Anchor,capture);
        var direct=new DirectInteractionSnapshot(DirectInteractionTarget.FiveRegionBody,DirectInteractionPhase.BodyLocalPull,default,default,0,0,true,body);
        var motion=new SurroundingPullMotion();
        for(var i=0;i<20;i++){p.Render(pet,direct);motion.Step(body.PullSource,16);}
        motion.Release();motion.Step(default,120);
        body=body with{Phase=BodyPullPhase.Settling,PullSource=BodyPullSession.Scale(body.PullSource,.352)};
        p.Render(pet,direct with{BodyPull=body,Phase=DirectInteractionPhase.BodyLocalSettle},TimeSpan.FromMilliseconds(120));
        var image=((Canvas)p.Content).Children.OfType<AlphaHitTestImage>().Single();
        Assert.Equal(SurroundingPullRenderer.Body(capture.Pixels.ToArray(),capture.Region,body.PullSource,capture.Anchor,motion.Current,motion.Far),Pixels(image));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Head_surrounding_response_is_live_delayed_and_reverses_without_changing_grab(bool mirror)=>Sta(()=>
    {
        var p=new DororongPresenter();var origin=new PointD(100,100);var facing=mirror?FacingDirection.Left:FacingDirection.Right;
        p.Render(new(PetState.Walk,origin,facing,0,false,null),DirectInteractionSnapshot.None);Layout(p);
        Assert.Equal(mirror?-1:1,Math.Sign(((ScaleTransform)p.FindName("BodyScaleTransform")).ScaleX));
        var image=(Image)p.FindName("DororongImage");var rest=Pixels(image);
        var grab=image.TranslatePoint(new Point(37.5,28.25),p);var press=origin+new PointD(grab.X,grab.Y);
        var direct=new DirectInteractionSnapshot(DirectInteractionTarget.Body,DirectInteractionPhase.BodyDragHold,press,press+new PointD(80,0),1,0,true);
        var pet=new PetSnapshot(PetState.Dragged,origin+new PointD(80,0),facing,0,false,null);
        p.Render(pet,direct);Layout(p);var first=Pixels(image);
        for(int i=0;i<20;i++)p.Render(pet,direct);
        var held=Pixels(image);
        Assert.False(first.SequenceEqual(held),"A disconnected surrounding renderer cannot produce delayed head response.");
        var actual=image.TranslatePoint(new Point(48.5,12.25),p); // Captured point + independently measured key8 shift(+11,-16).
        Assert.Equal(grab.X,actual.X,6);Assert.Equal(grab.Y,actual.Y,6);
        direct=direct with{PointerPosition=press+new PointD(-80,0)};
        p.Render(pet,direct);var reversalFirst=Pixels(image);
        for(int i=0;i<35;i++)p.Render(pet,direct);
        Assert.False(reversalFirst.SequenceEqual(Pixels(image)),"Reversal must continue to evolve after its first frame.");
        Assert.False(held.SequenceEqual(Pixels(image)));
        p.Render(new(PetState.Idle,origin,facing,0,false,null),DirectInteractionSnapshot.None);Layout(p);
        Assert.Equal(rest,Pixels(image));Assert.Equal(96,((BitmapSource)image.Source).PixelWidth);
    });

    [Theory]
    [InlineData(23,79)]
    [InlineData(43,85)]
    [InlineData(66,82)]
    [InlineData(53,69)]
    [InlineData(69,61)]
    public void All_five_body_regions_use_delayed_secondary_render_and_keep_head_locked(double x,double y)=>Sta(()=>
    {
        var p=new DororongPresenter();var origin=new PointD(100,100);
        p.Render(new(PetState.Idle,origin,FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None);Layout(p);
        Assert.True(p.TryCreateBodyPullCapture(new(x,y),out var captured));var capture=captured!;
        var body=new BodyPullSnapshot(capture.Region,BodyPullPhase.Pulling,origin,new(-10,6),capture.Anchor,capture);
        var direct=new DirectInteractionSnapshot(DirectInteractionTarget.FiveRegionBody,DirectInteractionPhase.BodyLocalPull,default,default,0,0,true,body);
        var pet=new PetSnapshot(PetState.Idle,origin,FacingDirection.Right,0,false,null);
        p.Render(pet,direct);Layout(p);var overlay=((Canvas)p.Content).Children.OfType<AlphaHitTestImage>().Single();var first=Pixels(overlay);
        for(int i=0;i<20;i++)p.Render(pet,direct);
        var held=Pixels(overlay);
        Assert.False(first.SequenceEqual(held),"The real body overlay must continue secondary follow after primary pull stops changing.");
        var baseline=BodyPullRenderer.Render(capture.Pixels,capture.Region,body.PullSource,capture.Anchor);
        Assert.False(baseline.SequenceEqual(held));
        for(var sy=0;sy<96;sy++)for(var sx=0;sx<96;sx++)if(BodyRegionMap.IsProtected(sx,sy))
            Assert.Equal(baseline.AsSpan(((sy+32)*160+sx+32)*4,4).ToArray(),held.AsSpan(((sy+32)*160+sx+32)*4,4).ToArray());
        p.Render(pet,DirectInteractionSnapshot.None);Layout(p);
        Assert.Empty(((Canvas)p.Content).Children.OfType<AlphaHitTestImage>());
        Assert.Equal(Visibility.Visible,((Image)p.FindName("DororongImage")).Visibility);
    });

    static byte[] Pixels(Image image)=>PremultipliedFrame.From((BitmapSource)image.Source).Pixels;
    static byte[] Crop(byte[] padded){var output=new byte[96*96*4];for(var y=0;y<96;y++)Array.Copy(padded,((y+32)*160+32)*4,output,y*384,384);return output;}
    static void Layout(FrameworkElement p){p.Measure(new Size(144,144));p.Arrange(new Rect(0,0,144,144));p.UpdateLayout();}
    static void Sta(Action action){Exception? error=null;var t=new Thread(()=>{try{action();}catch(Exception e){error=e;}});t.SetApartmentState(ApartmentState.STA);t.Start();t.Join();if(error is not null)ExceptionDispatchInfo.Capture(error).Throw();}
}
