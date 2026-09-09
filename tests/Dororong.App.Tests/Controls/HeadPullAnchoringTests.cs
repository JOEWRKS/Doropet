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

public sealed class HeadPullAnchoringTests
{
    // Independent rounded landmarks from canonical and the actual supplied8 images.
    private static readonly PointD[] Shifts = [new(0,-1),new(0,-3),new(1,-7),new(2,-11),new(4,-13),new(6,-15),new(8,-15),new(11,-16)];

    [Theory]
    [InlineData(false, 25.25, 37.5)]
    [InlineData(true, 25.25, 37.5)]
    [InlineData(false, 37.5, 28.25)]
    [InlineData(true, 37.5, 28.25)]
    [InlineData(false, 50.25, 48.5)]
    [InlineData(true, 50.25, 48.5)]
    public void Actual_presenter_keeps_captured_head_point_under_cursor_across_all_eight_keys(bool mirror,double x,double y) => Sta(() =>
    {
        var p=new DororongPresenter(); var initial=new PointD(100,100);
        p.Render(new(PetState.Walk,initial,mirror?FacingDirection.Left:FacingDirection.Right,.31,false,null),DirectInteractionSnapshot.None); Layout(p);
        var image=(Image)p.FindName("DororongImage");
        var windowPoint=image.TranslatePoint(new Point(x,y),p);
        var press=initial+new PointD(windowPoint.X,windowPoint.Y);
        var pending=new DirectInteractionSnapshot(DirectInteractionTarget.Body,DirectInteractionPhase.BodyPending,press,press,0,0,false);
        p.Render(new(PetState.Idle,initial,FacingDirection.Right,0,false,null),pending); Layout(p);
        for(var i=0;i<8;i++)
        {
            var window=initial+new PointD(10+i*11,-10-i*11);
            var pointer=window+new PointD(windowPoint.X,windowPoint.Y);
            p.Render(new(PetState.Dragged,window,FacingDirection.Right,0,false,null),pending with {Phase=DirectInteractionPhase.BodyDragEntry,PointerPosition=pointer,Strength=i/7.0,RequiresCapture=true}); Layout(p);
            var actual=image.TranslatePoint(new Point(x+Shifts[i].X,y+Shifts[i].Y),p);
            Assert.Equal(pointer.X,window.X+actual.X,6);
            Assert.Equal(pointer.Y,window.Y+actual.Y,6);
            // No artwork/source resize, and no foreground clipped by the144px window.
            var frame=PremultipliedFrame.From((BitmapSource)image.Source);
            Assert.Equal(96,frame.Source.PixelWidth); Assert.Equal(96,frame.Source.PixelHeight);
            for(var py=0;py<96;py++) for(var px=0;px<96;px++) if(frame.Pixels[(py*96+px)*4+3]>0)
            {
                var screen=image.TranslatePoint(new Point(px+.5,py+.5),p);
                Assert.InRange(screen.X,0,144); Assert.InRange(screen.Y,0,144);
            }
        }
        p.Render(new(PetState.Idle,initial,FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None); Layout(p);
        var translation=(TranslateTransform)p.FindName("BodyTranslateTransform");
        Assert.Equal(0,translation.X); Assert.Equal(0,translation.Y);
        Assert.Equal(BitmapScalingMode.HighQuality,RenderOptions.GetBitmapScalingMode(image));
    });

    [Fact]
    public void Release_starts_from_held_anchor_and_eases_correction_to_zero() => Sta(() =>
    {
        var p=new DororongPresenter();var origin=new PointD(100,100);
        p.Render(new(PetState.Idle,origin,FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None);Layout(p);
        var image=(Image)p.FindName("DororongImage");var local=image.TranslatePoint(new Point(37,28),p);
        var press=origin+new PointD(local.X,local.Y);
        var direct=new DirectInteractionSnapshot(DirectInteractionTarget.Body,DirectInteractionPhase.BodyDragHold,press,press+new PointD(80,0),1,0,true);
        p.Render(new(PetState.Dragged,new(180,100),FacingDirection.Right,0,false,null),direct);Layout(p);
        var transform=(TranslateTransform)p.FindName("BodyTranslateTransform");
        Assert.Equal(-11,transform.X,6);Assert.Equal(16,transform.Y,6);
        p.Render(new(PetState.Idle,new(180,100),FacingDirection.Right,0,false,null),direct with {Phase=DirectInteractionPhase.BodyDragSettle,RequiresCapture=false});Layout(p);
        Assert.Equal(-11,transform.X,6);Assert.Equal(16,transform.Y,6);
        p.Render(new(PetState.Idle,new(180,100),FacingDirection.Right,0,false,null),direct with {Phase=DirectInteractionPhase.BodyDragSettle,RequiresCapture=false,ReleaseProgress=.5});Layout(p);
        // Recovery now follows registered adjacent keys, not a rounded key5
        // switch. The grab correction must relax, not retain the held offset.
        Assert.InRange(transform.X,-5.5,0);Assert.InRange(transform.Y,0,8);
        p.Render(new(PetState.Idle,new(180,100),FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None);Layout(p);
        Assert.Equal(0,transform.X);Assert.Equal(0,transform.Y);
    });

    private static void Layout(FrameworkElement p){p.Measure(new Size(144,144));p.Arrange(new Rect(0,0,144,144));p.UpdateLayout();}
    private static void Sta(Action action){Exception? error=null;var t=new Thread(()=>{try{action();}catch(Exception e){error=e;}});t.SetApartmentState(ApartmentState.STA);t.Start();t.Join();if(error is not null)ExceptionDispatchInfo.Capture(error).Throw();}
}
