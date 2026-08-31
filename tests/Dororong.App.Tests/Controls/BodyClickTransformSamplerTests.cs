using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public sealed class BodyClickTransformSamplerTests
{
    [Fact]
    public void PendingPressUsesSubtleFootAnchoredCompression()
    {
        var sample = BodyClickTransformSampler.SamplePendingPress();

        Assert.Equal(1.012, sample.ScaleX, precision: 6);
        Assert.Equal(0.975, sample.ScaleY, precision: 6);
        Assert.Equal(0, sample.TranslationY, precision: 6);
    }

    [Theory]
    [InlineData(0.00, 1.012, 0.975, 0.0)]
    [InlineData(0.08, 1.024, 0.960, 0.0)]
    [InlineData(0.16, 1.000, 1.000, 0.0)]
    [InlineData(0.31, 1.000, 1.000, -10.5)]
    [InlineData(0.46, 1.000, 1.000, -12.0)]
    [InlineData(0.56, 1.000, 1.000, -12.0)]
    [InlineData(0.75, 1.000, 1.000, -6.0)]
    [InlineData(0.84, 1.000, 1.000, 0.0)]
    [InlineData(0.875, 1.0125, 0.9825, 0.0)]
    [InlineData(0.92, 1.0241, 0.9662, 0.0)]
    [InlineData(1.00, 1.000, 1.000, 0.0)]
    public void ConfirmedClickSamplesTheApprovedFourPartTimeline(
        double phase,
        double expectedScaleX,
        double expectedScaleY,
        double expectedTranslationY)
    {
        var sample = BodyClickTransformSampler.SampleConfirmedClick(phase);

        Assert.Equal(expectedScaleX, sample.ScaleX, precision: 4);
        Assert.Equal(expectedScaleY, sample.ScaleY, precision: 4);
        Assert.Equal(expectedTranslationY, sample.TranslationY, precision: 4);
    }

    [Fact]
    public void ConfirmedClickMovesContinuouslyAtSixteenMillisecondCadence()
    {
        var samples = Enumerable.Range(0, 33)
            .Select(index => BodyClickTransformSampler.SampleConfirmedClick(Math.Min(index * 16d / 500d, 1)))
            .ToArray();

        var lift = samples.Where((_, index) => index is >= 5 and <= 14).ToArray();
        Assert.All(lift.Zip(lift.Skip(1)), pair => Assert.True(pair.Second.TranslationY < pair.First.TranslationY));

        var descent = samples.Where((_, index) => index is >= 21 and <= 26).ToArray();
        Assert.All(descent.Zip(descent.Skip(1)), pair => Assert.True(pair.Second.TranslationY > pair.First.TranslationY));

        Assert.All(samples.Zip(samples.Skip(1)), pair =>
        {
            Assert.InRange(Math.Abs(pair.Second.TranslationY - pair.First.TranslationY), 0, 3.6);
            Assert.InRange(Math.Abs(pair.Second.ScaleX - pair.First.ScaleX), 0, 0.017);
            Assert.InRange(Math.Abs(pair.Second.ScaleY - pair.First.ScaleY), 0, 0.025);
        });

        Assert.Equal(BodyClickTransformSample.Rest, BodyClickTransformSampler.SampleConfirmedClick(1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void ConfirmedClickClampsOutsideProgressToAnExactEndpoint(double phase)
    {
        var expected = phase < 0
            ? BodyClickTransformSampler.SamplePendingPress()
            : BodyClickTransformSample.Rest;

        Assert.Equal(expected, BodyClickTransformSampler.SampleConfirmedClick(phase));
    }
}
