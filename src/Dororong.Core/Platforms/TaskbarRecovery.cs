using Dororong.Core.Geometry;

namespace Dororong.Core.Platforms;

public static class TaskbarRecovery
{
    private const double Tolerance = .001;
    private const double MinimumOverlap = 2;

    public static PlatformSurface? FindPenetration(
        PointD displayed,
        FootContact contact,
        IReadOnlyList<PlatformSurface> surfaces,
        bool directOwnsPosition,
        bool explicitlyPerched)
    {
        if (directOwnsPosition || explicitlyPerched || !Valid(displayed) || !Valid(contact))
            return null;

        var footLeft = displayed.X + contact.Left;
        var footRight = displayed.X + contact.Right;
        var sole = displayed.Y + contact.SoleY;
        if (!double.IsFinite(footLeft) || !double.IsFinite(footRight) || !double.IsFinite(sole))
            return null;

        return surfaces
            .Where(surface => Penetrates(surface, footLeft, footRight, sole))
            .OrderBy(surface => surface.Top)
            .ThenBy(surface => surface.Key.Handle)
            .ThenBy(surface => surface.Key.ProcessId)
            .ThenBy(surface => surface.Key.Generation)
            .ThenBy(surface => surface.MonitorId)
            .ThenBy(surface => surface.Left)
            .ThenBy(surface => surface.Right)
            .ThenBy(surface => surface.Bottom)
            .Cast<PlatformSurface?>()
            .FirstOrDefault();
    }

    private static bool Penetrates(PlatformSurface surface, double footLeft, double footRight, double sole) =>
        surface.Kind == PlatformKind.Taskbar &&
        Valid(surface) &&
        surface.Bottom is { } bottom &&
        bottom > surface.Top &&
        sole > surface.Top + Tolerance &&
        sole <= bottom + Tolerance &&
        Math.Min(surface.Right, footRight) - Math.Max(surface.Left, footLeft) >= MinimumOverlap;

    private static bool Valid(PointD point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y);

    private static bool Valid(FootContact contact) =>
        double.IsFinite(contact.Left) &&
        double.IsFinite(contact.Right) &&
        double.IsFinite(contact.SoleY) &&
        double.IsFinite(contact.VisibleTop) &&
        contact.Right >= contact.Left;

    private static bool Valid(PlatformSurface surface) =>
        double.IsFinite(surface.Left) &&
        double.IsFinite(surface.Right) &&
        double.IsFinite(surface.Top) &&
        Valid(surface.OwnerOrigin) &&
        surface.Bottom is { } bottom &&
        double.IsFinite(bottom) &&
        surface.Right > surface.Left;
}
