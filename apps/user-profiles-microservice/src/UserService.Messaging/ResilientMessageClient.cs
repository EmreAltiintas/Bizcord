using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace UserService.Messaging;

public sealed class ResilientMessageClient : IMessageClient
{
    private readonly IMessageClient _inner;
    private readonly ResiliencePipeline _publishPipeline;
    private readonly CircuitBreakerStateProvider _circuitState = new();

    public ResilientMessageClient(
        IMessageClient inner,
        IOptions<ResilienceOptions> options,
        ILogger<ResilientMessageClient> logger)
    {
        _inner = inner;
        var publish = options.Value.Publish;

        _publishPipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                FailureRatio = publish.FailureRatio,
                MinimumThroughput = publish.MinimumThroughput,
                SamplingDuration = publish.SamplingDuration,
                BreakDuration = publish.BreakDuration,
                StateProvider = _circuitState,
                OnOpened = args =>
                {
                    logger.LogWarning(
                        "Publish circuit opened for {BreakDuration}: {Error}",
                        args.BreakDuration, args.Outcome.Exception?.Message);
                    return default;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("Publish circuit closed");
                    return default;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = publish.Timeout,
                OnTimeout = args =>
                {
                    logger.LogWarning("Publish timed out after {Timeout}", args.Timeout);
                    return default;
                }
            })
            .Build();
    }

    public bool IsPublishCircuitOpen => _circuitState.CircuitState != CircuitState.Closed;

    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class =>
        _publishPipeline.ExecuteAsync(
            async ct => await _inner.PublishAsync(message, ct).ConfigureAwait(false),
            cancellationToken).AsTask();

    public Task SubscribeAsync<TMessage>(
        string subscriptionId,
        Func<TMessage, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken = default)
        where TMessage : class =>
        _inner.SubscribeAsync(subscriptionId, onMessage, cancellationToken);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
