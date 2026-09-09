using Dororong.App.Interaction;
using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

internal static class BodyPullRenderer
{
    internal const int Size = 160, Pad = 32;
    private static readonly bool[] Protected = Enumerable.Range(0, 96 * 96).Select(i => BodyRegionMap.IsProtected(i % 96, i / 96)).ToArray();
    internal static byte[] Render(ReadOnlySpan<byte> source, BodyRegion region, PointD pull, PointD anchor)
    {
        if (source.Length != 96 * 96 * 4) throw new ArgumentException("A frozen 96x96 premultiplied source is required.", nameof(source));
        var result = new byte[Size * Size * 4];
        for (var y = 0; y < 96; y++) source.Slice(y * 96 * 4, 96 * 4).CopyTo(result.AsSpan(((y + Pad) * Size + Pad) * 4));
        var magnitude = BodyPullSession.Length(pull);
        if (region == BodyRegion.None || !double.IsFinite(magnitude) || magnitude < .00001) return result;
        var rest = (byte[])result.Clone();
        if (region is BodyRegion.Belly or BodyRegion.Rump) RenderFlow(source, result, region, pull, anchor);
        else RenderPaw(source.ToArray(), result, PawGeometry.For(region), pull);
        var t = Math.Min(1, magnitude / (region is BodyRegion.Belly or BodyRegion.Rump ? 1 : 2));
        var blend = t * t * (3 - 2 * t);
        if (blend < 1) for (var i = 0; i < result.Length; i++) result[i] = (byte)Math.Round(rest[i] + (result[i] - rest[i]) * blend);
        // Restore exact frozen foreground last, including its transparent antialias boundary.
        for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++) if (Protected[y * 96 + x])
                    source.Slice((y * 96 + x) * 4, 4).CopyTo(result.AsSpan(Offset(x, y), 4));
        return result;
    }

    private static void RenderFlow(ReadOnlySpan<byte> source, byte[] output, BodyRegion region, PointD pull, PointD anchor)
    {
        var flow = new BodyFlow(region, pull, anchor);
        var texture = ExtendBodyAtForeground(source);
        for (var y = 40; y < 108; y++) for (var x = -11; x < 108; x++)
            {
                if (x >= 0 && x < 96 && y < 96 && Protected[y * 96 + x]) continue;
                var uv = flow.Map(new(x + .5, y + .5), -1);
                // Flow coordinates address pixel centers. Interpolate premultiplied
                // color and coverage together; darkest-neighbor selection dilates
                // ink and switches whole texels during subpixel movement.
                var u = uv.X - .5; var v = uv.Y - .5;
                var ix = (int)Math.Floor(u); var iy = (int)Math.Floor(v);
                var fx = u - ix; var fy = v - iy;
                var a = Texture(texture, ix, iy); var b = Texture(texture, ix + 1, iy);
                var c = Texture(texture, ix, iy + 1); var d = Texture(texture, ix + 1, iy + 1);
                var o = Offset(x, y);
                for (var channel = 0; channel < 4; channel++)
                {
                    var top = (a.IsEmpty ? 0 : a[channel]) * (1 - fx) + (b.IsEmpty ? 0 : b[channel]) * fx;
                    var bottom = (c.IsEmpty ? 0 : c[channel]) * (1 - fx) + (d.IsEmpty ? 0 : d[channel]) * fx;
                    output[o + channel] = (byte)Math.Clamp(Math.Round(top * (1 - fy) + bottom * fy), 0, 255);
                }
            }
    }
    private static ReadOnlySpan<byte> Texture(ReadOnlySpan<byte> source, int x, int y) => x >= 0 && x < 96 && y >= 0 && y < 96 ? source.Slice((y * 96 + x) * 4, 4) : ReadOnlySpan<byte>.Empty;
    private static byte[] ExtendBodyAtForeground(ReadOnlySpan<byte> source)
    {
        var texture = source.ToArray();
        // Continuous flow and a discrete ownership edge differ by a texel. Extend only
        // existing body texture two texels under that edge, then restore the exact head.
        // No protected texel is a texture donor, including at the ribbon/rose boundary.
        for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++)
            {
                var index = y * 96 + x; if (!Protected[index]) continue;
                texture.AsSpan(index * 4, 4).Clear(); var best = double.MaxValue; var donor = -1;
                for (var dy = -2; dy <= 2; dy++) for (var dx = -2; dx <= 2; dx++)
                    {
                        var tx = x + dx; var ty = y + dy; var distance = dx * dx + dy * dy;
                        if (tx < 0 || tx >= 96 || ty < 0 || ty >= 96 || distance > 4 || distance >= best) continue;
                        var i = ty * 96 + tx; if (Protected[i] || source[i * 4 + 3] == 0) continue;
                        best = distance; donor = i;
                    }
                if (donor >= 0) source.Slice(donor * 4, 4).CopyTo(texture.AsSpan(index * 4, 4));
            }
        return texture;
    }
    // Rank as composited on white, so transparent black cannot erase a thin contour.
    private static int Darkness(ReadOnlySpan<byte> p) => p.IsEmpty ? 765 : p[0] + p[1] + p[2] + 3 * (255 - p[3]);

    private static void RenderPaw(byte[] source, byte[] output, PawGeometry geometry, PointD pull)
    {
        var mask = new bool[96 * 96]; var hasTexture = false;
        for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++)
            {
                var i = y * 96 + x;
                mask[i] = !Protected[i] && !BodyRegionMap.IsExcludedConnector(x, y) && BodyRegionMap.Inside(geometry.Mask, x + .5, y + .5);
                if (!mask[i]) continue;
                hasTexture |= source[i * 4 + 3] > 0;
                output.AsSpan(Offset(x, y), 4).Clear();
            }
        if (!hasTexture) return;
        DrawClosure(source, output, geometry, mask);
        // Restore the shaped torso surface before depth-sorted paw triangles. Only
        // proximal geometry is occluded by it; the folded distal paw stays visible.
        for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++)
                if (geometry.OccludesProximal(x + .5, y + .5) && source[(y * 96 + x) * 4 + 3] > 0)
                    source.AsSpan((y * 96 + x) * 4, 4).CopyTo(output.AsSpan(Offset(x, y), 4));
        var torso = (byte[])output.Clone();
        var coverage = new bool[Size * Size]; var exposed = new bool[Size * Size]; var distal = new bool[Size * Size];
        // Root-to-tip order makes the distal paw cover its folded proximal limb.
        for (var y = (int)geometry.Mask.Min(p => p.Y); y < geometry.Mask.Max(p => p.Y); y++)
            for (var x = (int)geometry.Mask.Min(p => p.X); x < geometry.Mask.Max(p => p.X); x++)
            {
                var a = new PointD(x, y); var b = new PointD(x + 1, y); var c = new PointD(x + 1, y + 1); var d = new PointD(x, y + 1);
                Triangle(a, b, c); Triangle(a, c, d);
            }
        // A proximal edge joined to the torso is not an exterior contour. Test
        // the combined silhouette there; distal folds retain their overlap ink.
        var ink = source.AsSpan((86 * 96 + 43) * 4, 4).ToArray();
        for (var y = 1; y < Size - 1; y++) for (var x = 1; x < Size - 1; x++)
            {
                var i = y * Size + x;
                if (!coverage[i] || !exposed[i] || coverage[i - 1] && coverage[i + 1] && coverage[i - Size] && coverage[i + Size]) continue;
                if (!distal[i] && JoinedCoverage(i - 1) && JoinedCoverage(i + 1) && JoinedCoverage(i - Size) && JoinedCoverage(i + Size)) continue;
                var o = i * 4; var alpha = output[o + 3];
                if (alpha > 0 && Darkness(output.AsSpan(o, 4)) > 510 && ink[3] > 0)
                    for (var k = 0; k < 3; k++) output[o + k] = (byte)Math.Min(output[o + k], ink[k] * alpha / ink[3]);
            }
        // Triangles select one final paw texel per destination. Composite that
        // complete layer exactly once, so shared edges never double-blend AA and
        // a translucent distal contour cannot punch a hole in the opaque torso.
        for (var i = 0; i < coverage.Length; i++) if (coverage[i])
            {
                var o = i * 4; var inverseAlpha = 255 - output[o + 3];
                for (var channel = 0; channel < 4; channel++)
                    output[o + channel] = (byte)(output[o + channel] + (torso[o + channel] * inverseAlpha + 127) / 255);
            }
        // Ignore faint antialias fringe when deciding whether solid torso closes
        // a boundary, so genuinely exposed outer pixels still receive ink.
        bool JoinedCoverage(int index) => coverage[index] || torso[index * 4 + 3] >= 128;
        void Triangle(PointD sa, PointD sb, PointD sc)
        {
            var a = geometry.Vertex(sa, pull); var b = geometry.Vertex(sb, pull); var c = geometry.Vertex(sc, pull);
            var den = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y); if (Math.Abs(den) < 1e-10) return;
            for (var y = Math.Max(-Pad, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y)))); y <= Math.Min(Size - Pad - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y)))); y++)
                for (var x = Math.Max(-Pad, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X)))); x <= Math.Min(Size - Pad - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X)))); x++)
                {
                    var wa = ((b.Y - c.Y) * (x + .5 - c.X) + (c.X - b.X) * (y + .5 - c.Y)) / den;
                    var wb = ((c.Y - a.Y) * (x + .5 - c.X) + (a.X - c.X) * (y + .5 - c.Y)) / den; var wc = 1 - wa - wb;
                    if (wa < -1e-8 || wb < -1e-8 || wc < -1e-8) continue;
                    var u = (int)Math.Floor(wa * sa.X + wb * sb.X + wc * sc.X); var v = (int)Math.Floor(wa * sa.Y + wb * sb.Y + wc * sc.Y);
                    if (u < 0 || u >= 96 || v < 0 || v >= 96 || !mask[v * 96 + u] || source[(v * 96 + u) * 4 + 3] == 0) continue;
                    if (v < geometry.WristY && geometry.OccludesProximal(x + .5, y + .5) &&
                        x >= 0 && x < 96 && y >= 0 && y < 96 && source[(y * 96 + x) * 4 + 3] > 0) continue;
                    var o = Offset(x, y); source.AsSpan((v * 96 + u) * 4, 4).CopyTo(output.AsSpan(o, 4));
                    coverage[o / 4] = true; exposed[o / 4] = v >= geometry.Root.Y + 1;
                    distal[o / 4] = v >= geometry.WristY;
                }
        }
    }

    private static void DrawClosure(byte[] source, byte[] output, PawGeometry geometry, bool[] mask)
    {
        var arc = new PointD[33];
        for (var n = 0; n <= 32; n++)
        {
            var t = n / 32d; var a = 1 - t; var c = geometry.Closure;
            arc[n] = BodyPullSession.Scale(c[0], a * a * a) + BodyPullSession.Scale(c[1], 3 * a * a * t) + BodyPullSession.Scale(c[2], 3 * a * t * t) + BodyPullSession.Scale(c[3], t * t * t);
        }
        var fill = source.AsSpan((70 * 96 + 53) * 4, 4); var ink = source.AsSpan((86 * 96 + 43) * 4, 4);
        for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++)
            {
                if (!mask[y * 96 + x]) continue;
                for (var n = 1; n < arc.Length; n++)
                {
                    var a = arc[n - 1]; var b = arc[n];
                    if (x + .5 < a.X || x + .5 > b.X) continue;
                    var curveY = a.Y + (b.Y - a.Y) * (x + .5 - a.X) / (b.X - a.X);
                    var coverage = Math.Clamp(curveY + .7 - (y + .5), 0, 1); if (coverage == 0) break;
                    var edge = Math.Clamp(1 - Math.Abs(y + .5 - curveY), 0, 1); var o = Offset(x, y);
                    for (var k = 0; k < 4; k++) output[o + k] = (byte)Math.Round((fill[k] * (1 - edge) + ink[k] * edge) * coverage);
                    break;
                }
            }
    }
    private static int Offset(int x, int y) => ((y + Pad) * Size + x + Pad) * 4;
}
