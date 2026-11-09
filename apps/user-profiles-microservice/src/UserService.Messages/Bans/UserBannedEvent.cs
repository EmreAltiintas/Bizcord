namespace UserService.Messages.Bans;

/// <summary>Published by the User Service after the user was marked as banned (step 1 of the ban saga).</summary>
public sealed class UserBannedEvent
{
    public Guid SagaId { get; init; }

    public Guid UserId { get; init; }
}
