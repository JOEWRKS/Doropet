using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public sealed class TaskbarPerchReachabilityTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Integer_pixel_taskbar_at_fractional_dpi_ignores_roundoff_but_not_a_real_bottom_gap(double gap, bool expected)
    {
        var scene = new DesktopScene(1, TimeSpan.Zero,
            [new(1, new(0, 0, 1920, 1080))],
            [new(new(1, 1, 1), new(0, 1040 - gap, 1920, 40), 0,
                true, false, false, false, true, true, true, true)]);
        var runtime = new PetPlatformRuntime(new(new Native(scene)),
            _ => new(new(100, 100), new(100, 100), 1.5, 1.5),
            () => (new(44, 74, 135, 48), new(20, 48, 80, 87)), (_, _) => { },
            measurePerch: _ => new(44, 74, 94, 48));
        var position = new PointD(100, 610);
        runtime.BeginFrame(TimeSpan.Zero, position, new(true, position), true);
        Assert.Equal(expected, runtime.CanBeginPerch(position, FacingDirection.Right, true, true));
        Assert.Equal(expected, runtime.TryBeginPerch(position, FacingDirection.Right, true, true));
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(1.5, -1920, -240)]
    public void Transformed_monitor_keeps_same_logical_band(double scale, double x, double y)
    {
        var scene = new DesktopScene(1, TimeSpan.Zero,
            [new(7, new(x, y, 1920 * scale, 1080 * scale))],
            [new(new(3, 3, 3), new(x, y + 1040 * scale, 1920 * scale, 40 * scale), 0,
                true, false, false, false, true, true, true, true)]);
        var runtime = new PetPlatformRuntime(new(new Native(scene)),
            _ => new(new(x, y), new(-500, -100), scale, scale),
            () => (new(44, 74, 135, 48), new(20, 48, 80, 87)), (_, _) => { },
            measurePerch: _ => new(44, 74, 94, 48));
        runtime.BeginFrame(TimeSpan.Zero, new(-400, 835), PointerSample.Unavailable, true);
        Assert.False(runtime.CanBeginPerch(new(-400, 824.99), FacingDirection.Left, true, true));
        Assert.True(runtime.CanBeginPerch(new(-400, 825), FacingDirection.Left, true, true));
        Assert.True(runtime.CanBeginPerch(new(-400, 845), FacingDirection.Left, true, true));
        Assert.False(runtime.CanBeginPerch(new(-400, 845.01), FacingDirection.Left, true, true));
    }

    [Fact]
    public void Actual_tilted_head_silhouettes_remain_eligible_before_the_screen_clamp() => Controls.CheekProductTests.Sta(() =>
    {
        foreach (var facing in new[] { FacingDirection.Left, FacingDirection.Right })
        foreach (var angle in new[] { -20d, 0d, 20d })
        {
            var presenter = new Dororong.App.Controls.DororongPresenter();
            var pet = new PetSnapshot(PetState.Idle, new(100, 100), facing, 0, false, null);
            presenter.Render(pet, Dororong.App.Interaction.DirectInteractionSnapshot.None);
            Controls.PerchExpressionTests.Layout(presenter);
            var image = (System.Windows.Controls.Image)presenter.FindName("DororongImage");
            var local = image.TranslatePoint(new(25, 38), presenter);
            var press = pet.Position + new PointD(local.X, local.Y);
            var pending = new Dororong.App.Interaction.DirectInteractionSnapshot(
                Dororong.App.Interaction.DirectInteractionTarget.Body,
                Dororong.App.Interaction.DirectInteractionPhase.BodyPending, press, press, 0, 0, false);
            presenter.Render(pet, pending);
            presenter.Render(pet with { State = PetState.Dragged, Position = new(180, 100) }, pending with
            {
                Phase = Dororong.App.Interaction.DirectInteractionPhase.BodyDragHold,
                PointerPosition = press + new PointD(80, 0), Strength = 1, RequiresCapture = true, HeadSwingDegrees = angle
            });
            Controls.PerchExpressionTests.Layout(presenter);
            var sole = presenter.MeasurePlatformContact()!.Value.SoleY;
            var runtime = Create(sole);
            runtime.BeginFrame(TimeSpan.Zero, new(100, 900), PointerSample.Unavailable, true);
            var area = runtime.GetMovementArea(new(100, 900), runtime.Contact, new(144, 144));
            var limit = area.ClampTopLeft(new(100, 2000), new(144, 144));
            Assert.InRange(limit.Y + sole, 1079.999999, 1080.000001);
            var reachable = limit - new PointD(0, 10);
            Assert.True(runtime.CanBeginPerch(reachable, facing, true, true));
            Assert.True(runtime.TryBeginPerch(reachable, facing, true, true));
        }
    });

    // Catches eligibility still using an unreachable neutral-grip interval,
    // or readiness and release receiving different held-silhouette limits.
    [Theory]
    [InlineData(127, 933)]
    [InlineData(135, 925)]
    [InlineData(121, 939)]
    public void Bottom_taskbar_preserves_twenty_reachable_dips_and_original_attachment(double sole, double startY)
    {
        foreach (var facing in new[] { FacingDirection.Left, FacingDirection.Right })
        foreach (var offset in new[] { -.01, 0, 10, 20, 20.01 })
        {
            var runtime = Create(sole);
            var position = new PointD(100, startY + offset);
            runtime.BeginFrame(TimeSpan.Zero, position, new(true, position), direct: true);
            var expected = offset >= 0 && offset <= 20;
            Assert.Equal(expected, runtime.CanBeginPerch(position, facing, true, true));
            Assert.Equal(EdgePerchPhase.None, runtime.PerchPhase);
            Assert.Equal(expected, runtime.TryBeginPerch(position, facing, true, true));
            if (!expected) continue;
            Assert.True(runtime.TryAdvancePerch(TimeSpan.FromMilliseconds(160), position, out var attached));
            Assert.Equal(new PointD(100, 946), attached);
            Assert.Equal(EdgePerchPhase.Attached, runtime.PerchPhase);
        }
    }

    [Theory]
    [InlineData(false, 1040)] // ordinary window at exactly the same height
    [InlineData(true, 800)] // taskbar away from monitor bottom
    public void Other_edges_keep_the_original_below_edge_interval(bool taskbar, double top)
    {
        var runtime = Create(135, taskbar, top);
        var above = new PointD(100, top - 95);
        runtime.BeginFrame(TimeSpan.Zero, above, new(true, above), true);
        Assert.False(runtime.CanBeginPerch(above, FacingDirection.Right, true, true));
        Assert.True(runtime.CanBeginPerch(new(100, top - 84), FacingDirection.Right, true, true));
    }

    [Fact]
    public void Bottom_correction_preserves_carry_pointer_and_occlusion_gates()
    {
        var runtime = Create(135);
        var position = new PointD(100, 935);
        runtime.BeginFrame(TimeSpan.Zero, position, new(true, position), true);
        Assert.False(runtime.CanBeginPerch(position, FacingDirection.Right, false, true));
        Assert.False(runtime.CanBeginPerch(position, FacingDirection.Right, true, false));
        Assert.False(runtime.TryBeginPerch(position, FacingDirection.Right, false, true));
        Assert.False(runtime.TryBeginPerch(position, FacingDirection.Right, true, false));
        runtime = Create(135, occluded: true);
        runtime.BeginFrame(TimeSpan.Zero, position, new(true, position), true);
        Assert.False(runtime.CanBeginPerch(position, FacingDirection.Right, true, true));
        Assert.False(runtime.TryBeginPerch(position, FacingDirection.Right, true, true));
    }

    private static PetPlatformRuntime Create(double sole, bool taskbar = true, double top = 1040, bool occluded = false)
    {
        var windows = new List<DesktopWindow>
        {
            new(new(1, 1, 1), new(0, top, 1920, 40), 1, true, false, false, false, true, true, taskbar, taskbar)
        };
        if (occluded) windows.Add(new(new(2, 2, 2), new(80, 1000, 200, 80), 0, true, false, false, false, false, true, false, false));
        return new(new(new Native(new(1, TimeSpan.Zero, [new(1, new(0, 0, 1920, 1080))], windows))),
            _ => new(new(), new(), 1, 1), () => (new(44, 74, sole, 48), new(20, 48, 80, sole - 48)),
            (_, _) => { }, measurePerch: _ => new(44, 74, 94, 48));
    }

    private sealed class Native(DesktopScene scene) : IDesktopSceneNative
    {
        public DesktopScene Capture(TimeSpan now) => scene with { CapturedAt = now };
    }
}
