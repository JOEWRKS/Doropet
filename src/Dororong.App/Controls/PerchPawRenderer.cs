using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Interaction;
using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

// Local authored-paw deformation. Root, opposite paw and support geometry stay
// fixed. Rendering and hit testing share the same inverse deformation.
internal static class PerchPawRenderer
{
    private const int Size = 100, FirstRow = 64, LastRow = 80;
    private const double RootY = 63, PawLength = 14;

    internal static bool? HitTest(BitmapSource source, PerchPawSnapshot? paw, PointD point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || point.Y < FirstRow || point.Y >= Size || point.X < 0 || point.X >= Size)
            return null;
        var original = PremultipliedFrame.From(source).Pixels;
        var x = (int)point.X; var y = (int)point.Y;
        bool? authored = y < LastRow && original[(y * Size + x) * 4 + 3] != 0
            ? x is >= 29 and < 43 ? false : x is >= 43 and < 58 ? true : null
            : null;
        if (paw is null || authored == !paw.Right) return authored;
        return Sample(original, paw.Right, Inverse(point, paw), 3) > 0 ? paw.Right : null;
    }

    internal static BitmapSource Render(BitmapSource source, PerchPawSnapshot paw)
    {
        if (paw.Offset.X == 0 && paw.Offset.Y == 0 && paw.Angle == 0) return source;
        var original = PremultipliedFrame.From(source).Pixels;
        var result = (byte[])original.Clone();
        var (left, right) = Bounds(paw.Right);
        for (var y = FirstRow; y < LastRow; y++)
            for (var x = left; x < right; x++) Array.Clear(result, (y * Size + x) * 4, 4);

        for (var y = FirstRow; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var at = (y * Size + x) * 4;
            // Other limb remains in front when the selected limb bends inward.
            if ((paw.Right ? x >= 29 && x < 43 : x >= 43 && x < 58) && original[at + 3] != 0) continue;
            var sourcePoint = Inverse(new(x, y), paw);
            var alpha = Sample(original, paw.Right, sourcePoint, 3);
            if (alpha == 0) continue;
            for (var c = 0; c < 4; c++)
                result[at + c] = (byte)Math.Clamp(Math.Round(Sample(original, paw.Right, sourcePoint, c) + result[at + c] * (1 - alpha / 255)), 0, 255);
        }

        // Retain untouched source bytes exactly; round-tripping every pixel
        // through premultiplication would change fine hair/ribbon edge colours.
        var straight = new byte[result.Length];
        new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0).CopyPixels(straight, Size * 4, 0);
        for (var i = 0; i < result.Length; i += 4)
        {
            if (result.AsSpan(i, 4).SequenceEqual(original.AsSpan(i, 4))) continue;
            var alpha = result[i + 3]; straight[i + 3] = alpha;
            for (var c = 0; c < 3; c++)
                straight[i + c] = alpha == 0 ? (byte)0 : (byte)Math.Clamp(Math.Round(result[i + c] * 255d / alpha), 0, 255);
        }
        var bitmap = BitmapSource.Create(Size, Size, 96, 96, PixelFormats.Bgra32, null, straight, Size * 4);
        bitmap.Freeze(); return bitmap;
    }

    private static (int Left, int Right) Bounds(bool right) => right ? (43, 58) : (29, 43);

    private static PointD Inverse(PointD point, PerchPawSnapshot paw)
    {
        var stretch = Math.Clamp(1 + paw.Offset.Y / PawLength, .6, 2.3);
        var bend = paw.Offset.X + Math.Tan(paw.Angle * Math.PI / 180) * PawLength;
        // Do not hide the whole paw behind its neighbour on inward gestures.
        bend = paw.Right ? Math.Max(-2.5, bend) : Math.Min(2.5, bend);
        var y = (point.Y - RootY) / stretch;
        return new(point.X - bend * Math.Clamp(y / PawLength, 0, 1), y + RootY);
    }

    private static double Sample(byte[] pixels, bool rightPaw, PointD point, int channel)
    {
        var (left, right) = Bounds(rightPaw);
        var x = (int)Math.Floor(point.X); var y = (int)Math.Floor(point.Y);
        if (x < left - 1 || x >= right || y < FirstRow - 1 || y >= LastRow) return 0;
        var fx = point.X - x; var fy = point.Y - y;
        // The fixed shoulder row is the interpolation apron, not transparent
        // padding. Stretch samples between it and FirstRow; dropping it makes
        // the join translucent and reveals a dark background as a false line.
        // Output still begins at FirstRow, so the head itself stays byte-exact.
        double Texel(int xx, int yy) => xx >= left && xx < right && yy >= FirstRow - 1 && yy < LastRow
            ? pixels[(yy * Size + xx) * 4 + channel] : 0;
        return Texel(x, y) * (1 - fx) * (1 - fy) + Texel(x + 1, y) * fx * (1 - fy)
            + Texel(x, y + 1) * (1 - fx) * fy + Texel(x + 1, y + 1) * fx * fy;
    }
}
