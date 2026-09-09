using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.App.Interaction;
using System.Windows.Media;

namespace Dororong.App.Tests.Controls;

public class RecoveryAnchorContinuityTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Actual_presenter_partial_recovery_translation_has_no_single_pixel_snap(bool mirror) => CheekProductTests.Sta(() =>
    {
        var (presenter, pet, direct) = HeadTiltSamplingTests.Setup(mirror);
        direct = direct with { Phase=DirectInteractionPhase.BodyDragEntry, Strength=.4 };
        presenter.Render(pet, direct, TimeSpan.Zero);
        CheekProductTests.Layout(presenter);
        var transform = (TranslateTransform)presenter.FindName("BodyTranslateTransform");
        var previous = new PointD(transform.X, transform.Y);
        var release = direct with { Phase=DirectInteractionPhase.BodyDragSettle, IsPartialDragSettle=true, RequiresCapture=false };
        for(var i=0; i<=360; i++)
        {
            presenter.Render(pet with {State=PetState.Idle}, release with {ReleaseProgress=i/360d}, TimeSpan.Zero);
            CheekProductTests.Layout(presenter);
            var current = new PointD(transform.X,transform.Y);
            Assert.InRange(Math.Max(Math.Abs(current.X-previous.X),Math.Abs(current.Y-previous.Y)),0,.25);
            previous = current;
        }
        presenter.Render(pet with {State=PetState.Idle}, DirectInteractionSnapshot.None, TimeSpan.Zero);
        Assert.Equal(0,transform.X,6); Assert.Equal(0,transform.Y,6);
    });

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 14)] [InlineData(true, -14)]
    [InlineData(false, 0, 8)] [InlineData(true, 0, 8)]
    [InlineData(false, 0, 24)] [InlineData(true, 0, 24)]
    [InlineData(false, 0, 40)] [InlineData(true, 0, 40)]
    [InlineData(false, 0, 56)] [InlineData(true, 0, 56)]
    [InlineData(false, 0, 72)] [InlineData(true, 0, 72)]
    [InlineData(false, 0, 88)] [InlineData(true, 0, 88)]
    [InlineData(false, 0, 104)] [InlineData(true, 0, 104)]
    public void Button_up_without_elapsed_time_preserves_the_composed_partial_pose(bool mirror, double angle, int frameIndex = -1) => CheekProductTests.Sta(() =>
    {
        var (presenter, pet, direct) = HeadTiltSamplingTests.Setup(mirror);
        direct = direct with { Phase=DirectInteractionPhase.BodyDragEntry, Strength=frameIndex<0?.4:frameIndex/112d, HeadSwingDegrees=angle };
        presenter.Render(pet, direct, TimeSpan.Zero);
        CheekProductTests.Layout(presenter);
        var held = HeadTiltSamplingTests.Raster(presenter);
        presenter.Render(pet with { State=PetState.Idle }, direct with
        {
            Phase=DirectInteractionPhase.BodyDragSettle, IsPartialDragSettle=true,
            RequiresCapture=false, ReleaseProgress=0
        }, TimeSpan.Zero);
        CheekProductTests.Layout(presenter);
        var released = HeadTiltSamplingTests.Raster(presenter);
        var changed = held.Zip(released).Count(pair => pair.First != pair.Second);
        Assert.True(changed==0, $"Button-up changed {changed} composed pixel channels without any motion time");
    });

    // Re-measuring and rounding the pink centroid of every morphed bitmap
    // makes a continuous recovery jump a whole source pixel under the cursor.
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void Intermediate_release_anchor_does_not_jump_on_subframe_progress(int key) => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var rest = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var supplied = new SuppliedBodyDragFrames(PremultipliedFrame.From(rest));
        var recovery = new HeadRecoveryFrame(supplied.RecoveryFrom(supplied.Sample((key-1)/7d), rest), key);
        var capture = new CapturedHeadAnchor(default, default, FacingDirection.Right);
        var previous = HeadPullAnchoring.SourcePoint(recovery.Sample(0), capture);
        for (var i=1; i<=360; i++)
        {
            var point = HeadPullAnchoring.SourcePoint(recovery.Sample(i/360d), capture);
            var jump = Math.Max(Math.Abs(point.X-previous.X), Math.Abs(point.Y-previous.Y));
            Assert.True(jump < .25, $"Key {key}, progress {i}/360: anchor jumped {jump} source pixels");
            previous = point;
        }
    });
}
