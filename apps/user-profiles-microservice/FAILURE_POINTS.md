# Failure Points: User Service

Task 01, System Integration. Status column updated after Task 02, see `RELIABILITY.md`.

## Scope and dependency map

Service: `apps/user-profiles-microservice` (UserService.Api).

| Dependency | Direction | Mechanism | Where |
|---|---|---|---|
| RabbitMQ | Consumes `MessagePostedEvent` | EasyNetQ 8.1.7 pub/sub | `MessageSubscriptionService.cs:18-20` |
| RabbitMQ | Publishes `UserActivityRecordedEvent` | EasyNetQ pub/sub | `MessagePostedHandler.cs:34-42` |
| API gateway | Calls this service over REST | Sets `X-UserId` header | `UserProfilesController.cs:33-40` |
| Messaging Service | Upstream producer of `MessagePostedEvent` | Via RabbitMQ only | `docs/analysis-and-design.md:21` |

What this service does not have: no `HttpClient`, no gRPC, no database, no third-party API. The only outbound dependency is RabbitMQ, and profile data lives in memory. Sections 1 and 3 of the task therefore reduce to the broker and the in-memory store, and no HTTP timeout or retry findings exist because there are no HTTP calls.

## Summary

| ID | Title | Severity | Pattern (Task 02) | Status |
|---|---|---|---|---|
| FP-01 | Startup fails or hangs if RabbitMQ is down | High | Retry with backoff, startup in background | Mitigated |
| FP-02 | Profile update and event publish are not atomic | High | Outbox (other) | Open |
| FP-03 | All profile data lost on restart | High | Persistent store (other) | Open |
| FP-04 | Failed consumer messages are not retried and pile up in the error queue | Medium | Retry, dead-letter | Mitigated |
| FP-05 | Publish has no timeout, no confirm and no fallback | Medium | Timeout, Fallback | Mitigated |
| FP-06 | No health checks, so orchestration cannot see a broken service | Medium | Health checks (other) | Mitigated |
| FP-07 | Events for unknown profiles are silently acked and dropped | Medium | Fallback, dead-letter | Open |
| FP-08 | Redelivery causes duplicate `UserActivityRecordedEvent` | Medium | Idempotent consumer (other) | Mitigated |
| FP-09 | Consumer concurrency is unbounded and shares threads with publishing | Low | Bulkhead | Open |
| FP-10 | Silent fallback to `host=localhost` and hardcoded guest credentials | Low | Configuration validation (other) | Open |
| FP-11 | Read-modify-write on shared mutable entity without locking | Low | Concurrency control (other) | Open |

## Details

### FP-01: Startup fails or hangs if RabbitMQ is down

- **Location:** `MessageSubscriptionService.cs:18-20`, `EasyNetQMessageClient.cs:35-41` and `:64-66`, `Program.cs:18-22`
- **Description:** `StartAsync` awaits `SubscribeAsync`, which must declare the exchange and queue on the broker before returning. If RabbitMQ is not reachable yet, this call can throw or wait, which blocks or aborts host startup.
- **Consequence:** The whole API, including the REST endpoints that do not need the broker, does not come up. In a container restart after a broker outage, this service can crash-loop or never become ready. The comment at `EasyNetQMessageClient.cs:35-36` says EasyNetQ "retries in the background", but that only covers building the bus, not the subscribe call.
- **Severity:** High. A dependency outage at startup takes down the entire service, and the REST API is unrelated to the broker.
- **Pattern:** Retry with exponential backoff and jitter, run subscription setup in a background loop so the host starts regardless. Application/hosting layer (`MessageSubscriptionService`).
- **Status:** Mitigated

### FP-02: Profile update and event publish are not atomic

- **Location:** `MessagePostedHandler.cs:31-42`
- **Description:** The handler changes `LastActiveAtUtc` and calls `_repository.Update` first, then publishes `UserActivityRecordedEvent` as a separate step. If the publish fails, the state change stays and the event is never sent.
- **Consequence:** Downstream consumers of `UserActivityRecordedEvent` never learn about the activity. State and events diverge with no way to detect it.
- **Severity:** High. Silent data inconsistency between this service and others.
- **Pattern:** Outbox (write event with the state change, publish from a relay), combined with Retry on the relay. Application/infrastructure layer.
- **Status:** Open

### FP-03: All profile data lost on restart

