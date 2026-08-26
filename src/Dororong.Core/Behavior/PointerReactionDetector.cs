using Dororong.Core.Geometry;

namespace Dororong.Core.Behavior;

internal sealed class PointerReactionDetector
{
    private readonly BehaviorTuning _tuning;
    private double? _previousDistance;
    private double _filteredClosingSpeed;
    private bool _nearLatched;
    private TimeSpan _curiousCooldown;
    private TimeSpan _startledCooldown;

    public PointerReactionDetector(BehaviorTuning tuning)
    {
        _tuning = tuning;
    }

    public PointerReaction Update(PointerSample pointer, PointD petCenter, TimeSpan delta, bool isDirectInteractionPending)
    {
        _curiousCooldown = DecrementCooldown(_curiousCooldown, delta);
        _startledCooldown = DecrementCooldown(_startledCooldown, delta);

        if (!pointer.IsAvailable)
        {
            ResetSpeedBaseline();
            return PointerReaction.None;
        }

        if (isDirectInteractionPending)
        {
            return PointerReaction.None;
        }

        var distance = Distance(pointer.Position, petCenter);
        var rawClosingSpeed = 0d;
        var isMovingAway = false;
        if (_previousDistance is { } previousDistance && delta > TimeSpan.Zero)
        {
            rawClosingSpeed = (previousDistance - distance) / delta.TotalSeconds;
            isMovingAway = rawClosingSpeed < 0;
        }

        _filteredClosingSpeed = 0.35 * rawClosingSpeed + 0.65 * _filteredClosingSpeed;
        _previousDistance = distance;

        var enteredNearZone = false;
        if (!_nearLatched && distance <= _tuning.NearEnterDistance)
        {
            _nearLatched = true;
            enteredNearZone = true;
        }
        else if (_nearLatched && distance >= _tuning.NearExitDistance)
        {
            _nearLatched = false;
        }

        if (isMovingAway)
        {
            return PointerReaction.None;
        }

        if (distance <= _tuning.StartleReactionDistance &&
            _filteredClosingSpeed >= _tuning.StartleClosingSpeed &&
            _startledCooldown == TimeSpan.Zero)
        {
            _startledCooldown = _tuning.StartledCooldown;
            return PointerReaction.Startled;
        }

        if (enteredNearZone && _curiousCooldown == TimeSpan.Zero)
        {
            _curiousCooldown = _tuning.CuriousCooldown;
            return PointerReaction.Curious;
        }

        return PointerReaction.None;
    }

    private static TimeSpan DecrementCooldown(TimeSpan cooldown, TimeSpan delta) =>
        cooldown > delta ? cooldown - delta : TimeSpan.Zero;

    private void ResetSpeedBaseline()
    {
        _previousDistance = null;
        _filteredClosingSpeed = 0;
    }

    private static double Distance(PointD first, PointD second)
    {
        var deltaX = first.X - second.X;
        var deltaY = first.Y - second.Y;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }
}

internal enum PointerReaction
{
    None,
    Curious,
    Startled
}
