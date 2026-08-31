using System.Runtime.ExceptionServices;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class DororongPresenterInteractionDescriptorTests
{
    [Fact]
    public void Settled_sleep_visible_cheek_point_classifies_the_intended_cheek()
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            presenter.Render(Snapshot(PetState.Sleep, FacingDirection.Right, phase: 0.2), DirectInteractionSnapshot.None);

            var actual = presenter.ClassifyOpaqueSourcePoint(new PointD(43, 64), opaque: true);

            Assert.Equal(DirectInteractionTarget.LeftCheek, actual);
        });
    }

    [Fact]
    public void Unmirrored_idle_with_retained_left_logical_facing_stays_unmirrored_for_classification()
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            presenter.Render(Snapshot(PetState.Idle, FacingDirection.Left, phase: 0.1), DirectInteractionSnapshot.None);

            var actual = presenter.ClassifyOpaqueSourcePoint(new PointD(56, 56), opaque: true);

            Assert.Equal(DirectInteractionTarget.LeftCheek, actual);
        });
    }

    [Fact]
    public void Physically_mirrored_walk_swaps_screen_side_while_preserving_anatomical_target()
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            presenter.Render(Snapshot(PetState.Walk, FacingDirection.Left, phase: 0.1), DirectInteractionSnapshot.None);

            var actual = presenter.ClassifyOpaqueSourcePoint(new PointD(39, 56), opaque: true);

            Assert.Equal(DirectInteractionTarget.RightCheek, actual);
        });
    }

    [Fact]
    public void Sleep_entry_crossfade_interpolates_the_visible_cheek_region()
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            presenter.Render(Snapshot(PetState.Sleep, FacingDirection.Right, phase: 0.0575), DirectInteractionSnapshot.None);

            var actual = presenter.ClassifyOpaqueSourcePoint(new PointD(20, 52), opaque: true);

            Assert.Equal(DirectInteractionTarget.RightCheek, actual);
        });
    }

    private static PetSnapshot Snapshot(PetState state, FacingDirection facing, double phase) =>
        new(state, new PointD(100, 100), facing, phase, false, null);

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
