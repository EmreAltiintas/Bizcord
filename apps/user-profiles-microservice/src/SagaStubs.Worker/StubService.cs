using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using UserService.Messaging;

namespace SagaStubs.Worker;

/// <summary>
/// Base class for the stubs. Subscribes with the same retry pattern as the User Service:
/// each subscription is retried with backoff until the broker is reachable.
/// </summary>
public abstract class StubService : BackgroundService
{
    private readonly ResiliencePipeline _pipeline;

    protected StubService(
        IMessageClient messageClient,
        IOptions<ResilienceOptions> options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(messageClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        MessageClient = messageClient;
        Logger = logger;
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

    protected IMessageClient MessageClient { get; }

    protected ILogger Logger { get; }

    protected async Task Subscribe<TMessage>(
        string subscriptionId,
        Func<TMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
        where TMessage : class =>
        await _pipeline.ExecuteAsync(
            async ct => await MessageClient.SubscribeAsync(subscriptionId, handler, ct),
            cancellationToken);
}
