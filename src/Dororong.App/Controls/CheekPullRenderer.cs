namespace Dororong.App.Controls;

// Exact approved one-side raster algorithm; product input is captured separately.
internal static class CheekPullRenderer
{
    internal const int Size = 96;
    internal readonly record struct Row(int Y, int Edge, int Skin, int Root, double Weight);
    // Authored source selection from observed texels, not inferred anatomy.
    // Edge..Skin is the original contour/AA band; Skin..Root is skin.
    // Root is excluded/fixed. Rows narrow below the eye and above the mouth.
    internal static readonly Row[] Rows =
    [
        new(53,13,14,18,.08), new(54,12,13,18,.25), new(55,12,13,20,.50),
        new(56,12,13,21,.75), new(57,12,13,24,1), new(58,12,13,24,1),
        new(59,13,14,24,.95), new(60,13,15,24,.82), new(61,14,16,24,.64),
        new(62,16,18,24,.40), new(63,18,21,24,.17), new(64,19,22,24,0)
    ];

    internal static byte[] Render(byte[] source, double outwardDips, double verticalDips = 0,
        double releaseMilliseconds = 0, bool cancelled = false)
    {
        if (source.Length != Size * Size * 4) throw new ArgumentException("Expected96x96 BGRA.");
        if (!double.IsFinite(outwardDips) || !double.IsFinite(verticalDips) || !double.IsFinite(releaseMilliseconds))
            throw new ArgumentException("Finite gesture required.");
        if (verticalDips != 0) throw new NotSupportedException("This first-side study covers horizontal pulling only.");
        var result = (byte[])source.Clone();
        if (cancelled || releaseMilliseconds >= 220 || outwardDips == 0) return result;
        var p = Math.Clamp(releaseMilliseconds / 220, 0, 1);
        var released = 1 - p * p * (3 - 2 * p);
        // Current product Image is96DIP wide for96source pixels. This preview uses
        // that1:1 scale, with authored0.5response gain:20DIP gesture =>10px tip.
        var displacement = Math.Clamp(outwardDips, -10, 20) * .5 * released;
        foreach (var row in Rows)
        {
            if (displacement < 0)
            {
                // Inward motion compresses selected skin only; it must not erase
                // the immutable base/hair or invent pixels behind the cheek.
                var width = row.Root - row.Skin;
                var ratio = Math.Max(.5, 1 + displacement * row.Weight / width);
                for (var x = row.Skin; x < row.Root; x++)
                {
                    var sx = Math.Clamp((int)Math.Round(row.Root + (x - row.Root) / ratio), row.Skin, row.Root - 1);
                    Array.Copy(source, (row.Y * Size + sx) * 4, result, (row.Y * Size + x) * 4, 4);
                }
                continue;
            }
            var shift = (int)Math.Round(displacement * row.Weight);
            if (shift == 0) continue;
            var left = row.Edge - shift;
            var contourWidth = row.Skin - row.Edge;
            var skinLeft = left + contourWidth;
            for (var x = left; x < row.Root; x++)
            {
                // Move the original contour strip without horizontally stretching
                // its ink thickness; distribute original skin texels behind it.
                var sx = x < skinLeft ? row.Edge + x - left
                    : row.Skin + (int)Math.Round((x - skinLeft) * (double)(row.Root - 1 - row.Skin) / (row.Root - 1 - skinLeft));
                Array.Copy(source, (row.Y * Size + sx) * 4, result, (row.Y * Size + x) * 4, 4);
            }
        }
        if (displacement > 0) ConnectContour(source, result, displacement);
        return result;
    }

    // Keep the previously reviewed envelope/alpha/skin sampling exactly as-is.
    // Cover the continuous line between transported contour samples, instead of
    // assuming independently shifted horizontal bands remain connected.
    // 1.5px nominal stroke with a 1px coverage transition, evaluated in source
    // pixels. Only local ink RGB is blended; no whole-image filtering or new alpha.
    static void ConnectContour(byte[] source, byte[] result, double displacement)
    {
        var points = Rows.Select(r => (X: r.Edge - (int)Math.Round(displacement * r.Weight), Y: r.Y)).ToArray();
        for (var rowIndex = 0; rowIndex < Rows.Length; rowIndex++)
        {
            var row = Rows[rowIndex]; var left = points[rowIndex].X;
            if (left == row.Edge) continue;
            for (var x = left; x < row.Root; x++)
            {
                var best = double.PositiveInfinity; var segment = 0; var fraction = 0d;
                for (var j = 1; j < points.Length; j++)
                {
                    var a = points[j - 1]; var b = points[j];
                    var dx = b.X - a.X; var dy = b.Y - a.Y;
                    var t = Math.Clamp(((x - a.X) * dx + (row.Y - a.Y) * dy) / (double)(dx * dx + dy * dy), 0, 1);
                    var d = Math.Sqrt(Math.Pow(x - a.X - t * dx, 2) + Math.Pow(row.Y - a.Y - t * dy, 2));
                    if (d < best) { best = d; segment = j - 1; fraction = t; }
                }
                var coverage = Math.Clamp(1.25 - best, 0, 1);
                if (coverage == 0) continue;
                var first = Rows[segment]; var second = Rows[segment + 1];
                var aIndex = (first.Y * Size + first.Edge) * 4;
                var bIndex = (second.Y * Size + second.Edge) * 4;
                var target = (row.Y * Size + x) * 4;
                for (var c = 0; c < 3; c++)
                {
                    var ink = source[aIndex + c] * (1 - fraction) + source[bIndex + c] * fraction;
                    // Existing transported ink/AA is retained, never lightened.
                    result[target + c] = (byte)Math.Round(Math.Min(result[target + c], result[target + c] * (1 - coverage) + ink * coverage));
                }
            }
        }
    }
}
