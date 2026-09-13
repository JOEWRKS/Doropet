using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Runtime;

public sealed partial class PetLoopDirectInteractionTests
{
    [Fact]
    public void Click_only_body_press_stays_fixed_captures_release_and_never_becomes_head_drag()
    {
        using var h = new LoopHarness(CreateIdleBrain);
        h.Start(); h.Press(DirectInteractionTarget.ClickOnly, 0); h.Tick();
        Assert.True(h.Renders[^1].Core.IsDirectInteractionPending);
        Assert.Equal(1, h.CaptureCount);
        var origin = h.WindowPosition;
        h.MovePointerBy(new(80, -50)); h.Tick();
        Assert.Equal(origin, h.WindowPosition);
        Assert.Equal(DirectInteractionTarget.ClickOnly, h.Renders[^1].Direct.Target);
        Assert.Null(h.Renders[^1].Direct.BodyPull);
        Assert.NotEqual(PetState.Dragged, h.Renders[^1].Core.State);
        h.Release();
        Assert.Equal(DirectInteractionTarget.None, h.Renders[^1].Direct.Target);
        Assert.Equal(1, h.ReleaseCount);
        Assert.NotEqual(PetState.ClickReaction, h.Renders[^1].Core.State);
        h.Press(DirectInteractionTarget.ClickOnly, 0); h.Release();
        Assert.Equal(PetState.ClickReaction, h.Renders[^1].Core.State);
        Assert.Equal(0, h.FaultCount);
    }

