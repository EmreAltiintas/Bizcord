namespace UserService.Messages.Bans;

/// <summary>Sent by the ban saga to the Streaming Service (step 3).</summary>
public sealed class TerminateUserStreamsCommand
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }
}
