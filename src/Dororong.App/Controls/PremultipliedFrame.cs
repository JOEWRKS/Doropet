using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Controls;

internal sealed class PremultipliedFrame
{
    internal PremultipliedFrame(BitmapSource source, byte[] pixels, int stride)
    {
        Source = source;
        Pixels = pixels;
        Stride = stride;
    }

    internal BitmapSource Source { get; }

    internal byte[] Pixels { get; }

    internal int Stride { get; }

    internal static PremultipliedFrame From(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return new PremultipliedFrame(source, pixels, stride);
    }
}
