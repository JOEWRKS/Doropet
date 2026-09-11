using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.IO;
using System.IO.Compression;

namespace Dororong.App.Controls;

internal static class LocomotionFrames
{
    private const int EyeFrames = 65 + 8 * 32;
    internal static IReadOnlyList<BitmapSource> All { get; } = Load();
    private static readonly HashSet<BitmapSource> Frames = new(All);

    internal static bool Contains(object? frame) => frame is BitmapSource bitmap && Frames.Contains(bitmap);

    internal static BitmapSource Sit(double amount, bool closed = false)
    {
        Validate(amount);
        return All[(closed ? EyeFrames : 0) + (int)Math.Round(Math.Clamp(amount, 0, 1) * 64)];
    }

    internal static BitmapSource Walk(double amplitude, double distance, bool closed = false)
    {
        Validate(amplitude);
        Validate(distance);
        var level = (int)Math.Round(Math.Clamp(amplitude, 0, 1) * 8);
        if (level == 0) return Sit(0, closed);
        var phase = (int)Math.Round(((distance % 10 + 10) % 10) / 10 * 32) % 32;
        return All[(closed ? EyeFrames : 0) + 65 + (level - 1) * 32 + phase];
    }

    private static void Validate(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static IReadOnlyList<BitmapSource> Load()
    {
        using var resource = typeof(LocomotionFrames).Assembly.GetManifestResourceStream("Dororong.App.Assets.locomotion.pbgra.gz")
            ?? throw new InvalidDataException("Missing approved locomotion bank.");
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        using var reader = new BinaryReader(gzip);
        if (new string(reader.ReadChars(4)) != "LOCO" || reader.ReadUInt32() != 1 ||
            reader.ReadUInt32() != 96 || reader.ReadUInt32() != 96 || reader.ReadUInt32() != EyeFrames * 2)
            throw new InvalidDataException("Invalid locomotion bank geometry/version.");
        var frames = new BitmapSource[EyeFrames * 2];
        for (var n = 0; n < frames.Length; n++)
        {
            var bytes = reader.ReadBytes(96 * 96 * 4);
            if (bytes.Length != 96 * 96 * 4) throw new InvalidDataException("Truncated locomotion frame.");
            frames[n] = BitmapSource.Create(96, 96, 96, 96, PixelFormats.Pbgra32, null, bytes, 96 * 4);
            frames[n].Freeze();
        }
        if (gzip.ReadByte() != -1) throw new InvalidDataException("Unexpected locomotion bank payload.");
        return Array.AsReadOnly(frames);
    }
}
