using System.Windows;
using System.Windows.Interop;
using Dororong.App.Controls;
using Dororong.App.Interop;
using Dororong.App.Runtime;

namespace Dororong.App;

public partial class MainWindow : Window
{
    private readonly OneShotOperation _loadedOperation = new();
    private readonly OneShotOperation _fatalOperation = new();
    private WindowActivationGuard? _activationGuard;
    private PetLoop? _loop;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
        Presenter.DirectInteractionPressed += OnDirectInteractionPressed;
        Presenter.LostMouseCapture += OnDirectInteractionCanceled;
        Presenter.ExitRequested += OnExitRequested;
        Presenter.SitRequested += OnSitRequested;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            if (_activationGuard?.IsAttached == true)
            {
                return;
            }

            var detachError = DetachActivationGuard();
            if (detachError is not null)
            {
                throw detachError;
            }

            var handle = new WindowInteropHelper(this).Handle;
            var source = HwndSource.FromHwnd(handle)
                ?? throw new InvalidOperationException("The WPF window source is unavailable.");

            WindowStyleManager.ApplyNoActivateToolWindow(handle);
            _activationGuard = new WindowActivationGuard(source);
        }
        catch (Exception exception)
        {
            HandleFatal(exception);
        }
    }

    private Exception? DetachActivationGuard()
    {
        var guard = _activationGuard;
        _activationGuard = null;
        return CleanupSequence.Run(() => guard?.Dispose());
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        try
        {
            _loadedOperation.TryRun(() =>
            {
                if (_loop is not null)
                {
                    throw new InvalidOperationException("The pet loop has already been created.");
                }

                var loop = new PetLoop(this, Presenter, new DesktopInput());
                _loop = loop;
                loop.Faulted += OnLoopFaulted;
                loop.Start();
            });
        }
        catch (Exception exception)
        {
            HandleFatal(exception);
        }
    }

    private void OnDirectInteractionPressed(object? sender, DirectInteractionPressEventArgs e)
    {
        _loop?.NotifyDirectInteractionPressed(e);
    }

    private void OnDirectInteractionCanceled(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _loop?.NotifyDirectInteractionCanceled();
    }

    private void OnSitRequested(object? sender, EventArgs e) => _loop?.NotifySitRequested();

    private void OnExitRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnLoopFaulted(object? sender, Exception exception)
    {
        HandleFatal(exception);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        var cleanupException = CleanupWindowResources();
        if (cleanupException is not null)
        {
            HandleFatal(cleanupException);
        }
    }

    private void HandleFatal(Exception exception)
    {
        _fatalOperation.TryRun(() =>
            FatalBoundary.Run(
                exception,
                CleanupWindowResources,
                fatalException =>
                    MessageBox.Show(
                        this,
                        $"Dororong encountered an unexpected error and must close.\n\n{fatalException.Message}",
                        "Dororong",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error),
                () => Application.Current.Shutdown(1)));
    }

    private Exception? CleanupWindowResources()
    {
        var loop = _loop;
        _loop = null;
        var guard = _activationGuard;
        _activationGuard = null;

        return CleanupSequence.Run(
            () => SourceInitialized -= OnSourceInitialized,
            () => Loaded -= OnLoaded,
            () => Closed -= OnClosed,
            () => Presenter.DirectInteractionPressed -= OnDirectInteractionPressed,
            () => Presenter.LostMouseCapture -= OnDirectInteractionCanceled,
            () => Presenter.ExitRequested -= OnExitRequested,
            () => Presenter.SitRequested -= OnSitRequested,
            () =>
            {
                if (loop is not null)
                {
                    loop.Faulted -= OnLoopFaulted;
                }
            },
            () => loop?.Dispose(),
            () => guard?.Dispose());
    }
}
