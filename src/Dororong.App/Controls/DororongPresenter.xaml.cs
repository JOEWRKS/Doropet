using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

internal sealed class DirectInteractionPressEventArgs : EventArgs
{
    internal DirectInteractionPressEventArgs(
        DirectInteractionTarget target,
        PointD windowLocalPosition,
        PointD framePosition,
        double outwardSign)
        : this(target, windowLocalPosition, framePosition, outwardSign, null)
    {
    }

    internal DirectInteractionPressEventArgs(
        DirectInteractionTarget target,
        PointD windowLocalPosition,
        PointD framePosition,
        double outwardSign,
        BodyPullCapture? bodyCapture)
    {
        Target = target;
        WindowLocalPosition = windowLocalPosition;
        FramePosition = framePosition;
        OutwardSign = outwardSign;
        BodyCapture = bodyCapture;
    }

    internal DirectInteractionTarget Target { get; }
    internal PointD WindowLocalPosition { get; }
    internal PointD FramePosition { get; }
    internal double OutwardSign { get; }
    internal BodyPullCapture? BodyCapture { get; }
    internal CheekPullCapture? CheekCapture { get; init; }
}

public partial class DororongPresenter : UserControl
{
    private const double FrameMaximumCoordinate = 95;
    private const double BodyClickWakeFirstSegmentMilliseconds = 45;
    private const double BodyClickWakeDurationMilliseconds = 90;
    private const double PresentationTickMilliseconds = 16;
    // The centered logical presenter may rotate into the transparent outer grid.
    // Standalone previews retain their own bounds and the safe common-angle cap.
    private FrameworkElement SwingViewport => Parent as FrameworkElement ?? this;

    private static readonly BitmapImage CanonicalFrame = LoadFrame("dororong-canonical.png");
    private static readonly BitmapImage BlinkSquintFrame = LoadFrame("dororong-blink-squint.png");
    private static readonly BitmapImage ClosedEyesFrame = LoadFrame("dororong-closed-eyes.png");
    private static readonly BitmapImage SleepFrame = LoadFrame("dororong-sleep.png");
    private static readonly BitmapImage SleepCrouchClosedFrame = LoadFrame("dororong-sleep-crouch-closed.png");
    private static readonly BitmapImage SleepCrouchSquintFrame = LoadFrame("dororong-sleep-crouch-squint.png");
    private static readonly BitmapImage SleepTuckClosedFrame = LoadFrame("dororong-sleep-tuck-closed.png");
    private static readonly BitmapImage SleepTuckSquintFrame = LoadFrame("dororong-sleep-tuck-squint.png");
    private static readonly PremultipliedFrame CanonicalPremultipliedFrame = PremultipliedFrame.From(CanonicalFrame);
    private static readonly PremultipliedFrame BlinkSquintPremultipliedFrame = PremultipliedFrame.From(BlinkSquintFrame);
    private static readonly PremultipliedFrame ClosedEyesPremultipliedFrame = PremultipliedFrame.From(ClosedEyesFrame);
    private static readonly PremultipliedFrame SleepCrouchClosedPremultipliedFrame = PremultipliedFrame.From(SleepCrouchClosedFrame);
    private static readonly PremultipliedFrame SleepTuckClosedPremultipliedFrame = PremultipliedFrame.From(SleepTuckClosedFrame);
    private static readonly PremultipliedFrame SleepPremultipliedFrame = PremultipliedFrame.From(SleepFrame);
    private static readonly PremultipliedFrameSequence CanonicalToBlinkSquintSequence = new([CanonicalPremultipliedFrame, BlinkSquintPremultipliedFrame]);
    private static readonly PremultipliedFrameSequence BlinkSquintToClosedEyesSequence = new([BlinkSquintPremultipliedFrame, ClosedEyesPremultipliedFrame]);
    private static readonly PremultipliedFrameSequence ClosedEyesToSleepCrouchSequence = new([ClosedEyesPremultipliedFrame, SleepCrouchClosedPremultipliedFrame]);
    private static readonly PremultipliedFrameSequence SleepCrouchToSleepTuckSequence = new([SleepCrouchClosedPremultipliedFrame, SleepTuckClosedPremultipliedFrame]);
    private static readonly PremultipliedFrameSequence SleepTuckToSettledSleepSequence = new([SleepTuckClosedPremultipliedFrame, SleepPremultipliedFrame]);
    private static readonly SuppliedBodyDragFrames BodyDragFrames = new(CanonicalPremultipliedFrame);
    private PetState? _lastRenderedState;
    private bool _sleepEntryComplete;
    private PetState? _wakeBridgeState;
    private bool _wakeBridgeComplete;
    private FrameInteractionDescriptor _activeInteractionDescriptor = FrameInteractionDescriptor.Canonical;
    private FacingDirection _activeFacing = FacingDirection.Right;
    private bool _bodyClickPresentationActive;
    private FacingDirection _bodyClickVisibleFacing = FacingDirection.Right;
    private FacingDirection? _postBodyClickIdleFacing;
    private bool _bodyClickWakeActive;
    private bool _bodyClickWakeClickStarted;
    private double _bodyClickWakeElapsedMilliseconds;
    private double _bodyClickWakeAtClickStartMilliseconds;
    private bool _bodyDragPresentationActive;
    private FacingDirection _bodyDragVisibleFacing = FacingDirection.Right;
    private PointD? _lastRenderedWindowPosition;
    private PointD? _headPressOrigin;
    private CapturedHeadAnchor? _headAnchor;
    private bool _headSamplingOverride;
    private BitmapScalingMode _beforeHeadSampling;
    private double _lastHeadSwingAngle;
    private double _headSwingReleaseAngle;
    private bool _headSwingSettling;
    private bool _headLandingPresentationActive;
    private FacingDirection? _postHeadLandingIdleFacing;
    private BodyPullPresentation? _bodyPullPresentation;
    private readonly HeadSurroundingPresentation _headSurrounding = new();
    private CheekPullPresentation? _cheekPullPresentation;
    private FacingDirection? _postCheekIdleFacing;

