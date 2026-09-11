using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace Dororong.App.Tests.Product;

public sealed class CurrentRuntimeTests
{
    [Fact]
    public void Current_test_process_uses_requested_bundled_runtime()
    {
        using var process = Process.GetCurrentProcess();
        var coreClr = process.Modules.Cast<ProcessModule>()
            .Single(module => string.Equals(module.ModuleName, "coreclr.dll", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(coreClr.FileName), $"Loaded runtime module does not exist: {coreClr.FileName}");

        var expectedCoreClrPath = Environment.GetEnvironmentVariable("DORORONG_EXPECTED_CORECLR_PATH");
        var expectedCoreClrHash = Environment.GetEnvironmentVariable("DORORONG_EXPECTED_CORECLR_SHA256");
        Assert.Equal(string.IsNullOrEmpty(expectedCoreClrPath), string.IsNullOrEmpty(expectedCoreClrHash));
        if (string.IsNullOrEmpty(expectedCoreClrPath))
        {
            return;
        }

        Assert.Equal(Path.GetFullPath(expectedCoreClrPath), Path.GetFullPath(coreClr.FileName), ignoreCase: true);

        using var stream = File.OpenRead(coreClr.FileName);
        var actualHash = Convert.ToHexString(SHA256.HashData(stream));
        Assert.Equal(expectedCoreClrHash, actualHash, ignoreCase: true);
    }
}
