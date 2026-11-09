using Microsoft.Extensions.Logging;
using UserService.Application.UserProfiles;
using UserService.Domain.ValueObjects;
using UserService.Messages.Bans;
using UserService.Messaging;

namespace UserService.Application.Sagas.Ban;

/// <summary>
/// Orchestrates the "user is banned" saga. All saga logic lives here; the other services
/// only answer commands.
/// </summary>
/// <remarks>
/// <para>
/// Step 1 marks the profile as banned in this service. It is the point of no return and is
/// never rolled back when step 2 or 3 fails. Step 2 asks the Channel Service to remove the
/// user from all channels, step 3 asks the Streaming Service to end the user's active calls.
/// Both are retried (forward recovery) up to <see cref="BanSagaOptions.MaxStepAttempts"/>
/// attempts with exponential backoff. If a step is given up, <see cref="BanSagaState.NeedsOperator"/>
/// is set and the saga moves on.
/// </para>
/// <para>
/// Compensation exists only for a moderator unban: <see cref="UnbanUserCommand"/> removes the
/// ban and asks the Channel Service to restore the memberships. Calls that were ended in
/// step 3 cannot be restored.
/// </para>
/// <para>
/// Delivery is at-least-once, so every handler ignores a message whose step is already
/// registered in the saga state. Each handler has the signature expected by
/// <see cref="IMessageClient.SubscribeAsync{TMessage}"/> and is used directly there.
/// </para>
/// </remarks>
public sealed class BanSagaOrchestrator :
    IMessageHandler<BanUserCommand>,
    IMessageHandler<UserRemovedFromChannelsEvent>,
    IMessageHandler<ChannelRemovalFailedEvent>,
    IMessageHandler<UserStreamsTerminatedEvent>,
    IMessageHandler<StreamTerminationFailedEvent>,
    IMessageHandler<UnbanUserCommand>,
    IMessageHandler<ChannelMembershipRestoredEvent>
{
    private readonly IUserProfileRepository _profiles;
    private readonly ISagaStateRepository _states;
    private readonly IMessageClient _messageClient;
    private readonly BanSagaOptions _options;
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
        BanSagaOptions options,
        ILogger<BanSagaOrchestrator> logger,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(messageClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _profiles = profiles;
        _states = states;
        _messageClient = messageClient;
        _options = options;
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

        profile.Ban(message.Reason);
        _profiles.Update(profile);

        state.UserBanned = true;
        state.ChannelAttempts = 1;
        await _states.Save(state);

        await _messageClient.PublishAsync(
            new UserBannedEvent { SagaId = state.SagaId, UserId = state.UserId }, cancellationToken);
        await _messageClient.PublishAsync(
            new RemoveUserFromChannelsCommand { SagaId = state.SagaId, UserId = state.UserId }, cancellationToken);
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

        if (state.ChannelAttempts < _options.MaxStepAttempts)
        {
            var wait = BackoffFor(state.ChannelAttempts);
            _logger.LogWarning(
                "Channel removal failed for saga {SagaId} (attempt {Attempt} of {Max}, reason: {Reason}). Retrying in {Delay}",
                state.SagaId, state.ChannelAttempts, _options.MaxStepAttempts, message.Reason, wait);

            await _delay(wait, cancellationToken);

            state.ChannelAttempts++;
            await _states.Save(state);
            await _messageClient.PublishAsync(
                new RemoveUserFromChannelsCommand { SagaId = state.SagaId, UserId = state.UserId },
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

        if (state.StreamAttempts < _options.MaxStepAttempts)
        {
            var wait = BackoffFor(state.StreamAttempts);
            _logger.LogWarning(
                "Stream termination failed for saga {SagaId} (attempt {Attempt} of {Max}, reason: {Reason}). Retrying in {Delay}",
                state.SagaId, state.StreamAttempts, _options.MaxStepAttempts, message.Reason, wait);

            await _delay(wait, cancellationToken);

            state.StreamAttempts++;
            await _states.Save(state);
            await _messageClient.PublishAsync(
                new TerminateUserStreamsCommand { SagaId = state.SagaId, UserId = state.UserId },
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

        profile.Unban();
        _profiles.Update(profile);

        await _messageClient.PublishAsync(
            new UserUnbannedEvent { SagaId = message.SagaId, UserId = message.UserId }, cancellationToken);

        // Compensation for step 2 only. Calls ended in step 3 cannot be restored, so there
        // is no matching command for the Streaming Service.
        await _messageClient.PublishAsync(
            new RestoreChannelMembershipCommand { SagaId = message.SagaId, UserId = message.UserId },
            cancellationToken);
    }

    public Task Handle(ChannelMembershipRestoredEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogInformation(
            "Channel memberships restored for user {UserId} (saga {SagaId})", message.UserId, message.SagaId);

        return Task.CompletedTask;
    }

    private static bool ChannelStepDecided(BanSagaState state) => state.ChannelsRemoved || state.ChannelsGaveUp;

    private static bool StreamStepDecided(BanSagaState state) => state.StreamsTerminated || state.StreamsGaveUp;

    private TimeSpan BackoffFor(int failedAttempts) =>
        _options.BackoffBaseDelay * Math.Pow(2, failedAttempts - 1);

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
            new TerminateUserStreamsCommand { SagaId = state.SagaId, UserId = state.UserId }, cancellationToken);
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
