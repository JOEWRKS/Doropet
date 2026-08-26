using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Controls;

public sealed class AlphaHitTestImage : Image
{
    private ImageSource? _cachedSource;
    private byte[]? _pixels;
    private int _pixelWidth;
    private int _pixelHeight;
    private int _stride;

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        if (!TryMapToSourcePixel(hitTestParameters.HitPoint, out var pixelX, out var pixelY) ||
            !TryCachePixels())
        {
            return null;
        }

        var alpha = _pixels![(pixelY * _stride) + (pixelX * 4) + 3];
        return alpha == 0
            ? null
            : new PointHitTestResult(this, hitTestParameters.HitPoint);
    }

    private bool TryMapToSourcePixel(Point point, out int pixelX, out int pixelY)
    {
        pixelX = 0;
        pixelY = 0;

        if (Source is not BitmapSource bitmap ||
            ActualWidth <= 0 ||
            ActualHeight <= 0 ||
            bitmap.PixelWidth <= 0 ||
            bitmap.PixelHeight <= 0)
        {
            return false;
        }

        var scale = Math.Min(
            ActualWidth / bitmap.PixelWidth,
            ActualHeight / bitmap.PixelHeight);
        var renderedWidth = bitmap.PixelWidth * scale;
        var renderedHeight = bitmap.PixelHeight * scale;
        var offsetX = (ActualWidth - renderedWidth) / 2;
        var offsetY = (ActualHeight - renderedHeight) / 2;

        if (point.X < offsetX ||
            point.Y < offsetY ||
            point.X >= offsetX + renderedWidth ||
            point.Y >= offsetY + renderedHeight)
        {
            return false;
        }

        pixelX = Math.Min(
            bitmap.PixelWidth - 1,
            (int)((point.X - offsetX) / scale));
        pixelY = Math.Min(
            bitmap.PixelHeight - 1,
            (int)((point.Y - offsetY) / scale));
        return true;
    }

    private bool TryCachePixels()
    {
        if (ReferenceEquals(_cachedSource, Source) && _pixels is not null)
        {
            return true;
        }

        if (Source is not BitmapSource bitmap)
        {
            return false;
        }

        var converted = new FormatConvertedBitmap(
            bitmap,
            PixelFormats.Bgra32,
            null,
            0);
        _pixelWidth = converted.PixelWidth;
        _pixelHeight = converted.PixelHeight;
        _stride = _pixelWidth * 4;
        _pixels = new byte[_stride * _pixelHeight];
        converted.CopyPixels(_pixels, _stride, 0);
        _cachedSource = Source;
        return true;
    }
}
