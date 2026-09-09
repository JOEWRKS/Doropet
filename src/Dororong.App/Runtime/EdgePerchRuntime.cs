using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Runtime;

internal sealed class EdgePerchRuntime
{
    internal EdgePerchRuntime(Func<FacingDirection, PerchContact?> measure) =>
        _measure = measure ?? throw new ArgumentNullException(nameof(measure));

    private readonly Func<FacingDirection, PerchContact?> _measure;
    private readonly EdgePerch _engine = new();
    private PerchContact? _contact;
    internal EdgePerchPose Current => _engine.Current;
    internal FacingDirection Facing { get; private set; } = FacingDirection.Right;

    internal bool CanBegin(PointD displayed, FacingDirection facing, IReadOnlyList<PerchSurface> surfaces,
        IReadOnlyList<DesktopMonitor> monitors) =>
        Current.Phase == EdgePerchPhase.None && _measure(facing) is { } contact &&
        _engine.CanBegin(displayed, contact, surfaces, monitors);

    internal bool TryBegin(PointD displayed, FacingDirection facing, IReadOnlyList<PerchSurface> surfaces,
        IReadOnlyList<DesktopMonitor> monitors, bool carryReleased, bool pointerReliable, bool sceneReliable)
    {
        if (!carryReleased || !pointerReliable || !sceneReliable || _engine.Current.Phase != EdgePerchPhase.None)
            return false;
        var contact = _measure(facing);
        if (contact is null || !_engine.TryBegin(displayed, contact.Value, surfaces, monitors, carryReleased: true))
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
        _engine.Release(displayed);
        _contact = null;
    }
}
