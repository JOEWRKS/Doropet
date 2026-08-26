using System.Windows;
using System.Windows.Threading;
using Dororong.App.Runtime;

namespace Dororong.App;

public partial class App : Application
{
    private readonly AppStartupSequence _startupSequence = new();

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        _startupSequence.TryRun(
            () => new MainWindow(),
            window =>
            {
                MainWindow = (Window)window;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            },
            window => ((Window)window).Show(),
            CleanupStartupResources,
            ShowStartupError,
            Shutdown);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        HandleFatal(e.Exception);
    }

    private void HandleFatal(Exception exception)
    {
        _startupSequence.TryHandleFatal(
            exception,
            CleanupStartupResources,
            ShowStartupError,
            Shutdown);
    }

    private Exception? CleanupStartupResources()
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        var mainWindow = MainWindow;
        MainWindow = null;
        return CleanupSequence.Run(() => mainWindow?.Close());
    }

    private static void ShowStartupError(Exception exception)
    {
        MessageBox.Show(
            $"Dororong encountered an unexpected error and must close.\n\n{exception.Message}",
            "Dororong",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
