using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UserService.Messages;
using UserService.Messaging;

namespace UserService.Api.Tests;

public class MessageSubscriptionServiceTests
{
    private sealed class FlakySubscribeClient : IMessageClient
    {
        private readonly int _failures;

        public FlakySubscribeClient(int failures) => _failures = failures;

        public int SubscribeCalls { get; private set; }

        public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class => Task.CompletedTask;

        public Task SubscribeAsync<TMessage>(
            string subscriptionId,
            Func<TMessage, CancellationToken, Task> onMessage,
            CancellationToken cancellationToken = default)
            where TMessage : class
        {
            SubscribeCalls++;
            return SubscribeCalls <= _failures ? throw new TaskCanceledException("broker down") : Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoopHandler : IMessageHandler<MessagePostedEvent>
    {
        public Task Handle(MessagePostedEvent message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static MessageSubscriptionService Create(IMessageClient client)
    {
        var options = new ResilienceOptions();
        options.Subscribe.InitialDelay = TimeSpan.FromMilliseconds(10);
        options.Subscribe.MaxDelay = TimeSpan.FromMilliseconds(20);

        return new MessageSubscriptionService(
            client, new NoopHandler(), Options.Create(options), NullLogger<MessageSubscriptionService>.Instance);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task StartAsync_ReturnsImmediately_WhenBrokerIsDown()
    {
        var service = Create(new FlakySubscribeClient(failures: int.MaxValue));

        var start = service.StartAsync(CancellationToken.None);

        Assert.True(start.IsCompleted);
        Assert.False(service.IsSubscribed);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Service_KeepsRetrying_UntilSubscribed()
    {
        var client = new FlakySubscribeClient(failures: 3);
        var service = Create(client);

        await service.StartAsync(CancellationToken.None);
        await WaitUntil(() => service.IsSubscribed);

        Assert.True(service.IsSubscribed);
        Assert.Equal(4, client.SubscribeCalls);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HealthCheck_IsUnhealthy_UntilSubscribed()
    {
        var client = new FlakySubscribeClient(failures: 1);
        var service = Create(client);
        var check = new MessagingHealthCheck(service, client);

        var before = await check.CheckHealthAsync(new HealthCheckContext());
        await service.StartAsync(CancellationToken.None);
        await WaitUntil(() => service.IsSubscribed);
        var after = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, before.Status);
        Assert.Equal(HealthStatus.Healthy, after.Status);
        await service.StopAsync(CancellationToken.None);
    }
}
