using System.ComponentModel.DataAnnotations;

namespace UserService.Api.Contracts;

public sealed record BanUserProfileRequest([Required] string Reason);
