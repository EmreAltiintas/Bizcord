namespace UserService.Messages.Bans;

/// <summary>Lifts a ban. Handled by the User Service, which then asks the Channel Service to restore memberships.</summary>
public sealed class UnbanUserCommand
{
    public Guid SagaId { get; init; } = Guid.NewGuid();

    public Guid UserId { get; init; }

    public Guid ModeratorId { get; init; }
}
