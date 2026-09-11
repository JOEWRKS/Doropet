using System.Windows.Media;
using Dororong.App.Controls;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Interaction;

internal sealed class CheekPullCapture
{
    private readonly byte[] _pixels;
    private readonly PointD _outwardUnit;
    private readonly Func<double,double,double,byte[]>? _render;
    private readonly PointD _headAxis;
    internal Matrix SourceToWindow { get; }
    internal FacingDirection Facing { get; }
    internal PointD OutwardUnit => _outwardUnit;

    internal CheekPullCapture(byte[] pixels, Matrix sourceToWindow, FacingDirection facing,
        Func<double,double,double,byte[]>? render=null,PointD? headAxis=null)
    {
        if (pixels.Length != 96 * 96 * 4) throw new ArgumentException("96x96 BGRA source required.", nameof(pixels));
        var values = new[] { sourceToWindow.M11, sourceToWindow.M12, sourceToWindow.M21, sourceToWindow.M22, sourceToWindow.OffsetX, sourceToWindow.OffsetY };
        if (!sourceToWindow.HasInverse || values.Any(v => !double.IsFinite(v))) throw new ArgumentException("Finite invertible capture required.", nameof(sourceToWindow));
        _headAxis=headAxis ?? new(1,0);_render=render;
        var axis=sourceToWindow.Transform(new System.Windows.Vector(_headAxis.X,_headAxis.Y));
        var length = axis.Length;
        if (!double.IsFinite(length) || length <= 0) throw new ArgumentException("Finite source axis required.", nameof(sourceToWindow));
        _outwardUnit = new(-axis.X / length, -axis.Y / length);
        _pixels = (byte[])pixels.Clone(); SourceToWindow = sourceToWindow; Facing = facing;
    }

    // DIP projection, not a vertical-to-strength bonus or frame-space scale guess.
    internal double Measure(PointD delta) => delta.X * _outwardUnit.X + delta.Y * _outwardUnit.Y;
    internal CheekPullCapture TurnToward(double horizontalDelta)
    {
        if (!double.IsFinite(horizontalDelta) || Math.Abs(horizontalDelta) < 4 ||
            Math.Sign(horizontalDelta) == Math.Sign(_outwardUnit.X)) return this;
        // Reflect in source space around the body/image center. The window and
        // vertical support stay fixed; only the orientation of the frozen art changes.
        var reflected = new Matrix(-1, 0, 0, 1, 96, 0);
        reflected.Append(SourceToWindow);
        return new(_pixels, reflected, reflected.M11 < 0 ? FacingDirection.Left : FacingDirection.Right,_render,_headAxis);
    }
    internal byte[] Render(double pullDips, double eyePull = 20, double hairPull = 20)
        => _render?.Invoke(pullDips,eyePull,hairPull) ?? OutlineCheekRenderer.Render(_pixels, pullDips, eyePull, hairPull);
}

internal sealed record CheekPullSnapshot(CheekPullCapture Capture, double PullDips);
