namespace Dororong.App.Controls;

internal sealed class LocomotionPresentation
{
    private const double SitDurationMilliseconds = 650;
    private const double GaitBlendMilliseconds = 180;
    private double _sitProgress;
    private double _walkProgress;

    internal double Sit => Smooth(_sitProgress);
    internal double Walk => Smooth(_walkProgress);
    internal double Distance { get; private set; }
    internal bool HoldWalking => _sitProgress > 0;

    internal void Advance(double milliseconds, bool walking, double distance, bool blocked, bool sitRequested = false)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0 || !double.IsFinite(distance))
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        if (blocked)
        {
            _sitProgress = _walkProgress = Distance = 0;
            return;
        }
        if (sitRequested)
        {
            _sitProgress = Math.Min(1, _sitProgress + milliseconds / SitDurationMilliseconds);
            _walkProgress = Math.Max(0, _walkProgress - milliseconds / GaitBlendMilliseconds);
            return;
        }
        var rising = _sitProgress > 0;
        _sitProgress = Math.Max(0, _sitProgress - milliseconds / SitDurationMilliseconds);
        if (walking && !rising)
        {
            _walkProgress = Math.Min(1, _walkProgress + milliseconds / GaitBlendMilliseconds);
            Distance = (Distance + Math.Abs(distance)) % 10;
        }
        else
        {
            _walkProgress = Math.Max(0, _walkProgress - milliseconds / GaitBlendMilliseconds);
        }
    }

    private static double Smooth(double x) => x * x * (3 - 2 * x);
}
