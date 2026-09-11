using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Controls;

public sealed class ManualSittingTests
{
    [Theory]
    [InlineData(0)] // Original idle art before any walking.
    [InlineData(1)] // Walking bank.
    [InlineData(2)] // Seated bank.
    public void Ordinary_blinks_use_only_open_closed_eyes_with_shared_timing(int mode) => EdgePerchPresentationTests.Sta(() =>
    {
        var presenter = new DororongPresenter();
        presenter.SetSittingRequested(mode == 2);
        var image = (Image)presenter.FindName("DororongImage");
        var starts = new List<int>();
        var durations = new List<int>();
        var wasClosed = false;
        for (var ms = 20; ms <= 11000; ms += 20)
        {
            // Rapidly cycling core phases must not drive rapid eye flashes.
            presenter.RenderDesktop(new(mode == 1 ? PetState.Walk : PetState.Idle,
                new(100, 100), FacingDirection.Right, (ms % 500) / 500d, false, null),
                DirectInteractionSnapshot.None, TimeSpan.FromMilliseconds(20));
            if (ms < 1000) continue; // Complete pose entry before inspecting full-frame banks.
            var closed = mode == 0
                ? image.Source is BitmapImage bitmap && bitmap.UriSource.OriginalString.EndsWith("dororong-closed-eyes.png")
                : ReferenceEquals(image.Source, mode == 2 ? LocomotionFrames.Sit(1, true) : LocomotionFrames.Walk(1, 0, true));
            if (!closed)
            {
                if (mode == 0)
                    Assert.EndsWith("dororong-canonical.png", Assert.IsType<BitmapImage>(image.Source).UriSource.OriginalString);
                else
                    Assert.Same(mode == 2 ? LocomotionFrames.Sit(1) : LocomotionFrames.Walk(1, 0), image.Source);
            }
            if (closed && !wasClosed) starts.Add(ms);
            if (!closed && wasClosed) durations.Add(ms - starts[^1]);
            wasClosed = closed;
        }
        Assert.Equal(2, starts.Count);
        Assert.Equal(2, durations.Count);
        Assert.All(durations, duration => Assert.InRange(duration, 320, 400));
        Assert.InRange(starts[1] - starts[0], 4800, 5200);
    });

    [Fact]
    public void Idle_time_alone_never_sits()
    {
        var motion = new LocomotionPresentation();
        motion.Advance(10000, false, 0, false);
        Assert.Equal(0, motion.Sit);
    }

    [Fact]
    public void Context_menu_offers_sit_without_an_unlock_command() => EdgePerchPresentationTests.Sta(() =>
    {
        var presenter = new DororongPresenter();
        var menu = ((FrameworkElement)presenter.FindName("BodyGroup")).ContextMenu;
        Assert.Single(menu.Items.OfType<MenuItem>(), item => Equals(item.Header, "앉아"));
        Assert.DoesNotContain(menu.Items.OfType<MenuItem>(), item => Equals(item.Header, "고정 해제"));
    });
}
