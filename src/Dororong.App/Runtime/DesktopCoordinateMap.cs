using Dororong.Core.Geometry;
using Dororong.Core.Platforms;
using System.Windows;

namespace Dororong.App.Runtime;

internal readonly record struct DesktopCoordinateMap(PointD PhysicalOrigin, PointD LogicalOrigin, double ScaleX, double ScaleY)
{
    internal static DesktopCoordinateMap FromScreenSamples(PointD origin, PointD xDip, PointD yDip, PointD logicalOrigin)
    {
        if (!Finite(origin) || !Finite(xDip) || !Finite(yDip) || !Finite(logicalOrigin))
            throw new ArgumentOutOfRangeException(nameof(origin), "Screen samples must be finite.");
        if (Math.Abs(xDip.Y-origin.Y)>0.001 || Math.Abs(yDip.X-origin.X)>0.001)
            throw new InvalidOperationException("Presenter screen axes are not aligned with the desktop.");
        var map=new DesktopCoordinateMap(origin,logicalOrigin,xDip.X-origin.X,yDip.Y-origin.Y);
        map.Validate(); return map;
    }
    // Re-measure after host movement or DPI changes, then map the same physical scene/contact
    // together. Integration must verify PointToScreen against physical pointer coordinates on
    // every supported DPI setup; this method does not change process or thread DPI awareness.
    internal static DesktopCoordinateMap FromPresenter(FrameworkElement presenter, PointD viewportLogicalOrigin)
    {
        presenter.Dispatcher.VerifyAccess();
        var origin=presenter.PointToScreen(new Point(0,0));
        var x=presenter.PointToScreen(new Point(1,0));
        var y=presenter.PointToScreen(new Point(0,1));
        return FromScreenSamples(new(origin.X,origin.Y),new(x.X,x.Y),new(y.X,y.Y),viewportLogicalOrigin);
    }
    internal DesktopScene ToLogicalScene(DesktopScene scene)
    {
        var map=this;
        return scene with
        {
            Monitors=Array.AsReadOnly(scene.Monitors.Select(m=>m with {Bounds=map.ToLogicalRectangle(m.Bounds)}).ToArray()),
            Windows=Array.AsReadOnly(scene.Windows.Select(w=>w with {Bounds=map.ToLogicalRectangle(w.Bounds)}).ToArray())
        };
    }
    internal FootContact ToLogicalContact(FootContact contact)
    {
        var left=ToLogical(new(contact.Left,contact.SoleY));
        var right=ToLogical(new(contact.Right,contact.VisibleTop));
        return new(left.X,right.X,left.Y,right.Y);
    }
    private RectD ToLogicalRectangle(RectD rectangle)
    {
        var topLeft=ToLogical(new(rectangle.X,rectangle.Y));
        return new(topLeft.X,topLeft.Y,rectangle.Width/ScaleX,rectangle.Height/ScaleY);
    }
    internal PointD ToLogical(PointD p)
    {
        Validate();
        return new(LogicalOrigin.X + (p.X-PhysicalOrigin.X)/ScaleX, LogicalOrigin.Y + (p.Y-PhysicalOrigin.Y)/ScaleY);
    }
    internal PointD ToPhysical(PointD p)
    {
        Validate();
        return new(PhysicalOrigin.X + (p.X-LogicalOrigin.X)*ScaleX, PhysicalOrigin.Y + (p.Y-LogicalOrigin.Y)*ScaleY);
    }
    private void Validate()
    {
        if (!Finite(PhysicalOrigin) || !Finite(LogicalOrigin))
            throw new ArgumentOutOfRangeException(nameof(PhysicalOrigin));
        if (!double.IsFinite(ScaleX) || ScaleX <= 0 || !double.IsFinite(ScaleY) || ScaleY <= 0)
            throw new ArgumentOutOfRangeException(nameof(ScaleX), "Screen displacement must be finite and positive.");
    }
    private static bool Finite(PointD p) => double.IsFinite(p.X) && double.IsFinite(p.Y);
}
