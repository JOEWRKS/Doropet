using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Tests.Interaction;

namespace Dororong.App.Tests.Controls;

public sealed class BodyFlowSamplingTests
{
    [Theory]
    [InlineData(BodyRegion.Rump)]
    [InlineData(BodyRegion.Belly)]
    public void Flow_transports_dark_and_light_strokes_without_a_dark_dilation_bias(object part)
    {
        // Complementary opaque stripes must remain complementary under the same
        // deformation. Selecting the darkest neighboring texel thickens both inks.
        var source = new byte[96 * 96 * 4];
        var inverse = new byte[source.Length];
        for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++)
        {
            var i = (y * 96 + x) * 4;
            for (var c = 0; c < 3; c++)
            {
                source[i + c] = (byte)(x % 2 * 255);
                inverse[i + c] = (byte)(255 - source[i + c]);
            }
            source[i + 3] = inverse[i + 3] = 255;
        }
        var a = BodyPullRenderer.Render(source, (BodyRegion)part, new(16, 0), new(70, 61));
        var b = BodyPullRenderer.Render(inverse, (BodyRegion)part, new(16, 0), new(70, 61));
        for (var y = 68; y < 80; y++) for (var x = 65; x < 85; x++)
        {
            var p = BodyPullRendererTests.Pixel(a, x, y);
            var q = BodyPullRendererTests.Pixel(b, x, y);
            Assert.Equal(255, p[3]); Assert.Equal(255, q[3]);
            Assert.InRange(p[0] + q[0], 254, 256);
        }
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(12, -12)]
    [InlineData(0, -16)]
    public void Rump_contour_does_not_switch_whole_texels_for_a_subpixel_pointer_change(double x, double y)
    {
        var source = BodyPullTests.Source();
        var a = BodyPullRenderer.Render(source, BodyRegion.Rump, new(x, y), new(70, 61));
        var b = BodyPullRenderer.Render(source, BodyRegion.Rump, new(x + .01, y), new(70, 61));
        Assert.InRange(a.Zip(b, (u, v) => Math.Abs(u - v)).Max(), 0, 4);
    }
}
