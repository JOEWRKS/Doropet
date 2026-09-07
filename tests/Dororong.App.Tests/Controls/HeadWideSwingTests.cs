using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class HeadWideSwingTests
{
    // Catches the old 35-degree simulation cap even when sufficient display space exists.
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Fast_screen_motion_reaches_near_horizontal_with_a_bounded_angle(int direction)
    {
        var session = new HeadSwingSession();
        var p = new PointD(400, 300);
        session.Advance(TimeSpan.Zero, p, true, DirectInteractionPhase.BodyDragHold, 0);
        double angle = 0;
        for (var i = 0; i < 180; i++)
        {
            p += new PointD(direction * 24, 0); // 1500 screen px/s
            angle = session.Advance(TimeSpan.FromMilliseconds(16), p, true, DirectInteractionPhase.BodyDragHold, 0);
            Assert.InRange(angle, -88, 88);
        }
        Assert.InRange(angle * direction, 85, 88);
    }

    // Catches keeping the old inner-144px safety cap or failing the anchor transform.
    [Theory]
    [InlineData(false, 25.25, 37.5)]
    [InlineData(false, 37.5, 28.25)]
    [InlineData(false, 50.25, 48.5)]
    [InlineData(true, 25.25, 37.5)]
    [InlineData(true, 37.5, 28.25)]
    [InlineData(true, 50.25, 48.5)]
    public void Padded_render_shows_both_full_angles_without_moving_the_grab(bool mirror, double x, double y) => CheekProductTests.Sta(() =>
    {
        foreach (var sign in new[] {-1, 1})
        {
            var (root, presenter, pet, direct, local) = Setup(mirror, new(x,y));
            for (var i = 0; i < 24; i++)
                presenter.Render(pet, direct with { HeadSwingDegrees = sign * 88, PointerPosition = direct.PressOrigin + new PointD(sign*i*24,0) }, TimeSpan.FromMilliseconds(16));
            Layout(root);
            var image = (Image)presenter.FindName("DororongImage");
            var rotation = (RotateTransform)presenter.FindName("BodyRotateTransform");
            Assert.Equal(sign*88, rotation.Angle, 6);
            var grab = image.TranslatePoint(new Point(x+11,y-16),presenter);
            Assert.Equal(local.X,grab.X,6);Assert.Equal(local.Y,grab.Y,6);
            Assert.True(HeadPullAnchoring.FitsViewport(image,root));
            Assert.Equal(BitmapScalingMode.Linear,RenderOptions.GetBitmapScalingMode(image));
            Assert.Equal(96,image.ActualWidth);Assert.Equal(96,image.ActualHeight);
            // Check actual alpha pixel corners against the outer display, not just helper output.
            var pixels=PremultipliedFrame.From((BitmapSource)image.Source);
            for(var sy=0;sy<96;sy++)for(var sx=0;sx<96;sx++)if(pixels.Pixels[(sy*96+sx)*4+3]!=0)
                foreach(var delta in new[]{new Point(0,0),new Point(1,1)})
                {
                    var p=image.TranslatePoint(new Point(sx+delta.X,sy+delta.Y),root);
                    Assert.InRange(p.X,1,335);Assert.InRange(p.Y,1,335);
                }
            var held=rotation.Angle;
            presenter.Render(pet,direct with { Phase=DirectInteractionPhase.BodyDragSettle,HeadSwingDegrees=held },TimeSpan.Zero);
            Assert.Equal(held,rotation.Angle,6);
            for(var i=1;i<=12;i++)
            {
                presenter.Render(pet,direct with { Phase=DirectInteractionPhase.BodyDragSettle,ReleaseProgress=i/12d,HeadSwingDegrees=held },TimeSpan.FromMilliseconds(15));
                Assert.InRange(Math.Abs(rotation.Angle),0,Math.Abs(held));
                Assert.True(HeadPullAnchoring.FitsViewport(image,root));
                held=rotation.Angle;
            }
            Assert.Equal(0,rotation.Angle);
            presenter.Render(pet,DirectInteractionSnapshot.None);
            Assert.Equal(BitmapScalingMode.HighQuality,RenderOptions.GetBitmapScalingMode(image));
        }
    });

    internal static (Grid Root,DororongPresenter Presenter,PetSnapshot Pet,DirectInteractionSnapshot Direct,Point Local) Setup(bool mirror,PointD grab)
    {
        var presenter=new DororongPresenter();var root=new Grid();root.Children.Add(presenter);
        var pet=new PetSnapshot(PetState.Walk,new(100,100),mirror?FacingDirection.Left:FacingDirection.Right,0,false,null);
        presenter.Render(pet,DirectInteractionSnapshot.None);Layout(root);
        var image=(Image)presenter.FindName("DororongImage");
        var local=image.TranslatePoint(new Point(grab.X,grab.Y),presenter);
        var press=pet.Position+new PointD(local.X,local.Y);
        var direct=new DirectInteractionSnapshot(DirectInteractionTarget.Body,DirectInteractionPhase.BodyDragHold,press,press,1,0,true);
        return(root,presenter,pet,direct,local);
    }

    internal static void Layout(Grid root)
    {root.Measure(new Size(336,336));root.Arrange(new Rect(0,0,336,336));root.UpdateLayout();}
}
