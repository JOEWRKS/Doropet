using Dororong.App.Interaction;
using Dororong.Core.Geometry;
using static Dororong.App.Interaction.BodyPullSession;

namespace Dororong.App.Controls;

// Product-coordinate runtime geometry. Masks and closures are authored assumptions,
// disclosed in the proof overlay; the frozen product texels remain the only texture.
internal sealed record PawGeometry(PointD[] Mask, PointD Root, PointD Tip, double WristY, PointD[] Closure)
{
    private readonly double _gateLeft = Mask.Min(p => p.X);
    private readonly double _gateRight = Mask.Max(p => p.X);
    internal static PawGeometry For(BodyRegion region) => region switch
    {
        BodyRegion.FrontPaw => new([new(17, 70), new(28, 70), new(28, 79), new(26, 82), new(22, 83), new(19, 80), new(17, 76)], new(22, 71), new(23, 79), 77, [new(17, 70), new(20, 74), new(25, 74), new(28, 71)]),
        BodyRegion.MiddlePaw => new([new(32, 76), new(49, 76), new(49, 85), new(46, 88), new(41, 89), new(36, 85), new(33, 81)], new(39, 77), new(43, 85), 83, [new(32, 76), new(37, 80), new(44, 80), new(49, 76)]),
        BodyRegion.RightPaw => new([new(59, 72), new(72, 72), new(72, 82), new(69, 85), new(64, 86), new(61, 82), new(59, 77)], new(64, 74), new(66, 82), 80, [new(59, 73), new(63, 77), new(68, 77), new(72, 72)]),
        _ => throw new ArgumentOutOfRangeException(nameof(region))
    };

    internal PointD Vertex(PointD source, PointD pull)
    {
        // A pointer can enter the protected face, but paw material must fold below
        // it. This smooth compressive response is render-only; session pull/carry
        // coordinates remain untouched. Horizontal/downward displacement is exact.
        if (pull.Y < 0)
        {
            var upwardRoom = Root.X < 30 ? 5 : Root.X < 50 ? 8 : 12;
            pull = new(pull.X, upwardRoom * Math.Tanh(pull.Y / upwardRoom));
        }
        var target = Tip + pull; var rest = Tip - Root; var reach = target - Root;
        var direction = Unit(reach, Unit(rest, new(0, 1))); var normal = new PointD(-direction.Y, direction.X);
        var bend = Math.Sqrt(Math.Max(0, Length(rest) * Length(rest) - Length(reach) * Length(reach))) * .65;
        var elbow = Scale(Root + target, .5) + Scale(normal, bend);
        var end = Unit(target - elbow, direction);
        var baseAngle = Math.Atan2(rest.Y, rest.X);
        var angle = Math.Atan2(end.Y, end.X) - baseAngle; var cos = Math.Cos(angle); var sin = Math.Sin(angle);
        PointD Rotate(PointD p) => target + new PointD((p.X - Tip.X) * cos - (p.Y - Tip.Y) * sin, (p.X - Tip.X) * sin + (p.Y - Tip.Y) * cos);
        var wrist = Rotate(new(Root.X + (WristY - Root.Y) * rest.X / rest.Y, WristY));
        if (source.Y >= WristY) return Rotate(source);
        var control = Scale(Root + wrist, .5) + Scale(normal, bend * .8);
        var t = Math.Clamp((source.Y - Root.Y) / (WristY - Root.Y), 0, 1); var a = 1 - t;
        var center = Scale(Root, a * a) + Scale(control, 2 * a * t) + Scale(wrist, t * t);
        var tangent = Math.Atan2(2 * a * (control.Y - Root.Y) + 2 * t * (wrist.Y - control.Y), 2 * a * (control.X - Root.X) + 2 * t * (wrist.X - control.X)) - baseAngle;
        var mix = t * (2 - t);
        var nx = 1 - mix + mix * ((1 - t) * Math.Cos(tangent) + t * cos);
        var ny = mix * ((1 - t) * Math.Sin(tangent) + t * sin);
        var offset = source.X - (Root.X + (source.Y - Root.Y) * rest.X / rest.Y);
        return center + new PointD(offset * nx, offset * ny);
    }
    internal bool OccludesProximal(double x, double y)
    {
        // Authored curved torso depth gate, local to this attachment; distal paw
        // material is drawn in front of it, then the exact head wins over both.
        var u = (x - (_gateLeft + _gateRight) / 2) / ((_gateRight - _gateLeft) / 2);
        return Math.Abs(u) <= 1 && y >= Root.Y - 12 && y <= Root.Y + 1.5 * (1 - u * u);
    }
    private static PointD Unit(PointD value, PointD fallback) => Length(value) > .0001 ? Scale(value, 1 / Length(value)) : fallback;
}

internal sealed class BodyFlow
{
    private static readonly double[] Distances = BuildDistances();
    private readonly PointD _pull, _anchor;
    private readonly double _gate, _rx, _ry;
    internal BodyFlow(BodyRegion region, PointD pull, PointD anchor)
    {
        _pull = Limit(pull); _anchor = anchor;
        _gate = Math.Max(2, Distance(anchor.X, anchor.Y) * .75);
        _rx = region == BodyRegion.Belly ? 16 : 12; _ry = region == BodyRegion.Belly ? 16 : 17;
    }
    internal static PointD Limit(PointD pull)
    {
        var m = Math.Max(Math.Abs(pull.X), Math.Abs(pull.Y));
        if (!double.IsFinite(m) || m == 0) return default;
        var unit = Scale(pull, 1 / m); var n = Length(unit);
        return Scale(unit, 10 * Math.Tanh(m * n / 10) / n);
    }
    internal PointD Map(PointD point, double sign = 1)
    {
        var step = Scale(_pull, sign / 24);
        for (var n = 0; n < 24; n++)
        {
            var mid = point + Scale(step, Field(point) * .5);
            point += Scale(step, Field(mid));
        }
        return point;
    }
    private double Field(PointD p)
    {
        var t = Math.Min(1, Distance(p.X, p.Y) / _gate);
        var gx = (p.X - _anchor.X) / _rx; var gy = (p.Y - _anchor.Y) / _ry;
        return t * t * (3 - 2 * t) * Math.Exp(-gx * gx - gy * gy);
    }
    private static double[] BuildDistances()
    {
        var table = new double[161 * 161];
        for (var y = 0; y <= 160; y++) for (var x = 0; x <= 160; x++) table[y * 161 + x] = Math.Max(0, BodyRegionMap.HeadDistance(x - 32, y - 32) - .8);
        return table;
    }
    private static double Distance(double x, double y)
    {
        var u = Math.Clamp(x + 32, 0, 159.999999); var v = Math.Clamp(y + 32, 0, 159.999999);
        var ix = (int)u; var iy = (int)v; var fx = u - ix; var fy = v - iy; var i = iy * 161 + ix;
        return (Distances[i] * (1 - fx) + Distances[i + 1] * fx) * (1 - fy) + (Distances[i + 161] * (1 - fx) + Distances[i + 162] * fx) * fy;
    }
}
