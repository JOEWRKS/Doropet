using Dororong.Core.Geometry;

namespace Dororong.Core.Platforms;

public enum EdgePerchPhase { None, Entering, Attached }

public readonly record struct PerchContact(double Left, double Right, double GripY, double VisibleTop)
{
    // Held silhouette's bottom in the same local coordinates as the grip.
    // Only entry uses this; attached registration always uses the neutral grip.
    public double? HeldSoleY { get; init; }
}

public readonly record struct PerchSurface(PlatformSurface Surface, int ZOrder);

public readonly record struct EdgePerchPose(
    EdgePerchPhase Phase,
    PointD Position,
    SurfaceKey? Owner,
    long? MonitorId);

public sealed class EdgePerch
{
    private const double EntrySeconds = .16;
    private const double MaximumGripDistance = 20;
    private const double GeometryRoundoff = .000001;

    private SurfaceKey? owner;
    private long? monitorId;
    private double ownerRelativeX;
    private double entryY;
    private double elapsedSeconds;

    public EdgePerchPose Current { get; private set; }

    public bool TryBegin(
        PointD displayed,
        PerchContact contact,
        IReadOnlyList<PerchSurface> surfaces,
        IReadOnlyList<DesktopMonitor> monitors,
        bool carryReleased)
    {
        if (!carryReleased || SelectCandidate(displayed, contact, surfaces, monitors) is not { } winner)
            return false;

        owner = winner.Perch.Surface.Key;
        monitorId = winner.Monitor.Id;
        ownerRelativeX = displayed.X - winner.Perch.Surface.OwnerOrigin.X;
        entryY = displayed.Y;
        elapsedSeconds = 0;
        Current = new(EdgePerchPhase.Entering, displayed, owner, monitorId);
        return true;
    }

    // Preview and release share the exact selection; preview never acquires an owner.
    public bool CanBegin(PointD displayed, PerchContact contact,
        IReadOnlyList<PerchSurface> surfaces, IReadOnlyList<DesktopMonitor> monitors) =>
        FindCandidate(displayed, contact, surfaces, monitors) is not null;

    public PlatformSurface? FindCandidate(PointD displayed, PerchContact contact,
        IReadOnlyList<PerchSurface> surfaces, IReadOnlyList<DesktopMonitor> monitors) =>
        SelectCandidate(displayed, contact, surfaces, monitors)?.Perch.Surface;

    private static EntryCandidate? SelectCandidate(PointD displayed, PerchContact contact,
        IReadOnlyList<PerchSurface> surfaces, IReadOnlyList<DesktopMonitor> monitors)
    {
        if (!Valid(displayed) || !Valid(contact) ||
            surfaces is null || monitors is null)
        {
            return null;
        }

        var worldLeft = displayed.X + contact.Left;
        var worldRight = displayed.X + contact.Right;
        var worldGrip = displayed.Y + contact.GripY;
        if (!Finite(worldLeft) || !Finite(worldRight) || !Finite(worldGrip))
        {
            return null;
        }

        EntryCandidate? selected = null;
        foreach (var candidate in surfaces)
        {
            var monitor = monitors.FirstOrDefault(item => item.Id == candidate.Surface.MonitorId);
            if (monitor is null || !Valid(monitor.Bounds) ||
                !ValidForMonitor(candidate.Surface, monitor) ||
                !Contains(candidate.Surface, monitor, worldLeft, worldRight))
            {
                continue;
            }

            var targetY = candidate.Surface.Top - contact.GripY;
            var entryStartY = targetY;
            var entryTolerance = 0d;
            if (candidate.Surface.Kind == PlatformKind.Taskbar &&
                candidate.Surface.Bottom is { } bottom &&
                Math.Abs(bottom - monitor.Bounds.Bottom) <= GeometryRoundoff && contact.HeldSoleY is { } sole)
            {
                // Independently mapped rectangle bottoms may differ by roundoff
                // at fractional DPI. A real physical-pixel gap is not a match.
                // The full held silhouette is clamped to the monitor bottom.
                // Move (do not widen) the entry band into reachable space when
                // that clamp would truncate it. Ordinary window edges retain
                // the original below-edge band. Never relax the screen clamp.
                var lastReachableY = monitor.Bounds.Bottom - sole;
                entryStartY = Math.Min(targetY, lastReachableY - MaximumGripDistance);
                // GetMovementArea reconstructs this same bound via rectangle
                // height. Tilt can leave the clamped Y a few ulps past it.
                // Do not turn that rounding into a one-frame cue/release miss.
                entryTolerance = GeometryRoundoff;
            }
            var entryDistance = displayed.Y - entryStartY;
            if (!Finite(entryDistance) || entryDistance < -entryTolerance || entryDistance > MaximumGripDistance + entryTolerance ||
                !HasHeadroom(targetY, contact, monitor))
            {
                continue;
            }

            var eligible = new EntryCandidate(candidate, monitor, Math.Abs(worldGrip - candidate.Surface.Top));
            if (selected is null || ComesBefore(eligible, selected.Value))
            {
                selected = eligible;
            }
        }

        if (selected is not { } winner)
        {
            return null;
        }

        var relativeX = displayed.X - winner.Perch.Surface.OwnerOrigin.X;
        if (!Finite(relativeX))
        {
            return null;
        }
        return winner;
    }

