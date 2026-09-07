using Dororong.Core.Geometry;

namespace Dororong.App.Interaction;

internal readonly record struct HeadLandingSnapshot(PointD WindowPosition, double ElapsedMilliseconds, double Compression);

// Release-position motion only. The existing180ms pose return ends before contact;
// the following squash/rebound is presentation, not additional window travel.
internal static class HeadLandingMotion
{
    internal const double MaximumDrop = 36;
    internal const double FallMilliseconds = 220;
    internal const double DurationMilliseconds = FallMilliseconds + 220;

    internal static HeadLandingSnapshot Sample(PointD origin, double distance, double strength,
        double elapsedMilliseconds, RectD workArea, SizeD petSize)
    {
        var elapsed = Math.Clamp(elapsedMilliseconds, 0, DurationMilliseconds);
        var fall = Math.Clamp(elapsed / FallMilliseconds, 0, 1);
        var position = workArea.ClampTopLeft(origin + new PointD(0, distance * fall * fall), petSize);
        var landingElapsed = elapsed - FallMilliseconds;
        var compression = landingElapsed switch
        {
            < 0 => 0,
            < 40 => .18 * Smooth(landingElapsed / 40),
            < 120 => .18 - .22 * Smooth((landingElapsed - 40) / 80),
            _ => -.04 * (1 - Smooth((landingElapsed - 120) / 100))
        };
        // Even a zero-travel release gets a small landing response, never a full
        // impact from a drop that the work-area boundary prevented.
        var impact = strength * .4 + .6 * distance / MaximumDrop;
        return new(position, elapsed, compression * impact);
    }

    private static double Smooth(double p) => p * p * (3 - 2 * p);
}
