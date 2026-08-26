using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Tests.TestSupport;

namespace Dororong.Core.Tests.Behavior;

public sealed class PetBrainSleepTests
{
    [Fact]
    public void Inactivity_enters_sleep_only_after_the_sleep_delay()
    {
        var brain = PetTestInput.CreateSleepBrain();

        for (var index = 0; index < 9; index++)
        {
            brain.Update(PetTestInput.At(0.1));
        }

        Assert.Equal(PetState.Idle, brain.Current.State);

        brain.Update(PetTestInput.At(0.1));

        Assert.Equal(PetState.Sleep, brain.Current.State);
    }

    [Fact]
    public void Click_without_drag_wakes_sleep_into_click_reaction()
    {
        var brain = PetTestInput.CreateSleepingBrain();
        Assert.Equal(PetState.Sleep, brain.Current.State);

        brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(160, 150), primaryDown: true,
            bodyPressPosition: new PointD(160, 150)));

        var actual = brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(160, 150), primaryDown: false));

        Assert.Equal(PetState.ClickReaction, actual.State);
    }

    [Theory]
    [InlineData(false, PetState.Curious)]
    [InlineData(true, PetState.Startled)]
    public void New_pointer_approach_wakes_sleep_into_the_matching_reaction(
        bool fast,
        PetState expected)
    {
        var brain = PetTestInput.CreateSleepingBrain();
        Assert.Equal(PetState.Sleep, brain.Current.State);

        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));

        var actual = brain.Update(PetTestInput.At(
            fast ? 0.1 : 1.0,
            pointer: fast ? new PointD(260, 150) : new PointD(300, 150)));

        Assert.Equal(expected, actual.State);
    }

    [Fact]
    public void Crossing_the_drag_threshold_wakes_sleep_into_dragged()
    {
        var brain = PetTestInput.CreateSleepingBrain();
        Assert.Equal(PetState.Sleep, brain.Current.State);

        brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(160, 150), primaryDown: true,
            bodyPressPosition: new PointD(160, 150)));

        var actual = brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(165, 150), primaryDown: true));

        Assert.Equal(PetState.Dragged, actual.State);
        Assert.Equal(new PointD(60, 50), actual.GrabOffset);
    }

    [Theory]
    [InlineData(WakeRoute.SlowApproach)]
    [InlineData(WakeRoute.FastApproach)]
    [InlineData(WakeRoute.Click)]
    [InlineData(WakeRoute.Drag)]
    public void Every_wake_route_resets_stale_inactivity(WakeRoute route)
    {
        var brain = PetTestInput.CreateSleepingBrain(TimeSpan.FromSeconds(5));
        Assert.Equal(PetState.Sleep, brain.Current.State);

        WakeAndFinishReaction(brain, route);

        Assert.Equal(PetState.Idle, brain.Current.State);
    }

    [Fact]
    public void Reentering_the_near_zone_during_cooldown_resets_inactivity()
    {
        var brain = PetTestInput.CreateSleepBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(300, 150)));

        for (var index = 0; index < 11; index++)
        {
            brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        }

        brain.Update(PetTestInput.At(0.1, pointer: new PointD(300, 150)));
        for (var index = 0; index < 3; index++)
        {
            brain.Update(PetTestInput.At(0.1, pointer: new PointD(300, 150)));
        }

        Assert.Equal(PetState.Idle, brain.Current.State);
    }

    [Fact]
    public void A_stationary_near_cursor_does_not_repeatedly_reset_inactivity()
    {
        var brain = PetTestInput.CreateSleepBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(300, 150)));

        for (var index = 0; index < 16; index++)
        {
            brain.Update(PetTestInput.At(0.1, pointer: new PointD(300, 150)));
        }

        Assert.Equal(PetState.Sleep, brain.Current.State);
    }

    private static void WakeAndFinishReaction(PetBrain brain, WakeRoute route)
    {
        switch (route)
        {
            case WakeRoute.SlowApproach:
                brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
                brain.Update(PetTestInput.At(1.0, pointer: new PointD(300, 150)));
                UpdateRepeatedly(brain, 15, pointer: new PointD(300, 150));
                break;
            case WakeRoute.FastApproach:
                brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
                brain.Update(PetTestInput.At(0.1, pointer: new PointD(260, 150)));
                UpdateRepeatedly(brain, 7, pointer: new PointD(260, 150));
                break;
            case WakeRoute.Click:
                brain.Update(PetTestInput.At(
                    0.1, pointer: new PointD(160, 150), primaryDown: true,
                    bodyPressPosition: new PointD(160, 150)));
                brain.Update(PetTestInput.At(
                    0.1, pointer: new PointD(160, 150), primaryDown: false));
                UpdateRepeatedly(brain, 4, pointer: new PointD(160, 150));
                break;
            case WakeRoute.Drag:
                brain.Update(PetTestInput.At(
                    0.1, pointer: new PointD(160, 150), primaryDown: true,
                    bodyPressPosition: new PointD(160, 150)));
                brain.Update(PetTestInput.At(
                    0.1, pointer: new PointD(165, 150), primaryDown: true));
                brain.Update(PetTestInput.At(
                    0.1, pointer: new PointD(165, 150), primaryDown: false));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(route));
        }
    }

    private static void UpdateRepeatedly(PetBrain brain, int count, PointD pointer)
    {
        for (var index = 0; index < count; index++)
        {
            brain.Update(PetTestInput.At(0.1, pointer: pointer));
        }
    }

    public enum WakeRoute
    {
        SlowApproach,
        FastApproach,
        Click,
        Drag
    }
}
