namespace UserService.Messages.Bans;

/// <summary>Published by the User Service when the ban could not be applied. The saga stops.</summary>
public sealed class UserBanFailedEvent
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }

    public string Reason { get; init; } = string.Empty;
}
