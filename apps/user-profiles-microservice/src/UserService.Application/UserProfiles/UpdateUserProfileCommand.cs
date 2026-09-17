namespace UserService.Application.UserProfiles;

public sealed record UpdateUserProfileCommand(string DisplayName, string? AvatarUrl, string PresenceStatus);
