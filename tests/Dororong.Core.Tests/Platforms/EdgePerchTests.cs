using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.Core.Tests.Platforms;

public sealed class EdgePerchTests
{
    private static readonly SurfaceKey OwnerKey = new(1, 1, 1);
    private static readonly PointD Displayed = new(150, 140);
    private static readonly PerchContact Contact = new(20, 50, 70, 10);
    private static readonly DesktopMonitor Monitor = new(1, new(0, 0, 800, 600));

    [Theory]
    [InlineData(199.999,false)] [InlineData(200,true)] [InlineData(220,true)] [InlineData(220.001,false)]
    public void Readiness_uses_release_band_without_acquiring_or_changing_owner(double grip, bool expected)
    {
        var perch=new EdgePerch();var position=Displayed with{Y=grip-Contact.GripY};
        Assert.Equal(expected,perch.CanBegin(position,Contact,[Window()],[Monitor]));
        Assert.Equal(default,perch.Current);
        Assert.Equal(expected,perch.TryBegin(position,Contact,[Window()],[Monitor],true));
        var acquired=perch.Current;
        perch.CanBegin(new(-1000,-1000),Contact,[Window()],[Monitor]);
        Assert.Equal(acquired,perch.Current);
    }

    [Fact]
    public void Released_carry_in_band_enters_then_attaches_at_grip_aligned_position()
    {
        var perch = new EdgePerch();

        Assert.True(perch.TryBegin(Displayed, Contact, [Window()], [Monitor], carryReleased: true));
        Assert.Equal(new EdgePerchPose(EdgePerchPhase.Entering, Displayed, OwnerKey, 1), perch.Current);

        var pose = perch.Advance(TimeSpan.FromMilliseconds(160), Displayed, Contact,
            [Window()], [Monitor], sceneReliable: true);

        Assert.Equal(new EdgePerchPose(EdgePerchPhase.Attached, new(150, 130), OwnerKey, 1), pose);
    }

    [Fact]
    public void Carry_without_button_release_never_enters()
    {
        var perch = new EdgePerch();

        Assert.False(perch.TryBegin(Displayed, Contact, [Window()], [Monitor], carryReleased: false));
        Assert.Equal(EdgePerchPhase.None, perch.Current.Phase);
    }

    [Theory]
    [InlineData(199.999, false)]
    [InlineData(200, true)]
    [InlineData(220, true)]
    [InlineData(220.001, false)]
    public void Entry_band_is_zero_through_twenty_dip_inclusive(double worldGrip, bool expected)
    {
        var displayed = Displayed with { Y = worldGrip - Contact.GripY };

        Assert.Equal(expected,
            new EdgePerch().TryBegin(displayed, Contact, [Window()], [Monitor], carryReleased: true));
    }

    [Fact]
    public void Owner_translation_rebuilds_target_once_from_frozen_relative_x_and_entry_y()
    {
        var perch = Begun();
        var moved = Window(top: 190, left: 130, right: 530, ownerX: 130, ownerY: 190);

        var halfway = perch.Advance(TimeSpan.FromMilliseconds(80), Displayed, Contact,
            [moved], [Monitor], sceneReliable: true);
        Assert.Equal(new PointD(180, 130), halfway.Position);
        Assert.Equal(EdgePerchPhase.Entering, halfway.Phase);

        var attached = perch.Advance(TimeSpan.FromMilliseconds(80), halfway.Position, Contact,
            [moved], [Monitor], sceneReliable: true);
        Assert.Equal(new PointD(180, 120), attached.Position);
        Assert.Equal(EdgePerchPhase.Attached, attached.Phase);

        var stable = perch.Advance(TimeSpan.FromMilliseconds(16), attached.Position, Contact,
            [moved], [Monitor], sceneReliable: true);
        Assert.Equal(new PointD(180, 120), stable.Position);
    }

    [Fact]
    public void Same_handle_and_process_with_new_generation_detaches_at_displayed_position()
    {
        var perch = Begun();
        var reused = Window(key: new(1, 1, 2));
        var actual = new PointD(151, 139);

        var pose = perch.Advance(TimeSpan.FromMilliseconds(16), actual, Contact,
            [reused], [Monitor], sceneReliable: true);

        Assert.Equal(new EdgePerchPose(EdgePerchPhase.None, actual, null, null), pose);
    }

    [Theory]
    [InlineData(171, 500)]
    [InlineData(100, 199)]
    public void Resize_that_loses_either_paw_detaches(double left, double right)
    {
        var perch = Begun();
        var resized = Window(left: left, right: right);

        var pose = perch.Advance(TimeSpan.FromMilliseconds(16), Displayed, Contact,
            [resized], [Monitor], sceneReliable: true);

        Assert.Equal(EdgePerchPhase.None, pose.Phase);
        Assert.Equal(Displayed, pose.Position);
    }

