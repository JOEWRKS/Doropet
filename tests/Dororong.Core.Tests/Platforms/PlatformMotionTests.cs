using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.Core.Tests.Platforms;

public sealed class PlatformMotionTests
{
    private static readonly FootContact Contact = new(40, 80, 110, 20);
    private static PlatformSurface Window(double top = 300, double left = 0, double right = 400,
        double ownerX = 0, long handle = 1) => new(new(handle, 10, 1), 1, PlatformKind.Window,
            left, right, top, new(ownerX, top));
    private static PlatformSurface Floor => new(new(0, 0, 1), 1, PlatformKind.Floor, 0, 1000, 800, new(0, 800));
    private static PlatformSurface Bar(double top = 760, long monitor = 1) =>
        new(new(5, 10, 1), monitor, PlatformKind.Taskbar, 0, 1000, top, new(0, top)) { Bottom = 800 };
    private static PlatformPose Step(PlatformMotion motion, PointD displayed, PlatformSurface[] surfaces,
        double ms = 0, PointD? desired = null, bool direct = false, bool reliable = true) =>
        motion.Advance(new(TimeSpan.FromMilliseconds(ms), displayed, desired ?? displayed, Contact,
            surfaces, direct, reliable));
    private static PlatformMotion Supported(PlatformSurface surface, PointD? at = null)
    {
        var motion = new PlatformMotion();
        Step(motion, at ?? new(100, surface.Top - 110), new[] { surface, Floor });
        return motion;
    }

    [Theory]
    [InlineData(20, -30, 120, 160)]
    [InlineData(-20, 0, 80, 190)]
    [InlineData(0, 30, 100, 220)]
    [InlineData(0, -30, 100, 160)]
    public void Owner_translation_preserves_relative_feet_then_walking_ignores_brain_y(
        double dx, double dy, double x, double y)
    {
        var motion = Supported(Window());
        Assert.Equal(PlatformPhase.Supported, motion.Current.Phase);
        var moved = Window(300 + dy, dx, 400 + dx, dx);
        var pose = Step(motion, new(100, 190), new[] { moved, Floor });
        Assert.Equal(new PointD(x, y), pose.Position);
        pose = Step(motion, pose.Position, new[] { moved, Floor }, desired: new(x + 5, y + 80));
        Assert.Equal(new PointD(x + 5, y), pose.Position);
    }

    [Theory]
    [InlineData(150, 400)]
    [InlineData(0, 170)]
    public void Resize_with_contact_keeps_sole_and_does_not_follow_clipped_left(double left, double right)
    {
        var motion = Supported(Window());
        var pose = Step(motion, new(100, 190), new[] { Window(left: left, right: right), Floor });
        Assert.Equal(PlatformPhase.Supported, pose.Phase);
        Assert.Equal(new PointD(100, 190), pose.Position);
    }

    [Theory]
    [InlineData(181, 400)]
    [InlineData(0, 139)]
    public void Resize_removing_overlap_falls_from_displayed_location(double left, double right)
    {
        var motion = Supported(Window());
        var pose = Step(motion, new(100, 190), new[] { Window(left: left, right: right), Floor }, 16);
        Assert.Equal(PlatformPhase.Falling, pose.Phase);
        Assert.Equal(100, pose.Position.X);
        Assert.InRange(pose.Position.Y, 190.1, 191);
    }

    [Fact]
    public void Walking_off_starts_at_last_displayed_location()
    {
        var motion = Supported(Window());
        var pose = Step(motion, new(100, 190), new[] { Window(), Floor }, 16, new(500, -900));
        Assert.Equal(PlatformPhase.Falling, pose.Phase);
        Assert.Equal(new PointD(100, 190.2304), pose.Position);
    }

    [Fact]
    public void Quarter_second_fall_hits_upper_platform_and_pins_feet()
    {
        var motion = new PlatformMotion();
        var pose = Step(motion, new(100, 140), new[] { Window(500, handle: 2), Window(), Floor }, 250);
        Assert.Equal(PlatformPhase.Landing, pose.Phase);
        Assert.Equal(new PointD(100, 190), pose.Position);
        Assert.Equal(Window().Key, pose.Support);
    }

    [Theory]
    [InlineData("close")]
    [InlineData("minimize")]
    [InlineData("occlude")]
    public void Confirmed_absence_releases_support(string reason)
    {
        Assert.NotEmpty(reason); // These source outcomes all map to an absent surface.
        var motion = Supported(Window());
        var pose = Step(motion, new(110, 200), new[] { Floor }, 16);
        Assert.Equal(PlatformPhase.Falling, pose.Phase);
        Assert.InRange(pose.Position.Y, 200.1, 201);
    }

