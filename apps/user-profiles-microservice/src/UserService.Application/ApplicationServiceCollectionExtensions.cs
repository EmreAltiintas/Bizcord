using Microsoft.Extensions.DependencyInjection;
using UserService.Application.UserProfiles;

namespace UserService.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IUserProfileRepository, InMemoryUserProfileRepository>();
        services.AddSingleton<IUserProfileService, UserProfileService>();

        return services;
    }
}
