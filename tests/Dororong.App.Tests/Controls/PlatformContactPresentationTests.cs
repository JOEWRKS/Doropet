using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Controls;

public sealed class PlatformContactPresentationTests
{
    [Fact]
    public void Full_bounds_include_wide_upper_body_outside_support_band() => Sta(() =>
    {
        var pixels=new byte[8*8*4];
        for(var x=0;x<8;x++) pixels[x*4+3]=255;
        pixels[(6*8+3)*4+3]=255;
        var source=BitmapSource.Create(8,8,96,96,PixelFormats.Pbgra32,null,pixels,32); source.Freeze();
        var helper=new PlatformContactPresentation();
        var contact=helper.Measure(source,new TranslateTransform(10,20));
        Assert.Equal(new FootContact(13,14,27,20),contact);
        Assert.Equal(new RectD(10,20,8,7),helper.VisibleBounds);
    });
    [Fact]
    public void Contact_ignores_faint_fringe_and_uses_pixel_bottom_boundary() => Sta(() =>
    {
        var contact = new PlatformContactPresentation().Measure(Fixture(), new TranslateTransform(10, 20));
        Assert.Equal(new FootContact(12, 16, 27, 22), contact);
    });

    [Fact]
    public void Rotated_contact_uses_opaque_corners_not_empty_bounding_box_corners() => Sta(() =>
    {
        var pixels = new byte[4 * 4 * 4]; pixels[3] = 255; pixels[(3 * 4 + 3) * 4 + 3] = 255;
        var source = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Pbgra32, null, pixels, 16); source.Freeze();
        var contact = new PlatformContactPresentation().Measure(source, new RotateTransform(-45));
        Assert.Equal(Math.Sqrt(.5), contact.SoleY, 6);
        Assert.Equal(-Math.Sqrt(.5), contact.VisibleTop, 6);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Support_width_uses_clipped_lower_two_DIP_band_not_wide_head_or_source_bottom_row(bool rotated) => Sta(() =>
    {
        var pixels = new byte[8 * 8 * 4];
        for (var y = 0; y < 2; y++) for (var x = 0; x < 8; x++) pixels[(y * 8 + x) * 4 + 3] = 255;
        for (var y = 3; y <= 6; y++) for (var x = 3; x <= 4; x++) pixels[(y * 8 + x) * 4 + 3] = 255;
        // After 90 degrees this pixel only touches the band's top (Y6).
        // It must not enlarge the supporting interval to X-8.
        if (rotated) pixels[(7 * 8 + 5) * 4 + 3] = 255;
        var source = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Pbgra32, null, pixels, 32); source.Freeze();
        var contact = new PlatformContactPresentation().Measure(source, new RotateTransform(rotated ? 90 : 0));
        Assert.Equal(rotated ? -2 : 3, contact.Left, 6);
        Assert.Equal(rotated ? 0 : 5, contact.Right, 6);
        Assert.Equal(rotated ? 8 : 7, contact.SoleY, 6);
        Assert.Equal(0, contact.VisibleTop, 6);
    });

    [Theory]
    [InlineData(.03, 1.7188733853924696)]
    [InlineData(1, 2)]
    [InlineData(-1, -2)]
    public void Sway_is_radians_once_and_capped_in_degrees(double sway, double degrees) => Sta(() =>
    {
        var p = Presenter(); var image = (Image)p.FindName("DororongImage");
        p.ApplyPlatformPose(Pose(sway), 110);
        var a = image.TranslatePoint(new(30, 40), p); var b = image.TranslatePoint(new(60, 40), p);
        Assert.Equal(degrees, Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI, 6);
        Assert.Equal(110, p.MeasurePlatformContact()!.Value.SoleY, 6);
    });

