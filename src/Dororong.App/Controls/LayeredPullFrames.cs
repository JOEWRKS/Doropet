using System.IO;
using System.IO.Compression;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

// Frozen output of tools/GenerateLayeredPull; never rerun optical registration
// on the UI thread. Dense adjacent rasters allow a subframe release without
// switching back to the old whole-body mesh at button-up.
internal static class LayeredPullFrames
{
    private static readonly Lazy<PremultipliedFrame[]> Bank = new(Load);

    internal static BitmapSource Sample(double progress)
    {
        if (!double.IsFinite(progress)) throw new ArgumentOutOfRangeException(nameof(progress));
        var position = Math.Clamp(progress, 0, 1) * 112;
        var nearest = (int)Math.Round(position);
        var frames = Bank.Value;
        if (Math.Abs(position - nearest) < 1e-9) return frames[nearest].Source;
        var index = (int)Math.Floor(position);
        var t = position - index;
        var pixels = new byte[96 * 96 * 4];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = (byte)Math.Round(frames[index].Pixels[i] * (1-t) + frames[index+1].Pixels[i] * t);
        var result = Make(pixels);
        Register(result, frames[index].Source, frames[index+1].Source, t);
        return result;
    }

    private static PremultipliedFrame[] Load()
    {
        using var stream = typeof(LayeredPullFrames).Assembly.GetManifestResourceStream(
            "Dororong.App.Assets.layered-pull.pbgra.gz") ?? throw new InvalidDataException("Missing layered pull bank.");
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        var frames = new PremultipliedFrame[113];
        for (var i = 0; i < frames.Length; i++)
        {
            var pixels = new byte[96 * 96 * 4];
            gzip.ReadExactly(pixels);
            frames[i] = new(Make(pixels), pixels, 384);
        }
        if (gzip.ReadByte() != -1) throw new InvalidDataException("Unexpected layered bank length.");
        for (var i = 1; i < 112; i++)
            if (i % 16 != 0) Register(frames[i].Source, frames[i/16*16].Source, frames[(i/16+1)*16].Source, i%16/16d);
        return frames;
    }

    private static BitmapSource Make(byte[] pixels)
    {
        var source = BitmapSource.Create(96, 96, 96, 96, PixelFormats.Pbgra32, null, pixels, 384);
        source.Freeze();
        return source;
    }

    private static void Register(BitmapSource source, BitmapSource a, BitmapSource b, double t)
    {
        var from = HeadPullAnchoring.RegistrationPoint(a);
        var to = HeadPullAnchoring.RegistrationPoint(b);
        HeadPullAnchoring.RegisterRecoveryPoint(source, new PointD(from.X+(to.X-from.X)*t, from.Y+(to.Y-from.Y)*t));
    }
}
