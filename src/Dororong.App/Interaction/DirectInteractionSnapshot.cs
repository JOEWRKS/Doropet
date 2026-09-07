using Dororong.Core.Geometry;

namespace Dororong.App.Interaction;

internal readonly record struct DirectInteractionSnapshot(
    DirectInteractionTarget Target,
    DirectInteractionPhase Phase,
    PointD PressOrigin,
    PointD PointerPosition,
    double Strength,
    double ReleaseProgress,
    bool RequiresCapture,
    BodyPullSnapshot? BodyPull)
{
    internal bool IsPartialDragSettle { get; init; }
    internal double HeadSwingDegrees { get; init; }
    internal HeadLandingSnapshot? HeadLanding { get; init; }
    internal CheekPullSnapshot? CheekPull { get; init; }

    // Keep the existing seven-argument runtime/reflection contract for legacy renders.
    internal DirectInteractionSnapshot(DirectInteractionTarget Target, DirectInteractionPhase Phase,
        PointD PressOrigin, PointD PointerPosition, double Strength, double ReleaseProgress, bool RequiresCapture)
        : this(Target, Phase, PressOrigin, PointerPosition, Strength, ReleaseProgress, RequiresCapture, null) { }

    internal static DirectInteractionSnapshot None { get; } = new(
        DirectInteractionTarget.None, DirectInteractionPhase.None,
        default, default, 0, 1, false);

    internal bool SuppressesProximity => Phase != DirectInteractionPhase.None;
}
