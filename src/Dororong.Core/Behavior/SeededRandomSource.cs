namespace Dororong.Core.Behavior;

public sealed class SeededRandomSource : IRandomSource
{
    private readonly Random _random;

    public SeededRandomSource(int? seed = null)
    {
        _random = seed is { } value ? new Random(value) : new Random();
    }

    public double NextUnit() => _random.NextDouble();
}
