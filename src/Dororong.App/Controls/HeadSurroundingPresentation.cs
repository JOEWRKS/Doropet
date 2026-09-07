using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

internal sealed class HeadSurroundingPresentation
{
    private static readonly ConditionalWeakTable<BitmapSource, PremultipliedFrame> Frames = new();
    private readonly SurroundingPullMotion _motion = new();
    private bool _settling;
    private PointD? _previousPointer;
    private bool _holding;

    internal void Advance(DirectInteractionSnapshot snapshot, double elapsedMilliseconds)
    {
        var dt = elapsedMilliseconds;
        var settling = snapshot.Phase == DirectInteractionPhase.BodyDragSettle;
        if (settling && !_settling) { _motion.Release(); dt = 0; }
        _settling = settling;
        var offset = snapshot.PointerPosition - snapshot.PressOrigin;
        var length = BodyPullSession.Length(offset);
        var target = double.IsFinite(length) && length > 0
            ? BodyPullSession.Scale(offset, 14 * Math.Clamp(snapshot.Strength, 0, 1) / length) : default;
        var holding = snapshot.Phase == DirectInteractionPhase.BodyDragHold;
        if (holding && _holding)
        {
            // Once carried, the whole pet follows the mouse. Secondary lag follows
            // its recent velocity, not its distance from an obsolete press origin.
            target = dt > 0 && dt <= 250 && _previousPointer is { } previous
                ? BodyPullSession.Scale(snapshot.PointerPosition - previous, 75 / dt) : default;
            var speed = BodyPullSession.Length(target);
            if (!double.IsFinite(speed) || speed < .6) target = default;
            else if (speed > 14) target = BodyPullSession.Scale(target, 14 / speed);
        }
        _holding = holding;
        _previousPointer = snapshot.PointerPosition;
        // Simulation stays in screen axes; convert only at raster presentation.
        _motion.Step(target, double.IsFinite(dt) ? Math.Clamp(dt, 0, 250) : 0);
    }

    internal BitmapSource Render(BitmapSource source, CapturedHeadAnchor anchor, double displayedAngle)
    {
        if (_motion.Current == default && _motion.Far == default) return source;
        var radians = displayedAngle * Math.PI / 180;
        var cosine = Math.Cos(radians); var sine = Math.Sin(radians);
        // Presentation is scale (including facing), then rotation. Invert in reverse order.
        PointD Local(PointD value) => new((value.X * cosine + value.Y * sine) *
            (anchor.Facing == FacingDirection.Left ? -1 : 1), -value.X * sine + value.Y * cosine);
        var frame = Frames.GetValue(source, PremultipliedFrame.From);
        var padded = SurroundingPullRenderer.Head(frame.Pixels, HeadPullAnchoring.SourcePoint(source, anchor),
            Local(_motion.Current), Local(_motion.Far));
        var pixels = new byte[96 * 96 * 4];
        for (var y = 0; y < 96; y++) Array.Copy(padded, ((y + 32) * 160 + 32) * 4, pixels, y * 384, 384);
        var result = BitmapSource.Create(96, 96, 96, 96, PixelFormats.Pbgra32, null, pixels, 384);
        result.Freeze();
        return result;
    }
    internal void Reset() { _motion.Reset(); _settling = false; _holding = false; _previousPointer = null; }
}
