using UserService.Domain.Entities;
using UserService.Messages;

namespace UserService.Application.Mapping;

public static class UserProfileMappingExtensions
{
    public static UserProfileContract ToContract(this UserProfile profile) =>
        new(
            profile.Id.Value,
            profile.DisplayName.Value,
            profile.Avatar?.Value.ToString(),
            profile.PresenceStatus.Name,
            profile.CreatedAtUtc);
}