    public EdgePerchPose Advance(
        TimeSpan delta,
        PointD displayed,
        PerchContact contact,
        IReadOnlyList<PerchSurface> surfaces,
        IReadOnlyList<DesktopMonitor> monitors,
        bool sceneReliable)
    {
        if (Current.Phase == EdgePerchPhase.None || !sceneReliable)
        {
            return Current;
        }

        if (!Valid(displayed) || !Valid(contact) || surfaces is null || monitors is null ||
            owner is null || monitorId is null)
        {
            return Detach(displayed);
        }

        var monitor = monitors.FirstOrDefault(item => item.Id == monitorId.Value);
        if (monitor is null || !Valid(monitor.Bounds))
        {
            return Detach(displayed);
        }

        RetainedCandidate? retained = null;
        foreach (var candidate in surfaces)
        {
            var surface = candidate.Surface;
            if (surface.Key != owner.Value || surface.MonitorId != monitorId.Value ||
                !ValidForMonitor(surface, monitor))
            {
                continue;
            }

            var targetX = surface.OwnerOrigin.X + ownerRelativeX;
            var targetY = surface.Top - contact.GripY;
            var worldLeft = targetX + contact.Left;
            var worldRight = targetX + contact.Right;
            if (!Finite(targetX) || !Finite(targetY) || !Finite(worldLeft) || !Finite(worldRight) ||
                !Contains(surface, monitor, worldLeft, worldRight) ||
                !HasHeadroom(targetY, contact, monitor))
            {
                continue;
            }

            var eligible = new RetainedCandidate(candidate, new(targetX, targetY));
            if (retained is null || RetainedComesBefore(eligible, retained.Value))
            {
                retained = eligible;
            }
        }

        if (retained is not { } current)
        {
            return Detach(displayed);
        }

        if (Current.Phase == EdgePerchPhase.Entering)
        {
            elapsedSeconds = Math.Min(EntrySeconds,
                elapsedSeconds + Math.Max(0, delta.TotalSeconds));
            var progress = elapsedSeconds / EntrySeconds;
            var blend = Smoothstep(progress);
            var position = new PointD(current.Target.X,
                entryY + ((current.Target.Y - entryY) * blend));
            var phase = progress >= 1 ? EdgePerchPhase.Attached : EdgePerchPhase.Entering;
            return Current = new(phase, position, owner, monitorId);
        }

        return Current = new(EdgePerchPhase.Attached, current.Target, owner, monitorId);
    }

    public void Release(PointD displayed) => Detach(displayed);

