namespace UserService.Messages.Bans;

/// <summary>Published by the Streaming Service when the user's active calls could not be ended.</summary>
public sealed class StreamTerminationFailedEvent
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }

    public string Reason { get; init; } = string.Empty;
}
