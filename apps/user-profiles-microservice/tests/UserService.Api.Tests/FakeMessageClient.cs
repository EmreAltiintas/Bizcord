using UserService.Messaging;

namespace UserService.Api.Tests;

public sealed class FakeMessageClient : IMessageClient
{
    public List<object> Published { get; } = new();

    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        Published.Add(message);
        return Task.CompletedTask;
    }

    public Task SubscribeAsync<TMessage>(
        string subscriptionId,
        Func<TMessage, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken = default)
        where TMessage : class => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
