using Dororong.Core.Geometry;

namespace Dororong.Core.Behavior;

public readonly record struct PetSnapshot(
    PetState State,
    PointD Position,
    FacingDirection Facing,
    double Phase,
    bool IsDirectInteractionPending,
    PointD? GrabOffset);
