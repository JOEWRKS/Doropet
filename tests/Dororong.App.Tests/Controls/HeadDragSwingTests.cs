using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class HeadDragSwingTests
{
    // These exercise controller input through the actual Presenter. Removing the
    // speed law, damping, rotation or post-rotation anchor correction must fail.
    [Fact]
    public void Faster_horizontal_motion_produces_proportionally_greater_body_lag() => Sta(() =>
    {
        var slow = new Harness(); var fast = new Harness();
        slow.Hold(); fast.Hold();
        // The wide-swing gain doubled; keep this ratio probe below the preview cap.
        for (var i = 0; i < 150; i++) { slow.Move(1, 0); fast.Move(3, 0); }
        Assert.InRange(slow.Angle, 3, 9);
        Assert.InRange(fast.Angle / slow.Angle, 2.8, 3.2);
        // Positive screen rotation puts feet to the left of the grabbed head.
        Assert.True(fast.Foot.X < slow.Foot.X);
    });

    [Fact]
    public void Stopping_keeps_inertia_then_crosses_rest_and_damps_to_rest() => Sta(() =>
    {
        var h = new Harness(); h.Hold();
        for (var i = 0; i < 100; i++) h.Move(8, 0);
        var held = h.Angle;
        h.Move(0, 0);
        Assert.InRange(h.Angle, held - 3, held + 3);
        var angles = new List<double>();
        for (var i = 0; i < 220; i++) { h.Move(0, 0); angles.Add(h.Angle); }
        Assert.True(angles.Min() < -1, "A hanging body should swing past rest after a stop.");
        Assert.True(angles.Skip(90).Any(a => a > .05), "The residual swing should return again, not one-way ease out.");
        Assert.InRange(Math.Abs(h.Angle), 0, .1);
    });

    [Fact]
    public void Reversal_is_continuous_and_mirrored_facing_keeps_screen_space_lag() => Sta(() =>
    {
        var right = new Harness(); var left = new Harness(true);
        right.Hold(); left.Hold();
        for (var i = 0; i < 100; i++) { right.Move(8, 0); left.Move(8, 0); }
        Assert.InRange(right.Angle, 10, 30);
        Assert.Equal(right.Angle, left.Angle, 6);
        var old = right.Angle;
        right.Move(-8, 0); left.Move(-8, 0);
        Assert.InRange(right.Angle, old - 3, old + 3);
        for (var i = 0; i < 120; i++) { right.Move(-8, 0); left.Move(-8, 0); }
        Assert.InRange(right.Angle, -30, -10);
        Assert.Equal(right.Angle, left.Angle, 6);
    });

    [Fact]
    public void Same_speed_at_different_tick_intervals_has_the_same_response() => Sta(() =>
    {
        var fine = new Harness(); var coarse = new Harness(); fine.Hold(); coarse.Hold();
        for (var i = 0; i < 150; i++) fine.Move(1, 0, 8);
        for (var i = 0; i < 50; i++) coarse.Move(3, 0, 24);
        Assert.InRange(fine.Angle, 6, 14);
        Assert.InRange(Math.Abs(fine.Angle - coarse.Angle), 0, .35);
    });

    [Theory]
    [InlineData(false, 25.25, 37.5)]
    [InlineData(true, 25.25, 37.5)]
    [InlineData(false, 37.5, 28.25)]
    [InlineData(true, 37.5, 28.25)]
    [InlineData(false, 50.25, 48.5)]
    [InlineData(true, 50.25, 48.5)]
    public void Swing_keeps_grabbed_point_and_all_ink_inside_window(bool mirror, double x, double y) => Sta(() =>
    {
        var h = new Harness(mirror, x, y); h.Hold();
        for (var i = 0; i < 130; i++)
        {
            h.Move(i < 65 ? 40 : -40, 0);
            h.AssertAnchorAndBounds();
            Assert.InRange(h.Angle, -88, 88);
        }
        Assert.True(h.Angle < -10, "Rotation must actually be present, not disabled to satisfy bounds.");
        h.Release();
        for (var i = 0; i < 12; i++) { h.Step(16, false); h.AssertBounds(); }
        Assert.Equal(0, h.Angle);
    });

    [Fact]
    public void Entry_and_tremor_do_not_swing_but_release_preserves_then_removes_held_angle() => Sta(() =>
    {
        var h = new Harness();
        h.Move(40, 0); Assert.Equal(0, h.Angle);
        h.Hold();
        for (var i = 0; i < 100; i++) h.Move(i % 2 == 0 ? .15 : -.15, 0);
        Assert.InRange(Math.Abs(h.Angle), 0, .1);
        for (var i = 0; i < 100; i++) h.Move(8, 0);
        var held = h.Angle; Assert.True(held > 10);
        h.Release(); Assert.Equal(held, h.Angle, 6);
        h.Step(90, false); Assert.Equal(held / 2, h.Angle, 5);
        h.Step(90, false); Assert.Equal(0, h.Angle);
        Assert.Equal(BitmapScalingMode.HighQuality, RenderOptions.GetBitmapScalingMode(h.Image));
    });

    [Fact]
    public void Cancel_and_regrab_discard_old_velocity_and_angle() => Sta(() =>
    {
        var h = new Harness(); h.Hold();
        for (var i = 0; i < 100; i++) h.Move(8, 0);
        Assert.True(h.Angle > 10);
        h.Reset(); Assert.Equal(0, h.Angle); h.Hold();
        for (var i = 0; i < 100; i++) h.Move(0, 0);
        Assert.Equal(0, h.Angle);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Releasing_a_viewport_limited_swing_never_adds_more_tilt(bool mirror) => Sta(() =>
    {
        var h = new Harness(mirror); h.Hold();
        for (var i = 0; i < 100; i++) h.Move(mirror ? -40 : 40, 0);
        var previous = Math.Abs(h.Angle);
        Assert.InRange(previous, 10, 34); // Exercise the real viewport limit, not an unsaturated swing.
        h.Release(); Assert.Equal(previous, Math.Abs(h.Angle), 6);
        for (var i = 0; i < 12; i++)
        {
            h.Step(16, false);
            Assert.InRange(Math.Abs(h.Angle), 0, previous + 1e-6);
            previous = Math.Abs(h.Angle);
            h.AssertBounds();
        }
        Assert.Equal(0, h.Angle);
    });

    [Fact]
    public void Invalid_pointer_and_time_gap_do_not_create_resume_impulse() => Sta(() =>
    {
        var h = new Harness(); h.Hold();
        h.Unavailable(); h.Pointer += new PointD(10000, 0); h.Step(16, true);
        Assert.Equal(0, h.Angle);
        h.Pointer += new PointD(10000, 0); h.Step(2000, true);
        Assert.Equal(0, h.Angle);
        h.Pointer += new PointD(10000, 0); h.Step(0, true);
        Assert.Equal(0, h.Angle);
        for (var i = 0; i < 100; i++) h.Move(8, 0);
        Assert.True(h.Angle > 10);
    });

    private sealed class Harness
    {
        private readonly DirectInteractionController _controller = new();
        private readonly DororongPresenter _presenter = new();
        private readonly bool _mirror;
        private readonly PointD _grab;
        private PointD _press, _windowGrab;
        private static readonly PointD Initial = new(100, 100);
        internal PointD Pointer;
        internal Image Image => (Image)_presenter.FindName("DororongImage");
        internal double Angle => ((RotateTransform)_presenter.FindName("BodyRotateTransform")).Angle;
        internal Point Foot => Image.TranslatePoint(new Point(48, 82), _presenter);
        internal Harness(bool mirror = false, double x = 37.5, double y = 28.25)
        { _mirror = mirror; _grab = new(x, y); Reset(); }
        internal void Reset()
        {
            _controller.Cancel();
            _presenter.Render(new(PetState.Walk, Initial, _mirror ? FacingDirection.Left : FacingDirection.Right, .31, false, null), DirectInteractionSnapshot.None); Layout();
            var p = Image.TranslatePoint(new Point(_grab.X, _grab.Y), _presenter);
            _windowGrab = new(p.X, p.Y); _press = Initial + _windowGrab; Pointer = _press;
            _controller.BeginDistanceBody(_press, new(4, 4)); Step(0, true);
        }
        internal void Hold() { Pointer = _press + new PointD(0, -80); Step(16, true); }
        internal void Move(double x, double y, double ms = 16) { Pointer += new PointD(x, y); Step(ms, true); }
        internal void Release() => Step(0, false);
        internal void Unavailable()
        {
            var s = _controller.Advance(TimeSpan.FromMilliseconds(16), PointerSample.Unavailable, true, PetState.Dragged, PetState.Dragged);
            Render(s, true);
        }
        internal void Step(double ms, bool down)
        {
            var displacement = Pointer - _press;
            var dragged = down && (Math.Abs(displacement.X) >= 4 || Math.Abs(displacement.Y) >= 4);
            var s = _controller.Advance(TimeSpan.FromMilliseconds(ms), new(true, Pointer), down, PetState.Dragged, dragged ? PetState.Dragged : PetState.Idle);
            Render(s, dragged);
        }
        private void Render(DirectInteractionSnapshot s, bool dragged)
        {
            var window = s.RequiresCapture ? Pointer - _windowGrab : Initial;
            _presenter.Render(new(dragged ? PetState.Dragged : PetState.Idle, window, FacingDirection.Right, 0, false, null), s); Layout();
        }
        internal void AssertAnchorAndBounds()
        {
            // Literal final-key displacement from the independent approved8 measurements.
            var local = Image.TranslatePoint(new Point(_grab.X + 11, _grab.Y - 16), _presenter);
            Assert.Equal(_windowGrab.X, local.X, 6); Assert.Equal(_windowGrab.Y, local.Y, 6);
            AssertBounds();
        }
        internal void AssertBounds()
        {
            var frame = PremultipliedFrame.From((BitmapSource)Image.Source);
            for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++) if (frame.Pixels[(y * 96 + x) * 4 + 3] > 0)
            {
                var p = Image.TranslatePoint(new Point(x + .5, y + .5), _presenter);
                Assert.InRange(p.X, 0, 144); Assert.InRange(p.Y, 0, 144);
            }
        }
        private void Layout() { _presenter.Measure(new Size(144, 144)); _presenter.Arrange(new Rect(0, 0, 144, 144)); _presenter.UpdateLayout(); }
    }
    private static void Sta(Action action)
    {
        Exception? error = null; var t = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
        t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join(); if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
