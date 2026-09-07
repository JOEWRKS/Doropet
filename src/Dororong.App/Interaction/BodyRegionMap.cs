using Dororong.Core.Geometry;

namespace Dororong.App.Interaction;

internal enum BodyRegion { None, FrontPaw, MiddlePaw, RightPaw, Belly, Rump }

internal static class BodyRegionMap
{
    // Authored on source-coordinate-grid.png (96x96), not a rescale of the prototype.
    // Boundary includes hair/rose/ribbon and their edge texels; hidden anatomy is not inferred.
    internal static readonly PointD[] Foreground = [new(0, 0), new(96, 0), new(96, 48), new(69, 48), new(69, 51), new(67, 55), new(65, 59), new(63, 63), new(60, 62), new(58, 58), new(56, 62), new(52, 66), new(49, 70), new(46, 72), new(43, 72), new(42, 68), new(23, 68), new(20, 70), new(17, 70), new(0, 70)];
    internal static bool IsProtected(double x, double y) => HeadDistance(x + .5, y + .5) <= .8;
    // Excluded connector/valley sample area; this does not assert a fourth paw's anatomy.
    internal static bool IsExcludedConnector(double x, double y) => x >= 27 && x < 34 && y >= 74 && y < 78;

    internal static BodyRegion Pick(PointD point, ReadOnlySpan<byte> pixels)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || point.X < 0 || point.Y < 0 || point.X >= 96 || point.Y >= 96 || pixels.Length != 96 * 96 * 4) return BodyRegion.None;
        var x = (int)point.X; var y = (int)point.Y;
        if (pixels[(y * 96 + x) * 4 + 3] == 0 || IsProtected(x, y) || IsExcludedConnector(x, y)) return BodyRegion.None;
        if (x < 28 && y >= 69) return BodyRegion.FrontPaw;
        if (x > 58 && y > 70 + (x - 59) * .2) return BodyRegion.RightPaw;
        if (x > 60 + (y - 65) * .12) return BodyRegion.Rump;
        if (x > 44 + (74 - y) * .4 && y < 80 - (x - 46) * .25) return BodyRegion.Belly;
        return BodyRegion.MiddlePaw;
    }

    internal static (PointD Root, PointD Tip) Limb(BodyRegion region, PointD anchor) => region switch
    {
        BodyRegion.FrontPaw => (new(22, 71), new(23, 79)),
        BodyRegion.MiddlePaw => (new(39, 77), new(43, 85)),
        BodyRegion.RightPaw => (new(64, 74), new(66, 82)),
        _ => (anchor, anchor)
    };

    internal static bool Inside(IReadOnlyList<PointD> polygon, double x, double y)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i]; var b = polygon[j];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    internal static double HeadDistance(double x, double y)
    {
        if (Inside(Foreground, x, y)) return 0;
        var best = double.MaxValue;
        for (var i = 0; i < Foreground.Length; i++)
        {
            var a = Foreground[i]; var b = Foreground[(i + 1) % Foreground.Length];
            var dx = b.X - a.X; var dy = b.Y - a.Y;
            var t = Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
            best = Math.Min(best, Math.Sqrt(Math.Pow(x - a.X - t * dx, 2) + Math.Pow(y - a.Y - t * dy, 2)));
        }
        return best;
    }
}
