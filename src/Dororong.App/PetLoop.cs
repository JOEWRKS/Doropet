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
            var releasedWholeCarry = _primaryWasDown && !primaryButtonDown && _directInteractionController.IsWholeCarry;
            var releasedFacing = _directInteractionController.Current.PressFacing;
            var attachedCheek = _directInteractionController.Current.IsAttachedCheek ||
                (_queuedDirectPress is { IsAttachedCheek: true } && platforms?.PerchPhase == EdgePerchPhase.Attached);
            var capturedAtEntry = OwnsCapturedPosition(_snapshot, _directInteractionController.Current);
            var canBeginPress = _queuedDirectPress is not null &&
                _directInteractionController.Current.Target == DirectInteractionTarget.None;
            var directAtEntry = platforms is null
                ? OwnsPosition(_snapshot, _directInteractionController.Current) || _queuedDirectPress is not null
                : !attachedCheek && primaryButtonDown && (capturedAtEntry || canBeginPress);
            var pointer = _host.SamplePointer();
            if (platforms is not null)
            {
                // A release (including a press+release between two ticks) drops
                // the old surface owner before its pending translation can apply.
                if (!attachedCheek && !primaryButtonDown && (canBeginPress || capturedAtEntry)) platforms.Reset(displayed);
                pointer = platforms.BeginFrame(elapsed, displayed, pointer, directAtEntry,
                    attachedCheek || _directInteractionController.Current.Target == DirectInteractionTarget.None,
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
                    else _directInteractionController.BeginCheekCarry(cheekCapture, windowPosition, globalPressPosition);
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
                if (queuedPress.Target == DirectInteractionTarget.Body)
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
                SuspendAutonomousMotion: platforms?.SuspendsAutonomousMotion ?? false,
                SurfaceBoundMotion: platforms is not null));
            var directCurrent = _directInteractionController.Advance(
                delta,
                pointer,
                primaryButtonDown,
                previous.State,
                current.State);

            // The release tick must first let the brain leave DRAGGED. Later
            // landing positions use its existing local-interaction position seam.
            if (platforms is null)
                directCurrent = _directInteractionController.BeginHeadLanding(current.Position, workArea, petSize);

            UpdateMouseCapture(current, directCurrent);
            PlatformPose? platformPose = null;
            double? targetSole = null;
            if (platforms is not null)
            {
                var directOwns = !attachedCheek && primaryButtonDown && OwnsCapturedPosition(current, directCurrent);
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
                    if(attachedCheek)
                    {
                        _directInteractionController.Cancel();brain.CancelDirectInteraction();
                        directCurrent=DirectInteractionSnapshot.None;current=brain.Current;
                        ReleaseMouseCapture();
                    }
                    targetSole = platforms.Contact.SoleY;
                    var clickHop = current.State == PetState.ClickReaction
                        ? BodyClickTransformSampler.SampleConfirmedClick(current.Phase).TranslationY : 0;
                    var pose = platforms.Advance(elapsed, delta, displayed, current.Position, platforms.Contact, directOwns, clickHop);
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
                    pointer.IsAvailable) == true
            };
            _host.SetWindowPosition(current.Position);
            _host.Render(current, directCurrent, delta);
            platforms?.AfterRender(platformPose, targetSole);
            if (platforms?.PerchPhase is EdgePerchPhase.Entering or EdgePerchPhase.Attached)
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
            presenter.Render,
            presenter.CaptureMouse,
            presenter.ReleaseMouseCapture,
            presenter.Render)
        {
            MaintainPerchLayer = () => PerchTaskbarLayerGuard.EnsureAboveOverlappingTaskbar(source.Handle),
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
