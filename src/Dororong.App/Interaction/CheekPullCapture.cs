using System.Windows.Media;
using Dororong.App.Controls;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Interaction;

internal sealed class CheekPullCapture
{
    private readonly byte[] _pixels;
    private readonly PointD _outwardUnit;
    internal Matrix SourceToWindow { get; }
    internal FacingDirection Facing { get; }
    internal PointD OutwardUnit => _outwardUnit;

    internal CheekPullCapture(byte[] pixels, Matrix sourceToWindow, FacingDirection facing)
    {
        if (pixels.Length != 96 * 96 * 4) throw new ArgumentException("96x96 BGRA source required.", nameof(pixels));
        var values = new[] { sourceToWindow.M11, sourceToWindow.M12, sourceToWindow.M21, sourceToWindow.M22, sourceToWindow.OffsetX, sourceToWindow.OffsetY };
        if (!sourceToWindow.HasInverse || values.Any(v => !double.IsFinite(v))) throw new ArgumentException("Finite invertible capture required.", nameof(sourceToWindow));
        var length = Math.Sqrt(sourceToWindow.M11 * sourceToWindow.M11 + sourceToWindow.M12 * sourceToWindow.M12);
        if (!double.IsFinite(length) || length <= 0) throw new ArgumentException("Finite source axis required.", nameof(sourceToWindow));
        _outwardUnit = new(-sourceToWindow.M11 / length, -sourceToWindow.M12 / length);
        _pixels = (byte[])pixels.Clone(); SourceToWindow = sourceToWindow; Facing = facing;
    }

    // DIP projection, not a vertical-to-strength bonus or frame-space scale guess.
    internal double Measure(PointD delta) => delta.X * _outwardUnit.X + delta.Y * _outwardUnit.Y;
    internal byte[] Render(double pullDips, double eyePull = 20, double hairPull = 20)
        => OutlineCheekRenderer.Render(_pixels, pullDips, eyePull, hairPull);
}

internal sealed record CheekPullSnapshot(CheekPullCapture Capture, double PullDips);
