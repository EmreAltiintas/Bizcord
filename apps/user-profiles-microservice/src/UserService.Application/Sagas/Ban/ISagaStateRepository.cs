namespace UserService.Application.Sagas.Ban;

/// <summary>Stores the state of ban sagas.</summary>
public interface ISagaStateRepository
{
    /// <summary>Returns the state for <paramref name="sagaId"/>, or null if the saga is unknown.</summary>
    Task<BanSagaState?> GetById(Guid sagaId);

    /// <summary>
    /// Returns the most recently started saga that banned <paramref name="userId"/>, or null if there is none.
    /// Sagas that failed before banning the user are not considered.
    /// </summary>
    Task<BanSagaState?> GetLatestByUserId(Guid userId);

    /// <summary>Inserts or replaces the state.</summary>
    Task Save(BanSagaState state);
}
