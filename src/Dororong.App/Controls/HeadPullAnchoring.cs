using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

internal sealed record CapturedHeadAnchor(PointD RelativePoint, PointD WindowPoint, FacingDirection Facing);

// Position-only registration. Rounded pink-hair centroids are landmarks, not a
// claim of exact pixel correspondence/anatomy. No source pixels are changed.
internal static class HeadPullAnchoring
{
    private sealed record Landmark(PointD Point, bool Found, Rect InkBounds);
    private static readonly ConditionalWeakTable<BitmapSource, Landmark> Landmarks = new();

    internal static CapturedHeadAnchor? Capture(Image image, FrameworkElement presenter, PointD windowPoint, FacingDirection facing)
    {
        if (!HeadPullDistance.IsFinite(windowPoint) || image.Source is not BitmapSource source ||
            image.ActualWidth <= 0 || image.ActualHeight <= 0) return null;
        var inverse = image.TransformToAncestor(presenter).Inverse;
        if (inverse is null) return null;
        var local = inverse.Transform(new Point(windowPoint.X, windowPoint.Y));
        var scale = Math.Min(image.ActualWidth / source.PixelWidth, image.ActualHeight / source.PixelHeight);
        var point = new PointD((local.X - (image.ActualWidth - source.PixelWidth * scale) / 2) / scale,
            (local.Y - (image.ActualHeight - source.PixelHeight * scale) / 2) / scale);
        if (!HeadPullDistance.IsFinite(point) || point.X < 0 || point.Y < 0 || point.X >= source.PixelWidth || point.Y >= source.PixelHeight) return null;
        var frame = PremultipliedFrame.From(source);
        if (frame.Pixels[(int)point.Y * frame.Stride + (int)point.X * 4 + 3] == 0) return null;
        var landmark = Landmarks.GetValue(source, Measure);
        return landmark.Found ? new(point - landmark.Point, windowPoint, facing) : null;
    }

    internal static PointD SourcePoint(BitmapSource source, CapturedHeadAnchor capture) =>
        Landmarks.GetValue(source, Measure).Point + capture.RelativePoint;

    internal static PointD Correction(Image image, FrameworkElement presenter, CapturedHeadAnchor capture, BitmapSource? registrationSource = null)
    {
        if (image.Source is not BitmapSource source) return default;
        // Secondary hair motion must not move the registration landmark underneath the cursor.
        var landmark = Landmarks.GetValue(registrationSource ?? source, Measure);
        if (!landmark.Found) return default;
        var point = landmark.Point + capture.RelativePoint;
        var scale = Math.Min(image.ActualWidth / source.PixelWidth, image.ActualHeight / source.PixelHeight);
        var local = image.TranslatePoint(new Point(point.X * scale + (image.ActualWidth - source.PixelWidth * scale) / 2,
            point.Y * scale + (image.ActualHeight - source.PixelHeight * scale) / 2), presenter);
        return capture.WindowPoint - new PointD(local.X, local.Y);
    }

    internal static bool FitsViewport(Image image, FrameworkElement presenter)
    {
        if (image.Source is not BitmapSource source) return false;
        var bounds = Landmarks.GetValue(source, Measure).InkBounds;
        return FitsBounds(image, presenter, source, bounds);
    }

    internal static bool FitsSwingViewport(Image image, FrameworkElement presenter, BitmapSource registrationSource)
    {
        var bounds = Landmarks.GetValue(registrationSource, Measure).InkBounds;
        if (bounds.IsEmpty) return true;
        // Head Warp writes only within original ink bounds +/-4 native pixels.
        // Its field is exactly zero at y>=84, so the pinned soles cannot expand.
        // Use this direction-independent support, not a changing deformed alpha
        // box, to keep equal speeds at equal angles during either-direction lag.
        var left = Math.Max(0, bounds.Left - 4);
        var top = Math.Max(0, bounds.Top - 4);
        var right = Math.Min(registrationSource.PixelWidth, bounds.Right + 4);
        var bottom = Math.Min(registrationSource.PixelHeight, Math.Max(bounds.Bottom, Math.Min(84, bounds.Bottom + 4)));
        return FitsBounds(image, presenter, registrationSource, new Rect(left, top, right - left, bottom - top));
    }

    private static bool FitsBounds(Image image, FrameworkElement presenter, BitmapSource source, Rect bounds)
    {
        if (bounds.IsEmpty) return true;
        var scale = Math.Min(image.ActualWidth / source.PixelWidth, image.ActualHeight / source.PixelHeight);
        var offsetX = (image.ActualWidth - source.PixelWidth * scale) / 2;
        var offsetY = (image.ActualHeight - source.PixelHeight * scale) / 2;
        foreach (var corner in new[] { bounds.TopLeft, bounds.TopRight, bounds.BottomLeft, bounds.BottomRight })
        {
            var point = image.TranslatePoint(new Point(corner.X * scale + offsetX, corner.Y * scale + offsetY), presenter);
            if (point.X < 1 || point.Y < 1 || point.X > presenter.ActualWidth - 1 || point.Y > presenter.ActualHeight - 1) return false;
        }
        return true;
    }

    private static Landmark Measure(BitmapSource source)
    {
        var frame = PremultipliedFrame.From(source);
        long sumX = 0, sumY = 0, count = 0;
        var minX = source.PixelWidth; var minY = source.PixelHeight; var maxX = -1; var maxY = -1;
        for (var y = 0; y < source.PixelHeight; y++) for (var x = 0; x < source.PixelWidth; x++)
        {
            var i = y * frame.Stride + x * 4; var p = frame.Pixels;
            if (p[i + 3] > 0)
            { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            // White body/background and purple eyes cannot pull this landmark.
            if (p[i + 3] < 128 || p[i + 2] <= p[i] + 12 || p[i] <= p[i + 1] + 12) continue;
            sumX += x; sumY += y; count++;
        }
        var bounds = maxX < 0 ? Rect.Empty : new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return count == 0 ? new(default, false, bounds) : new(new(Math.Round((double)sumX / count), Math.Round((double)sumY / count)), true, bounds);
    }
}
