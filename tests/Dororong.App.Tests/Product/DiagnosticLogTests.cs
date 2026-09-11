using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Principal;
using System.Text.Json;
using Dororong.App.Product;

namespace Dororong.App.Tests.Product;

public sealed class DiagnosticLogTests
{
    [Fact]
    public void Product_assembly_keeps_internal_identity_but_uses_the_approved_title()
    {
        var assembly = typeof(ProductIdentity).Assembly;
        var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location);

        Assert.Equal("Dororong.App", assembly.GetName().Name);
        Assert.Equal("도로롱 (Dororong)", assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
        Assert.Equal("도로롱 (Dororong)", fileVersion.FileDescription);
        Assert.Equal("Dororong.App.dll", fileVersion.InternalName);
        Assert.Equal("Dororong.App.dll", fileVersion.OriginalFilename);
    }

    [Fact]
    public void Product_identity_uses_approved_values_and_a_version_independent_current_user_lease()
    {
        Assert.Equal("도로롱 (Dororong)", ProductIdentity.DisplayName);
        Assert.Equal("0.1.0", ProductIdentity.Version);
        Assert.Equal("JOEWRKS", ProductIdentity.Publisher);
        Assert.Equal("Dororong.exe", ProductIdentity.ExecutableName);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JOEWRKS", "Dororong", "logs"), ProductIdentity.LogDirectory);

        var sid = WindowsIdentity.GetCurrent().User?.Value;
        Assert.False(string.IsNullOrWhiteSpace(sid));
        Assert.Equal($@"Local\JOEWRKS.Dororong.{sid}", ProductIdentity.InstanceName);
        Assert.DoesNotContain(ProductIdentity.Version, ProductIdentity.InstanceName, StringComparison.Ordinal);
    }

    [Fact]
    public void Exception_message_data_and_directory_are_not_written()
    {
        using var temp = new TempDirectory();
        var log = new DiagnosticLog(temp.Path);
        var error = CaptureSensitiveException();

        log.Write(DiagnosticEvent.LoopFailure, error);

        var file = Assert.Single(Directory.GetFiles(temp.Path));
        var text = File.ReadAllText(file);
        Assert.DoesNotContain("PRIVATE_SENTINEL", text, StringComparison.Ordinal);
        Assert.DoesNotContain(temp.Path, text, StringComparison.OrdinalIgnoreCase);

        using var record = JsonDocument.Parse(Assert.Single(File.ReadAllLines(file)));
        var fields = record.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();
        Assert.Equal(new[] { "event", "exceptionType", "hresult", "method", "pid", "timestamp", "version" }, fields);
        Assert.Equal("LoopFailure", record.RootElement.GetProperty("event").GetString());
        Assert.Equal(Environment.ProcessId, record.RootElement.GetProperty("pid").GetInt32());
        Assert.Equal("0.1.0", record.RootElement.GetProperty("version").GetString());
        Assert.Equal(typeof(InvalidOperationException).FullName,
            record.RootElement.GetProperty("exceptionType").GetString());
        Assert.Equal(error.HResult, record.RootElement.GetProperty("hresult").GetInt32());
        Assert.Equal(nameof(ThrowSensitiveException), record.RootElement.GetProperty("method").GetString());
        Assert.Equal(TimeSpan.Zero,
            DateTimeOffset.Parse(record.RootElement.GetProperty("timestamp").GetString()!).Offset);
    }

    [Fact]
    public void Method_name_punctuation_is_sanitized_before_the_128_character_boundary()
    {
        using var temp = new TempDirectory();
        var log = new DiagnosticLog(temp.Path);
        var methodName = new string('A', 126) + "!?Z";
        var error = CaptureDynamicException(methodName);

        log.Write(DiagnosticEvent.LoopFailure, error);

        using var record = JsonDocument.Parse(Assert.Single(File.ReadAllLines(
            Assert.Single(Directory.GetFiles(temp.Path, "diagnostic*.log")))));
        var sanitized = record.RootElement.GetProperty("method").GetString();
        Assert.Equal(new string('A', 126) + "__", sanitized);
        Assert.Equal(128, sanitized?.Length);
    }

