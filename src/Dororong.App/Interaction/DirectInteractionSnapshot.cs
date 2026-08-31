using Dororong.Core.Geometry;

namespace Dororong.App.Interaction;

internal readonly record struct DirectInteractionSnapshot(
    DirectInteractionTarget Target,
    DirectInteractionPhase Phase,
    PointD PressOrigin,
    PointD PointerPosition,
    double Strength,
    double ReleaseProgress,
    bool RequiresCapture)
{
    internal static DirectInteractionSnapshot None { get; } = new(
        DirectInteractionTarget.None, DirectInteractionPhase.None,
        default, default, 0, 1, false);

    internal bool SuppressesProximity => Phase != DirectInteractionPhase.None;
}
