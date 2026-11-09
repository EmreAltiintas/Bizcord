using Microsoft.Extensions.Logging;
using UserService.Application.UserProfiles;
using UserService.Domain.ValueObjects;
using UserService.Messages.Bans;
using UserService.Messaging;

namespace UserService.Application.Sagas.Ban;

/// <summary>
/// Orchestrates the ban saga. All saga logic is here, the other services only answer commands.
/// </summary>
/// <remarks>
/// Step 1 bans the user and is never rolled back. Steps 2 (channels) and 3 (calls) are retried
/// up to three times with backoff. If a step gives up, <see cref="BanSagaState.NeedsOperator"/> is
/// set and the saga moves on. Only a moderator unban compensates, and it is rejected while the
/// ban saga is still running. Delivery is at-least-once, so a message for a step that is already
/// registered in the state is ignored, and so is a failure event for an old attempt.
/// Each Handle method can be passed directly to <see cref="IMessageClient.SubscribeAsync{TMessage}"/>.
/// </remarks>
public sealed class BanSagaOrchestrator
{
    private const int MaxStepAttempts = 3;
    private static readonly TimeSpan BackoffBaseDelay = TimeSpan.FromSeconds(1);

    private readonly IUserProfileRepository _profiles;
    private readonly ISagaStateRepository _states;
    private readonly IMessageClient _messageClient;
    private readonly ILogger<BanSagaOrchestrator> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <param name="delay">
    /// Waits between retries. Defaults to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>;
    /// tests replace it so they do not sleep.
    /// </param>
    public BanSagaOrchestrator(
        IUserProfileRepository profiles,
        ISagaStateRepository states,
        IMessageClient messageClient,
        ILogger<BanSagaOrchestrator> logger,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(messageClient);
        ArgumentNullException.ThrowIfNull(logger);

        _profiles = profiles;
        _states = states;
        _messageClient = messageClient;
        _logger = logger;
        _delay = delay ?? Task.Delay;
    }

