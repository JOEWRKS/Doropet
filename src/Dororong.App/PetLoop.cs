using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Interop;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App;

internal sealed class PetLoop : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(16);
    private const double InitialMargin = 32;

    private readonly PetLoopClock _clock;
    private readonly PetLoopTimer _timer;
    private readonly PetLoopHost _host;
    private readonly Func<PointD, PetBrain> _brainFactory;
    private readonly EventHandler _tickHandler;
    private readonly PetLoopLifecycle _lifecycle = new();
    private readonly DirectInteractionController _directInteractionController = new();
    private DirectInteractionPressEventArgs? _queuedDirectPress;
    private PetBrain? _brain;
    private PetSnapshot _snapshot;
    private TimeSpan _lastElapsed;
    private bool _isMouseCaptured;
    private bool _releasingMouseCapture;
    private bool _timerAttached;
    private bool _timerStarted;
    private bool _clockStarted;
    private bool _cleanupComplete;
    private bool _primaryWasDown;
    private bool _sitRequested;
    private double _lastPounceOffset;
    private CheekPullCapture? _hopCapture;
    private double _lastCheekHopX, _lastCheekHopY;
    private double _cheekHopBaseY;

    public PetLoop(Window window, DororongPresenter presenter, DesktopInput input)
        : this(
            CreateProductionRuntime(window, presenter, input),
            initialPosition => new PetBrain(
                BehaviorTuning.Default,
                new SeededRandomSource(),
                initialPosition))
    {
    }

    private PetLoop(
        ProductionRuntime runtime,
        Func<PointD, PetBrain> brainFactory)
        : this(runtime.Clock, runtime.Timer, runtime.Host, brainFactory)
    {
    }

    internal PetLoop(
        PetLoopClock clock,
        PetLoopTimer timer,
        PetLoopHost host,
        Func<PointD, PetBrain> brainFactory)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timer = timer ?? throw new ArgumentNullException(nameof(timer));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _brainFactory = brainFactory ?? throw new ArgumentNullException(nameof(brainFactory));
        _tickHandler = OnTick;
    }

    public event EventHandler<Exception>? Faulted;

    internal void NotifySitRequested()
    {
        if (_lifecycle.Phase != PetLoopPhase.Running || _queuedDirectPress is not null ||
            _snapshot.IsDirectInteractionPending || _directInteractionController.Current.Target != DirectInteractionTarget.None)
            return;
        _sitRequested = true;
        _host.SetSittingRequested?.Invoke(true);
    }

    public void Start()
    {
        if (!_lifecycle.TryStart())
        {
            return;
        }

        try
        {
            var petSize = _host.GetPetSize();
            var platforms = _host.Platforms;
            if (platforms is not null)
                platforms.BeginFrame(_clock.Elapsed, _host.GetWindowPosition(), _host.SamplePointer(), false);
            var workArea = platforms?.GetMovementArea(_host.GetWindowPosition(), platforms.Contact, petSize) ?? _host.GetWorkArea();
            var initialPosition = workArea.ClampTopLeft(
                new PointD(
                    workArea.Right - petSize.Width - InitialMargin,
                    workArea.Bottom - petSize.Height - InitialMargin),
                petSize);

            _brain = _brainFactory(initialPosition)
                ?? throw new InvalidOperationException("The behavior brain factory returned null.");
            _snapshot = _brain.Current;
            _host.SetWindowPosition(_snapshot.Position);
            _host.SetLocomotionBlocked?.Invoke(platforms?.SuspendsAutonomousMotion ?? false);
            _host.Render(_snapshot, _directInteractionController.Current);
            _host.Platforms?.AfterRender(null, null);

            _timer.Attach(_tickHandler);
            _timerAttached = true;
            _clock.Start();
            _clockStarted = true;
            _lastElapsed = _clock.Elapsed;
            _timer.Start();
            _timerStarted = true;
            _primaryWasDown = _host.IsPrimaryButtonDown();
        }
        catch (Exception exception)
        {
            HandleFault(exception);
        }
    }

    internal void NotifyDirectInteractionPressed(DirectInteractionPressEventArgs press)
    {
        if (_lifecycle.Phase != PetLoopPhase.Running)
        {
            return;
        }

        _queuedDirectPress = press ?? throw new ArgumentNullException(nameof(press));
    }

    internal void NotifyDirectInteractionCanceled()
    {
        if (_releasingMouseCapture) return;
        if (_queuedDirectPress is null && !_isMouseCaptured && _directInteractionController.Current.Target == DirectInteractionTarget.None)
        {
            return;
        }

        _queuedDirectPress = null;
        _directInteractionController.Cancel();
        if (_brain is not null)
        {
            _brain.CancelDirectInteraction();
            _snapshot = _brain.Current;
        }
        ReleaseMouseCapture();
        if (_brain is not null) _host.Render(_snapshot, DirectInteractionSnapshot.None);
        _host.Platforms?.Clear(_host.GetWindowPosition());
    }

    public void Dispose()
    {
        if (!_lifecycle.TryDispose() || _cleanupComplete)
        {
            return;
        }

        var cleanupException = StopAndDetach();
        _cleanupComplete = true;
        GC.SuppressFinalize(this);
        if (cleanupException is not null)
        {
            throw cleanupException;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_lifecycle.Phase != PetLoopPhase.Running)
        {
            return;
        }

        try
        {
            var brain = _brain
                ?? throw new InvalidOperationException("The behavior brain has not been created.");
            var elapsed = _clock.Elapsed;
            var delta = elapsed - _lastElapsed;
            _lastElapsed = elapsed;

            var displayed = _host.GetWindowPosition();
            var platforms = _host.Platforms;
            var primaryButtonDown = _host.IsPrimaryButtonDown();
            // A released local paw must not swallow a new head/cheek/paw press.
            var interruptedPaw = _queuedDirectPress is not null &&
                _directInteractionController.Current.Phase == DirectInteractionPhase.PawRelease
                ? _directInteractionController.Current.PawPull : null;
            if (interruptedPaw is not null) _directInteractionController.Cancel();
            var releasedWholeCarry = _primaryWasDown && !primaryButtonDown && _directInteractionController.IsWholeCarry;
            var releasedFacing = _directInteractionController.Current.PressFacing;
            var attachedCheek = _directInteractionController.Current.IsAttachedCheek ||
                (_queuedDirectPress is { IsAttachedCheek: true } && platforms?.PerchPhase == EdgePerchPhase.Attached);
            var attachedPaw = _directInteractionController.Current.PawPull is not null ||
                (_queuedDirectPress?.Target is DirectInteractionTarget.PerchLeftPaw or DirectInteractionTarget.PerchRightPaw &&
                 platforms?.PerchPhase==EdgePerchPhase.Attached);
            var seatedCheek = _sitRequested && !attachedCheek &&
                (_directInteractionController.Current.IsSeatedCheek ||
                 _queuedDirectPress is { Target: DirectInteractionTarget.RightCheek, CheekCapture: not null });
            var clickOnly = _queuedDirectPress?.Target == DirectInteractionTarget.ClickOnly ||
                _directInteractionController.Current.Target == DirectInteractionTarget.ClickOnly;
            // Cheek capture owns the stretch only, never the desktop position.
            // Keep support physics active and never enter carry/perch readiness.
            var localCheek = _directInteractionController.Current.Target is DirectInteractionTarget.LeftCheek or DirectInteractionTarget.RightCheek ||
                _queuedDirectPress?.Target is DirectInteractionTarget.LeftCheek or DirectInteractionTarget.RightCheek;
            var stationaryCheek = attachedCheek || attachedPaw || seatedCheek || localCheek || clickOnly;
            var capturedAtEntry = OwnsCapturedPosition(_snapshot, _directInteractionController.Current);
            var canBeginPress = _queuedDirectPress is not null &&
                _directInteractionController.Current.Target == DirectInteractionTarget.None;
            var directAtEntry = platforms is null
                ? OwnsPosition(_snapshot, _directInteractionController.Current) || _queuedDirectPress is not null
                : !stationaryCheek && primaryButtonDown && (capturedAtEntry || canBeginPress);
            var pointer = _host.SamplePointer();
            if (platforms is not null)
            {
                // A release (including a press+release between two ticks) drops
                // the old surface owner before its pending translation can apply.
                if (!stationaryCheek && !primaryButtonDown && (canBeginPress || capturedAtEntry)) platforms.Reset(displayed);
                pointer = platforms.BeginFrame(elapsed, displayed, pointer, directAtEntry,
                    stationaryCheek || _directInteractionController.Current.Target == DirectInteractionTarget.None,
                    _directInteractionController.Current is
                        { Phase: DirectInteractionPhase.BodyDragSettle, IsPartialDragSettle: true });
                if (!directAtEntry)
                {
                    displayed=platforms.FramePosition;
                    _snapshot = brain.ApplyPlatformPosition(displayed);
                }
            }
            var queuedPress = _queuedDirectPress;
            _queuedDirectPress = null;
            PointD? bodyPress = null;
            if (queuedPress is not null &&
                _directInteractionController.Current.Target == DirectInteractionTarget.None)
            {
                var windowPosition = _host.GetWindowPosition();
                var globalPressPosition = windowPosition + queuedPress.WindowLocalPosition;
                if (queuedPress.Target == DirectInteractionTarget.FiveRegionBody && queuedPress.BodyCapture is { } bodyCapture)
                {
                    _directInteractionController.BeginBodyPull(bodyCapture, windowPosition, globalPressPosition);
                }
                else if (queuedPress.Target == DirectInteractionTarget.RightCheek && queuedPress.CheekCapture is { } cheekCapture)
                {
                    if(attachedCheek) _directInteractionController.BeginCheekPull(cheekCapture,globalPressPosition);
                    else if(seatedCheek) _directInteractionController.BeginSeatedCheekPull(cheekCapture,globalPressPosition);
                    else _directInteractionController.BeginCheekPull(cheekCapture, globalPressPosition);
                }
                else if (queuedPress.Target is DirectInteractionTarget.PerchLeftPaw or DirectInteractionTarget.PerchRightPaw)
                {
                    if(attachedPaw)_directInteractionController.BeginPerchPaw(queuedPress.Target,globalPressPosition,interruptedPaw);
                }
                else if (queuedPress.Target == DirectInteractionTarget.Body)
                {
                    _directInteractionController.BeginDistanceBody(globalPressPosition, _host.GetDragThreshold());
                }
                else
                {
                    _directInteractionController.Begin(queuedPress.Target, globalPressPosition, queuedPress.OutwardSign);
                }
                _directInteractionController.SetPressContext(queuedPress.PressFacing, attachedCheek, queuedPress.StartsHanging);
                if (queuedPress.Target is DirectInteractionTarget.Body or DirectInteractionTarget.ClickOnly)
                {
                    bodyPress = globalPressPosition;
                }
            }

            var petSize = _host.GetPetSize();
            var workArea = platforms?.GetMovementArea(displayed, platforms.Contact, petSize) ?? _host.GetWorkArea();
            var bodyWasActive = _directInteractionController.Current.Target == DirectInteractionTarget.FiveRegionBody;
            var cheekWasActive = _directInteractionController.HasCheekCarry;
            PointD? bodyPosition = bodyWasActive
                ? _directInteractionController.AdvanceBodyPull(delta, pointer, primaryButtonDown, workArea, petSize)
                : cheekWasActive
                    ? _directInteractionController.AdvanceCheekCarry(delta, pointer, primaryButtonDown, workArea, petSize)
                    : _directInteractionController.AdvanceHeadLanding(delta, workArea, petSize);
            // Released shape recovery is visual only. Physics must not receive
            // the captured carry session's now-stale window position.
            if (platforms is not null && (!primaryButtonDown ||
                !OwnsCapturedPosition(_snapshot, _directInteractionController.Current))) bodyPosition = null;
            var directBeforeCore = _directInteractionController.Current;
            var previous = _snapshot;
            _host.SetLocomotionBlocked?.Invoke(platforms?.SuspendsAutonomousMotion ?? false);
            var holdHunt = _host.UpdateHunting?.Invoke(previous, directBeforeCore, pointer, delta,
                primaryButtonDown || queuedPress is not null) ?? false;
            var pounce = _host.GetPouncePose?.Invoke() ?? PouncePose.Rest;
            // Sit/body clicks retire the jump without taking carry ownership.
            // Release its retained support offset into real falling physics;
            // otherwise clearing the offset teleports the pet down by up to12DIP.
            if (!pounce.IsJump && _lastPounceOffset < 0 &&
                (primaryButtonDown || _sitRequested || clickOnly) &&
                (stationaryCheek || !OwnsCapturedPosition(previous, directBeforeCore)))
                platforms?.Reset(displayed);
            var holdWalk = directBeforeCore.Target == DirectInteractionTarget.None &&
                queuedPress is null && (_host.HoldLocomotionWalk?.Invoke(previous) ?? false);
            var current = brain.Update(new PetInput(
                delta,
                workArea,
                petSize,
                pointer,
                primaryButtonDown,
                bodyWasActive || cheekWasActive || IsLocalInteractionActive(directBeforeCore),
                bodyPress,
                _host.GetDragThreshold(),
                bodyPosition,
                DistanceDrivenBodyDrag: true,
                SuspendAutonomousMotion: (platforms?.SuspendsAutonomousMotion ?? false) || holdWalk || holdHunt ||
                    (_sitRequested && previous.State != PetState.ClickReaction),
                SurfaceBoundMotion: platforms is not null,
                SuppressPointerReactions: _host.UpdateHunting is not null,
                TrackPointerFacing: holdHunt,
                ClickOnlyPress: queuedPress?.Target == DirectInteractionTarget.ClickOnly,
                TrackingFacingOverride: pounce.IsJump ? pounce.Direction : null));
            // The idle-to-walk boundary may occur inside Update. Retain support
            // position on that first tick too, before platform physics advances.
            if (directBeforeCore.Target == DirectInteractionTarget.None && queuedPress is null &&
                (_host.HoldLocomotionWalk?.Invoke(current) ?? false))
                current = brain.ApplyPlatformPosition(displayed);
            if (pounce.IsEngaged)
                current = brain.ApplyPlatformPosition(workArea.ClampTopLeft(current.Position +
                    new PointD(pounce.DeltaX, platforms is null ? pounce.OffsetY - _lastPounceOffset : 0), petSize));
            _lastPounceOffset = pounce.OffsetY;
            var directCurrent = _directInteractionController.Advance(
                delta,
                pointer,
                primaryButtonDown,
                previous.State,
                current.State);

            var cheekHop = directCurrent is { Phase: DirectInteractionPhase.CheekRelease, IsAttachedCheek: false,
                CheekPull: { SpringRelease: true } activeHop } ? activeHop : null;
            // Advance can retire the capture between timer samples. Consume the
            // exact last horizontal delta once; never reverse it on retirement.
            if (cheekHop is null && directCurrent.Target == DirectInteractionTarget.None &&
                directBeforeCore is { Phase: DirectInteractionPhase.CheekRelease, IsAttachedCheek: false,
                    CheekPull: { SpringRelease: true } finishedHop })
                cheekHop = finishedHop with { RecoilOffset=CheekSpringMotion.Offset(
                    CheekSpringMotion.DurationMilliseconds,finishedHop.ReleasedPullDips),RecoilLift=0 };
            var cheekHopY = 0d;
            if (cheekHop is not null)
            {
                if (!ReferenceEquals(_hopCapture,cheekHop.Capture))
                { _hopCapture=cheekHop.Capture; _lastCheekHopX=0; _lastCheekHopY=0; _cheekHopBaseY=current.Position.Y; }
                var x=-Math.Sign(cheekHop.Capture.OutwardUnit.X)*cheekHop.RecoilOffset;
                cheekHopY=-cheekHop.RecoilLift;
                current=brain.ApplyPlatformPosition(workArea.ClampTopLeft(new PointD(
                    current.Position.X+x-_lastCheekHopX,
                    platforms is null?_cheekHopBaseY+cheekHopY:current.Position.Y),petSize));
                _lastCheekHopX=x;_lastCheekHopY=cheekHopY;
            }
            else
            {
                if(_lastCheekHopY<0)platforms?.Reset(displayed);
                _hopCapture=null;_lastCheekHopX=0;_lastCheekHopY=0;
            }

            // A press or local cheek tug is not a move. Release the requested
            // hold only when the head crosses its drag threshold or another
            // body part actually enters whole-character carry.
            if (_sitRequested && primaryButtonDown &&
                (current.State == PetState.Dragged || _directInteractionController.IsWholeCarry))
            {
                _sitRequested = false;
                _host.SetSittingRequested?.Invoke(false);
            }

            // The release tick must first let the brain leave DRAGGED. Later
            // landing positions use its existing local-interaction position seam.
            if (platforms is null)
                directCurrent = _directInteractionController.BeginHeadLanding(current.Position, workArea, petSize);

            UpdateMouseCapture(current, directCurrent);
            PlatformPose? platformPose = null;
            double? targetSole = null;
            if (platforms is not null)
            {
                var directOwns = !stationaryCheek && primaryButtonDown && OwnsCapturedPosition(current, directCurrent);
                var beganPerch = platforms.TryBeginPerch(displayed,releasedFacing ?? current.Facing,releasedWholeCarry,pointer.IsAvailable);
                if(beganPerch)
                {
                    _directInteractionController.Cancel();brain.CancelDirectInteraction();directCurrent=DirectInteractionSnapshot.None;
                    ReleaseMouseCapture();
                }
                if(platforms.TryAdvancePerch(beganPerch ? TimeSpan.Zero : delta,displayed,out var perchPosition))
                {
                    current=brain.ApplyPlatformPosition(perchPosition);
                }
                else
                {
                    if(attachedCheek || attachedPaw)
                    {
                        _directInteractionController.Cancel();brain.CancelDirectInteraction();
                        directCurrent=DirectInteractionSnapshot.None;current=brain.Current;
                        ReleaseMouseCapture();
                    }
                    targetSole = platforms.Contact.SoleY;
                    var clickHop = current.State == PetState.ClickReaction
                        ? BodyClickTransformSampler.SampleConfirmedClick(current.Phase).TranslationY : 0;
                    var pose = platforms.Advance(elapsed, delta, displayed, current.Position, platforms.Contact, directOwns,
                        cheekHop is not null ? cheekHopY : pounce.IsJump ? pounce.OffsetY : clickHop);
                    if (!directOwns)
                    {
                        current = brain.ApplyPlatformPosition(pose.Position);
                        platformPose = pose;
                    }
                }
            }
            directCurrent = directCurrent with
            {
                IsPerchReady = platforms?.CanBeginPerch(current.Position,
                    directCurrent.PressFacing ?? current.Facing,
                    primaryButtonDown && _directInteractionController.IsWholeCarry,
                    pointer.IsAvailable, advertise: true) == true
            };
            // Strip only the final render snapshot: legacy BeginHeadLanding
            // above may return the controller snapshot again. HWND owns the hop.
            if(cheekHop is not null && directCurrent.CheekPull is { } renderedCheek)
                directCurrent=directCurrent with { CheekPull=renderedCheek with { RecoilOffset=0,RecoilLift=0 } };
            _host.SetWindowPosition(current.Position);
            _host.SetLocomotionBlocked?.Invoke(platforms?.SuspendsAutonomousMotion ?? false);
            _host.Render(current, directCurrent, delta);
            platforms?.AfterRender(platformPose, targetSole);
            if (directCurrent.IsPerchReady ||
                platforms?.PerchPhase is EdgePerchPhase.Entering or EdgePerchPhase.Attached)
                _host.MaintainPerchLayer?.Invoke();
            _snapshot = current;
            _primaryWasDown=primaryButtonDown;
        }
        catch (Exception exception)
        {
            HandleFault(exception);
        }
    }

    private void UpdateMouseCapture(
        PetSnapshot current,
        DirectInteractionSnapshot directInteraction)
    {
        var requiresCapture = current.State == PetState.Dragged || directInteraction.RequiresCapture;
        if (requiresCapture && !_isMouseCaptured)
        {
            if (!_host.CaptureMouse())
            {
                throw new InvalidOperationException("Dororong could not capture the mouse for direct interaction.");
            }

            _isMouseCaptured = true;
        }
        else if (!requiresCapture)
        {
            ReleaseMouseCapture();
        }
    }

    private static bool IsLocalInteractionActive(DirectInteractionSnapshot directInteraction) =>
        directInteraction.PawPull is not null ||
        directInteraction.Target is DirectInteractionTarget.LeftCheek or DirectInteractionTarget.RightCheek or DirectInteractionTarget.FiveRegionBody ||
        directInteraction.Phase == DirectInteractionPhase.BodyDragSettle;

    private static bool OwnsPosition(PetSnapshot core, DirectInteractionSnapshot direct) =>
        core.State == PetState.Dragged || core.IsDirectInteractionPending ||
        direct.Target != DirectInteractionTarget.None || direct.HeadLanding is not null;

    private static bool OwnsCapturedPosition(PetSnapshot core, DirectInteractionSnapshot direct) =>
        core.State == PetState.Dragged || core.IsDirectInteractionPending || direct.RequiresCapture ||
        direct.Phase == DirectInteractionPhase.BodyPending;

    private void HandleFault(Exception exception)
    {
        if (!_lifecycle.TryFault())
        {
            return;
        }

        var cleanupException = StopAndDetach();
        _cleanupComplete = true;
        Faulted?.Invoke(
            this,
            cleanupException is null
                ? exception
                : new AggregateException(exception, cleanupException).Flatten());
    }

    private Exception? StopAndDetach()
    {
        var timerStarted = _timerStarted;
        var timerAttached = _timerAttached;
        var clockStarted = _clockStarted;
        _timerStarted = false;
        _timerAttached = false;
        _clockStarted = false;

        return CleanupSequence.Run(
            () =>
            {
                if (timerStarted)
                {
                    _timer.Stop();
                }
            },
            () =>
            {
                if (timerAttached)
                {
                    _timer.Detach(_tickHandler);
                }
            },
            () =>
            {
                if (clockStarted)
                {
                    _clock.Stop();
                }
            },
            ReleaseMouseCapture,
            () => _host.Platforms?.Clear(_host.GetWindowPosition()),
            () =>
            {
                if (_directInteractionController.Current.Target is DirectInteractionTarget.FiveRegionBody or DirectInteractionTarget.Body ||
                    _directInteractionController.Current.CheekPull is not null)
                {
                    if (_brain is not null)
                    {
                        _brain.CancelDirectInteraction();
                        _snapshot = _brain.Current;
                    }
                    _host.Render(_snapshot, DirectInteractionSnapshot.None);
                }
            },
            () =>
            {
                _lastElapsed = TimeSpan.Zero;
                _queuedDirectPress = null;
                _directInteractionController.Cancel();
                _brain = null;
            });
    }

    private void ReleaseMouseCapture()
    {
        if (!_isMouseCaptured)
        {
            return;
        }

        _isMouseCaptured = false;
        _releasingMouseCapture = true;
        try { _host.ReleaseMouseCapture(); }
        finally { _releasingMouseCapture = false; }
    }

    private static ProductionRuntime CreateProductionRuntime(
        Window window,
        DororongPresenter presenter,
        DesktopInput input)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(input);

        var source = PresentationSource.FromVisual(window) as HwndSource
            ?? throw new InvalidOperationException("The WPF window source is unavailable.");
        var stopwatch = new Stopwatch();
        var viewport = new PetWindowViewport(window, presenter);
        var dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal, window.Dispatcher)
        {
            Interval = TickInterval
        };

        var clock = new PetLoopClock(
            () => stopwatch.Elapsed,
            stopwatch.Start,
            stopwatch.Stop);
        var timer = new PetLoopTimer(
            handler => dispatcherTimer.Tick += handler,
            handler => dispatcherTimer.Tick -= handler,
            dispatcherTimer.Start,
            dispatcherTimer.Stop);
        var host = new PetLoopHost(
            GetWorkArea,
            viewport.GetPetSize,
            () => new SizeD(
                SystemParameters.MinimumHorizontalDragDistance,
                SystemParameters.MinimumVerticalDragDistance),
            () => input.SamplePointer(source),
            input.IsPrimaryButtonDown,
            viewport.GetPosition,
            viewport.SetPosition,
            presenter.RenderDesktop,
            presenter.CaptureMouse,
            presenter.ReleaseMouseCapture,
            presenter.RenderDesktop)
        {
            MaintainPerchLayer = () => PerchTaskbarLayerGuard.EnsureAboveOverlappingTaskbar(source.Handle),
            HoldLocomotionWalk = presenter.HoldLocomotionWalk,
            SetLocomotionBlocked = presenter.SetLocomotionBlocked,
            SetSittingRequested = presenter.SetSittingRequested,
            UpdateHunting = presenter.UpdateHuntingWithPounce,
            GetPouncePose = () => presenter.Pounce,
            Platforms = new PetPlatformRuntime(new DesktopSceneSource(new DesktopSceneNative(source.Handle), background: true),
                position => DesktopCoordinateMap.FromPresenter(presenter, position),
                presenter.MeasurePlatformGeometry, presenter.ApplyPlatformPose, DesktopMetadataReader.ReadMonitorBounds,
                presenter.MeasureEdgePerchContact, presenter.ApplyEdgePerch)
        };

        return new ProductionRuntime(clock, timer, host);
    }

    private static RectD GetWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        return new RectD(workArea.X, workArea.Y, workArea.Width, workArea.Height);
    }

    private sealed record ProductionRuntime(
        PetLoopClock Clock,
        PetLoopTimer Timer,
        PetLoopHost Host);
}
