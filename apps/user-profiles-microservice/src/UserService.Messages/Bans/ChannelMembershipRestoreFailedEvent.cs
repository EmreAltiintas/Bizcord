namespace UserService.Messages.Bans;

/// <summary>Published by the Channel Service when memberships could not be restored after an unban.</summary>
public sealed class ChannelMembershipRestoreFailedEvent
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }

    public string Reason { get; init; } = string.Empty;
}
