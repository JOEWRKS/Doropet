namespace Dororong.Core.Behavior;

public sealed record BehaviorTuning
{
    public static BehaviorTuning Default { get; } = new();

    public TimeSpan MaxDelta { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan IdleMin { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan IdleMax { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan WalkMin { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan WalkMax { get; init; } = TimeSpan.FromSeconds(7);
    public double WalkSpeed { get; init; } = 21;
    public double IdleToWalkProbability { get; init; } = 0.65;
    public TimeSpan CuriousDuration { get; init; } = TimeSpan.FromSeconds(1.6);
    public TimeSpan StartledDuration { get; init; } = TimeSpan.FromSeconds(0.75);
    public TimeSpan ClickReactionDuration { get; init; } = TimeSpan.FromSeconds(0.5);
    public TimeSpan SleepDelay { get; init; } = TimeSpan.FromSeconds(90);
    public double NearEnterDistance { get; init; } = 150;
    public double NearExitDistance { get; init; } = 210;
    public double StartleReactionDistance { get; init; } = 220;
    public double StartleClosingSpeed { get; init; } = 650;
    public TimeSpan CuriousCooldown { get; init; } = TimeSpan.FromSeconds(4);
    public TimeSpan StartledCooldown { get; init; } = TimeSpan.FromSeconds(3);
    public double StartleRetreatDistance { get; init; } = 72;
}
