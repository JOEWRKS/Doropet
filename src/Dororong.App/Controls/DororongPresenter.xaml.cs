using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

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
    internal FacingDirection? PressFacing { get; init; }
    internal bool IsAttachedCheek { get; init; }
    internal bool StartsHanging { get; init; }
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
    private readonly LocomotionPresentation _locomotion = new();
    private bool _locomotionBlocked;
    private bool _locomotionStarted;
    private PointD? _locomotionPosition;
    internal bool HoldLocomotionWalk(PetSnapshot snapshot) => snapshot.State == PetState.Walk &&
        !snapshot.IsDirectInteractionPending && !_locomotionBlocked && _locomotion.HoldWalking;
    internal void SetLocomotionBlocked(bool blocked) => _locomotionBlocked = blocked;
    private bool _sittingRequested;
    private double _sittingClockMilliseconds;
    private double _ordinaryBlinkMilliseconds;
    internal void SetSittingRequested(bool requested)
    {
        if (requested && !_sittingRequested) _sittingClockMilliseconds = 0;
        _sittingRequested = requested;
    }
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
    private AlphaHitTestImage? _pendingHeadCaptureImage;
    private bool _headSamplingOverride;
    private BitmapScalingMode _beforeHeadSampling;
    private double _lastHeadSwingAngle;
    private double _headSwingReleaseAngle;
    private bool _headSwingSettling;
    private BitmapSource? _lastHeadSource;
    private Func<double, BitmapSource>? _headRecovery;
    private bool _headLandingPresentationActive;
    private FacingDirection? _postHeadLandingIdleFacing;
    private BodyPullPresentation? _bodyPullPresentation;
    private readonly HeadSurroundingPresentation _headSurrounding = new();
    private CheekPullPresentation? _cheekPullPresentation;
    private FacingDirection? _postCheekIdleFacing;
    private readonly PlatformContactPresentation _platformContact = new();
    private readonly EdgePerchPresentation _edgePerch;
    private readonly ExtremeLandingPresentation _extremeLanding;
    private bool _directOwnsPresentation;
    private bool _pendingClickPresentation;
    private readonly PerchReadinessPresentation _perchReadiness = new();

    public DororongPresenter()
    {
        InitializeComponent();
        _extremeLanding = new(BodyGroup, DororongImage);
        _edgePerch = new((Canvas)Content, DororongImage, OnBodyPrimaryPressed);
        Unloaded += (_, _) => { _edgePerch.Restore(); _platformContact.Restore(); _extremeLanding.Reset(); ResetHunting(); };
    }

    internal event EventHandler<DirectInteractionPressEventArgs>? DirectInteractionPressed;

    internal FootContact? MeasurePlatformContact()
        => MeasurePlatformGeometry()?.Contact;

    internal (FootContact Contact, RectD Bounds)? MeasurePlatformGeometry()
    {
        var image = _extremeLanding.VisibleImage ?? _bodyPullPresentation?.VisibleImage ?? _cheekPullPresentation?.VisibleImage ?? DororongImage;
        if (image.Visibility != Visibility.Visible || image.Source is not BitmapSource source ||
            image.ActualWidth <= 0 || image.ActualHeight <= 0) return null;
        var scale = Math.Min(image.ActualWidth / source.PixelWidth, image.ActualHeight / source.PixelHeight);
        var transform = new GeneralTransformGroup();
        transform.Children.Add(new ScaleTransform(scale, scale));
        transform.Children.Add(new TranslateTransform((image.ActualWidth - source.PixelWidth * scale) / 2,
            (image.ActualHeight - source.PixelHeight * scale) / 2));
        transform.Children.Add(ReferenceEquals(image, _cheekPullPresentation?.VisibleImage)
            ? _cheekPullPresentation!.PhysicsTransform(this)
            : image.TransformToAncestor(this));
        var contact = _platformContact.Measure(source, transform);
        return double.IsFinite(contact.SoleY) ? (contact, _platformContact.VisibleBounds) : null;
    }

    internal PerchContact? MeasureEdgePerchContact(FacingDirection facing) => _edgePerch.Measure(facing, this);
    internal AlphaHitTestImage EdgePerchImage => _edgePerch.Image;

    internal void ApplyEdgePerch(EdgePerchPhase phase, FacingDirection facing, TimeSpan delta, bool immediateRestore) =>
        _edgePerch.Apply(phase, facing, delta, immediateRestore);

    internal AlphaHitTestImage ResolvePrimaryPressedImage(object? eventSource)
    {
        var perch = _edgePerch.VisibleImage;
        var extreme = _extremeLanding.VisibleImage;
        return eventSource is AlphaHitTestImage eventImage &&
            (ReferenceEquals(eventImage, perch) || ReferenceEquals(eventImage, extreme) || ReferenceEquals(eventImage, DororongImage))
                ? eventImage
                : perch ?? extreme ?? DororongImage;
    }

    internal void ApplyPlatformPose(PlatformPose? pose, double? targetSoleY = null)
    {
        _platformContact.Restore();
        _extremeLanding.RestoreFrame();
        if (!_directOwnsPresentation && pose is { IsExtremeLanding: true } extreme && targetSoleY is not null)
            _extremeLanding.Apply(extreme.LegSpread);
        else _extremeLanding.Reset();
        // The authored pending press scales about a point one pixel below the
        // true sole. Pin that subpixel change before release, otherwise a tap
        // starts below a window edge and is mistaken for a real support loss.
        if (_pendingClickPresentation && targetSoleY is { } pressedSole && MeasurePlatformContact() is { } pressedContact)
        {
            _platformContact.Apply(BodyGroup, pressedContact, pressedSole, 0, 0);
            return;
        }
        // Position belongs to motion. Its same local contact is the explicit
        // target; a pose alone cannot reconstruct it from the support identity.
        if (_directOwnsPresentation || pose is not { Phase: not PlatformPhase.Suspended } platform ||
            targetSoleY is not { } target || MeasurePlatformContact() is not { } current) return;
        FrameworkElement body = _bodyPullPresentation?.VisibleImage ?? _cheekPullPresentation?.VisibleImage ?? (FrameworkElement)BodyGroup;
        _platformContact.Apply(body, current, target, platform.Squash, platform.Sway, platform.IsExtremeLanding);
    }

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

    private bool CanUseBodyMap => _extremeLanding.VisibleImage is null && _lastRenderedState != PetState.Sleep && !_bodyDragPresentationActive &&
        ReferenceEquals(_activeInteractionDescriptor, FrameInteractionDescriptor.Canonical) && DororongImage.Visibility == Visibility.Visible;

    private bool CanUseCheek => CanUseBodyMap &&
        (ReferenceEquals(DororongImage.Source, CanonicalFrame) || ReferenceEquals(DororongImage.Source, BlinkSquintFrame) ||
         ReferenceEquals(DororongImage.Source, ClosedEyesFrame) || LocomotionFrames.Contains(DororongImage.Source) || UprightRumpSource.Contains(DororongImage.Source) ||
         (_huntingImage is not null && ReferenceEquals(DororongImage.Source, _huntingImage)));

    internal bool TryCreateCheekPullCapture(PointD sourcePosition, out CheekPullCapture? capture)
    {
        if(_edgePerch.IsAttached) return _edgePerch.TryCaptureCheek(sourcePosition,this,out capture);
        capture = null;
        if (!CanUseCheek || !InteractionHitMap.IsSelectedCheek(HuntHeadPoint(sourcePosition)) || DororongImage.Source is not BitmapSource source) return false;
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
        capture = IsHuntingImage ? CaptureHuntingCheek(pixels,matrix) : new(pixels, matrix, _activeFacing);
        return true;
    }

    internal void RenderDesktop(PetSnapshot snapshot, DirectInteractionSnapshot direct) =>
        RenderDesktop(snapshot, direct, TimeSpan.FromMilliseconds(PresentationTickMilliseconds));

    internal void RenderDesktop(PetSnapshot snapshot, DirectInteractionSnapshot direct, TimeSpan elapsed)
    {
        // The simulation's facing is a screen direction; authored Render/capture
        // facing is legacy mirror parity (the unmirrored source looks left).
        // Convert only autonomous poses at this boundary. Captured direct poses
        // already carry the actual displayed transform and must not be flipped again.
        if (snapshot.State is PetState.Idle or PetState.Walk &&
            !snapshot.IsDirectInteractionPending && direct.Target == DirectInteractionTarget.None)
            snapshot = snapshot with { Facing = snapshot.Facing == FacingDirection.Right ? FacingDirection.Left : FacingDirection.Right };
        Render(snapshot, direct, elapsed);
    }

    public event EventHandler? ExitRequested;
    public event EventHandler? SitRequested;

    internal void Render(PetSnapshot snapshot, DirectInteractionSnapshot directInteraction)
        => Render(snapshot, directInteraction, TimeSpan.FromMilliseconds(16));

    internal void Render(PetSnapshot snapshot, DirectInteractionSnapshot directInteraction, TimeSpan elapsed)
    {
        _perchReadiness.Restore();
        RenderPose(snapshot, directInteraction, elapsed);
        var image = _bodyPullPresentation?.VisibleImage ?? _cheekPullPresentation?.VisibleImage ?? DororongImage;
        Point? pinnedSource = null;
        if (directInteraction.IsPerchReady && image.Source is BitmapSource bitmap && image.ActualWidth > 0 && image.ActualHeight > 0)
        {
            var localPointer = directInteraction.PointerPosition - snapshot.Position;
            if (image.TransformToAncestor(this).Inverse is { } inverse)
            {
                var point = inverse.Transform(new Point(localPointer.X, localPointer.Y));
                pinnedSource = new Point(point.X * bitmap.PixelWidth / image.ActualWidth,
                    point.Y * bitmap.PixelHeight / image.ActualHeight);
            }
        }
        _perchReadiness.Apply(image, directInteraction.IsPerchReady,
            directInteraction.Target == DirectInteractionTarget.Body, elapsed, pinnedSource,
            ReferenceEquals(image,_cheekPullPresentation?.VisibleImage) ? _cheekPullPresentation.RenderReadiness : null);
    }

    private void RenderPose(PetSnapshot snapshot, DirectInteractionSnapshot directInteraction, TimeSpan elapsed)
    {
        // Disabled body drags retain the actual pose (including seated/hunting
        // art), rather than entering pending head squash or a body overlay.
        if (directInteraction.Target == DirectInteractionTarget.ClickOnly) return;

        if (_sittingRequested && !_locomotionBlocked && !_edgePerch.IsAttached &&
            !snapshot.IsDirectInteractionPending && snapshot.State != PetState.ClickReaction &&
            directInteraction.Target == DirectInteractionTarget.None && directInteraction.HeadLanding is null)
        {
            // Core autonomous time is paused, but a seated pet still blinks.
            _sittingClockMilliseconds = (_sittingClockMilliseconds + Math.Max(0, elapsed.TotalMilliseconds)) % 3200;
            snapshot = snapshot with { State = PetState.Idle, Phase = _sittingClockMilliseconds / 3200 };
        }
        var locomotionAllowed = !_locomotionBlocked && snapshot.State is PetState.Idle or PetState.Walk &&
            !snapshot.IsDirectInteractionPending && directInteraction.Target == DirectInteractionTarget.None &&
            directInteraction.HeadLanding is null && !_edgePerch.IsAttached;
        // Eye timing is shared across standing, sitting and walking, independent
        // of motion phase. A frozen cheek capture pauses its eye clock so its
        // closed-eye endpoint stays continuous when that overlay retires.
        _ordinaryBlinkMilliseconds = locomotionAllowed
            ? (_ordinaryBlinkMilliseconds + (double.IsFinite(elapsed.TotalMilliseconds) ? Math.Max(0, elapsed.TotalMilliseconds) : 0)) % 5000
            : directInteraction.Target == DirectInteractionTarget.RightCheek ? _ordinaryBlinkMilliseconds : 0;
        var ordinaryEyesClosed = _ordinaryBlinkMilliseconds is >= 2000 and < 2360;
        var traveled = _locomotionPosition is { } last ? Math.Abs(snapshot.Position.X-last.X) : 0;
        _locomotionPosition = snapshot.Position;
        // While still pinned, suspend the hidden seated pose instead of erasing
        // it: a captured local tug must retire back to the same seated source.
        // Platform/direct rendering still has priority. Actual carry clears the
        // request in the loop, restoring the existing reset/recovery behavior.
        if (locomotionAllowed || !_sittingRequested)
            _locomotion.Advance(Math.Max(0,elapsed.TotalMilliseconds),snapshot.State==PetState.Walk && !_huntSession.IsActive,traveled,!locomotionAllowed,_sittingRequested);
        if (!locomotionAllowed && !_sittingRequested) _locomotionStarted = false;
        else if (Math.Round(_locomotion.Sit * 64) > 0 || Math.Round(_locomotion.Walk * 8) > 0)
            _locomotionStarted = true;
        // Retain head-release facing independently of the legacy timed landing:
        // platform-owned falls finish shape recovery while still airborne.
        if ((_headLandingPresentationActive && directInteraction.HeadLanding is null) ||
            (_bodyDragPresentationActive && directInteraction.Target != DirectInteractionTarget.Body))
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
            _pendingHeadCaptureImage = null;
            _lastHeadSource = null;
            _headRecovery = null;
            _headSurrounding.Reset();
            _headPressOrigin = null;
            _headAnchor = null;
            _lastHeadSwingAngle = _headSwingReleaseAngle = 0;
            _headSwingSettling = false;
        }
        else if (_headPressOrigin != directInteraction.PressOrigin)
        {
            _lastHeadSource = null;
            _headRecovery = null;
            _headSurrounding.Reset();
            _headPressOrigin = directInteraction.PressOrigin;
            _lastHeadSwingAngle = _headSwingReleaseAngle = 0;
            _headSwingSettling = false;
            var captureImage = _pendingHeadCaptureImage ?? _edgePerch.VisibleImage ?? _extremeLanding.VisibleImage ?? DororongImage;
            _pendingHeadCaptureImage = null;
            _headAnchor = _lastRenderedWindowPosition is { } previousWindow
                ? HeadPullAnchoring.Capture(captureImage, this, directInteraction.PressOrigin - previousWindow, directInteraction.PressFacing ?? _activeFacing)
                : null;
        }
        _lastRenderedWindowPosition = snapshot.Position;
        // Regrab capture above must see the still-composed perch source and its
        // live transform. Retire that ownership before a body/cheek overlay can
        // hide the canonical image; the later idempotent runtime restore must
        // not resurrect canonical underneath the new direct presentation.
        if (directInteraction.Target != DirectInteractionTarget.None && !directInteraction.IsAttachedCheek && directInteraction.PawPull is null) _edgePerch.Restore();
        _edgePerch.RenderLocalPaw(directInteraction.PawPull);
        _edgePerch.RenderLocalCheek(directInteraction.IsAttachedCheek ? directInteraction.CheekPull : null,directInteraction.Phase,elapsed);
        if(directInteraction.IsAttachedCheek || directInteraction.PawPull is not null) return;
        // Body/cheek captures were taken at press; head capture above must still
        // see the actual displayed platform pose. Only now retire this layer,
        // including before either overlay's early return.
        _platformContact.Restore();
        _extremeLanding.RestoreFrame();
        _pendingClickPresentation = directInteraction.Target == DirectInteractionTarget.Body &&
            directInteraction.Phase == DirectInteractionPhase.BodyPending && !directInteraction.StartsHanging;
        _directOwnsPresentation = directInteraction.RequiresCapture ||
            directInteraction.Phase == DirectInteractionPhase.BodyPending || directInteraction.HeadLanding is not null;
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
            _bodyDragVisibleFacing = directInteraction.PressFacing ?? _activeFacing;
        }
        else if (!bodyDragPresentationActive)
        {
            _bodyDragPresentationActive = false;
        }

        if (bodyClickPresentationActive && !_bodyClickPresentationActive)
        {
            _bodyClickPresentationActive = true;
            _bodyClickVisibleFacing = directInteraction.PressFacing ?? _activeFacing;
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
                if (ordinaryEyesClosed)
                {
                    DororongImage.Source = ClosedEyesFrame;
                }

                break;

            case PetState.Walk:
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

        if (locomotionAllowed)
        {
            var closed = ordinaryEyesClosed;
            // Direct recovery owns its exact canonical endpoint until another
            // motion begins. Once locomotion starts, keep its cleaned standing
            // endpoint after rising; do not restore the old exterior fringe.
            if (_locomotionStarted)
                DororongImage.Source = _locomotion.Sit > 0
                    ? LocomotionFrames.Sit(_locomotion.Sit,closed)
                    : LocomotionFrames.Walk(_locomotion.Walk,_locomotion.Distance,closed);
            else
                DororongImage.Source = UprightRumpSource.Standing(closed);
            BodyScaleTransform.ScaleX = snapshot.Facing == FacingDirection.Left ? -1 : 1;
            BodyScaleTransform.ScaleY = 1;
            BodyTranslateTransform.Y = 0;
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
        ApplyHunting(snapshot, directInteraction, ordinaryEyesClosed);
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
            Phase: DirectInteractionPhase.BodyPending,
            StartsHanging: false
        };

    private static bool IsBodyDragPresentationActive(DirectInteractionSnapshot directInteraction) =>
        directInteraction is
        {
            Target: DirectInteractionTarget.Body,
            Phase: DirectInteractionPhase.BodyPending,
            StartsHanging: true
        } || directInteraction is
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
            DirectInteractionPhase.BodyPending when directInteraction.StartsHanging => BodyDragFrames.Sample(1),
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

        if (directInteraction.Phase == DirectInteractionPhase.BodyDragSettle && _headAnchor is not null)
        {
            _headRecovery ??= BodyDragFrames.CreateRecovery(_lastHeadSource ?? source, CanonicalFrame);
            source = _headRecovery(directInteraction.ReleaseProgress);
        }
        else
        {
            _lastHeadSource = source;
            _headRecovery = null;
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
            // Release returns to the rest sampling mode before impact, avoiding
            // a contour-weight switch halfway through a short-drop rebound.
            // Held poses retain their approved upright/tilted sampling.
            RenderOptions.SetBitmapScalingMode(DororongImage,
                directInteraction.Phase == DirectInteractionPhase.BodyDragSettle
                    ? _beforeHeadSampling
                    : BodyRotateTransform.Angle != 0 ? BitmapScalingMode.Linear
                    : BodyDragFrames.AnatomicalKey(source) is null ? _beforeHeadSampling : BitmapScalingMode.NearestNeighbor);
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
        if(_edgePerch.IsAttached)
        {
            if(!opaque)return DirectInteractionTarget.None;
            return InteractionHitMap.IsSelectedCheek(new(sourcePosition.X-8,sourcePosition.Y+6))
                ? DirectInteractionTarget.RightCheek : DirectInteractionTarget.Body;
        }
        if (!opaque || !InteractionHitMap.InSource(sourcePosition)) return DirectInteractionTarget.None;
        if (CanUseBodyMap && DororongImage.Source is BitmapSource source)
        {
            var pixels = PremultipliedFrame.From(source).Pixels;
            if (pixels[((int)sourcePosition.Y * 96 + (int)sourcePosition.X) * 4 + 3] == 0)
                return DirectInteractionTarget.None;
            if (ClassifyHuntingHead(sourcePosition) is { } huntingHead) return huntingHead;
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
        RestorePounceTransform();
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
        var image=ResolvePrimaryPressedImage(e.OriginalSource);
        var press=CreateDirectPress(image,e.GetPosition(image));
        if(press is null)return;
        DirectInteractionPressed?.Invoke(this,press);
        e.Handled=true;
    }

    internal DirectInteractionPressEventArgs? CreateDirectPress(AlphaHitTestImage pressedImage, Point imagePosition)
    {
        var perch = _edgePerch.VisibleImage;
        var extreme = _extremeLanding.VisibleImage;
        if (!pressedImage.TryGetOpaqueSourcePoint(imagePosition, out var sourcePosition))
        {
            return null;
        }

        var perchHit = ReferenceEquals(pressedImage, perch);
        var splat = ReferenceEquals(pressedImage, extreme);
        var framePosition = perchHit ? new PointD(48, 48) : splat ? new PointD(40,40) : GetVisibleFramePoint(sourcePosition);
        var target = splat || (perchHit && !_edgePerch.IsAttached) ? DirectInteractionTarget.Body : ClassifyOpaqueSourcePoint(sourcePosition, opaque: true);
        // Keep the old region map for rendering diagnostics, but no product
        // input may start an arm/belly/rump pull or fall back to head carry.
        if (target == DirectInteractionTarget.FiveRegionBody)
            target = DirectInteractionTarget.ClickOnly;
        if (perchHit && _edgePerch.HitTestPaw(sourcePosition) is { } rightPaw)
            target = rightPaw ? DirectInteractionTarget.PerchRightPaw : DirectInteractionTarget.PerchLeftPaw;
        if (splat && !InteractionHitMap.IsUpperHead(new(sourcePosition.X - 32, sourcePosition.Y - 32)))
            target = DirectInteractionTarget.ClickOnly;
        if (target == DirectInteractionTarget.Body && perchHit &&
            !InteractionHitMap.IsUpperHead(new(sourcePosition.X - 8, sourcePosition.Y + 6)))
            target = DirectInteractionTarget.ClickOnly;
        if (target == DirectInteractionTarget.Body && !perchHit && !splat && !CanUseBodyMap &&
            !_activeInteractionDescriptor.IsHeadOrCheek(sourcePosition))
            target = DirectInteractionTarget.ClickOnly;
        if (target == DirectInteractionTarget.None)
        {
            return null;
        }
        _pendingHeadCaptureImage = target == DirectInteractionTarget.Body ? pressedImage : null;

        var windowPosition = pressedImage.TranslatePoint(imagePosition,this);
        var facing=perchHit ? _edgePerch.Facing : _activeFacing;
        CheekPullCapture? cheekCapture = null;
        if (target == DirectInteractionTarget.RightCheek && !TryCreateCheekPullCapture(sourcePosition, out cheekCapture)) return null;
        return new DirectInteractionPressEventArgs(
                target,
                new PointD(windowPosition.X, windowPosition.Y),
                framePosition,
                _activeInteractionDescriptor.GetScreenOutwardSign(target, facing)) { CheekCapture = cheekCapture, PressFacing=facing,
                    IsAttachedCheek=perchHit && _edgePerch.IsAttached && cheekCapture is not null,
                    StartsHanging=perchHit && _edgePerch.IsAttached && target == DirectInteractionTarget.Body };
    }

    private void OnSitClicked(object sender, RoutedEventArgs e) => SitRequested?.Invoke(this, EventArgs.Empty);

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}
