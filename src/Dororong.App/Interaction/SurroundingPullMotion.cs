using Dororong.Core.Geometry;

namespace Dororong.App.Interaction;

internal sealed class SurroundingPullMotion
{
    internal PointD Current { get; private set; }
    internal PointD Far { get; private set; }
    PointD releasedNear, releasedFar;
    bool releasing; double elapsed;
    internal void Step(PointD target, double milliseconds)
    {
        if(!double.IsFinite(target.X)||!double.IsFinite(target.Y)||!double.IsFinite(milliseconds)||milliseconds<0)
            throw new ArgumentException("Finite input and nonnegative elapsed time required.");
        if(releasing)
        {
            elapsed=Math.Min(200,elapsed+milliseconds);var t=elapsed/200;var amount=1-t*t*(3-2*t);
            Current=Scale(releasedNear,amount);Far=Scale(releasedFar,amount);return;
        }
        // Exact cascade solution for a piecewise-constant target, independent of tick partition.
        var a=Math.Exp(-milliseconds/75);var b=Math.Exp(-milliseconds/120);
        var old=Current;Current=target+Scale(old-target,a);
        Far=target+Scale(Far-target,b)+Scale(old-target,75d/(75-120)*(a-b));
    }
    internal void Release(){if(releasing)return;releasing=true;elapsed=0;releasedNear=Current;releasedFar=Far;}
    internal void Reset(){Current=Far=default;releasing=false;elapsed=0;}
    static PointD Scale(PointD p,double q)=>new(p.X*q,p.Y*q);
}
