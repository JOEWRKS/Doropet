using Dororong.Core.Geometry;

namespace Dororong.Core.Behavior;

internal sealed class PointerReactionDetector
{
    private readonly BehaviorTuning _tuning;
    private double? _previousDistance;
    private PointD? _previousPointerPosition;
    private PointD? _lastApproachDirection;
    private double _filteredClosingSpeed;
    private bool _nearLatched;
    private TimeSpan _curiousCooldown;
    private TimeSpan _startledCooldown;

    public PointerReactionDetector(BehaviorTuning tuning)
    {
        _tuning = tuning;
    }

    public PointerReactionDecision Update(PointerSample pointer, PointD petCenter, TimeSpan delta, bool isDirectInteractionPending)
    {
        _curiousCooldown = DecrementCooldown(_curiousCooldown, delta);
        _startledCooldown = DecrementCooldown(_startledCooldown, delta);

        if (!pointer.IsAvailable)
        {
            ResetSpeedBaseline();
            return PointerReactionDecision.None;
        }

        if (isDirectInteractionPending)
        {
            ResetSpeedBaseline();
            return PointerReactionDecision.None;
        }

        var distance = Distance(pointer.Position, petCenter);
        var rawClosingSpeed = 0d;
        var isMovingAway = false;
        if (_previousDistance is { } previousDistance && delta > TimeSpan.Zero)
        {
            rawClosingSpeed = (previousDistance - distance) / delta.TotalSeconds;
            isMovingAway = rawClosingSpeed < 0;
            if (rawClosingSpeed > 0 && _previousPointerPosition is { } previousPointerPosition)
            {
                _lastApproachDirection = Normalize(pointer.Position - previousPointerPosition);
            }
        }

        _filteredClosingSpeed = 0.35 * rawClosingSpeed + 0.65 * _filteredClosingSpeed;
        _previousDistance = distance;
        _previousPointerPosition = pointer.Position;

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
            return PointerReactionDecision.None with { EnteredNearZone = enteredNearZone };
        }

        if (distance <= _tuning.StartleReactionDistance &&
            _filteredClosingSpeed >= _tuning.StartleClosingSpeed &&
            _startledCooldown == TimeSpan.Zero)
        {
            _startledCooldown = _tuning.StartledCooldown;
            return new PointerReactionDecision(PointerReaction.Startled, _lastApproachDirection, enteredNearZone);
        }

        if (enteredNearZone && _curiousCooldown == TimeSpan.Zero)
        {
            _curiousCooldown = _tuning.CuriousCooldown;
            return new PointerReactionDecision(PointerReaction.Curious, null, EnteredNearZone: true);
        }

        return PointerReactionDecision.None with { EnteredNearZone = enteredNearZone };
    }

    private static TimeSpan DecrementCooldown(TimeSpan cooldown, TimeSpan delta) =>
        cooldown > delta ? cooldown - delta : TimeSpan.Zero;

    private void ResetSpeedBaseline()
    {
        _previousDistance = null;
        _previousPointerPosition = null;
        _lastApproachDirection = null;
        _filteredClosingSpeed = 0;
    }

    private static PointD? Normalize(PointD vector)
    {
        var length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
        return length == 0 ? null : new PointD(vector.X / length, vector.Y / length);
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

internal readonly record struct PointerReactionDecision(
    PointerReaction Reaction,
    PointD? ApproachDirection,
    bool EnteredNearZone)
{
    public static PointerReactionDecision None => new(PointerReaction.None, null, EnteredNearZone: false);
}