    [Fact]
    public void Paw_interval_cannot_bridge_disjoint_visible_segments()
    {
        var leftSegment = Window(left: 100, right: 185);
        var rightSegment = Window(left: 190, right: 500);

        Assert.False(new EdgePerch().TryBegin(Displayed, Contact,
            [leftSegment, rightSegment], [Monitor], carryReleased: true));
    }

    [Fact]
    public void Complete_paw_interval_may_touch_segment_boundaries()
    {
        Assert.True(new EdgePerch().TryBegin(Displayed, Contact,
            [Window(left: 170, right: 200)], [Monitor], carryReleased: true));
    }

    [Fact]
    public void Floor_is_not_a_perch_candidate()
    {
        Assert.False(new EdgePerch().TryBegin(Displayed, Contact,
            [Window(kind: PlatformKind.Floor)], [Monitor], carryReleased: true));
    }

    [Fact]
    public void Horizontal_taskbar_surface_can_be_a_perch_candidate()
    {
        Assert.True(new EdgePerch().TryBegin(Displayed, Contact,
            [Window(kind: PlatformKind.Taskbar)], [Monitor], carryReleased: true));
    }

    [Theory]
    [InlineData(double.NaN, 50, 70, 10)]
    [InlineData(20, double.PositiveInfinity, 70, 10)]
    [InlineData(51, 50, 70, 10)]
    [InlineData(20, 50, double.NegativeInfinity, 10)]
    [InlineData(20, 50, 9, 10)]
    public void Nonfinite_or_reversed_contact_is_rejected(
        double left, double right, double gripY, double visibleTop)
    {
        Assert.False(new EdgePerch().TryBegin(Displayed, new(left, right, gripY, visibleTop),
            [Window()], [Monitor], carryReleased: true));
    }

    [Theory]
    [InlineData(double.NaN, 140)]
    [InlineData(150, double.PositiveInfinity)]
    public void Nonfinite_displayed_position_is_rejected(double x, double y)
    {
        Assert.False(new EdgePerch().TryBegin(new(x, y), Contact,
            [Window()], [Monitor], carryReleased: true));
    }

    [Fact]
    public void Nonfinite_surface_geometry_is_rejected()
    {
        Assert.False(new EdgePerch().TryBegin(Displayed, Contact,
            [Window(left: double.NaN)], [Monitor], carryReleased: true));
    }

    [Fact]
    public void Head_that_would_extend_above_monitor_top_is_rejected()
    {
        var tooHigh = Window(top: 50, ownerY: 50);
        var exactClearance = Window(top: 60, ownerY: 60);
        var highDisplayed = Displayed with { Y = -10 };

        Assert.False(new EdgePerch().TryBegin(highDisplayed, Contact,
            [tooHigh], [Monitor], carryReleased: true));
        Assert.True(new EdgePerch().TryBegin(highDisplayed, Contact,
            [exactClearance], [Monitor], carryReleased: true));
    }

    [Fact]
    public void Missing_or_nonintersecting_monitor_rejects_entry()
    {
        var absent = new EdgePerch().TryBegin(Displayed, Contact, [Window()], [], carryReleased: true);
        var nonintersecting = Window(left: 900, right: 1200);

        Assert.False(absent);
        Assert.False(new EdgePerch().TryBegin(new(900, 140), Contact,
            [nonintersecting], [Monitor], carryReleased: true));
    }

    [Fact]
    public void Losing_the_owner_monitor_detaches()
    {
        var perch = Begun();

        var pose = perch.Advance(TimeSpan.FromMilliseconds(16), Displayed, Contact,
            [Window()], [], sceneReliable: true);

        Assert.Equal(new EdgePerchPose(EdgePerchPhase.None, Displayed, null, null), pose);
    }

    [Fact]
    public void Unavailable_scene_freezes_pose_and_entry_clock()
    {
        var perch = Begun();
        var before = perch.Current;

        var frozen = perch.Advance(TimeSpan.FromSeconds(10), new(700, 500), Contact,
            [], [], sceneReliable: false);
        Assert.Equal(before, frozen);

        var halfway = perch.Advance(TimeSpan.FromMilliseconds(80), Displayed, Contact,
            [Window()], [Monitor], sceneReliable: true);
        Assert.Equal(EdgePerchPhase.Entering, halfway.Phase);
        Assert.Equal(new PointD(150, 135), halfway.Position);
    }

