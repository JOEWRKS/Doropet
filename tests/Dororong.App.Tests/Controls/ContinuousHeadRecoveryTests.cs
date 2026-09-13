using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Controls;

public class ContinuousHeadRecoveryTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, .4)]
    [InlineData(true, .4)]
    public void Captured_full_and_partial_release_preserves_first_pose_and_relaxes_continuously(bool mirror, double strength)
        => CheekProductTests.Sta(() =>
    {
        var (p, pet, direct) = HeadTiltSamplingTests.Setup(mirror);
        direct = direct with { Phase = strength < 1 ? DirectInteractionPhase.BodyDragEntry : DirectInteractionPhase.BodyDragHold, Strength = strength };
        p.Render(pet, direct, TimeSpan.Zero); CheekProductTests.Layout(p);
        var image = (Image)p.FindName("DororongImage");
        var held = Pixels((BitmapSource)image.Source);
        var release = direct with { Phase = DirectInteractionPhase.BodyDragSettle, RequiresCapture = false,
            IsPartialDragSettle = strength < 1 };
        p.Render(pet with { State = PetState.Idle }, release, TimeSpan.Zero); CheekProductTests.Layout(p);
        Assert.Equal(held, Pixels((BitmapSource)image.Source));
        var previousHeight = p.MeasurePlatformGeometry()!.Value.Bounds.Height;
        for (var i = 1; i <= 12; i++)
        {
            p.Render(pet with { State = PetState.Idle }, release with { ReleaseProgress = i / 12d }, TimeSpan.FromMilliseconds(15));
            CheekProductTests.Layout(p);
            var height = p.MeasurePlatformGeometry()!.Value.Bounds.Height;
            Assert.InRange(Math.Abs(height - previousHeight), 0, 3.5);
            previousHeight = height;
        }
        var completed = Pixels((BitmapSource)image.Source);
        p.Render(pet with { State = PetState.Idle }, DirectInteractionSnapshot.None, TimeSpan.Zero);
        Assert.Equal(UprightRumpTests.CleanFixture(completed), Pixels((BitmapSource)image.Source));
    });

    private static byte[] Pixels(BitmapSource source)
    {
        var bytes = new byte[source.PixelWidth * source.PixelHeight * 4];
        source.CopyPixels(bytes, source.PixelWidth * 4, 0);
        return bytes;
    }
}