    [Fact]
    public void Captured_cheek_preserves_position_and_release_without_click() => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new LoopHarness(CreateIdleBrain);h.Start();var position=h.WindowPosition;
        h.PressCapturedCheek();h.Tick();Assert.NotNull(h.Renders[^1].Direct.CheekPull);
        h.MovePointerBy(new(-12,0));h.Tick();Assert.Equal(12,h.Renders[^1].Direct.CheekPull!.PullDips);
        for(var i=0;i<60;i++)h.Tick();Assert.Equal(position,h.WindowPosition);Assert.Equal(PetState.Idle,h.Renders[^1].Core.State);
        h.LosePointer();h.Tick();Assert.Equal(12,h.Renders[^1].Direct.CheekPull!.PullDips);
        h.Release();Assert.Equal(12,h.Renders[^1].Direct.CheekPull!.PullDips);Assert.False(h.Renders[^1].Direct.RequiresCapture);
        for(var i=0;i<46;i++)h.Tick();Assert.Equal(DirectInteractionSnapshot.None,h.Renders[^1].Direct);
        Assert.Equal(position+new PointD(2,0),h.WindowPosition);Assert.DoesNotContain(h.Renders,r=>r.Core.State is PetState.Dragged or PetState.ClickReaction);
        Assert.Equal(1,h.CaptureCount);Assert.Equal(1,h.ReleaseCount);
    });

    [Theory]
    [InlineData("cancel")]
    [InlineData("dispose")]
    [InlineData("fault")]
    public void Captured_cheek_cleanup_clears_the_display_and_capture_exactly_once(string action) => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new LoopHarness(CreateIdleBrain){NotifyLostCaptureOnRelease=true};h.Start();h.PressCapturedCheek();h.Tick();
        h.MovePointerBy(new(-20,0));h.Tick();Assert.NotNull(h.Renders[^1].Direct.CheekPull);
        if(action=="cancel")h.Cancel();else if(action=="dispose")h.Dispose();else h.FaultOnNextTick();
        Assert.Equal(DirectInteractionSnapshot.None,h.Renders[^1].Direct);Assert.Equal(1,h.ReleaseCount);
        h.Dispose();Assert.Equal(1,h.ReleaseCount);
    });

    [Fact]
    public void Captured_cheek_quick_release_and_failed_capture_leave_no_stale_overlay() => Controls.CheekProductTests.Sta(() =>
    {
        using(var h=new LoopHarness(CreateIdleBrain)){h.Start();h.PressCapturedCheek();h.Release();Assert.Equal(0,h.Renders[^1].Direct.CheekPull!.PullDips);Assert.Equal(0,h.CaptureCount);h.Cancel();Assert.Equal(DirectInteractionSnapshot.None,h.Renders[^1].Direct);}
        using(var h=new LoopHarness(CreateIdleBrain){CaptureSucceeds=false}){h.Start();h.PressCapturedCheek();h.Tick();Assert.Equal(1,h.FaultCount);Assert.Equal(DirectInteractionSnapshot.None,h.Renders[^1].Direct);}
    });

    [Fact]
    public void Head_window_follows_each_pointer_sample_before_and_across_full_extension()
    {
        using var h = new LoopHarness(CreateIdleBrain);
        h.Start(); h.Press(DirectInteractionTarget.Body, 0); h.Tick();
        foreach (var (pointerX, windowX) in new[] { (165, 105), (200, 140), (202, 142), (239, 179), (240, 180), (241, 181), (260, 200) })
        {
            h.MovePointerTo(new(pointerX, 150)); h.Tick();
            Assert.Equal(new PointD(windowX, 100), h.WindowPosition);
            Assert.Equal(new PointD(60, 50), h.Renders[^1].Core.GrabOffset);
            Assert.Equal(h.WindowPosition, h.Renders[^1].Core.Position);
            Assert.Equal(pointerX >= 240 ? DirectInteractionPhase.BodyDragHold : DirectInteractionPhase.BodyDragEntry, h.Renders[^1].Direct.Phase);
        }
        h.MovePointerTo(new(185, 140)); h.Tick();
        Assert.Equal(new PointD(125, 90), h.WindowPosition);
        Assert.Equal(1, h.CaptureCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Head_cancel_after_partial_or_clamped_carry_releases_ownership_and_regrab_is_fresh(bool carry)
    {
        using var h = new LoopHarness(CreateIdleBrain);
        h.Start(); h.Press(DirectInteractionTarget.Body, 0); h.Tick();
        h.MovePointerTo(carry ? new(1500, -500) : new(182, 150)); h.Tick();
        var last = h.WindowPosition;
        if (carry) Assert.Equal(new(680, 0), last);
        h.Cancel(); h.MovePointerTo(new(300, 300)); h.Tick();
        Assert.Equal(last, h.WindowPosition);
        Assert.Null(h.Renders[^1].Core.GrabOffset);
        Assert.False(h.Renders[^1].Core.IsDirectInteractionPending);
        h.Press(DirectInteractionTarget.Body, 0); h.Tick();
        Assert.Equal(DirectInteractionPhase.BodyPending, h.Renders[^1].Direct.Phase);
        Assert.Equal(0, h.Renders[^1].Direct.Strength);
        Assert.Equal(1, h.ReleaseCount);
    }

    [Fact]
    public void Head_wakes_sleep_and_capture_failure_or_fault_clears_presentation()
    {
        foreach (var captureFails in new[] { true, false })
        {
            using var h = new LoopHarness(CreateSleepingBrain) { CaptureSucceeds = !captureFails };
            h.Start(); h.Press(DirectInteractionTarget.Body, 0); h.Tick();
            Assert.Equal(PetState.Idle, h.Renders[^1].Core.State);
            h.MovePointerBy(new(22, 0)); h.Tick();
            if (!captureFails) h.FaultOnNextTick();
            Assert.Equal(1, h.FaultCount);
            Assert.Equal(DirectInteractionTarget.None, h.Renders[^1].Direct.Target);
            Assert.NotEqual(PetState.Dragged, h.Renders[^1].Core.State);
            Assert.Null(h.Renders[^1].Core.GrabOffset);
        }
    }

    [Fact]
    public void Head_deadzone_click_and_unavailable_release_keep_existing_click_contract()
    {
        foreach (var available in new[] { true, false })
        {
            using var h = new LoopHarness(CreateIdleBrain);
            h.Start(); h.Press(DirectInteractionTarget.Body, 0); h.Tick();
            h.MovePointerBy(new(3.9, 3.9)); h.Tick();
            Assert.Equal(DirectInteractionPhase.BodyPending, h.Renders[^1].Direct.Phase);
            Assert.Equal(0, h.CaptureCount);
            if (!available) h.LosePointer();
            h.Release();
            Assert.Equal(available ? PetState.ClickReaction : PetState.Idle, h.Renders[^1].Core.State);
            Assert.Equal(new(100, 100), h.WindowPosition);
        }
    }

    [Fact]
    public void Head_partial_hold_is_distance_driven_stationary_and_reversible()
    {
        using var h = new LoopHarness(CreateIdleBrain);
        h.Start(); var origin = h.WindowPosition;
        h.Press(DirectInteractionTarget.Body, 0); h.Tick();
        h.MovePointerBy(new(42, 0)); h.Tick();
        Assert.Equal(0.5, h.Renders[^1].Direct.Strength, 8);
        for (var i = 0; i < 64; i++) h.Tick();
        Assert.Equal(0.5, h.Renders[^1].Direct.Strength, 8);
        Assert.Equal(DirectInteractionPhase.BodyDragEntry, h.Renders[^1].Direct.Phase);
        Assert.Equal(origin + new PointD(42, 0), h.WindowPosition);
        h.MovePointerBy(new(19, 0)); h.Tick();
        Assert.Equal(0.75, h.Renders[^1].Direct.Strength, 8);
        h.MovePointerBy(new(-38, 0)); h.Tick();
        Assert.Equal(0.25, h.Renders[^1].Direct.Strength, 8);
        h.MovePointerBy(new(-23, 0)); h.Tick();
        Assert.Equal(0, h.Renders[^1].Direct.Strength);
        Assert.Equal(origin, h.WindowPosition);
        Assert.Equal(1, h.CaptureCount);
    }

    [Theory]
    [InlineData(80, 0, 80, 0)]
    [InlineData(100, 0, 100, 0)]
    [InlineData(0, -80, 0, -80)]
    [InlineData(48, 64, 48, 64)]
    public void Head_full_extension_keeps_original_grab_offset_in_all_directions(double x, double y, double dx, double dy)
    {
        using var h = new LoopHarness(CreateIdleBrain);
        h.Start(); var origin = h.WindowPosition;
        h.Press(DirectInteractionTarget.Body, 0); h.Tick();
        h.MovePointerBy(new(x, y)); h.Tick();
        Assert.Equal(origin.X + dx, h.WindowPosition.X, 8);
        Assert.Equal(origin.Y + dy, h.WindowPosition.Y, 8);
        Assert.Equal(DirectInteractionPhase.BodyDragHold, h.Renders[^1].Direct.Phase);
        h.MovePointerBy(new(5, 3)); h.Tick();
        Assert.Equal(origin.X + dx + 5, h.WindowPosition.X, 8);
        Assert.Equal(origin.Y + dy + 3, h.WindowPosition.Y, 8);
        var carried = h.WindowPosition;
        h.Release(); for (var i = 0; i < 28; i++) h.Tick();
        Assert.Equal(carried + new PointD(0, 36), h.WindowPosition);
        Assert.Equal(h.WindowPosition, h.Renders[^1].Core.Position);
        Assert.Equal(DirectInteractionTarget.None, h.Renders[^1].Direct.Target);
    }

    [Fact]
    public void Head_partial_release_keeps_displayed_strength_and_settles_without_full_hang()
    {
        using var h = new LoopHarness(CreateIdleBrain) { NotifyLostCaptureOnRelease = true };
        h.Start(); var origin = h.WindowPosition;
        h.Press(DirectInteractionTarget.Body, 0); h.Tick();
        h.MovePointerBy(new(42, 0)); h.Tick();
        h.MovePointerBy(new(100, 0)); // A release sample must not replace the last displayed stretch.
        h.Release();
        Assert.Equal(DirectInteractionPhase.BodyDragSettle, h.Renders[^1].Direct.Phase);
        Assert.Equal(0.5, h.Renders[^1].Direct.Strength, 8);
        var previous = 0.5;
        for (var i = 0; i < 11; i++)
        {
            h.Tick();
            Assert.InRange(h.Renders[^1].Direct.Strength, 0, previous);
            previous = h.Renders[^1].Direct.Strength;
        }
        Assert.Equal(DirectInteractionPhase.BodyDragSettle, h.Renders[^1].Direct.Phase);
        h.Tick();
        Assert.Equal(DirectInteractionPhase.BodyDragSettle, h.Renders[^1].Direct.Phase);
        Assert.Equal(0, h.Renders[^1].Direct.Strength);
        for (var i = 0; i < 16; i++) h.Tick();
        Assert.Equal(DirectInteractionTarget.None, h.Renders[^1].Direct.Target);
        Assert.Equal(origin + new PointD(42, 18), h.WindowPosition);
        Assert.Equal(1, h.ReleaseCount);
    }

    [Fact]
    public void Head_invalid_or_unavailable_pointer_preserves_last_finite_state_and_cancel_stops_following()
    {
        using var h = new LoopHarness(CreateIdleBrain);
        h.Start(); h.Press(DirectInteractionTarget.Body, 0); h.Tick();
        h.MovePointerBy(new(42, 0)); h.Tick();
        foreach (var p in new[] { new PointD(double.NaN, 10), new PointD(double.PositiveInfinity, 10) })
        {
            h.MovePointerTo(p); h.Tick();
            Assert.Equal(new PointD(142, 100), h.WindowPosition);
            Assert.Equal(0.5, h.Renders[^1].Direct.Strength, 8);
        }
        h.LosePointer(); h.Tick();
        Assert.Equal(0.5, h.Renders[^1].Direct.Strength, 8);
        h.Cancel(); h.MovePointerTo(new(500, 400)); h.Tick();
        Assert.Equal(new PointD(142, 100), h.WindowPosition);
        Assert.NotEqual(PetState.Dragged, h.Renders[^1].Core.State);
        Assert.False(h.Renders[^1].Core.IsDirectInteractionPending);
        Assert.Equal(1, h.ReleaseCount);
    }

    [Fact]
    public void Five_body_hold_pauses_an_already_walking_brain_and_regrab_has_fresh_input()
    {
        using var h = new LoopHarness(CreateWalkingBrain); h.Start(); Assert.Equal(PetState.Walk, h.Renders[^1].Core.State);
        h.Press(DirectInteractionTarget.FiveRegionBody, 0); h.Tick(); var position = h.WindowPosition;
        for (var i = 0; i < 40; i++) h.Tick();
        Assert.Equal(position, h.WindowPosition);
        h.Release(); for (var i = 0; i < 13; i++) h.Tick();
        h.Press(DirectInteractionTarget.FiveRegionBody, 0); h.Tick();
        Assert.Equal(default, h.Renders[^1].Direct.BodyPull!.Value.PullSource);
        Assert.Equal(2, h.CaptureCount);
    }
    [Fact]
    public void Normal_mouse_release_lost_capture_notification_does_not_cancel_body_settle()
    {
        using var h = new LoopHarness(CreateIdleBrain) { NotifyLostCaptureOnRelease = true };
        h.Start(); h.Press(DirectInteractionTarget.FiveRegionBody, 0); h.Tick();
        h.MovePointerBy(new(12, 0)); for (var i = 0; i < 10; i++) h.Tick();
        h.Release(); h.Tick();
        Assert.Equal(DirectInteractionPhase.BodyLocalSettle, h.Renders[^1].Direct.Phase);
        Assert.True(h.Renders[^1].Direct.BodyPull!.Value.PullSource.X > 0);
        Assert.Equal(1, h.ReleaseCount);
    }
    [Fact]
    public void Five_body_local_hold_captures_and_prevents_autonomous_motion()
    {
        using var h = new LoopHarness(CreateIdleBrain); h.Start(); var before = h.WindowPosition;
        h.Press(DirectInteractionTarget.FiveRegionBody, 0); h.Tick();
        Assert.Equal(1, h.CaptureCount);
        h.MovePointerBy(new(12, 0)); for (var i = 0; i < 40; i++) h.Tick();
        Assert.Equal(before, h.WindowPosition);
        Assert.True(h.Renders[^1].Direct.BodyPull!.Value.PullSource.X > 10);
        Assert.Equal(PetState.Idle, h.Renders[^1].Core.State);
    }

    [Fact]
    public void Five_body_carry_clamps_reconciles_brain_and_does_not_snap_back_on_release_or_cancel()
    {
        foreach (var cancel in new[] { false, true })
        {
            using var h = new LoopHarness(CreateIdleBrain); h.Start();
            h.Press(DirectInteractionTarget.FiveRegionBody, 0); h.Tick();
            h.MovePointerTo(new(1500, -500)); for (var i = 0; i < 30; i++) h.Tick();
            Assert.Equal(new(680, 0), h.WindowPosition);
            Assert.Equal(h.WindowPosition, h.Renders[^1].Core.Position);
            Assert.Equal(h.WindowPosition, h.Renders[^1].Direct.BodyPull!.Value.WindowPosition);
            if (cancel) h.Cancel(); else h.Release();
            for (var i = 0; i < 20; i++) h.Tick();
            Assert.Equal(new(680, 0), h.WindowPosition);
            Assert.Equal(1, h.ReleaseCount);
            Assert.Equal(DirectInteractionTarget.None, h.Renders[^1].Direct.Target);
        }
    }

    [Fact]
    public void Five_body_quick_release_capture_failure_and_fault_clear_presentation()
    {
        using (var h = new LoopHarness(CreateIdleBrain))
        {
            h.Start(); h.Press(DirectInteractionTarget.FiveRegionBody, 0); h.Release();
            Assert.Equal(0, h.CaptureCount);
            for (var i = 0; i < 20; i++) h.Tick();
            Assert.Equal(DirectInteractionTarget.None, h.Renders[^1].Direct.Target);
        }
        foreach (var captureFails in new[] { true, false })
        {
            using var h = new LoopHarness(CreateIdleBrain) { CaptureSucceeds = !captureFails };
            h.Start(); h.Press(DirectInteractionTarget.FiveRegionBody, 0); h.Tick();
            if (!captureFails) h.FaultOnNextTick();
            Assert.Equal(1, h.FaultCount);
            Assert.Equal(DirectInteractionTarget.None, h.Renders[^1].Direct.Target);
        }
    }

    [Fact]
    public void Cheek_press_captures_without_moving_window()
    {
        using var harness = new LoopHarness(CreateIdleBrain);
        harness.Start();
        var beforePress = harness.WindowPosition;

        harness.Press(DirectInteractionTarget.RightCheek, outwardSign: -1);
        harness.Tick();

        Assert.Equal(1, harness.CaptureCount);
        Assert.All(harness.WindowPositions, position => Assert.Equal(beforePress, position));
        Assert.Equal(DirectInteractionPhase.CheekPress, harness.Renders[^1].Direct.Phase);
        Assert.True(harness.Renders[^1].Direct.RequiresCapture);
    }

    [Fact]
    public void Body_press_does_not_capture_before_core_drag_threshold()
    {
        using var harness = new LoopHarness(CreateIdleBrain);
        harness.Start();

        harness.Press(DirectInteractionTarget.Body, outwardSign: 0);
        harness.Tick();

        Assert.Equal(0, harness.CaptureCount);
        Assert.Equal(PetState.Idle, harness.Renders[^1].Core.State);
        Assert.Equal(DirectInteractionPhase.BodyPending, harness.Renders[^1].Direct.Phase);
        Assert.False(harness.Renders[^1].Direct.RequiresCapture);
    }

    [Fact]
    public void Body_click_keeps_every_real_loop_window_position_unchanged()
    {
        using var harness = new LoopHarness(CreateIdleBrain);
        harness.Start();
        var beforePress = harness.WindowPosition;

        harness.Press(DirectInteractionTarget.Body, outwardSign: 0);
        harness.Tick();
        harness.Release();
        harness.Tick();

        Assert.Equal(PetState.ClickReaction, harness.Renders[^1].Core.State);
        Assert.All(harness.WindowPositions, position => Assert.Equal(beforePress, position));
    }

    [Fact]
    public void Body_drag_follows_before_full_extension_then_settles_at_clamped_release()
    {
        using var harness = new LoopHarness(CreateIdleBrain);
        harness.Start();
        var beforePress = harness.WindowPosition;

        harness.Press(DirectInteractionTarget.Body, outwardSign: 0);
        harness.Tick();
        harness.MovePointerBy(new PointD(20, 10));
        harness.Tick();

        var entered = harness.Renders[^1];
        Assert.Equal(PetState.Dragged, entered.Core.State);
        Assert.Equal(beforePress + new PointD(20, 10), harness.WindowPosition);
        Assert.Equal(new PointD(60, 50), entered.Core.GrabOffset);
        Assert.Equal(DirectInteractionPhase.BodyDragEntry, entered.Direct.Phase);
        Assert.True(entered.Direct.RequiresCapture);
        Assert.Equal(1, harness.CaptureCount);

        harness.MovePointerTo(new PointD(900, 700));
        harness.Tick();
        Assert.Equal(new PointD(680, 500), harness.WindowPosition);

        harness.Release();

        var released = harness.Renders[^1];
        Assert.Equal(PetState.Idle, released.Core.State);
        Assert.Equal(new PointD(680, 500), harness.WindowPosition);
        Assert.Null(released.Core.GrabOffset);
        Assert.Equal(DirectInteractionPhase.BodyDragSettle, released.Direct.Phase);
        Assert.False(released.Direct.RequiresCapture);
        Assert.Equal(1, harness.ReleaseCount);
    }

    [Fact]
    public void Locked_cheek_rejects_a_second_body_press()
    {
        using var harness = new LoopHarness(CreateIdleBrain);
        harness.Start();
        harness.Press(DirectInteractionTarget.LeftCheek, outwardSign: 1);
        harness.Tick();

        harness.Press(DirectInteractionTarget.Body, outwardSign: 0);
        harness.Tick();

        var rendered = harness.Renders[^1];
        Assert.Equal(DirectInteractionTarget.LeftCheek, rendered.Direct.Target);
        Assert.False(rendered.Core.IsDirectInteractionPending);
    }

    [Fact]
    public void Release_cancel_dispose_and_fault_each_release_capture_once()
    {
        AssertCleanupReleasesCaptureOnce(harness => harness.Release());
        AssertCleanupReleasesCaptureOnce(harness => harness.Cancel());
        AssertCleanupReleasesCaptureOnce(harness => harness.Dispose());
        AssertCleanupReleasesCaptureOnce(harness => harness.FaultOnNextTick());
    }

    [Fact]
    public void Render_receives_core_and_direct_snapshots_from_the_same_tick()
    {
        using var harness = new LoopHarness(CreateSleepingBrain);
        harness.Start();
        Assert.Equal(PetState.Sleep, harness.Renders[^1].Core.State);
        Assert.Equal(DirectInteractionSnapshot.None, harness.Renders[^1].Direct);

        harness.Press(DirectInteractionTarget.LeftCheek, outwardSign: 1);
        harness.Tick();

        var rendered = harness.Renders[^1];
        Assert.Equal(PetState.Idle, rendered.Core.State);
        Assert.Equal(DirectInteractionTarget.LeftCheek, rendered.Direct.Target);
        Assert.Equal(DirectInteractionPhase.CheekPress, rendered.Direct.Phase);
    }

    private static void AssertCleanupReleasesCaptureOnce(Action<LoopHarness> cleanup)
    {
        var harness = new LoopHarness(CreateIdleBrain);
        harness.Start();
        harness.Press(DirectInteractionTarget.LeftCheek, outwardSign: 1);
        harness.Tick();
        Assert.Equal(1, harness.CaptureCount);

        cleanup(harness);
        harness.Dispose();

        Assert.Equal(1, harness.ReleaseCount);
    }

    private static PetBrain CreateIdleBrain() =>
        new(
            BehaviorTuning.Default with
            {
                IdleMin = TimeSpan.FromMinutes(10),
                IdleMax = TimeSpan.FromMinutes(10),
                SleepDelay = TimeSpan.FromMinutes(10)
            },
            new SeededRandomSource(1234),
            new PointD(100, 100));

    private static PetBrain CreateWalkingBrain()
    {
        var brain = new PetBrain(BehaviorTuning.Default with
        {
            IdleMin = TimeSpan.FromMilliseconds(1),
            IdleMax = TimeSpan.FromMilliseconds(1),
            WalkMin = TimeSpan.FromMinutes(10),
            WalkMax = TimeSpan.FromMinutes(10),
            IdleToWalkProbability = 1
        }, new SeededRandomSource(1234), new(100, 100));
        brain.Update(new PetInput(TimeSpan.FromMilliseconds(1), LoopHarness.WorkArea, LoopHarness.PetSize, PointerSample.Unavailable, false, false, null, LoopHarness.DragThreshold));
        return brain;
    }

    private static PetBrain CreateSleepingBrain()
    {
        var brain = new PetBrain(
            BehaviorTuning.Default with
            {
                MaxDelta = TimeSpan.FromMilliseconds(100),
                IdleMin = TimeSpan.FromMinutes(10),
                IdleMax = TimeSpan.FromMinutes(10),
                SleepDelay = TimeSpan.FromMilliseconds(100)
            },
            new SeededRandomSource(1234),
            new PointD(100, 100));
        brain.Update(new PetInput(
            TimeSpan.FromMilliseconds(100),
            LoopHarness.WorkArea,
            LoopHarness.PetSize,
            PointerSample.Unavailable,
            false,
            false,
            null,
            LoopHarness.DragThreshold));
        return brain;
    }

    private sealed class LoopHarness : IDisposable
    {
        internal static readonly RectD WorkArea = new(0, 0, 800, 600);
        internal static readonly SizeD PetSize = new(120, 100);
        internal static readonly SizeD DragThreshold = new(4, 4);

        private readonly PetLoop _loop;
        private EventHandler? _tick;
        private TimeSpan _elapsed;
        private bool _primaryButtonDown;
        private PointerSample _pointer = PointerSample.Unavailable;
        private bool _throwOnSetWindowPosition;

        internal LoopHarness(Func<PetBrain> brainFactory)
        {
            var clock = new PetLoopClock(
                () => _elapsed,
                () => { },
                () => { });
            var timer = new PetLoopTimer(
                handler => _tick += handler,
                handler => _tick -= handler,
                () => { },
                () => { });
            var host = new PetLoopHost(
                () => WorkArea,
                () => PetSize,
                () => DragThreshold,
                () => _pointer,
                () => _primaryButtonDown,
                () => WindowPosition,
                position =>
                {
                    if (_throwOnSetWindowPosition)
                    {
                        throw new InvalidOperationException("set-window fault");
                    }

                    WindowPosition = position;
                    WindowPositions.Add(position);
                },
                (core, direct) => Renders.Add((core, direct)),
                () =>
                {
                    CaptureCount++;
                    return CaptureSucceeds;
                },
                () =>
                {
                    ReleaseCount++;
                    if (NotifyLostCaptureOnRelease) _loop!.NotifyDirectInteractionCanceled();
                });
            _loop = new PetLoop(clock, timer, host, _ => brainFactory());
            _loop.Faulted += (_, _) => FaultCount++;
        }

        internal PointD WindowPosition { get; private set; }
        internal List<PointD> WindowPositions { get; } = [];
        internal List<(PetSnapshot Core, DirectInteractionSnapshot Direct)> Renders { get; } = [];
        internal int CaptureCount { get; private set; }
        internal int ReleaseCount { get; private set; }
        internal bool CaptureSucceeds { get; init; } = true;
        internal int FaultCount { get; private set; }
        internal bool NotifyLostCaptureOnRelease { get; init; }

        internal void Start() => _loop.Start();

        internal void Press(DirectInteractionTarget target, double outwardSign)
        {
            var localPosition = new PointD(60, 50);
            _pointer = new PointerSample(true, WindowPosition + localPosition);
            _primaryButtonDown = true;
            _loop.NotifyDirectInteractionPressed(new DirectInteractionPressEventArgs(
                target,
                localPosition,
                new PointD(30, 56),
                outwardSign,
                target == DirectInteractionTarget.FiveRegionBody ? Interaction.BodyPullTests.Capture() : null));
        }

        internal void PressCapturedCheek(bool mirror = false)
        {
            var local=new PointD(60,50);_pointer=new(true,WindowPosition+local);_primaryButtonDown=true;
            var matrix = mirror ? new System.Windows.Media.Matrix(-1,0,0,1,96,0) : System.Windows.Media.Matrix.Identity;
            var capture=new CheekPullCapture(Controls.CheekProductTests.Read("canonical.png"),matrix,mirror ? FacingDirection.Left : FacingDirection.Right);
            _loop.NotifyDirectInteractionPressed(new DirectInteractionPressEventArgs(DirectInteractionTarget.RightCheek,local,new(16,58),-1){CheekCapture=capture});
        }

        internal void PressFromPresenter(DororongPresenter presenter, PointD source)
        {
            var local=new PointD(60,50);_pointer=new(true,WindowPosition+local);_primaryButtonDown=true;
            var target=presenter.ClassifyOpaqueSourcePoint(source,true);
            if(target==DirectInteractionTarget.None)return; // Actual input handler does not dispatch unowned pixels.
            presenter.TryCreateBodyPullCapture(source,out var body);
            presenter.TryCreateCheekPullCapture(source,out var cheek);
            _loop.NotifyDirectInteractionPressed(new DirectInteractionPressEventArgs(target,local,source,-1,body){CheekCapture=cheek});
        }

        internal void Tick()
        {
            _elapsed += TimeSpan.FromMilliseconds(16);
            _tick?.Invoke(this, EventArgs.Empty);
        }

        internal void MovePointerBy(PointD delta) =>
            _pointer = new PointerSample(true, _pointer.Position + delta);

        internal void MovePointerTo(PointD position) =>
            _pointer = new PointerSample(true, position);

        internal void LosePointer() => _pointer = PointerSample.Unavailable;

        internal void Release()
        {
            _primaryButtonDown = false;
            Tick();
        }

        internal void Cancel() => _loop.NotifyDirectInteractionCanceled();

        internal void FaultOnNextTick()
        {
            _throwOnSetWindowPosition = true;
            Tick();
        }

        public void Dispose() => _loop.Dispose();
    }
}
