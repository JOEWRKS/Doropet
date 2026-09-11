using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Tests.TestSupport;

namespace Dororong.Core.Tests.Behavior;

public sealed class PetBrainAutonomyTests
{
    [Theory]
    [InlineData(0.0, 21.0, FacingDirection.Right)]
    [InlineData(0.5, -21.0, FacingDirection.Left)]
    public void Default_walking_moves_twenty_one_dips_per_second_in_either_direction(
        double heading, double expectedDisplacement, FacingDirection expectedFacing)
    {
        var brain = new PetBrain(
            BehaviorTuning.Default,
            new SequenceRandomSource(0.0, 0.0, heading, 0.0),
            new PointD(300, 100));
        for (var frame = 0; frame < 20; frame++)
            brain.Update(PetTestInput.At(0.1));
        Assert.Equal(PetState.Walk, brain.Current.State);
        var start = brain.Current.Position;

        for (var frame = 0; frame < 10; frame++)
            brain.Update(PetTestInput.At(0.1));

        Assert.Equal(PetState.Walk, brain.Current.State);
        Assert.Equal(expectedDisplacement, brain.Current.Position.X - start.X, precision: 6);
        Assert.Equal(start.Y, brain.Current.Position.Y, precision: 6);
        Assert.Equal(expectedFacing, brain.Current.Facing);
    }

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
    public void Update_reflects_walk_overshoot_at_the_right_work_area_edge()
    {
        var brain = PetTestInput.CreateBrain(
            new SequenceRandomSource(0.0, 0.0, 0.0),
            new PointD(675, 100));
        brain.Update(PetTestInput.At(1.0));

        var actual = brain.Update(PetTestInput.At(0.5));

        Assert.Equal(664, actual.Position.X, precision: 6);
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

        Assert.Equal(new PointD(16, 100), actual.Position);
        Assert.Equal(FacingDirection.Right, actual.Facing);
    }

    [Fact]
    public void Update_moves_inward_after_reflecting_at_the_top_work_area_edge()
    {
        var brain = PetTestInput.CreateBrain(
            new SequenceRandomSource(0.0, 0.75, 0.0),
            new PointD(100, 5));
        brain.Update(PetTestInput.At(1.0));

        var clamped = brain.Update(PetTestInput.At(0.5));
        var inward = brain.Update(PetTestInput.At(0.25));

        Assert.Equal(16, clamped.Position.Y, precision: 6);
        Assert.Equal(26.5, inward.Position.Y, precision: 6);
        Assert.Equal(PetState.Walk, inward.State);
    }

    [Fact]
    public void Update_moves_inward_after_reflecting_at_the_bottom_work_area_edge()
    {
        var brain = PetTestInput.CreateBrain(
            new SequenceRandomSource(0.0, 0.25, 0.0),
            new PointD(100, 495));
        brain.Update(PetTestInput.At(1.0));

        var clamped = brain.Update(PetTestInput.At(0.5));
        var inward = brain.Update(PetTestInput.At(0.25));

        Assert.Equal(484, clamped.Position.Y, precision: 6);
        Assert.Equal(473.5, inward.Position.Y, precision: 6);
        Assert.Equal(PetState.Walk, inward.State);
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

    [Fact]
    public void Idle_phase_advances_one_quarter_after_one_second()
    {
        var tuning = BehaviorTuning.Default with
        {
            MaxDelta = TimeSpan.FromSeconds(1),
            IdleMin = TimeSpan.FromSeconds(11),
            IdleMax = TimeSpan.FromSeconds(11),
            SleepDelay = TimeSpan.FromMinutes(11)
        };
        var brain = new PetBrain(tuning, new SequenceRandomSource(0.0), new PointD(100, 100));

        var actual = brain.Update(PetTestInput.At(1.0));

        Assert.Equal(PetState.Idle, actual.State);
        Assert.Equal(0.25, actual.Phase, precision: 10);
    }

    [Fact]
    public void Walk_with_multiple_reflections_matches_split_frames_in_position_and_heading()
    {
        var wholeFrame = CreateBoundaryBrain(headingUnit: 0, speed: 2000, new PointD(10, 200));
        var splitFrames = CreateBoundaryBrain(headingUnit: 0, speed: 2000, new PointD(10, 200));
        wholeFrame.Update(PetTestInput.At(0.1));
        splitFrames.Update(PetTestInput.At(0.1));

        var whole = wholeFrame.Update(PetTestInput.At(1.0));
        for (var index = 0; index < 10; index++)
        {
            splitFrames.Update(PetTestInput.At(0.1));
        }

        var split = splitFrames.Current;
        Assert.Equal(650, whole.Position.X, precision: 6);
        Assert.Equal(whole.Position.X, split.Position.X, precision: 6);
        Assert.Equal(whole.Position.Y, split.Position.Y, precision: 6);
        Assert.Equal(whole.Facing, split.Facing);

        var wholeAfterProbe = wholeFrame.Update(PetTestInput.At(0.01));
        var splitAfterProbe = splitFrames.Update(PetTestInput.At(0.01));

        Assert.Equal(670, wholeAfterProbe.Position.X, precision: 6);
        Assert.Equal(wholeAfterProbe.Position.X, splitAfterProbe.Position.X, precision: 6);
        Assert.Equal(wholeAfterProbe.Position.Y, splitAfterProbe.Position.Y, precision: 6);
    }

    [Theory]
    [InlineData(0.0, 10, 200, 350, 200, 340, 200, FacingDirection.Left)]
    [InlineData(0.5, 670, 200, 330, 200, 340, 200, FacingDirection.Right)]
    [InlineData(0.25, 200, 10, 200, 10, 200, 20, FacingDirection.Right)]
    [InlineData(0.75, 200, 490, 200, 490, 200, 480, FacingDirection.Left)]
    public void Walk_consumes_overshoot_across_representative_edges(
        double headingUnit,
        double initialX,
        double initialY,
        double expectedX,
        double expectedY,
        double expectedProbeX,
        double expectedProbeY,
        FacingDirection expectedFacing)
    {
        var brain = CreateBoundaryBrain(
            headingUnit,
            speed: 1000,
            new PointD(initialX, initialY));
        brain.Update(PetTestInput.At(0.1));

        var reflected = brain.Update(PetTestInput.At(1.0));
        var afterProbe = brain.Update(PetTestInput.At(0.01));

        Assert.Equal(expectedX, reflected.Position.X, precision: 6);
        Assert.Equal(expectedY, reflected.Position.Y, precision: 6);
        Assert.Equal(expectedFacing, reflected.Facing);
        Assert.Equal(expectedProbeX, afterProbe.Position.X, precision: 6);
        Assert.Equal(expectedProbeY, afterProbe.Position.Y, precision: 6);
    }

    private static PetBrain CreateBoundaryBrain(double headingUnit, double speed, PointD initialPosition) =>
        new(
            BehaviorTuning.Default with
            {
                MaxDelta = TimeSpan.FromSeconds(2),
                IdleMin = TimeSpan.FromSeconds(0.1),
                IdleMax = TimeSpan.FromSeconds(0.1),
                WalkMin = TimeSpan.FromSeconds(10),
                WalkMax = TimeSpan.FromSeconds(10),
                WalkSpeed = speed,
                IdleToWalkProbability = 1,
                SleepDelay = TimeSpan.FromMinutes(10)
            },
            new SequenceRandomSource(0, headingUnit),
            initialPosition);
}
