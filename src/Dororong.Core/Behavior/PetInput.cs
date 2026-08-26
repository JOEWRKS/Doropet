using Dororong.Core.Geometry;

namespace Dororong.Core.Behavior;

public readonly record struct PetInput(
    TimeSpan Delta,
    RectD WorkArea,
    SizeD PetSize,
    PointerSample Pointer,
    bool PrimaryButtonDown,
    PointD? BodyPressPosition,
    SizeD DragThreshold);