    public DororongPresenter()
    {
        InitializeComponent();
    }

    internal event EventHandler<DirectInteractionPressEventArgs>? DirectInteractionPressed;

    internal bool TryCreateBodyPullCapture(PointD sourcePosition, out BodyPullCapture? capture)
    {
        capture = null;
        if (!CanUseBodyMap || DororongImage.Source is not BitmapSource source || DororongImage.ActualWidth <= 0) return false;
        var pixels = PremultipliedFrame.From(source).Pixels;
        var region = InteractionHitMap.PickBody(sourcePosition, pixels);
        if (region == BodyRegion.None) return false;
        var transform = DororongImage.TransformToAncestor(this);
        var scale = Math.Min(DororongImage.ActualWidth / 96, DororongImage.ActualHeight / 96);
        var offsetX = (DororongImage.ActualWidth - 96 * scale) / 2; var offsetY = (DororongImage.ActualHeight - 96 * scale) / 2;
        var origin = transform.Transform(new Point(offsetX, offsetY));
        var x = transform.Transform(new Point(offsetX + scale, offsetY)); var y = transform.Transform(new Point(offsetX, offsetY + scale));
        var matrix = new Matrix(x.X - origin.X, x.Y - origin.Y, y.X - origin.X, y.Y - origin.Y, origin.X, origin.Y);
        if (!matrix.HasInverse) return false;
        capture = new(region, sourcePosition, matrix, pixels);
        return true;
    }

    private bool CanUseBodyMap => _lastRenderedState != PetState.Sleep && !_bodyDragPresentationActive &&
        ReferenceEquals(_activeInteractionDescriptor, FrameInteractionDescriptor.Canonical) && DororongImage.Visibility == Visibility.Visible;

    private bool CanUseCheek => CanUseBodyMap &&
        (ReferenceEquals(DororongImage.Source, CanonicalFrame) || ReferenceEquals(DororongImage.Source, BlinkSquintFrame) ||
         ReferenceEquals(DororongImage.Source, ClosedEyesFrame));

    internal bool TryCreateCheekPullCapture(PointD sourcePosition, out CheekPullCapture? capture)
    {
        capture = null;
        if (!CanUseCheek || !InteractionHitMap.IsSelectedCheek(sourcePosition) || DororongImage.Source is not BitmapSource source) return false;
        var scale = Math.Min(DororongImage.ActualWidth / 96, DororongImage.ActualHeight / 96);
        if (!double.IsFinite(scale) || scale <= 0) return false;
        var transform = DororongImage.TransformToAncestor(this);
        var ox = (DororongImage.ActualWidth - 96 * scale) / 2; var oy = (DororongImage.ActualHeight - 96 * scale) / 2;
        if (!DororongImage.TryGetOpaqueSourcePoint(new Point(ox + sourcePosition.X * scale, oy + sourcePosition.Y * scale), out _)) return false;
        var a = transform.Transform(new Point(ox, oy)); var b = transform.Transform(new Point(ox + scale, oy)); var c = transform.Transform(new Point(ox, oy + scale));
        var matrix = new Matrix(b.X - a.X, b.Y - a.Y, c.X - a.X, c.Y - a.Y, a.X, a.Y);
        if (!matrix.HasInverse) return false;
        var bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[96 * 96 * 4]; bitmap.CopyPixels(pixels, 384, 0);
        capture = new(pixels, matrix, _activeFacing);
        return true;
    }

    public event EventHandler? ExitRequested;

