using Microsoft.Extensions.Options;
using UserService.Messages.Bans;
using UserService.Messaging;

namespace SagaStubs.Worker;

/// <summary>Simulates the Channel Service, which does not exist yet.</summary>
public sealed class ChannelStub : StubService
{
    private readonly FailureTracker _failures;

    public ChannelStub(
        IMessageClient messageClient,
        FailureTracker failures,
        IOptions<ResilienceOptions> options,
        ILogger<ChannelStub> logger)
        : base(messageClient, options, logger) =>
        _failures = failures;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Subscribe<RemoveUserFromChannelsCommand>(
            "channel-stub.remove-user-from-channels", RemoveUserFromChannels, stoppingToken);
        await Subscribe<RestoreChannelMembershipCommand>(
            "channel-stub.restore-channel-membership", RestoreChannelMembership, stoppingToken);

        Logger.LogInformation("Channel stub is listening");
    }

    private async Task RemoveUserFromChannels(RemoveUserFromChannelsCommand command, CancellationToken cancellationToken)
    {
        if (_failures.ShouldFail(command.SagaId))
        {
            Logger.LogWarning("Simulated failure removing user {UserId} from channels (saga {SagaId})", command.UserId, command.SagaId);
            await MessageClient.PublishAsync(
                new ChannelRemovalFailedEvent
                {
                    SagaId = command.SagaId,
                    UserId = command.UserId,
                    Reason = "Simulated channel service failure."
                },
                cancellationToken);
            return;
        }

        Logger.LogInformation("Removed user {UserId} from all channels (saga {SagaId})", command.UserId, command.SagaId);
        await MessageClient.PublishAsync(
            new UserRemovedFromChannelsEvent { SagaId = command.SagaId, UserId = command.UserId },
            cancellationToken);
    }

    private async Task RestoreChannelMembership(RestoreChannelMembershipCommand command, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Restored channel memberships for user {UserId} (saga {SagaId})", command.UserId, command.SagaId);
        await MessageClient.PublishAsync(
            new ChannelMembershipRestoredEvent { SagaId = command.SagaId, UserId = command.UserId },
            cancellationToken);
    }
}
