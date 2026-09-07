using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Interaction;

// Window ownership is independent of the frozen source-to-window image transform.
// The renderer still receives the approved signed horizontal DIP input only.
internal sealed class CheekCarrySession
{
    private readonly CheekPullCapture _capture;
    private readonly PointD _initialWindow, _press;
    private PointD _target, _lead, _filtered;
    private double _pending;
    private bool _carried;
    internal PointD WindowPosition { get; private set; }
    internal double PullDips { get; private set; }

    internal CheekCarrySession(CheekPullCapture capture, PointD initialWindow, PointD press)
    {
        _capture = capture; _initialWindow = initialWindow; _press = press;
        WindowPosition = initialWindow;
    }

    internal void Advance(TimeSpan delta, PointerSample pointer, RectD workArea, SizeD petSize)
    {
        if (pointer.IsAvailable && HeadPullDistance.IsFinite(pointer.Position))
        {
            var next = pointer.Position - _press;
            var difference = next - _target;
            var length = BodyPullSession.Length(difference);
            if (HeadPullDistance.IsFinite(next) && double.IsFinite(length) && length > .8)
                _target = next;
        }
        var milliseconds = delta.TotalMilliseconds;
        if (!double.IsFinite(milliseconds) || milliseconds <= 0) return;
        _pending = Math.Min(_pending + milliseconds, 250);
        var filter = 1 - Math.Exp(-4d / 55);
        var follow = 1 - Math.Exp(-4d / 75);
        while (_pending + 1e-8 >= 4)
        {
            _lead += BodyPullSession.Scale(_target - _lead, filter);
            _filtered += BodyPullSession.Scale(_lead - _filtered, filter);
            // Exponential filters approach, but never reach, an exact threshold.
            // Snap only the sub-millipixel settled residue so a literal20DIP pull
            // can enter carry while19.9DIP remains below the gate.
            if (BodyPullSession.Length(_target - _filtered) < .001)
                _lead = _filtered = _target;
            var remaining = _filtered - (WindowPosition - _initialWindow);
            if (!_carried && _capture.Measure(remaining) >= 20 - 1e-8) _carried = true;
            if (_carried)
            {
                // Hand only horizontal cap overflow to the window, then let the
                // same damped follower carry in any direction (including reversal).
                var signed = _capture.Measure(remaining);
                var overflow = signed - Math.Clamp(signed, -10, 20);
                WindowPosition += BodyPullSession.Scale(_capture.OutwardUnit, overflow);
                remaining = _filtered - (WindowPosition - _initialWindow);
                WindowPosition += BodyPullSession.Scale(remaining, follow);
            }
            WindowPosition = workArea.ClampTopLeft(WindowPosition, petSize);
            // Recompute from the actual clamped position, never an off-screen target.
            PullDips = Math.Clamp(_capture.Measure(_filtered - (WindowPosition - _initialWindow)), -10, 20);
            _pending = Math.Max(0, _pending - 4);
        }
    }
}
