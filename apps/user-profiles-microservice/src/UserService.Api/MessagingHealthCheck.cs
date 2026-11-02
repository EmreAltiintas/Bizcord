using Microsoft.Extensions.Diagnostics.HealthChecks;
using UserService.Messaging;

namespace UserService.Api;

public sealed class MessagingHealthCheck : IHealthCheck
{
    private readonly MessageSubscriptionService _subscription;
    private readonly IMessageClient _messageClient;

    public MessagingHealthCheck(MessageSubscriptionService subscription, IMessageClient messageClient)
    {
        _subscription = subscription;
        _messageClient = messageClient;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_subscription.IsSubscribed)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Not subscribed to the broker."));
        }

        if (_messageClient is ResilientMessageClient { IsPublishCircuitOpen: true })
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Publish circuit is open."));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
