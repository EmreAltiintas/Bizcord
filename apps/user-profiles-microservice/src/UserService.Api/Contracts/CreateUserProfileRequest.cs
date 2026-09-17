using System.ComponentModel.DataAnnotations;

namespace UserService.Api.Contracts;

public sealed record CreateUserProfileRequest([Required] string DisplayName);
