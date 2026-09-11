using System.IO;
using System.IO.Compression;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Controls;

internal sealed class HuntFrames
{
    internal static HuntFrames Instance { get; } = new();
    private BitmapSource[] Bodies { get; } = new BitmapSource[157];
    private byte[][] Heads { get; } = new byte[2][];
    private byte[][] EnlargedHeads { get; } = new byte[2][];
    internal BitmapSource Body(int index) => Bodies[index];
    internal ReadOnlyMemory<byte> Head(int index) => Heads[index];
    internal void CopyEnlargedHead(int index, Span<byte> destination) => EnlargedHeads[index].AsSpan().CopyTo(destination);

    private HuntFrames()
    {
        using var resource = typeof(HuntFrames).Assembly.GetManifestResourceStream("Dororong.App.Assets.hunting.pbgra.gz")
            ?? throw new InvalidDataException("Missing approved hunting layers.");
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        using var reader = new BinaryReader(gzip);
        if (new string(reader.ReadChars(4)) != "HUNT" || reader.ReadUInt32() != 1 || reader.ReadUInt32() != 256 ||
            reader.ReadUInt32() != 256 || reader.ReadUInt32() != 157) throw new InvalidDataException("Invalid hunting bank.");
        byte[] Read(int count)
        {
            var data = reader.ReadBytes(count);
            return data.Length == count ? data : throw new InvalidDataException("Truncated hunting bank.");
        }
        for (var i = 0; i < Bodies.Length; i++)
        {
            Bodies[i] = BitmapSource.Create(256, 256, 96, 96, PixelFormats.Pbgra32, null, Read(256 * 256 * 4), 1024);
            Bodies[i].Freeze();
        }
        for (var i = 0; i < 2; i++) { Heads[i] = Read(96 * 96 * 4); EnlargedHeads[i] = Read(384 * 384 * 4); }
        if (gzip.ReadByte() != -1) throw new InvalidDataException("Unexpected hunting payload.");
    }
}
