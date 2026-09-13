using Dororong.Core.Geometry;

namespace Dororong.Core.Platforms;

public static class PlatformGeometry
{
    private const double MinimumContact = 2d;

    public static IReadOnlyList<PlatformSurface> Build(DesktopScene scene, double bodyHeight)
    {
        var result = new List<PlatformSurface>();
        if (!IsFinite(bodyHeight) || bodyHeight < 0)
        {
            return result;
        }

        foreach (var monitor in scene.Monitors)
        {
            if (!IsValidRectangle(monitor.Bounds))
            {
                continue;
            }

            foreach (var candidate in scene.Windows.OrderBy(window => window.ZOrder))
            {
                if (!CanProvidePlatform(candidate) || !IsValidRectangle(candidate.Bounds))
                {
                    continue;
                }

                var top = candidate.Bounds.Y;
                if (top < monitor.Bounds.Y + bodyHeight ||
                    top < monitor.Bounds.Y || top >= monitor.Bounds.Bottom)
                {
                    continue;
                }

                var left = Math.Max(candidate.Bounds.X, monitor.Bounds.X);
                var right = Math.Min(candidate.Bounds.Right, monitor.Bounds.Right);
                if (!(right > left))
                {
                    continue;
                }

                var remaining = new List<(double Left, double Right)> { (left, right) };
                foreach (var occluder in scene.Windows)
                {
                    if (occluder.ZOrder >= candidate.ZOrder || !CanOcclude(occluder) ||
                        (candidate.Taskbar && occluder.IsTransientMenu &&
                         candidate.Key.ProcessId != 0 && candidate.Key.ProcessId == occluder.Key.ProcessId) ||
                        !IsValidRectangle(occluder.Bounds) ||
                        occluder.Bounds.Y > top || occluder.Bounds.Bottom <= top)
                    {
                        continue;
                    }

                    var occluderLeft = Math.Max(occluder.Bounds.X, monitor.Bounds.X);
                    var occluderRight = Math.Min(occluder.Bounds.Right, monitor.Bounds.Right);
                    if (!(occluderRight > occluderLeft))
                    {
                        continue;
                    }

                    var next = new List<(double Left, double Right)>();
                    foreach (var interval in remaining)
                    {
                        if (occluderRight <= interval.Left || occluderLeft >= interval.Right)
                        {
                            next.Add(interval);
                            continue;
                        }

                        if (occluderLeft > interval.Left)
                        {
                            next.Add((interval.Left, Math.Min(interval.Right, occluderLeft)));
                        }

                        if (occluderRight < interval.Right)
                        {
                            next.Add((Math.Max(interval.Left, occluderRight), interval.Right));
                        }
                    }

                    remaining = next;
                }

                var kind = candidate.Taskbar ? PlatformKind.Taskbar : PlatformKind.Window;
                foreach (var interval in remaining)
                {
                    if (interval.Right > interval.Left)
                    {
                        result.Add(new(candidate.Key, monitor.Id, kind, interval.Left, interval.Right,
                            top, new(candidate.Bounds.X, candidate.Bounds.Y))
                        {
                            Bottom = kind == PlatformKind.Taskbar
                                ? Math.Min(candidate.Bounds.Bottom, monitor.Bounds.Bottom) : null
                        });
                    }
                }
            }

            if (monitor.Bounds.Height >= bodyHeight)
            {
                // HWNDs are nonzero. This stable synthetic identity is unique per monitor.
                var floorKey = new SurfaceKey(0, 0, monitor.Id);
                result.Add(new(floorKey, monitor.Id, PlatformKind.Floor,
                    monitor.Bounds.X, monitor.Bounds.Right, monitor.Bounds.Bottom,
                    new(monitor.Bounds.X, monitor.Bounds.Bottom)));
            }
        }

        return result;
    }

    public static PlatformSurface? FirstCrossing(
        IReadOnlyList<PlatformSurface> surfaces,
        FootContact before,
        FootContact after)
    {
        if (!IsValidContact(before) || !IsValidContact(after))
        {
            return null;
        }

        var deltaY = after.SoleY - before.SoleY;
        if (!(deltaY > 0) || !IsFinite(deltaY))
        {
            return null;
        }

        PlatformSurface? first = null;
        var firstT = double.PositiveInfinity;
        foreach (var surface in surfaces)
        {
            if (!IsValidSurface(surface) ||
                surface.Top < before.SoleY || surface.Top > after.SoleY)
            {
                continue;
            }

            var t = (surface.Top - before.SoleY) / deltaY;
            var left = before.Left + ((after.Left - before.Left) * t);
            var right = before.Right + ((after.Right - before.Right) * t);
            var overlap = Math.Min(right, surface.Right) - Math.Max(left, surface.Left);
            if (overlap >= MinimumContact && t < firstT)
            {
                first = surface;
                firstT = t;
            }
        }

        return first;
    }

    private static bool CanProvidePlatform(DesktopWindow window) =>
        IsPresent(window) && window.CanSupport && (!window.Taskbar || window.HorizontalTaskbar);

    private static bool CanOcclude(DesktopWindow window) =>
        IsPresent(window) && window.CanOcclude;

    private static bool IsPresent(DesktopWindow window) =>
        window.Visible && !window.Minimized && !window.Cloaked && !window.Excluded;

    private static bool IsValidRectangle(RectD rectangle) =>
        IsFinite(rectangle.X) && IsFinite(rectangle.Y) &&
        IsFinite(rectangle.Width) && IsFinite(rectangle.Height) &&
        rectangle.Width > 0 && rectangle.Height > 0 &&
        IsFinite(rectangle.Right) && IsFinite(rectangle.Bottom);

    private static bool IsValidContact(FootContact contact) =>
        IsFinite(contact.Left) && IsFinite(contact.Right) &&
        IsFinite(contact.SoleY) && IsFinite(contact.VisibleTop) &&
        contact.Right >= contact.Left;

    private static bool IsValidSurface(PlatformSurface surface) =>
        IsFinite(surface.Left) && IsFinite(surface.Right) && IsFinite(surface.Top) &&
        IsFinite(surface.OwnerOrigin.X) && IsFinite(surface.OwnerOrigin.Y) &&
        surface.Right > surface.Left;

    private static bool IsFinite(double value) => double.IsFinite(value);
}
