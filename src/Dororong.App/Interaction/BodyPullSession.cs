using System.Windows.Media;
using Dororong.Core.Geometry;

namespace Dororong.App.Interaction;

internal enum BodyPullPhase { Idle, Pressed, Pulling, Carried, Settling }
internal sealed class BodyPullCapture
{
    private readonly byte[] _pixels;
    internal BodyPullCapture(BodyRegion region, PointD anchor, Matrix sourceToWindow, ReadOnlySpan<byte> pixels)
    {
        if (pixels.Length != 96 * 96 * 4) throw new ArgumentException("A 96x96 source is required.", nameof(pixels));
        Region = region; Anchor = anchor; SourceToWindow = sourceToWindow; _pixels = pixels.ToArray();
    }
    internal BodyRegion Region { get; }
    internal PointD Anchor { get; }
    internal Matrix SourceToWindow { get; }
    internal ReadOnlySpan<byte> Pixels => _pixels;
}
internal readonly record struct BodyPullSnapshot(BodyRegion Region, BodyPullPhase Phase, PointD WindowPosition, PointD PullSource, PointD AnchorSource, BodyPullCapture? Capture)
{
    internal bool RequiresCapture => Phase is BodyPullPhase.Pressed or BodyPullPhase.Pulling or BodyPullPhase.Carried;
}
internal sealed class BodyPullSession
{
    private PointD _press, _initialWindow, _root, _tip, _target, _lead, _filtered, _settlePull;
    private Matrix _toWindow, _toSource;
    private double _pending, _settleElapsed;
    internal BodyPullSnapshot Current { get; private set; }
    internal bool Begin(BodyPullCapture capture, PointD window, PointD globalPress)
    {
        if (Current.Phase != BodyPullPhase.Idle || capture.Region == BodyRegion.None || !Finite(window) || !Finite(globalPress) || !capture.SourceToWindow.HasInverse) return false;
        _toWindow = capture.SourceToWindow; _toSource = _toWindow; _toSource.Invert();
        _press = globalPress; _initialWindow = window;
        (_root, _tip) = BodyRegionMap.Limb(capture.Region, capture.Anchor);
        _target = _lead = _filtered = default; _pending = 0;
        Current = new(capture.Region, BodyPullPhase.Pressed, window, default, capture.Anchor, capture);
        return true;
    }
    internal void Move(PointD pointer)
    {
        if (!Current.RequiresCapture || !Finite(pointer)) return;
        var next = Vector(_toSource, pointer - _press);
        var d = next - _target; var length = Length(d);
        if (length > .8) _target = next - Scale(d, .8 / length);
    }
    internal void Tick(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds <= 0) return;
        if (Current.Phase == BodyPullPhase.Settling)
        {
            _settleElapsed += milliseconds;
            var t = Math.Min(1, _settleElapsed / 200); var q = 1 - t * t * (3 - 2 * t);
            Current = Current with { PullSource = Scale(_settlePull, q) };
            if (t == 1) Current = Current with { Phase = BodyPullPhase.Idle, PullSource = default, Capture = null };
            return;
        }
        if (!Current.RequiresCapture) return;
        _pending = Math.Min(_pending + milliseconds, 250);
        var k = 1 - Math.Exp(-4d / 55);
        while (_pending + 1e-8 >= 4)
        {
            _lead += Scale(_target - _lead, k); _filtered += Scale(_lead - _filtered, k);
            if (Current.Phase != BodyPullPhase.Carried)
                Current = Current with { Phase = Length(_filtered) >= 18 ? BodyPullPhase.Carried : BodyPullPhase.Pulling };
            SyncPull(allowWindowMovement: true);
            if (Current.Phase == BodyPullPhase.Carried)
            {
                Current = Current with { WindowPosition = Current.WindowPosition + Vector(_toWindow, Scale(Current.PullSource, 1 - Math.Exp(-4d / 75))) };
                SyncPull(allowWindowMovement: true);
            }
            _pending = Math.Max(0, _pending - 4);
        }
    }
    private void SyncPull(bool allowWindowMovement)
    {
        var pull = _filtered - Vector(_toSource, Current.WindowPosition - _initialWindow);
        var reach = _tip - _root + pull; var length = Length(reach);
        var frontPaw = Current.Region == BodyRegion.FrontPaw;
        var maximumReach = frontPaw ? 22d : 32d;
        if ((frontPaw || Current.Phase == BodyPullPhase.Carried) && length > maximumReach)
        {
            // The small front paw can reach its shorter cap before the normal
            // carry threshold. Hand excess to the window, never just shrink the
            // rendered paw away from the filtered grab point. Other regions retain32.
            if (frontPaw) Current = Current with { Phase = BodyPullPhase.Carried };
            var excess = Scale(reach, 1 - maximumReach / length);
            if (allowWindowMovement) Current = Current with { WindowPosition = Current.WindowPosition + Vector(_toWindow, excess) };
            pull -= excess;
        }
        Current = Current with { PullSource = pull };
    }
    internal void Release()
    {
        if (!Current.RequiresCapture) return;
        _pending = 0; _settleElapsed = 0; _settlePull = Current.PullSource;
        Current = Current with { Phase = BodyPullPhase.Settling };
    }
    internal void Reconcile(PointD clampedPosition)
    {
        if (!Finite(clampedPosition)) return;
        Current = Current with { WindowPosition = clampedPosition };
        if (Current.RequiresCapture) SyncPull(allowWindowMovement: false);
    }
    internal void Cancel()
    {
        Current = Current with { Phase = BodyPullPhase.Idle, PullSource = default, Capture = null };
        _pending = 0;
    }
    internal static PointD Vector(Matrix m, PointD p) => new(m.M11 * p.X + m.M21 * p.Y, m.M12 * p.X + m.M22 * p.Y);
    internal static PointD Scale(PointD p, double amount) => new(p.X * amount, p.Y * amount);
    internal static double Length(PointD p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);
    private static bool Finite(PointD p) => double.IsFinite(p.X) && double.IsFinite(p.Y);
}