- **Location:** `ApplicationServiceCollectionExtensions.cs:14`, `InMemoryUserProfileRepository.cs:9`
- **Description:** Profiles are stored in a `ConcurrentDictionary` registered as a singleton. A restart, crash or redeploy empties it.
- **Consequence:** Every profile created through `POST /api/user-profiles` disappears, `GET` returns 404, and every later `MessagePostedEvent` is dropped (see FP-07). Running two replicas gives each its own diverging data set.
- **Severity:** High. Total data loss on the most ordinary operational event. The design doc (`docs/analysis-and-design.md:40`) already lists a database as a later step.
- **Pattern:** Persistent database behind `IUserProfileRepository`. When added, it brings its own failure points: command timeout, connection pool limits, migrations at startup. Infrastructure layer.
- **Status:** Open

### FP-04: Failed consumer messages are not retried and pile up in the error queue

- **Location:** `MessageSubscriptionService.cs:19-20`, `EasyNetQMessageClient.cs:64-66` (configuration callback `_ => { }`)
- **Description:** The handler has no try/catch and the subscription uses default EasyNetQ settings. An exception in `Handle` (for example a failed publish at `MessagePostedHandler.cs:34`) is not retried by this code, and EasyNetQ moves the message to its default error queue.
- **Consequence:** A transient fault turns into a permanently failed message that needs manual requeueing. Nothing monitors or drains the error queue.
- **Severity:** Medium. Messages are not lost outright, but recovery is manual.
- **Pattern:** Retry with backoff and jitter inside the handler, then a dead-letter queue with an alert. Messaging layer (`EasyNetQMessageClient`).
- **Status:** Mitigated

### FP-05: Publish has no timeout, no confirm and no fallback

- **Location:** `EasyNetQMessageClient.cs:44-50`, `MessagePostedHandler.cs:34-42`
- **Description:** `PublishAsync` is passed only the caller's `CancellationToken`. Nothing in this service limits how long a publish may take, and no code checks that the broker accepted the message. The handler's token comes from the consumer, so it is not a time limit either.
- **Consequence:** A slow or unreachable broker can hold the consumer thread and with it message processing. Whether a publish is lost without error when the broker is down depends on EasyNetQ defaults (see open questions).
- **Severity:** Medium. Affects an event path, not user-facing REST calls.
- **Pattern:** Timeout (linked `CancellationTokenSource`), Fallback (log and park the event for later), Circuit Breaker around publish. Messaging layer.
- **Status:** Mitigated

### FP-06: No health checks, so orchestration cannot see a broken service

- **Location:** `Program.cs:9-39` (no `AddHealthChecks` or `MapHealthChecks`), `Dockerfile:30-33` (no `HEALTHCHECK`), `docker-compose.yaml:16-28` (no `healthcheck` on `user-service`)
- **Description:** The service exposes no liveness or readiness endpoint. Only RabbitMQ has a health check in compose, and `user-service` depends on it only at start (`depends_on`, line 26-28).
- **Consequence:** If the broker connection drops after startup, the service still looks healthy and keeps accepting REST calls while its event handling is dead. Other services and the orchestrator cannot tell.
- **Severity:** Medium. Failures stay invisible until someone notices missing events.
- **Pattern:** Health checks (readiness covering broker connection), plus restart policy in compose. API/hosting layer.
- **Status:** Mitigated

### FP-07: Events for unknown profiles are silently acked and dropped

- **Location:** `MessagePostedHandler.cs:24-28`
- **Description:** If no profile exists for `AuthorId`, the handler returns normally, so EasyNetQ acks the message. No log line, no error, no parking.
- **Consequence:** Combined with FP-03 (restart empties the store) or a race where the first message arrives before the profile is created, activity is permanently lost and no `UserActivityRecordedEvent` is published for that `MessageId`.
- **Severity:** Medium. Data loss that is invisible in logs.
- **Pattern:** Fallback (log with MessageId, route to a parking queue or retry briefly), then Retry. Application layer (`MessagePostedHandler`).
- **Status:** Open

### FP-08: Redelivery causes duplicate `UserActivityRecordedEvent`

