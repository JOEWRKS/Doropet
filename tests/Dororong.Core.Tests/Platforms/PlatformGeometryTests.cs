using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.Core.Tests.Platforms;

public sealed class PlatformGeometryTests
{
    [Fact]
    public void Taskbar_bottom_metadata_is_clipped_and_other_surfaces_leave_it_unknown()
    {
        var surfaces = PlatformGeometry.Build(Scene(new[] { Monitor(1, 0, 0, 1000, 800) },
            Window(1, 0, 760, 1000, 100, 0, taskbar: true, horizontalTaskbar: true),
            Window(2, 100, 300, 200, 100, 1)), 100);
        Assert.Equal(800d, Assert.Single(surfaces.Where(s => s.Kind == PlatformKind.Taskbar)).Bottom);
        Assert.All(surfaces.Where(s => s.Kind != PlatformKind.Taskbar), s => Assert.Null(s.Bottom));
    }
    [Fact]
    public void Front_window_splits_rear_top_into_literal_visible_intervals()
    {
        var scene = Scene(
            new[] { Monitor(1, 0, 0, 1000, 800) },
            Window(1, 100, 400, 600, 300, z: 1),
            Window(2, 250, 300, 200, 300, z: 0));

        var surfaces = PlatformGeometry.Build(scene, 100);

        Assert.Contains(Surface(1, 1, PlatformKind.Window, 100, 250, 400, 100, 400), surfaces);
        Assert.Contains(Surface(1, 1, PlatformKind.Window, 450, 700, 400, 100, 400), surfaces);
        Assert.DoesNotContain(surfaces, s => s.Key.Handle == 1 && s.Left < 450 && s.Right > 250);
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, false, true)]
    public void Ineligible_front_window_neither_supports_nor_occludes(
        bool visible, bool minimized, bool cloaked, bool excluded)
    {
        var scene = Scene(
            new[] { Monitor(1, 0, 0, 1000, 800) },
            Window(1, 100, 400, 600, 300, z: 1),
            Window(2, 250, 300, 200, 300, z: 0, visible: visible,
                minimized: minimized, cloaked: cloaked, excluded: excluded));

        var surfaces = PlatformGeometry.Build(scene, 100);

        Assert.Contains(Surface(1, 1, PlatformKind.Window, 100, 700, 400, 100, 400), surfaces);
        Assert.DoesNotContain(surfaces, s => s.Key.Handle == 2);
    }

    [Fact]
    public void Visible_non_supporting_popup_still_occludes_rear_top()
    {
        var scene = Scene(
            new[] { Monitor(1, 0, 0, 1000, 800) },
            Window(1, 100, 400, 600, 300, z: 1),
            Window(2, 250, 300, 200, 300, z: 0, canSupport: false, canOcclude: true));

        var surfaces = PlatformGeometry.Build(scene, 100);

        Assert.Contains(Surface(1, 1, PlatformKind.Window, 100, 250, 400, 100, 400), surfaces);
        Assert.Contains(Surface(1, 1, PlatformKind.Window, 450, 700, 400, 100, 400), surfaces);
        Assert.DoesNotContain(surfaces, s => s.Key.Handle == 2);
    }

    [Fact]
    public void Occluder_below_candidate_top_does_not_remove_interval()
    {
        var scene = Scene(
            new[] { Monitor(1, 0, 0, 1000, 800) },
            Window(1, 100, 300, 600, 300, z: 1),
            Window(2, 250, 400, 200, 200, z: 0));

        Assert.Contains(Surface(1, 1, PlatformKind.Window, 100, 700, 300, 100, 300),
            PlatformGeometry.Build(scene, 100));
    }

    [Fact]
    public void Negative_origin_monitor_clips_width_but_not_to_an_artificial_top()
    {
        var scene = Scene(
            new[] { Monitor(7, -1000, -200, 1000, 800) },
            Window(3, -1100, 100, 300, 200, z: 0));

        Assert.Contains(Surface(3, 7, PlatformKind.Window, -1000, -800, 100, -1100, 100),
            PlatformGeometry.Build(scene, 100));
    }

    [Fact]
    public void Offscreen_top_does_not_become_a_platform_at_monitor_clip_edge()
    {
        var scene = Scene(
            new[] { Monitor(1, 0, 0, 1000, 800) },
            Window(1, 100, -50, 300, 200, z: 0));

        Assert.DoesNotContain(PlatformGeometry.Build(scene, 20), s => s.Key.Handle == 1);
    }

    [Fact]
    public void Insufficient_headroom_rejects_window_and_horizontal_taskbar()
    {
        var scene = Scene(
            new[] { Monitor(1, 0, 0, 1000, 800) },
            Window(1, 100, 80, 300, 200, z: 0),
            Window(2, 0, 90, 1000, 40, z: 1, taskbar: true, horizontalTaskbar: true));

        var surfaces = PlatformGeometry.Build(scene, 100);

        Assert.DoesNotContain(surfaces, s => s.Key.Handle is 1 or 2);
    }

    [Fact]
    public void Adds_one_floor_per_real_monitor_without_filling_disconnected_gap()
    {
        var scene = Scene(new[]
        {
            Monitor(4, -800, 0, 800, 600),
            Monitor(9, 400, 100, 600, 700)
        });

        var floors = PlatformGeometry.Build(scene, 100).Where(s => s.Kind == PlatformKind.Floor).ToArray();

        Assert.Equal(new[]
        {
            Surface(0, 4, PlatformKind.Floor, -800, 0, 600, -800, 600, generation: 4),
            Surface(0, 9, PlatformKind.Floor, 400, 1000, 800, 400, 800, generation: 9)
        }, floors);
        Assert.DoesNotContain(floors, s => s.Left < 400 && s.Right > 0);
    }

    [Fact]
    public void Horizontal_visible_taskbar_supports_but_vertical_or_hidden_taskbar_does_not()
    {
        var scene = Scene(
            new[] { Monitor(1, 0, 0, 1000, 800) },
            Window(1, 0, 760, 1000, 40, z: 0, taskbar: true, horizontalTaskbar: true),
            Window(2, 960, 0, 40, 800, z: 0, taskbar: true, horizontalTaskbar: false),
            Window(3, 0, 758, 1000, 2, z: 0, visible: false, taskbar: true, horizontalTaskbar: true));

        var surfaces = PlatformGeometry.Build(scene, 100);

        Assert.Contains(Surface(1, 1, PlatformKind.Taskbar, 0, 1000, 760, 0, 760), surfaces);
        Assert.DoesNotContain(surfaces, s => s.Key.Handle is 2 or 3);
    }

    [Theory]
    [InlineData(double.NaN, 0, 100, 100)]
    [InlineData(0, double.PositiveInfinity, 100, 100)]
    [InlineData(0, 0, double.NaN, 100)]
    [InlineData(0, 0, 100, double.NegativeInfinity)]
    [InlineData(0, 0, 0, 100)]
    [InlineData(0, 0, 100, -1)]
    public void Malformed_window_rectangles_are_rejected(double x, double y, double width, double height)
    {
        var scene = Scene(
            new[] { Monitor(1, 0, 0, 1000, 800) },
            Window(1, x, y, width, height, z: 0));

        Assert.DoesNotContain(PlatformGeometry.Build(scene, 100), s => s.Key.Handle == 1);
    }

    [Fact]
    public void Malformed_monitors_and_non_finite_body_height_are_rejected_consistently()
    {
        var badMonitorScene = Scene(new[] { Monitor(1, 0, 0, double.NaN, 800) });
        var goodScene = Scene(new[] { Monitor(1, 0, 0, 1000, 800) });

        Assert.Empty(PlatformGeometry.Build(badMonitorScene, 100));
        Assert.Empty(PlatformGeometry.Build(goodScene, double.NaN));
        Assert.Empty(PlatformGeometry.Build(goodScene, double.PositiveInfinity));
        Assert.Empty(PlatformGeometry.Build(goodScene, -1));
    }

    [Fact]
    public void Same_height_front_window_wins_by_z_order_and_keeps_owner_identity()
    {
        var scene = Scene(
            new[] { Monitor(1, 0, 0, 1000, 800) },
            Window(1, 100, 400, 600, 200, z: 1),
            Window(2, 250, 400, 200, 200, z: 0));

        var surfaces = PlatformGeometry.Build(scene, 100);

        Assert.Contains(Surface(2, 1, PlatformKind.Window, 250, 450, 400, 250, 400), surfaces);
        Assert.Contains(Surface(1, 1, PlatformKind.Window, 100, 250, 400, 100, 400), surfaces);
        Assert.Contains(Surface(1, 1, PlatformKind.Window, 450, 700, 400, 100, 400), surfaces);
    }

    [Fact]
    public void Fast_fall_hits_first_top_not_lower_top()
    {
        var upper = Surface(1, 1, PlatformKind.Window, 0, 300, 300, 0, 300);
        var lower = Surface(2, 1, PlatformKind.Window, 0, 300, 500, 0, 500);

        var hit = PlatformGeometry.FirstCrossing(new[] { lower, upper },
            new(100, 120, 250, 160), new(100, 120, 650, 560));

        Assert.Equal(upper, hit);
    }

    [Fact]
    public void Diagonal_sweep_uses_horizontal_overlap_at_crossing_time()
    {
        var platform = Surface(1, 1, PlatformKind.Window, 100, 200, 300, 100, 300);

        var hit = PlatformGeometry.FirstCrossing(new[] { platform },
            new(0, 20, 200, 100), new(300, 320, 400, 300));

        Assert.Equal(platform, hit);
    }

    [Theory]
    [InlineData(300, 300)]
    [InlineData(350, 300)]
    public void Zero_or_upward_delta_y_has_no_crossing(double beforeY, double afterY)
    {
        var platform = Surface(1, 1, PlatformKind.Window, 0, 300, 300, 0, 300);

        Assert.Null(PlatformGeometry.FirstCrossing(new[] { platform },
            new(100, 120, beforeY, 0), new(100, 120, afterY, 0)));
    }

    [Fact]
    public void Contact_requires_at_least_two_coordinate_units_of_overlap()
    {
        var platform = Surface(1, 1, PlatformKind.Window, 100, 200, 300, 100, 300);

        Assert.Null(PlatformGeometry.FirstCrossing(new[] { platform },
            new(99, 101, 200, 100), new(99, 101, 400, 300)));
        Assert.Equal(platform, PlatformGeometry.FirstCrossing(new[] { platform },
            new(99, 102, 200, 100), new(99, 102, 400, 300)));
    }

    [Fact]
    public void Tiny_surface_segment_cannot_supply_two_units_of_contact()
    {
        var platform = Surface(1, 1, PlatformKind.Window, 100, 101.5, 300, 100, 300);

        Assert.Null(PlatformGeometry.FirstCrossing(new[] { platform },
            new(90, 110, 200, 100), new(90, 110, 400, 300)));
    }

    [Fact]
    public void Malformed_contact_or_surface_is_ignored()
    {
        var malformed = Surface(1, 1, PlatformKind.Window, 0, 100, double.NaN, 0, 0);
        var valid = Surface(2, 1, PlatformKind.Window, 0, 100, 300, 0, 300);

        Assert.Null(PlatformGeometry.FirstCrossing(new[] { malformed },
            new(10, 20, 200, 100), new(10, 20, 400, 300)));
        Assert.Null(PlatformGeometry.FirstCrossing(new[] { valid },
            new(double.NaN, 20, 200, 100), new(10, 20, 400, 300)));
    }

    private static DesktopScene Scene(IReadOnlyList<DesktopMonitor> monitors,
        params DesktopWindow[] windows) => new(1, TimeSpan.Zero, monitors, windows);

    private static DesktopMonitor Monitor(long id, double x, double y, double width, double height) =>
        new(id, new(x, y, width, height));

    private static DesktopWindow Window(long handle, double x, double y, double width, double height,
        int z, bool visible = true, bool minimized = false, bool cloaked = false,
        bool excluded = false, bool canSupport = true, bool canOcclude = true,
        bool taskbar = false, bool horizontalTaskbar = false) =>
        new(new(handle, 10, 1), new(x, y, width, height), z, visible, minimized, cloaked,
            excluded, canSupport, canOcclude, taskbar, horizontalTaskbar);

    private static PlatformSurface Surface(long handle, long monitorId, PlatformKind kind,
        double left, double right, double top, double ownerX, double ownerY, long generation = 1) =>
        new(new(handle, handle == 0 ? 0u : 10u, generation), monitorId, kind,
            left, right, top, new(ownerX, ownerY)) { Bottom = kind == PlatformKind.Taskbar ? 800 : null };
}
