namespace UserService.Messages.Bans;

/// <summary>
/// Sent after an unban so the Channel Service can restore the memberships removed by the ban saga.
/// It carries the SagaId of the ban saga it compensates, so the answer can be matched to that saga's state.
/// </summary>
public sealed class RestoreChannelMembershipCommand
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }
}
