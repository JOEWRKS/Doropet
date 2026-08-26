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

    private readonly Window _window;
    private readonly DororongPresenter _presenter;
    private readonly DesktopInput _input;
    private readonly PetLoopLifecycle _lifecycle = new();
    private readonly BodyPressQueue _bodyPressQueue = new();
    private DispatcherTimer? _timer;
    private Stopwatch? _stopwatch;
    private HwndSource? _windowSource;
    private PetBrain? _brain;
    private PetSnapshot _snapshot;
    private TimeSpan _lastElapsed;
    private bool _isMouseCaptured;
    private bool _cleanupComplete;

    public PetLoop(Window window, DororongPresenter presenter, DesktopInput input)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _input = input ?? throw new ArgumentNullException(nameof(input));
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
            var workArea = GetWorkArea();
            var petSize = GetPetSize();
            var initialPosition = workArea.ClampTopLeft(
                new PointD(
                    workArea.Right - petSize.Width - InitialMargin,
                    workArea.Bottom - petSize.Height - InitialMargin),
                petSize);

            _windowSource = PresentationSource.FromVisual(_window) as HwndSource
                ?? throw new InvalidOperationException("The WPF window source is unavailable.");
            _brain = new PetBrain(
                BehaviorTuning.Default,
                new SeededRandomSource(),
                initialPosition);
            _snapshot = _brain.Current;

            _window.Left = _snapshot.Position.X;
            _window.Top = _snapshot.Position.Y;
            _presenter.Render(_snapshot);

            _stopwatch = new Stopwatch();
            _timer = new DispatcherTimer(DispatcherPriority.Normal, _window.Dispatcher)
            {
                Interval = TickInterval
            };
            _timer.Tick += OnTick;
            _stopwatch.Start();
            _timer.Start();
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

        _bodyPressQueue.Enqueue(new PointD(
            _window.Left + localPosition.X,
            _window.Top + localPosition.Y));
    }

    public void Dispose()
    {
        _lifecycle.TryDispose();
        if (_cleanupComplete)
        {
            return;
        }

        var cleanupException = StopAndDetach();
        if (cleanupException is not null)
        {
            throw cleanupException;
        }

        _cleanupComplete = true;
        GC.SuppressFinalize(this);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_lifecycle.Phase != PetLoopPhase.Running)
        {
            return;
        }

        try
        {
            var stopwatch = _stopwatch
                ?? throw new InvalidOperationException("The update clock has not been started.");
            var brain = _brain
                ?? throw new InvalidOperationException("The behavior brain has not been created.");
            var windowSource = _windowSource
                ?? throw new InvalidOperationException("The WPF window source is unavailable.");

            var elapsed = stopwatch.Elapsed;
            var delta = elapsed - _lastElapsed;
            _lastElapsed = elapsed;

            var pointer = _input.TryGetPointerInDips(windowSource, out var pointerPosition)
                ? new PointerSample(true, pointerPosition)
                : PointerSample.Unavailable;
            var primaryButtonDown = _input.IsPrimaryButtonDown();
            var bodyPress = _bodyPressQueue.Consume();

            var previous = _snapshot;
            var current = brain.Update(new PetInput(
                delta,
                GetWorkArea(),
                GetPetSize(),
                pointer,
                primaryButtonDown,
                bodyPress,
                new SizeD(
                    SystemParameters.MinimumHorizontalDragDistance,
                    SystemParameters.MinimumVerticalDragDistance)));

            UpdateMouseCapture(previous, current);
            _window.Left = current.Position.X;
            _window.Top = current.Position.Y;
            _presenter.Render(current);
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
            case MouseCaptureChange.Capture when !_presenter.CaptureMouse():
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
        _cleanupComplete = cleanupException is null;

        Faulted?.Invoke(
            this,
            cleanupException is null
                ? exception
                : new AggregateException(exception, cleanupException).Flatten());
    }

    private Exception? StopAndDetach()
    {
        var timer = _timer;
        _timer = null;
        var stopwatch = _stopwatch;
        _stopwatch = null;

        return CleanupSequence.Run(
            () => timer?.Stop(),
            () =>
            {
                if (timer is not null)
                {
                    timer.Tick -= OnTick;
                }
            },
            () => stopwatch?.Stop(),
            ReleaseMouseCapture,
            () =>
            {
                _lastElapsed = TimeSpan.Zero;
                _bodyPressQueue.Clear();
                _brain = null;
                _windowSource = null;
            });
    }

    private void ReleaseMouseCapture()
    {
        if (!_isMouseCaptured)
        {
            return;
        }

        _presenter.ReleaseMouseCapture();
        _isMouseCaptured = false;
    }

    private SizeD GetPetSize() => new(_window.ActualWidth, _window.ActualHeight);

    private static RectD GetWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        return new RectD(workArea.X, workArea.Y, workArea.Width, workArea.Height);
    }
}
