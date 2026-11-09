namespace UserService.Messages.Bans;

/// <summary>Published by the Streaming Service when the user's active calls could not be ended.</summary>
public sealed class StreamTerminationFailedEvent
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }

    /// <summary>The attempt number of the command that failed. A redelivered event for an old attempt is ignored.</summary>
    public int Attempt { get; init; }

    public string Reason { get; init; } = string.Empty;
}
