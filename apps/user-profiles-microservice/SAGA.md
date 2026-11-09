# Ban saga: User Service

Scenario: a moderator bans a user. The User Service orchestrates the work in `BanSagaOrchestrator` using our own `IMessageClient`. The Channel and Streaming services belong to other teams, so `src/SagaStubs.Worker` simulates them. Messages are in `UserService.Messages.Bans` and all carry `SagaId` and `UserId`.

## Steps and compensation

| Step | Command | Success / failure event | On failure | Compensation |
|---|---|---|---|---|
| 1. Mark user as banned (User Service) | `BanUserCommand` | `UserBannedEvent` / `UserBanFailedEvent` | Saga stops | `UnbanUserCommand` (moderator only) |
| 2. Remove from channels (Channel) | `RemoveUserFromChannelsCommand` | `UserRemovedFromChannelsEvent` / `ChannelRemovalFailedEvent` | Retry, 3 attempts with backoff, then `NeedsOperator` and go on | `RestoreChannelMembershipCommand` after unban |
| 3. End calls (Streaming) | `TerminateUserStreamsCommand` | `UserStreamsTerminatedEvent` / `StreamTerminationFailedEvent` | Same as step 2, then the saga completes | None, the call is gone |

Step 1 is the point of no return and is never rolled back when step 2 or 3 fails. Retries wait 1 s and 2 s. Nothing is compensated automatically, only a moderator unban compensates.

When a compensation is impossible we log it and alert an operator (`NeedsOperator`). For an ended call, the other participants only see that the user left. Telling them why is a job for the Streaming service.

## The hard question: the ban succeeds, streaming fails

The user is banned. Rolling back would let a banned user in again because of a problem in another service. After 3 failed attempts the saga is completed with `StreamsGaveUp` and `NeedsOperator` set, and an operator ends the call by hand. The state can be read with `GET /api/user-profiles/sagas/{sagaId}`.

## Orchestration instead of choreography

All saga logic is in one class. The other services only answer a command, and know nothing about each other. With choreography the order, the retry rules and the "operator needed" decision would be spread over three services owned by different teams, and adding a step would mean changing a neighbour.

## State

`BanSagaState` is kept in `InMemorySagaStateRepository`, because the service has no database. Handlers ignore a message whose step is already registered, since delivery is at-least-once.

## Demo

```bash
CHANNEL_FAIL_FIRST_ATTEMPTS=2 STREAMING_FAIL_FIRST_ATTEMPTS=0 docker compose up -d --force-recreate channel-stub streaming-stub
```

Then `POST /api/user-profiles/{id}/ban` with `{ "reason": "spam" }` and a token with the `internal-service` role. Use 99 as the value to make a stub always fail.

## Known limitations

- State is lost on restart and only works with one instance.
- No outbox. A crash between saving state and publishing leaves them out of step (same type of gap as FP-02).
- Retry uses `Task.Delay` inside the handler.
- No timeout if an answer never comes.
- JWTs are stateless, so sessions are not invalidated. Other services must check the ban status.
- `IsBanned` is not enforced by any endpoint yet.
- No system message is posted in the channels. That belongs to the Channel service.
