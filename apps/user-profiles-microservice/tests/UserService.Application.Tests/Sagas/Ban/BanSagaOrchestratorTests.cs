using Microsoft.Extensions.Logging.Abstractions;
using UserService.Application.Sagas.Ban;
using UserService.Application.UserProfiles;
using UserService.Domain.Entities;
using UserService.Domain.ValueObjects;
using UserService.Messages.Bans;
using UserService.Messaging;

namespace UserService.Application.Tests.Sagas.Ban;

public class BanSagaOrchestratorTests
{
    private sealed class RecordingMessageClient : IMessageClient
    {
        public List<object> Published { get; } = new();

        public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class
        {
            Published.Add(message);
            return Task.CompletedTask;
        }

        public Task SubscribeAsync<TMessage>(
            string subscriptionId,
            Func<TMessage, CancellationToken, Task> onMessage,
            CancellationToken cancellationToken = default)
            where TMessage : class => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public int Count<TMessage>() => Published.OfType<TMessage>().Count();
    }

    private sealed class Fixture
    {
        public InMemoryUserProfileRepository Profiles { get; } = new();

        public InMemorySagaStateRepository States { get; } = new();

        public RecordingMessageClient Client { get; } = new();

        public List<TimeSpan> Delays { get; } = new();

        public UserProfile Profile { get; } = UserProfile.Create(UserId.New(), DisplayName.Create("Emre"));

        public BanSagaOrchestrator Orchestrator { get; }

        public Fixture()
        {
            Profiles.Add(Profile);
            Orchestrator = new BanSagaOrchestrator(
                Profiles,
                States,
                Client,
                NullLogger<BanSagaOrchestrator>.Instance,
                (delay, _) =>
                {
                    Delays.Add(delay);
                    return Task.CompletedTask;
                });
        }

        public Guid ProfileId => Profile.Id.Value;

        public async Task<Guid> StartBan(string reason = "spam")
        {
            var command = new BanUserCommand { UserId = ProfileId, ModeratorId = Guid.NewGuid(), Reason = reason };
            await Orchestrator.Handle(command, CancellationToken.None);
            return command.SagaId;
        }

        public Task ChannelsRemoved(Guid sagaId) =>
            Orchestrator.Handle(new UserRemovedFromChannelsEvent { SagaId = sagaId, UserId = ProfileId }, CancellationToken.None);

        // Fails the attempt the saga is currently waiting for, unless a specific attempt is given.
        public async Task ChannelRemovalFailed(Guid sagaId, int? attempt = null) =>
            await Orchestrator.Handle(
                new ChannelRemovalFailedEvent
                {
                    SagaId = sagaId,
                    UserId = ProfileId,
                    Attempt = attempt ?? (await State(sagaId)).ChannelAttempts,
                    Reason = "down"
                },
                CancellationToken.None);

        public Task StreamsTerminated(Guid sagaId) =>
            Orchestrator.Handle(new UserStreamsTerminatedEvent { SagaId = sagaId, UserId = ProfileId }, CancellationToken.None);

        public async Task StreamTerminationFailed(Guid sagaId, int? attempt = null) =>
            await Orchestrator.Handle(
                new StreamTerminationFailedEvent
                {
                    SagaId = sagaId,
                    UserId = ProfileId,
                    Attempt = attempt ?? (await State(sagaId)).StreamAttempts,
                    Reason = "down"
                },
                CancellationToken.None);

        public async Task<BanSagaState> State(Guid sagaId) =>
            (await States.GetById(sagaId))!;
    }

    [Fact]
    public async Task Happy_path_completes_without_operator()
    {
        var f = new Fixture();

        var sagaId = await f.StartBan();
        await f.ChannelsRemoved(sagaId);
        await f.StreamsTerminated(sagaId);

        var state = await f.State(sagaId);
        Assert.True(state.UserBanned);
        Assert.True(state.ChannelsRemoved);
        Assert.True(state.StreamsTerminated);
        Assert.True(state.IsCompleted);
        Assert.NotNull(state.CompletedAt);
        Assert.False(state.IsFailed);
        Assert.False(state.NeedsOperator);
        Assert.True(f.Profile.IsBanned);
        Assert.Equal("spam", f.Profile.BanReason);
        Assert.Equal(1, f.Client.Count<UserBannedEvent>());
        Assert.Equal(1, f.Client.Count<RemoveUserFromChannelsCommand>());
        Assert.Equal(1, f.Client.Count<TerminateUserStreamsCommand>());
    }

