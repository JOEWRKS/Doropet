using System.Diagnostics;
using System.IO;
using Dororong.App.Runtime;

namespace Dororong.App.Product;

internal readonly record struct ProductShellStartResult(bool ShouldRun, int ExitCode)
{
    internal static ProductShellStartResult Started { get; } = new(true, 0);
    internal static ProductShellStartResult Duplicate { get; } = new(false, 0);
}

internal sealed class ProductShell : IDisposable
{
    private readonly Func<string, IDisposable?> acquireLease;
    private readonly Func<Action, Action, IDisposable> createTray;
    private readonly Action openLogs;
    private readonly Action<DiagnosticEvent, Exception?> writeDiagnostic;
    private IDisposable? lease;
    private IDisposable? tray;
    private bool attempted;
    private bool started;
    private int disposed;

    internal ProductShell()
    {
        var diagnosticLog = new DiagnosticLog(ProductIdentity.LogDirectory);
        acquireLease = name => SingleInstanceLease.TryAcquire(name);
        createTray = (openLogFolder, exit) => new TrayService(openLogFolder, exit);
        openLogs = OpenLogDirectory;
        writeDiagnostic = diagnosticLog.Write;
    }

    internal ProductShell(
        Func<string, IDisposable?> acquireLease,
        Func<Action, Action, IDisposable> createTray,
        Action openLogs,
        Action<DiagnosticEvent, Exception?> writeDiagnostic)
    {
        this.acquireLease = acquireLease ?? throw new ArgumentNullException(nameof(acquireLease));
        this.createTray = createTray ?? throw new ArgumentNullException(nameof(createTray));
        this.openLogs = openLogs ?? throw new ArgumentNullException(nameof(openLogs));
        this.writeDiagnostic = writeDiagnostic ?? throw new ArgumentNullException(nameof(writeDiagnostic));
    }

    internal ProductShellStartResult TryStart(
        Func<Action<DiagnosticEvent, Exception?>, object> createMainWindow,
        Action<object> closeMainWindow,
        out object? mainWindow)
    {
        ArgumentNullException.ThrowIfNull(createMainWindow);
        ArgumentNullException.ThrowIfNull(closeMainWindow);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (attempted)
        {
            throw new InvalidOperationException("The product shell has already attempted startup.");
        }

        attempted = true;
        mainWindow = null;
        var acquiredLease = acquireLease(ProductIdentity.InstanceName);
        if (acquiredLease is null)
        {
            return ProductShellStartResult.Duplicate;
        }

        lease = acquiredLease;
        object? createdWindow = null;
        try
        {
            createdWindow = createMainWindow(Report);
            ArgumentNullException.ThrowIfNull(createdWindow);
            tray = createTray(
                () => RunTrayCommand(openLogs),
                () => RunTrayCommand(() => closeMainWindow(createdWindow)));
            ArgumentNullException.ThrowIfNull(tray);
            mainWindow = createdWindow;
            started = true;
            Report(DiagnosticEvent.Started);
            return ProductShellStartResult.Started;
        }
        catch (Exception startupException)
        {
            var cleanupException = CleanupSequence.Run(
                () =>
                {
                    if (createdWindow is not null)
                    {
                        closeMainWindow(createdWindow);
                    }
                },
                DisposeTray,
                DisposeLease);
            if (cleanupException is not null)
            {
                throw new AggregateException(startupException, cleanupException).Flatten();
            }

            throw;
        }
    }

    internal void Report(DiagnosticEvent code, Exception? error = null)
    {
        try
        {
            writeDiagnostic(code, error);
        }
        catch
        {
            // Diagnostics cannot replace the application failure being handled.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        if (started)
        {
            Report(DiagnosticEvent.Stopped);
        }

        var cleanupException = CleanupSequence.Run(DisposeTray, DisposeLease);
        if (cleanupException is not null)
        {
            throw cleanupException;
        }
    }

    private static void OpenLogDirectory()
    {
        Directory.CreateDirectory(ProductIdentity.LogDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = ProductIdentity.LogDirectory,
            UseShellExecute = true
        });
    }

    private void RunTrayCommand(Action command)
    {
        try
        {
            command();
        }
        catch (Exception exception)
        {
            Report(DiagnosticEvent.TrayCommandFailure, exception);
        }
    }

    private void DisposeTray()
    {
        var ownedTray = tray;
        tray = null;
        ownedTray?.Dispose();
    }

    private void DisposeLease()
    {
        var ownedLease = lease;
        lease = null;
        ownedLease?.Dispose();
    }
}
