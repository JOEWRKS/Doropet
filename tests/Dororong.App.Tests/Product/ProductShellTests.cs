using System.Diagnostics;
using System.IO;
using System.Reflection;
using Dororong.App.Product;
using Dororong.App.Runtime;

namespace Dororong.App.Tests.Product;

public class ProductShellTests
{
    [Fact]
    public void Lease_acquisition_failure_creates_no_window_or_tray_and_preserves_the_failure()
    {
        var trace = new List<string>();
        using var shell = CreateShell(
            acquireLease: _ =>
            {
                trace.Add("lease");
                throw new UnauthorizedAccessException("injected identity failure");
            },
            createTray: (_, _) =>
            {
                trace.Add("tray");
                return new TraceResource(trace, "tray-dispose");
            });

        var error = Assert.Throws<UnauthorizedAccessException>(() => shell.TryStart(
            _ =>
            {
                trace.Add("window");
                return new object();
            },
            _ => trace.Add("window-close"),
            out _));

        Assert.Equal("injected identity failure", error.Message);
        Assert.Equal(["lease"], trace);
    }

    [Fact]
    public void Duplicate_instance_creates_no_window_or_tray_and_returns_success_exit_result()
    {
        var trace = new List<string>();
        using var shell = CreateShell(
            acquireLease: _ =>
            {
                trace.Add("lease-denied");
                return null;
            },
            createTray: (_, _) =>
            {
                trace.Add("tray");
                return new TraceResource(trace, "tray-dispose");
            });

        var result = shell.TryStart(
            _ =>
            {
                trace.Add("window");
                return new object();
            },
            _ => trace.Add("window-close"),
            out var window);

        Assert.Equal(ProductShellStartResult.Duplicate, result);
        Assert.False(result.ShouldRun);
        Assert.Null(window);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["lease-denied"], trace);
    }

    [Fact]
    public void Window_initialization_failure_releases_the_acquired_lease()
    {
        var trace = new List<string>();
        using var shell = CreateShell(
            acquireLease: _ =>
            {
                trace.Add("lease");
                return new TraceResource(trace, "lease-dispose");
            },
            createTray: (_, _) => throw new InvalidOperationException("tray should not be reached"));

        Assert.Throws<InvalidOperationException>(() => shell.TryStart(
            _ =>
            {
                trace.Add("window");
                throw new InvalidOperationException("injected window failure");
            },
            _ => trace.Add("window-close"),
            out _));

        Assert.Equal(["lease", "window", "lease-dispose"], trace);
    }

    [Fact]
    public void Tray_initialization_failure_closes_the_created_window_and_releases_the_lease()
    {
        var trace = new List<string>();
        var createdWindow = new object();
        using var shell = CreateShell(
            acquireLease: _ =>
            {
                trace.Add("lease");
                return new TraceResource(trace, "lease-dispose");
            },
            createTray: (_, _) =>
            {
                trace.Add("tray");
                throw new InvalidOperationException("injected tray failure");
            });

        Assert.Throws<InvalidOperationException>(() => shell.TryStart(
            _ =>
            {
                trace.Add("window");
                return createdWindow;
            },
            window =>
            {
                Assert.Same(createdWindow, window);
                trace.Add("window-close");
            },
            out _));

        Assert.Equal(["lease", "window", "tray", "window-close", "lease-dispose"], trace);
    }

    [Fact]
    public void Initialization_cleanup_releases_the_lease_when_window_close_also_fails()
    {
        var trace = new List<string>();
        using var shell = CreateShell(
            acquireLease: _ =>
            {
                trace.Add("lease");
                return new TraceResource(trace, "lease-dispose");
            },
            createTray: (_, _) =>
            {
                trace.Add("tray");
                throw new InvalidOperationException("injected tray failure");
            });

        Assert.Throws<AggregateException>(() => shell.TryStart(
            _ =>
            {
                trace.Add("window");
                return new object();
            },
            _ =>
            {
                trace.Add("window-close");
                throw new InvalidOperationException("injected window cleanup failure");
            },
            out _));

        Assert.Equal(["lease", "window", "tray", "window-close", "lease-dispose"], trace);
    }

    [Fact]
    public void Tray_exit_closes_the_exact_window_created_for_the_shell()
    {
        Action? trayExit = null;
        var createdWindow = new object();
        object? closedWindow = null;
        using var shell = CreateShell(
            acquireLease: _ => new TraceResource([], "lease-dispose"),
            createTray: (_, exit) =>
            {
                trayExit = exit;
                return new TraceResource([], "tray-dispose");
            });

        var result = shell.TryStart(_ => createdWindow, window => closedWindow = window, out var window);
        trayExit!();

        Assert.Equal(ProductShellStartResult.Started, result);
        Assert.True(result.ShouldRun);
        Assert.Same(createdWindow, window);
        Assert.Same(createdWindow, closedWindow);
    }

    [Fact]
    public void Repeated_cleanup_disposes_tray_and_lease_once_and_logs_one_stop()
    {
        var trace = new List<string>();
        var events = new List<DiagnosticEvent>();
        var shell = CreateShell(
            acquireLease: _ => new TraceResource(trace, "lease-dispose"),
            createTray: (_, _) => new TraceResource(trace, "tray-dispose"),
            writeDiagnostic: (code, _) => events.Add(code));
        shell.TryStart(_ => new object(), _ => { }, out _);

        shell.Dispose();
        shell.Dispose();

        Assert.Equal(["tray-dispose", "lease-dispose"], trace);
        Assert.Equal([DiagnosticEvent.Started, DiagnosticEvent.Stopped], events);
    }

    [Fact]
    public void Cleanup_releases_the_lease_after_tray_disposal_fails()
    {
        var trace = new List<string>();
        var shell = CreateShell(
            acquireLease: _ => new TraceResource(trace, "lease-dispose"),
            createTray: (_, _) => new TraceResource(trace, "tray-dispose", throwOnDispose: true));
        shell.TryStart(_ => new object(), _ => { }, out _);

        Assert.Throws<InvalidOperationException>(shell.Dispose);

        Assert.Equal(["tray-dispose", "lease-dispose"], trace);
        Assert.Null(Record.Exception(shell.Dispose));
    }

    [Fact]
    public void Logging_failure_is_suppressed_and_fatal_boundary_still_returns_exit_one()
    {
        using var shell = CreateShell(writeDiagnostic: (_, _) =>
            throw new IOException("injected logger failure"));
        Assert.Null(Record.Exception(() =>
            shell.Report(DiagnosticEvent.DispatcherFailure, new InvalidOperationException("direct"))));
        var exitCode = 0;
        var error = Record.Exception(() => FatalBoundary.Run(
            new InvalidOperationException("primary"),
            () => null,
            fatal => shell.Report(DiagnosticEvent.DispatcherFailure, fatal),
            () => exitCode = 1));

        Assert.Null(error);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Assembly_metadata_and_embedded_product_icon_use_approved_identity()
    {
        var assembly = typeof(Dororong.App.App).Assembly;
        var file = FileVersionInfo.GetVersionInfo(assembly.Location);

        Assert.Equal(new Version(0, 1, 0, 0), assembly.GetName().Version);
        Assert.Equal("도로롱 (Dororong)", assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
        Assert.Equal("JOEWRKS", assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company);
        Assert.Equal("0.1.0", file.ProductVersion);
        Assert.Equal("0.1.0.0", file.FileVersion);
        Assert.Contains("Dororong.App.Assets.dororong.ico", assembly.GetManifestResourceNames());
    }

    private static ProductShell CreateShell(
        Func<string, IDisposable?>? acquireLease = null,
        Func<Action, Action, IDisposable>? createTray = null,
        Action? openLogs = null,
        Action<DiagnosticEvent, Exception?>? writeDiagnostic = null) => new(
            acquireLease ?? (_ => new TraceResource([], "lease-dispose")),
            createTray ?? ((_, _) => new TraceResource([], "tray-dispose")),
            openLogs ?? (() => { }),
            writeDiagnostic ?? ((_, _) => { }));

    private sealed class TraceResource(
        List<string> trace,
        string disposalEvent,
        bool throwOnDispose = false) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            trace.Add(disposalEvent);
            if (throwOnDispose)
            {
                throw new InvalidOperationException($"injected {disposalEvent} failure");
            }
        }
    }
}