    public async Task Handle(BanUserCommand message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (await _states.GetById(message.SagaId) is not null)
        {
            _logger.LogInformation("Ban saga {SagaId} already started, ignoring duplicate command", message.SagaId);
            return;
        }

        var state = new BanSagaState
        {
            SagaId = message.SagaId,
            UserId = message.UserId,
            ModeratorId = message.ModeratorId,
            Reason = message.Reason,
            StartedAt = DateTimeOffset.UtcNow
        };

        var profile = _profiles.GetById(new UserId(message.UserId));
        if (profile is null)
        {
            await FailAsync(state, "User profile not found.", cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(message.Reason))
        {
            await FailAsync(state, "A ban reason is required.", cancellationToken);
            return;
        }

        if (profile.IsBanned)
        {
            await FailAsync(state, "User is already banned.", cancellationToken);
            return;
        }

        profile.Ban(message.Reason);
        _profiles.Update(profile);

        state.UserBanned = true;
        state.ChannelAttempts = 1;
        await _states.Save(state);

        await _messageClient.PublishAsync(
            new UserBannedEvent { SagaId = state.SagaId, UserId = state.UserId }, cancellationToken);
        await _messageClient.PublishAsync(
            new RemoveUserFromChannelsCommand
            {
                SagaId = state.SagaId,
                UserId = state.UserId,
                Attempt = state.ChannelAttempts
            },
            cancellationToken);
    }

    public async Task Handle(UserRemovedFromChannelsEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var state = await LoadActive(message.SagaId, nameof(UserRemovedFromChannelsEvent));
        if (state is null || ChannelStepDecided(state))
        {
            return;
        }

        state.ChannelsRemoved = true;
        await StartStreamStep(state, cancellationToken);
    }

    public async Task Handle(ChannelRemovalFailedEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var state = await LoadActive(message.SagaId, nameof(ChannelRemovalFailedEvent));
        if (state is null || ChannelStepDecided(state))
        {
            return;
        }

        if (IsStaleAttempt(message.Attempt, state.ChannelAttempts, state.SagaId, nameof(ChannelRemovalFailedEvent)))
        {
            return;
        }

        if (state.ChannelAttempts < MaxStepAttempts)
        {
            var wait = BackoffFor(state.ChannelAttempts);
            _logger.LogWarning(
                "Channel removal failed for saga {SagaId} (attempt {Attempt} of {Max}, reason: {Reason}). Retrying in {Delay}",
                state.SagaId, state.ChannelAttempts, MaxStepAttempts, message.Reason, wait);

            await _delay(wait, cancellationToken);

            state.ChannelAttempts++;
            await _states.Save(state);
            await _messageClient.PublishAsync(
                new RemoveUserFromChannelsCommand
                {
                    SagaId = state.SagaId,
                    UserId = state.UserId,
                    Attempt = state.ChannelAttempts
                },
                cancellationToken);
            return;
        }

        _logger.LogError(
            "Giving up on channel removal for saga {SagaId} after {Attempts} attempts (reason: {Reason}). Operator action needed",
            state.SagaId, state.ChannelAttempts, message.Reason);

        state.ChannelsGaveUp = true;
        state.NeedsOperator = true;
        await StartStreamStep(state, cancellationToken);
    }

    public async Task Handle(UserStreamsTerminatedEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var state = await LoadActive(message.SagaId, nameof(UserStreamsTerminatedEvent));
        if (state is null || StreamStepDecided(state))
        {
            return;
        }

        state.StreamsTerminated = true;
        await CompleteAsync(state);
    }

    public async Task Handle(StreamTerminationFailedEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var state = await LoadActive(message.SagaId, nameof(StreamTerminationFailedEvent));
        if (state is null || StreamStepDecided(state))
        {
            return;
        }

        if (IsStaleAttempt(message.Attempt, state.StreamAttempts, state.SagaId, nameof(StreamTerminationFailedEvent)))
        {
            return;
        }

        if (state.StreamAttempts < MaxStepAttempts)
        {
            var wait = BackoffFor(state.StreamAttempts);
            _logger.LogWarning(
                "Stream termination failed for saga {SagaId} (attempt {Attempt} of {Max}, reason: {Reason}). Retrying in {Delay}",
                state.SagaId, state.StreamAttempts, MaxStepAttempts, message.Reason, wait);

            await _delay(wait, cancellationToken);

            state.StreamAttempts++;
            await _states.Save(state);
            await _messageClient.PublishAsync(
                new TerminateUserStreamsCommand
                {
                    SagaId = state.SagaId,
                    UserId = state.UserId,
                    Attempt = state.StreamAttempts
                },
                cancellationToken);
            return;
        }

        _logger.LogError(
            "Giving up on stream termination for saga {SagaId} after {Attempts} attempts (reason: {Reason}). The ban stands but calls may still be running. Operator action needed",
            state.SagaId, state.StreamAttempts, message.Reason);

        state.StreamsGaveUp = true;
        state.NeedsOperator = true;
        await CompleteAsync(state);
    }

    public async Task Handle(UnbanUserCommand message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var profile = _profiles.GetById(new UserId(message.UserId));
        if (profile is null)
        {
            _logger.LogWarning("Unban {SagaId} ignored: user {UserId} not found", message.SagaId, message.UserId);
            return;
        }

        if (!profile.IsBanned)
        {
            _logger.LogInformation("Unban {SagaId} ignored: user {UserId} is not banned", message.SagaId, message.UserId);
            return;
        }

        var banSaga = await _states.GetLatestByUserId(message.UserId);
        if (banSaga is { IsInProgress: true })
        {
            _logger.LogWarning(
                "Unban {SagaId} rejected: ban saga {BanSagaId} for user {UserId} is still running",
                message.SagaId, banSaga.SagaId, message.UserId);
            return;
        }

        profile.Unban();
        _profiles.Update(profile);

        await _messageClient.PublishAsync(
            new UserUnbannedEvent { SagaId = message.SagaId, UserId = message.UserId }, cancellationToken);

        // Compensation for step 2 only. Calls ended in step 3 cannot be restored, so there
        // is no matching command for the Streaming Service. The command carries the id of
        // the ban saga it compensates, so the answer is recorded in that saga's state.
        await _messageClient.PublishAsync(
            new RestoreChannelMembershipCommand
            {
                SagaId = banSaga?.SagaId ?? message.SagaId,
                UserId = message.UserId
            },
            cancellationToken);
    }

    public async Task Handle(ChannelMembershipRestoredEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var state = await _states.GetById(message.SagaId);
        if (state is null)
        {
            _logger.LogWarning(
                "Channel memberships restored for user {UserId}, but saga {SagaId} is unknown",
                message.UserId, message.SagaId);
            return;
        }

        if (state.MembershipRestored || state.MembershipRestoreFailed)
        {
            return;
        }

        state.MembershipRestored = true;
        await _states.Save(state);

        _logger.LogInformation(
            "Channel memberships restored for user {UserId} (saga {SagaId})", message.UserId, message.SagaId);
    }

    public async Task Handle(ChannelMembershipRestoreFailedEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var state = await _states.GetById(message.SagaId);
        if (state is null)
        {
            _logger.LogError(
                "Restoring channel memberships failed for user {UserId} (reason: {Reason}), and saga {SagaId} is unknown. Operator action needed",
                message.UserId, message.Reason, message.SagaId);
            return;
        }

        if (state.MembershipRestored || state.MembershipRestoreFailed)
        {
            return;
        }

        state.MembershipRestoreFailed = true;
        state.NeedsOperator = true;
        await _states.Save(state);

        _logger.LogError(
            "Restoring channel memberships failed for user {UserId} (saga {SagaId}, reason: {Reason}). Operator action needed",
            message.UserId, message.SagaId, message.Reason);
    }

    private static bool ChannelStepDecided(BanSagaState state) => state.ChannelsRemoved || state.ChannelsGaveUp;

    private static bool StreamStepDecided(BanSagaState state) => state.StreamsTerminated || state.StreamsGaveUp;

    private TimeSpan BackoffFor(int failedAttempts) =>
        BackoffBaseDelay * Math.Pow(2, failedAttempts - 1);

    private async Task<BanSagaState?> LoadActive(Guid sagaId, string messageName)
    {
        var state = await _states.GetById(sagaId);
        if (state is null)
        {
            _logger.LogWarning("{MessageType} for unknown saga {SagaId} ignored", messageName, sagaId);
            return null;
        }

        if (state.IsFailed)
        {
            _logger.LogWarning("{MessageType} for failed saga {SagaId} ignored", messageName, sagaId);
            return null;
        }

        return state;
    }

    private async Task StartStreamStep(BanSagaState state, CancellationToken cancellationToken)
    {
        state.StreamAttempts = 1;
        await _states.Save(state);
        await _messageClient.PublishAsync(
            new TerminateUserStreamsCommand
            {
                SagaId = state.SagaId,
                UserId = state.UserId,
                Attempt = state.StreamAttempts
            },
            cancellationToken);
    }

    private bool IsStaleAttempt(int received, int current, Guid sagaId, string messageName)
    {
        if (received == current)
        {
            return false;
        }

        _logger.LogInformation(
            "{MessageType} for attempt {Received} ignored, saga {SagaId} is at attempt {Current}",
            messageName, received, sagaId, current);
        return true;
    }

    private async Task CompleteAsync(BanSagaState state)
    {
        state.IsCompleted = true;
        state.CompletedAt = DateTimeOffset.UtcNow;
        await _states.Save(state);

        if (state.NeedsOperator)
        {
            _logger.LogWarning("Ban saga {SagaId} completed, but an operator must follow up", state.SagaId);
        }
        else
        {
            _logger.LogInformation("Ban saga {SagaId} completed", state.SagaId);
        }
    }

    private async Task FailAsync(BanSagaState state, string reason, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Ban saga {SagaId} failed for user {UserId}: {Reason}", state.SagaId, state.UserId, reason);

        state.IsFailed = true;
        state.CompletedAt = DateTimeOffset.UtcNow;
        await _states.Save(state);

        await _messageClient.PublishAsync(
            new UserBanFailedEvent { SagaId = state.SagaId, UserId = state.UserId, Reason = reason },
            cancellationToken);
    }
}
