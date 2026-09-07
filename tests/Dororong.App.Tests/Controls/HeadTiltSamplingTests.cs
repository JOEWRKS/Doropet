using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class HeadTiltSamplingTests
{
    [Theory]
    [InlineData(false, -14)]
    [InlineData(false, 14)]
    [InlineData(true, -14)]
    [InlineData(true, 14)]
    public void Tilted_head_uses_smooth_output_instead_of_the_previous_nearest_raster(bool mirror, double angle) => CheekProductTests.Sta(() =>
    {
        var (presenter, pet, direct) = Setup(mirror);
        presenter.Render(pet, direct with { HeadSwingDegrees = angle }, TimeSpan.Zero); CheekProductTests.Layout(presenter);
        Assert.NotEqual(0, ((RotateTransform)presenter.FindName("BodyRotateTransform")).Angle);
        var actual = Raster(presenter);
        var image = (Image)presenter.FindName("DororongImage");
        var selected = RenderOptions.GetBitmapScalingMode(image);
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        var previous = Raster(presenter);
        RenderOptions.SetBitmapScalingMode(image, selected);
        Assert.False(actual.SequenceEqual(previous), "Tilted presentation still has the old nearest-neighbor raster");
        Assert.Equal(BitmapScalingMode.Linear, selected);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Release_remains_smooth_until_zero_angle_and_exit_restores_original_sampling(bool mirror) => CheekProductTests.Sta(() =>
    {
        var (presenter, pet, direct) = Setup(mirror);
        var image = (Image)presenter.FindName("DororongImage");
        var originalMode = RenderOptions.GetBitmapScalingMode(image);
        presenter.Render(pet, direct with { HeadSwingDegrees = 14 }, TimeSpan.Zero);
        var release = direct with { Phase = DirectInteractionPhase.BodyDragSettle, HeadSwingDegrees = 14, ReleaseProgress = 0 };
        presenter.Render(pet, release, TimeSpan.Zero);
        presenter.Render(pet, release with { ReleaseProgress = .5 }, TimeSpan.FromMilliseconds(100));
        Assert.NotEqual(0, ((RotateTransform)presenter.FindName("BodyRotateTransform")).Angle);
        Assert.Equal(BitmapScalingMode.Linear, RenderOptions.GetBitmapScalingMode(image));
        presenter.Render(pet, release with { ReleaseProgress = 1 }, TimeSpan.FromMilliseconds(100));
        Assert.Equal(0, ((RotateTransform)presenter.FindName("BodyRotateTransform")).Angle);
        Assert.Equal(BitmapScalingMode.NearestNeighbor, RenderOptions.GetBitmapScalingMode(image));
        presenter.Render(pet, DirectInteractionSnapshot.None);
        Assert.Equal(originalMode, RenderOptions.GetBitmapScalingMode(image));
    });

    [Fact]
    public void Upright_hold_preserves_old_raster_and_cancel_does_not_leak_smoothing() => CheekProductTests.Sta(() =>
    {
        var (presenter, pet, direct) = Setup(false);
        var image = (Image)presenter.FindName("DororongImage");
        var originalMode = RenderOptions.GetBitmapScalingMode(image);
        presenter.Render(pet, direct, TimeSpan.Zero); CheekProductTests.Layout(presenter);
        var upright = Raster(presenter);
        Assert.Equal(BitmapScalingMode.NearestNeighbor, RenderOptions.GetBitmapScalingMode(image));
        presenter.Render(pet, direct with { HeadSwingDegrees = 14 }, TimeSpan.Zero);
        presenter.Render(pet, direct, TimeSpan.Zero); CheekProductTests.Layout(presenter);
        Assert.Equal(upright, Raster(presenter));
        presenter.Render(pet, direct with { HeadSwingDegrees = -14 }, TimeSpan.Zero);
        presenter.Render(pet, DirectInteractionSnapshot.None);
        Assert.Equal(originalMode, RenderOptions.GetBitmapScalingMode(image));
    });

    internal static (DororongPresenter Presenter, PetSnapshot Pet, DirectInteractionSnapshot Direct) Setup(bool mirror)
    {
        var presenter = new DororongPresenter();
        var pet = new PetSnapshot(PetState.Walk, new(100, 100), mirror ? FacingDirection.Left : FacingDirection.Right, 0, false, null);
        presenter.Render(pet, DirectInteractionSnapshot.None); CheekProductTests.Layout(presenter);
        var image = (Image)presenter.FindName("DororongImage");
        var grab = image.TranslatePoint(new Point(37.5, 28.25), presenter);
        var press = pet.Position + new PointD(grab.X, grab.Y);
        var direct = new DirectInteractionSnapshot(DirectInteractionTarget.Body, DirectInteractionPhase.BodyDragHold, press, press, 1, 0, true);
        return (presenter, pet, direct);
    }

    internal static byte[] Raster(DororongPresenter presenter)
    {
        var bitmap = Snapshot(presenter); var pixels = new byte[144 * 144 * 4];
        bitmap.CopyPixels(pixels, 576, 0); return pixels;
    }

    internal static BitmapSource Snapshot(DororongPresenter presenter)
    {
        var bitmap = new RenderTargetBitmap(144, 144, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(presenter); bitmap.Freeze(); return bitmap;
    }
}
