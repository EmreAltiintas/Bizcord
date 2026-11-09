namespace UserService.Messages.Bans;

/// <summary>Published by the Channel Service when the user could not be removed from all channels.</summary>
public sealed class ChannelRemovalFailedEvent
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }

    /// <summary>The attempt number of the command that failed. A redelivered event for an old attempt is ignored.</summary>
    public int Attempt { get; init; }

    public string Reason { get; init; } = string.Empty;
}