    [Fact]
    public void Support_band_clips_crossing_pixel_edges_in_presenter_DIP_units() => Sta(() =>
    {
        var source = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Pbgra32, null, new byte[] { 0, 0, 0, 255 }, 4);
        source.Freeze();
        // Parallelogram corners (0,0),(4,4),(4,8),(0,4).
        // Its lower Y6..8 band begins at X2 on the lower-left edge.
        var contact = new PlatformContactPresentation().Measure(source, new MatrixTransform(4, 4, 0, 4, 0, 0));
        Assert.Equal(new FootContact(2, 4, 8, 0), contact);
    });

    [Theory]
    [InlineData(-.02878679656440358, 1.0287867965644036)]
    [InlineData(-.03, 1.03)]
    [InlineData(-1, 1.10)]
    public void Late_landing_extension_is_preserved_and_bounded_without_moving_corrected_sole(double squash, double verticalScale) => Sta(() =>
    {
        var p = Presenter(); var image = (Image)p.FindName("DororongImage");
        // First value is the producer's late-landing p=.75 rebound.
        p.ApplyPlatformPose(Pose(0) with { Phase = PlatformPhase.Landing, Squash = squash }, 110);
        var a = image.TranslatePoint(new(40, 30), p); var b = image.TranslatePoint(new(40, 60), p);
        Assert.Equal(verticalScale, (b.Y - a.Y) / 30, 6);
        Assert.Equal(110, IndependentSole(image, p), 6);
    });

    [Fact]
    public void Actual_frames_contact_explicit_motion_target_in_large_host_and_restore_exact_pixels() => Sta(() =>
    {
        foreach (var state in new[] { PetState.Idle, PetState.Walk, PetState.Curious, PetState.Sleep })
        foreach (var facing in new[] { FacingDirection.Right, FacingDirection.Left })
        foreach (var phase in new[] { 0d, .25, .5, .75 })
        {
            var p = Presenter(state, facing, phase); var host = (Grid)p.Parent;
            var before = Pixels(host); var baseContact = p.MeasurePlatformContact(); Assert.NotNull(baseContact);
            var image = (Image)p.FindName("DororongImage");
            Assert.Equal(IndependentSole(image, p), baseContact.Value.SoleY, 6);
            p.ApplyPlatformPose(Pose(.03) with { Squash = .08 }, 112);
            // The host's 96 DIP gutter is not part of the presenter's contact.
            Assert.InRange(Math.Abs(IndependentSole(image, host) - 208), 0, .5);
            p.ApplyPlatformPose(null); Assert.Equal(before, Pixels(host));
            // Re-render the same temporal sample: locomotion now also advances
            // from elapsed time, independently of the core expression phase.
            p.Render(new(state, default, facing, phase, false, null), DirectInteractionSnapshot.None, TimeSpan.Zero);
            Assert.Equal(before, Pixels(host));
        }
    });

    [Fact]
    public void Applying_without_explicit_target_does_not_invent_one_or_leave_old_layer() => Sta(() =>
    {
        var p = Presenter(); var before = Pixels((Grid)p.Parent);
        p.ApplyPlatformPose(Pose(.03), 100);
        Assert.Equal(100, p.MeasurePlatformContact()!.Value.SoleY, 6);
        p.ApplyPlatformPose(Pose(.03));
        Assert.Equal(before, Pixels((Grid)p.Parent));
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Body_and_cheek_capture_actual_corrected_transform_then_retire_layer_before_overlay(bool cheek, bool mirror) => Sta(() =>
    {
        var p = Presenter(PetState.Walk, mirror ? FacingDirection.Left : FacingDirection.Right); var image = (Image)p.FindName("DororongImage");
        var body = (FrameworkElement)p.FindName("BodyGroup"); var originalTransform = body.RenderTransform;
        p.ApplyPlatformPose(Pose(.03) with { Squash = .08 }, 102);
        Assert.Equal(102, IndependentSole(image, p), 6);
        var sourcePoint = cheek ? new PointD(31, 60) : new PointD(23.25, 77.5);
        var visible = image.TranslatePoint(new(sourcePoint.X, sourcePoint.Y), p);
        DirectInteractionSnapshot direct;
        if (cheek)
        {
            Assert.True(p.TryCreateCheekPullCapture(sourcePoint, out var capture));
            AssertPoint(visible, capture!.SourceToWindow.Transform(new Point(sourcePoint.X, sourcePoint.Y)));
            direct = new(DirectInteractionTarget.RightCheek, DirectInteractionPhase.CheekPull, default, default, 0, 0, true)
                { CheekPull = new(capture, 0) };
        }
        else
        {
            Assert.Equal(DirectInteractionTarget.FiveRegionBody, p.ClassifyOpaqueSourcePoint(sourcePoint, true));
            Assert.True(p.TryCreateBodyPullCapture(sourcePoint, out var capture));
            AssertPoint(visible, capture!.SourceToWindow.Transform(new Point(sourcePoint.X, sourcePoint.Y)));
            var session = new BodyPullSession(); session.Begin(capture, default, new(visible.X, visible.Y));
            direct = new(DirectInteractionTarget.FiveRegionBody, DirectInteractionPhase.BodyLocalPull, default, default, 0, 0, true, session.Current);
        }
        p.Render(new(PetState.Idle, default, FacingDirection.Right, 0, false, null), direct);
        ((Grid)p.Parent).UpdateLayout();
        Assert.Same(originalTransform, body.RenderTransform);
        var overlay = ((Canvas)p.Content).Children.OfType<Image>().Single();
        var pad = cheek ? 0 : BodyPullRenderer.Pad;
        AssertPoint(visible, overlay.TranslatePoint(new(sourcePoint.X + pad, sourcePoint.Y + pad), p));
        p.ApplyPlatformPose(Pose(.03), 90);
        Assert.Same(originalTransform, body.RenderTransform);
        p.Render(new(PetState.Idle, default, FacingDirection.Right, 0, false, null), DirectInteractionSnapshot.None);
        Assert.Same(originalTransform, body.RenderTransform);
    });

    [Fact]
    public void Head_anchor_captures_corrected_pose_before_first_direct_frame() => Sta(() =>
    {
        var p = Presenter(); var image = (Image)p.FindName("DororongImage");
        p.ApplyPlatformPose(Pose(.03), 100);
        Assert.Equal(100, IndependentSole(image, p), 6);
        var point = image.TranslatePoint(new(37.5, 28.25), p); var press = new PointD(point.X, point.Y);
        var direct = new DirectInteractionSnapshot(DirectInteractionTarget.Body, DirectInteractionPhase.BodyDragEntry, press, press, 0, 0, true);
        p.Render(new(PetState.Dragged, default, FacingDirection.Right, 0, false, null), direct);
        AssertPoint(point, image.TranslatePoint(new(37.5, 27.25), p));
    });

    private static PlatformPose Pose(double sway) => new(PlatformPhase.Supported, default, null, 0, sway);
    private static DororongPresenter Presenter(PetState state = PetState.Idle, FacingDirection facing = FacingDirection.Right, double phase = 0)
    {
        var p = new DororongPresenter(); var host = new Grid { Width = 336, Height = 336 }; host.Children.Add(p);
        p.Render(new(state, default, facing, phase, false, null), DirectInteractionSnapshot.None);
        host.Measure(new(336, 336)); host.Arrange(new(0, 0, 336, 336)); host.UpdateLayout(); return p;
    }
    private static BitmapSource Fixture()
    {
        var pixels = new byte[8 * 8 * 4];
        for (var y = 2; y <= 7; y++) for (var x = 2; x <= 5; x++) pixels[(y * 8 + x) * 4 + 3] = (byte)(y == 7 ? 10 : 255);
        var source = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Pbgra32, null, pixels, 32); source.Freeze(); return source;
    }
    private static double IndependentSole(Image image, Visual parent)
    {
        var source = new FormatConvertedBitmap((BitmapSource)image.Source, PixelFormats.Pbgra32, null, 0);
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4]; source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        var max = double.NegativeInfinity;
        var imageToParent = image.TransformToAncestor(parent);
        for (var y = 0; y < source.PixelHeight; y++) for (var x = 0; x < source.PixelWidth; x++)
            if (pixels[(y * source.PixelWidth + x) * 4 + 3] >= 128)
                foreach (var corner in new Point[] { new(x,y), new(x+1,y), new(x,y+1), new(x+1,y+1) })
                    max = Math.Max(max, imageToParent.Transform(corner).Y);
        return max;
    }
    private static byte[] Pixels(FrameworkElement visual)
    {
        visual.UpdateLayout(); var bitmap = new RenderTargetBitmap(336, 336, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var pixels = new byte[336 * 336 * 4]; bitmap.CopyPixels(pixels, 336 * 4, 0); return pixels;
    }
    private static void AssertPoint(Point expected, Point actual) { Assert.Equal(expected.X, actual.X, 6); Assert.Equal(expected.Y, actual.Y, 6); }
    private static void Sta(Action action)
    {
        Exception? error = null; var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
