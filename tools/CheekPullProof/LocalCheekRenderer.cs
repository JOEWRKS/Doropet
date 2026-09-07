namespace CheekPullProof;

// Stage A only. This assembly is not referenced by the App.
internal static class LocalCheekRenderer
{
    internal const int Size = 96;
    // Manually inspected canonical skin/selected-face texels, inclusive x bounds.
    // No hair, outline, eye or mouth texels are included.
    internal static readonly (int Y, int Left, int Right)[] Rows =
    [
        (53, 14, 17), (54, 13, 18), (55, 13, 19), (56, 13, 20),
        (57, 13, 23), (58, 13, 24), (59, 14, 24), (60, 15, 24),
        (61, 16, 24), (62, 18, 24), (63, 21, 24)
    ];

    internal static bool Selected(int x, int y) =>
        Rows.Any(row => row.Y == y && x >= row.Left && x <= row.Right);

    internal static byte[] Render(byte[] source, double outwardDips, double verticalDips = 0,
        double releaseMilliseconds = 0, bool cancelled = false)
    {
        if (source.Length != Size * Size * 4) throw new ArgumentException("Expected 96x96 BGRA source.");
        var output = (byte[])source.Clone();
        if (cancelled || releaseMilliseconds >= 220) return output;
        if (!double.IsFinite(outwardDips) || !double.IsFinite(verticalDips) || !double.IsFinite(releaseMilliseconds))
            throw new ArgumentException("Finite displacement and release time required.");
        var progress = Math.Clamp(releaseMilliseconds / 220, 0, 1);
        var release = 1 - progress * progress * (3 - 2 * progress);
        // Assumed Stage A scale only: 96 source pixels in 144 DIPs. This is NOT
        // a measured production transform (the production image is 96 DIPs inside
        // its 144-DIP window); real captured affine mapping was gated out of Stage A.
        // Max 20 DIP gesture gives a 6-source-pixel tip displacement before occlusion.
        var horizontal = Math.Clamp(outwardDips, -10, 20) / 1.5 * 0.45 * release;
        var vertical = Math.Clamp(verticalDips, -20, 20) / 1.5 * 0.25 * release;
        if (horizontal == 0 && vertical == 0) return output;
        foreach (var row in Rows)
        {
            var t = Math.Max(0, 1 - Math.Abs(row.Y - 58) / 5.0);
            var envelope = t * t * (3 - 2 * t);
            var root = row.Right + 1.0;
            var width = root - row.Left;
            var stretch = 1 + horizontal * envelope / width;
            for (var x = row.Left; x <= row.Right; x++)
            {
                // Inverse original-texel sampling. The root stays fixed; movement tapers at
                // the upper/lower attachment. Locked opaque destinations fully occlude it.
                // The selected skin has no transparent neighbor, so its connected visible
                // destination is confined to this precise mask. Do not paint an island
                // beyond the locked hair or call the resulting interior warp a cheek pull.
                var sx = root + (x - root) / stretch;
                var influence = (root - x) / width * envelope;
                var sy = Math.Clamp((int)Math.Round(row.Y - vertical * influence), Rows[0].Y, Rows[^1].Y);
                var sourceRow = Rows.Single(candidate => candidate.Y == sy);
                var ix = Math.Clamp((int)Math.Round(sx), sourceRow.Left, sourceRow.Right);
                Array.Copy(source, (sy * Size + ix) * 4, output, (row.Y * Size + x) * 4, 4);
            }
        }
        return output;
    }
}
