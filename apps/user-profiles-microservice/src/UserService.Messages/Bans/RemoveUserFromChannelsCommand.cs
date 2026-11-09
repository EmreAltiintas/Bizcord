namespace UserService.Messages.Bans;

/// <summary>Sent by the ban saga to the Channel Service (step 2).</summary>
public sealed class RemoveUserFromChannelsCommand
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }
}
