using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Tests.TestSupport;

namespace Dororong.Core.Tests.Behavior;

public sealed class PetBrainAutonomyTests
{
    [Fact]
    public void Update_enters_walk_only_after_the_idle_duration_finishes()
    {
        var brain = PetTestInput.CreateBrain(
            new SequenceRandomSource(0.0, 0.0, 0.0),
            new PointD(100, 100));

        Assert.Equal(PetState.Idle, brain.Update(PetTestInput.At(0.9)).State);
        Assert.Equal(PetState.Walk, brain.Update(PetTestInput.At(0.1)).State);
    }

    [Fact]
    public void Update_moves_by_speed_times_elapsed_time_while_walking()
    {
        var brain = PetTestInput.CreateBrain(
            new SequenceRandomSource(0.0, 0.0, 0.0),
            new PointD(100, 100));
        brain.Update(PetTestInput.At(1.0));

        var actual = brain.Update(PetTestInput.At(0.5));

        Assert.Equal(PetState.Walk, actual.State);
        Assert.Equal(121, actual.Position.X, precision: 6);
        Assert.Equal(100, actual.Position.Y, precision: 6);
    }

    [Fact]
    public void Update_clamps_a_walk_at_the_right_work_area_edge()
    {
        var brain = PetTestInput.CreateBrain(
            new SequenceRandomSource(0.0, 0.0, 0.0),
            new PointD(675, 100));
        brain.Update(PetTestInput.At(1.0));

        var actual = brain.Update(PetTestInput.At(0.5));

        Assert.Equal(680, actual.Position.X, precision: 6);
        Assert.Equal(FacingDirection.Left, actual.Facing);
    }

    [Fact]
    public void Update_clamps_an_oversized_frame_to_the_configured_max_delta()
    {
        var tuning = BehaviorTuning.Default with
        {
            MaxDelta = TimeSpan.FromSeconds(0.1),
            IdleMin = TimeSpan.FromSeconds(0.1),
            IdleMax = TimeSpan.FromSeconds(0.1),
            WalkMin = TimeSpan.FromSeconds(2),
            WalkMax = TimeSpan.FromSeconds(2),
            WalkSpeed = 42,
            IdleToWalkProbability = 1
        };
        var brain = new PetBrain(tuning, new SequenceRandomSource(0.0, 0.0), new PointD(100, 100));
        brain.Update(PetTestInput.At(0.1));

        var actual = brain.Update(PetTestInput.At(1.0));

        Assert.Equal(104.2, actual.Position.X, precision: 6);
    }

    [Fact]
    public void Update_ignores_a_negative_delta()
    {
        var brain = PetTestInput.CreateBrain(
            new SequenceRandomSource(0.0, 0.0, 0.0),
            new PointD(100, 100));
        var before = brain.Current;

        var actual = brain.Update(PetTestInput.At(-1.0));

        Assert.Equal(before, actual);
    }

    [Fact]
    public void Update_matches_split_frames_when_an_idle_to_walk_transition_occurs_mid_frame()
    {
        var tuning = BehaviorTuning.Default with
        {
            MaxDelta = TimeSpan.FromSeconds(2),
            IdleMin = TimeSpan.FromSeconds(0.5),
            IdleMax = TimeSpan.FromSeconds(0.5),
            WalkMin = TimeSpan.FromSeconds(1),
            WalkMax = TimeSpan.FromSeconds(1),
            WalkSpeed = 42,
            IdleToWalkProbability = 1
        };
        var wholeFrame = new PetBrain(tuning, new SequenceRandomSource(0.0, 0.0), new PointD(100, 100));
        var splitFrames = new PetBrain(tuning, new SequenceRandomSource(0.0, 0.0), new PointD(100, 100));

        var whole = wholeFrame.Update(PetTestInput.At(1.5));
        splitFrames.Update(PetTestInput.At(0.5));
        var split = splitFrames.Update(PetTestInput.At(1.0));

        Assert.Equal(new PointD(142, 100), whole.Position);
        Assert.Equal(whole, split);
    }

    [Fact]
    public void Update_normalizes_idle_position_when_the_work_area_shrinks()
    {
        var brain = PetTestInput.CreateBrain(
            new SequenceRandomSource(0.0, 0.0, 0.0),
            new PointD(675, 550));

        var actual = brain.Update(PetTestInput.At(0.1, workArea: new RectD(0, 0, 400, 300)));

        Assert.Equal(PetState.Idle, actual.State);
        Assert.Equal(new PointD(280, 200), actual.Position);
    }

    [Fact]
    public void PetBrain_rejects_zero_autonomous_durations_before_an_update_can_loop()
    {
        var tuning = BehaviorTuning.Default with
        {
            IdleMin = TimeSpan.Zero,
            IdleMax = TimeSpan.Zero
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PetBrain(tuning, new SequenceRandomSource(0.0), new PointD(100, 100)));
    }

    [Fact]
    public void Update_starts_a_fresh_idle_interval_when_the_walk_probability_fails()
    {
        var tuning = BehaviorTuning.Default with
        {
            MaxDelta = TimeSpan.FromSeconds(1),
            IdleMin = TimeSpan.FromSeconds(1),
            IdleMax = TimeSpan.FromSeconds(2),
            IdleToWalkProbability = 0.5
        };
        var brain = new PetBrain(tuning, new SequenceRandomSource(0.0, 0.9, 0.5), new PointD(100, 100));

        brain.Update(PetTestInput.At(1.0));
        var actual = brain.Update(PetTestInput.At(1.0));

        Assert.Equal(PetState.Idle, actual.State);
    }

    [Fact]
    public void Update_reflects_walk_facing_at_the_left_work_area_edge()
    {
        var brain = PetTestInput.CreateBrain(
            new SequenceRandomSource(0.0, 0.5, 0.0),
            new PointD(5, 100));
        brain.Update(PetTestInput.At(1.0));

        var actual = brain.Update(PetTestInput.At(0.5));

        Assert.Equal(new PointD(0, 100), actual.Position);
        Assert.Equal(FacingDirection.Right, actual.Facing);
    }

    [Fact]
    public void Update_keeps_phase_below_one_at_an_exact_walk_gait_cycle()
    {
        var tuning = BehaviorTuning.Default with
        {
            MaxDelta = TimeSpan.FromSeconds(1),
            IdleMin = TimeSpan.FromSeconds(0.1),
            IdleMax = TimeSpan.FromSeconds(0.1),
            WalkMin = TimeSpan.FromSeconds(2),
            WalkMax = TimeSpan.FromSeconds(2),
            IdleToWalkProbability = 1
        };
        var brain = new PetBrain(tuning, new SequenceRandomSource(0.0, 0.0), new PointD(100, 100));
        brain.Update(PetTestInput.At(0.1));

        var actual = brain.Update(PetTestInput.At(0.6));

        Assert.Equal(PetState.Walk, actual.State);
        Assert.InRange(actual.Phase, 0, 1);
        Assert.True(actual.Phase < 1);
    }
}
