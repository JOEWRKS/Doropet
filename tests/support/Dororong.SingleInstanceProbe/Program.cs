using Dororong.App.Product;

namespace Dororong.SingleInstanceProbe;

internal static class Program
{
    private static SingleInstanceLease? abandonedLease;

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            return 64;
        }

        return args[0] switch
        {
            "try" => TryOnce(args[1]),
            "hold" => Hold(args[1]),
            "abandon" => Abandon(args[1]),
            _ => 64
        };
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
