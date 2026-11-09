namespace UserService.Messages.Bans;

/// <summary>Starts the ban saga for a user. Handled by the User Service.</summary>
public sealed class BanUserCommand
{
    public Guid SagaId { get; init; } = Guid.NewGuid();

    public Guid UserId { get; init; }

    public Guid ModeratorId { get; init; }

    public string Reason { get; init; } = string.Empty;
}
