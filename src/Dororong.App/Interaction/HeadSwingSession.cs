using Dororong.Core.Geometry;

namespace Dororong.App.Interaction;

// A speed-driven, damped hanging pose; no changes to drag ownership or position.
internal sealed class HeadSwingSession
{
    internal const double MaximumAngle = 88;
    private const double DegreesPerPixelPerSecond = .08;
    private const double VelocityFilterSeconds = .06;
    private const double NaturalFrequency = 11;
    private const double DampingRatio = .27;
    private PointD? _previousPointer;
    private bool _holding;
    private bool _releasing;
    private double _filteredSpeed;
    private double _angle;
    private double _angularSpeed;
    private double _releaseAngle;

    internal double Advance(TimeSpan delta, PointD pointer, bool available,
        DirectInteractionPhase phase, double releaseProgress)
    {
        available = available && double.IsFinite(pointer.X) && double.IsFinite(pointer.Y);
        if (phase == DirectInteractionPhase.BodyDragSettle)
        {
            if (!_releasing) { _releaseAngle = _angle; _releasing = true; }
            var p = Math.Clamp(releaseProgress, 0, 1);
            return _releaseAngle * (1 - p * p * (3 - 2 * p));
        }
        if (phase != DirectInteractionPhase.BodyDragHold) { Reset(); return 0; }
        if (!_holding)
        {
            _holding = true;
            _previousPointer = available ? pointer : null;
            return 0; // Don't turn the initial extension into a velocity impulse.
        }

        var seconds = delta.TotalSeconds;
        if (seconds <= 0 || seconds > .25)
        {
            // A paused/invalid clock does not represent a high-speed mouse fling.
            _previousPointer = available ? pointer : null;
            _filteredSpeed = 0;
            if (seconds > .25) { _angle = 0; _angularSpeed = 0; }
            return _angle;
        }
        // Screen-horizontal px/s controls left/right lag; pure vertical travel
        // does not invent a sideways direction. No pet-relative distance is used.
        var speed = available && _previousPointer is { } previous
            ? Math.Clamp((pointer.X - previous.X) / seconds, -4000, 4000) : 0;
        _previousPointer = available ? pointer : null;

        // Small bounded steps make elapsed-time behavior consistent across frame rates.
        var steps = (int)Math.Ceiling(seconds * 240);
        var dt = seconds / steps;
        var filter = 1 - Math.Exp(-dt / VelocityFilterSeconds);
        for (var i = 0; i < steps; i++)
        {
            _filteredSpeed += (speed - _filteredSpeed) * filter;
            var target = Math.Abs(_filteredSpeed) < 8 ? 0
                : Math.Clamp(_filteredSpeed * DegreesPerPixelPerSecond, -MaximumAngle, MaximumAngle);
            _angularSpeed += (NaturalFrequency * NaturalFrequency * (target - _angle)
                - 2 * DampingRatio * NaturalFrequency * _angularSpeed) * dt;
            _angle += _angularSpeed * dt;
            if (Math.Abs(_angle) > MaximumAngle)
            {
                _angle = Math.Clamp(_angle, -MaximumAngle, MaximumAngle);
                if (_angle * _angularSpeed > 0) _angularSpeed = 0;
            }
        }
        if (Math.Abs(_filteredSpeed) < 8 && Math.Abs(_angle) < .025 && Math.Abs(_angularSpeed) < .25)
        { _angle = 0; _angularSpeed = 0; }
        return _angle;
    }

    internal void Reset()
    {
        _previousPointer = null;
        _holding = false;
        _releasing = false;
        _filteredSpeed = _angle = _angularSpeed = _releaseAngle = 0;
    }
}
