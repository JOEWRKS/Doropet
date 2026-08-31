using System.Runtime.ExceptionServices;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class DororongPresenterInteractionDescriptorTests
{
    private static readonly DirectInteractionSnapshot BodyPending = new(
        DirectInteractionTarget.Body,
        DirectInteractionPhase.BodyPending,
        new PointD(48, 70),
        new PointD(48, 70),
        0,
        0,
        true);

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

    [Fact]
    public void Settled_sleep_body_pending_continues_wake_without_restarting_on_click_release()
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            var image = Assert.IsAssignableFrom<System.Windows.Controls.Image>(presenter.FindName("DororongImage"));
            var translation = Assert.IsType<System.Windows.Media.TranslateTransform>(presenter.FindName("BodyTranslateTransform"));

            presenter.Render(Snapshot(PetState.Sleep, FacingDirection.Right, phase: 0.2), DirectInteractionSnapshot.None);
            AssertFrame(image, "dororong-sleep.png");

            for (var tick = 0; tick < 4; tick++)
            {
                presenter.Render(Snapshot(PetState.Idle, FacingDirection.Right, phase: 0), BodyPending);
            }

            AssertFrame(image, "dororong-sleep-crouch-squint.png");
            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Right, phase: 0), DirectInteractionSnapshot.None);
            AssertFrame(image, "dororong-sleep-crouch-squint.png");

            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Right, phase: 0.052), DirectInteractionSnapshot.None);
            AssertFrame(image, "dororong-canonical.png");
            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Right, phase: 0.31), DirectInteractionSnapshot.None);
            AssertFrame(image, "dororong-canonical.png");
            Assert.Equal(-10.5, translation.Y, precision: 4);
        });
    }

    [Fact]
    public void Settled_sleep_immediate_click_uses_the_same_forward_wake_order_before_the_hop()
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            var image = Assert.IsAssignableFrom<System.Windows.Controls.Image>(presenter.FindName("DororongImage"));

            presenter.Render(Snapshot(PetState.Sleep, FacingDirection.Right, phase: 0.2), DirectInteractionSnapshot.None);
            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Right, phase: 0), DirectInteractionSnapshot.None);
            AssertFrame(image, "dororong-sleep-tuck-squint.png");
            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Right, phase: 0.1), DirectInteractionSnapshot.None);
            AssertFrame(image, "dororong-sleep-crouch-squint.png");
            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Right, phase: 0.18), DirectInteractionSnapshot.None);
            AssertFrame(image, "dororong-canonical.png");
            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Right, phase: 0.31), DirectInteractionSnapshot.None);
            AssertFrame(image, "dororong-canonical.png");
        });
    }

    [Fact]
    public void Awake_body_click_keeps_the_canonical_expression_through_the_entire_hop()
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            var image = Assert.IsAssignableFrom<System.Windows.Controls.Image>(presenter.FindName("DororongImage"));

            presenter.Render(Snapshot(PetState.Idle, FacingDirection.Right, phase: 0), DirectInteractionSnapshot.None);
            foreach (var phase in new[] { 0d, 0.08, 0.16, 0.31, 0.46, 0.56, 0.75, 0.84, 0.92, 1d })
            {
                presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Right, phase), DirectInteractionSnapshot.None);
                AssertFrame(image, "dororong-canonical.png");
            }
        });
    }

    [Fact]
    public void Nonwalking_left_logical_facing_stays_visibly_unmirrored_through_body_click_rest()
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            var scale = Assert.IsType<System.Windows.Media.ScaleTransform>(presenter.FindName("BodyScaleTransform"));

            presenter.Render(Snapshot(PetState.Idle, FacingDirection.Left, phase: 0), DirectInteractionSnapshot.None);
            Assert.Equal(1, scale.ScaleX);
            presenter.Render(Snapshot(PetState.Idle, FacingDirection.Left, phase: 0), BodyPending);
            Assert.Equal(1, scale.ScaleX);
            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Left, phase: 0.31), DirectInteractionSnapshot.None);
            Assert.Equal(1, scale.ScaleX);
            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Left, phase: 1), DirectInteractionSnapshot.None);
            Assert.Equal(1, scale.ScaleX);
        });
    }

    [Fact]
    public void Mirrored_left_walk_stays_visibly_mirrored_through_body_click_rest()
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            var scale = Assert.IsType<System.Windows.Media.ScaleTransform>(presenter.FindName("BodyScaleTransform"));

            presenter.Render(Snapshot(PetState.Walk, FacingDirection.Left, phase: 0.1), DirectInteractionSnapshot.None);
            Assert.Equal(-1, scale.ScaleX);
            presenter.Render(Snapshot(PetState.Walk, FacingDirection.Left, phase: 0.1), BodyPending);
            Assert.Equal(-1, scale.ScaleX);
            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Left, phase: 0.31), DirectInteractionSnapshot.None);
            Assert.Equal(-1, scale.ScaleX);
            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Left, phase: 1), DirectInteractionSnapshot.None);
            Assert.Equal(-1, scale.ScaleX);
            presenter.Render(Snapshot(PetState.Idle, FacingDirection.Left, phase: 0), DirectInteractionSnapshot.None);
            Assert.Equal(-1, scale.ScaleX);
        });
    }

    [Fact]
    public void Pending_cancel_releases_facing_lock_before_opposite_direction_new_pending()
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            var scale = Assert.IsType<System.Windows.Media.ScaleTransform>(presenter.FindName("BodyScaleTransform"));

            presenter.Render(Snapshot(PetState.Walk, FacingDirection.Left, phase: 0.1), DirectInteractionSnapshot.None);
            presenter.Render(Snapshot(PetState.Walk, FacingDirection.Left, phase: 0.1), BodyPending);
            Assert.Equal(-1, scale.ScaleX);

            presenter.Render(Snapshot(PetState.Walk, FacingDirection.Right, phase: 0.1), DirectInteractionSnapshot.None);
            Assert.Equal(1, scale.ScaleX);
            presenter.Render(Snapshot(PetState.Walk, FacingDirection.Right, phase: 0.1), BodyPending);
            Assert.Equal(1, scale.ScaleX);
        });
    }

    [Fact]
    public void Partial_sleep_wake_cancel_does_not_leak_into_a_fresh_sleep_click()
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            var image = Assert.IsAssignableFrom<System.Windows.Controls.Image>(presenter.FindName("DororongImage"));

            presenter.Render(Snapshot(PetState.Sleep, FacingDirection.Right, phase: 0.2), DirectInteractionSnapshot.None);
            presenter.Render(Snapshot(PetState.Idle, FacingDirection.Right, phase: 0), BodyPending);
            presenter.Render(Snapshot(PetState.Idle, FacingDirection.Right, phase: 0), BodyPending);
            AssertFrame(image, "dororong-sleep-tuck-squint.png");

            presenter.Render(Snapshot(PetState.Idle, FacingDirection.Right, phase: 0), DirectInteractionSnapshot.None);
            presenter.Render(Snapshot(PetState.Sleep, FacingDirection.Right, phase: 0.2), DirectInteractionSnapshot.None);
            AssertFrame(image, "dororong-sleep.png");
            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Right, phase: 0), DirectInteractionSnapshot.None);
            AssertFrame(image, "dororong-sleep-tuck-squint.png");
            presenter.Render(Snapshot(PetState.ClickReaction, FacingDirection.Right, phase: 0.1), DirectInteractionSnapshot.None);
            AssertFrame(image, "dororong-sleep-crouch-squint.png");
        });
    }

    private static void AssertFrame(System.Windows.Controls.Image image, string fileName) =>
        Assert.EndsWith(fileName, image.Source.ToString(), StringComparison.OrdinalIgnoreCase);

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
