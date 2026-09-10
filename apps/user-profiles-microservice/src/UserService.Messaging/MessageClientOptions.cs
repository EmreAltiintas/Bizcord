namespace UserService.Messaging;

/// <summary>
/// Connection settings for the message broker.
/// </summary>
/// <remarks>
/// Kept deliberately small. The value is a broker connection string whose format is
/// interpreted by the concrete <see cref="IMessageClient"/> implementation
/// (for <see cref="EasyNetQMessageClient"/> it is an EasyNetQ connection string,
/// e.g. <c>"host=localhost"</c> or <c>"host=rabbitmq;username=guest;password=guest"</c>).
/// Bind it from <c>appsettings.json</c> / environment variables via
/// <see cref="MessagingServiceCollectionExtensions.AddMessageClient"/>.
/// </remarks>
public sealed class MessageClientOptions
{
    /// <summary>
    /// Broker connection string. Defaults to the local RabbitMQ instance started by
    /// <c>docker-compose.yaml</c>.
    /// </summary>
    public string ConnectionString { get; set; } = "host=localhost";
}
