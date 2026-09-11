using Dororong.Core.Geometry;

namespace Dororong.Core.Behavior;

public readonly record struct PetInput(
    TimeSpan Delta,
    RectD WorkArea,
    SizeD PetSize,
    PointerSample Pointer,
    bool PrimaryButtonDown,
    bool LocalInteractionActive,
    PointD? BodyPressPosition,
    SizeD DragThreshold,
    PointD? LocalInteractionPosition = null,
    bool DistanceDrivenBodyDrag = false,
    bool SuspendAutonomousMotion = false,
    bool SurfaceBoundMotion = false,
    bool SuppressPointerReactions = false,
    bool TrackPointerFacing = false);
