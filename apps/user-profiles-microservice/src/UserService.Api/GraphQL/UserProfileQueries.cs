using HotChocolate.Authorization;
using UserService.Application.UserProfiles;
using UserService.Messages;

namespace UserService.Api.GraphQL;

// Read side of the GraphQL layer. Calls IUserProfileService, same as the REST controller,
// and requires the same policy as the matching REST endpoints.
public sealed class UserProfileQueries
{
    [Authorize(Policy = AuthorizationPolicies.InternalService)]
    public UserProfileContract? GetUserProfile(Guid id, [Service] IUserProfileService userProfileService) =>
        userProfileService.GetById(id);

    [Authorize(Policy = AuthorizationPolicies.InternalService)]
    public IReadOnlyCollection<UserProfileContract> GetUserProfiles([Service] IUserProfileService userProfileService) =>
        userProfileService.GetAll();
}
