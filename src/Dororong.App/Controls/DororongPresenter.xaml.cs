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
    {
        Target = target;
        WindowLocalPosition = windowLocalPosition;
        FramePosition = framePosition;
        OutwardSign = outwardSign;
    }

    internal DirectInteractionTarget Target { get; }
    internal PointD WindowLocalPosition { get; }
    internal PointD FramePosition { get; }
    internal double OutwardSign { get; }
}

public partial class DororongPresenter : UserControl
{
    private const double FrameMaximumCoordinate = 95;
    private const double BodyClickWakeFirstSegmentMilliseconds = 45;
    private const double BodyClickWakeDurationMilliseconds = 90;
    private const double PresentationTickMilliseconds = 16;

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
    private static readonly PremultipliedFrameSequence BodyDragEntrySequence = LoadFrameSequence(
        "body-drag-entry-00-press.png",
        "body-drag-entry-01-release.png",
        "body-drag-entry-02-lengthen.png",
        "body-drag-entry-03-drop.png",
        "body-drag-entry-04-stretch.png",
        "body-drag-entry-05-dangle.png",
        "body-drag-entry-06-near-hang.png",
        "body-drag-entry-07-hang.png");
    private static readonly PremultipliedFrameSequence BodyDragSettleSequence = LoadFrameSequence(
        "body-drag-settle-00-hang.png",
        "body-drag-settle-01-lift.png",
        "body-drag-settle-02-gather.png",
        "body-drag-settle-03-land.png",
        "body-drag-settle-04-recover.png");
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

    public DororongPresenter()
    {
        InitializeComponent();
    }

    internal event EventHandler<DirectInteractionPressEventArgs>? DirectInteractionPressed;

    public event EventHandler? ExitRequested;

    internal void Render(PetSnapshot snapshot, DirectInteractionSnapshot directInteraction)
    {
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

        ApplyBodyClickPresentation(snapshot, directInteraction);
        ApplyWakeBridge(snapshot.State, p);
        ApplyBodyClickWakeBridge(snapshot, directInteraction);
        ApplyBodyDragPresentation(directInteraction);
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

    private void ApplyBodyDragPresentation(DirectInteractionSnapshot directInteraction)
    {
        var source = directInteraction.Phase switch
        {
            DirectInteractionPhase.BodyDragEntry => BodyDragEntrySequence.Sample(directInteraction.Strength),
            DirectInteractionPhase.BodyDragHold => BodyDragEntrySequence.Sample(1),
            DirectInteractionPhase.BodyDragSettle => BodyDragSettleSequence.Sample(directInteraction.ReleaseProgress),
            _ => null
        };
        if (directInteraction.Target != DirectInteractionTarget.Body || source is null)
        {
            return;
        }

        BodyScaleTransform.ScaleX = _bodyDragVisibleFacing == FacingDirection.Left ? -1 : 1;
        BodyScaleTransform.ScaleY = 1;
        BodyRotateTransform.Angle = 0;
        BodyTranslateTransform.X = 0;
        BodyTranslateTransform.Y = 0;
        ImageBreathingScaleTransform.ScaleX = 1;
        ImageBreathingScaleTransform.ScaleY = 1;
        DororongImage.Source = source;
        DororongImage.Opacity = 1;
        _activeInteractionDescriptor = FrameInteractionDescriptor.Canonical;
    }

    internal DirectInteractionTarget ClassifyOpaqueSourcePoint(PointD sourcePosition, bool opaque) =>
        _activeInteractionDescriptor.Classify(
            GetVisibleFramePoint(sourcePosition),
            _activeFacing,
            opaque);

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

    private static PremultipliedFrameSequence LoadFrameSequence(params string[] fileNames) =>
        new(fileNames
            .Select(fileName => PremultipliedFrame.From(LoadFrame(fileName)))
            .ToArray());

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
        DirectInteractionPressed?.Invoke(
            this,
            new DirectInteractionPressEventArgs(
                target,
                new PointD(windowPosition.X, windowPosition.Y),
                framePosition,
                _activeInteractionDescriptor.GetScreenOutwardSign(target, _activeFacing)));
        e.Handled = true;
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}
