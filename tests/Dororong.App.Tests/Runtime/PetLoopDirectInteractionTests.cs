using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Runtime;

public sealed class PetLoopDirectInteractionTests
{
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
    public void Body_drag_moves_on_the_threshold_tick_with_original_offset_then_settles_at_clamped_release()
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
                    return true;
                },
                () => ReleaseCount++);
            _loop = new PetLoop(clock, timer, host, _ => brainFactory());
        }

        internal PointD WindowPosition { get; private set; }
        internal List<PointD> WindowPositions { get; } = [];
        internal List<(PetSnapshot Core, DirectInteractionSnapshot Direct)> Renders { get; } = [];
        internal int CaptureCount { get; private set; }
        internal int ReleaseCount { get; private set; }

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
                outwardSign));
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
