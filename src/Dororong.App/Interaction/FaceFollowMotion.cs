// Ported from the user-accepted cheek-round-outline preview; raster math unchanged.
namespace Dororong.App.Interaction;

internal sealed class FaceFollowMotion
{
    internal double Eye { get; private set; }
    internal double Hair { get; private set; }
    bool releasing;double releasedMs,releaseEye,releaseHair;
    internal void Step(double target,double milliseconds)
    {
        if(!double.IsFinite(target)||!double.IsFinite(milliseconds)||milliseconds<0)throw new ArgumentException("Finite target and nonnegative elapsed time required");
        if(releasing){
            releasedMs=Math.Min(220,releasedMs+milliseconds);var t=releasedMs/220;var amount=1-t*t*(3-2*t);
            Eye=releaseEye*amount;Hair=releaseHair*amount;return;
        }
        target=Math.Clamp(target,0,20);
        Eye+=(target-Eye)*(1-Math.Exp(-milliseconds/55));
        Hair+=(target-Hair)*(1-Math.Exp(-milliseconds/90));
    }
    internal void BeginRelease(){if(releasing)return;releasing=true;releasedMs=0;releaseEye=Eye;releaseHair=Hair;}
    internal void Reset(){Eye=0;Hair=0;releasing=false;releasedMs=0;releaseEye=0;releaseHair=0;}
}
