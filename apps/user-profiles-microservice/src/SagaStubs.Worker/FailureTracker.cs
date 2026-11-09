using System.Collections.Concurrent;

namespace SagaStubs.Worker;

/// <summary>Counts attempts per saga id and decides which ones should fail.</summary>
public sealed class FailureTracker
{
    private readonly int _failFirstAttempts;
    private readonly ConcurrentDictionary<Guid, int> _attempts = new();

    public FailureTracker(int failFirstAttempts) => _failFirstAttempts = failFirstAttempts;

    /// <summary>Registers an attempt for <paramref name="sagaId"/> and returns true if it should fail.</summary>
    public bool ShouldFail(Guid sagaId) =>
        _attempts.AddOrUpdate(sagaId, 1, (_, count) => count + 1) <= _failFirstAttempts;
}
