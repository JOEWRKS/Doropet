using Dororong.App.Controls;
using Dororong.App.Tests.Controls;
using Dororong.App.Interaction;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Tests.Runtime;

public class HeadLandingContinuityTests
{
    [Theory]
    [InlineData(12)] [InlineData(24)] [InlineData(40)]
    public void Partial_touchdown_does_not_relocate_the_host_when_recovery_finishes(int lift) => CheekProductTests.Sta(() =>
    {
        var frames=Release(-1,lift);
        var exit=Array.FindIndex(frames,f=>f.Direct.Target==DirectInteractionTarget.None);
        Assert.True(exit>0);
        Assert.Equal(PlatformPhase.Landing,frames[exit].Phase);
        // The world sole may match after composition while HWND placement jumps
        // and the old image is still displayed. Bound the native move itself,
        // across BOTH recovery exit and the following presentation-rebase tick.
        var touchdown=Array.FindIndex(frames,f=>f.Phase==PlatformPhase.Landing);
        for(var i=touchdown+1;i<=exit+2;i++)
            Assert.True(Math.Abs(frames[i].HostY-frames[i-1].HostY)<2,
                $"{lift}DIP release moved HWND {frames[i].HostY-frames[i-1].HostY:F3}DIP at {frames[i].Ms}ms");
    });

    [Theory]
    [InlineData(12)] [InlineData(24)] [InlineData(40)] [InlineData(60)] [InlineData(78)]
    public void Partial_lift_releases_on_the_original_surface_without_pose_jump(int lift) => CheekProductTests.Sta(() =>
    {
        var frames=Release(-1,lift);
        Assert.True(frames[0].Direct.IsPartialDragSettle);
        var first=Array.FindIndex(frames,f=>f.Phase==PlatformPhase.Landing);
        Assert.True(first>=0);
        Assert.DoesNotContain(frames.Skip(first),f=>f.Phase==PlatformPhase.Falling);
        Assert.True(frames[^1].Canonical);
        Assert.Equal(PlatformPhase.Supported,frames[^1].Phase);
        for(var i=1;i<frames.Length;i++)
            Assert.InRange(Math.Abs(frames[i].Height/(1-frames[i].Squash)-frames[i-1].Height/(1-frames[i-1].Squash)),0,3.5);
    });

