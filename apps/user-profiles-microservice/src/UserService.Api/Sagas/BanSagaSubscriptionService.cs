using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using UserService.Application.Sagas.Ban;
using UserService.Messages.Bans;
using UserService.Messaging;

namespace UserService.Api.Sagas;

/// <summary>
/// Subscribes <see cref="BanSagaOrchestrator"/> to every message it receives, retrying each
/// subscription until the broker is reachable.
/// </summary>
public sealed class BanSagaSubscriptionService : BackgroundService
{
    private readonly IMessageClient _messageClient;
    private readonly BanSagaOrchestrator _orchestrator;
    private readonly ResiliencePipeline _pipeline;

    public BanSagaSubscriptionService(
        IMessageClient messageClient,
        BanSagaOrchestrator orchestrator,
        IOptions<ResilienceOptions> options,
        ILogger<BanSagaSubscriptionService> logger)
    {
        ArgumentNullException.ThrowIfNull(messageClient);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _messageClient = messageClient;
        _orchestrator = orchestrator;
        var subscribe = options.Value.Subscribe;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = int.MaxValue,
                Delay = subscribe.InitialDelay,
                MaxDelay = subscribe.MaxDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Ban saga subscription failed (attempt {Attempt}), retrying in {Delay}",
                        args.AttemptNumber + 1, args.RetryDelay);
                    return default;
                }
            })
            .Build();
    }

    public bool IsSubscribed { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Each subscription is retried on its own so a failure on one does not subscribe the
        // earlier ones a second time.
        await Subscribe<BanUserCommand>("user-service.ban-user", _orchestrator.Handle, stoppingToken);
        await Subscribe<UserRemovedFromChannelsEvent>("user-service.user-removed-from-channels", _orchestrator.Handle, stoppingToken);
        await Subscribe<ChannelRemovalFailedEvent>("user-service.channel-removal-failed", _orchestrator.Handle, stoppingToken);
        await Subscribe<UserStreamsTerminatedEvent>("user-service.user-streams-terminated", _orchestrator.Handle, stoppingToken);
        await Subscribe<StreamTerminationFailedEvent>("user-service.stream-termination-failed", _orchestrator.Handle, stoppingToken);
        await Subscribe<UnbanUserCommand>("user-service.unban-user", _orchestrator.Handle, stoppingToken);
        await Subscribe<ChannelMembershipRestoredEvent>("user-service.channel-membership-restored", _orchestrator.Handle, stoppingToken);
        await Subscribe<ChannelMembershipRestoreFailedEvent>("user-service.channel-membership-restore-failed", _orchestrator.Handle, stoppingToken);

        IsSubscribed = true;
    }

    private async Task Subscribe<TMessage>(
        string subscriptionId,
        Func<TMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
        where TMessage : class =>
        await _pipeline.ExecuteAsync(
            async ct => await _messageClient.SubscribeAsync(subscriptionId, handler, ct),
            cancellationToken);
}
