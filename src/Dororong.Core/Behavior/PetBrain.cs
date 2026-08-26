using Dororong.Core.Geometry;

namespace Dororong.Core.Behavior;

public sealed class PetBrain
{
    private readonly BehaviorTuning _tuning;
    private readonly IRandomSource _random;
    private PetState _state;
    private PointD _position;
    private FacingDirection _facing;
    private PointD _heading;
    private TimeSpan _stateElapsed;
    private TimeSpan _stateDuration;

    public PetBrain(BehaviorTuning tuning, IRandomSource random, PointD initialPosition)
    {
        _tuning = tuning;
        _random = random;
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

        if (_state == PetState.Walk)
        {
            Move(delta, input.WorkArea, input.PetSize);
        }

        _stateElapsed += delta;
        if (_stateElapsed >= _stateDuration)
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
            else if (_state == PetState.Walk)
            {
                StartIdle();
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

    private void Move(TimeSpan delta, RectD workArea, SizeD petSize)
    {
        var distance = _tuning.WalkSpeed * delta.TotalSeconds;
        var moved = _position + new PointD(_heading.X * distance, _heading.Y * distance);
        var clamped = workArea.ClampTopLeft(moved, petSize);

        if (clamped.X != moved.X)
        {
            _heading = _heading with { X = -_heading.X };
        }

        if (clamped.Y != moved.Y)
        {
            _heading = _heading with { Y = -_heading.Y };
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

    private double FinitePhase(TimeSpan duration) =>
        Math.Clamp(_stateElapsed.Ticks / (double)duration.Ticks, 0, 1);
}
