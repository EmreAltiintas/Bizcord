namespace UserService.Messages.Bans;

/// <summary>Published by the Channel Service when the user was removed from all channels.</summary>
public sealed class UserRemovedFromChannelsEvent
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }
}
