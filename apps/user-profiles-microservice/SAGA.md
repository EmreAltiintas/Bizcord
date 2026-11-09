# Ban saga: User Service

Task 01 (Saga Workflow Scenarios) and Task 02 (Implement the Saga pattern).

Scenario: a moderator bans a user. The User Service orchestrates the work across three services. Only the User Service exists in this repo, so the Channel and Streaming services are simulated by stubs (`src/SagaStubs.Worker`). The saga is built on our own `IMessageClient`, with no saga framework.

## Steps and messages

All messages are in `UserService.Messages.Bans`. Every message carries `SagaId` and `UserId`.

| Step | Owner | Command | Success event | Failure event | On failure |
|---|---|---|---|---|---|
| 1. Mark the user as banned | User Service (local) | `BanUserCommand` | `UserBannedEvent` | `UserBanFailedEvent` (unknown user, blank reason) | Saga stops, `IsFailed` is set. Nothing to undo. |
| 2. Remove the user from all channels | Channel Service | `RemoveUserFromChannelsCommand` | `UserRemovedFromChannelsEvent` | `ChannelRemovalFailedEvent` | Retry up to 3 attempts, then `NeedsOperator` and continue to step 3 |
| 3. End the user's active calls | Streaming Service | `TerminateUserStreamsCommand` | `UserStreamsTerminatedEvent` | `StreamTerminationFailedEvent` | Retry up to 3 attempts, then `NeedsOperator` and complete the saga |
| Unban (compensation) | User Service | `UnbanUserCommand` | `UserUnbannedEvent` | none | Idempotent, a repeated unban is ignored |
| Restore memberships | Channel Service | `RestoreChannelMembershipCommand` | `ChannelMembershipRestoredEvent` | none | The orchestrator only logs the confirmation |

Step 1 is the point of no return. It is never rolled back when step 2 or 3 fails. Steps 2 and 3 use forward recovery: retry, and if that does not help, hand over to an operator.

Retries wait 1 second before the second attempt and 2 seconds before the third (`BanSaga:BackoffBaseDelay`, doubled each time). `BanSaga:MaxStepAttempts` is 3 and counts the first attempt.

The saga is completed (`IsCompleted`) when steps 2 and 3 are both decided, meaning each either succeeded or was given up. `NeedsOperator` tells whether anything was given up.

```
POST /ban ──> BanUserCommand
                 │  step 1: Ban(), save profile
                 ├─> UserBannedEvent
                 └─> RemoveUserFromChannelsCommand ──> Channel Service
                                                        │
        UserRemovedFromChannelsEvent / ChannelRemovalFailedEvent
                 │  (failed: wait, resend, at most 3 attempts)
                 └─> TerminateUserStreamsCommand ──> Streaming Service
                                                        │
        UserStreamsTerminatedEvent / StreamTerminationFailedEvent
                 └─> IsCompleted (NeedsOperator if a step was given up)
```

Subscription ids all start with `user-service.` and are kebab-case (`user-service.ban-user`, `user-service.channel-removal-failed`, and so on). The stubs use `channel-stub.` and `streaming-stub.`. Because every service has its own id, each receives its own copy of a message.

## The hard question: the ban succeeds, but streaming fails

The ban stands. The user is marked as banned in the User Service and that is not rolled back, for two reasons:

- Rolling back would let a banned user back in because of a problem in a different service. The moderator's decision is the source of truth.
- Steps 2 and 3 are side effects of the ban, not preconditions for it. They can be repeated safely, and the ban can be completed later.

What happens in the saga:

1. The streaming step is attempted up to 3 times with exponential backoff.
2. After the third failure the orchestrator logs an error, sets `StreamsGaveUp` and `NeedsOperator`, and completes the saga. `IsFailed` stays false because the ban itself succeeded.
3. An operator reads the saga with `GET /api/user-profiles/sagas/{sagaId}`, sees `needsOperator: true` and `streamsGaveUp: true`, and ends the calls by hand.

`IsBanned` is stored on the profile, but nothing in the User Service enforces it yet and `UserProfileContract` does not expose it. A call that is still running keeps running until the operator ends it. No endpoint lists sagas that need an operator, so the saga id has to be known.

## Why orchestration instead of choreography

All saga logic is in one class, `BanSagaOrchestrator`. The Channel and Streaming services only answer a command with a success or failure event. They know nothing about each other or about the ban.

With choreography, each service would react to the previous service's event. That would be harder here:

- The sequence (ban, then channels, then streaming) would be spread over three services. Finding out why a step did not run means reading three code bases owned by different teams.
- The retry and give-up rules would have to be implemented and kept in sync in every service, or one service would need to know the others' limits.
- `NeedsOperator` is a decision about the whole saga. No single choreographed service knows that both steps are decided.
- Adding a step, for example removing the user from voice rooms, would mean changing a neighbour's subscription instead of one method.
- The other teams' services do not exist yet. With orchestration they only need to implement one command and one pair of answers each.

The price is that the User Service knows the other services' commands and is a single point of coordination.

## Where the state is, and why

`BanSagaState` is kept by `ISagaStateRepository`, implemented by `InMemorySagaStateRepository` (a `ConcurrentDictionary`, registered as a singleton). The orchestrator is the only writer.

