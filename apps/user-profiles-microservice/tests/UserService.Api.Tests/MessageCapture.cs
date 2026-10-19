using UserService.Messaging;

namespace UserService.Api.Tests;

/// <summary>Subscribes to <typeparamref name="TMessage"/> and lets a test await the next one.</summary>
public sealed class MessageCapture<TMessage>
    where TMessage : class
{
    private readonly TaskCompletionSource<TMessage> _received =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public MessageCapture(IMessageClient client) =>
        client.SubscribeAsync<TMessage>(
            $"test-capture-{Guid.NewGuid()}",
            (message, _) =>
            {
                _received.TrySetResult(message);
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();

    public async Task<TMessage> WaitForMessageAsync(TimeSpan timeout) =>
        await _received.Task.WaitAsync(timeout);
}
