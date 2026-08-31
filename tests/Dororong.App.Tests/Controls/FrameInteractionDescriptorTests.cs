using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class FrameInteractionDescriptorTests
{
    [Fact]
    public void Canonical_descriptor_classifies_opaque_cheeks_body_and_transparency()
    {
        var descriptor = FrameInteractionDescriptor.Canonical;

        Assert.Equal(
            DirectInteractionTarget.RightCheek,
            descriptor.Classify(new PointD(30, 56), FacingDirection.Right, opaque: true));
        Assert.Equal(
            DirectInteractionTarget.LeftCheek,
            descriptor.Classify(new PointD(56, 56), FacingDirection.Right, opaque: true));
        Assert.Equal(
            DirectInteractionTarget.None,
            descriptor.Classify(new PointD(56, 56), FacingDirection.Right, opaque: false));
        Assert.Equal(
            DirectInteractionTarget.RightCheek,
            descriptor.Classify(new PointD(56, 56), FacingDirection.Left, opaque: true));
        Assert.Equal(
            DirectInteractionTarget.Body,
            descriptor.Classify(new PointD(45, 75), FacingDirection.Right, opaque: true));
    }

    [Theory]
    [InlineData(3, FacingDirection.Right, -1)]
    [InlineData(2, FacingDirection.Right, 1)]
    [InlineData(3, FacingDirection.Left, 1)]
    [InlineData(2, FacingDirection.Left, -1)]
    public void Outward_sign_tracks_the_visible_side_without_changing_anatomical_identity(
        int targetValue,
        FacingDirection facing,
        double expected)
    {
        var target = (DirectInteractionTarget)targetValue;

        Assert.Equal(expected, FrameInteractionDescriptor.Canonical.GetScreenOutwardSign(target, facing));
    }
}
