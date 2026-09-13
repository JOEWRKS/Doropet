using System.Windows.Media.Imaging;
using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public class PerchOutlineCleanupTests
{
    [Theory]
    [InlineData(28, 59)]
    [InlineData(50, 62)]
    [InlineData(50, 63)]
    [InlineData(47, 64)]
    [InlineData(38, 60)]
    [InlineData(38, 61)]
    public void Lower_chin_and_hair_tip_use_the_ordinary_authored_contour(int x, int y) => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var reference = PerchExpressionTests.Pixels(new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png")));
        var actual = PerchExpressionTests.Pixels(PerchExpressionFrames.Open);
        var donor = ((y + 6) * 96 + x - 8) * 4;
        Assert.Equal(255, reference[donor + 3]);
        Assert.Equal(reference.AsSpan(donor, 4).ToArray(), actual.AsSpan((y * 100 + x) * 4, 4).ToArray());
    });

    [Fact]
    public void Nearly_opaque_white_body_is_not_mistaken_for_faint_exterior_matte() => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var pixels = PerchExpressionTests.Pixels(PerchExpressionFrames.Open);
        Assert.Equal(new byte[] { 251, 251, 251, 253 }, pixels.AsSpan((63 * 100 + 60) * 4, 4).ToArray());
    });

    [Theory]
    [InlineData(51, 59)] [InlineData(51, 60)] [InlineData(50, 61)]
    [InlineData(51, 61)] [InlineData(50, 62)] [InlineData(50, 63)]
    public void Body_under_the_hair_tip_has_no_accidental_transparent_hole(int x, int y) => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var pixels = PerchExpressionTests.Pixels(PerchExpressionFrames.Open);
        var at = (y * 100 + x) * 4;
        Assert.Equal(255, pixels[at + 3]);
        // Some formerly missing texels belong to the hair, not white chest.
        // Its authored colour is checked separately; neither may remain a hole.
    });

    [Theory]
    [InlineData(59, 64)] [InlineData(60, 64)] [InlineData(58, 65)]
    public void Exterior_white_matte_does_not_make_a_second_outline(int x, int y) => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var pixels = PerchExpressionTests.Pixels(PerchExpressionFrames.Open);
        Assert.Equal(0, pixels[(y * 100 + x) * 4 + 3]);
    });

    [Fact]
    public void Cleanup_keeps_face_rose_and_opaque_ink_outside_head_and_ribbon_patches_exact() => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var source = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-edge-perch-09.png"));
        var before = PerchExpressionTests.Pixels(source);
        var after = PerchExpressionTests.Pixels(PerchExpressionFrames.Open);
        Assert.Equal(before.Length, after.Length);
        for (var y = 0; y < 100; y++) for (var x = 0; x < 100; x++)
        {
            var at = (y * 100 + x) * 4;
            if (x is >= 28 and <= 60 && y is >= 52 and <= 65) continue;
            if (x is >= 68 and <= 78 && y is >= 30 and <= 55) continue;
            if (y < 57 || before[at + 3] == 255)
                Assert.Equal(before.AsSpan(at, 4).ToArray(), after.AsSpan(at, 4).ToArray());
        }
    });
}
