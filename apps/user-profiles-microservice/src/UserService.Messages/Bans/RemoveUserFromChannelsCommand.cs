namespace UserService.Messages.Bans;

/// <summary>Sent by the ban saga to the Channel Service (step 2).</summary>
public sealed class RemoveUserFromChannelsCommand
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }

    /// <summary>Attempt number, starting at 1. Echoed in <see cref="ChannelRemovalFailedEvent"/>.</summary>
    public int Attempt { get; init; }
}
