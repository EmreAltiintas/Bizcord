namespace UserService.Messages.Bans;

/// <summary>Published by the User Service after the ban was removed.</summary>
public sealed class UserUnbannedEvent
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }
}
