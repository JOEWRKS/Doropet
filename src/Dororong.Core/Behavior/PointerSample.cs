using Dororong.Core.Geometry;

namespace Dororong.Core.Behavior;

public readonly record struct PointerSample(bool IsAvailable, PointD Position)
{
    // Optional physical screen coordinates for velocity-based presentation.
    // Position remains in logical DIPs for all existing movement/hit-test rules.
    public PointD? ScreenPixelPosition { get; init; }

    public static PointerSample Unavailable => new(false, default);
}
