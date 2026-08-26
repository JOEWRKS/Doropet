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

    public PointerReactionDecision Update(
        PointerSample pointer,
        PointD petCenter,
        TimeSpan simulationDelta,
        TimeSpan observationDelta,
        bool isDirectInteractionPending)
    {
        _curiousCooldown = DecrementCooldown(_curiousCooldown, simulationDelta);
        _startledCooldown = DecrementCooldown(_startledCooldown, simulationDelta);

        if (!pointer.IsAvailable)
        {
            ResetSpeedBaseline();
            return PointerReactionDecision.None;
        }

        if (isDirectInteractionPending)
        {
            ResetSpeedBaseline();
            UpdateNearLatch(Distance(pointer.Position, petCenter));
            return PointerReactionDecision.None;
        }

        var distance = Distance(pointer.Position, petCenter);
        var rawClosingSpeed = 0d;
        var isMovingAway = false;
        if (_previousDistance is { } previousDistance && observationDelta > TimeSpan.Zero)
        {
            rawClosingSpeed = (previousDistance - distance) / observationDelta.TotalSeconds;
            isMovingAway = rawClosingSpeed < 0;
            if (rawClosingSpeed > 0 && _previousPointerPosition is { } previousPointerPosition)
            {
                _lastApproachDirection = Normalize(pointer.Position - previousPointerPosition);
            }
        }

        var smoothingAlpha = SmoothingAlpha(observationDelta);
        _filteredClosingSpeed =
            smoothingAlpha * rawClosingSpeed +
            (1 - smoothingAlpha) * _filteredClosingSpeed;
        _previousDistance = distance;
        _previousPointerPosition = pointer.Position;

        var enteredNearZone = UpdateNearLatch(distance);

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

    private static double SmoothingAlpha(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        const double referenceAlpha = 0.35;
        const double referenceSeconds = 0.1;
        return 1 - Math.Pow(
            1 - referenceAlpha,
            elapsed.TotalSeconds / referenceSeconds);
    }

    private void ResetSpeedBaseline()
    {
        _previousDistance = null;
        _previousPointerPosition = null;
        _lastApproachDirection = null;
        _filteredClosingSpeed = 0;
    }

    private bool UpdateNearLatch(double distance)
    {
        if (!_nearLatched && distance <= _tuning.NearEnterDistance)
        {
            _nearLatched = true;
            return true;
        }

        if (_nearLatched && distance >= _tuning.NearExitDistance)
        {
            _nearLatched = false;
        }

        return false;
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
