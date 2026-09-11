using System.IO;
using System.Security.Principal;
using System.Text.Json;
using Dororong.App.Product;

namespace Dororong.App.Tests.Product;

public sealed class DiagnosticLogTests
{
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

    private sealed class TempDirectory : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"DororongDiagnosticLogTests-{Guid.NewGuid():N}");

        internal TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