    [Fact]
    public void Unreliable_scene_freezes_relation_then_floor_fallback_releases_it()
    {
        var motion = Supported(Window());
        var pose = Step(motion, new(100, 190), Array.Empty<PlatformSurface>(), 250,
            new(500, 500), reliable: false);
        Assert.Equal(PlatformPhase.Supported, pose.Phase);
        Assert.Equal(Window().Key, pose.Support);
        Assert.Equal(new PointD(100, 190), pose.Position);
        pose = Step(motion, pose.Position, new[] { Floor }, 16);
        Assert.Equal(PlatformPhase.Falling, pose.Phase);
        Assert.InRange(pose.Position.Y, 190.1, 191);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(250)]
    public void Direct_ownership_clears_fall_or_landing_on_first_frame(double fallingMs)
    {
        var motion = new PlatformMotion();
        Step(motion, new(100, 140), new[] { Window(), Floor }, fallingMs);
        var pose = Step(motion, motion.Current.Position, new[] { Window(), Floor }, 250,
            new(700, 90), direct: true, reliable: false);
        Assert.Equal(new PlatformPose(PlatformPhase.Suspended, new(700, 90), null, 0, 0), pose);
        pose = Step(motion, new(300, 140), new[] { Window(), Floor }, 16);
        Assert.Equal(300, pose.Position.X);
        Assert.InRange(pose.Position.Y, 140.1, 141);
    }

    [Fact]
    public void Reset_forgets_old_support_and_velocity()
    {
        var motion = Supported(Window());
        motion.Reset(new(300, 140));
        var pose = Step(motion, new(300, 140), new[] { Floor }, 16);
        Assert.Equal(PlatformPhase.Falling, pose.Phase);
        Assert.InRange(pose.Position.Y, 140.1, 141);
    }

    [Fact]
    public void Landing_compresses_at_contact_then_hops_and_finishes_at_450ms()
    {
        var motion = new PlatformMotion();
        Step(motion, new(100, 190), new[] { Window(), Floor }, 16); // exact contact is supported
        Step(motion, new(100, 140), new[] { Floor }, 0); // remove old support
        var pose = Step(motion, new(100, 140), new[] { Window(), Floor }, 250);
        pose = Step(motion, pose.Position, new[] { Window(), Floor }, 80);
        Assert.Equal(PlatformPhase.Landing, pose.Phase);
        Assert.Equal(.05,pose.Squash,6);
        Assert.Equal(190, pose.Position.Y);
        pose = Step(motion, pose.Position, new[] { Window(), Floor }, 120);
        Assert.InRange(pose.Squash, -.03, -.001);
        Assert.Equal(187, pose.Position.Y,6);
        pose = Step(motion, pose.Position, new[] { Window(), Floor }, 250);
        Assert.Equal(PlatformPhase.Supported, pose.Phase);
        Assert.Equal(0, pose.Squash);
    }

