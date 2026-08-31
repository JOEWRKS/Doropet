namespace Dororong.App.Controls;

internal readonly record struct BodyClickTransformSample(
    double ScaleX,
    double ScaleY,
    double TranslationY)
{
    internal static BodyClickTransformSample Rest { get; } = new(1, 1, 0);
}

internal static class BodyClickTransformSampler
{
    private static readonly BodyClickTransformSample PendingPress = new(1.012, 0.975, 0);
    private static readonly BodyClickTransformSample DeepPress = new(1.024, 0.960, 0);

    internal static BodyClickTransformSample SamplePendingPress() => PendingPress;

    internal static BodyClickTransformSample SampleConfirmedClick(double phase)
    {
        var progress = Math.Clamp(phase, 0, 1);
        if (progress <= 0)
        {
            return PendingPress;
        }

        if (progress >= 1)
        {
            return BodyClickTransformSample.Rest;
        }

        if (progress < 0.08)
        {
            return Interpolate(PendingPress, DeepPress, SmoothStep(progress / 0.08));
        }

        if (progress < 0.16)
        {
            return Interpolate(DeepPress, BodyClickTransformSample.Rest, SmoothStep((progress - 0.08) / 0.08));
        }

        if (progress < 0.46)
        {
            var liftProgress = (progress - 0.16) / 0.30;
            return new(1, 1, -12 * EaseOutCubic(liftProgress));
        }

        if (progress < 0.66)
        {
            return new(1, 1, -12);
        }

        if (progress < 0.84)
        {
            var descentProgress = (progress - 0.66) / 0.18;
            return new(1, 1, -12 * (1 - SmoothStep(descentProgress)));
        }

        if (progress < 0.91)
        {
            return Interpolate(
                BodyClickTransformSample.Rest,
                new BodyClickTransformSample(1.025, 0.965, 0),
                SmoothStep((progress - 0.84) / 0.07));
        }

        return Interpolate(
            new BodyClickTransformSample(1.025, 0.965, 0),
            BodyClickTransformSample.Rest,
            SmoothStep((progress - 0.91) / 0.09));
    }

    private static BodyClickTransformSample Interpolate(
        BodyClickTransformSample from,
        BodyClickTransformSample to,
        double amount) =>
        new(
            Lerp(from.ScaleX, to.ScaleX, amount),
            Lerp(from.ScaleY, to.ScaleY, amount),
            Lerp(from.TranslationY, to.TranslationY, amount));

    private static double Lerp(double from, double to, double amount) => from + ((to - from) * amount);

    private static double SmoothStep(double progress) => progress * progress * (3 - (2 * progress));

    private static double EaseOutCubic(double progress)
    {
        var remaining = 1 - progress;
        return 1 - (remaining * remaining * remaining);
    }

}
