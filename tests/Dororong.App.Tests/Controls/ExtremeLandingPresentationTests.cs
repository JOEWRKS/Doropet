using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Controls;

public class ExtremeLandingPresentationTests
{
    [Fact]
    public void Landing_body_pixels_are_click_only_but_head_can_still_be_grabbed() => CheekProductTests.Sta(() =>
    {
        var p = Create(); var sole = p.MeasurePlatformGeometry()!.Value.Contact.SoleY;
        p.ApplyPlatformPose(new(PlatformPhase.Landing, new(100,100), new(0,0,1), .574, 0)
            { IsExtremeLanding = true, LegSpread = 1 }, sole);
        var image = ((Canvas)p.FindName("BodyGroup")).Children.OfType<AlphaHitTestImage>().Single(i => i.Visibility == Visibility.Visible);
        Assert.Equal(DirectInteractionTarget.ClickOnly, p.CreateDirectPress(image, new(32 + 88, 32 + 81))!.Target);
        Assert.Equal(DirectInteractionTarget.Body, p.CreateDirectPress(image, new(32 + 35, 32 + 35))!.Target);
    });

    [Theory]
    [InlineData(FacingDirection.Left)] [InlineData(FacingDirection.Right)]
    public void Both_facing_directions_fit_the_product_transparent_viewport(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        var p=Create();p.Render(new(PetState.Idle,new(100,100),facing,0,false,null),DirectInteractionSnapshot.None);
        var window=new Window {Content=p}; var viewport=new Dororong.App.Runtime.PetWindowViewport(window,p);
        p.Measure(new(144,144));p.Arrange(new Rect(0,0,144,144));p.UpdateLayout();
        var sole=p.MeasurePlatformContact()!.Value.SoleY;
        p.ApplyPlatformPose(new(PlatformPhase.Landing,new(100,100),new(0,0,1),.574,0)
            {IsExtremeLanding=true,LegSpread=1},sole);
        var bounds=p.MeasurePlatformGeometry()!.Value.Bounds;
        var gutterX=(window.Width-p.Width)/2;var gutterY=(window.Height-p.Height)/2;
        Assert.InRange(bounds.X+gutterX,0,window.Width);
        Assert.InRange(bounds.Right+gutterX,0,window.Width);
        Assert.InRange(bounds.Y+gutterY,0,window.Height);
        Assert.InRange(bounds.Bottom+gutterY,0,window.Height);
        window.Content=null;window.Close();
    });

    [Fact]
    public void Full_splat_flattens_art_spreads_paws_and_pins_the_sole() => CheekProductTests.Sta(() =>
    {
        var p = Create(); var before = p.MeasurePlatformGeometry()!.Value;
        p.ApplyPlatformPose(new(PlatformPhase.Landing, new(100,100), new(0,0,1), .574, 0)
            { IsExtremeLanding = true, LegSpread = 1 }, before.Contact.SoleY);
        var after = p.MeasurePlatformGeometry()!.Value;
        Assert.Equal(.426, after.Bounds.Height / before.Bounds.Height, 6);
        Assert.True(after.Bounds.Width > before.Bounds.Width * 1.5);
        Assert.Equal(before.Contact.SoleY, after.Contact.SoleY, 6);
        var body = (Canvas)p.FindName("BodyGroup");
        var visible = body.Children.OfType<Image>().Single(i => i.Visibility == Visibility.Visible);
        var pixels = PremultipliedFrame.From((BitmapSource)visible.Source);
        // Front paw tip moves left and rear paw tip right in the original art,
        // independently of the later global flattening transform.
        Assert.True(Alpha(pixels, 32 + 4, 32 + 78) > 0);
        Assert.True(Alpha(pixels, 32 + 88, 32 + 81) > 0);
        var folder = Environment.GetEnvironmentVariable("DORORONG_SPLAT_EVIDENCE");
        if (!string.IsNullOrEmpty(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
            var canvas = new Canvas { Width=240,Height=160,Background=Brushes.White };
            canvas.Children.Add(p); Canvas.SetLeft(p,48);
            canvas.Measure(new(240,160));canvas.Arrange(new Rect(0,0,240,160));canvas.UpdateLayout();
            var bitmap=new RenderTargetBitmap(960,640,384,384,PixelFormats.Pbgra32); bitmap.Render(canvas);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=System.IO.File.Create(System.IO.Path.Combine(folder,"full-splat.png"));encoder.Save(file);
        }
    });

    [Fact]
    public void Hold_freezes_pose_and_finish_restores_original_art_and_layout() => CheekProductTests.Sta(() =>
    {
        var p = Create(); var before = p.MeasurePlatformGeometry()!.Value;
        var image = (Image)p.FindName("DororongImage"); var source = image.Source;
        var pose = new PlatformPose(PlatformPhase.Landing, new(100,100), new(0,0,1), .574, 0)
            { IsExtremeLanding = true, LegSpread = 1 };
        p.ApplyPlatformPose(pose, before.Contact.SoleY);
        var held = p.MeasurePlatformGeometry()!.Value;
        for (var i = 0; i < 10; i++)
        {
            p.Render(new(PetState.Idle,new(100,100),FacingDirection.Right,.66+i*.01,false,null),DirectInteractionSnapshot.None);
            p.ApplyPlatformPose(pose, before.Contact.SoleY);
            Assert.Equal(held.Bounds, p.MeasurePlatformGeometry()!.Value.Bounds);
        }
        p.Render(new(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None);
        p.ApplyPlatformPose(null);
        Assert.Same(source, image.Source);
        Assert.Equal(Visibility.Visible, image.Visibility);
        Assert.Equal(before.Bounds, p.MeasurePlatformGeometry()!.Value.Bounds);
    });

    private static int Alpha(PremultipliedFrame f, int x, int y) => f.Pixels[y*f.Stride+x*4+3];
    private static DororongPresenter Create()
    {
        var p = new DororongPresenter();
        p.Render(new(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None);
        p.Measure(new(144,144));p.Arrange(new Rect(0,0,144,144));p.UpdateLayout();return p;
    }
}