    [Fact]
    public async Task Independent_process_writers_observe_one_transaction_lock_and_rotate_near_cap_safely()
    {
        using var temp = new TempDirectory();
        var currentPath = Path.Combine(temp.Path, "diagnostic.log");
        File.WriteAllBytes(currentPath, new byte[1_048_500]);
        var initialHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            File.ReadAllBytes(currentPath)));

        using (var blocker = StartProbe("hold-log-lock", temp.Path))
        {
            Assert.Equal("LOCKED", await ReadStatus(blocker));
            using var blockedFirst = StartProbe("write-log", temp.Path, "1");
            using var blockedSecond = StartProbe("write-log", temp.Path, "1");
            Assert.Equal("READY", await ReadStatus(blockedFirst));
            Assert.Equal("READY", await ReadStatus(blockedSecond));
            await Release(blockedFirst);
            await Release(blockedSecond);
            Assert.Equal("DONE", await ReadStatus(blockedFirst));
            Assert.Equal("DONE", await ReadStatus(blockedSecond));
            await WaitForExit(blockedFirst);
            await WaitForExit(blockedSecond);
            Assert.Equal(initialHash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(currentPath))));
            await Stop(blocker);
        }

        const int recordsPerWriter = 1_000;
        using var first = StartProbe("write-log", temp.Path, recordsPerWriter.ToString());
        using var second = StartProbe("write-log", temp.Path, recordsPerWriter.ToString());
        Assert.Equal("READY", await ReadStatus(first));
        Assert.Equal("READY", await ReadStatus(second));
        await Release(first);
        await Release(second);
        Assert.Equal("DONE", await ReadStatus(first));
        Assert.Equal("DONE", await ReadStatus(second));
        await WaitForExit(first);
        await WaitForExit(second);

        var logFiles = Directory.GetFiles(temp.Path, "diagnostic*.log");
        Assert.InRange(logFiles.Length, 1, 3);
        Assert.All(logFiles, path => Assert.InRange(new FileInfo(path).Length, 1, 1_048_576));
        Assert.False(File.Exists(Path.Combine(temp.Path, ".diagnostic.lock")));
        var records = File.ReadAllLines(currentPath);
        Assert.InRange(records.Length, 1, recordsPerWriter * 2);
        Assert.All(records, line => JsonDocument.Parse(line).Dispose());
    }

    [Fact]
    public void Repeated_writes_rotate_within_three_one_mebibyte_files()
    {
        using var temp = new TempDirectory();
        var log = new DiagnosticLog(temp.Path);

        for (var index = 0; index < 30_000; index++)
        {
            log.Write(DiagnosticEvent.Started);
        }

        var files = Directory.GetFiles(temp.Path);
        Assert.InRange(files.Length, 2, 3);
        Assert.All(files, path => Assert.InRange(new FileInfo(path).Length, 1, 1_048_576));
    }

    [Fact]
    public void Concurrent_writes_remain_complete_json_records()
    {
        using var temp = new TempDirectory();
        var log = new DiagnosticLog(temp.Path);

        Parallel.For(0, 2_000, index =>
            log.Write(index % 2 == 0 ? DiagnosticEvent.Started : DiagnosticEvent.Stopped));

        var file = Assert.Single(Directory.GetFiles(temp.Path));
        var lines = File.ReadAllLines(file);
        Assert.Equal(2_000, lines.Length);
        Assert.All(lines, line =>
        {
            using var record = JsonDocument.Parse(line);
            Assert.Contains(record.RootElement.GetProperty("event").GetString(),
                new[] { "Started", "Stopped" });
        });
    }

    [Fact]
    public void Write_does_not_throw_when_log_path_is_occupied_by_a_file()
    {
        using var temp = new TempDirectory();
        var occupiedPath = Path.Combine(temp.Path, "logs");
        File.WriteAllText(occupiedPath, "occupied");
        var log = new DiagnosticLog(occupiedPath);

        var error = Record.Exception(() => log.Write(DiagnosticEvent.StartupFailure,
            new IOException("PRIVATE_SENTINEL")));

        Assert.Null(error);
        Assert.Equal("occupied", File.ReadAllText(occupiedPath));
    }

    private static Exception CaptureSensitiveException()
    {
        try
        {
            ThrowSensitiveException();
            throw new InvalidOperationException("Unreachable");
        }
        catch (InvalidOperationException error)
        {
            error.Data["PRIVATE_SENTINEL"] = "PRIVATE_SENTINEL";
            return error;
        }
    }

    private static void ThrowSensitiveException() =>
        throw new InvalidOperationException("PRIVATE_SENTINEL");

    private static Exception CaptureDynamicException(string methodName)
    {
        var method = new DynamicMethod(methodName, typeof(void), Type.EmptyTypes, typeof(DiagnosticLogTests).Module);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldstr, "PRIVATE_SENTINEL");
        il.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) })!);
        il.Emit(OpCodes.Throw);
        try
        {
            method.CreateDelegate<Action>()();
            throw new InvalidOperationException("Unreachable");
        }
        catch (InvalidOperationException error)
        {
            return error;
        }
    }

    private static Process StartProbe(string mode, params string[] arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = ProbePath(),
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(mode);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return Process.Start(start) ?? throw new InvalidOperationException("Probe process did not start.");
    }

    private static async Task<string> ReadStatus(Process process)
    {
        var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
        if (line is not null)
        {
            return line;
        }

        var error = await process.StandardError.ReadToEndAsync();
        throw new InvalidOperationException($"Probe exited without status (code {process.ExitCode}): {error}");
    }

    private static async Task Release(Process process)
    {
        await process.StandardInput.WriteLineAsync();
        await process.StandardInput.FlushAsync();
    }

    private static async Task WaitForExit(Process process) =>
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));

    private static async Task Stop(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        await Release(process);
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

    private sealed class TempDirectory : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"DororongDiagnosticLogTests-{Guid.NewGuid():N}");

        internal TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