The state lives in the User Service because the User Service owns the orchestrator, and the orchestrator needs to know which steps are already registered to be idempotent. The profile's own ban data (`IsBanned`, `BanReason`, `BannedAtUtc`) is domain state on `UserProfile` and is separate from the saga's progress.

In-memory is a deliberate limitation: the service has no database, as with `InMemoryUserProfileRepository`. See the limitations below.

## Idempotency

Delivery is at-least-once, so every handler ignores a message whose step is already registered:

- A second `BanUserCommand` with an existing `SagaId` is ignored.
- A success or failure event for a step that is already succeeded or given up is ignored. An event for an unknown or failed saga is ignored with a warning.
- A second `UnbanUserCommand` for a user who is not banned is ignored.
- `UserProfile.Ban` and `UserProfile.Unban` do nothing when the profile is already in that state.

## Configuration

| Key | Default | Meaning |
|---|---|---|
| `BanSaga:MaxStepAttempts` | 3 | Attempts per step, including the first |
| `BanSaga:BackoffBaseDelay` | 00:00:01 | Delay before the first retry, doubled for each further one |
| `Stub:Role` | none | `channel` or `streaming` (stubs only) |
| `Stub:FailFirstAttempts` | 0 | Stub fails the first N attempts per saga id. 0 never fails, 99 always fails |

In `docker-compose.yaml` the failures are set with `CHANNEL_FAIL_FIRST_ATTEMPTS` and `STREAMING_FAIL_FIRST_ATTEMPTS`.

## Running the demo

```bash
docker compose up -d rabbitmq
CHANNEL_FAIL_FIRST_ATTEMPTS=2 STREAMING_FAIL_FIRST_ATTEMPTS=0 docker compose up -d --force-recreate channel-stub streaming-stub
# start the User Service (see README.md), then, with a token that has the internal-service role:
curl -X POST http://localhost:5084/api/user-profiles/<user-id>/ban \
  -H "Authorization: Bearer <token>" -H "Content-Type: application/json" -d '{"reason":"spam"}'
curl http://localhost:5084/api/user-profiles/sagas/<saga-id> -H "Authorization: Bearer <token>"
```

| Channel fails first | Streaming fails first | Result |
|---|---|---|
| 0 | 0 | `channelAttempts` 1, `streamAttempts` 1, `needsOperator` false |
| 2 | 0 | `channelAttempts` 3, `channelsRemoved` true, `needsOperator` false |
| 0 | 99 | `streamAttempts` 3, `streamsGaveUp` true, `needsOperator` true, `userBanned` true |

The caller's `sub` claim becomes the `ModeratorId`.

## Endpoints

| Endpoint | Policy | Result |
|---|---|---|
| `POST /api/user-profiles/{id}/ban` with `{ "reason": "..." }` | `InternalService` | 202 with `{ "sagaId": "..." }` and a `Location` header, 400 without reason or valid `sub`, 503 if the broker is down |
| `POST /api/user-profiles/{id}/unban` | `InternalService` | 202 with `{ "sagaId": "..." }` |
| `GET /api/user-profiles/sagas/{sagaId}` | `InternalService` | 200 with the saga state, 404 if unknown |

The ban endpoint does not check that the profile exists. An unknown user makes the saga fail (`isFailed: true`, `UserBanFailedEvent`), which can be read from the saga state.

## Known limitations

- **In-memory state.** Saga state is lost on restart and works with one instance only. A saga that is in flight at restart is not resumed, and a second instance would not see the first one's state or its processed steps. A database table keyed by `SagaId` would fix it.
- **No outbox.** The orchestrator saves state and publishes in separate steps, and `Ban` updates the profile before the event is published. A crash in between leaves the state and the broker out of step. This is the same kind of gap as FP-02 in `FAILURE_POINTS.md`.
- **Retry with `Task.Delay` in the handler.** The wait for a retry happens inside the message handler, so it holds a consumer slot while it sleeps, and a restart during the wait loses the retry. A scheduled message or a timer table would avoid it.
- **No timeout for a missing answer.** If a stub never answers, the saga stays at that step forever. Failure events are retried, silence is not. There is no saga timeout or sweeper.
- **Duplicate failure events count as attempts.** Events carry no attempt number, so a redelivered `ChannelRemovalFailedEvent` or `StreamTerminationFailedEvent` that arrives while the step is still open is treated as a new failure.
- **No locking per saga.** Messages for one saga are meant to arrive one after another, and no lock protects the state against two handlers touching it at the same time.
- **Error queue.** The orchestrator's handlers are not wrapped in `RetryingMessageHandler`, because the saga has its own retry. An exception thrown from a handler goes to the broker's error queue.
- **JWTs are stateless.** A token that was issued before the ban stays valid until it expires. Sessions cannot be invalidated from here. Other services have to check the ban status themselves, for example by calling the User Service or by consuming `UserBannedEvent`. Nothing does that yet.
- **Ban status is not enforced in the User Service.** `IsBanned` is not checked by any endpoint and is not part of `UserProfileContract`.
- **Unban cannot restore everything.** `RestoreChannelMembershipCommand` brings back channel memberships. Calls that were ended in step 3 are gone and cannot be restored.
- **Readiness.** `/health/ready` reports on the presence subscription only. It does not wait for `BanSagaSubscriptionService`.
