using Microsoft.Extensions.Options;
using UserService.Messages.Bans;
using UserService.Messaging;

namespace SagaStubs.Worker;

/// <summary>Simulates the Streaming/VOIP Service, which does not exist yet.</summary>
public sealed class StreamingStub : StubService
{
    private readonly FailureTracker _failures;

    public StreamingStub(
        IMessageClient messageClient,
        FailureTracker failures,
        IOptions<ResilienceOptions> options,
        ILogger<StreamingStub> logger)
        : base(messageClient, options, logger) =>
        _failures = failures;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Subscribe<TerminateUserStreamsCommand>(
            "streaming-stub.terminate-user-streams", TerminateUserStreams, stoppingToken);

        Logger.LogInformation("Streaming stub is listening");
    }

    private async Task TerminateUserStreams(TerminateUserStreamsCommand command, CancellationToken cancellationToken)
    {
        if (_failures.ShouldFail(command.SagaId, "terminate"))
        {
            Logger.LogWarning("Simulated failure ending streams for user {UserId} (saga {SagaId})", command.UserId, command.SagaId);
            await MessageClient.PublishAsync(
                new StreamTerminationFailedEvent
                {
                    SagaId = command.SagaId,
                    UserId = command.UserId,
                    Attempt = command.Attempt,
                    Reason = "Simulated streaming service failure."
                },
                cancellationToken);
            return;
        }

        Logger.LogInformation("Ended active streams for user {UserId} (saga {SagaId})", command.UserId, command.SagaId);
        await MessageClient.PublishAsync(
            new UserStreamsTerminatedEvent { SagaId = command.SagaId, UserId = command.UserId },
            cancellationToken);
    }
}
