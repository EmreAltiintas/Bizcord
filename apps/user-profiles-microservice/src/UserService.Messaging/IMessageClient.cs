namespace UserService.Messaging;

/// <summary>
/// Broker-agnostic publish/subscribe client.
/// </summary>
/// <remarks>
/// No EasyNetQ or RabbitMQ types appear in this contract, so the underlying transport
/// (RabbitMQ today, something else such as Kafka later) can be swapped by providing a new
/// implementation without touching callers. Implementations typically wrap a single
/// long-lived connection to the broker, hence <see cref="IAsyncDisposable"/>.
/// </remarks>
public interface IMessageClient : IAsyncDisposable
{
    /// <summary>
    /// Publishes <paramref name="message"/> to every interested subscriber.
    /// </summary>
    /// <typeparam name="TMessage">The message contract type.</typeparam>
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class;

    /// <summary>
    /// Registers <paramref name="onMessage"/> as a handler for messages of type
    /// <typeparamref name="TMessage"/>.
    /// </summary>
    /// <param name="subscriptionId">
    /// Names the subscription. Handlers that share a subscription id compete for messages
    /// (competing-consumers load balancing); handlers with distinct ids each receive their
    /// own copy of every message.
    /// </param>
    /// <param name="onMessage">Handler invoked for each received message.</param>
    /// <typeparam name="TMessage">The message contract type.</typeparam>
    Task SubscribeAsync<TMessage>(
        string subscriptionId,
        Func<TMessage, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken = default)
        where TMessage : class;
}
