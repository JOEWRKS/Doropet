using Dororong.Core.Behavior;

namespace Dororong.App.Controls;

internal enum PouncePhase { Watch, Flight, Landing, Track }

internal readonly record struct PouncePose(PouncePhase Phase, double DeltaX, double OffsetY,
    FacingDirection Direction, double Age, double TrackingRemaining,
    double AngleRadians, double ScaleX, double ScaleY)
{
    internal bool IsJump => Phase is PouncePhase.Flight or PouncePhase.Landing;
    internal bool IsEngaged => Phase != PouncePhase.Watch;
    internal static PouncePose Rest => new(PouncePhase.Watch, 0, 0, FacingDirection.Left, 0, 0, 0, 1, 1);
}

internal sealed class PounceSession
{
    private PouncePhase _phase;
    private double _elapsed, _dwell, _distance, _traveled;
    private FacingDirection _direction = FacingDirection.Left;
    internal PouncePose Current { get; private set; } = PouncePose.Rest;
    internal PouncePose Advance(double seconds, bool near, double targetDeltaX,
        FacingDirection facing, bool blocked = false)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (blocked) { Reset(); return Current; }
        var valid = double.IsFinite(targetDeltaX);
        near &= valid;
        var remaining = seconds;
        var dx = 0d;
        for (;;)
        {
            if ((_phase == PouncePhase.Track || _phase == PouncePhase.Watch && near) && valid)
                _direction = Math.Abs(targetDeltaX) > 4 ? targetDeltaX > 0 ? FacingDirection.Right : FacingDirection.Left : facing;
            if (_phase == PouncePhase.Watch)
            {
                if (!near) { _dwell = 0; break; }
                var used = Math.Min(remaining, 1.5 - _dwell);
                _dwell += used; remaining -= used;
                if (_dwell < 1.5 - 1e-9) break;
                _distance = (_direction == FacingDirection.Right ? 1 : -1) * Math.Min(28, Math.Max(0, Math.Abs(targetDeltaX) - 8));
                _traveled = 0; _elapsed = 0; _phase = PouncePhase.Flight;
            }
            else
            {
                var duration = _phase == PouncePhase.Flight ? .34 : _phase == PouncePhase.Landing ? .28 : 3;
                var used = Math.Min(remaining, duration - _elapsed);
                _elapsed += used; remaining -= used;
                if (_phase == PouncePhase.Flight)
                {
                    var next = _distance * Ease(_elapsed / .34);
                    dx += next - _traveled; _traveled = next;
                }
                if (_elapsed < duration - 1e-9) break;
                _elapsed = 0;
                _phase = _phase == PouncePhase.Flight ? PouncePhase.Landing :
                    _phase == PouncePhase.Landing ? PouncePhase.Track : PouncePhase.Watch;
                _dwell = 0;
            }
            if (remaining <= 1e-9) break;
        }
        var y = 0d; var angle = 0d; var sx = 1d; var sy = 1d;
        if (_phase == PouncePhase.Flight)
        {
            var u = Math.Clamp(_elapsed / .34, 0, 1);
            y = -12 * 4 * u * (1 - u);
            angle = .12 * Math.Sin(Math.PI * u) - .10 * Ease((u - .5) / .5);
            sx += .035 * Math.Sin(Math.PI * u);
        }
        else if (_phase == PouncePhase.Landing)
        {
            var u = Math.Clamp(_elapsed / .28, 0, 1);
            angle = -.10 * (1 - Ease(u / .55));
            var compression = Math.Sin(Math.PI * Math.Clamp(u / .8, 0, 1));
            sx += .025 * compression; sy -= .065 * compression;
        }
        return Current = new(_phase, dx, y, _direction,
            _phase == PouncePhase.Flight ? _elapsed : _phase == PouncePhase.Landing ? .34 + _elapsed : 0,
            _phase == PouncePhase.Track ? 3 - _elapsed : 0, angle, sx, sy);
    }

    internal void Reset()
    {
        _phase = PouncePhase.Watch;
        _elapsed = _dwell = _distance = _traveled = 0;
        Current = PouncePose.Rest;
    }

    private static double Ease(double value) { var u = Math.Clamp(value, 0, 1); return u * u * (3 - 2 * u); }
}
