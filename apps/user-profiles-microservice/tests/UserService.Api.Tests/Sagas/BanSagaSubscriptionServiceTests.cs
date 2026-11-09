using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UserService.Api.Sagas;
using UserService.Application.Sagas.Ban;
using UserService.Application.UserProfiles;
using UserService.Messaging;

namespace UserService.Api.Tests.Sagas;

public class BanSagaSubscriptionServiceTests
{
    private sealed class RecordingSubscribeClient : IMessageClient
    {
        private readonly int _failures;

        public RecordingSubscribeClient(int failures = 0) => _failures = failures;

        public List<string> Subscriptions { get; } = new();

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
            if (SubscribeCalls <= _failures)
            {
                throw new TaskCanceledException("broker down");
            }

            Subscriptions.Add(subscriptionId);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static BanSagaSubscriptionService Create(IMessageClient client)
    {
        var options = new ResilienceOptions();
        options.Subscribe.InitialDelay = TimeSpan.FromMilliseconds(10);
        options.Subscribe.MaxDelay = TimeSpan.FromMilliseconds(20);

        var orchestrator = new BanSagaOrchestrator(
            new InMemoryUserProfileRepository(),
            new InMemorySagaStateRepository(),
            client,
            NullLogger<BanSagaOrchestrator>.Instance);

        return new BanSagaSubscriptionService(
            client, orchestrator, Options.Create(options), NullLogger<BanSagaSubscriptionService>.Instance);
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
    public async Task Subscribes_to_every_message_the_orchestrator_receives_with_unique_ids()
    {
        var client = new RecordingSubscribeClient();
        var service = Create(client);

        await service.StartAsync(CancellationToken.None);
        await WaitUntil(() => service.IsSubscribed);

        Assert.True(service.IsSubscribed);
        Assert.Equal(8, client.Subscriptions.Count);
        Assert.Equal(client.Subscriptions.Count, client.Subscriptions.Distinct().Count());
        Assert.All(client.Subscriptions, id => Assert.StartsWith("user-service.", id));
        Assert.Contains("user-service.ban-user", client.Subscriptions);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Keeps_retrying_until_the_broker_answers_without_subscribing_twice()
    {
        var client = new RecordingSubscribeClient(failures: 3);
        var service = Create(client);

        await service.StartAsync(CancellationToken.None);
        await WaitUntil(() => service.IsSubscribed);

        Assert.True(service.IsSubscribed);
        Assert.Equal(8, client.Subscriptions.Count);
        Assert.Equal(11, client.SubscribeCalls);
        await service.StopAsync(CancellationToken.None);
    }
}
