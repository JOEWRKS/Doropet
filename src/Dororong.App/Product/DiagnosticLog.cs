using System.Buffers;
using System.Globalization;
using System.IO;
using System.Security;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Dororong.App.Product;

internal enum DiagnosticEvent
{
    Started,
    Stopped,
    StartupFailure,
    LoopFailure,
    DispatcherFailure,
    CleanupFailure,
    TrayCommandFailure
}

internal sealed class DiagnosticLog
{
    private const int MaxFileBytes = 1_048_576;
    private const int MaxExceptionTypeLength = 256;
    private const int MaxMethodLength = 128;
    private static readonly TimeSpan TransactionLockTimeout = TimeSpan.FromMilliseconds(10);
    private readonly string directory;
    private readonly object writeGate = new();

    internal DiagnosticLog(string directory)
    {
        this.directory = directory;
    }

    public void Write(DiagnosticEvent code, Exception? error = null)
    {
        var record = BuildRecord(code, error);
        lock (writeGate)
        {
            try
            {
                WriteRecord(record);
            }
            catch (Exception exception) when (IsStorageFailure(exception))
            {
                // Diagnostics must never replace the application error being handled.
            }
        }
    }

    private void WriteRecord(byte[] record)
    {
        Directory.CreateDirectory(directory);
        using var transactionLock = TryAcquireTransactionLock();
        if (transactionLock is null)
        {
            return;
        }

        var currentPath = Path.Combine(directory, "diagnostic.log");
        var currentLength = File.Exists(currentPath) ? new FileInfo(currentPath).Length : 0;
        if (currentLength + record.Length > MaxFileBytes)
        {
            Rotate(currentPath);
        }

        using var stream = new FileStream(
            currentPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.None);
        stream.Write(record);
    }

    private FileStream? TryAcquireTransactionLock()
    {
        var lockPath = Path.Combine(directory, ".diagnostic.lock");
        var started = Stopwatch.GetTimestamp();
        do
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                Thread.Yield();
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (SecurityException)
            {
                return null;
            }
        }
        while (Stopwatch.GetElapsedTime(started) < TransactionLockTimeout);

        return null;
    }

    private void Rotate(string currentPath)
    {
        var previousPath = Path.Combine(directory, "diagnostic.1.log");
        var oldestPath = Path.Combine(directory, "diagnostic.2.log");

        File.Delete(oldestPath);
        if (File.Exists(previousPath))
        {
            File.Move(previousPath, oldestPath);
        }

        if (File.Exists(currentPath))
        {
            File.Move(currentPath, previousPath);
        }
    }

    private static byte[] BuildRecord(DiagnosticEvent code, Exception? error)
    {
        var buffer = new ArrayBufferWriter<byte>(512);
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("timestamp", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            json.WriteString("version", ProductIdentity.Version);
            json.WriteNumber("pid", Environment.ProcessId);
            json.WriteString("event", code.ToString());
            WriteStringOrNull(json, "exceptionType", Truncate(error?.GetType().FullName, MaxExceptionTypeLength));
            if (error is null)
            {
                json.WriteNull("hresult");
            }
            else
            {
                json.WriteNumber("hresult", error.HResult);
            }

            WriteStringOrNull(json, "method", SanitizeMethodName(error?.TargetSite?.Name));
            json.WriteEndObject();
        }

        var record = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(record);
        record[^1] = (byte)'\n';
        return record;
    }

    private static string? SanitizeMethodName(string? method)
    {
        method = Truncate(method, MaxMethodLength);
        if (method is null)
        {
            return null;
        }

        var sanitized = new StringBuilder(method.Length);
        foreach (var character in method)
        {
            sanitized.Append(character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'
                or '_' or '.' ? character : '_');
        }

        return sanitized.ToString();
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    private static void WriteStringOrNull(Utf8JsonWriter json, string property, string? value)
    {
        if (value is null)
        {
            json.WriteNull(property);
        }
        else
        {
            json.WriteString(property, value);
        }
    }

    private static bool IsStorageFailure(Exception error) => error is
        IOException or
        UnauthorizedAccessException or
        SecurityException or
        ArgumentException or
        NotSupportedException;
}
