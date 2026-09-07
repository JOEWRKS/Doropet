using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Interaction;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Interaction;

public sealed class BodyPullTests
{
    [Theory]
    [InlineData(-1, -1)] [InlineData(0, -1)] [InlineData(1, -1)] [InlineData(-1, 0)]
    [InlineData(1, 0)] [InlineData(-1, 1)] [InlineData(0, 1)] [InlineData(1, 1)]
    public void Front_paw_reach_stays_short_during_pulling_carry_reversal_clamp_and_release(double dx, double dy)
    {
        var s = Start();
        for (var step = 1; step <= 120; step++)
        {
            s.Move(new(123 + dx * step, 177 + dy * step)); s.Tick(16);
            ShortReach(s.Current);
        }
        Assert.Equal(BodyPullPhase.Carried, s.Current.Phase);
        Assert.True(BodyPullSession.Length(s.Current.WindowPosition - new PointD(100, 100)) > 20);
        s.Move(new(123 - 200 * dx, 177 - 200 * dy));
        for (var step = 0; step < 25; step++) { s.Tick(16); ShortReach(s.Current); }
        Assert.True(s.Current.PullSource.X * dx + s.Current.PullSource.Y * dy < 0);
        s.Reconcile(new(0, 0)); ShortReach(s.Current);
        Assert.Equal(new(0, 0), s.Current.WindowPosition);
        s.Release();
        for (var step = 0; step < 50; step++) { s.Tick(4); ShortReach(s.Current); }
        Assert.Equal(BodyPullPhase.Idle, s.Current.Phase);
        Assert.Equal(default, s.Current.PullSource);

        static void ShortReach(BodyPullSnapshot snapshot)
            => Assert.InRange(BodyPullSession.Length(snapshot.PullSource + new PointD(1, 8)), 0, 22.0000001);
    }

    [Fact]
    public void Front_paw_hands_excess_reach_to_window_before_normal_carry_distance_without_changing_filter()
    {
        var affine = new Matrix(-1.5, 0, 0, 1.5, 144, 0);
        var front = new BodyPullSession(); var reference = new BodyPullSession();
        var pixels = Source();
        Assert.True(front.Begin(new(BodyRegion.FrontPaw, new(23, 77), affine, pixels), new(100, 100), new(150, 150)));
        Assert.True(reference.Begin(new(BodyRegion.MiddlePaw, new(42, 82), affine, pixels), new(100, 100), new(150, 150)));
        //24 screen DIPs =16 source pixels: below old18 carry threshold, but would
        // stretch the front rest vector(1,8) beyond the new22 root-to-tip cap.
        front.Move(new(150, 174)); reference.Move(new(150, 174));
        for (var step = 0; step < 125; step++)
        {
            front.Tick(4); reference.Tick(4);
            Assert.InRange(BodyPullSession.Length(front.Current.PullSource + new PointD(1, 8)), 0, 22.0000001);
            var inverse = affine; inverse.Invert();
            var accounted = front.Current.PullSource + BodyPullSession.Vector(inverse, front.Current.WindowPosition - new PointD(100, 100));
            Assert.Equal(reference.Current.PullSource.X, accounted.X, 8);
            Assert.Equal(reference.Current.PullSource.Y, accounted.Y, 8);
        }
        Assert.Equal(BodyPullPhase.Carried, front.Current.Phase);
        Assert.True(front.Current.WindowPosition.Y > 100);
        Assert.Equal(BodyPullPhase.Pulling, reference.Current.Phase);
        Assert.Equal(new(100, 100), reference.Current.WindowPosition);
    }

    [Fact]
    public void Suspension_caps_total_backlog_even_with_a_primed_three_millisecond_remainder()
    {
        var primed = Start(); var fresh = Start();
        primed.Tick(3);
        primed.Move(new(153, 185)); fresh.Move(new(153, 185));
        primed.Tick(1000); fresh.Tick(250);
        Assert.Equal(fresh.Current.WindowPosition, primed.Current.WindowPosition);
        Assert.Equal(fresh.Current.PullSource, primed.Current.PullSource);
        primed.Tick(2); fresh.Tick(2);
        Assert.Equal(fresh.Current.PullSource, primed.Current.PullSource);
    }

    [Fact]
    public void Equal_elapsed_input_is_display_rate_independent_and_suspension_backlog_is_capped()
    {
        var snapshots = new List<BodyPullSnapshot>();
        foreach (var hz in new[] { 30, 60, 120 })
        {
            var s = Start(); s.Move(new(153, 185)); for (var n = 0; n < hz; n++) s.Tick(1000d / hz); snapshots.Add(s.Current);
        }
        foreach (var s in snapshots.Skip(1))
        {
            Assert.Equal(snapshots[0].WindowPosition.X, s.WindowPosition.X, 8);
            Assert.Equal(snapshots[0].WindowPosition.Y, s.WindowPosition.Y, 8);
            Assert.Equal(snapshots[0].PullSource.X, s.PullSource.X, 8);
        }
        var a = Start(); var b = Start(); a.Move(new(500, 500)); b.Move(new(500, 500)); a.Tick(10000); b.Tick(250);
        Assert.Equal(a.Current.WindowPosition, b.Current.WindowPosition);
        Assert.Equal(a.Current.PullSource, b.Current.PullSource);
        a.Reconcile(new(0, 0));
        var reach = a.Current.PullSource + new PointD(1, 8);
        Assert.InRange(BodyPullSession.Length(reach), 0, 32.0000001);
    }

