using System.Windows;
using System.Windows.Interop;
using Dororong.App.Controls;
using Dororong.App.Interop;

namespace Dororong.App;

public partial class MainWindow : Window
{
    private WindowActivationGuard? _activationGuard;
    private PetLoop? _loop;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
        Presenter.BodyPrimaryPressed += OnBodyPrimaryPressed;
        Presenter.ExitRequested += OnExitRequested;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (_activationGuard?.IsAttached == true)
        {
            return;
        }

        DetachActivationGuard();

        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle)
            ?? throw new InvalidOperationException("The WPF window source is unavailable.");

        WindowStyleManager.ApplyNoActivateToolWindow(handle);
        _activationGuard = new WindowActivationGuard(source);
    }

    private void DetachActivationGuard()
    {
        _activationGuard?.Dispose();
        _activationGuard = null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loop = new PetLoop(this, Presenter, new DesktopInput());
        _loop.Faulted += OnLoopFaulted;
        _loop.Start();
    }

    private void OnBodyPrimaryPressed(object? sender, BodyPressEventArgs e)
    {
        _loop?.NotifyBodyPressed(e.LocalPosition);
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnLoopFaulted(object? sender, Exception exception)
    {
        DisposeLoop();
        MessageBox.Show(
            this,
            $"Dororong encountered an unexpected error and must close.\n\n{exception.Message}",
            "Dororong",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Application.Current.Shutdown(1);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;
        Loaded -= OnLoaded;
        Closed -= OnClosed;
        Presenter.BodyPrimaryPressed -= OnBodyPrimaryPressed;
        Presenter.ExitRequested -= OnExitRequested;

        DisposeLoop();
        DetachActivationGuard();
    }

    private void DisposeLoop()
    {
        if (_loop is null)
        {
            return;
        }

        _loop.Faulted -= OnLoopFaulted;
        _loop.Dispose();
        _loop = null;
    }
}
