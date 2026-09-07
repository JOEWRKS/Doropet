using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class HeadLandingTests
{
    // Real loop/brain/controller/Presenter, not a manually advanced substitute.
    [Theory]
    [InlineData(80, 36)]
    [InlineData(42, 18)]
    public void Release_falls_from_last_displayed_position_and_keeps_landed_position(double pull, double drop) => Sta(() =>
    {
        using var h = new Harness(); h.Pull(pull); var release = h.Position;
        h.Pointer += new PointD(200, 0); // Button-up sample must not teleport the pet.
        h.Release(); Assert.Equal(release, h.Position); Assert.Equal(1, h.ReleaseCount);
        h.Tick(110); Assert.Equal(release.Y + drop / 4, h.Position.Y, 6);
        Assert.Equal(1, h.ScaleY, 6); // No squash before the longer fall reaches contact.
        h.Tick(110); Assert.Equal(release.Y + drop, h.Position.Y, 6);
        Assert.Equal(release.X, h.Position.X);
        Assert.Equal(DirectInteractionPhase.BodyDragSettle, h.LastDirect.Phase);
        h.Tick(180); Assert.Equal(DirectInteractionPhase.BodyDragSettle, h.LastDirect.Phase); // 400ms: recovery still active.
        h.Tick(40); Assert.Equal(DirectInteractionTarget.None, h.LastDirect.Target);
        for (var i = 0; i < 20; i++) h.Tick(16);
        Assert.Equal(release + new PointD(0, drop), h.Position);
        Assert.Equal(h.Position, h.LastCore.Position); Assert.Equal(1, h.ReleaseCount);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Landing_squashes_then_rebounds_around_the_same_sole(bool mirror) => Sta(() =>
    {
        using var h = new Harness(mirror: mirror); h.Pull(80); h.Release();
        h.Tick(220); var sole = h.Sole; Assert.Equal(1, h.ScaleY, 6);
        h.Tick(40); Assert.InRange(h.ScaleY, .80, .85); Assert.InRange(Math.Abs(h.ScaleX), 1.08, 1.12);
        Assert.Equal(sole.X, h.Sole.X, 6); Assert.Equal(sole.Y, h.Sole.Y, 6); h.AssertBounds();
        h.Tick(80); Assert.InRange(h.ScaleY, 1.02, 1.06); Assert.InRange(Math.Abs(h.ScaleX), .96, .99);
        Assert.Equal(sole.X, h.Sole.X, 6); Assert.Equal(sole.Y, h.Sole.Y, 6); h.AssertBounds();
        h.Tick(100); Assert.Equal(1, h.ScaleY, 6); Assert.Equal(1, Math.Abs(h.ScaleX), 6);
        Assert.Equal(DirectInteractionTarget.None, h.LastDirect.Target);
        Assert.Equal(mirror ? -1 : 1, h.ScaleX);
        h.Tick(32); Assert.Equal(mirror ? -1 : 1, h.ScaleX);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void Drop_near_bottom_uses_only_available_space(double available) => Sta(() =>
    {
        using var h = new Harness(initial: new PointD(100, 456 - available)); h.Pull(80); var start = h.Position;
        h.Release();
        for (var i = 0; i < 30; i++)
        {
            h.Tick(16); Assert.InRange(h.Position.Y, start.Y, 456); h.AssertBounds();
        }
        Assert.Equal(456, h.Position.Y, 6);
        Assert.Equal(DirectInteractionTarget.None, h.LastDirect.Target);
    });

    [Fact]
    public void Cancel_during_fall_freezes_current_position_and_next_grab_has_no_landing_state() => Sta(() =>
    {
        using var h = new Harness(); h.Pull(80); var origin = h.Position; h.Release(); h.Tick(90);
        Assert.True(h.Position.Y > origin.Y); var atCancel = h.Position;
        h.Cancel(); h.Tick(100); Assert.Equal(atCancel, h.Position); Assert.Equal(DirectInteractionTarget.None, h.LastDirect.Target);
        h.Press(); Assert.Equal(DirectInteractionPhase.BodyPending, h.LastDirect.Phase);
        Assert.Equal(1, h.ScaleY, 6); h.PullFromPress(42);
        Assert.Equal(atCancel + new PointD(42, 0), h.Position); Assert.Equal(0.5, h.LastDirect.Strength, 6);
    });

    [Fact]
    public void Deadzone_click_does_not_fall_or_squash() => Sta(() =>
    {
        using var h = new Harness(); h.Press(); var start = h.Position; h.Release();
        for (var i = 0; i < 30; i++) h.Tick(16);
        Assert.Equal(start, h.Position); Assert.Equal(DirectInteractionTarget.None, h.LastDirect.Target);
    });

    [Theory]
    [InlineData(false, 90)]
    [InlineData(true, 90)]
    [InlineData(false, 260)]
    [InlineData(true, 260)]
    public void Fault_or_dispose_clears_fall_and_squash_without_resuming_motion(bool dispose, double elapsed) => Sta(() =>
    {
        using var h = new Harness(); h.Pull(80); h.Release(); h.Tick(elapsed); var stopped = h.Position;
        if (dispose) h.Dispose(); else h.FaultOnInput();
        Assert.Equal(DirectInteractionTarget.None, h.LastDirect.Target);
        Assert.Null(h.LastDirect.HeadLanding); Assert.Equal(1, h.ScaleY, 6);
        h.Tick(500); Assert.Equal(stopped, h.Position); Assert.Equal(1, h.ReleaseCount);
    });

    [Fact]
    public void Large_tick_finishes_at_landing_target_without_overshoot() => Sta(() =>
    {
        using var h = new Harness(); h.Pull(80); var start = h.Position; h.Release();
        h.Tick(0); Assert.Equal(start, h.Position);
        h.Tick(2000); Assert.Equal(start + new PointD(0, 36), h.Position);
        Assert.Equal(DirectInteractionTarget.None, h.LastDirect.Target); Assert.Equal(1, h.ScaleY, 6);
    });

    private sealed class Harness : IDisposable
    {
        private readonly DororongPresenter _presenter = new();
        private readonly PetLoop _loop;
        private EventHandler? _tick;
        private TimeSpan _elapsed;
        private bool _down;
        private bool _failInput;
        private bool _expectFault;
        private Exception? _fault;
        private PointD _press;
        internal PointD Pointer;
        internal PointD Position;
        internal PetSnapshot LastCore;
        internal DirectInteractionSnapshot LastDirect;
        internal int ReleaseCount;
        private Image Image => (Image)_presenter.FindName("DororongImage");
        internal double ScaleX => ((ScaleTransform)_presenter.FindName("BodyScaleTransform")).ScaleX;
        internal double ScaleY => ((ScaleTransform)_presenter.FindName("BodyScaleTransform")).ScaleY;
        internal Point Sole { get { var p = Image.TranslatePoint(new(48, 87), _presenter); return new(p.X + Position.X, p.Y + Position.Y); } }
        internal Harness(PointD? initial = null, bool mirror = false)
        {
            var start = initial ?? new PointD(100, 100);
            var clock = new PetLoopClock(() => _elapsed, () => { }, () => { });
            var timer = new PetLoopTimer(h => _tick += h, h => _tick -= h, () => { }, () => { });
            var host = new PetLoopHost(() => new(0, 0, 800, 600), () => new(144, 144), () => new(4, 4),
                () => _failInput ? throw new InvalidOperationException("test input failure") : new(true, Pointer), () => _down, () => Position, p => Position = p,
                (core, direct) => { LastCore = core; LastDirect = direct; _presenter.Render(core, direct); Layout(); },
                () => true, () => ReleaseCount++);
            _loop = new(clock, timer, host, _ => new PetBrain(BehaviorTuning.Default with
            { IdleMin = TimeSpan.FromMinutes(10), IdleMax = TimeSpan.FromMinutes(10), SleepDelay = TimeSpan.FromMinutes(10) }, new SeededRandomSource(17), start));
            _loop.Faulted += (_, e) => _fault = e;
            _loop.Start();
            if (mirror) { _presenter.Render(new(PetState.Walk, Position, FacingDirection.Left, 0, false, null), DirectInteractionSnapshot.None); Layout(); }
        }
        internal void Press()
        {
            var local = Image.TranslatePoint(new(37.5, 28.25), _presenter);
            _press = Position + new PointD(local.X, local.Y); Pointer = _press; _down = true;
            _loop.NotifyDirectInteractionPressed(new(DirectInteractionTarget.Body, new(local.X, local.Y), new(37.5, 28.25), 0)); Tick(16);
        }
        internal void Pull(double distance) { Press(); PullFromPress(distance); }
        internal void PullFromPress(double distance) { Pointer = _press + new PointD(distance, 0); Tick(16); }
        internal void Release() { _down = false; Tick(16); }
        internal void Tick(double ms)
        {
            _elapsed += TimeSpan.FromMilliseconds(ms); _tick?.Invoke(this, EventArgs.Empty);
            if (_fault is not null && !_expectFault) throw new Exception("Real loop faulted", _fault);
        }
        internal void FaultOnInput() { _expectFault = true; _failInput = true; Tick(16); Assert.NotNull(_fault); }
        internal void Cancel() => _loop.NotifyDirectInteractionCanceled();
        internal void AssertBounds()
        {
            var frame = PremultipliedFrame.From((BitmapSource)Image.Source);
            for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++) if (frame.Pixels[(y * 96 + x) * 4 + 3] > 0)
            {
                var p = Image.TranslatePoint(new(x + .5, y + .5), _presenter);
                Assert.InRange(p.X, 0, 144); Assert.InRange(p.Y, 0, 144);
            }
        }
        private void Layout() { _presenter.Measure(new(144, 144)); _presenter.Arrange(new Rect(0, 0, 144, 144)); _presenter.UpdateLayout(); }
        public void Dispose() => _loop.Dispose();
    }
    private static void Sta(Action action)
    {
        Exception? error = null; var t = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
        t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join(); if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
