using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UserService.Application.Sagas.Ban;
using UserService.Application.UserProfiles;
using UserService.Messages;
using UserService.Messaging;

namespace UserService.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IUserProfileRepository, InMemoryUserProfileRepository>();
        services.AddSingleton<IUserProfileService, UserProfileService>();

        services.AddRetryingHandler<MessagePostedEvent, MessagePostedHandler>();

        // Ban saga. BanSagaOptions can be registered by the host before this call to override the defaults.
        services.TryAddSingleton(new BanSagaOptions());
        services.AddSingleton<ISagaStateRepository, InMemorySagaStateRepository>();
        services.AddSingleton<BanSagaOrchestrator>();

        return services;
    }
}

