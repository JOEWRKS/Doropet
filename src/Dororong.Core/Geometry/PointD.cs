namespace Dororong.Core.Geometry;

public readonly record struct PointD(double X, double Y)
{
    public static PointD operator +(PointD point, PointD offset) =>
        new(point.X + offset.X, point.Y + offset.Y);

    public static PointD operator -(PointD point, PointD offset) =>
        new(point.X - offset.X, point.Y - offset.Y);
}
