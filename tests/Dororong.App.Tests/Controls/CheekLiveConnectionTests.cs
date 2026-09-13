using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class CheekLiveConnectionTests
{
    [Fact]
    public void Capture_remains_available_when_uniform_image_width_changes() => CheekProductTests.Sta(() =>
    {
        var p=Idle();var image=(Image)p.FindName("DororongImage");image.Width=192;image.Height=96;CheekProductTests.Layout(p);
        Assert.True(TryCapture(p,new(16.5,58.5),out _));
    });

    [Fact]
    public void Captured_pixels_are_independent_of_the_callers_mutable_source_buffer() => CheekProductTests.Sta(() =>
    {
        var source=CheekProductTests.Read("canonical.png");var capture=new CheekPullCapture(source,Matrix.Identity,FacingDirection.Right);
        Array.Clear(source);Assert.Equal(ApprovedCheekConnectionTests.Read("max-native.png"),capture.Render(20));
    });

    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    public void Actual_capture_freezes_affine_pose_and_displays_approved_max_then_restores(bool mirror,bool rotated) => CheekProductTests.Sta(() =>
    {
        var p=new DororongPresenter();
        p.Render(new(rotated?PetState.Curious:PetState.Walk,new(100,100),mirror?FacingDirection.Left:FacingDirection.Right,.31,false,null),DirectInteractionSnapshot.None);
        CheekProductTests.Layout(p);
        var image=(Image)p.FindName("DororongImage");
        var a=image.TranslatePoint(new Point(0,0),p);var b=image.TranslatePoint(new Point(1,0),p);var c=image.TranslatePoint(new Point(0,1),p);
        var axis=new Vector(b.X-a.X,b.Y-a.Y);axis.Normalize();
        var capture=Capture(p);var controller=new DirectInteractionController();var press=new PointD(150,150);
        Begin(controller,capture,press);
        var max=controller.Advance(TimeSpan.FromMilliseconds(16),new(true,press-new PointD(axis.X*20,axis.Y*20)),true,PetState.Idle,PetState.Idle);
        p.Render(new(PetState.Idle,new(100,100),FacingDirection.Right,.71,false,null),max,TimeSpan.FromMilliseconds(10000));CheekProductTests.Layout(p);
        var overlay=Overlay(p);
        Assert.Equal(Visibility.Hidden,image.Visibility);
        Assert.Equal(ApprovedCheekConnectionTests.Read("max-native.png",!rotated),Pixels(overlay));
        var positions=new[]{a,b,c};var probes=new[]{new Point(0,0),new Point(1,0),new Point(0,1)};
        for(var i=0;i<3;i++){var actual=overlay.TranslatePoint(probes[i],p);Assert.Equal(positions[i].X,actual.X,8);Assert.Equal(positions[i].Y,actual.Y,8);}
        var released=controller.Advance(TimeSpan.Zero,PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        p.Render(new(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null),released);
        Assert.Equal(ApprovedCheekConnectionTests.Read("max-native.png",!rotated),Pixels(Overlay(p)));
        var half=controller.Advance(TimeSpan.FromMilliseconds(110),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        p.Render(new(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null),half,TimeSpan.FromMilliseconds(110));
        Assert.InRange(half.CheekPull!.PullDips,-3,-2);
        Assert.False(ApprovedCheekConnectionTests.Read("max-native.png").SequenceEqual(Pixels(Overlay(p))));
        var done=controller.Advance(TimeSpan.FromMilliseconds(610),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        p.Render(new(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null),done);CheekProductTests.Layout(p);
        Assert.Equal(DirectInteractionSnapshot.None,done);Assert.Equal(Visibility.Visible,image.Visibility);
        Assert.Empty(((Canvas)p.Content).Children.OfType<Image>());
        Assert.Equal(UprightRumpTests.CleanFixture(CheekProductTests.Read("canonical.png")),Pixels(image));
        Assert.Equal(mirror?-1:1,((ScaleTransform)p.FindName("BodyScaleTransform")).ScaleX);
    });

    [Fact]
    public void Captured_input_is_horizontal_signed_clamped_and_missing_or_invalid_pointer_does_not_jump() => CheekProductTests.Sta(() =>
    {
        var p=Idle();var capture=Capture(p);var controller=new DirectInteractionController();var start=new PointD(100,100);Begin(controller,capture,start);
        Assert.Equal(0,Pull(controller.Advance(TimeSpan.Zero,new(true,new(100,160)),true,PetState.Idle,PetState.Idle)),8);
        Assert.Equal(-10,Pull(controller.Advance(TimeSpan.Zero,new(true,new(140,100)),true,PetState.Idle,PetState.Idle)));
        Assert.Equal(20,Pull(controller.Advance(TimeSpan.Zero,new(true,new(-500,100)),true,PetState.Idle,PetState.Idle)));
        foreach(var pointer in new[]{PointerSample.Unavailable,new PointerSample(true,new(double.NaN,0)),new PointerSample(true,new(double.PositiveInfinity,0))})
            Assert.Equal(20,Pull(controller.Advance(TimeSpan.FromMilliseconds(16),pointer,true,PetState.Idle,PetState.Idle)));
        controller.Advance(TimeSpan.Zero,new(true,new(110,100)),true,PetState.Idle,PetState.Idle);
        var release=controller.Advance(TimeSpan.Zero,PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);Assert.False(release.RequiresCapture);Assert.Equal(-10,Pull(release));
        Assert.Equal(-5,Pull(controller.Advance(TimeSpan.FromMilliseconds(110),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle)));
        controller.Cancel();Assert.Equal(DirectInteractionSnapshot.None,controller.Current);
        Begin(controller,capture,start);Assert.Equal(0,Pull(controller.Current));
    });

    [Fact]
    public void Cancel_restores_original_and_other_targets_cannot_replace_held_capture() => CheekProductTests.Sta(() =>
    {
        var p=Idle();var image=(Image)p.FindName("DororongImage");var controller=new DirectInteractionController();Begin(controller,Capture(p),new(100,100));
        var held=controller.Advance(TimeSpan.Zero,new(true,new(80,100)),true,PetState.Idle,PetState.Idle);
        p.Render(new(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null),held);
        controller.Begin(DirectInteractionTarget.Body,new(10,10),0);Assert.Equal(held,controller.Current);
        controller.Cancel();p.Render(new(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null),controller.Current);
        Assert.Equal(Visibility.Visible,image.Visibility);Assert.Empty(((Canvas)p.Content).Children.OfType<Image>());
        Assert.Equal(UprightRumpTests.CleanFixture(CheekProductTests.Read("canonical.png")),Pixels(image));
    });

    [Fact]
    public void Expanded_eye_mouth_can_capture_but_body_and_sleep_cannot() => CheekProductTests.Sta(() =>
    {
        var p=Idle();Assert.True(TryCapture(p,new(24,52),out _));Assert.True(TryCapture(p,new(28,59),out _));
        Assert.False(TryCapture(p,new(23,67),out _));Assert.False(TryCapture(p,new(39,64),out _));
        p.Render(new(PetState.Sleep,new(100,100),FacingDirection.Right,.2,false,null),DirectInteractionSnapshot.None);
        Assert.False(TryCapture(p,new(16,58),out _));
    });

    internal static DororongPresenter Idle(){var p=new DororongPresenter();p.Render(new(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None);CheekProductTests.Layout(p);return p;}
    internal static object Capture(DororongPresenter p){Assert.True(TryCapture(p,new(16.5,58.5),out var value));return value!;}
    static bool TryCapture(DororongPresenter p,PointD at,out object? capture){var m=typeof(DororongPresenter).GetMethod("TryCreateCheekPullCapture",BindingFlags.Instance|BindingFlags.NonPublic);Assert.NotNull(m);object?[] args=[at,null];var ok=Assert.IsType<bool>(m.Invoke(p,args));capture=args[1];return ok;}
    internal static void Begin(DirectInteractionController c,object capture,PointD at){var m=typeof(DirectInteractionController).GetMethod("BeginCheekPull",BindingFlags.Instance|BindingFlags.NonPublic);Assert.NotNull(m);m.Invoke(c,[capture,at]);}
    static double Pull(DirectInteractionSnapshot snapshot){var property=typeof(DirectInteractionSnapshot).GetProperty("CheekPull",BindingFlags.Instance|BindingFlags.NonPublic);Assert.NotNull(property);var value=property.GetValue(snapshot);Assert.NotNull(value);return (double)value.GetType().GetProperty("PullDips")!.GetValue(value)!;}
    internal static Image Overlay(DororongPresenter p)=>Assert.Single(((Canvas)p.Content).Children.OfType<Image>());
    internal static byte[] Pixels(Image image){var b=new FormatConvertedBitmap((BitmapSource)image.Source,PixelFormats.Bgra32,null,0);var pixels=new byte[96*96*4];b.CopyPixels(pixels,384,0);return pixels;}
}
