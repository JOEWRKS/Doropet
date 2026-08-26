using Dororong.Core.Geometry;

namespace Dororong.Core.Behavior;

public readonly record struct PointerSample(bool IsAvailable, PointD Position)
{
    public static PointerSample Unavailable => new(false, default);
}