    [Fact]
    public void Large_delta_is_capped_without_backlog_and_speed_is_bounded()
    {
        var motion = new PlatformMotion();
        var pose = Step(motion, new(100, 0), Array.Empty<PlatformSurface>(), 10000);
        Assert.Equal(56.25, pose.Position.Y, 6);
        pose = Step(motion, pose.Position, Array.Empty<PlatformSurface>(), 0);
        Assert.Equal(56.25, pose.Position.Y, 6);
        for (var i = 0; i < 5; i++) pose = Step(motion, pose.Position, Array.Empty<PlatformSurface>(), 250);
        var previous = pose.Position.Y;
        pose = Step(motion, pose.Position, Array.Empty<PlatformSurface>(), 250);
        Assert.Equal(300, pose.Position.Y - previous, 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Nonpositive_delta_does_not_integrate(double ms)
    {
        var pose = Step(new PlatformMotion(), new(100, 140), new[] { Floor }, ms);
        Assert.Equal(PlatformPhase.Falling, pose.Phase);
        Assert.Equal(new PointD(100, 140), pose.Position);
    }

    [Fact]
    public void Recovery_of_old_handle_above_actual_feet_does_not_teleport()
    {
        var motion = Supported(Window());
        var pose = Step(motion, new(100, 190), new[] { Floor }, 250);
        pose = Step(motion, pose.Position, new[] { Window(), Floor }, 16);
        Assert.Equal(PlatformPhase.Falling, pose.Phase);
        Assert.True(pose.Position.Y > 246);
        Assert.Null(pose.Support);
    }

    [Fact]
    public void Reappearing_taskbar_lifts_from_actual_height_tracks_destination_and_finishes()
    {
        var motion = Supported(Floor);
        var pose = Step(motion, new(100, 690), new[] { Floor, Bar() }, 80);
        Assert.Equal(PlatformPhase.Lifting, pose.Phase);
        Assert.Equal(670, pose.Position.Y, 6);
        pose = Step(motion, pose.Position, new[] { Floor, Bar(750) }, 80);
        Assert.Equal(PlatformPhase.Supported, pose.Phase);
        Assert.Equal(new PointD(100, 640), pose.Position);
        Assert.Equal(Bar().Key, pose.Support);
    }

    [Fact]
    public void Taskbar_disappearing_mid_lift_falls_from_interpolated_position_then_can_reappear()
    {
        var motion = Supported(Floor);
        var pose = Step(motion, new(100, 690), new[] { Floor, Bar() }, 80);
        pose = Step(motion, pose.Position, new[] { Floor }, 16);
        Assert.Equal(PlatformPhase.Falling, pose.Phase);
        Assert.InRange(pose.Position.Y, 670.1, 671);
        pose = Step(motion, pose.Position, new[] { Floor, Bar() }, 80);
        Assert.Equal(PlatformPhase.Lifting, pose.Phase);
        Assert.InRange(pose.Position.Y, 660, 661);
    }

    [Fact]
    public void Hiding_supported_taskbar_lands_on_monitor_floor()
    {
        var motion = Supported(Bar());
        var pose = Step(motion, new(100, 650), new[] { Floor }, 250);
        Assert.Equal(PlatformPhase.Landing, pose.Phase);
        Assert.Equal(690, pose.Position.Y);
        Assert.Equal(Floor.Key, pose.Support);
    }

    [Theory]
    [InlineData(2, 800d)]
    [InlineData(1, 700d)]
    [InlineData(1, null)]
    public void Other_monitor_floating_or_unknown_bottom_taskbar_cannot_lift(long monitor, double? bottom)
    {
        var motion = Supported(Floor);
        var pose = Step(motion, new(100, 690), new[] { Floor, Bar(650, monitor) with { Bottom = bottom } }, 80);
        Assert.Equal(PlatformPhase.Supported, pose.Phase);
        Assert.Equal(690, pose.Position.Y);
        Assert.Equal(Floor.Key, pose.Support);
    }

    [Fact]
    public void Reappearing_taskbar_never_pulls_down_from_higher_window()
    {
        var motion = Supported(Window());
        var pose = Step(motion, new(100, 190), new[] { Window(), Floor, Bar() }, 160);
        Assert.Equal(PlatformPhase.Supported, pose.Phase);
        Assert.Equal(190, pose.Position.Y);
        Assert.Equal(Window().Key, pose.Support);
    }

    [Fact]
    public void Existing_taskbar_does_not_lift_pet_that_walks_into_it_from_below()
    {
        var motion = new PlatformMotion();
        Step(motion, new(100, 690), new[] { Floor, Bar() }, direct: true);
        var pose = Step(motion, new(100, 690), new[] { Floor, Bar() }, 80);
        Assert.Equal(PlatformPhase.Supported, pose.Phase);
        Assert.Equal(Floor.Key, pose.Support);
    }

    [Fact]
    public void Contact_on_taskbar_top_is_supported_without_empty_lift()
    {
        var motion = Supported(Bar());
        Assert.Equal(PlatformPhase.Supported, motion.Current.Phase);
    }

    [Fact]
    public void Lift_begins_at_last_actual_y_even_if_floor_pose_was_adjusted_by_host()
    {
        var motion = Supported(Floor);
        var pose = Step(motion, new(100, 680), new[] { Floor, Bar() }, 80);
        Assert.Equal(665, pose.Position.Y, 6);
    }

    [Fact]
    public void Late_landing_rebound_never_exceeds_three_percent()
    {
        var motion = new PlatformMotion();
        var pose = Step(motion, new(100, 140), new[] { Window() }, 250);
        pose = Step(motion, pose.Position, new[] { Window() }, 192);
        Assert.InRange(pose.Squash, -.03, 0);
    }

    [Fact]
    public void Moving_support_produces_small_decaying_upper_body_sway_without_sole_drift()
    {
        var motion = Supported(Window());
        var moved = Window(left: 20, right: 420, ownerX: 20);
        var pose = Step(motion, new(100, 190), new[] { moved, Floor }, 16);
        Assert.InRange(pose.Sway, .001, .03);
        Assert.Equal(190, pose.Position.Y);
        var initialSway = pose.Sway;
        pose = Step(motion, pose.Position, new[] { moved, Floor }, 250);
        Assert.InRange(pose.Sway, 0, initialSway / 2);
    }
}