    [Fact]
    public async Task Messages_carry_the_saga_id_and_user_id()
    {
        var f = new Fixture();

        var sagaId = await f.StartBan();
        await f.ChannelsRemoved(sagaId);

        var remove = f.Client.Published.OfType<RemoveUserFromChannelsCommand>().Single();
        var terminate = f.Client.Published.OfType<TerminateUserStreamsCommand>().Single();
        Assert.Equal((sagaId, f.ProfileId), (remove.SagaId, remove.UserId));
        Assert.Equal((sagaId, f.ProfileId), (terminate.SagaId, terminate.UserId));
        Assert.Equal(1, remove.Attempt);
        Assert.Equal(1, terminate.Attempt);
    }

    [Fact]
    public async Task Unknown_user_fails_the_saga_and_sends_no_further_commands()
    {
        var f = new Fixture();
        var command = new BanUserCommand { UserId = Guid.NewGuid(), ModeratorId = Guid.NewGuid(), Reason = "spam" };

        await f.Orchestrator.Handle(command, CancellationToken.None);

        var state = await f.State(command.SagaId);
        Assert.True(state.IsFailed);
        Assert.False(state.UserBanned);
        var failed = Assert.IsType<UserBanFailedEvent>(Assert.Single(f.Client.Published));
        Assert.Equal(command.SagaId, failed.SagaId);
        Assert.Equal(command.UserId, failed.UserId);
        Assert.NotEmpty(failed.Reason);
        Assert.Equal(0, f.Client.Count<RemoveUserFromChannelsCommand>());
    }

    [Fact]
    public async Task Blank_reason_fails_the_saga_without_banning()
    {
        var f = new Fixture();

        var sagaId = await f.StartBan(reason: " ");

        Assert.True((await f.State(sagaId)).IsFailed);
        Assert.False(f.Profile.IsBanned);
        Assert.Equal(1, f.Client.Count<UserBanFailedEvent>());
    }

