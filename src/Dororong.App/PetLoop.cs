using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Dororong.App.Controls;
using Dororong.App.Interop;
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
    private DispatcherTimer? _timer;
    private Stopwatch? _stopwatch;
    private HwndSource? _windowSource;
    private PetBrain? _brain;
    private PetSnapshot _snapshot;
    private TimeSpan _lastElapsed;
    private PointD? _queuedBodyPress;
    private bool _isMouseCaptured;
    private bool _faulted;
    private bool _disposed;

    public PetLoop(Window window, DororongPresenter presenter, DesktopInput input)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    public event EventHandler<Exception>? Faulted;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_timer is not null)
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
        if (_disposed || _faulted)
        {
            return;
        }

        _queuedBodyPress = new PointD(
            _window.Left + localPosition.X,
            _window.Top + localPosition.Y);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAndDetach();
        GC.SuppressFinalize(this);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_disposed || _faulted)
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
            var bodyPress = _queuedBodyPress;
            _queuedBodyPress = null;

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
        if (previous.State != PetState.Dragged && current.State == PetState.Dragged)
        {
            if (!_presenter.CaptureMouse())
            {
                throw new InvalidOperationException("Dororong could not capture the mouse for dragging.");
            }

            _isMouseCaptured = true;
        }
        else if (previous.State == PetState.Dragged && current.State != PetState.Dragged)
        {
            ReleaseMouseCapture();
        }
    }

    private void HandleFault(Exception exception)
    {
        if (_faulted)
        {
            return;
        }

        _faulted = true;
        Exception? cleanupException = null;
        try
        {
            StopAndDetach();
        }
        catch (Exception error)
        {
            cleanupException = error;
        }

        Faulted?.Invoke(
            this,
            cleanupException is null
                ? exception
                : new AggregateException(exception, cleanupException));
    }

    private void StopAndDetach()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer = null;
        }

        _stopwatch?.Stop();
        _stopwatch = null;
        _lastElapsed = TimeSpan.Zero;
        _queuedBodyPress = null;
        _brain = null;
        _windowSource = null;
        ReleaseMouseCapture();
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
