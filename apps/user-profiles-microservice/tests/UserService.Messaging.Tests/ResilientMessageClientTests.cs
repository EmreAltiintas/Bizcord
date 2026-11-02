using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace UserService.Messaging.Tests;

public class ResilientMessageClientTests
{
    private sealed class StubClient : IMessageClient
    {
        public int PublishCalls { get; private set; }

        public Func<CancellationToken, Task> OnPublish { get; set; } = _ => Task.CompletedTask;

        public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class
        {
            PublishCalls++;
            return OnPublish(cancellationToken);
        }

        public Task SubscribeAsync<TMessage>(
            string subscriptionId,
            Func<TMessage, CancellationToken, Task> onMessage,
            CancellationToken cancellationToken = default)
            where TMessage : class => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static ResilientMessageClient Create(StubClient inner, TimeSpan? timeout = null)
    {
        var options = new ResilienceOptions();
        options.Publish.Timeout = timeout ?? TimeSpan.FromSeconds(5);
        options.Publish.MinimumThroughput = 2;
        options.Publish.FailureRatio = 0.5;
        options.Publish.BreakDuration = TimeSpan.FromSeconds(30);

        return new ResilientMessageClient(
            inner, Options.Create(options), NullLogger<ResilientMessageClient>.Instance);
    }

    [Fact]
    public async Task Publish_PassesThrough_WhenInnerSucceeds()
    {
        var inner = new StubClient();
        var client = Create(inner);

        await client.PublishAsync(new object());

        Assert.Equal(1, inner.PublishCalls);
        Assert.False(client.IsPublishCircuitOpen);
    }

    [Fact]
    public async Task Publish_ThrowsTimeout_WhenInnerHangs()
    {
        var inner = new StubClient { OnPublish = ct => Task.Delay(Timeout.Infinite, ct) };
        var client = Create(inner, TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => client.PublishAsync(new object()));
    }

    [Fact]
    public async Task Publish_OpensCircuit_AfterRepeatedFailures_AndStopsCallingInner()
    {
        var inner = new StubClient { OnPublish = _ => throw new InvalidOperationException("broker down") };
        var client = Create(inner);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PublishAsync(new object()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PublishAsync(new object()));

        await Assert.ThrowsAsync<BrokenCircuitException>(() => client.PublishAsync(new object()));

        Assert.Equal(2, inner.PublishCalls);
        Assert.True(client.IsPublishCircuitOpen);
    }
}
