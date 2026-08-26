using Dororong.Core.Geometry;

namespace Dororong.Core.Behavior;

public sealed class PetBrain
{
    private readonly BehaviorTuning _tuning;
    private readonly IRandomSource _random;
    private readonly PointerReactionDetector _pointerReactionDetector;
    private PetState _state;
    private PointD _position;
    private FacingDirection _facing;
    private PointD _heading;
    private TimeSpan _stateElapsed;
    private TimeSpan _stateDuration;
    private PointD _startledRetreatDirection;

    public PetBrain(BehaviorTuning tuning, IRandomSource random, PointD initialPosition)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(random);
        ValidateAutonomousDurationRange(tuning.IdleMin, tuning.IdleMax, nameof(tuning.IdleMin));
        ValidateAutonomousDurationRange(tuning.WalkMin, tuning.WalkMax, nameof(tuning.WalkMin));
        ValidatePositiveDuration(tuning.CuriousDuration, nameof(tuning.CuriousDuration));
        ValidatePositiveDuration(tuning.StartledDuration, nameof(tuning.StartledDuration));
        ValidatePositiveDuration(tuning.ClickReactionDuration, nameof(tuning.ClickReactionDuration));

        _tuning = tuning;
        _random = random;
        _pointerReactionDetector = new PointerReactionDetector(tuning);
        _state = PetState.Idle;
        _position = initialPosition;
        _facing = FacingDirection.Right;
        _heading = new PointD(1, 0);
        _stateDuration = SelectDuration(_tuning.IdleMin, _tuning.IdleMax);
    }

    public PetSnapshot Current => new(
        _state,
        _position,
        _facing,
        GetPhase(),
        IsDirectInteractionPending: false,
        GrabOffset: null);

    public PetSnapshot Update(PetInput input)
    {
        var delta = ClampDelta(input.Delta);
        if (delta == TimeSpan.Zero)
        {
            return Current;
        }

        NormalizePosition(input.WorkArea, input.PetSize);

        var petCenter = _position + new PointD(input.PetSize.Width / 2, input.PetSize.Height / 2);
        var reaction = _pointerReactionDetector.Update(
            input.Pointer,
            petCenter,
            delta,
            isDirectInteractionPending: input.BodyPressPosition.HasValue);
        if (reaction == PointerReaction.Startled && _state != PetState.Startled)
        {
            StartStartled(input.Pointer.Position, petCenter);
        }
        else if (reaction == PointerReaction.Curious && _state != PetState.Startled)
        {
            StartCurious(input.Pointer.Position, petCenter);
        }

        var remaining = delta;
        while (remaining > TimeSpan.Zero)
        {
            var untilStateBoundary = _stateDuration - _stateElapsed;
            var consumed = remaining < untilStateBoundary ? remaining : untilStateBoundary;

            if (_state == PetState.Walk)
            {
                Move(consumed, input.WorkArea, input.PetSize);
            }
            else if (_state == PetState.Startled)
            {
                Retreat(consumed, input.WorkArea, input.PetSize);
            }

            _stateElapsed += consumed;
            remaining -= consumed;

            if (_stateElapsed >= _stateDuration)
            {
                AdvanceAutonomousState();
            }
        }

        return Current;
    }

    private TimeSpan ClampDelta(TimeSpan delta)
    {
        if (delta <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return delta > _tuning.MaxDelta ? _tuning.MaxDelta : delta;
    }

    private void AdvanceAutonomousState()
    {
        if (_state == PetState.Idle)
        {
            if (_random.NextUnit() < _tuning.IdleToWalkProbability)
            {
                StartWalk();
            }
            else
            {
                StartIdle();
            }
        }
        else if (_state == PetState.Walk || _state == PetState.Curious || _state == PetState.Startled)
        {
            StartIdle();
        }
    }

    private void StartIdle()
    {
        _state = PetState.Idle;
        _stateElapsed = TimeSpan.Zero;
        _stateDuration = SelectDuration(_tuning.IdleMin, _tuning.IdleMax);
    }

    private void StartWalk()
    {
        var angle = _random.NextUnit() * Math.Tau;
        _heading = new PointD(Math.Cos(angle), Math.Sin(angle));
        _facing = _heading.X < 0 ? FacingDirection.Left : FacingDirection.Right;
        _state = PetState.Walk;
        _stateElapsed = TimeSpan.Zero;
        _stateDuration = SelectDuration(_tuning.WalkMin, _tuning.WalkMax);
    }

    private void StartCurious(PointD pointerPosition, PointD petCenter)
    {
        _facing = pointerPosition.X < petCenter.X ? FacingDirection.Left : FacingDirection.Right;
        _state = PetState.Curious;
        _stateElapsed = TimeSpan.Zero;
        _stateDuration = _tuning.CuriousDuration;
    }

    private void StartStartled(PointD pointerPosition, PointD petCenter)
    {
        var direction = petCenter - pointerPosition;
        var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        _startledRetreatDirection = length == 0
            ? new PointD(0, 0)
            : new PointD(direction.X / length, direction.Y / length);
        _facing = _startledRetreatDirection.X < 0 ? FacingDirection.Left : FacingDirection.Right;
        _state = PetState.Startled;
        _stateElapsed = TimeSpan.Zero;
        _stateDuration = _tuning.StartledDuration;
    }

    private void Move(TimeSpan delta, RectD workArea, SizeD petSize)
    {
        var distance = _tuning.WalkSpeed * delta.TotalSeconds;
        var moved = _position + new PointD(_heading.X * distance, _heading.Y * distance);
        _position = moved;
        NormalizePosition(workArea, petSize);
    }

    private void Retreat(TimeSpan delta, RectD workArea, SizeD petSize)
    {
        if (_tuning.StartledDuration <= TimeSpan.Zero)
        {
            return;
        }

        var distance = _tuning.StartleRetreatDistance * delta.TotalSeconds / _tuning.StartledDuration.TotalSeconds;
        _position += new PointD(_startledRetreatDirection.X * distance, _startledRetreatDirection.Y * distance);
        NormalizePosition(workArea, petSize);
    }

    private void NormalizePosition(RectD workArea, SizeD petSize)
    {
        var clamped = workArea.ClampTopLeft(_position, petSize);
        if (_state != PetState.Walk)
        {
            _position = clamped;
            return;
        }

        if (clamped.X != _position.X)
        {
            _heading = _heading with
            {
                X = clamped.X > _position.X ? Math.Abs(_heading.X) : -Math.Abs(_heading.X)
            };
        }

        if (clamped.Y != _position.Y)
        {
            _heading = _heading with
            {
                Y = clamped.Y > _position.Y ? Math.Abs(_heading.Y) : -Math.Abs(_heading.Y)
            };
        }

        _position = clamped;
        _facing = _heading.X < 0 ? FacingDirection.Left : FacingDirection.Right;
    }

    private TimeSpan SelectDuration(TimeSpan minimum, TimeSpan maximum)
    {
        if (minimum == maximum)
        {
            return minimum;
        }

        return minimum + TimeSpan.FromTicks((long)((maximum - minimum).Ticks * _random.NextUnit()));
    }

    private static void ValidateAutonomousDurationRange(TimeSpan minimum, TimeSpan maximum, string parameterName)
    {
        if (minimum <= TimeSpan.Zero || maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Autonomous state durations must be positive and ordered.");
        }
    }

    private static void ValidatePositiveDuration(TimeSpan duration, string parameterName)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Finite reaction durations must be positive.");
        }
    }

    private double GetPhase() => _state switch
    {
        PetState.Idle => RepeatingPhase(_stateElapsed, TimeSpan.FromSeconds(2)),
        PetState.Walk => RepeatingPhase(_stateElapsed, TimeSpan.FromSeconds(0.6)),
        PetState.Sleep => RepeatingPhase(_stateElapsed, TimeSpan.FromSeconds(2.4)),
        PetState.Dragged => 0,
        PetState.Curious => FinitePhase(_tuning.CuriousDuration),
        PetState.Startled => FinitePhase(_tuning.StartledDuration),
        PetState.ClickReaction => FinitePhase(_tuning.ClickReactionDuration),
        _ => 0
    };

    private static double RepeatingPhase(TimeSpan elapsed, TimeSpan period) =>
        elapsed.Ticks % period.Ticks / (double)period.Ticks;

    private double FinitePhase(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 0;
        }

        var phase = _stateElapsed.Ticks / (double)duration.Ticks;
        return Math.Clamp(phase, 0, Math.BitDecrement(1d));
    }
}