    [Fact]
    public void Capture_freezes_source_storage_before_later_frame_buffer_reuse()
    {
        var source = Source(); var capture = new BodyPullCapture(BodyRegion.Belly, new(53, 70), Matrix.Identity, source);
        var pixel = (70 * 96 + 53) * 4; var alpha = source[pixel + 3]; source[pixel + 3] = 0;
        Assert.Equal(alpha, capture.Pixels[pixel + 3]);
    }

    // These literal pixels were read on the unchanged product's 96x96 coordinate grid.
    [Theory]
    [InlineData(23, 77, BodyRegion.FrontPaw)]
    [InlineData(42, 82, BodyRegion.MiddlePaw)]
    [InlineData(66, 80, BodyRegion.RightPaw)]
    [InlineData(53, 70, BodyRegion.Belly)]
    [InlineData(70, 61, BodyRegion.Rump)]
    [InlineData(35, 59, BodyRegion.None)] // face
    [InlineData(59, 36, BodyRegion.None)] // rose
    [InlineData(64, 51, BodyRegion.None)] // ribbon
    [InlineData(44, 69, BodyRegion.None)] // hair tip
    [InlineData(30, 75, BodyRegion.None)] // tiny under-chin connector/fourth paw
    [InlineData(0, 0, BodyRegion.None)]
    [InlineData(70, 90, BodyRegion.None)]
    [InlineData(-0.1, 77, BodyRegion.None)]
    public void Actual_product_pixels_have_one_authored_owner(double x, double y, object expected)
        => Assert.Equal((BodyRegion)expected, BodyRegionMap.Pick(new(x, y), Source()));

    [Fact]
    public void Alpha_zero_cannot_claim_a_body_region()
        => Assert.Equal(BodyRegion.None, BodyRegionMap.Pick(new(23, 77), new byte[96 * 96 * 4]));

    [Fact]
    public void Local_pull_is_filtered_without_window_motion_and_tremor_is_rejected()
    {
        var s = Start();
        s.Move(new(123.5, 177)); s.Tick(100);
        Assert.Equal(default, s.Current.PullSource);
        s.Move(new(135, 177));
        Assert.Equal(default, s.Current.PullSource);
        s.Tick(16);
        Assert.InRange(s.Current.PullSource.X, .1, 4);
        for (var i = 0; i < 40; i++) s.Tick(16);
        Assert.InRange(s.Current.PullSource.X, 10.9, 11.3);
        Assert.Equal(new(100, 100), s.Current.WindowPosition);
    }

    [Fact]
    public void Captured_negative_positions_reverse_after_carry_and_release_retains_clamp()
    {
        var s = Start();
        s.Move(new(170, 177)); s.Tick(250);
        Assert.Equal(BodyPullPhase.Carried, s.Current.Phase);
        Assert.True(s.Current.PullSource.X > 0);
        s.Move(new(-90, -20)); s.Tick(250);
        Assert.Equal(BodyPullPhase.Carried, s.Current.Phase);
        Assert.True(s.Current.PullSource.X < 0);
        s.Reconcile(new(0, 0)); s.Release();
        Assert.False(s.Current.RequiresCapture);
        s.Tick(200);
        Assert.Equal(BodyPullPhase.Idle, s.Current.Phase);
        Assert.Equal(new(0, 0), s.Current.WindowPosition);
        Assert.Equal(default, s.Current.PullSource);
        Assert.True(s.Begin(Capture(), new(0, 0), new(23, 77)));
        s.Tick(16);
        Assert.Equal(default, s.Current.PullSource);
    }

    [Fact]
    public void Visible_mirror_converts_screen_delta_and_bad_updates_do_not_poison_session()
    {
        var capture = new BodyPullCapture(BodyRegion.FrontPaw, new(23, 77), new Matrix(-1, 0, 0, 1, 96, 0), Source());
        var s = new BodyPullSession(); Assert.True(s.Begin(capture, new(100, 100), new(173, 177)));
        s.Move(new(161, 177)); s.Tick(250);
        Assert.True(s.Current.PullSource.X > 10);
        var before = s.Current; s.Move(new(double.NaN, double.PositiveInfinity)); s.Tick(double.NaN);
        Assert.Equal(before, s.Current);
    }

    [Fact]
    public void Release_before_first_tick_discards_delayed_pointer_and_region_stays_locked()
    {
        var s = Start(); s.Move(new(800, 900)); s.Release();
        Assert.False(s.Begin(Capture(), new(100, 100), new(123, 177)));
        s.Tick(200);
        Assert.Equal(new(100, 100), s.Current.WindowPosition);
        Assert.Equal(default, s.Current.PullSource);
    }

    internal static BodyPullSession Start()
    {
        var s = new BodyPullSession(); Assert.True(s.Begin(Capture(), new(100, 100), new(123, 177))); return s;
    }
    internal static BodyPullCapture Capture() => new(BodyRegion.FrontPaw, new(23, 77), Matrix.Identity, Source());
    internal static byte[] Source()
    {
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "src"))) root = Directory.GetParent(root)!.FullName;
        var bitmap = new BitmapImage(new Uri(Path.Combine(root, "src/Dororong.App/Assets/dororong-canonical.png")));
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0);
        var pixels = new byte[96 * 96 * 4]; converted.CopyPixels(pixels, 96 * 4, 0); return pixels;
    }
}
