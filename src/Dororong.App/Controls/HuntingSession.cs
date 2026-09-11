namespace Dororong.App.Controls;

internal sealed class HuntingSession
{
    private const double Duration = 2.6;
    private double _now;
    private double? _start;
    private double? _releaseAt;
    private bool _reversingRecovery;

    public double AgeSeconds { get; private set; } = Duration;
    // Skip the notice pause so lowering begins on the next presentation tick.
    // Replay lowering in250ms; retain the authored wiggle/recovery clock.
    public double FrameAgeSeconds => AgeSeconds < .7
        ? Math.Min(.7, .25 + AgeSeconds * (.45 / .25))
        : AgeSeconds;

    public bool IsActive => _start is not null;

    public void Advance(double seconds, bool near, bool blocked = false)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds), "Nonnegative finite delta required.");

        if (blocked)
        {
            Reset();
            return;
        }

        if (near && _releaseAt is { } release && _now >= release)
        {
            _reversingRecovery = true;
            _releaseAt = null;
        }
        if (_reversingRecovery)
        {
            _start = 0;
            if (near)
            {
                // Follow the authored tail backwards: both crouch depth and
                // the fading rump offset remain continuous on re-approach.
                AgeSeconds = Math.Max(1.65, AgeSeconds - seconds);
                _now = AgeSeconds;
                if (AgeSeconds <= 1.65) _reversingRecovery = false;
                return;
            }
            _now = AgeSeconds;
            _releaseAt = 1.65;
            _reversingRecovery = false;
        }

        if (_start is null)
        {
            if (!near)
            {
                AgeSeconds = Duration;
                return;
            }

            _now = 0;
            _start = _now;
        }
        else
        {
            _now += seconds;
        }

        var elapsed = Math.Max(0, _now - _start.Value);
        if (!near && _releaseAt is null)
            _releaseAt = _start + 1.65 + (Math.Max(0, Math.Ceiling(((elapsed - 1.65) / 0.4) - 1e-9)) * 0.4);
        if (near && _releaseAt is not null && _now < _releaseAt)
            _releaseAt = null;
        if (_releaseAt is not null && _now >= _releaseAt)
        {
            var age = 1.65 + _now - _releaseAt.Value;
            if (age >= Duration)
            {
                _start = null;
                _releaseAt = null;
                AgeSeconds = Duration;
                return;
            }

            AgeSeconds = age;
            return;
        }

        AgeSeconds = elapsed < 1.65 ? elapsed : 1.25 + ((elapsed - 1.25) % 0.4);
        if (near && elapsed >= 1.65)
        {
            _now = AgeSeconds;
            _start = 0;
        }
    }

    public void Reset()
    {
        _now = 0;
        _start = null;
        _releaseAt = null;
        _reversingRecovery = false;
        AgeSeconds = Duration;
    }
}
