using System.Threading;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Runtime;

internal sealed class OneShotOperation
{
    private int _hasRun;

    public bool TryRun(Action operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (Interlocked.Exchange(ref _hasRun, 1) != 0)
        {
            return false;
        }

        operation();
        return true;
    }
}

internal sealed class AppStartupSequence
{
    private readonly OneShotOperation _startup = new();
    private readonly OneShotOperation _fatal = new();

    public bool TryRun(
        Func<object> createMainWindow,
        Action<object> setMainWindow,
        Action<object> showMainWindow,
        Func<Exception?> cleanup,
        Action<Exception> showError,
        Action<int> shutdown)
    {
        ArgumentNullException.ThrowIfNull(createMainWindow);
        ArgumentNullException.ThrowIfNull(setMainWindow);
        ArgumentNullException.ThrowIfNull(showMainWindow);
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentNullException.ThrowIfNull(showError);
        ArgumentNullException.ThrowIfNull(shutdown);

        return _startup.TryRun(() =>
        {
            try
            {
                var mainWindow = createMainWindow();
                setMainWindow(mainWindow);
                showMainWindow(mainWindow);
            }
            catch (Exception exception)
            {
                TryHandleFatal(exception, cleanup, showError, shutdown);
            }
        });
    }

    public bool TryHandleFatal(
        Exception exception,
        Func<Exception?> cleanup,
        Action<Exception> showError,
        Action<int> shutdown)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentNullException.ThrowIfNull(showError);
        ArgumentNullException.ThrowIfNull(shutdown);

        return _fatal.TryRun(() =>
            FatalBoundary.Run(
                exception,
                cleanup,
                showError,
                () => shutdown(1)));
    }
}

internal enum PetLoopPhase
{
    Created,
    Running,
    Faulted,
    Disposed
}

internal sealed class PetLoopLifecycle
{
    public PetLoopPhase Phase { get; private set; } = PetLoopPhase.Created;

    public bool TryStart()
    {
        if (Phase != PetLoopPhase.Created)
        {
            return false;
        }

        Phase = PetLoopPhase.Running;
        return true;
    }

    public bool TryFault()
    {
        if (Phase != PetLoopPhase.Running)
        {
            return false;
        }

        Phase = PetLoopPhase.Faulted;
        return true;
    }

    public bool TryDispose()
    {
        if (Phase == PetLoopPhase.Disposed)
        {
            return false;
        }

        Phase = PetLoopPhase.Disposed;
        return true;
    }
}

internal static class CleanupSequence
{
    public static Exception? Run(params Action[] steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        List<Exception>? errors = null;

        foreach (var step in steps)
        {
            try
            {
                step();
            }
            catch (Exception exception)
            {
                errors ??= [];
                errors.Add(exception);
            }
        }

        return errors?.Count switch
        {
            null => null,
            1 => errors[0],
            _ => new AggregateException(errors)
        };
    }
}

internal static class FatalBoundary
{
    public static void Run(
        Exception primaryException,
        Func<Exception?> cleanup,
        Action<Exception> showError,
        Action shutdown)
    {
        ArgumentNullException.ThrowIfNull(primaryException);
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentNullException.ThrowIfNull(showError);
        ArgumentNullException.ThrowIfNull(shutdown);

        var fatalException = primaryException;
        try
        {
            var cleanupException = cleanup();
            if (cleanupException is not null)
            {
                fatalException = new AggregateException(
                    primaryException,
                    cleanupException).Flatten();
            }
        }
        catch (Exception cleanupException)
        {
            fatalException = new AggregateException(
                primaryException,
                cleanupException).Flatten();
        }

        try
        {
            showError(fatalException);
        }
        catch
        {
            // A failed error dialog must not prevent deterministic shutdown.
        }
        finally
        {
            shutdown();
        }
    }
}

internal sealed class BodyPressQueue
{
    private PointD? _queued;

    public void Enqueue(PointD position)
    {
        _queued = position;
    }

    public PointD? Consume()
    {
        var position = _queued;
        _queued = null;
        return position;
    }

    public void Clear()
    {
        _queued = null;
    }
}

internal enum MouseCaptureChange
{
    None,
    Capture,
    Release
}

internal static class MouseCaptureTransition
{
    public static MouseCaptureChange Decide(PetState previous, PetState current)
    {
        if (previous != PetState.Dragged && current == PetState.Dragged)
        {
            return MouseCaptureChange.Capture;
        }

        return previous == PetState.Dragged && current != PetState.Dragged
            ? MouseCaptureChange.Release
            : MouseCaptureChange.None;
    }
}
