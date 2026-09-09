using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public sealed class EdgePerchRuntimeTests
{
    private static readonly PointD Displayed = new(100, 100);
    private static readonly DesktopMonitor Monitor = new(1, new(0, 0, 800, 600));
    private static readonly PerchSurface Surface = new(
        new(new(1, 2, 3), 1, PlatformKind.Window, 0, 800, 188, new(0, 188)), 0);

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Entry_requires_whole_carry_release_pointer_and_reliable_scene(
        bool carry, bool pointer, bool scene)
    {
        var runtime = new EdgePerchRuntime(_ => new(47, 77, 94, 48));

        Assert.False(runtime.TryBegin(Displayed, FacingDirection.Right, [Surface], [Monitor],
            carry, pointer, scene));
        Assert.Equal(EdgePerchPhase.None, runtime.Current.Phase);
    }

    [Fact]
    public void Measured_image09_contact_enters_and_owner_motion_is_applied_once()
    {
        var measurements = 0;
        var runtime = new EdgePerchRuntime(facing =>
        {
            measurements++;
            Assert.Equal(FacingDirection.Left, facing);
            return new(61, 91, 94, 48);
        });

        Assert.True(runtime.TryBegin(Displayed, FacingDirection.Left, [Surface], [Monitor],
            carryReleased: true, pointerReliable: true, sceneReliable: true));
        var moved = Surface with { Surface = Surface.Surface with { Left = 20, Right = 820, Top = 180, OwnerOrigin = new(20, 180) } };
        var pose = runtime.Advance(TimeSpan.FromMilliseconds(160), Displayed, [moved], [Monitor], sceneReliable: true);

        Assert.Equal(1, measurements);
        Assert.Equal(FacingDirection.Left, runtime.Facing);
        Assert.Equal(EdgePerchPhase.Attached, pose.Phase);
        Assert.Equal(new PointD(120, 86), pose.Position);
    }

    [Fact]
    public void Temporarily_unavailable_scene_freezes_but_explicit_reliable_loss_detaches()
    {
        var runtime = new EdgePerchRuntime(_ => new(47, 77, 94, 48));
        Assert.True(runtime.TryBegin(Displayed, FacingDirection.Right, [Surface], [Monitor], true, true, true));

        var frozen = runtime.Advance(TimeSpan.FromSeconds(2), new(500, 500), [], [], sceneReliable: false);
        Assert.Equal(new EdgePerchPose(EdgePerchPhase.Entering, Displayed, Surface.Surface.Key, 1), frozen);

        var detached = runtime.Advance(TimeSpan.FromMilliseconds(16), Displayed, [], [Monitor], sceneReliable: true);
        Assert.Equal(new EdgePerchPose(EdgePerchPhase.None, Displayed, null, null), detached);
    }
}
