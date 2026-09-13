using Dororong.App.Controls;
using Dororong.App.Interaction;

namespace Dororong.App.Tests.Controls;

public class PerchPawSeamTests
{
    [Theory]
    [InlineData(false, 35)]
    [InlineData(true, 49)]
    public void Downward_pull_keeps_opaque_shoulder_join_over_dark_background(bool right, int x) => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        foreach (var source in new[] { PerchExpressionFrames.Open, PerchExpressionFrames.Closed })
        {
            var original = PerchExpressionTests.Pixels(source);
            Assert.Equal(255, original[(63 * 100 + x) * 4 + 3]);
            Assert.Equal(255, original[(64 * 100 + x) * 4 + 3]);
            foreach (var distance in new[] { 1d, 6d, 12d, 18d })
            {
                var rendered = PerchExpressionTests.Pixels(PerchPawRenderer.Render(source, new(right, new(0, distance))));
                // An opaque interior must not turn translucent at the cut row;
                // black window/taskbar pixels otherwise show as an inner line.
                Assert.Equal(255, rendered[(64 * 100 + x) * 4 + 3]);
                Assert.Equal(original.AsSpan(0, 64 * 100 * 4).ToArray(), rendered.AsSpan(0, 64 * 100 * 4).ToArray());
            }
        }
    });
}