    [Fact]
    public async Task Channel_step_is_retried_with_backoff_until_it_succeeds()
    {
        var f = new Fixture();
        var sagaId = await f.StartBan();

        await f.ChannelRemovalFailed(sagaId);
        await f.ChannelRemovalFailed(sagaId);
        await f.ChannelsRemoved(sagaId);

        var state = await f.State(sagaId);
        Assert.Equal(3, state.ChannelAttempts);
        Assert.True(state.ChannelsRemoved);
        Assert.False(state.ChannelsGaveUp);
        Assert.False(state.NeedsOperator);
        Assert.Equal(3, f.Client.Count<RemoveUserFromChannelsCommand>());
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, f.Delays);
        Assert.Equal(1, f.Client.Count<TerminateUserStreamsCommand>());
    }

    [Fact]
    public async Task Channel_step_gives_up_after_three_attempts_and_the_saga_moves_on()
    {
        var f = new Fixture();
        var sagaId = await f.StartBan();

        await f.ChannelRemovalFailed(sagaId);
        await f.ChannelRemovalFailed(sagaId);
        await f.ChannelRemovalFailed(sagaId);

        var state = await f.State(sagaId);
        Assert.True(state.ChannelsGaveUp);
        Assert.False(state.ChannelsRemoved);
        Assert.True(state.NeedsOperator);
        Assert.False(state.IsCompleted);
        Assert.Equal(3, f.Client.Count<RemoveUserFromChannelsCommand>());
        Assert.Equal(1, f.Client.Count<TerminateUserStreamsCommand>());
        Assert.True(f.Profile.IsBanned);

        await f.StreamsTerminated(sagaId);

        Assert.True((await f.State(sagaId)).IsCompleted);
        Assert.True((await f.State(sagaId)).NeedsOperator);
    }

    [Fact]
    public async Task Streaming_step_gives_up_but_the_ban_stands_and_an_operator_is_needed()
    {
        var f = new Fixture();
        var sagaId = await f.StartBan();
        await f.ChannelsRemoved(sagaId);

        await f.StreamTerminationFailed(sagaId);
        await f.StreamTerminationFailed(sagaId);
        await f.StreamTerminationFailed(sagaId);

        var state = await f.State(sagaId);
        Assert.True(f.Profile.IsBanned);
        Assert.True(state.UserBanned);
        Assert.True(state.StreamsGaveUp);
        Assert.False(state.StreamsTerminated);
        Assert.True(state.NeedsOperator);
        Assert.True(state.IsCompleted);
        Assert.False(state.IsFailed);
        Assert.Equal(3, f.Client.Count<TerminateUserStreamsCommand>());
    }

    [Fact]
    public async Task Duplicate_ban_command_starts_the_saga_only_once()
    {
        var f = new Fixture();
        var command = new BanUserCommand { UserId = f.ProfileId, ModeratorId = Guid.NewGuid(), Reason = "spam" };

        await f.Orchestrator.Handle(command, CancellationToken.None);
        await f.Orchestrator.Handle(command, CancellationToken.None);

        Assert.Equal(1, f.Client.Count<UserBannedEvent>());
        Assert.Equal(1, f.Client.Count<RemoveUserFromChannelsCommand>());
    }

    [Fact]
    public async Task Duplicate_events_do_not_change_state_or_repeat_commands()
    {
        var f = new Fixture();
        var sagaId = await f.StartBan();

        await f.ChannelsRemoved(sagaId);
        await f.ChannelsRemoved(sagaId);
        await f.ChannelRemovalFailed(sagaId);
        await f.StreamsTerminated(sagaId);
        var completedAt = (await f.State(sagaId)).CompletedAt;
        var publishedBefore = f.Client.Published.Count;

        await f.StreamsTerminated(sagaId);
        await f.StreamTerminationFailed(sagaId);
        await f.ChannelsRemoved(sagaId);

        var after = await f.State(sagaId);
        Assert.Equal(1, f.Client.Count<TerminateUserStreamsCommand>());
        Assert.Equal(publishedBefore, f.Client.Published.Count);
        Assert.Equal(completedAt, after.CompletedAt);
        Assert.Equal(1, after.ChannelAttempts);
        Assert.Equal(1, after.StreamAttempts);
        Assert.False(after.NeedsOperator);
        Assert.Empty(f.Delays);
    }

    [Fact]
    public async Task Event_for_an_unknown_saga_is_ignored()
    {
        var f = new Fixture();

        await f.ChannelsRemoved(Guid.NewGuid());

        Assert.Empty(f.Client.Published);
    }

    [Fact]
    public async Task Unban_removes_the_ban_and_asks_the_channel_service_to_restore_memberships()
    {
        var f = new Fixture();
        var banSaga = await f.StartBan();
        await f.ChannelsRemoved(banSaga);
        await f.StreamsTerminated(banSaga);
        f.Client.Published.Clear();
        var unban = new UnbanUserCommand { UserId = f.ProfileId, ModeratorId = Guid.NewGuid() };

        await f.Orchestrator.Handle(unban, CancellationToken.None);

        Assert.False(f.Profile.IsBanned);
        var unbanned = f.Client.Published.OfType<UserUnbannedEvent>().Single();
        var restore = f.Client.Published.OfType<RestoreChannelMembershipCommand>().Single();
        Assert.Equal((unban.SagaId, f.ProfileId), (unbanned.SagaId, unbanned.UserId));
        Assert.Equal((banSaga, f.ProfileId), (restore.SagaId, restore.UserId));
        Assert.Equal(0, f.Client.Count<TerminateUserStreamsCommand>());
    }

    [Fact]
    public async Task Unban_is_rejected_while_the_ban_saga_is_running()
    {
        var f = new Fixture();
        await f.StartBan();
        f.Client.Published.Clear();

        await f.Orchestrator.Handle(
            new UnbanUserCommand { UserId = f.ProfileId, ModeratorId = Guid.NewGuid() }, CancellationToken.None);

        Assert.True(f.Profile.IsBanned);
        Assert.Empty(f.Client.Published);
    }

    [Fact]
    public async Task Ban_of_an_already_banned_user_fails_the_saga_without_new_commands()
    {
        var f = new Fixture();
        var first = await f.StartBan();
        await f.ChannelsRemoved(first);
        await f.StreamsTerminated(first);
        f.Client.Published.Clear();

        var second = await f.StartBan();

        Assert.True((await f.State(second)).IsFailed);
        Assert.Equal(1, f.Client.Count<UserBanFailedEvent>());
        Assert.Equal(0, f.Client.Count<RemoveUserFromChannelsCommand>());
    }

    [Fact]
    public async Task Redelivered_failure_for_an_old_attempt_does_not_use_up_a_retry()
    {
        var f = new Fixture();
        var sagaId = await f.StartBan();

        await f.ChannelRemovalFailed(sagaId, attempt: 1);
        await f.ChannelRemovalFailed(sagaId, attempt: 1);

        var state = await f.State(sagaId);
        Assert.Equal(2, state.ChannelAttempts);
        Assert.Equal(2, f.Client.Count<RemoveUserFromChannelsCommand>());
        Assert.Single(f.Delays);
        Assert.Equal(2, f.Client.Published.OfType<RemoveUserFromChannelsCommand>().Last().Attempt);
    }

    [Fact]
    public async Task Redelivered_stream_failure_for_an_old_attempt_is_ignored()
    {
        var f = new Fixture();
        var sagaId = await f.StartBan();
        await f.ChannelsRemoved(sagaId);

        await f.StreamTerminationFailed(sagaId, attempt: 1);
        await f.StreamTerminationFailed(sagaId, attempt: 1);

        Assert.Equal(2, (await f.State(sagaId)).StreamAttempts);
        Assert.Equal(2, f.Client.Count<TerminateUserStreamsCommand>());
    }

    [Fact]
    public async Task Restoring_memberships_is_recorded_in_the_ban_saga()
    {
        var f = new Fixture();
        var sagaId = await f.StartBan();
        await f.ChannelsRemoved(sagaId);
        await f.StreamsTerminated(sagaId);

        await f.Orchestrator.Handle(
            new ChannelMembershipRestoredEvent { SagaId = sagaId, UserId = f.ProfileId }, CancellationToken.None);

        var state = await f.State(sagaId);
        Assert.True(state.MembershipRestored);
        Assert.False(state.NeedsOperator);
    }

    [Fact]
    public async Task Failed_restore_of_memberships_needs_an_operator_and_is_not_retried()
    {
        var f = new Fixture();
        var sagaId = await f.StartBan();
        await f.ChannelsRemoved(sagaId);
        await f.StreamsTerminated(sagaId);
        f.Client.Published.Clear();

        await f.Orchestrator.Handle(
            new ChannelMembershipRestoreFailedEvent { SagaId = sagaId, UserId = f.ProfileId, Reason = "down" },
            CancellationToken.None);

        var state = await f.State(sagaId);
        Assert.True(state.MembershipRestoreFailed);
        Assert.False(state.MembershipRestored);
        Assert.True(state.NeedsOperator);
        Assert.Empty(f.Client.Published);
        Assert.Empty(f.Delays);
    }

    [Fact]
    public async Task Duplicate_unban_does_not_publish_again()
    {
        var f = new Fixture();
        var banSaga = await f.StartBan();
        await f.ChannelsRemoved(banSaga);
        await f.StreamsTerminated(banSaga);
        var unban = new UnbanUserCommand { UserId = f.ProfileId, ModeratorId = Guid.NewGuid() };

        await f.Orchestrator.Handle(unban, CancellationToken.None);
        await f.Orchestrator.Handle(unban, CancellationToken.None);

        Assert.Equal(1, f.Client.Count<UserUnbannedEvent>());
        Assert.Equal(1, f.Client.Count<RestoreChannelMembershipCommand>());
    }
}
