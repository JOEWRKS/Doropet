namespace Dororong.Core.Geometry;

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public PointD ClampTopLeft(PointD topLeft, SizeD size) => new(
        Math.Clamp(topLeft.X, X, Math.Max(X, Right - size.Width)),
        Math.Clamp(topLeft.Y, Y, Math.Max(Y, Bottom - size.Height)));
}
