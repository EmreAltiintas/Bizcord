using Microsoft.Extensions.DependencyInjection;
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

        services.AddSingleton<ISagaStateRepository, InMemorySagaStateRepository>();
        services.AddSingleton<BanSagaOrchestrator>();

        return services;
    }
}

