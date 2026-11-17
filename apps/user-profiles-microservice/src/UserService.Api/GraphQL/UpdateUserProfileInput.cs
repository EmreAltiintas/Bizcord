namespace UserService.Api.GraphQL;

public sealed record UpdateUserProfileInput(string DisplayName, string? AvatarUrl, string PresenceStatus);
