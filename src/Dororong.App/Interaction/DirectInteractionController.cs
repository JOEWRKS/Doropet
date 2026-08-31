using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Interaction;

internal sealed class DirectInteractionController
{
    internal const double MaximumCheekPull = 20;
    internal static readonly TimeSpan CheekReleaseDuration = TimeSpan.FromMilliseconds(220);
    internal static readonly TimeSpan DragEntryDuration = TimeSpan.FromMilliseconds(140);
    internal static readonly TimeSpan DragSettleDuration = TimeSpan.FromMilliseconds(180);

    private double _outwardSign;
    private double _phaseElapsedMilliseconds;
    private double _releaseStrength;

    internal DirectInteractionSnapshot Current { get; private set; } = DirectInteractionSnapshot.None;

    internal void Begin(DirectInteractionTarget target, PointD pointerPosition, double outwardSign)
    {
        if (Current.Target != DirectInteractionTarget.None)
        {
            return;
        }

        _outwardSign = outwardSign;
        _phaseElapsedMilliseconds = 0;
        _releaseStrength = 0;

        Current = target switch
        {
            DirectInteractionTarget.Body => new(target, DirectInteractionPhase.BodyPending, pointerPosition, pointerPosition, 0, 0, false),
            DirectInteractionTarget.LeftCheek or DirectInteractionTarget.RightCheek => new(target, DirectInteractionPhase.CheekPress, pointerPosition, pointerPosition, 0, 0, true),
            _ => DirectInteractionSnapshot.None
        };
    }

    internal DirectInteractionSnapshot Advance(
        TimeSpan delta,
        PointerSample pointer,
        bool primaryButtonDown,
        PetState previousState,
        PetState currentState)
    {
        if (Current.Target == DirectInteractionTarget.None)
        {
            return Current;
        }

        var pointerPosition = pointer.IsAvailable ? pointer.Position : Current.PointerPosition;
        return Current.Target == DirectInteractionTarget.Body
            ? AdvanceBody(delta, pointerPosition, primaryButtonDown, previousState, currentState)
            : AdvanceCheek(delta, pointerPosition, primaryButtonDown);
    }

    internal void Cancel()
    {
        _outwardSign = 0;
        _phaseElapsedMilliseconds = 0;
        _releaseStrength = 0;
        Current = DirectInteractionSnapshot.None;
    }

    private DirectInteractionSnapshot AdvanceCheek(TimeSpan delta, PointD pointerPosition, bool primaryButtonDown)
    {
        if (!primaryButtonDown && Current.Phase != DirectInteractionPhase.CheekRelease)
        {
            _phaseElapsedMilliseconds = 0;
            _releaseStrength = Current.Strength;
            Current = Current with
            {
                Phase = DirectInteractionPhase.CheekRelease,
                PointerPosition = pointerPosition,
                ReleaseProgress = 0,
                RequiresCapture = false
            };
            return Current;
        }

        if (Current.Phase == DirectInteractionPhase.CheekRelease)
        {
            _phaseElapsedMilliseconds += delta.TotalMilliseconds;
            var progress = Math.Clamp(_phaseElapsedMilliseconds / CheekReleaseDuration.TotalMilliseconds, 0, 1);
            if (progress >= 1)
            {
                Cancel();
                return Current;
            }

            var smoothstep = progress * progress * (3 - (2 * progress));
            Current = Current with
            {
                ReleaseProgress = progress,
                Strength = _releaseStrength * (1 - smoothstep),
                RequiresCapture = false
            };
            return Current;
        }

        var deltaX = pointerPosition.X - Current.PressOrigin.X;
        var deltaY = pointerPosition.Y - Current.PressOrigin.Y;
        var strength = Math.Clamp((Math.Max(0, deltaX * _outwardSign) + (0.25 * Math.Abs(deltaY))) / MaximumCheekPull, 0, 1);
        Current = Current with
        {
            Phase = deltaX * _outwardSign > 0 ? DirectInteractionPhase.CheekPull : DirectInteractionPhase.CheekPress,
            PointerPosition = pointerPosition,
            Strength = strength,
            ReleaseProgress = 0,
            RequiresCapture = true
        };
        return Current;
    }

    private DirectInteractionSnapshot AdvanceBody(
        TimeSpan delta,
        PointD pointerPosition,
        bool primaryButtonDown,
        PetState previousState,
        PetState currentState)
    {
        if (Current.Phase == DirectInteractionPhase.BodyPending && currentState == PetState.Dragged)
        {
            _phaseElapsedMilliseconds = 0;
            Current = Current with
            {
                Phase = DirectInteractionPhase.BodyDragEntry,
                PointerPosition = pointerPosition,
                Strength = 0,
                ReleaseProgress = 0,
                RequiresCapture = primaryButtonDown
            };
            return Current;
        }

        if (Current.Phase is DirectInteractionPhase.BodyDragEntry or DirectInteractionPhase.BodyDragHold)
        {
            if (currentState != PetState.Dragged)
            {
                _phaseElapsedMilliseconds = 0;
                Current = Current with
                {
                    Phase = DirectInteractionPhase.BodyDragSettle,
                    PointerPosition = pointerPosition,
                    Strength = 1,
                    ReleaseProgress = 0,
                    RequiresCapture = false
                };
                return Current;
            }

            if (Current.Phase == DirectInteractionPhase.BodyDragEntry)
            {
                _phaseElapsedMilliseconds += delta.TotalMilliseconds;
                var progress = Math.Clamp(
                    _phaseElapsedMilliseconds / DragEntryDuration.TotalMilliseconds,
                    0,
                    1);
                if (progress >= 1)
                {
                    _phaseElapsedMilliseconds = 0;
                    Current = Current with
                    {
                        Phase = DirectInteractionPhase.BodyDragHold,
                        PointerPosition = pointerPosition,
                        Strength = 1,
                        RequiresCapture = primaryButtonDown
                    };
                    return Current;
                }

                Current = Current with
                {
                    PointerPosition = pointerPosition,
                    Strength = progress,
                    RequiresCapture = primaryButtonDown
                };
                return Current;
            }

            Current = Current with
            {
                PointerPosition = pointerPosition,
                Strength = 1,
                RequiresCapture = primaryButtonDown
            };
            return Current;
        }

        if (Current.Phase == DirectInteractionPhase.BodyDragSettle)
        {
            _phaseElapsedMilliseconds += delta.TotalMilliseconds;
            var progress = Math.Clamp(
                _phaseElapsedMilliseconds / DragSettleDuration.TotalMilliseconds,
                0,
                1);
            if (progress >= 1)
            {
                Cancel();
                return Current;
            }

            Current = Current with
            {
                PointerPosition = pointerPosition,
                ReleaseProgress = progress,
                RequiresCapture = false
            };
            return Current;
        }

        if (!primaryButtonDown)
        {
            Cancel();
            return Current;
        }

        Current = Current with { PointerPosition = pointerPosition, RequiresCapture = false };
        return Current;
    }
}
