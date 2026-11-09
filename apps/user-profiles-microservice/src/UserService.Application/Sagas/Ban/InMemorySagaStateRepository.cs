using System.Collections.Concurrent;

namespace UserService.Application.Sagas.Ban;

/// <summary>
/// Keeps saga state in memory.
/// </summary>
/// <remarks>
/// This is a deliberate limitation: the service has no database, so the state is lost when
/// the process restarts and only works with a single instance. A saga that is in flight at
/// restart cannot be resumed, and a second instance would not see the first one's state.
/// </remarks>
public sealed class InMemorySagaStateRepository : ISagaStateRepository
{
    private readonly ConcurrentDictionary<Guid, BanSagaState> _states = new();

    public Task<BanSagaState?> GetById(Guid sagaId) =>
        Task.FromResult(_states.GetValueOrDefault(sagaId));

    public Task<BanSagaState?> GetLatestByUserId(Guid userId) =>
        Task.FromResult(_states.Values
            .Where(state => state.UserId == userId && state.UserBanned)
            .OrderByDescending(state => state.StartedAt)
            .FirstOrDefault());

    public Task Save(BanSagaState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _states[state.SagaId] = state;
        return Task.CompletedTask;
    }
}
