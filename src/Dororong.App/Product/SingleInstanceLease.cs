namespace Dororong.App.Product;

public sealed class SingleInstanceLease : IDisposable
{
    private readonly Mutex mutex;
    private readonly int ownerThreadId;
    private int disposed;

    private SingleInstanceLease(Mutex mutex)
    {
        this.mutex = mutex;
        ownerThreadId = Environment.CurrentManagedThreadId;
    }

    public static SingleInstanceLease? TryAcquire(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(initiallyOwned: false, name);
            var ownsMutex = false;
            try
            {
                ownsMutex = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                ownsMutex = true;
            }

            if (!ownsMutex)
            {
                mutex.Dispose();
                return null;
            }

            return new SingleInstanceLease(mutex);
        }
        catch
        {
            mutex?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        if (Environment.CurrentManagedThreadId != ownerThreadId)
        {
            throw new InvalidOperationException("The lease must be released by its owning thread.");
        }

        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        try
        {
            mutex.ReleaseMutex();
        }
        finally
        {
            mutex.Dispose();
        }
    }
}
