using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    /// backed by <see cref="EasyNetQMessageClient"/> and wrapped in <see cref="ResilientMessageClient"/>.
    /// </summary>
    public static IServiceCollection AddMessageClient(
        this IServiceCollection services,
        Action<MessageClientOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddOptions<MessageClientOptions>().Configure(configureOptions);
        services.AddOptions<ResilienceOptions>();
        services.TryAddSingleton<EasyNetQMessageClient>();
        services.TryAddSingleton<IMessageClient>(sp => new ResilientMessageClient(
            sp.GetRequiredService<EasyNetQMessageClient>(),
            sp.GetRequiredService<IOptions<ResilienceOptions>>(),
            sp.GetRequiredService<ILogger<ResilientMessageClient>>()));

        return services;
    }

    public static IServiceCollection AddRetryingHandler<TMessage, THandler>(this IServiceCollection services)
        where TMessage : class
        where THandler : class, IMessageHandler<TMessage>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<THandler>();
        services.AddSingleton<IMessageHandler<TMessage>>(sp => new RetryingMessageHandler<TMessage>(
            sp.GetRequiredService<THandler>(),
            sp.GetRequiredService<IOptions<ResilienceOptions>>(),
            sp.GetRequiredService<ILogger<RetryingMessageHandler<TMessage>>>()));

        return services;
    }
}
