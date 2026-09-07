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
    private double _releaseCheekPullDips;
    private CheekCarrySession? _cheekCarry;
    internal bool HasCheekCarry => _cheekCarry is not null;
    private bool _distanceDrivenBody;
    private SizeD _headDragThreshold;
    private readonly BodyPullSession _bodyPull = new();
    private readonly HeadSwingSession _headSwing = new();
    private PointD _headLandingOrigin;
    private double _headLandingDistance;

    internal DirectInteractionSnapshot Current { get; private set; } = DirectInteractionSnapshot.None;

    internal void BeginCheekPull(CheekPullCapture capture, PointD pointerPosition)
    {
        if (Current.Target != DirectInteractionTarget.None || !HeadPullDistance.IsFinite(pointerPosition)) return;
        Begin(DirectInteractionTarget.RightCheek, pointerPosition, 0);
        _releaseCheekPullDips = 0;
        Current = Current with { CheekPull = new(capture, 0) };
    }

    internal void BeginCheekCarry(CheekPullCapture capture, PointD windowPosition, PointD pointerPosition)
    {
        if (Current.Target != DirectInteractionTarget.None || !HeadPullDistance.IsFinite(windowPosition) ||
            !HeadPullDistance.IsFinite(pointerPosition)) return;
        BeginCheekPull(capture, pointerPosition);
        _cheekCarry = new(capture, windowPosition, pointerPosition);
    }

    internal PointD AdvanceCheekCarry(TimeSpan delta, PointerSample pointer, bool down, RectD workArea, SizeD petSize)
    {
        var session = _cheekCarry ?? throw new InvalidOperationException("No cheek carry session.");
        var cheek = Current.CheekPull!;
        if (!down || Current.Phase == DirectInteractionPhase.CheekRelease)
        {
            // Button-up must use the last displayed shape and position, not a new
            // pointer sample or one more follow step. Cancel may clear the session.
            AdvanceCapturedCheek(cheek, delta, Current.PointerPosition, false);
            return session.WindowPosition;
        }
        session.Advance(delta, pointer, workArea, petSize);
        var amount = session.PullDips;
        Current = Current with
        {
            CheekPull = cheek with { PullDips = amount }, Strength = Math.Abs(amount) / MaximumCheekPull,
            Phase = amount > 0 ? DirectInteractionPhase.CheekPull : DirectInteractionPhase.CheekPress,
            PointerPosition = pointer.IsAvailable && HeadPullDistance.IsFinite(pointer.Position) ? pointer.Position : Current.PointerPosition
        };
        return session.WindowPosition;
    }

    internal void BeginBodyPull(BodyPullCapture capture, PointD windowPosition, PointD pointerPosition)
    {
        if (Current.Target != DirectInteractionTarget.None || !_bodyPull.Begin(capture, windowPosition, pointerPosition)) return;
        Current = new(DirectInteractionTarget.FiveRegionBody, DirectInteractionPhase.BodyLocalPull, pointerPosition, pointerPosition, 0, 0, true, _bodyPull.Current);
    }

    internal PointD AdvanceBodyPull(TimeSpan delta, PointerSample pointer, bool primaryButtonDown, RectD workArea, SizeD petSize)
    {
        if (pointer.IsAvailable) _bodyPull.Move(pointer.Position);
        if (!primaryButtonDown) _bodyPull.Release();
        _bodyPull.Tick(delta.TotalMilliseconds);
        _bodyPull.Reconcile(workArea.ClampTopLeft(_bodyPull.Current.WindowPosition, petSize));
        var body = _bodyPull.Current;
        Current = body.Phase == BodyPullPhase.Idle ? DirectInteractionSnapshot.None : Current with
        {
            Phase = body.Phase == BodyPullPhase.Settling ? DirectInteractionPhase.BodyLocalSettle : DirectInteractionPhase.BodyLocalPull,
            PointerPosition = pointer.IsAvailable ? pointer.Position : Current.PointerPosition,
            RequiresCapture = body.RequiresCapture,
            BodyPull = body
        };
        return body.WindowPosition;
    }

    internal void Begin(DirectInteractionTarget target, PointD pointerPosition, double outwardSign)
    {
        if (Current.Target != DirectInteractionTarget.None)
        {
            return;
        }

        _outwardSign = outwardSign;
        _phaseElapsedMilliseconds = 0;
        _releaseStrength = 0;
        _distanceDrivenBody = false;
        _headSwing.Reset();

        Current = target switch
        {
            DirectInteractionTarget.Body => new(target, DirectInteractionPhase.BodyPending, pointerPosition, pointerPosition, 0, 0, false),
            DirectInteractionTarget.LeftCheek or DirectInteractionTarget.RightCheek => new(target, DirectInteractionPhase.CheekPress, pointerPosition, pointerPosition, 0, 0, true),
            _ => DirectInteractionSnapshot.None
        };
    }

    internal void BeginDistanceBody(PointD pointerPosition, SizeD dragThreshold)
    {
        if (Current.Target != DirectInteractionTarget.None || !HeadPullDistance.IsFinite(pointerPosition)) return;
        Begin(DirectInteractionTarget.Body, pointerPosition, 0);
        _distanceDrivenBody = true;
        _headDragThreshold = dragThreshold;
    }

    internal DirectInteractionSnapshot BeginHeadLanding(PointD position, RectD workArea, SizeD petSize)
    {
        if (!_distanceDrivenBody || Current.Phase != DirectInteractionPhase.BodyDragSettle ||
            Current.HeadLanding is not null || _phaseElapsedMilliseconds != 0 || _releaseStrength <= 0)
            return Current;

        _headLandingOrigin = workArea.ClampTopLeft(position, petSize);
        var target = workArea.ClampTopLeft(_headLandingOrigin + new PointD(0, HeadLandingMotion.MaximumDrop * _releaseStrength), petSize);
        _headLandingDistance = Math.Max(0, target.Y - _headLandingOrigin.Y);
        Current = Current with { HeadLanding = HeadLandingMotion.Sample(_headLandingOrigin, _headLandingDistance,
            _releaseStrength, 0, workArea, petSize) };
        return Current;
    }

    internal PointD? AdvanceHeadLanding(TimeSpan delta, RectD workArea, SizeD petSize)
    {
        if (Current.HeadLanding is null) return null;
        var landing = HeadLandingMotion.Sample(_headLandingOrigin, _headLandingDistance, _releaseStrength,
            _phaseElapsedMilliseconds + Math.Max(0, delta.TotalMilliseconds), workArea, petSize);
        Current = Current with { HeadLanding = landing };
        return landing.WindowPosition;
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

        if (Current.Target == DirectInteractionTarget.FiveRegionBody || HasCheekCarry) return Current;

        var pointerAvailable = pointer.IsAvailable && (!_distanceDrivenBody ||
            HeadPullDistance.TryMeasure(Current.PressOrigin, pointer.Position, _headDragThreshold, out _));
        var pointerPosition = pointerAvailable ? pointer.Position : Current.PointerPosition;
        if (Current.Target != DirectInteractionTarget.Body)
            return AdvanceCheek(delta, pointerPosition, primaryButtonDown);

        AdvanceBody(delta, pointerPosition, primaryButtonDown, previousState, currentState);
        if (_distanceDrivenBody && Current.Target == DirectInteractionTarget.Body)
            Current = Current with { HeadSwingDegrees = _headSwing.Advance(delta, pointer.ScreenPixelPosition ?? pointerPosition,
                pointerAvailable, Current.Phase, Current.ReleaseProgress) };
        return Current;
    }

    internal void Cancel()
    {
        _bodyPull.Cancel();
        _headSwing.Reset();
        _outwardSign = 0;
        _phaseElapsedMilliseconds = 0;
        _releaseStrength = 0;
        _distanceDrivenBody = false;
        Current = DirectInteractionSnapshot.None;
        _releaseCheekPullDips = 0;
        _cheekCarry = null;
        _headLandingOrigin = default;
        _headLandingDistance = 0;
    }

    private DirectInteractionSnapshot AdvanceCheek(TimeSpan delta, PointD pointerPosition, bool primaryButtonDown)
    {
        if (Current.CheekPull is { } cheek) return AdvanceCapturedCheek(cheek, delta, pointerPosition, primaryButtonDown);
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

    private DirectInteractionSnapshot AdvanceCapturedCheek(CheekPullSnapshot cheek, TimeSpan delta, PointD pointer, bool down)
    {
        if (!down && Current.Phase != DirectInteractionPhase.CheekRelease)
        {
            _phaseElapsedMilliseconds = 0; _releaseCheekPullDips = cheek.PullDips;
            Current = Current with { Phase = DirectInteractionPhase.CheekRelease, ReleaseProgress = 0, RequiresCapture = false };
            return Current;
        }
        if (Current.Phase == DirectInteractionPhase.CheekRelease)
        {
            _phaseElapsedMilliseconds += Math.Max(0, delta.TotalMilliseconds);
            var progress = Math.Clamp(_phaseElapsedMilliseconds / CheekReleaseDuration.TotalMilliseconds, 0, 1);
            if (progress >= 1) { Cancel(); return Current; }
            var pull = _releaseCheekPullDips * (1 - progress * progress * (3 - 2 * progress));
            Current = Current with { CheekPull = cheek with { PullDips = pull }, Strength = Math.Abs(pull) / MaximumCheekPull,
                ReleaseProgress = progress, RequiresCapture = false };
            return Current;
        }
        if (!HeadPullDistance.IsFinite(pointer)) return Current;
        var projected = cheek.Capture.Measure(pointer - Current.PressOrigin);
        if (!double.IsFinite(projected)) return Current;
        var amount = Math.Clamp(projected, -10, MaximumCheekPull);
        Current = Current with { CheekPull = cheek with { PullDips = amount }, PointerPosition = pointer,
            Strength = Math.Abs(amount) / MaximumCheekPull, Phase = amount > 0 ? DirectInteractionPhase.CheekPull : DirectInteractionPhase.CheekPress,
            ReleaseProgress = 0, RequiresCapture = true };
        return Current;
    }

    private DirectInteractionSnapshot AdvanceBody(
        TimeSpan delta,
        PointD pointerPosition,
        bool primaryButtonDown,
        PetState previousState,
        PetState currentState)
    {
        if (_distanceDrivenBody)
            return AdvanceDistanceBody(delta, pointerPosition, primaryButtonDown, currentState);

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

    private DirectInteractionSnapshot AdvanceDistanceBody(TimeSpan delta, PointD pointerPosition,
        bool primaryButtonDown, PetState currentState)
    {
        if (Current.Phase == DirectInteractionPhase.BodyDragSettle)
        {
            _phaseElapsedMilliseconds += Math.Max(0, delta.TotalMilliseconds);
            var progress = Math.Clamp(_phaseElapsedMilliseconds / DragSettleDuration.TotalMilliseconds, 0, 1);
            var duration = Current.HeadLanding is null ? DragSettleDuration.TotalMilliseconds : HeadLandingMotion.DurationMilliseconds;
            if (_phaseElapsedMilliseconds >= duration) { Cancel(); return Current; }
            var smoothstep = progress * progress * (3 - 2 * progress);
            Current = Current with
            {
                ReleaseProgress = progress,
                Strength = Current.IsPartialDragSettle ? _releaseStrength * (1 - smoothstep) : 1,
                RequiresCapture = false
            };
            return Current;
        }

        if (!primaryButtonDown || (Current.Phase != DirectInteractionPhase.BodyPending && currentState != PetState.Dragged))
        {
            if (Current.Phase == DirectInteractionPhase.BodyPending) { Cancel(); return Current; }
            _phaseElapsedMilliseconds = 0;
            _releaseStrength = Current.Strength;
            Current = Current with
            {
                Phase = DirectInteractionPhase.BodyDragSettle,
                IsPartialDragSettle = Current.Phase != DirectInteractionPhase.BodyDragHold,
                ReleaseProgress = 0,
                RequiresCapture = false
            };
            return Current;
        }

        if (currentState != PetState.Dragged)
        {
            Current = Current with { PointerPosition = pointerPosition };
            return Current;
        }

        HeadPullDistance.TryMeasure(Current.PressOrigin, pointerPosition, _headDragThreshold, out var pull);
        var carry = Current.Phase == DirectInteractionPhase.BodyDragHold || pull.Distance >= HeadPullDistance.FullExtension;
        Current = Current with
        {
            Phase = carry ? DirectInteractionPhase.BodyDragHold : DirectInteractionPhase.BodyDragEntry,
            PointerPosition = pointerPosition,
            Strength = carry ? 1 : pull.Strength,
            RequiresCapture = true
        };
        return Current;
    }
}
