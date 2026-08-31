using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.Core.Tests.TestSupport;

internal static class PetTestInput
{
    public static PetInput At(
        double seconds,
        PointD? pointer = null,
        bool primaryDown = false,
        bool localInteractionActive = false,
        PointD? bodyPressPosition = null,
        RectD? workArea = null) => new(
        TimeSpan.FromSeconds(seconds),
        workArea ?? new RectD(0, 0, 800, 600),
        new SizeD(120, 100),
        pointer is { } position ? new PointerSample(true, position) : PointerSample.Unavailable,
        primaryDown,
        localInteractionActive,
        bodyPressPosition,
        new SizeD(4, 4));

    public static PetBrain CreateBrain(IRandomSource random, PointD initialPosition) =>
        new(BehaviorTuning.Default with
        {
            MaxDelta = TimeSpan.FromSeconds(1),
            IdleMin = TimeSpan.FromSeconds(1),
            IdleMax = TimeSpan.FromSeconds(1),
            WalkMin = TimeSpan.FromSeconds(1),
            WalkMax = TimeSpan.FromSeconds(1),
            WalkSpeed = 42,
            IdleToWalkProbability = 1
        }, random, initialPosition);

    public static PetBrain CreateBrainAt(PointD initialPosition) =>
        CreateBrain(new SequenceRandomSource(Enumerable.Repeat(0.0, 32).ToArray()), initialPosition);

    public static PetBrain CreateReactionBrain() =>
        CreateReactionBrainAt(new PointD(100, 100));

    public static PetBrain CreateReactionBrainAt(PointD initialPosition) =>
        new(BehaviorTuning.Default with
        {
            MaxDelta = TimeSpan.FromSeconds(1),
            IdleMin = TimeSpan.FromMinutes(11),
            IdleMax = TimeSpan.FromMinutes(11),
            SleepDelay = TimeSpan.FromMinutes(11)
        }, new SequenceRandomSource(Enumerable.Repeat(0.0, 32).ToArray()), initialPosition);

    public static PetBrain CreateSleepBrain() =>
        CreateSleepBrain(TimeSpan.FromSeconds(1));

    public static PetBrain CreateSleepBrain(TimeSpan sleepDelay) =>
        new(BehaviorTuning.Default with
        {
            MaxDelta = TimeSpan.FromMilliseconds(100),
            IdleMin = TimeSpan.FromSeconds(11),
            IdleMax = TimeSpan.FromSeconds(11),
            SleepDelay = sleepDelay
        }, new SequenceRandomSource(Enumerable.Repeat(0.0, 32).ToArray()), new PointD(100, 100));

    public static PetBrain CreateSleepingBrain() =>
        CreateSleepingBrain(TimeSpan.FromSeconds(1));

    public static PetBrain CreateSleepingBrain(TimeSpan sleepDelay)
    {
        var brain = CreateSleepBrain(sleepDelay);
        var updateCount = (int)Math.Ceiling(sleepDelay.TotalSeconds / 0.1);
        for (var update = 0; update < updateCount; update++)
        {
            brain.Update(At(0.1));
        }

        return brain;
    }
}