    // A forced canonical switch can pass all post-contact checks while snapping at contact.
    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(3)]
    [InlineData(30)]
    [InlineData(80)]
    [InlineData(220)]
    public void Recovery_crosses_touchdown_without_a_shape_snap_or_second_fall(int gap) => CheekProductTests.Sta(() =>
    {
        var frames = Release(gap);
        var landing = frames.Where(f => f.Phase == PlatformPhase.Landing).ToArray();
        Assert.NotEmpty(landing);
        Assert.All(landing, f =>
        {
            Assert.Null(f.Direct.HeadLanding);
        });
        Assert.Contains(landing, f => f.Squash > 0);
        Assert.Contains(landing, f => f.Squash < 0);
        var first = Array.FindIndex(frames, f => f.Phase == PlatformPhase.Landing);
        Assert.DoesNotContain(frames.Skip(first), f => f.Phase == PlatformPhase.Falling);
        Assert.Equal(PlatformPhase.Supported, frames[^1].Phase);
        Assert.True(frames[^1].Canonical);
        // Remove intended platform squash; compare consecutive actual visible heights,
        // including first touchdown and the final recovery-to-idle handoff.
        for (var i = 1; i < frames.Length; i++)
        {
            var change = Math.Abs(frames[i].Height / (1 - frames[i].Squash) -
                frames[i - 1].Height / (1 - frames[i - 1].Squash));
            Assert.True(change < 3.5, $"Shape jumped {change:F3} DIP at {frames[i].Ms}ms ({frames[i].Phase})");
        }
        Assert.InRange(frames[^1].LocalSole, 110, 112);
        if (gap >= 80)
            Assert.Contains(frames.Take(first), f => f.Direct.Phase == DirectInteractionPhase.BodyDragSettle);
    });

    // Restoring nearest sampling in release would visibly change contour weight at recovery end.
    [Theory]
    [InlineData(24)] [InlineData(220)]
    public void Released_head_keeps_rest_sampling_before_and_after_touchdown(int lift) => CheekProductTests.Sta(() =>
    {
        var frames = Release(lift < 80 ? -1 : 12, lift);
        Assert.All(frames, f => Assert.Equal(BitmapScalingMode.HighQuality, f.Sampling));
    });

    private sealed record Frame(int Ms, PlatformPhase? Phase, DirectInteractionSnapshot Direct,
        bool Canonical, BitmapScalingMode Sampling, double Squash, double LocalSole, double Height, double HostY);

    private static Frame[] Release(int gap, double lift = 220)
    {
        var presenter = new DororongPresenter
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var grid = new Grid { Width = 336, Height = 336 };
        grid.Children.Add(presenter);
        grid.Measure(new(336, 336));
        grid.Arrange(new(0, 0, 336, 336));
        grid.UpdateLayout();
        var native = new Scene();
        var position = new PointD(400, 929);
        var time = TimeSpan.Zero;
        EventHandler? tick = null;
        var down = false;
        var pointer = PointerSample.Unavailable;
        DirectInteractionSnapshot direct = default;
        PlatformPose? pose = null;
        var runtime = new PetPlatformRuntime(new(native), _ => new(new(), new(), 1, 1),
            presenter.MeasurePlatformGeometry, (v, s) => { pose = v; presenter.ApplyPlatformPose(v, s); });
        var host = new PetLoopHost(() => new(0, 0, 1920, 1080), () => new(144, 144), () => new(4, 4),
            () => pointer, () => down, () => position, v => position = v,
            (s, d) => { direct = d; presenter.Render(s, d); grid.UpdateLayout(); }, () => true, () => { })
        { Platforms = runtime };
        using var loop = new PetLoop(new(() => time, () => { }, () => { }),
            new(h => tick += h, h => tick -= h, () => { }, () => { }), host,
            _ => new(BehaviorTuning.Default with { IdleMin = TimeSpan.FromMinutes(10), IdleMax = TimeSpan.FromMinutes(10) },
                new SeededRandomSource(1), position));
        Exception? fault = null;
        loop.Faulted += (_, e) => fault = e;
        loop.Start();
        void Tick()
        {
            time += TimeSpan.FromMilliseconds(16);
            tick?.Invoke(null, EventArgs.Empty);
            Assert.Null(fault);
        }
        for (var i = 0; i < 10; i++) Tick();
        var image = (Image)presenter.FindName("DororongImage");
        var canonical = Hash((BitmapSource)image.Source);
        var point = image.TranslatePoint(new(40, 30), presenter);
        var local = new PointD(point.X, point.Y);
        var origin = position + local;
        pointer = new(true, origin);
        down = true;
        loop.NotifyDirectInteractionPressed(new(DirectInteractionTarget.Body, local, new(40, 30), -1));
        Tick();
        for (var i = 1; i <= 20; i++)
        {
            pointer = new(true, origin + new PointD(0, -i * lift / 20));
            Tick();
            Assert.NotEqual(DirectInteractionTarget.None, direct.Target);
        }
        if(gap>=0) native.Top = position.Y + runtime.Contact.SoleY + gap;
        down = false;
        var frames = new List<Frame>();
        for (var i = 0; i < 64; i++)
        {
            Tick();
            frames.Add(new((i + 1) * 16, pose?.Phase, direct, Hash((BitmapSource)image.Source) == canonical,
                RenderOptions.GetBitmapScalingMode(image), pose?.Squash ?? 0, runtime.Contact.SoleY,
                presenter.MeasurePlatformGeometry()!.Value.Bounds.Height,position.Y));
        }
        return frames.ToArray();
    }

    private static string Hash(BitmapSource source)
    {
        var bytes = new byte[source.PixelWidth * source.PixelHeight * 4];
        source.CopyPixels(bytes, source.PixelWidth * 4, 0);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private sealed class Scene : IDesktopSceneNative
    {
        internal double? Top;
        public DesktopScene? Capture(TimeSpan now) => new(1, now, [new(1, new(0, 0, 1920, 1080))],
            Top is { } y
                ? [new(new(2, 10, 1), new(0, y, 1920, 1080 - y), 0, true, false, false, false, true, true, false, false)]
                : [new(new(1, 10, 1), new(0, 1040, 1920, 40), 0, true, false, false, false, true, true, true, true)]);
    }
}
