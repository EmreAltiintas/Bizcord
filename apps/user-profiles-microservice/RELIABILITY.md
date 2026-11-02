# Reliability measures: User Service

Task 02, System Integration. Findings and IDs refer to `FAILURE_POINTS.md`.

The service has no HTTP calls, so all measures target the RabbitMQ integration. Polly.Core 8.5.2 is used directly (no `HttpClient` to attach a handler to). All values are bound from the `Resilience` section in `src/UserService.Api/appsettings.json` to `ResilienceOptions`.

## Implemented

### FP-01: Startup depends on the broker

| | |
|---|---|
| Pattern and layer | Retry with exponential backoff and jitter, unlimited attempts, capped delay. `MessageSubscriptionService` is now a `BackgroundService`, so `StartAsync` no longer waits for the broker. |
| Config | `Resilience:Subscribe:InitialDelay` = 00:00:02, `MaxDelay` = 00:00:30 |
| Tests | `MessageSubscriptionServiceTests.StartAsync_ReturnsImmediately_WhenBrokerIsDown`, `Service_KeepsRetrying_UntilSubscribed`. Manual simulation 1. |
| Before | Host start depended on the subscribe call to the broker. Verified in this work: with the broker down the host stopped with an unhandled `TaskCanceledException` from EasyNetQ. |
| Now | API starts and answers requests with the broker down. Subscription is retried every 2 to 30 seconds and logged. |

Polly does not retry `OperationCanceledException` by default, and EasyNetQ throws `TaskCanceledException` when the broker is unreachable. The pipelines therefore use `ShouldHandle = Handle<Exception>()`. Polly still stops retrying when the host token is cancelled.

### FP-04: Consumer failures are not retried

| | |
|---|---|
| Pattern and layer | Retry with a limit, exponential backoff and jitter. `RetryingMessageHandler<T>` decorates `MessagePostedHandler` in the Messaging layer (`AddRetryingHandler`). After the last attempt the exception is rethrown, so the broker's error queue acts as dead letter. |
| Config | `Resilience:Handler:MaxRetryAttempts` = 3, `Delay` = 00:00:01 |
| Tests | `RetryingMessageHandlerTests.Handle_Retries_UntilHandlerSucceeds`, `Handle_Rethrows_AfterMaxRetries_SoBrokerCanDeadLetter`, `Handle_DoesNotRetry_WhenFirstAttemptSucceeds` |
| Before | One failure sent the message straight to the error queue. |
| Now | Up to 3 retries with logged attempts, then the error queue. |

The error queue is the EasyNetQ default. That the rethrown exception ends in that queue was not verified against a running broker in this task.

### FP-08: Duplicate result events on redelivery

| | |
|---|---|
| Pattern and layer | Idempotent consumer. `MessagePostedHandler` skips a `MessageId` it already completed. The id is stored only after the publish succeeds, so a failed attempt is still retried. |
| Config | None. Capacity of the in-memory id set is a constant (10 000, oldest dropped first). |
| Tests | `MessagePostedHandlerTests.Handle_IgnoresRedelivery_OfAlreadyProcessedMessage` |
| Before | Every delivery published a new `UserActivityRecordedEvent`. |
| Now | One result event per `MessageId` per service instance. The set is lost on restart (same limit as FP-03). |

### FP-05: Publish has no timeout or breaker

| | |
|---|---|
| Pattern and layer | Timeout per publish plus Circuit Breaker. `ResilientMessageClient` wraps `EasyNetQMessageClient` and applies the pipeline to `PublishAsync`. The breaker is outside the timeout, so timeouts count as failures. |
| Config | `Resilience:Publish:Timeout` = 00:00:05, `FailureRatio` = 0.5, `MinimumThroughput` = 5, `SamplingDuration` = 00:00:30, `BreakDuration` = 00:00:15 |
| Tests | `ResilientMessageClientTests.Publish_ThrowsTimeout_WhenInnerHangs`, `Publish_OpensCircuit_AfterRepeatedFailures_AndStopsCallingInner`, `Publish_PassesThrough_WhenInnerSucceeds` |
| Before | Publish could hang and kept hitting a dead broker. |
| Now | A publish fails after 5 seconds. After repeated failures the circuit opens and calls fail immediately for 15 seconds. Open and close are logged. |

No fallback is used on purpose. The only publisher is the message handler, and swallowing the error would lose the result event silently. The exception reaches the handler retry (FP-04) and then the error queue.

### FP-06: No health checks

| | |
|---|---|
| Pattern and layer | Health checks in `Program.cs`. `/health/live` has no checks. `/health/ready` runs `MessagingHealthCheck`, which is unhealthy until the subscription exists or while the publish circuit is open. |
| Config | None |
| Tests | `MessageSubscriptionServiceTests.HealthCheck_IsUnhealthy_UntilSubscribed`. Manual simulation 1. |
| Before | No endpoint. |
| Now | `/health/ready` returns 503 without a broker subscription and 200 once subscribed. |

A `healthcheck` entry for `user-service` in compose was not added, because the `aspnet` image has no `curl` or `wget`.

## Not handled

| ID | Reason |
|---|---|
| FP-02 | Needs an outbox. Handler retry (FP-04) re-runs the update and publish, which reduces the gap but does not remove it. |
| FP-03 | Needs a database and a new repository. Larger than a resilience pattern. |
| FP-07 | Not part of the chosen scope. Needs a decision on whether unknown authors are an error. |
| FP-09 | Low severity. No load observed. |
| FP-10 | Low severity. Configuration issue, not a runtime failure. |
| FP-11 | Low severity. Disappears with a real database. |

## Manual simulation

Run from `apps/user-profiles-microservice`.

1. Broker down at startup
   ```bash
   docker compose stop rabbitmq
   docker compose up -d --build --no-deps user-service
   curl -i localhost:5084/health/live
   curl -i localhost:5084/health/ready
   docker logs -f bizcord-user-service
   ```
   Expect: `live` 200 and `ready` 503. Copy `.env.example` to `.env` first (see `SECURITY.md`). Logs repeat `Broker subscription failed (attempt N), retrying in ...` with growing delay.
2. Broker comes back
   ```bash
   docker compose up -d rabbitmq
   curl -i localhost:5084/health/ready
   ```
   Expect: retries stop and `ready` returns 200 within about 30 seconds after the broker is healthy.

Handler retry, dead letter and the publish circuit need a failure inside the handler or the publish call, which cannot be triggered reliably by hand. They are covered by the unit tests above. Manual runs 1 and 2 were executed against the real broker for this task: `ready` returned 503 with the broker stopped and 200 after it restarted, while `live` answered throughout. The REST API also kept answering at that time, before JWT authentication was added.
