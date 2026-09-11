using System.IO;
using System.Security.Principal;

namespace Dororong.App.Product;

internal static class ProductIdentity
{
    internal static string DisplayName => "도로롱 (Dororong)";
    internal static string Version => "0.1.0";
    internal static string Publisher => "JOEWRKS";
    internal static string ExecutableName => "Dororong.exe";
    internal static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Publisher,
        "Dororong",
        "logs");
    internal static string InstanceName { get; } = BuildInstanceName();

    private static string BuildInstanceName()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("The current Windows user has no SID.");
        return $@"Local\JOEWRKS.Dororong.{sid}";
    }
}
