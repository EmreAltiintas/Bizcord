using UserService.Messages;
using UserService.Messaging;

namespace UserService.Api;

/// <summary>Subscribes the MessagePostedEvent handler to the broker when the host starts.</summary>
public sealed class MessageSubscriptionService : IHostedService
{
    private readonly IMessageClient _messageClient;
    private readonly IMessageHandler<MessagePostedEvent> _handler;

    public MessageSubscriptionService(IMessageClient messageClient, IMessageHandler<MessagePostedEvent> handler)
    {
        _messageClient = messageClient;
        _handler = handler;
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        _messageClient.SubscribeAsync<MessagePostedEvent>(
            "user-service.message-posted", _handler.Handle, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
