using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Controls;

// Register source keys before Sample() blends them. This is integer translation,
// not new artwork, scaling, or a change to the animation's sequence/timing.
internal static class BodyDragFootAlignment
{
    internal static PremultipliedFrame Align(PremultipliedFrame frame, PremultipliedFrame reference)
    {
        var offsetY = SoleRow(reference) - SoleRow(frame);
        if (offsetY == 0) return frame;

        var width = frame.Source.PixelWidth;
        var height = frame.Source.PixelHeight;
        var pixels = new byte[frame.Pixels.Length];
        for (var y = 0; y < height; y++)
        {
            var destinationY = y + offsetY;
            if (destinationY < 0 || destinationY >= height)
            {
                for (var x = 0; x < width; x++)
                    if (frame.Pixels[y * frame.Stride + x * 4 + 3] != 0)
                        throw new InvalidOperationException("Foot alignment would clip visible source pixels.");
                continue;
            }
            Array.Copy(frame.Pixels, y * frame.Stride, pixels, destinationY * frame.Stride, frame.Stride);
        }

        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, frame.Stride);
        source.Freeze();
        return new PremultipliedFrame(source, pixels, frame.Stride);
    }

    private static int SoleRow(PremultipliedFrame frame)
    {
        // Ignore faint antialias fringes when locating the visible sole; the
        // translation itself retains ALL nonzero-alpha pixels, including fringes.
        for (var y = frame.Source.PixelHeight - 1; y >= 0; y--)
        for (var x = 0; x < frame.Source.PixelWidth; x++)
            if (frame.Pixels[y * frame.Stride + x * 4 + 3] >= 128) return y;
        throw new InvalidOperationException("A foot reference requires a visible opaque body.");
    }
}