    internal void Render(PetSnapshot snapshot, DirectInteractionSnapshot directInteraction)
        => Render(snapshot, directInteraction, TimeSpan.FromMilliseconds(16));

    internal void Render(PetSnapshot snapshot, DirectInteractionSnapshot directInteraction, TimeSpan elapsed)
    {
        // Runtime landing ends in idle: retain its last visible facing instead
        // of letting ResetPose turn a left-facing landing around on its last tick.
        if (_headLandingPresentationActive && directInteraction.HeadLanding is null)
        {
            _postHeadLandingIdleFacing = _activeFacing;
        }
        _headLandingPresentationActive = directInteraction.HeadLanding is not null;
        if (snapshot.State != PetState.Idle || directInteraction.Target != DirectInteractionTarget.None)
        {
            _postHeadLandingIdleFacing = null;
        }

        // Capture from the last actually displayed pose, before pending squash or
        // a first-tick overshoot changes it. Core owns window following/clamping.
        if (directInteraction.Target != DirectInteractionTarget.Body)
        {
            _headSurrounding.Reset();
            _headPressOrigin = null;
            _headAnchor = null;
            _lastHeadSwingAngle = _headSwingReleaseAngle = 0;
            _headSwingSettling = false;
        }
        else if (_headPressOrigin != directInteraction.PressOrigin)
        {
            _headSurrounding.Reset();
            _headPressOrigin = directInteraction.PressOrigin;
            _lastHeadSwingAngle = _headSwingReleaseAngle = 0;
            _headSwingSettling = false;
            _headAnchor = _lastRenderedWindowPosition is { } previousWindow
                ? HeadPullAnchoring.Capture(DororongImage, this, directInteraction.PressOrigin - previousWindow, _activeFacing)
                : null;
        }
        _lastRenderedWindowPosition = snapshot.Position;
        if (directInteraction.CheekPull is { } cheek)
        {
            _bodyPullPresentation?.Restore();
            _cheekPullPresentation ??= new((Canvas)Content, DororongImage);
            _cheekPullPresentation.Render(cheek, directInteraction.Phase, elapsed.TotalMilliseconds);
            return;
        }
        if (_cheekPullPresentation?.Capture is { } completedCheek) _postCheekIdleFacing = completedCheek.Facing;
        _cheekPullPresentation?.Restore();
        if (snapshot.State != PetState.Idle || directInteraction.Target != DirectInteractionTarget.None) _postCheekIdleFacing = null;
        if (directInteraction.BodyPull is { Capture: not null } bodyPull)
        {
            _bodyPullPresentation ??= new((Canvas)Content, DororongImage);
            _bodyPullPresentation.Render(bodyPull, elapsed.TotalMilliseconds);
            return;
        }
        _bodyPullPresentation?.Restore();
        var p = Math.Clamp(snapshot.Phase, 0, 1);
        var cycle = Math.Sin(p * Math.PI * 2);
        var bounce = Math.Sin(p * Math.PI);
        var bodyClickPresentationActive = IsBodyClickPresentationActive(snapshot, directInteraction);
        var bodyDragPresentationActive = IsBodyDragPresentationActive(directInteraction);
        if (bodyDragPresentationActive && !_bodyDragPresentationActive)
        {
            _bodyDragPresentationActive = true;
            _bodyDragVisibleFacing = _activeFacing;
        }
        else if (!bodyDragPresentationActive)
        {
            _bodyDragPresentationActive = false;
        }

        if (bodyClickPresentationActive && !_bodyClickPresentationActive)
        {
            _bodyClickPresentationActive = true;
            _bodyClickVisibleFacing = _activeFacing;
            _postBodyClickIdleFacing = null;
        }
        else if (!bodyClickPresentationActive)
        {
            if (_bodyClickPresentationActive && _lastRenderedState == PetState.ClickReaction)
            {
                _postBodyClickIdleFacing = _bodyClickVisibleFacing;
            }

            ResetBodyClickPresentationState();
            if (snapshot.State != PetState.Idle)
            {
                _postBodyClickIdleFacing = null;
            }
        }

        var wokeFromSettledSleep =
            _lastRenderedState == PetState.Sleep &&
            _sleepEntryComplete;

        if (snapshot.State == PetState.Sleep && _lastRenderedState != PetState.Sleep)
        {
            _sleepEntryComplete = false;
            _wakeBridgeState = null;
            _wakeBridgeComplete = false;
        }
        else if (snapshot.State != PetState.Sleep && _lastRenderedState == PetState.Sleep)
        {
            _sleepEntryComplete = false;
            if (wokeFromSettledSleep && bodyClickPresentationActive)
            {
                StartBodyClickWake();
                _wakeBridgeState = null;
                _wakeBridgeComplete = false;
            }
            else
            {
                _wakeBridgeState = wokeFromSettledSleep && SupportsWakeBridge(snapshot.State)
                    ? snapshot.State
                    : null;
                _wakeBridgeComplete = !wokeFromSettledSleep;
            }
        }
        else if (snapshot.State != PetState.Sleep && snapshot.State != _lastRenderedState)
        {
            _wakeBridgeState = null;
            _wakeBridgeComplete = false;
        }

        ResetPose();

        switch (snapshot.State)
        {
            case PetState.Idle:
                ApplyBreathing(p);
                if (p is >= 0.65 and < 0.69)
                {
                    DororongImage.Source = BlinkSquintFrame;
                }
                else if (p is >= 0.69 and < 0.73)
                {
                    DororongImage.Source = ClosedEyesFrame;
                }
                else if (p is >= 0.73 and < 0.77)
                {
                    DororongImage.Source = BlinkSquintFrame;
                }

                break;

            case PetState.Walk:
                BodyTranslateTransform.Y = -4 * Math.Abs(cycle);
                BodyScaleTransform.ScaleX = snapshot.Facing == FacingDirection.Left ? -1 : 1;
                break;

            case PetState.Curious:
                BodyRotateTransform.Angle = snapshot.Facing == FacingDirection.Right ? 7 : -7;
                break;

            case PetState.Startled:
                if (p < 0.5)
                {
                    BodyScaleTransform.ScaleX = 1 + (0.18 * bounce);
                    BodyScaleTransform.ScaleY = 1 - (0.14 * bounce);
                }

                break;

            case PetState.ClickReaction:
                break;

            case PetState.Dragged:
                BodyScaleTransform.ScaleY = 1.12;
                var bodyCenterX = Canvas.GetLeft(BodyGroup) + (BodyGroup.Width / 2);
                BodyRotateTransform.Angle = Math.Clamp(
                    (snapshot.GrabOffset?.X ?? bodyCenterX) - bodyCenterX,
                    -8,
                    8);
                break;

            case PetState.Sleep:
                ApplySleepPose(snapshot.Phase);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(snapshot), snapshot.State, "Unknown pet state.");
        }

