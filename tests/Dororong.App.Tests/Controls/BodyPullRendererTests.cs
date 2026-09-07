using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Tests.Interaction;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class BodyPullRendererTests
{
    [Theory]
    [InlineData(-8, 0)]
    [InlineData(-12, 0)]
    [InlineData(-16, 0)]
    [InlineData(-12, 5)]
    public void Connected_front_paw_root_does_not_receive_a_cut_like_procedural_outline(double dx, double dy)
    {
        var source = BodyPullTests.Source();
        var output = BodyPullRenderer.Render(source, BodyRegion.FrontPaw, new(dx, dy), new(23, 77));
        // Hand-inspected interior attachment pixels, not the free outer contour.
        foreach (var point in new[] { (18, 72), (22, 73) })
        {
            var pixel = Pixel(output, point.Item1, point.Item2);
            Assert.Equal(255, pixel[3]);
            Assert.True(pixel.Take(3).All(channel => channel >= 230),
                $"A false cut line darkened the joined body at {point}: {string.Join(',', pixel)}");
        }
    }

    [Fact]
    public void Free_front_paw_boundary_still_receives_its_missing_outline()
    {
        var output = BodyPullRenderer.Render(BodyPullTests.Source(), BodyRegion.FrontPaw, new(-12, 0), new(23, 77));
        // Exposed proximal silhouette, away from torso; suppressing all paw ink fails.
        Assert.Equal(new byte[] { 80, 74, 92, 255 }, Pixel(output, 24, 74));
    }

    [Fact]
    public void Folded_distal_paw_keeps_its_overlap_outline_on_opaque_torso()
    {
        var source = BodyPullTests.Source();
        // A light distal texel moves to (34,77), in front of opaque torso.
        // It still needs overlap ink even though the combined silhouette is closed.
        source.AsSpan((79 * 96 + 19) * 4, 4).Fill(255);
        Assert.Equal(255, SourcePixel(source, 34, 77)[3]);
        var output = BodyPullRenderer.Render(source, BodyRegion.FrontPaw, new(12, -12), new(23, 77));
        Assert.Equal(new byte[] { 80, 74, 92, 255 }, Pixel(output, 34, 77));
    }

    [Theory]
    [InlineData(BodyRegion.FrontPaw, 23, 80)]
    [InlineData(BodyRegion.MiddlePaw, 43, 85)]
    [InlineData(BodyRegion.RightPaw, 66, 82)]
    public void Upward_fold_removes_the_original_distal_contour(object part, int x, int y)
    {
        var source = BodyPullTests.Source(); Assert.True(SourcePixel(source, x, y)[3] > 0);
        var output = BodyPullRenderer.Render(source, (BodyRegion)part, new(0, -16), new(x, y));
        Assert.Equal(0, Pixel(output, x, y)[3]);
    }

    [Fact]
    public void Compressive_fold_has_no_position_or_velocity_step_as_upward_pull_increases()
    {
        foreach (var region in new[] { BodyRegion.FrontPaw, BodyRegion.MiddlePaw, BodyRegion.RightPaw })
        {
            var geometry = PawGeometry.For(region);
            for (var amount = 0d; amount <= 24; amount += .25)
            {
                const double h = .0001;
                var a = geometry.Vertex(geometry.Tip, new(0, -amount - h));
                var b = geometry.Vertex(geometry.Tip, new(0, -amount));
                var c = geometry.Vertex(geometry.Tip, new(0, -amount + h));
                Assert.InRange(BodyPullSession.Length(c - a), 0, 2 * h + .00000001);
                Assert.InRange(BodyPullSession.Length((c - b) - (b - a)), 0, .000001);
            }
        }
    }

    [Theory]
    [InlineData(BodyRegion.FrontPaw, 20, 78, 25, 80)]
    [InlineData(BodyRegion.MiddlePaw, 41, 83, 46, 85)]
    [InlineData(BodyRegion.RightPaw, 63, 80, 68, 82)]
    public void Upward_fold_transports_visible_distal_material_without_leaving_the_original_paw(object part, int left, int top, int right, int bottom)
    {
        var source = BodyPullTests.Source(); var original = (byte[])source.Clone(); var marked = 0;
        for (var y = top; y <= bottom; y++) for (var x = left; x <= right; x++)
            {
                var i = (y * 96 + x) * 4; if (source[i + 3] != 255) continue;
                source[i] = 17; source[i + 1] = 113; source[i + 2] = 197; marked++;
            }
        Assert.True(marked >= 8, "The hand-checked distal fixture must contain material.");
        var output = BodyPullRenderer.Render(source, (BodyRegion)part, new(0, -16), new(left, top));
        var transported = 0; var leftBehind = 0;
        for (var y = -32; y < 128; y++) for (var x = -32; x < 128; x++)
            {
                var p = Pixel(output, x, y);
                if (p[0] == 17 && p[1] == 113 && p[2] == 197 && p[3] == 255)
                { if (y < top) transported++; else leftBehind++; }
                if (x >= 0 && x < 96 && y >= 0 && y < 96 && BodyRegionMap.IsProtected(x, y)) Assert.Equal(SourcePixel(original, x, y), p);
            }
        Assert.True(transported >= 8, $"{part}: only {transported} distal texels remain visible above the original paw.");
        Assert.Equal(0, leftBehind);
    }

    [Fact]
    public void Broad_hair_attachment_does_not_open_holes_in_opaque_belly_material()
    {
        var source = BodyPullTests.Source();
        foreach (var region in new[] { BodyRegion.Belly, BodyRegion.Rump })
        {
            var output = BodyPullRenderer.Render(source, region, new(12, 12), region == BodyRegion.Belly ? new(53, 70) : new(70, 61));
            foreach (var point in new PointD[] { new(57, 63), new(56, 64), new(55, 65), new(54, 66), new(53, 67), new(50, 71), new(51, 72) })
            {
                Assert.Equal(255, SourcePixel(source, (int)point.X, (int)point.Y)[3]);
                Assert.Equal(255, Pixel(output, (int)point.X, (int)point.Y)[3]);
            }
        }
    }

    [Theory]
    [InlineData(BodyRegion.FrontPaw, 0, -16, 19, 71)]
    [InlineData(BodyRegion.MiddlePaw, 0, -16, 40, 73)]
    public void Folded_distal_antialias_keeps_the_underlying_opaque_torso_closed(object part, double dx, double dy, int x, int y)
    {
        var source = BodyPullTests.Source();
        var output = BodyPullRenderer.Render(source, (BodyRegion)part, new(dx, dy), new(x, y));
        Assert.Equal(255, SourcePixel(source, x, y)[3]);
        Assert.Equal(255, Pixel(output, x, y)[3]);
    }

    [Theory]
    [InlineData(BodyRegion.FrontPaw, 18, 75, 27, 76, 22, 71)]
    [InlineData(BodyRegion.MiddlePaw, 34, 81, 47, 82, 40, 73)]
    [InlineData(BodyRegion.RightPaw, 61, 78, 70, 79, 65, 71)]
    public void Folded_proximal_material_is_hidden_at_the_torso_depth_gate(object part, int left, int top, int right, int bottom, int gateX, int gateY)
    {
        var source = BodyPullTests.Source();
        for (var y = top; y <= bottom; y++) for (var x = left; x <= right; x++)
            {
                var i = (y * 96 + x) * 4; if (source[i + 3] != 255) continue;
                source[i] = 17; source[i + 1] = 113; source[i + 2] = 197;
            }
        var output = BodyPullRenderer.Render(source, (BodyRegion)part, new(0, -16), new(left, top));
        for (var y = gateY; y < gateY + 2; y++) for (var x = gateX; x < gateX + 2; x++)
            {
                var pixel = Pixel(output, x, y);
                Assert.False(pixel[0] == 17 && pixel[1] == 113 && pixel[2] == 197, $"{part} exposed proximal texel at {x},{y}");
            }
    }

    [Theory]
    [InlineData(BodyRegion.FrontPaw, 23, 77)]
    [InlineData(BodyRegion.MiddlePaw, 42, 82)]
    [InlineData(BodyRegion.RightPaw, 66, 80)]
    [InlineData(BodyRegion.Belly, 53, 70)]
    [InlineData(BodyRegion.Rump, 70, 61)]
    public void Each_region_moves_in_eight_directions_with_exact_protected_foreground_and_transparent_padding(object part, int ax, int ay)
    {
        var region = (BodyRegion)part; var source = BodyPullTests.Source(); var original = (byte[])source.Clone();
        var idle = BodyPullRenderer.Render(source, region, default, new(ax, ay));
        for (var y = -32; y < 128; y++) for (var x = -32; x < 128; x++)
                Assert.Equal(SourcePixel(source, x, y), Pixel(idle, x, y));
        foreach (var direction in new PointD[] { new(-12, -12), new(0, -16), new(12, -12), new(-16, 0), new(16, 0), new(-12, 12), new(0, 16), new(12, 12) })
        {
            var output = BodyPullRenderer.Render(source, region, direction, new(ax, ay)); var changes = 0;
            for (var y = -32; y < 128; y++) for (var x = -32; x < 128; x++)
                {
                    var p = Pixel(output, x, y);
                    if (x >= 0 && x < 96 && y >= 0 && y < 96 && BodyRegionMap.IsProtected(x, y)) Assert.Equal(SourcePixel(source, x, y), p);
                    if (!p.SequenceEqual(Pixel(idle, x, y))) changes++;
                    Assert.True(p[0] <= p[3] && p[1] <= p[3] && p[2] <= p[3], "Premultiplied alpha invariant");
                    if (x is -32 or 127 || y is -32 or 127) Assert.Equal(0, p[3]);
                }
            Assert.True(changes > 15, $"{region} {direction}: {changes} changed pixels");
        }
        Assert.Equal(original, source);
    }

    [Theory]
    [InlineData(BodyRegion.FrontPaw, 23, 80)]
    [InlineData(BodyRegion.MiddlePaw, 43, 85)]
    [InlineData(BodyRegion.RightPaw, 66, 82)]
    public void Moved_distal_paw_does_not_leave_its_old_contour_underneath(object part, int x, int y)
    {
        var source = BodyPullTests.Source();
        Assert.True(SourcePixel(source, x, y)[3] > 0);
        var output = BodyPullRenderer.Render(source, (BodyRegion)part, new(16, 0), new(x, y));
        Assert.Equal(0, Pixel(output, x, y)[3]);
    }

    [Fact]
    public void Near_zero_departure_does_not_pop_and_fold_angle_wrap_is_continuous()
    {
        var source = BodyPullTests.Source();
        foreach (var region in new[] { BodyRegion.FrontPaw, BodyRegion.MiddlePaw, BodyRegion.RightPaw, BodyRegion.Belly, BodyRegion.Rump })
        {
            var a = BodyPullRenderer.Render(source, region, default, new(53, 70));
            var b = BodyPullRenderer.Render(source, region, new(.0001, 0), new(53, 70));
            Assert.Equal(a, b);
            var c = BodyPullRenderer.Render(source, region, new(.1, 0), new(53, 70));
            Assert.InRange(a.Zip(c, (u, v) => Math.Abs(u - v)).Max(), 0, 8);
        }
        var left = BodyPullRenderer.Render(source, BodyRegion.FrontPaw, new(-20, -8.00001), new(23, 77));
        var right = BodyPullRenderer.Render(source, BodyRegion.FrontPaw, new(-20, -7.99999), new(23, 77));
        Assert.True(left.Zip(right, (a, b) => a != b ? 1 : 0).Sum() < 48);
    }

    [Fact]
    public void No_head_texture_is_copied_into_body_or_padding()
    {
        var source = new byte[96 * 96 * 4];
        for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++) if (BodyRegionMap.IsProtected(x, y))
                { var i = (y * 96 + x) * 4; source[i] = 150; source[i + 1] = 30; source[i + 2] = 180; source[i + 3] = 255; }
        foreach (var region in new[] { BodyRegion.FrontPaw, BodyRegion.MiddlePaw, BodyRegion.RightPaw, BodyRegion.Belly, BodyRegion.Rump })
        {
            var output = BodyPullRenderer.Render(source, region, new(12, 12), new(53, 70));
            for (var y = -32; y < 128; y++) for (var x = -32; x < 128; x++)
                    if (x < 0 || x >= 96 || y < 0 || y >= 96 || !BodyRegionMap.IsProtected(x, y)) Assert.Equal(0, Pixel(output, x, y)[3]);
        }
    }

    internal static byte[] Pixel(byte[] pixels, int x, int y) => pixels.AsSpan(((y + BodyPullRenderer.Pad) * BodyPullRenderer.Size + x + BodyPullRenderer.Pad) * 4, 4).ToArray();
    private static byte[] SourcePixel(byte[] pixels, int x, int y) => x >= 0 && x < 96 && y >= 0 && y < 96 ? pixels.AsSpan((y * 96 + x) * 4, 4).ToArray() : new byte[4];
}
