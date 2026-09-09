using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.CompilerServices;
using Dororong.Core.Platforms;
using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

internal sealed class PlatformContactPresentation
{
    internal RectD VisibleBounds { get; private set; }
    // Weak keys allow generated frozen crossfade frames to be collected. Mutable
    // bitmaps are read afresh, so a changed alpha mask cannot reuse stale contact.
    private static readonly ConditionalWeakTable<BitmapSource, Point[]> Corners = new();
    private Point[] _measured = [];
    private FrameworkElement? _body;
    private Transform? _baseTransform;
    private Point _baseOrigin;

    // The transform maps source pixel coordinates (not the image rectangle) to
    // presenter DIPs. Retain real occupied corners for the subsequent pose too.
    internal FootContact Measure(BitmapSource source, GeneralTransform imageToPresenter)
    {
        var corners = source.IsFrozen ? Corners.GetValue(source, ReadCorners) : ReadCorners(source);
        _measured = new Point[corners.Length];
        var left = double.PositiveInfinity; var right = double.NegativeInfinity;
        var top = double.PositiveInfinity; var bottom = double.NegativeInfinity;
        var fullLeft = double.PositiveInfinity; var fullRight = double.NegativeInfinity;
        // Snapshot the WPF affine chain once, not once per occupied pixel corner.
        // Live TransformGroup.Value traversal dominates otherwise (many thousands
        // of dependency-property/tree evaluations per contact, several times/tick).
        var affine = TryGetAffine(imageToPresenter, out var matrix);
        for (var i = 0; i < corners.Length; i++)
        {
            var p = affine ? matrix.Transform(corners[i]) : imageToPresenter.Transform(corners[i]); _measured[i] = p;
            top = Math.Min(top, p.Y); bottom = Math.Max(bottom, p.Y);
            fullLeft = Math.Min(fullLeft, p.X); fullRight = Math.Max(fullRight, p.X);
        }
        VisibleBounds = new(fullLeft, top, fullRight - fullLeft, bottom - top);
        // Support is the current lower 2-DIP silhouette band, not the head's
        // horizontal extent or a source row that ceases to be lowest on tilt.
        // Each quad is clipped before taking its X envelope. Merely touching
        // the band's upper edge contributes no positive-area support.
        var bandTop = bottom - 2;
        ReadOnlySpan<int> order = [0, 1, 3, 2];
        for (var i = 0; i < _measured.Length; i += 4)
        {
            var lowest = Math.Max(Math.Max(_measured[i].Y, _measured[i + 1].Y), Math.Max(_measured[i + 2].Y, _measured[i + 3].Y));
            // Trigonometric roundoff at e.g. 90 degrees is not real area.
            if (lowest <= bandTop + 1e-9) continue;
            for (var edge = 0; edge < 4; edge++)
            {
                var a = _measured[i + order[edge]]; var b = _measured[i + order[(edge + 1) % 4]];
                if (a.Y >= bandTop) { left = Math.Min(left, a.X); right = Math.Max(right, a.X); }
                if ((a.Y < bandTop && b.Y > bandTop) || (a.Y > bandTop && b.Y < bandTop))
                {
                    var x = a.X + (b.X - a.X) * ((bandTop - a.Y) / (b.Y - a.Y));
                    left = Math.Min(left, x); right = Math.Max(right, x);
                }
            }
        }
        return new(left, right, bottom, top);
    }

    private static bool TryGetAffine(GeneralTransform transform, out Matrix matrix)
    {
        if (transform is Transform affine)
        {
            matrix = affine.Value;
            return true;
        }
        matrix = Matrix.Identity;
        if (transform is not GeneralTransformGroup group || group.Children is not { Count: > 0 }) return false;
        foreach (var child in group.Children)
        {
            if (!TryGetAffine(child, out var next)) return false;
            matrix.Append(next);
        }
        return true;
    }

    internal void Apply(FrameworkElement body, FootContact current, double targetSoleY, double squash, double sway, bool extreme = false)
    {
        Restore();
        if (_measured.Length == 0 || !double.IsFinite(targetSoleY) || !double.IsFinite(current.SoleY) ||
            !double.IsFinite(current.Left) || !double.IsFinite(current.Right)) return;
        squash = double.IsFinite(squash) ? Math.Clamp(squash, extreme ? -.16 : -.10, extreme ? .82 : .30) : 0;
        var angle = double.IsFinite(sway) ? Math.Clamp(sway * 180 / Math.PI, -2, 2) : 0;
        var center = new Point((current.Left + current.Right) / 2, current.SoleY);
        var pose = Matrix.Identity;
        pose.ScaleAt(1 + squash * .5, 1 - squash, center.X, center.Y);
        pose.RotateAt(angle, center.X, center.Y);
        var soleAfterPose = double.NegativeInfinity;
        foreach (var corner in _measured) soleAfterPose = Math.Max(soleAfterPose, pose.Transform(corner).Y);
        var correctionY = targetSoleY - soleAfterPose;

        _body = body; _baseTransform = body.RenderTransform; _baseOrigin = body.RenderTransformOrigin;
        var origin = new Point(body.ActualWidth * _baseOrigin.X, body.ActualHeight * _baseOrigin.Y);
        var layout = VisualTreeHelper.GetOffset(body);
        // Preserve the original transform objects and their WPF origin. The
        // platform layer acts in presenter coordinates after that base pose.
        var composition = new TransformGroup();
        composition.Children.Add(new TranslateTransform(-origin.X, -origin.Y));
        composition.Children.Add(_baseTransform);
        composition.Children.Add(new TranslateTransform(origin.X + layout.X, origin.Y + layout.Y));
        composition.Children.Add(new MatrixTransform(pose));
        composition.Children.Add(new TranslateTransform(-layout.X, correctionY - layout.Y));
        body.RenderTransformOrigin = new(0, 0);
        body.RenderTransform = composition;
        body.UpdateLayout();
    }

    internal void Restore()
    {
        if (_body is null) return;
        _body.RenderTransform = _baseTransform!;
        _body.RenderTransformOrigin = _baseOrigin;
        _body.UpdateLayout();
        _body = null; _baseTransform = null;
    }

    private static Point[] ReadCorners(BitmapSource source)
    {
        var bitmap = source.Format == PixelFormats.Pbgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        var stride = checked(bitmap.PixelWidth * 4); var pixels = new byte[checked(stride * bitmap.PixelHeight)];
        bitmap.CopyPixels(pixels, stride, 0);
        var corners = new List<Point>();
        for (var y = 0; y < bitmap.PixelHeight; y++) for (var x = 0; x < bitmap.PixelWidth; x++)
        {
            if (pixels[y * stride + x * 4 + 3] < 128) continue;
            corners.Add(new(x, y)); corners.Add(new(x + 1, y));
            corners.Add(new(x, y + 1)); corners.Add(new(x + 1, y + 1));
        }
        return corners.ToArray();
    }
}