        if (!bodyClickPresentationActive &&
            snapshot.State == PetState.Idle &&
            _postBodyClickIdleFacing is { } idleFacing)
        {
            BodyScaleTransform.ScaleX = idleFacing == FacingDirection.Left ? -1 : 1;
        }

        if (_postHeadLandingIdleFacing is { } landingFacing)
        {
            BodyScaleTransform.ScaleX = landingFacing == FacingDirection.Left ? -1 : 1;
        }

        ApplyBodyClickPresentation(snapshot, directInteraction);
        ApplyWakeBridge(snapshot.State, p);
        ApplyBodyClickWakeBridge(snapshot, directInteraction);
        ApplyBodyDragPresentation(directInteraction, elapsed.TotalMilliseconds);
        if (_postCheekIdleFacing is { } cheekFacing) BodyScaleTransform.ScaleX = cheekFacing == FacingDirection.Left ? -1 : 1;
        _activeFacing = BodyScaleTransform.ScaleX < 0
            ? FacingDirection.Left
            : FacingDirection.Right;
        _lastRenderedState = snapshot.State;
    }

    private static bool IsBodyClickPresentationActive(
        PetSnapshot snapshot,
        DirectInteractionSnapshot directInteraction) =>
        snapshot.State == PetState.ClickReaction ||
        directInteraction is
        {
            Target: DirectInteractionTarget.Body,
            Phase: DirectInteractionPhase.BodyPending
        };

    private static bool IsBodyDragPresentationActive(DirectInteractionSnapshot directInteraction) =>
        directInteraction is
        {
            Target: DirectInteractionTarget.Body,
            Phase: DirectInteractionPhase.BodyDragEntry or
                DirectInteractionPhase.BodyDragHold or
                DirectInteractionPhase.BodyDragSettle
        };

    private void StartBodyClickWake()
    {
        _bodyClickWakeActive = true;
        _bodyClickWakeClickStarted = false;
        _bodyClickWakeElapsedMilliseconds = 0;
        _bodyClickWakeAtClickStartMilliseconds = 0;
    }

    private void ResetBodyClickPresentationState()
    {
        _bodyClickPresentationActive = false;
        _bodyClickWakeActive = false;
        _bodyClickWakeClickStarted = false;
        _bodyClickWakeElapsedMilliseconds = 0;
        _bodyClickWakeAtClickStartMilliseconds = 0;
    }

    private void ApplyBodyClickPresentation(
        PetSnapshot snapshot,
        DirectInteractionSnapshot directInteraction)
    {
        BodyClickTransformSample? sample = null;
        if (directInteraction is
            {
                Target: DirectInteractionTarget.Body,
                Phase: DirectInteractionPhase.BodyPending
            })
        {
            sample = BodyClickTransformSampler.SamplePendingPress();
        }
        else if (snapshot.State == PetState.ClickReaction)
        {
            sample = BodyClickTransformSampler.SampleConfirmedClick(snapshot.Phase);
        }

        if (sample is not { } bodyClick)
        {
            return;
        }

        BodyScaleTransform.ScaleX = _bodyClickVisibleFacing == FacingDirection.Left ? -1 : 1;
        BodyScaleTransform.ScaleY = 1;
        BodyRotateTransform.Angle = 0;
        BodyTranslateTransform.X = 0;
        BodyTranslateTransform.Y = bodyClick.TranslationY;
        ImageBreathingScaleTransform.ScaleX = bodyClick.ScaleX;
        ImageBreathingScaleTransform.ScaleY = bodyClick.ScaleY;
        DororongImage.Source = CanonicalFrame;
        DororongImage.Opacity = 1;
        _activeInteractionDescriptor = FrameInteractionDescriptor.Canonical;
    }

    private void ApplyBodyClickWakeBridge(
        PetSnapshot snapshot,
        DirectInteractionSnapshot directInteraction)
    {
        if (!_bodyClickWakeActive)
        {
            return;
        }

        double elapsedMilliseconds;
        if (directInteraction is
            {
                Target: DirectInteractionTarget.Body,
                Phase: DirectInteractionPhase.BodyPending
            })
        {
            elapsedMilliseconds = _bodyClickWakeElapsedMilliseconds;
            _bodyClickWakeElapsedMilliseconds = Math.Min(
                BodyClickWakeDurationMilliseconds,
                _bodyClickWakeElapsedMilliseconds + PresentationTickMilliseconds);
        }
        else if (snapshot.State == PetState.ClickReaction)
        {
            if (!_bodyClickWakeClickStarted)
            {
                _bodyClickWakeClickStarted = true;
                _bodyClickWakeAtClickStartMilliseconds = _bodyClickWakeElapsedMilliseconds;
            }

            elapsedMilliseconds = Math.Min(
                BodyClickWakeDurationMilliseconds,
                _bodyClickWakeAtClickStartMilliseconds + (Math.Clamp(snapshot.Phase, 0, 1) * 500));
            _bodyClickWakeElapsedMilliseconds = Math.Max(
                _bodyClickWakeElapsedMilliseconds,
                elapsedMilliseconds);
        }
        else
        {
            ResetBodyClickPresentationState();
            return;
        }

        if (elapsedMilliseconds < BodyClickWakeFirstSegmentMilliseconds)
        {
            DororongImage.Source = SleepTuckSquintFrame;
            _activeInteractionDescriptor = FrameInteractionDescriptor.SleepTuck;
        }
        else if (elapsedMilliseconds < BodyClickWakeDurationMilliseconds)
        {
            DororongImage.Source = SleepCrouchSquintFrame;
            _activeInteractionDescriptor = FrameInteractionDescriptor.SleepCrouch;
        }
        else
        {
            _bodyClickWakeActive = false;
        }
    }

    private void ApplyBodyDragPresentation(DirectInteractionSnapshot directInteraction, double elapsedMilliseconds)
    {
        var source = directInteraction.Phase switch
        {
            DirectInteractionPhase.BodyDragEntry => BodyDragFrames.Sample(directInteraction.Strength),
            DirectInteractionPhase.BodyDragHold => BodyDragFrames.Sample(1),
            DirectInteractionPhase.BodyDragSettle when directInteraction.IsPartialDragSettle => BodyDragFrames.Sample(directInteraction.Strength),
            DirectInteractionPhase.BodyDragSettle => BodyDragFrames.Sample(1 - directInteraction.ReleaseProgress),
            _ => null
        };
        if (directInteraction.Target != DirectInteractionTarget.Body || source is null)
        {
            return;
        }

        BodyScaleTransform.ScaleX = (_headAnchor?.Facing ?? _bodyDragVisibleFacing) == FacingDirection.Left ? -1 : 1;
        BodyScaleTransform.ScaleY = 1;
        BodyRotateTransform.Angle = 0;
        BodyTranslateTransform.X = 0;
        BodyTranslateTransform.Y = 0;
        ImageBreathingScaleTransform.ScaleX = 1;
        ImageBreathingScaleTransform.ScaleY = 1;
        DororongImage.Source = source;
        DororongImage.Opacity = 1;
        if (_headAnchor is { } anchor)
        {
            _headSurrounding.Advance(directInteraction, elapsedMilliseconds);
            var release = directInteraction.Phase == DirectInteractionPhase.BodyDragSettle
                ? Math.Clamp(directInteraction.ReleaseProgress, 0, 1) : 0;
            var remaining = 1 - release * release * (3 - 2 * release);
            var angle = double.IsFinite(directInteraction.HeadSwingDegrees)
                ? Math.Clamp(directInteraction.HeadSwingDegrees, -HeadSwingSession.MaximumAngle, HeadSwingSession.MaximumAngle) : 0;
            var settling = directInteraction.Phase == DirectInteractionPhase.BodyDragSettle;
            if (settling && !_headSwingSettling) _headSwingReleaseAngle = _lastHeadSwingAngle;
            _headSwingSettling = settling;
            if (settling)
            {
                // Release the visible capped angle, not a larger hidden simulation
                // angle. A relaxing viewport cap must never add tilt after button-up.
                angle = Math.CopySign(Math.Min(Math.Abs(_headSwingReleaseAngle * remaining),
                    Math.Abs(_lastHeadSwingAngle)), _headSwingReleaseAngle);
            }
            ApplyAnchoredHeadAngle(anchor, angle, remaining, source);
            // The same grabbed pose must have one safe magnitude in both
            // directions, even though the source silhouette is asymmetric.
            if (angle != 0 && !BothHeadAnglesFit(anchor, angle, remaining, source))
            {
                // Never move the grab point or shrink the art to make a swing fit.
                // Limit this pose's angle if its ink approaches the window boundary.
                var low = 0d; var high = 1d;
                for (var i = 0; i < 12; i++)
                {
                    var fraction = (low + high) / 2;
                    if (BothHeadAnglesFit(anchor, angle * fraction, remaining, source)) low = fraction;
                    else high = fraction;
                }
                angle *= low;
                ApplyAnchoredHeadAngle(anchor, angle, remaining, source);
            }
            // Render once at the final common safe angle; no extra motion tick.
            DororongImage.Source = _headSurrounding.Render(source, anchor, angle);
            if (angle != 0 && !HeadPullAnchoring.FitsViewport(DororongImage, SwingViewport))
            {
                ApplyAnchoredHeadAngle(anchor, 0, remaining, source);
                DororongImage.Source = _headSurrounding.Render(source, anchor, 0);
            }
            _lastHeadSwingAngle = BodyRotateTransform.Angle;
            _beforeHeadSampling = RenderOptions.GetBitmapScalingMode(DororongImage);
            // Keep upright pixel art exact, but blend samples during the actual
            // displayed tilt so thin contours do not jump between source pixels.
            RenderOptions.SetBitmapScalingMode(DororongImage, BodyRotateTransform.Angle != 0
                ? BitmapScalingMode.Linear : BitmapScalingMode.NearestNeighbor);
            _headSamplingOverride = true;
        }
        if (directInteraction.HeadLanding is { Compression: not 0 } landing)
        {
            // All supplied keys retain the canonical sole edge at sourceY87.
            // Keep this contact point fixed while the whole sprite squashes.
            var sole = DororongImage.TranslatePoint(new Point(48, 87), this);
            BodyScaleTransform.ScaleX *= 1 + landing.Compression * (5d / 9);
            BodyScaleTransform.ScaleY = 1 - landing.Compression;
            var movedSole = DororongImage.TranslatePoint(new Point(48, 87), this);
            BodyTranslateTransform.X += sole.X - movedSole.X;
            BodyTranslateTransform.Y += sole.Y - movedSole.Y;
        }
        _activeInteractionDescriptor = FrameInteractionDescriptor.Canonical;
    }

    private void ApplyAnchoredHeadAngle(CapturedHeadAnchor anchor, double angle, double remaining, BitmapSource registrationSource)
    {
        BodyRotateTransform.Angle = angle;
        BodyTranslateTransform.X = 0;
        BodyTranslateTransform.Y = 0;
        var correction = HeadPullAnchoring.Correction(DororongImage, this, anchor, registrationSource);
        BodyTranslateTransform.X = correction.X * remaining;
        BodyTranslateTransform.Y = correction.Y * remaining;
    }

    private bool BothHeadAnglesFit(CapturedHeadAnchor anchor, double angle, double remaining, BitmapSource registrationSource)
    {
        var displayed = BodyRotateTransform.Angle;
        ApplyAnchoredHeadAngle(anchor, Math.Abs(angle), remaining, registrationSource);
        var positive = HeadPullAnchoring.FitsSwingViewport(DororongImage, SwingViewport, registrationSource);
        ApplyAnchoredHeadAngle(anchor, -Math.Abs(angle), remaining, registrationSource);
        var negative = HeadPullAnchoring.FitsSwingViewport(DororongImage, SwingViewport, registrationSource);
        ApplyAnchoredHeadAngle(anchor, displayed, remaining, registrationSource);
        return positive && negative;
    }

    internal DirectInteractionTarget ClassifyOpaqueSourcePoint(PointD sourcePosition, bool opaque)
    {
        if (!opaque || !InteractionHitMap.InSource(sourcePosition)) return DirectInteractionTarget.None;
        if (CanUseBodyMap && DororongImage.Source is BitmapSource source)
        {
            var pixels = PremultipliedFrame.From(source).Pixels;
            if (pixels[((int)sourcePosition.Y * 96 + (int)sourcePosition.X) * 4 + 3] == 0)
                return DirectInteractionTarget.None;
            if (CanUseCheek && InteractionHitMap.IsSelectedCheek(sourcePosition)) return DirectInteractionTarget.RightCheek;
            if (InteractionHitMap.PickBody(sourcePosition,pixels) != BodyRegion.None) return DirectInteractionTarget.FiveRegionBody;
            // Keep the existing opposite-side cheek behavior. The remaining face,
            // lower hair and unassigned body gaps must NOT fall through to head drag.
            if (_activeInteractionDescriptor.Classify(GetVisibleFramePoint(sourcePosition),_activeFacing,true) == DirectInteractionTarget.LeftCheek)
                return DirectInteractionTarget.LeftCheek;
            return InteractionHitMap.IsUpperHead(sourcePosition) ? DirectInteractionTarget.Body : DirectInteractionTarget.None;
        }
        var legacy = _activeInteractionDescriptor.Classify(
            GetVisibleFramePoint(sourcePosition),
            _activeFacing,
            opaque);
        // Separate sleep/noncanonical wake behavior is unchanged; no canonical
        // hit mask is applied to an alternate pose or an active captured overlay.
        return legacy == DirectInteractionTarget.RightCheek ? DirectInteractionTarget.Body : legacy;
    }

    private PointD GetVisibleFramePoint(PointD sourcePosition) =>
        _activeFacing == FacingDirection.Left
            ? new PointD(FrameMaximumCoordinate - sourcePosition.X, sourcePosition.Y)
            : sourcePosition;

    private static bool SupportsWakeBridge(PetState state) =>
        state is PetState.Curious or PetState.Startled or PetState.ClickReaction;

    private void ApplySleepPose(double phase)
    {
        if (_sleepEntryComplete)
        {
            ApplySettledSleep(phase);
            return;
        }

        if (phase < 0.0225)
        {
            ApplySleepCrossfade(CanonicalToBlinkSquintSequence, FrameInteractionDescriptor.Canonical, FrameInteractionDescriptor.Canonical, phase, 0, 0.0225);
        }
        else if (phase < 0.045)
        {
            ApplySleepCrossfade(BlinkSquintToClosedEyesSequence, FrameInteractionDescriptor.Canonical, FrameInteractionDescriptor.Canonical, phase, 0.0225, 0.045);
        }
        else if (phase < 0.070)
        {
            ApplySleepCrossfade(ClosedEyesToSleepCrouchSequence, FrameInteractionDescriptor.Canonical, FrameInteractionDescriptor.SleepCrouch, phase, 0.045, 0.070);
        }
        else if (phase < 0.100)
        {
            ApplySleepCrossfade(SleepCrouchToSleepTuckSequence, FrameInteractionDescriptor.SleepCrouch, FrameInteractionDescriptor.SleepTuck, phase, 0.070, 0.100);
        }
        else if (phase < 0.135)
        {
            ApplySleepCrossfade(SleepTuckToSettledSleepSequence, FrameInteractionDescriptor.SleepTuck, FrameInteractionDescriptor.SettledSleep, phase, 0.100, 0.135);
        }
        else
        {
            _sleepEntryComplete = true;
            ApplySettledSleep(phase);
        }
    }

    private void ApplySleepCrossfade(
        PremultipliedFrameSequence frames,
        FrameInteractionDescriptor fromDescriptor,
        FrameInteractionDescriptor toDescriptor,
        double phase,
        double start,
        double end)
    {
        var progress = Math.Clamp((phase - start) / (end - start), 0, 1);
        var opacity = progress * progress * (3 - (2 * progress));
        if (opacity <= 0)
        {
            DororongImage.Source = frames.Sample(0);
            _activeInteractionDescriptor = fromDescriptor;
            return;
        }

        if (opacity >= 1)
        {
            DororongImage.Source = frames.Sample(1);
            _activeInteractionDescriptor = toDescriptor;
            return;
        }

        DororongImage.Source = frames.Sample(progress);
        _activeInteractionDescriptor = FrameInteractionDescriptor.Interpolate(
            fromDescriptor,
            toDescriptor,
            opacity);
    }

    private void ApplySettledSleep(double phase)
    {
        DororongImage.Source = SleepFrame;
        _activeInteractionDescriptor = FrameInteractionDescriptor.SettledSleep;
        DororongImage.RenderTransformOrigin = new Point(0.435630, 0.854167);
        ApplyBreathing((phase - 0.135 + 1) % 1);
    }

    private void ApplyWakeBridge(PetState state, double phase)
    {
        if (_wakeBridgeState != state || _wakeBridgeComplete)
        {
            return;
        }

        var (firstLimit, secondLimit) = state switch
        {
            PetState.Curious => (0.05625, 0.1125),
            PetState.Startled => (0.06, 0.12),
            PetState.ClickReaction => (0.09, 0.18),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported wake bridge state.")
        };

        if (phase < firstLimit)
        {
            DororongImage.Source = SleepTuckSquintFrame;
            _activeInteractionDescriptor = FrameInteractionDescriptor.SleepTuck;
        }
        else if (phase < secondLimit)
        {
            DororongImage.Source = SleepCrouchSquintFrame;
            _activeInteractionDescriptor = FrameInteractionDescriptor.SleepCrouch;
        }
        else
        {
            _wakeBridgeComplete = true;
        }
    }

    private void ApplyBreathing(double phase)
    {
        var amount = GetBreathingAmount(phase);
        ImageBreathingScaleTransform.ScaleX = 1 + (0.024 * amount);
        ImageBreathingScaleTransform.ScaleY = 1 + (0.012 * amount);
    }

    private static double GetBreathingAmount(double phase)
    {
        if (phase < 0.38)
        {
            var inhaleProgress = phase / 0.38;
            return inhaleProgress * inhaleProgress * (3 - (2 * inhaleProgress));
        }

        if (phase <= 0.43)
        {
            return 1;
        }

        var exhaleProgress = (phase - 0.43) / 0.57;
        return 1 - (exhaleProgress * exhaleProgress * (3 - (2 * exhaleProgress)));
    }

    private void ResetPose()
    {
        if (_headSamplingOverride)
        {
            RenderOptions.SetBitmapScalingMode(DororongImage, _beforeHeadSampling);
            _headSamplingOverride = false;
        }
        BodyScaleTransform.ScaleX = 1;
        BodyScaleTransform.ScaleY = 1;
        BodyRotateTransform.Angle = 0;
        BodyTranslateTransform.X = 0;
        BodyTranslateTransform.Y = 0;
        ImageBreathingScaleTransform.ScaleX = 1;
        ImageBreathingScaleTransform.ScaleY = 1;
        DororongImage.RenderTransformOrigin = new Point(0.428987, 0.916667);
        DororongImage.Source = CanonicalFrame;
        DororongImage.Opacity = 1;
        _activeInteractionDescriptor = FrameInteractionDescriptor.Canonical;
        _activeFacing = FacingDirection.Right;
    }

    private static BitmapImage LoadFrame(string fileName)
    {
        var frame = new BitmapImage();
        frame.BeginInit();
        frame.UriSource = new Uri(
            $"pack://application:,,,/Dororong.App;component/Assets/{fileName}",
            UriKind.Absolute);
        frame.CacheOption = BitmapCacheOption.OnLoad;
        frame.EndInit();
        frame.Freeze();
        return frame;
    }

    private void OnBodyPrimaryPressed(object sender, MouseButtonEventArgs e)
    {
        if (!DororongImage.TryGetOpaqueSourcePoint(e.GetPosition(DororongImage), out var sourcePosition))
        {
            return;
        }

        var framePosition = GetVisibleFramePoint(sourcePosition);
        var target = ClassifyOpaqueSourcePoint(sourcePosition, opaque: true);
        if (target == DirectInteractionTarget.None)
        {
            return;
        }

        var windowPosition = e.GetPosition(this);
        BodyPullCapture? bodyCapture = null;
        CheekPullCapture? cheekCapture = null;
        if (target == DirectInteractionTarget.RightCheek && !TryCreateCheekPullCapture(sourcePosition, out cheekCapture)) return;
        if (target == DirectInteractionTarget.FiveRegionBody)
        {
            var actual = e.GetPosition(DororongImage);
            var scale = Math.Min(DororongImage.ActualWidth / 96, DororongImage.ActualHeight / 96);
            var precise = new PointD((actual.X - (DororongImage.ActualWidth - 96 * scale) / 2) / scale, (actual.Y - (DororongImage.ActualHeight - 96 * scale) / 2) / scale);
            if (!TryCreateBodyPullCapture(precise, out bodyCapture)) return;
        }
        DirectInteractionPressed?.Invoke(
            this,
            new DirectInteractionPressEventArgs(
                target,
                new PointD(windowPosition.X, windowPosition.Y),
                framePosition,
                _activeInteractionDescriptor.GetScreenOutwardSign(target, _activeFacing),
                bodyCapture) { CheekCapture = cheekCapture });
        e.Handled = true;
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}
