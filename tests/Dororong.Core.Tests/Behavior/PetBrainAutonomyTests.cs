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
}
