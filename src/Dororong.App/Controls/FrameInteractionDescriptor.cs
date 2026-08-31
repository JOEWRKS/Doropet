using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

internal sealed class FrameInteractionDescriptor
{
    private const double FrameMaximumCoordinate = 95;

    private readonly RectD _leftCheek;
    private readonly RectD _rightCheek;

    private FrameInteractionDescriptor(RectD leftCheek, RectD rightCheek)
    {
        _leftCheek = leftCheek;
        _rightCheek = rightCheek;
    }

    internal static FrameInteractionDescriptor Canonical { get; } = new(
        leftCheek: new RectD(48, 48, 18, 17),
        rightCheek: new RectD(22, 48, 18, 17));

    internal DirectInteractionTarget Classify(
        PointD sourcePoint,
        FacingDirection facing,
        bool opaque)
    {
        if (!opaque)
        {
            return DirectInteractionTarget.None;
        }

        var canonicalPoint = facing == FacingDirection.Left
            ? new PointD(FrameMaximumCoordinate - sourcePoint.X, sourcePoint.Y)
            : sourcePoint;
        if (Contains(_leftCheek, canonicalPoint))
        {
            return DirectInteractionTarget.LeftCheek;
        }

        return Contains(_rightCheek, canonicalPoint)
            ? DirectInteractionTarget.RightCheek
            : DirectInteractionTarget.Body;
    }

    internal double GetScreenOutwardSign(
        DirectInteractionTarget target,
        FacingDirection facing)
    {
        var canonicalSign = target switch
        {
            DirectInteractionTarget.LeftCheek => 1,
            DirectInteractionTarget.RightCheek => -1,
            _ => 0
        };
        return facing == FacingDirection.Left
            ? -canonicalSign
            : canonicalSign;
    }

    private static bool Contains(RectD rectangle, PointD point) =>
        point.X >= rectangle.X &&
        point.X < rectangle.Right &&
        point.Y >= rectangle.Y &&
        point.Y < rectangle.Bottom;
}
