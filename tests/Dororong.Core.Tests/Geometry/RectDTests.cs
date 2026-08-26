using Dororong.Core.Geometry;

namespace Dororong.Core.Tests.Geometry;

public sealed class RectDTests
{
    [Theory]
    [InlineData(-10, 20, 0, 20)]
    [InlineData(50, -5, 50, 0)]
    [InlineData(760, 550, 680, 500)]
    [InlineData(300, 220, 300, 220)]
    public void ClampTopLeft_keeps_the_entire_pet_inside_the_work_area(
        double x, double y, double expectedX, double expectedY)
    {
        var workArea = new RectD(0, 0, 800, 600);

        var actual = workArea.ClampTopLeft(new PointD(x, y), new SizeD(120, 100));

        Assert.Equal(new PointD(expectedX, expectedY), actual);
    }
}
