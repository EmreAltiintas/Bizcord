using System.ComponentModel.DataAnnotations;

namespace UserService.Api.Contracts;

public sealed record UpdateUserProfileRequest(
    [Required] string DisplayName,
    string? AvatarUrl,
    [Required] string PresenceStatus);
