using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.Core.Tests.Platforms;

public sealed class TaskbarRecoveryTests
{
    private static readonly PointD Displayed = new(100, 970);
    private static readonly FootContact Contact = new(40, 65, 110, 30);
    private static readonly PlatformSurface Bar = Taskbar(top: 1040, bottom: 1080);

    [Fact]
    public void Visible_taskbar_penetrated_by_world_foot_is_selected()
    {
        var selected = TaskbarRecovery.FindPenetration(
            Displayed, Contact, [Bar], directOwnsPosition: false, explicitlyPerched: false);

        Assert.Equal(Bar, selected);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Existing_position_owner_suppresses_recovery(bool directOwnsPosition, bool explicitlyPerched)
    {
        var selected = TaskbarRecovery.FindPenetration(
            Displayed, Contact, [Bar], directOwnsPosition, explicitlyPerched);

        Assert.Null(selected);
    }

    [Fact]
    public void Sole_exactly_on_taskbar_top_is_normal_contact()
    {
        var contact = Contact with { SoleY = 70 };

        var selected = TaskbarRecovery.FindPenetration(
            Displayed, contact, [Bar], directOwnsPosition: false, explicitlyPerched: false);

        Assert.Null(selected);
    }

    [Fact]
    public void Existing_taskbar_floor_persistence_is_corrected_without_squash_or_bounce()
    {
        var floor = new PlatformSurface(new(0, 0, 1), 1, PlatformKind.Floor,
            0, 1920, 1080, new(0, 1080));
        PlatformSurface[] surfaces = [floor, Bar];
        var motion = new PlatformMotion();

        var direct = motion.Advance(new(TimeSpan.Zero, Displayed, Displayed, Contact,
            surfaces, DirectOwnsPosition: true, SceneReliable: true));
        var persisted = motion.Advance(new(TimeSpan.FromMilliseconds(16), direct.Position, direct.Position,
            Contact, surfaces, DirectOwnsPosition: false, SceneReliable: true));

        Assert.Equal(PlatformPhase.Supported, persisted.Phase);
        Assert.Equal(floor.Key, persisted.Support);
        Assert.Equal(1080, persisted.Position.Y + Contact.SoleY);

        var penetration = TaskbarRecovery.FindPenetration(
            persisted.Position, Contact, surfaces, directOwnsPosition: false, explicitlyPerched: false);
        Assert.Equal(Bar, penetration);

        var corrected = new PointD(persisted.Position.X, penetration!.Value.Top - Contact.SoleY);
        motion.Reset(corrected);
        var recovered = motion.Advance(new(TimeSpan.Zero, corrected, corrected, Contact,
            surfaces, DirectOwnsPosition: false, SceneReliable: true));

        Assert.Equal(PlatformPhase.Supported, recovered.Phase);
        Assert.Equal(Bar.Key, recovered.Support);
        Assert.Equal(corrected, recovered.Position);
        Assert.Equal(0, recovered.Squash);

        for (var frame = 0; frame < 4; frame++)
        {
            recovered = motion.Advance(new(TimeSpan.FromMilliseconds(120), recovered.Position, recovered.Position,
                Contact, surfaces, DirectOwnsPosition: false, SceneReliable: true));
            Assert.Equal(PlatformPhase.Supported, recovered.Phase);
            Assert.Equal(corrected, recovered.Position);
            Assert.Equal(0, recovered.Squash);
        }
    }

    [Fact]
    public void Already_supported_above_taskbar_needs_no_recovery()
    {
        var displayed = Displayed with { Y = Bar.Top - Contact.SoleY };
        var motion = new PlatformMotion();
        var pose = motion.Advance(new(TimeSpan.Zero, displayed, displayed, Contact,
            [Bar], DirectOwnsPosition: false, SceneReliable: true));

        Assert.Equal(PlatformPhase.Supported, pose.Phase);
        Assert.Equal(Bar.Key, pose.Support);
        Assert.Null(TaskbarRecovery.FindPenetration(
            pose.Position, Contact, [Bar], directOwnsPosition: false, explicitlyPerched: false));
    }

    [Fact]
    public void Taskbar_on_other_horizontal_span_is_not_selected()
    {
        var otherSpan = Taskbar(top: 1040, bottom: 1080, left: 500, right: 900, monitorId: 2);

        Assert.Null(TaskbarRecovery.FindPenetration(
            Displayed, Contact, [otherSpan], directOwnsPosition: false, explicitlyPerched: false));
    }

    [Fact]
    public void Absent_hidden_taskbar_is_not_invented()
    {
        Assert.Null(TaskbarRecovery.FindPenetration(
            Displayed, Contact, [], directOwnsPosition: false, explicitlyPerched: false));
    }

    [Theory]
    [InlineData(1079.999, true)]
    [InlineData(1080, true)]
    [InlineData(1080.001, true)]
    [InlineData(1080.002, false)]
    public void Taskbar_bottom_allows_one_thousandth_dip_tolerance(double worldSole, bool expected)
    {
        var displayed = new PointD(Displayed.X, 0);
        var contact = Contact with { SoleY = worldSole };

        var selected = TaskbarRecovery.FindPenetration(
            displayed, contact, [Bar], directOwnsPosition: false, explicitlyPerched: false);

        Assert.Equal(expected, selected is not null);
    }

    [Theory]
    [InlineData(1040.001, false)]
    [InlineData(1040.002, true)]
    public void Penetration_must_exceed_top_tolerance(double worldSole, bool expected)
    {
        var displayed = new PointD(Displayed.X, 0);
        var contact = Contact with { SoleY = worldSole };

        var selected = TaskbarRecovery.FindPenetration(
            displayed, contact, [Bar], directOwnsPosition: false, explicitlyPerched: false);

        Assert.Equal(expected, selected is not null);
    }

    [Theory]
    [InlineData(163, true)]
    [InlineData(163.001, false)]
    public void Foot_requires_two_dip_horizontal_overlap(double taskbarLeft, bool expected)
    {
        var bar = Taskbar(top: 1040, bottom: 1080, left: taskbarLeft);

        var selected = TaskbarRecovery.FindPenetration(
            Displayed, Contact, [bar], directOwnsPosition: false, explicitlyPerched: false);

        Assert.Equal(expected, selected is not null);
    }

    [Fact]
    public void Highest_penetrating_top_wins_then_stable_key_breaks_ties()
    {
        var lower = Taskbar(top: 1040, bottom: 1080, key: new(1, 1, 1));
        var higherLaterKey = Taskbar(top: 1030, bottom: 1080, key: new(9, 1, 1));
        var higherStableKey = Taskbar(top: 1030, bottom: 1080, key: new(2, 3, 4));

        var selected = TaskbarRecovery.FindPenetration(
            Displayed, Contact, [lower, higherLaterKey, higherStableKey],
            directOwnsPosition: false, explicitlyPerched: false);

        Assert.Equal(higherStableKey, selected);
    }

    [Fact]
    public void Non_taskbars_missing_bottom_and_invalid_ranges_are_ignored()
    {
        PlatformSurface[] invalid =
        [
            Bar with { Kind = PlatformKind.Window },
            Bar with { Kind = PlatformKind.Floor },
            Bar with { Bottom = null },
            Bar with { Right = Bar.Left },
            Bar with { Bottom = Bar.Top },
            Bar with { Bottom = Bar.Top - 1 }
        ];

        Assert.Null(TaskbarRecovery.FindPenetration(
            Displayed, Contact, invalid, directOwnsPosition: false, explicitlyPerched: false));
    }

    [Fact]
    public void Nonfinite_input_or_surface_geometry_is_ignored()
    {
        Assert.Null(TaskbarRecovery.FindPenetration(
            Displayed with { X = double.NaN }, Contact, [Bar], false, false));
        Assert.Null(TaskbarRecovery.FindPenetration(
            Displayed, Contact with { SoleY = double.PositiveInfinity }, [Bar], false, false));
        Assert.Null(TaskbarRecovery.FindPenetration(
            Displayed, Contact with { Left = 66 }, [Bar], false, false));
        Assert.Null(TaskbarRecovery.FindPenetration(
            Displayed, Contact, [Bar with { Top = double.NaN }], false, false));
        Assert.Null(TaskbarRecovery.FindPenetration(
            Displayed, Contact, [Bar with { Bottom = double.PositiveInfinity }], false, false));
        Assert.Null(TaskbarRecovery.FindPenetration(
            Displayed, Contact, [Bar with { OwnerOrigin = new(double.NaN, Bar.Top) }], false, false));
    }

    private static PlatformSurface Taskbar(
        double top,
        double? bottom,
        double left = 0,
        double right = 1920,
        SurfaceKey? key = null,
        long monitorId = 1) =>
        new(key ?? new(5, 10, 1), monitorId, PlatformKind.Taskbar,
            left, right, top, new(left, top)) { Bottom = bottom };
}
