namespace UserService.Messages.Bans;

/// <summary>Published by the Channel Service when memberships were restored after an unban.</summary>
public sealed class ChannelMembershipRestoredEvent
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }
}
