using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.Core.Tests.Behavior;

public sealed class HeadPullDistanceTests
{
    [Theory]
    [InlineData(3, 0, 0, false)]
    [InlineData(4, 0, 0, true)]
    [InlineData(23, 0, 0.25, true)]
    [InlineData(42, 0, 0.5, true)]
    [InlineData(61, 0, 0.75, true)]
    [InlineData(80, 0, 1, true)]
    [InlineData(48, 64, 1, true)]
    [InlineData(300, 400, 1, true)]
    public void Radial_extension_uses_the_OS_deadzone_and_clamps_overshoot(double x, double y, double strength, bool outside)
    {
        Assert.True(HeadPullDistance.TryMeasure(new(0, 0), new(x, y), new(4, 4), out var pull));
        Assert.Equal(strength, pull.Strength, 10);
        Assert.Equal(outside, pull.OutsideDeadzone);
    }

    [Fact]
    public void Non_square_OS_deadzone_is_preserved_and_extreme_inputs_stay_finite()
    {
        Assert.True(HeadPullDistance.TryMeasure(default, new(7.9, 3.9), new(8, 4), out var inside));
        Assert.False(inside.OutsideDeadzone);
        Assert.True(HeadPullDistance.TryMeasure(default, new(0, 42), new(8, 4), out var vertical));
        Assert.Equal(0.5, vertical.Strength);
        Assert.True(HeadPullDistance.TryMeasure(default, new(double.MaxValue, 0), new(4, 4), out var extreme));
        Assert.Equal(1, extreme.Strength);
        Assert.Equal(new PointD(80, 0), extreme.Extension);
        Assert.False(HeadPullDistance.TryMeasure(default, new(double.MaxValue, double.MaxValue), new(4, 4), out _));
        Assert.False(HeadPullDistance.TryMeasure(default, new(double.NaN, 0), new(4, 4), out _));
    }
}
