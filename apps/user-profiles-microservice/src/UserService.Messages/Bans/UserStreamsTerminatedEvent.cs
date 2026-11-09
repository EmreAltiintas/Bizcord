namespace UserService.Messages.Bans;

/// <summary>Published by the Streaming Service when the user's active calls were ended.</summary>
public sealed class UserStreamsTerminatedEvent
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }
}
