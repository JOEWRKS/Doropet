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
    private PointD? _pressPosition;
    private PointD? _grabOffset;
    private TimeSpan _inactivity;

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
        IsDirectInteractionPending: _pressPosition.HasValue,
        GrabOffset: _grabOffset);

    public PetSnapshot Update(PetInput input)
    {
        var delta = ClampDelta(input.Delta);
        if (delta == TimeSpan.Zero)
        {
            return Current;
        }

        if (_state != PetState.Dragged)
        {
            NormalizePosition(input.WorkArea, input.PetSize);
        }

        var handledDirectInteraction = ProcessDirectInteraction(input);

        var petCenter = _position + new PointD(input.PetSize.Width / 2, input.PetSize.Height / 2);
        var reaction = _pointerReactionDetector.Update(
            input.Pointer,
            petCenter,
            simulationDelta: delta,
            observationDelta: input.Delta,
            isDirectInteractionPending: handledDirectInteraction || _pressPosition.HasValue || _state == PetState.Dragged);
        if (reaction.EnteredNearZone || reaction.Reaction == PointerReaction.Startled)
        {
            ResetInactivity();
        }

        if (reaction.Reaction == PointerReaction.Startled &&
            _state is not (PetState.Startled or PetState.ClickReaction or PetState.Dragged))
        {
            StartStartled(input.Pointer.Position, petCenter, reaction.ApproachDirection);
        }
        else if (reaction.Reaction == PointerReaction.Curious &&
                 _state is not (PetState.Startled or PetState.ClickReaction or PetState.Dragged))
        {
            StartCurious(input.Pointer.Position, petCenter);
        }

        if (_pressPosition.HasValue && _state != PetState.Dragged)
        {
            _inactivity += delta;
            return Current;
        }

        var remaining = delta;
        while (remaining > TimeSpan.Zero)
        {
            if (TryStartSleep())
            {
                continue;
            }

            var untilStateBoundary = _stateDuration - _stateElapsed;
            var untilSleepBoundary = CanEnterSleep()
                ? _tuning.SleepDelay - _inactivity
                : TimeSpan.MaxValue;
            var consumed = Min(remaining, untilStateBoundary, untilSleepBoundary);

            if (_state == PetState.Walk)
            {
                Move(consumed, input.WorkArea, input.PetSize);
            }
            else if (_state == PetState.Startled)
            {
                Retreat(consumed, input.WorkArea, input.PetSize);
            }

            _stateElapsed += consumed;
            _inactivity += consumed;
            remaining -= consumed;

            if (TryStartSleep())
            {
                continue;
            }

            if (_stateElapsed >= _stateDuration)
            {
                AdvanceAutonomousState();
                TryStartSleep();
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
        else if (_state is PetState.Walk or PetState.Curious or PetState.Startled or PetState.ClickReaction)
        {
            StartIdle();
        }
    }

    private bool ProcessDirectInteraction(PetInput input)
    {
        var handled = false;
        if (input.BodyPressPosition is { } pressPosition &&
            !_pressPosition.HasValue &&
            _state != PetState.Dragged)
        {
            _pressPosition = pressPosition;
            _grabOffset = pressPosition - _position;
            ResetInactivity();
            handled = true;
        }

        if (_state == PetState.Dragged)
        {
            handled = true;
            if (input.PrimaryButtonDown)
            {
                ResetInactivity();
                if (input.Pointer.IsAvailable && _grabOffset is { } dragOffset)
                {
                    _position = input.Pointer.Position - dragOffset;
                }
            }
            else
            {
                ResetInactivity();
                ClearDirectInteraction();
                StartIdle();
                NormalizePosition(input.WorkArea, input.PetSize);
            }

            return handled;
        }

        if (_pressPosition is not { } savedPressPosition)
        {
            return handled;
        }

        handled = true;
        var crossedDragThreshold = input.Pointer.IsAvailable &&
            (Math.Abs(input.Pointer.Position.X - savedPressPosition.X) >= input.DragThreshold.Width ||
             Math.Abs(input.Pointer.Position.Y - savedPressPosition.Y) >= input.DragThreshold.Height);

        if (input.PrimaryButtonDown)
        {
            if (crossedDragThreshold && _grabOffset is { } dragOffset)
            {
                ResetInactivity();
                _state = PetState.Dragged;
                _stateElapsed = TimeSpan.Zero;
                _stateDuration = TimeSpan.MaxValue;
                _position = input.Pointer.Position - dragOffset;
            }

            return handled;
        }

        ClearDirectInteraction();
        if (!input.Pointer.IsAvailable || crossedDragThreshold)
        {
            StartIdle();
        }
        else
        {
            ResetInactivity();
            StartClickReaction();
        }

        return handled;
    }

    private void ClearDirectInteraction()
    {
        _pressPosition = null;
        _grabOffset = null;
    }

    private void StartClickReaction()
    {
        _state = PetState.ClickReaction;
        _stateElapsed = TimeSpan.Zero;
        _stateDuration = _tuning.ClickReactionDuration;
    }

    private void StartSleep()
    {
        _state = PetState.Sleep;
        _stateElapsed = TimeSpan.Zero;
        _stateDuration = TimeSpan.MaxValue;
    }

    private bool TryStartSleep()
    {
        if (!CanEnterSleep() || _inactivity < _tuning.SleepDelay)
        {
            return false;
        }

        StartSleep();
        return true;
    }

    private bool CanEnterSleep() =>
        _state == PetState.Idle && !_pressPosition.HasValue;

    private void ResetInactivity()
    {
        _inactivity = TimeSpan.Zero;
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

    private void StartStartled(PointD pointerPosition, PointD petCenter, PointD? approachDirection)
    {
        var direction = petCenter - pointerPosition;
        var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        _startledRetreatDirection = length == 0
            ? approachDirection ?? new PointD(0, 0)
            : new PointD(direction.X / length, direction.Y / length);
        _facing = _startledRetreatDirection.X < 0 ? FacingDirection.Left : FacingDirection.Right;
        _state = PetState.Startled;
        _stateElapsed = TimeSpan.Zero;
        _stateDuration = _tuning.StartledDuration;
    }

    private void Move(TimeSpan delta, RectD workArea, SizeD petSize)
    {
        var distance = _tuning.WalkSpeed * delta.TotalSeconds;
        var maximumX = Math.Max(workArea.X, workArea.Right - petSize.Width);
        var maximumY = Math.Max(workArea.Y, workArea.Bottom - petSize.Height);
        var x = ReflectAxis(_position.X, _heading.X, distance, workArea.X, maximumX);
        var y = ReflectAxis(_position.Y, _heading.Y, distance, workArea.Y, maximumY);

        _position = new PointD(x.Position, y.Position);
        _heading = new PointD(x.Heading, y.Heading);
        _facing = _heading.X < 0 ? FacingDirection.Left : FacingDirection.Right;
    }

    private static (double Position, double Heading) ReflectAxis(
        double position,
        double heading,
        double distance,
        double minimum,
        double maximum)
    {
        var span = maximum - minimum;
        if (span <= 0)
        {
            return (minimum, heading);
        }

        if (heading == 0 || distance == 0)
        {
            return (Math.Clamp(position, minimum, maximum), heading);
        }

        var period = span * 2;
        var unfolded = (position - minimum) + (heading * distance);
        var phase = unfolded % period;
        if (phase < 0)
        {
            phase += period;
        }

        if (phase == 0)
        {
            return (minimum, Math.Abs(heading));
        }

        if (phase == span)
        {
            return (maximum, -Math.Abs(heading));
        }

        return phase < span
            ? (minimum + phase, heading)
            : (minimum + period - phase, -heading);
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

    private static TimeSpan Min(TimeSpan first, TimeSpan second, TimeSpan third)
    {
        var minimum = first < second ? first : second;
        return minimum < third ? minimum : third;
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
