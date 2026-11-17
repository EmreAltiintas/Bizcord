using System.Security.Claims;
using HotChocolate.Authorization;
using UserService.Application.Common;
using Result = UserService.Application.Common.Result<UserService.Messages.UserProfileContract>;
using UserService.Application.UserProfiles;
using UserService.Messages;

namespace UserService.Api.GraphQL;

// Write side of the GraphQL layer. Calls IUserProfileService, same as the REST controller.
// The profile id comes from the token subject, as in the REST API.
public sealed class UserProfileMutations
{
    [Authorize(Policy = AuthorizationPolicies.ProfilesWrite)]
    public UserProfileContract CreateUserProfile(
        CreateUserProfileInput input,
        ClaimsPrincipal user,
        [Service] IUserProfileService userProfileService)
    {
        var subject = GetSubject(user)
            ?? throw Error("Token has no valid sub claim.", "BAD_REQUEST");

        var result = userProfileService.Create(new CreateUserProfileCommand(input.DisplayName, subject));

        return Unwrap(result);
    }

    [Authorize(Policy = AuthorizationPolicies.ProfilesWrite)]
    public UserProfileContract UpdateUserProfile(
        Guid id,
        UpdateUserProfileInput input,
        ClaimsPrincipal user,
        [Service] IUserProfileService userProfileService)
    {
        if (GetSubject(user) != id)
        {
            throw Error("Token subject does not match the profile.", "FORBIDDEN");
        }

        var result = userProfileService.Update(
            id, new UpdateUserProfileCommand(input.DisplayName, input.AvatarUrl, input.PresenceStatus));

        return Unwrap(result);
    }

    private static Guid? GetSubject(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value, out var subject) ? subject : null;

    private static UserProfileContract Unwrap(Result result)
    {
        if (result.IsSuccess)
        {
            return result.Value!;
        }

        var code = result.ErrorType switch
        {
            ResultErrorType.NotFound => "NOT_FOUND",
            ResultErrorType.Conflict => "CONFLICT",
            _ => "BAD_REQUEST"
        };

        throw Error(result.Error ?? "The request failed.", code);
    }

    private static GraphQLException Error(string message, string code) =>
        new(ErrorBuilder.New().SetMessage(message).SetCode(code).Build());
}
