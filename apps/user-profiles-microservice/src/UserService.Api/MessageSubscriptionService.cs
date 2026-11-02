using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using UserService.Messages;
using UserService.Messaging;

namespace UserService.Api;

/// <summary>Subscribes the MessagePostedEvent handler to the broker, retrying until the broker is reachable.</summary>
public sealed class MessageSubscriptionService : BackgroundService
{
    private readonly IMessageClient _messageClient;
    private readonly IMessageHandler<MessagePostedEvent> _handler;
    private readonly ResiliencePipeline _pipeline;

    public MessageSubscriptionService(
        IMessageClient messageClient,
        IMessageHandler<MessagePostedEvent> handler,
        IOptions<ResilienceOptions> options,
        ILogger<MessageSubscriptionService> logger)
    {
        _messageClient = messageClient;
        _handler = handler;
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
                        "Broker subscription failed (attempt {Attempt}), retrying in {Delay}",
                        args.AttemptNumber + 1, args.RetryDelay);
                    return default;
                }
            })
            .Build();
    }

    public bool IsSubscribed { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _pipeline.ExecuteAsync(
            async ct => await _messageClient.SubscribeAsync<MessagePostedEvent>(
                "user-service.message-posted", _handler.Handle, ct),
            stoppingToken);

        IsSubscribed = true;
    }
}
