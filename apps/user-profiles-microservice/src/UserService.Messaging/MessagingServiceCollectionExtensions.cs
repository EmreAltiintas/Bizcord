using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace UserService.Messaging;

/// <summary>
/// DI wiring for the message client. Calling <see cref="AddMessageClient"/> is the only
/// step a host needs to enable messaging.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="MessageClientOptions"/> (configured via
    /// <paramref name="configureOptions"/>) and <see cref="IMessageClient"/> as a singleton
    /// backed by <see cref="EasyNetQMessageClient"/>.
    /// </summary>
    public static IServiceCollection AddMessageClient(
        this IServiceCollection services,
        Action<MessageClientOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddOptions<MessageClientOptions>().Configure(configureOptions);
        services.TryAddSingleton<IMessageClient, EasyNetQMessageClient>();

        return services;
    }
}
