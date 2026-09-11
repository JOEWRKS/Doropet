using System.Windows;
using System.Windows.Threading;
using Dororong.App.Product;
using Dororong.App.Runtime;

namespace Dororong.App;

public partial class App : Application
{
    private readonly AppStartupSequence _startupSequence = new();
    private ProductShell? _productShell;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        ProductShell? productShell = null;
        object? createdWindow = null;
        try
        {
            productShell = new ProductShell();
            _productShell = productShell;
            var result = productShell.TryStart(
                reportDiagnostic => new MainWindow(reportDiagnostic),
                window => ((Window)window).Close(),
                out createdWindow);
            if (!result.ShouldRun)
            {
                _productShell = null;
                productShell.Dispose();
                Shutdown(result.ExitCode);
                return;
            }
        }
        catch (Exception exception)
        {
            HandleFatal(exception, DiagnosticEvent.StartupFailure);
            return;
        }

        _startupSequence.TryRun(
            () => createdWindow!,
            window =>
            {
                MainWindow = (Window)window;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            },
            window => ((Window)window).Show(),
            CleanupStartupResources,
            fatalException =>
            {
                productShell!.Report(DiagnosticEvent.StartupFailure, fatalException);
                ShowStartupError(fatalException);
            },
            Shutdown);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        CleanupStartupResources();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        HandleFatal(e.Exception, DiagnosticEvent.DispatcherFailure);
    }

    private void HandleFatal(Exception exception, DiagnosticEvent diagnosticEvent)
    {
        var productShell = _productShell;
        _startupSequence.TryHandleFatal(
            exception,
            CleanupStartupResources,
            fatalException =>
            {
                productShell?.Report(diagnosticEvent, fatalException);
                ShowStartupError(fatalException);
            },
            Shutdown);
    }

    private Exception? CleanupStartupResources()
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        var mainWindow = MainWindow;
        MainWindow = null;
        var productShell = _productShell;
        _productShell = null;
        var cleanupException = CleanupSequence.Run(
            () => mainWindow?.Close(),
            () => productShell?.Dispose());
        if (cleanupException is not null)
        {
            productShell?.Report(DiagnosticEvent.CleanupFailure, cleanupException);
        }

        return cleanupException;
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
