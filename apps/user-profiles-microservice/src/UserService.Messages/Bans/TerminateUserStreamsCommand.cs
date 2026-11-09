namespace UserService.Messages.Bans;

/// <summary>Sent by the ban saga to the Streaming Service (step 3).</summary>
public sealed class TerminateUserStreamsCommand
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }

    /// <summary>Attempt number, starting at 1. Echoed in <see cref="StreamTerminationFailedEvent"/>.</summary>
    public int Attempt { get; init; }
}
