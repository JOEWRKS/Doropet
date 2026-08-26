using Dororong.Core.Behavior;

namespace Dororong.Core.Tests.TestSupport;

internal sealed class SequenceRandomSource(params double[] values) : IRandomSource
{
    private readonly Queue<double> _values = new(values);

    public double NextUnit() => _values.Count > 0
        ? _values.Dequeue()
        : throw new InvalidOperationException("The test consumed more random values than declared.");
}