    [Fact]
    public void Release_preserves_actual_displayed_position_for_direct_regrab()
    {
        var perch = Begun();
        perch.Advance(TimeSpan.FromMilliseconds(80), Displayed, Contact,
            [Window()], [Monitor], sceneReliable: true);
        var actual = new PointD(177, 123);

        perch.Release(actual);

        Assert.Equal(new EdgePerchPose(EdgePerchPhase.None, actual, null, null), perch.Current);
    }

    [Fact]
    public void Release_returns_only_neutral_perch_state_for_motion_to_take_over()
    {
        var perch = Begun();

        perch.Release(Displayed);

        Assert.Equal(EdgePerchPhase.None, perch.Current.Phase);
        Assert.Null(perch.Current.Owner);
        Assert.Null(perch.Current.MonitorId);
    }

    [Fact]
    public void Eighty_milliseconds_is_smoothstep_halfway_and_one_sixty_completes()
    {
        var perch = Begun();

        var halfway = perch.Advance(TimeSpan.FromMilliseconds(80), Displayed, Contact,
            [Window()], [Monitor], sceneReliable: true);
        Assert.Equal(new PointD(150, 135), halfway.Position);
        Assert.Equal(EdgePerchPhase.Entering, halfway.Phase);

        var complete = perch.Advance(TimeSpan.FromMilliseconds(80), halfway.Position, Contact,
            [Window()], [Monitor], sceneReliable: true);
        Assert.Equal(new PointD(150, 130), complete.Position);
        Assert.Equal(EdgePerchPhase.Attached, complete.Phase);
    }

    [Fact]
    public void Negative_delta_does_not_rewind_entry()
    {
        var perch = Begun();
        var halfway = perch.Advance(TimeSpan.FromMilliseconds(80), Displayed, Contact,
            [Window()], [Monitor], sceneReliable: true);

        var pose = perch.Advance(TimeSpan.FromMilliseconds(-40), halfway.Position, Contact,
            [Window()], [Monitor], sceneReliable: true);

        Assert.Equal(halfway, pose);
    }

    [Fact]
    public void Long_delta_completes_entry_without_overshoot()
    {
        var perch = Begun();

        var pose = perch.Advance(TimeSpan.FromHours(1), Displayed, Contact,
            [Window()], [Monitor], sceneReliable: true);

        Assert.Equal(new EdgePerchPose(EdgePerchPhase.Attached, new(150, 130), OwnerKey, 1), pose);
    }

    [Fact]
    public void Candidate_order_is_distance_then_front_z_order_then_stable_key()
    {
        var fartherFront = Window(key: new(9, 9, 9));
        var closerBack = Window(key: new(8, 8, 8), top: 205, ownerY: 205, zOrder: 99);
        var distanceWinner = new EdgePerch();
        Assert.True(distanceWinner.TryBegin(Displayed, Contact,
            [fartherFront, closerBack], [Monitor], carryReleased: true));
        Assert.Equal(closerBack.Surface.Key, distanceWinner.Current.Owner);

        var back = Window(key: new(7, 7, 7), zOrder: 5);
        var front = Window(key: new(6, 6, 6), zOrder: 0);
        var zWinner = new EdgePerch();
        Assert.True(zWinner.TryBegin(Displayed, Contact,
            [back, front], [Monitor], carryReleased: true));
        Assert.Equal(front.Surface.Key, zWinner.Current.Owner);

        PerchSurface[] tied =
        [
            Window(key: new(1, 2, 1)),
            Window(key: new(2, 1, 1)),
            Window(key: new(1, 1, 2)),
            Window(key: new(1, 1, 1))
        ];
        var keyWinner = new EdgePerch();
        Assert.True(keyWinner.TryBegin(Displayed, Contact, tied, [Monitor], carryReleased: true));
        Assert.Equal(new SurfaceKey(1, 1, 1), keyWinner.Current.Owner);
    }

    private static EdgePerch Begun()
    {
        var perch = new EdgePerch();
        Assert.True(perch.TryBegin(Displayed, Contact, [Window()], [Monitor], carryReleased: true));
        return perch;
    }

    private static PerchSurface Window(
        SurfaceKey? key = null,
        PlatformKind kind = PlatformKind.Window,
        double top = 200,
        double left = 100,
        double right = 500,
        double ownerX = 100,
        double ownerY = 200,
        long monitorId = 1,
        int zOrder = 0) =>
        new(new(key ?? OwnerKey, monitorId, kind, left, right, top, new(ownerX, ownerY)), zOrder);
}
