using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Runtime;

internal sealed class EdgePerchRuntime
{
    internal EdgePerchRuntime(Func<FacingDirection, PerchContact?> measure, Func<double?>? measureHeldSole = null)
    {
        _measure = measure ?? throw new ArgumentNullException(nameof(measure));
        _measureHeldSole = measureHeldSole;
    }

    private readonly Func<FacingDirection, PerchContact?> _measure;
    private readonly Func<double?>? _measureHeldSole;
    private readonly EdgePerch _engine = new();
    private PerchContact? _contact;
    private Readiness? _readiness;
    private sealed record Readiness(PointD Position, FacingDirection Facing, PerchContact Contact,
        PlatformSurface Surface, DesktopMonitor Monitor);
    internal EdgePerchPose Current => _engine.Current;
    internal FacingDirection Facing { get; private set; } = FacingDirection.Right;

    internal bool CanBegin(PointD displayed, FacingDirection facing, IReadOnlyList<PerchSurface> surfaces,
        IReadOnlyList<DesktopMonitor> monitors, bool advertise = false)
    {
        if (advertise) ClearReadiness();
        if (Current.Phase != EdgePerchPhase.None || Measure(facing) is not { } contact ||
            _engine.FindCandidate(displayed, contact, surfaces, monitors) is not { } surface) return false;
        // Only the published cue retains its pre-render geometry. Pure queries
        // must not replace that decision with the next animation's silhouette.
        if (advertise && surface.Kind == PlatformKind.Taskbar)
            _readiness = new(displayed, facing, contact, surface, monitors.First(m => m.Id == surface.MonitorId));
        return true;
    }

    internal void ClearReadiness() => _readiness = null;

    internal bool TryBegin(PointD displayed, FacingDirection facing, IReadOnlyList<PerchSurface> surfaces,
        IReadOnlyList<DesktopMonitor> monitors, bool carryReleased, bool pointerReliable, bool sceneReliable,
        SizeD readbackTolerance = default)
    {
        var advertised = _readiness;
        ClearReadiness(); // A cue is usable only by the immediately following frame.
        if (!carryReleased || !pointerReliable || !sceneReliable || _engine.Current.Phase != EdgePerchPhase.None)
            return false;
        var contact = Measure(facing);
        var acquisitionPosition = displayed;
        if (advertised is not null && advertised.Facing == facing &&
            Math.Abs(advertised.Position.X - displayed.X) <= readbackTolerance.Width &&
            Math.Abs(advertised.Position.Y - displayed.Y) <= readbackTolerance.Height)
        {
            // Recheck the current scene with the exact geometry that advertised
            // readiness. A changed/disappeared/occluded target cannot be latched.
            if (!monitors.Contains(advertised.Monitor) ||
                _engine.FindCandidate(advertised.Position, advertised.Contact, surfaces, monitors) != advertised.Surface) return false;
            contact = advertised.Contact;
            // Retain the published continuous origin across one physical pixel
            // of host readback quantization, not a wider entry/search interval.
            acquisitionPosition = advertised.Position;
        }
        if (contact is null || !_engine.TryBegin(acquisitionPosition, contact.Value, surfaces, monitors, carryReleased: true))
            return false;
        _contact = contact; Facing = facing;
        return true;
    }

    internal EdgePerchPose Advance(TimeSpan delta, PointD displayed, IReadOnlyList<PerchSurface> surfaces,
        IReadOnlyList<DesktopMonitor> monitors, bool sceneReliable)
    {
        if (_contact is not { } contact) return _engine.Current;
        var pose = _engine.Advance(delta, displayed, contact, surfaces, monitors, sceneReliable);
        if (pose.Phase == EdgePerchPhase.None) _contact = null;
        return pose;
    }

    internal void Release(PointD displayed)
    {
        ClearReadiness();
        _engine.Release(displayed);
        _contact = null;
    }

    private PerchContact? Measure(FacingDirection facing) => _measure(facing) is { } contact
        ? contact with { HeldSoleY = _measureHeldSole?.Invoke() }
        : null;
}
