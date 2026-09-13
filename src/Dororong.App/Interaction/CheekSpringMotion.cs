namespace Dororong.App.Interaction;

// Face recovery and backward hop overlap, sampled from elapsed release time.
internal static class CheekSpringMotion
{
    internal const double ReturnMilliseconds=440;
    internal const double DurationMilliseconds=ReturnMilliseconds;
    private const double HopStartMilliseconds=65;
    private const double HopDurationMilliseconds=280;
    internal static double Remaining(double milliseconds)
    {
        if(milliseconds<=0)return 1;
        if(milliseconds>=ReturnMilliseconds)return 0;
        var t=milliseconds/1000;
        return Math.Exp(-7.5*t)*Math.Cos(17*t)*(1-Smooth((milliseconds-360)/80));
    }
    internal static double Offset(double milliseconds,double pull)
    {
        if(milliseconds<=HopStartMilliseconds)return 0;
        var strength=Math.Clamp(pull/20,0,1);
        var amplitude=2*Smooth(strength/.25)+6*Smooth((strength-.75)/.25);
        return amplitude*Smooth((milliseconds-HopStartMilliseconds)/HopDurationMilliseconds);
    }
    internal static double Lift(double milliseconds,double pull)
    {
        // Launch just before the first rest crossing; remain landed during the
        // final small face oscillation instead of replaying the vertical arc.
        if(milliseconds<=HopStartMilliseconds||milliseconds>=HopStartMilliseconds+HopDurationMilliseconds)return 0;
        var strength=Math.Clamp(pull/20,0,1);
        var amplitude=Smooth(strength/.25)+3*Smooth((strength-.75)/.25);
        return amplitude*Math.Pow(Math.Sin(Math.PI*(milliseconds-HopStartMilliseconds)/HopDurationMilliseconds),2);
    }
    private static double Smooth(double value){var t=Math.Clamp(value,0,1);return t*t*(3-2*t);}
}
