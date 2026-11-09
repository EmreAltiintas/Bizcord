using System.Collections.Concurrent;

namespace SagaStubs.Worker;

/// <summary>Counts attempts per saga id and operation, and decides which ones should fail.</summary>
public sealed class FailureTracker
{
    private readonly int _failFirstAttempts;
    private readonly ConcurrentDictionary<(Guid SagaId, string Operation), int> _attempts = new();

    public FailureTracker(int failFirstAttempts) => _failFirstAttempts = failFirstAttempts;

    /// <summary>Registers an attempt of <paramref name="operation"/> for <paramref name="sagaId"/> and returns true if it should fail.</summary>
    public bool ShouldFail(Guid sagaId, string operation) =>
        _attempts.AddOrUpdate((sagaId, operation), 1, (_, count) => count + 1) <= _failFirstAttempts;
}
