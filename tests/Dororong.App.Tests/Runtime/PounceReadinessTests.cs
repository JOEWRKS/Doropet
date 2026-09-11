using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Tests.Controls;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public sealed class PounceReadinessTests
{
    // Exercises the pounce-enabled presenter/loop sleep path, not host construction.
    [Fact]
    public void Nearby_pointer_wakes_sleep_then_completes_a_real_pounce() => CheekProductTests.Sta(() =>
    {
        using var h = new PounceLoopTests.Harness(BehaviorTuning.Default with
        {
            SleepDelay = TimeSpan.FromSeconds(1),
            IdleMin = TimeSpan.FromMinutes(10), IdleMax = TimeSpan.FromMinutes(10)
        });
        h.Start();
        Assert.Equal(PetState.Sleep, h.Snapshot.State);
        h.Pointer = new(true, h.Position + new PointD(126, 81));
        h.Tick();
        Assert.Equal(PetState.Idle, h.Snapshot.State);
        h.Ticks(14);
        Assert.Equal(PouncePhase.Flight, h.Presenter.Pounce.Phase);
        h.Tick(.17);
        Assert.Equal(548, h.WorldSole, 4);
        // Observe after landing; double-to-TimeSpan subdivisions may lose100ns.
        // Exact phase boundaries are separately covered by PounceSessionTests.
        h.Tick(.46);
        Assert.Equal(PouncePhase.Track, h.Presenter.Pounce.Phase);
        Assert.Equal(560, h.WorldSole, 4);
        h.Pointer = PointerSample.Unavailable;
        h.Ticks(55);
        Assert.Equal(PetState.Sleep, h.Snapshot.State);
        Assert.Equal(PouncePhase.Watch, h.Presenter.Pounce.Phase);
    });

    // Catches stale capture/support ownership after losing a midflight head grab.
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Canceling_a_midflight_head_grab_releases_capture_and_lands_without_snap(bool moveBeforeCancel) => CheekProductTests.Sta(() =>
    {
        using var h = new PounceLoopTests.Harness(); h.Start();
        h.Pointer = new(true, h.Position + new PointD(126, 81)); h.Ticks(16);
        var image = (AlphaHitTestImage)h.Presenter.FindName("DororongImage");
        var press = Assert.IsType<DirectInteractionPressEventArgs>(h.Presenter.CreateDirectPress(image, new(40, 40)));
        Assert.Equal(DirectInteractionTarget.Body, press.Target);
        h.Down = true; h.Pointer = new(true, h.Position + press.WindowLocalPosition);
        h.Loop.NotifyDirectInteractionPressed(press); h.Tick(.016);
        if (moveBeforeCancel)
        {
            h.Pointer = new(true, h.Pointer.Position + new PointD(25, -20)); h.Tick(.016);
            Assert.Equal(PetState.Dragged, h.Snapshot.State);
        }
        var before = h.Position;
        // Pending head presses do not capture until the drag threshold is crossed.
        Assert.Equal(moveBeforeCancel ? 1 : 0, h.Captures);
        Assert.Equal(0, h.Releases);
        h.Loop.NotifyDirectInteractionCanceled();
        Assert.Equal(before, h.Position);
        Assert.Equal(moveBeforeCancel ? 1 : 0, h.Releases);
        Assert.Equal(DirectInteractionTarget.None, h.Direct.Target);
        Assert.False(h.Snapshot.IsDirectInteractionPending);
        Assert.NotEqual(PetState.Dragged, h.Snapshot.State);
        // Button stays held: normal button release cannot repair a broken cancel.
        h.Tick(.016);
        Assert.Equal(DirectInteractionTarget.None, h.Direct.Target);
        Assert.False(h.Snapshot.IsDirectInteractionPending);
        Assert.NotEqual(PetState.Dragged, h.Snapshot.State);
        Assert.Equal(moveBeforeCancel ? 1 : 0, h.Captures);
        Assert.Equal(h.Captures, h.Releases);
        Assert.Equal(PouncePhase.Watch, h.Presenter.Pounce.Phase);
        Assert.InRange(h.Position.Y - before.Y, 0, .5);
        h.Down = false; h.Pointer = PointerSample.Unavailable;
        h.Ticks(30);
        Assert.Equal(PlatformPhase.Supported, h.PlatformPose.Phase);
        Assert.Equal(560, h.WorldSole, 4);
    });

    // Catches accumulated landing drift, stale dwell, and duplicate position writers.
    [Fact]
    public void Repeated_alternating_pounces_keep_the_same_support_and_full_tracking_delay() => CheekProductTests.Sta(() =>
    {
        using var h = new PounceLoopTests.Harness(); h.Start();
        var origin = h.Position;
        var support = h.PlatformPose.Support;
        Assert.NotNull(support);
        foreach (var direction in new[] { 1, -1, 1, -1, 1, -1 })
        {
            var takeoff = h.Position;
            h.Pointer = new(true, takeoff + new PointD(72 + direction * 54, 81));
            h.Ticks(14); Assert.Equal(PouncePhase.Watch, h.Presenter.Pounce.Phase);
            h.Tick(); Assert.Equal(PouncePhase.Flight, h.Presenter.Pounce.Phase);
            h.Tick(.62);
            Assert.Equal(PouncePhase.Track, h.Presenter.Pounce.Phase);
            Assert.Equal(takeoff.X + direction * 28, h.Position.X, 4);
            Assert.Equal(560, h.WorldSole, 4);
            Assert.Equal(support, h.PlatformPose.Support);
            var landed = h.Position;
            for (var tick = 0; tick < 29; tick++)
            {
                h.Tick();
                Assert.Equal(landed, h.Position);
                Assert.Equal(PouncePhase.Track, h.Presenter.Pounce.Phase);
                Assert.Equal(support, h.PlatformPose.Support);
                Assert.Equal(1, h.WritesThisTick);
            }
            h.Tick(); Assert.Equal(PouncePhase.Watch, h.Presenter.Pounce.Phase);
        }
        Assert.Equal(origin.X, h.Position.X, 4);
        Assert.Equal(560, h.WorldSole, 4);
        Assert.Equal(1, h.MaxWritesPerTick);
    });
}
