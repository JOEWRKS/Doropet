using Dororong.App.Product;

namespace Dororong.SingleInstanceProbe;

internal static class Program
{
    private static SingleInstanceLease? abandonedLease;

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            return 64;
        }

        return args[0] switch
        {
            "try" when args.Length == 2 => TryOnce(args[1]),
            "hold" when args.Length == 2 => Hold(args[1]),
            "abandon" when args.Length == 2 => Abandon(args[1]),
            "write-log" when args.Length == 3 && int.TryParse(args[2], out var count) => WriteLog(args[1], count),
            "hold-log-lock" when args.Length == 2 => HoldLogLock(args[1]),
            _ => 64
        };
    }

    private static int WriteLog(string directory, int count)
    {
        var log = new DiagnosticLog(directory);
        Console.WriteLine("READY");
        Console.Out.Flush();
        Console.ReadLine();
        for (var index = 0; index < count; index++)
        {
            log.Write(DiagnosticEvent.Started);
        }

        Console.WriteLine("DONE");
        return 0;
    }

    private static int HoldLogLock(string directory)
    {
        Directory.CreateDirectory(directory);
        using var transaction = new FileStream(
            Path.Combine(directory, ".diagnostic.lock"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 1,
            FileOptions.DeleteOnClose);
        Console.WriteLine("LOCKED");
        Console.Out.Flush();
        Console.ReadLine();
        return 0;
    }

    private static int TryOnce(string name)
    {
        using var lease = SingleInstanceLease.TryAcquire(name);
        Console.WriteLine(lease is null ? "DENIED" : "ACQUIRED");
        return 0;
    }

    private static int Hold(string name)
    {
        using var lease = SingleInstanceLease.TryAcquire(name);
        Console.WriteLine(lease is null ? "DENIED" : "ACQUIRED");
        Console.Out.Flush();
        if (lease is null)
        {
            return 2;
        }

        Console.ReadLine();
        return 0;
    }

    private static int Abandon(string name)
    {
        var owner = new Thread(() => abandonedLease = SingleInstanceLease.TryAcquire(name));
        owner.Start();
        owner.Join();

        Console.WriteLine(abandonedLease is null ? "DENIED" : "ABANDONED_READY");
        Console.Out.Flush();
        if (abandonedLease is null)
        {
            return 2;
        }

        Console.ReadLine();
        GC.KeepAlive(abandonedLease);
        return 0;
    }
}
