using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace UserService.Messaging;

public sealed class RetryingMessageHandler<TMessage> : IMessageHandler<TMessage>
    where TMessage : class
{
    private readonly IMessageHandler<TMessage> _inner;
    private readonly ResiliencePipeline _pipeline;

    public RetryingMessageHandler(
        IMessageHandler<TMessage> inner,
        IOptions<ResilienceOptions> options,
        ILogger<RetryingMessageHandler<TMessage>> logger)
    {
        _inner = inner;
        var handler = options.Value.Handler;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = handler.MaxRetryAttempts,
                Delay = handler.Delay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Handler for {MessageType} failed, retry {Attempt} of {MaxAttempts} in {Delay}",
                        typeof(TMessage).Name, args.AttemptNumber + 1, handler.MaxRetryAttempts, args.RetryDelay);
                    return default;
                }
            })
            .Build();
    }

    public async Task Handle(TMessage message, CancellationToken cancellationToken) =>
        await _pipeline.ExecuteAsync(
            async ct => await _inner.Handle(message, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
}
