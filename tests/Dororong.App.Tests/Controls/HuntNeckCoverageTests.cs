using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public sealed class HuntNeckCoverageTests
{
    [Fact]
    public void Downward_gaze_keeps_the_exposed_inner_neck_white_and_opaque() => EdgePerchPresentationTests.Sta(() =>
    {
        var image = new HuntRenderer().Render(0, new(0, .65, -Math.PI / 9));
        var pixels = new byte[96 * 96 * 4];
        image.CopyPixels(pixels, 384, 0);
        // Independently measured centers of the diagnosed hole, between head and body.
        foreach (var (x, y) in new[] { (54,56), (54,57), (54,58), (54,59), (54,60), (54,61),
            (53,62), (53,63), (52,64), (51,65), (50,66) })
        {
            var index = (y * 96 + x) * 4;
            Assert.True(pixels[index + 3] >= 250, $"Inner neck ({x},{y}) alpha was {pixels[index + 3]}.");
            for (var channel = 0; channel < 3; channel++)
                Assert.True(pixels[index + channel] >= 235, "Neck backing must be white fill, without a second jaw outline.");
        }
    });

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(28)]
    [InlineData(42)]
    [InlineData(60)]
    [InlineData(99)]
    [InlineData(123)]
    [InlineData(140)]
    [InlineData(156)]
    public void Inner_neck_stays_connected_through_roll_extremes_and_pose_phases(int frame) => EdgePerchPresentationTests.Sta(() =>
    {
        var renderer = new HuntRenderer();
        var neck = HuntPose.At(frame / 60d).Map(54, 60);
        var x = (int)Math.Round(neck.X);
        var y = (int)Math.Round(neck.Y);
        var pixels = new byte[96 * 96 * 4];
        foreach (var degrees in new[] { -20, -10, 0, 10, 20 })
        {
            renderer.Render(frame, new(0, 0, degrees * Math.PI / 180)).CopyPixels(pixels, 384, 0);
            var alpha = pixels[(y * 96 + x) * 4 + 3];
            Assert.True(alpha >= 250, $"Frame {frame}, roll {degrees}: inner neck ({x},{y}) alpha {alpha}.");
        }
    });
}
