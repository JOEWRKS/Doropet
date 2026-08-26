using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Dororong.App.Controls;
using Dororong.App.Interop;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App;

internal sealed class PetLoop : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(33);
    private const double InitialMargin = 32;

    private readonly PetLoopClock _clock;
    private readonly PetLoopTimer _timer;
    private readonly PetLoopHost _host;
    private readonly Func<PointD, PetBrain> _brainFactory;
    private readonly EventHandler _tickHandler;
    private readonly PetLoopLifecycle _lifecycle = new();
    private readonly BodyPressQueue _bodyPressQueue = new();
    private PetBrain? _brain;
    private PetSnapshot _snapshot;
    private TimeSpan _lastElapsed;
    private bool _isMouseCaptured;
    private bool _timerAttached;
    private bool _timerStarted;
    private bool _clockStarted;
    private bool _cleanupComplete;

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
            var workArea = _host.GetWorkArea();
            var petSize = _host.GetPetSize();
            var initialPosition = workArea.ClampTopLeft(
                new PointD(
                    workArea.Right - petSize.Width - InitialMargin,
                    workArea.Bottom - petSize.Height - InitialMargin),
                petSize);

            _brain = _brainFactory(initialPosition)
                ?? throw new InvalidOperationException("The behavior brain factory returned null.");
            _snapshot = _brain.Current;
            _host.SetWindowPosition(_snapshot.Position);
            _host.Render(_snapshot);

            _timer.Attach(_tickHandler);
            _timerAttached = true;
            _clock.Start();
            _clockStarted = true;
            _lastElapsed = _clock.Elapsed;
            _timer.Start();
            _timerStarted = true;
        }
        catch (Exception exception)
        {
            HandleFault(exception);
        }
    }

    public void NotifyBodyPressed(PointD localPosition)
    {
        if (_lifecycle.Phase != PetLoopPhase.Running)
        {
            return;
        }

        var windowPosition = _host.GetWindowPosition();
        _bodyPressQueue.Enqueue(new PointD(
            windowPosition.X + localPosition.X,
            windowPosition.Y + localPosition.Y));
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

            var pointer = _host.SamplePointer();
            var primaryButtonDown = _host.IsPrimaryButtonDown();
            var bodyPress = _bodyPressQueue.Consume();
            var previous = _snapshot;
            var current = brain.Update(new PetInput(
                delta,
                _host.GetWorkArea(),
                _host.GetPetSize(),
                pointer,
                primaryButtonDown,
                bodyPress,
                _host.GetDragThreshold()));

            UpdateMouseCapture(previous, current);
            _host.SetWindowPosition(current.Position);
            _host.Render(current);
            _snapshot = current;
        }
        catch (Exception exception)
        {
            HandleFault(exception);
        }
    }

    private void UpdateMouseCapture(PetSnapshot previous, PetSnapshot current)
    {
        switch (MouseCaptureTransition.Decide(previous.State, current.State))
        {
            case MouseCaptureChange.Capture when !_host.CaptureMouse():
                throw new InvalidOperationException("Dororong could not capture the mouse for dragging.");

            case MouseCaptureChange.Capture:
                _isMouseCaptured = true;
                break;

            case MouseCaptureChange.Release:
                ReleaseMouseCapture();
                break;

            case MouseCaptureChange.None:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(current), current.State, "Unknown capture transition.");
        }
    }

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
            () =>
            {
                _lastElapsed = TimeSpan.Zero;
                _bodyPressQueue.Clear();
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
        _host.ReleaseMouseCapture();
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
            () => new SizeD(window.ActualWidth, window.ActualHeight),
            () => new SizeD(
                SystemParameters.MinimumHorizontalDragDistance,
                SystemParameters.MinimumVerticalDragDistance),
            () => input.TryGetPointerInDips(source, out var pointerPosition)
                ? new PointerSample(true, pointerPosition)
                : PointerSample.Unavailable,
            input.IsPrimaryButtonDown,
            () => new PointD(window.Left, window.Top),
            position =>
            {
                window.Left = position.X;
                window.Top = position.Y;
            },
            presenter.Render,
            presenter.CaptureMouse,
            presenter.ReleaseMouseCapture);

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
