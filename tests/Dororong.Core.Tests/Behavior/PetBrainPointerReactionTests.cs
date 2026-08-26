using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Tests.TestSupport;

namespace Dororong.Core.Tests.Behavior;

public sealed class PetBrainPointerReactionTests
{
    [Fact]
    public void Slow_new_entry_into_the_near_zone_enters_curious()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));

        var actual = brain.Update(PetTestInput.At(1.0, pointer: new PointD(300, 150)));

        Assert.Equal(PetState.Curious, actual.State);
    }

    [Fact]
    public void Fast_closing_motion_inside_the_reaction_zone_enters_startled()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));

        var actual = brain.Update(PetTestInput.At(0.1, pointer: new PointD(260, 150)));

        Assert.Equal(PetState.Startled, actual.State);
        Assert.True(actual.Position.X < 100);
    }

    [Fact]
    public void Fast_motion_away_from_the_pet_does_not_startle()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(360, 150)));

        var actual = brain.Update(PetTestInput.At(0.1, pointer: new PointD(370, 150)));

        Assert.NotEqual(PetState.Startled, actual.State);
    }

    [Fact]
    public void A_cursor_that_remains_near_does_not_retrigger_curious()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(1.0, pointer: new PointD(300, 150)));

        for (var index = 0; index < 30; index++)
        {
            brain.Update(PetTestInput.At(0.1, pointer: new PointD(300, 150)));
        }

        Assert.Equal(PetState.Idle, brain.Current.State);
    }

    [Fact]
    public void Curious_cannot_interrupt_an_active_startled_reaction()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(260, 150)));
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));

        var actual = brain.Update(PetTestInput.At(0.1, pointer: new PointD(280, 150)));

        Assert.Equal(PetState.Startled, actual.State);
    }

    [Fact]
    public void Pending_body_press_suppresses_a_simultaneous_fast_approach()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));

        var actual = brain.Update(PetTestInput.At(
            0.1,
            pointer: new PointD(260, 150),
            bodyPressPosition: new PointD(160, 150)));

        Assert.Equal(PetState.Idle, actual.State);
    }

    [Fact]
    public void A_valid_sample_after_an_unavailable_sample_does_not_use_stale_speed()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(0.1));

        var actual = brain.Update(PetTestInput.At(0.1, pointer: new PointD(260, 150)));

        Assert.NotEqual(PetState.Startled, actual.State);
    }

    [Fact]
    public void A_stationary_cursor_near_the_pet_stays_idle_after_curious_cooldown_expires()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(1.0, pointer: new PointD(300, 150)));

        for (var index = 0; index < 50; index++)
        {
            brain.Update(PetTestInput.At(0.1, pointer: new PointD(300, 150)));
        }

        Assert.Equal(PetState.Idle, brain.Current.State);
    }

    [Fact]
    public void Leaving_the_near_zone_and_reentering_after_cooldown_enters_curious_again()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(1.0, pointer: new PointD(300, 150)));

        for (var index = 0; index < 50; index++)
        {
            brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        }

        var actual = brain.Update(PetTestInput.At(1.0, pointer: new PointD(300, 150)));

        Assert.Equal(PetState.Curious, actual.State);
    }

    [Fact]
    public void Reentering_the_near_zone_before_curious_cooldown_expires_stays_idle()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(300, 150)));
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(1.5, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(0.5, pointer: new PointD(460, 150)));

        var actual = brain.Update(PetTestInput.At(0.1, pointer: new PointD(300, 150)));

        Assert.Equal(PetState.Idle, actual.State);
    }

    [Fact]
    public void Startled_cooldown_allows_curious_but_suppresses_another_startled_reaction()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(260, 150)));
        brain.Update(PetTestInput.At(0.65, pointer: new PointD(260, 150)));
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(500, 150)));

        var curious = brain.Update(PetTestInput.At(1.0, pointer: new PointD(228, 150)));

        brain.Update(PetTestInput.At(0.1, pointer: new PointD(500, 150)));
        var actual = brain.Update(PetTestInput.At(0.05, pointer: new PointD(188, 150)));

        Assert.Equal(PetState.Curious, curious.State);
        Assert.Equal(PetState.Curious, actual.State);
        Assert.Equal(28, actual.Position.X, 8);
    }

    [Fact]
    public void Near_enter_and_exit_boundaries_allow_a_new_curious_reaction()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(470, 150)));

        var firstEntry = brain.Update(PetTestInput.At(1.0, pointer: new PointD(310, 150)));

        for (var index = 0; index < 40; index++)
        {
            brain.Update(PetTestInput.At(0.1, pointer: new PointD(370, 150)));
        }

        var reentry = brain.Update(PetTestInput.At(0.1, pointer: new PointD(310, 150)));

        Assert.Equal(PetState.Curious, firstEntry.State);
        Assert.Equal(PetState.Curious, reentry.State);
    }

    [Fact]
    public void Startle_reaction_distance_and_filtered_speed_boundaries_are_inclusive()
    {
        var tuning = BehaviorTuning.Default with
        {
            MaxDelta = TimeSpan.FromSeconds(1),
            IdleMin = TimeSpan.FromMinutes(11),
            IdleMax = TimeSpan.FromMinutes(11),
            StartledDuration = TimeSpan.FromSeconds(2),
            StartleReactionDistance = 220,
            StartleClosingSpeed = 350
        };
        var brain = new PetBrain(tuning, new SequenceRandomSource(0), new PointD(100, 100));
        brain.Update(PetTestInput.At(1.0, pointer: new PointD(1380, 150)));

        var actual = brain.Update(PetTestInput.At(1.0, pointer: new PointD(380, 150)));

        Assert.Equal(PetState.Startled, actual.State);
    }

    [Fact]
    public void Startled_retreats_the_configured_distance_away_from_the_pointer()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 450)));
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(260, 50)));

        var actual = brain.Update(PetTestInput.At(0.65, pointer: new PointD(260, 50)));
        var retreatComponent = 72 / Math.Sqrt(2);

        Assert.Equal(100 - retreatComponent, actual.Position.X, 8);
        Assert.Equal(100 + retreatComponent, actual.Position.Y, 8);
    }

    [Fact]
    public void Startled_retreat_is_clamped_to_the_work_area()
    {
        var brain = PetTestInput.CreateReactionBrainAt(new PointD(0, 100));
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(360, 150)));
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(160, 150)));

        var actual = brain.Update(PetTestInput.At(0.65, pointer: new PointD(160, 150)));

        Assert.Equal(0, actual.Position.X);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    public void Constructor_rejects_nonpositive_curious_duration(double seconds)
    {
        var tuning = BehaviorTuning.Default with { CuriousDuration = TimeSpan.FromSeconds(seconds) };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PetBrain(tuning, new SequenceRandomSource(0), new PointD(100, 100)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    public void Constructor_rejects_nonpositive_startled_duration(double seconds)
    {
        var tuning = BehaviorTuning.Default with { StartledDuration = TimeSpan.FromSeconds(seconds) };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PetBrain(tuning, new SequenceRandomSource(0), new PointD(100, 100)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    public void Constructor_rejects_nonpositive_click_reaction_duration(double seconds)
    {
        var tuning = BehaviorTuning.Default with { ClickReactionDuration = TimeSpan.FromSeconds(seconds) };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PetBrain(tuning, new SequenceRandomSource(0), new PointD(100, 100)));
    }
}
