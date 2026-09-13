using Dororong.Core.Geometry;

namespace Dororong.Core.Platforms;

public enum PlatformKind
{
    Window,
    Taskbar,
    Floor
}

public readonly record struct SurfaceKey(long Handle, uint ProcessId, long Generation);

public sealed record DesktopMonitor(long Id, RectD Bounds);

public sealed record DesktopWindow(
    SurfaceKey Key,
    RectD Bounds,
    int ZOrder,
    bool Visible,
    bool Minimized,
    bool Cloaked,
    bool Excluded,
    bool CanSupport,
    bool CanOcclude,
    bool Taskbar,
    bool HorizontalTaskbar)
{
    // Menus retain ordinary occlusion; only their own shell process's taskbar
    // treats them as transient UI rather than a removed supporting edge.
    public bool IsTransientMenu { get; init; }
}

public sealed record DesktopScene(
    long Revision,
    TimeSpan CapturedAt,
    IReadOnlyList<DesktopMonitor> Monitors,
    IReadOnlyList<DesktopWindow> Windows);

public readonly record struct PlatformSurface(
    SurfaceKey Key,
    long MonitorId,
    PlatformKind Kind,
    double Left,
    double Right,
    double Top,
    PointD OwnerOrigin)
{
    /// <summary>Taskbar owner bottom clipped to its monitor; null means unknown.</summary>
    public double? Bottom { get; init; }
}

public readonly record struct FootContact(double Left, double Right, double SoleY, double VisibleTop);