- **Location:** `MessagePostedHandler.cs:22-43`
- **Description:** The handler does not record which `MessageId` it has handled. `RecordActivity` itself is safe to repeat (`UserProfile.cs`, monotonic update), but every redelivery publishes the result event again with a new `ProcessedAt`.
- **Consequence:** Once retries or manual requeueing from FP-04 exist, downstream consumers see the same `MessageId` more than once. Any non-idempotent consumer double-counts.
- **Severity:** Medium. Not triggered today without retries, but Task 02 retries will trigger it.
- **Pattern:** Idempotent consumer (deduplicate on `MessageId`). Application layer. Must be done together with any retry added for FP-04.
- **Status:** Mitigated

### FP-09: Consumer concurrency is unbounded and shares threads with publishing

- **Location:** `EasyNetQMessageClient.cs:64-66`, `MessagePostedHandler.cs:34`
- **Description:** The subscription uses EasyNetQ defaults for prefetch and handler concurrency, and the handler publishes through the same bus and connection it consumes from. Nothing isolates the two.
- **Consequence:** A burst of `MessagePostedEvent` or a slow publish can saturate the consumer and delay all message handling.
- **Severity:** Low. No evidence of load in this project yet, and the work per message is small.
- **Pattern:** Bulkhead (explicit prefetch and concurrency limit, optionally separate publish connection). Messaging layer.
- **Status:** Open

### FP-10: Silent fallback to `host=localhost` and hardcoded guest credentials

- **Location:** `Program.cs:18-20`, `docker-compose.yaml:25`
- **Description:** If the `RabbitMq` connection string is missing, the service falls back to `host=localhost` without a warning. Inside a container, localhost is the container itself, so it will never reach the broker. Compose also hardcodes `guest`/`guest`.
- **Consequence:** A missing environment variable looks like a broker outage and, combined with FP-01, produces a hard-to-diagnose startup failure. Guest credentials are a security weakness outside local dev.
- **Severity:** Low. Misconfiguration risk, not a runtime fault.
- **Pattern:** Fail fast on missing configuration with a clear message, secrets from environment or secret store. Hosting layer.
- **Status:** Open

### FP-11: Read-modify-write on shared mutable entity without locking

- **Location:** `InMemoryUserProfileRepository.cs:13-17`, `UserProfileService.cs:40-58`, `MessagePostedHandler.cs:24-32`
- **Description:** `GetById` returns the same mutable `UserProfile` instance to every caller. A `PUT` and a `MessagePostedEvent` for the same user can mutate it concurrently with no lock or version check.
- **Consequence:** Torn updates between profile fields and `LastActiveAtUtc` are possible under concurrent requests.
- **Severity:** Low. Small window, low-value fields, and the in-memory store will be replaced (FP-03).
- **Pattern:** Optimistic concurrency (version column) once a database exists. Infrastructure layer.
- **Status:** Open

## Top 3 to fix in Task 02

1. **FP-01** (Startup depends on the broker). Smallest change with the largest effect on availability: move subscription setup into a retrying background loop so the REST API and the service itself survive a broker that is down or slow to start. Directly demonstrates Retry with backoff and jitter.
2. **FP-04 together with FP-08** (Consumer retry, dead-letter, idempotency). These must be done as a pair: retries without deduplication create duplicate `UserActivityRecordedEvent`. Covers Retry, dead-letter and idempotent consumer in the one place this service has real integration risk.
3. **FP-05 together with FP-06** (Publish timeout and circuit breaker, plus readiness check). Adds Timeout and Circuit Breaker around the only outbound call and exposes the broker state through a health endpoint, which also gives a way to verify the other fixes.

FP-02 and FP-03 are higher severity but need an outbox and a database, which is a larger change than a resilience-pattern task. They are the natural next step after Task 02.

## Open questions

1. What exactly does EasyNetQ 8.1.7 do on `SubscribeAsync` and `PublishAsync` when RabbitMQ is unreachable: throw, wait, or buffer? FP-01 and FP-05 assume the call can throw or block. This should be confirmed by running the service with the broker stopped.
2. Are publisher confirms enabled? EasyNetQ defaults are assumed here (confirms off), which would mean a publish can succeed locally and still be lost.
3. Does the Messaging Service publish `MessagePostedEvent` for authors whose profile was never created through this service? This decides how serious FP-07 is in practice.
4. Is the `X-UserId` header only reachable through the API gateway? `UserProfilesController.cs:33-40` trusts it fully. This is a security question, not a failure point, so it is left out of the table.
