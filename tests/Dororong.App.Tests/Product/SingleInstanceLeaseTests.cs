using System.Diagnostics;
using System.IO;

namespace Dororong.App.Tests.Product;

public sealed class SingleInstanceLeaseTests
{
    [Fact]
    public async Task Concurrent_processes_allow_one_owner_and_deny_the_other_safely()
    {
        var name = UniqueName();
        using var owner = StartProbe("hold", name);
        try
        {
            Assert.Equal("ACQUIRED", await ReadStatus(owner));

            using var denied = StartProbe("try", name);
            Assert.Equal("DENIED", await ReadStatus(denied));
            await WaitForExit(denied);
            Assert.Equal(0, denied.ExitCode);
        }
        finally
        {
            await Stop(owner);
        }
    }

    [Fact]
    public async Task Graceful_owner_exit_allows_a_later_process_to_acquire()
    {
        var name = UniqueName();
        using var owner = StartProbe("hold", name);
        Assert.Equal("ACQUIRED", await ReadStatus(owner));
        await Stop(owner);

        using var successor = StartProbe("try", name);
        Assert.Equal("ACQUIRED", await ReadStatus(successor));
        await WaitForExit(successor);
        Assert.Equal(0, successor.ExitCode);
    }

    [Fact]
    public async Task Owner_thread_exit_is_recovered_as_an_abandoned_mutex()
    {
        var name = UniqueName();
        using var abandonedOwner = StartProbe("abandon", name);
        try
        {
            Assert.Equal("ABANDONED_READY", await ReadStatus(abandonedOwner));

            using var successor = StartProbe("try", name);
            Assert.Equal("ACQUIRED", await ReadStatus(successor));
            await WaitForExit(successor);
            Assert.Equal(0, successor.ExitCode);
        }
        finally
        {
            await Stop(abandonedOwner);
        }
    }

    private static string UniqueName() => $@"Local\JOEWRKS.Dororong.Tests.{Guid.NewGuid():N}";

    private static Process StartProbe(string mode, string name)
    {
        var start = new ProcessStartInfo
        {
            FileName = ProbePath(),
            Arguments = $"{mode} {name}",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        return Process.Start(start) ?? throw new InvalidOperationException("Probe process did not start.");
    }

    private static async Task<string> ReadStatus(Process process)
    {
        var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
        if (line is not null)
        {
            return line;
        }

        var error = await process.StandardError.ReadToEndAsync();
        throw new InvalidOperationException($"Probe exited without status (code {process.ExitCode}): {error}");
    }

    private static async Task WaitForExit(Process process) =>
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

    private static async Task Stop(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        await process.StandardInput.WriteLineAsync();
        try
        {
            await WaitForExit(process);
        }
        catch (TimeoutException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }

    private static string ProbePath()
    {
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory);
        var binDirectory = FindAncestor(testOutput, "bin")
            ?? throw new InvalidOperationException("Test bin directory was not found.");
        var repository = FindRepository(testOutput)
            ?? throw new InvalidOperationException("Repository directory was not found.");
        var outputSuffix = Path.GetRelativePath(binDirectory.FullName, testOutput.FullName);
        var path = Path.Combine(repository.FullName, "tests", "support", "Dororong.SingleInstanceProbe",
            "bin", outputSuffix, "Dororong.SingleInstanceProbe.exe");
        Assert.True(File.Exists(path), $"Probe executable was not built: {path}");
        return path;
    }

    private static DirectoryInfo? FindAncestor(DirectoryInfo start, string name)
    {
        for (DirectoryInfo? current = start; current is not null; current = current.Parent)
        {
            if (string.Equals(current.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }
        }

        return null;
    }

    private static DirectoryInfo? FindRepository(DirectoryInfo start)
    {
        for (DirectoryInfo? current = start; current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "DororongDesktopPet.sln")))
            {
                return current;
            }
        }

        return null;
    }
}
