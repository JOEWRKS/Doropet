using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

internal readonly record struct HuntPose(double Amount, double HeadBob, double HeadRoll, double Sway)
{
    public static HuntPose At(double seconds)
    {
        if (!double.IsFinite(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds), "Finite time required.");

        var amount = Ease((seconds - 0.25) / 0.45) * (1 - Ease((seconds - 2.05) / 0.55));
        var envelope = Ease((seconds - 0.7) / 0.18) * (1 - Ease((seconds - 1.65) / 0.25));
        return new HuntPose(
            amount,
            6.3 * amount,
            -0.07 * amount,
            1.425 * Math.Sin((seconds - 0.75) * Math.PI * 5) * envelope);
    }

    public PointD Head(double x, double y)
    {
        var dx = x - 43;
        var dy = y - 48;
        return new PointD(
            43 + (dx * Math.Cos(HeadRoll)) - (dy * Math.Sin(HeadRoll)),
            48 + (dx * Math.Sin(HeadRoll)) + (dy * Math.Cos(HeadRoll)) + HeadBob);
    }

    public PointD Map(double x, double y)
    {
        var head = Head(x, y);
        var rear = Ease((x - 48) / 23);
        var contact = 1 - Ease((y - 60) / (Floor(x) - 60));
        var fore = 1 - Ease((x - 46) / 8);
        var rootY = 66 + (8 * Ease((x - 24) / 12));
        var reach = Ease((y - rootY) / 11);
        var toeCenter = 23 + (20 * Ease((x - 27) / 5));
        var rounding = 1 - Ease((x - toeCenter + 7) / 7);
        var px = x + ((head.X - x) * (1 - rear) + (Sway * rear)) * contact
            - (11 - (2.2 * rounding)) * Amount * fore * reach;
        var py = y + ((head.Y - y) * (1 - rear) - (1.6 * Amount * rear) + (0.22 * Sway * rear)) * contact;
        var farToe = 23 - (11 * Amount);
        var padFloor = 79 + (6 * Ease((px - farToe - 3) / 9));
        var padZone = 1 - Ease((px - 44) / 9);
        var padLift = 1.15 * Amount * padZone * Ease((py - padFloor + 9) / 5) * Ease((padFloor - py) / 3);
        return new PointD(px, py - padLift);
    }

    private static double Floor(double x)
    {
        ReadOnlySpan<PointD> points = [new(23, 79), new(43, 85), new(54.5, 78.5), new(66, 82)];
        for (var i = 1; i < points.Length; i++)
        {
            if (x > points[i].X)
                continue;

            var a = points[i - 1];
            var b = points[i];
            var u = Clamp01((x - a.X) / (b.X - a.X));
            return a.Y + ((b.Y - a.Y) * u);
        }

        return 82;
    }

    private static double Ease(double value)
    {
        value = Clamp01(value);
        return value * value * (3 - (2 * value));
    }

    private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));
}

internal readonly record struct HuntGazePose(double EyeX, double EyeY, double Roll);

internal sealed class HuntGaze
{
    private double _eyeX;
    private double _eyeY;
    private double _headX;
    private double _headY;

    public HuntGazePose Advance(double seconds, PointD? target, bool flip, bool active)
    {
        ValidateDelta(seconds);
        var valid = active && target is { } point && double.IsFinite(point.X) && double.IsFinite(point.Y);
        var x = valid ? Clamp(target!.Value.X / 110) * (flip ? -1 : 1) : 0;
        var y = valid ? Clamp(target!.Value.Y / 85) : 0;
        var eye = 1 - Math.Exp(-seconds / 0.075);
        var head = 1 - Math.Exp(-seconds / 0.23);
        _eyeX += (x - _eyeX) * eye;
        _eyeY += (y - _eyeY) * eye;
        _headX += (x - _headX) * head;
        _headY += (y - _headY) * head;
        return new HuntGazePose(
            0.85 * _eyeX,
            0.65 * _eyeY,
            ((0.4375 * _headX) - (0.5625 * _headY)) * (Math.PI / 9));
    }

    public void Reset() => _eyeX = _eyeY = _headX = _headY = 0;

    private static double Clamp(double value) => Math.Max(-1, Math.Min(1, value));

    private static void ValidateDelta(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds), "Nonnegative finite delta required.");
    }
}
