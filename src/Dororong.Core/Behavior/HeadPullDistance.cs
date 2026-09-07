using Dororong.Core.Geometry;

namespace Dororong.Core.Behavior;

/// <summary>One radial DIP measurement for head extension and carry acquisition.</summary>
public readonly record struct HeadPullDistance(double Distance, double Strength, bool OutsideDeadzone, PointD Extension)
{
    public const double FullExtension = 80;

    public static bool IsFinite(PointD point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    public static bool TryMeasure(PointD origin, PointD pointer, SizeD threshold, out HeadPullDistance result)
    {
        result = default;
        var offset = pointer - origin;
        if (!IsFinite(origin) || !IsFinite(pointer) || !IsFinite(offset)) return false;
        var scale = Math.Max(Math.Abs(offset.X), Math.Abs(offset.Y));
        if (scale == 0) return true;
        var scaledX = offset.X / scale;
        var scaledY = offset.Y / scale;
        var length = Math.Sqrt(scaledX * scaledX + scaledY * scaledY);
        var distance = scale * length;
        if (!double.IsFinite(distance)) return false;
        var ux = scaledX / length;
        var uy = scaledY / length;
        // Intersect the pointer ray with the OS rectangular click deadzone.
        var width = ValidThreshold(threshold.Width);
        var height = ValidThreshold(threshold.Height);
        var deadzone = Math.Min(ux == 0 ? double.PositiveInfinity : width / Math.Abs(ux),
            uy == 0 ? double.PositiveInfinity : height / Math.Abs(uy));
        deadzone = Math.Min(deadzone, Math.BitDecrement(FullExtension));
        var strength = Math.Clamp((distance - deadzone) / (FullExtension - deadzone), 0, 1);
        result = new(distance, strength, distance >= deadzone,
            new PointD(ux * FullExtension, uy * FullExtension));
        return true;
    }

    private static double ValidThreshold(double value) => double.IsFinite(value) && value > 0 ? value : 4;
}