    private EdgePerchPose Detach(PointD displayed)
    {
        owner = null;
        monitorId = null;
        ownerRelativeX = entryY = elapsedSeconds = 0;
        return Current = new(EdgePerchPhase.None,
            Valid(displayed) ? displayed : Current.Position, null, null);
    }

    private static bool ComesBefore(EntryCandidate left, EntryCandidate right)
    {
        var comparison = left.Distance.CompareTo(right.Distance);
        if (comparison != 0) return comparison < 0;
        comparison = left.Perch.ZOrder.CompareTo(right.Perch.ZOrder);
        if (comparison != 0) return comparison < 0;
        comparison = left.Perch.Surface.Key.Handle.CompareTo(right.Perch.Surface.Key.Handle);
        if (comparison != 0) return comparison < 0;
        comparison = left.Perch.Surface.Key.ProcessId.CompareTo(right.Perch.Surface.Key.ProcessId);
        if (comparison != 0) return comparison < 0;
        return left.Perch.Surface.Key.Generation < right.Perch.Surface.Key.Generation;
    }

    private static bool RetainedComesBefore(RetainedCandidate left, RetainedCandidate right)
    {
        var comparison = left.Perch.ZOrder.CompareTo(right.Perch.ZOrder);
        if (comparison != 0) return comparison < 0;
        comparison = left.Perch.Surface.Left.CompareTo(right.Perch.Surface.Left);
        if (comparison != 0) return comparison < 0;
        return left.Perch.Surface.Right < right.Perch.Surface.Right;
    }

    private static bool Contains(
        PlatformSurface surface,
        DesktopMonitor monitor,
        double worldLeft,
        double worldRight) =>
        worldLeft >= surface.Left && worldRight <= surface.Right &&
        worldLeft >= monitor.Bounds.X && worldRight <= monitor.Bounds.Right;

    private static bool HasHeadroom(double targetY, PerchContact contact, DesktopMonitor monitor)
    {
        var visibleTop = targetY + contact.VisibleTop;
        return Finite(visibleTop) && visibleTop >= monitor.Bounds.Y;
    }

    private static bool ValidForMonitor(PlatformSurface surface, DesktopMonitor monitor) =>
        Valid(surface) && surface.Kind != PlatformKind.Floor &&
        surface.Top >= monitor.Bounds.Y && surface.Top < monitor.Bounds.Bottom &&
        Math.Max(surface.Left, monitor.Bounds.X) < Math.Min(surface.Right, monitor.Bounds.Right);

    private static bool Valid(PointD point) => Finite(point.X) && Finite(point.Y);

    private static bool Valid(PerchContact contact) =>
        Finite(contact.Left) && Finite(contact.Right) &&
        Finite(contact.GripY) && Finite(contact.VisibleTop) &&
        contact.Left <= contact.Right && contact.VisibleTop <= contact.GripY &&
        (contact.HeldSoleY is not { } sole || Finite(sole) && sole >= contact.VisibleTop);

    private static bool Valid(PlatformSurface surface) =>
        Finite(surface.Left) && Finite(surface.Right) && Finite(surface.Top) &&
        Valid(surface.OwnerOrigin) && surface.Left < surface.Right;

    private static bool Valid(RectD bounds) =>
        Finite(bounds.X) && Finite(bounds.Y) && Finite(bounds.Width) && Finite(bounds.Height) &&
        bounds.Width > 0 && bounds.Height > 0 && Finite(bounds.Right) && Finite(bounds.Bottom);

    private static double Smoothstep(double value)
    {
        value = Math.Clamp(value, 0, 1);
        return value * value * (3 - (2 * value));
    }

    private static bool Finite(double value) => double.IsFinite(value);

    private readonly record struct EntryCandidate(
        PerchSurface Perch,
        DesktopMonitor Monitor,
        double Distance);

    private readonly record struct RetainedCandidate(PerchSurface Perch, PointD Target);
}
