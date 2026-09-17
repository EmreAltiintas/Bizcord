namespace UserService.Messages;

public sealed record UserProfileContract(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    string PresenceStatus,
    DateTimeOffset CreatedAt);
