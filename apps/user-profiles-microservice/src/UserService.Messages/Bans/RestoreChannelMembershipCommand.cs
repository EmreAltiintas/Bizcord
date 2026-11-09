namespace UserService.Messages.Bans;

/// <summary>Sent after an unban so the Channel Service can restore the memberships removed by the ban saga.</summary>
public sealed class RestoreChannelMembershipCommand
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }
}
