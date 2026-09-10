using EasyNetQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UserService.Messaging;

/// <summary>
/// <see cref="IMessageClient"/> implementation backed by EasyNetQ / RabbitMQ.
/// </summary>
/// <remarks>
/// Owns the single long-lived <see cref="IBus"/> instance and is the only type in the
/// codebase that references EasyNetQ. EasyNetQ 8 creates the bus through a service
/// collection, so the client keeps a private <see cref="ServiceProvider"/> and hands its
/// lifetime off to <see cref="DisposeAsync"/>. Register it as a singleton (see
/// <see cref="MessagingServiceCollectionExtensions.AddMessageClient"/>); the host's DI
/// container disposes it on shutdown, tearing down the connection and all subscriptions.
/// </remarks>
public sealed class EasyNetQMessageClient : IMessageClient
{
    private readonly ServiceProvider _busProvider;
    private readonly IBus _bus;

    public EasyNetQMessageClient(IOptions<MessageClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var connectionString = options.Value.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                $"{nameof(MessageClientOptions)}.{nameof(MessageClientOptions.ConnectionString)} must not be empty.",
                nameof(options));
        }

        // EasyNetQ connects lazily and retries in the background, so building the bus does
        // not block or throw when the broker is unavailable.
        var services = new ServiceCollection();
        RabbitHutch.AddEasyNetQ(services, connectionString);
        _busProvider = services.BuildServiceProvider();
        _bus = _busProvider.GetRequiredService<IBus>();
    }

    /// <inheritdoc />
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);

        return _bus.PubSub.PublishAsync(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SubscribeAsync<TMessage>(
        string subscriptionId,
        Func<TMessage, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        ArgumentNullException.ThrowIfNull(onMessage);

        // The returned SubscriptionResult is owned by the bus and is torn down when the
        // bus is disposed, so there is nothing extra to track here.
        await _bus.PubSub
            .SubscribeAsync(subscriptionId, onMessage, _ => { }, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _busProvider.DisposeAsync().ConfigureAwait(false);
}
