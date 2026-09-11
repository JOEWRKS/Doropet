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

    internal static FrameInteractionDescriptor SleepCrouch { get; } = new(
        leftCheek: new RectD(37, 53, 18, 17),
        rightCheek: new RectD(14, 53, 18, 17));

    internal static FrameInteractionDescriptor SleepTuck { get; } = new(
        leftCheek: new RectD(34, 59, 17, 16),
        rightCheek: new RectD(12, 59, 18, 16));

    internal static FrameInteractionDescriptor SettledSleep { get; } = new(
        leftCheek: new RectD(33, 55, 17, 16),
        rightCheek: new RectD(12, 55, 18, 16));

    internal static FrameInteractionDescriptor Interpolate(
        FrameInteractionDescriptor from,
        FrameInteractionDescriptor to,
        double progress)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        var amount = Math.Clamp(progress, 0, 1);
        return new FrameInteractionDescriptor(
            Interpolate(from._leftCheek, to._leftCheek, amount),
            Interpolate(from._rightCheek, to._rightCheek, amount));
    }

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

    // Alternate sleep poses move the head with their cheek landmarks. Only
    // that area can wake-and-carry; the remaining opaque body is click-only.
    internal bool IsHeadOrCheek(PointD canonicalPoint) =>
        Contains(_leftCheek, canonicalPoint) || Contains(_rightCheek, canonicalPoint) ||
        (canonicalPoint.X >= Math.Max(0, _rightCheek.X - 16) &&
         canonicalPoint.X < _leftCheek.Right &&
         canonicalPoint.Y < Math.Min(_leftCheek.Y, _rightCheek.Y));

    private static bool Contains(RectD rectangle, PointD point) =>
        point.X >= rectangle.X &&
        point.X < rectangle.Right &&
        point.Y >= rectangle.Y &&
        point.Y < rectangle.Bottom;

    private static RectD Interpolate(RectD from, RectD to, double progress) =>
        new(
            from.X + ((to.X - from.X) * progress),
            from.Y + ((to.Y - from.Y) * progress),
            from.Width + ((to.Width - from.Width) * progress),
            from.Height + ((to.Height - from.Height) * progress));
}
