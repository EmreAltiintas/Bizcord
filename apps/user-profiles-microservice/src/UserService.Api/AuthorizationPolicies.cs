using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace UserService.Api;

public static class AuthorizationPolicies
{
    public const string InternalService = "InternalService";
    public const string ProfilesRead = "ProfilesRead";
    public const string ProfilesWrite = "ProfilesWrite";

    public static void Register(AuthorizationOptions options)
    {
        options.AddPolicy(InternalService, policy => policy.RequireRole("internal-service"));
        options.AddPolicy(ProfilesRead, policy => policy.RequireAssertion(ctx => HasScope(ctx.User, "user-profiles.read")));
        options.AddPolicy(ProfilesWrite, policy => policy.RequireAssertion(ctx => HasScope(ctx.User, "user-profiles.write")));
    }

    private static bool HasScope(ClaimsPrincipal user, string scope) =>
        user.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope);
}
