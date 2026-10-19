# Bizcord: Analysis and Design (User Service)

Items marked (assumption) describe services that are not built in this repository.

## 1. System overview

Bizcord is a real-time enterprise communication platform, a Discord-like application where
users communicate with each other in real time. Only the backend is in scope. The system is
developed as a monorepo with one microservice per bounded context. This repository contains the
User Service. Services communicate synchronously over REST and asynchronously over RabbitMQ
using EasyNetQ.

## 2. Domains and bounded contexts

The project scope defines four functional domains. Each one maps to a bounded context and a
microservice.

| Domain | Responsibility | Service |
|---|---|---|
| User profiles | Who a user is: display name, avatar, status message, presence, last active | User Service (this service) |
| Realtime communication | Sending and receiving messages. Publishes MessagePostedEvent | Messaging Service (assumption) |
| Channel management | Creating and managing channels | Channel Service (assumption) |
| Engagement | Notifications, reactions and similar | Engagement Service (assumption) |

The chosen bounded context for this service is user profiles.

## 3. User Service responsibilities

- Create, read, update and delete user profiles.
- Own and validate profile data (display name, avatar, status message, presence status).
- Keep track of when a user was last active, based on messages they post.

The service is not responsible for credentials, chat messages, channels or notifications.

## 4. Service boundaries and data ownership

- The User Service is the single owner of user profile data (the `UserProfile` aggregate).
  Other services use the REST API or events, never the storage.
- Other services identify a user only by `UserId` (a Guid).
- Data is currently kept in memory (`InMemoryUserProfileRepository`). A database is a later step.

### Domain model

```
UserProfile (aggregate root, id = UserId)
  DisplayName      value object, validated
  AvatarReference  value object, optional
  StatusMessage    value object
  PresenceStatus   Online, Idle, DoNotDisturb, Offline
  CreatedAtUtc
  LastActiveAtUtc  set when the user posts a message
Domain events: UserProfileCreated, DisplayNameChanged, PresenceStatusChanged
```

### Layers

- `UserService.Api`: controllers and the message subscription.
- `UserService.Application`: use cases, repository abstraction and the message handler.
- `UserService.Domain`: entities, value objects and domain events.
- `UserService.Messages`: shared message contracts.
- `UserService.Messaging`: `IMessageClient` and `IMessageHandler`, implemented with EasyNetQ.

## 5. Interactions

### Synchronous: REST (`/api/user-profiles`)

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/user-profiles` | Create a profile (201, 400) |
| GET | `/api/user-profiles` | List profiles |
| GET | `/api/user-profiles/{id}` | Get one profile (200, 404) |
| PUT | `/api/user-profiles/{id}` | Update name, avatar and presence (200, 400, 404) |
| DELETE | `/api/user-profiles/{id}` | Delete a profile (204, 404) |

### Asynchronous: events

| Event | Direction | Description |
|---|---|---|
| `MessagePostedEvent` | Consumed | Published by the Messaging Service. Carries MessageId, ChannelId, AuthorId, Content, PostedAt |
| `UserActivityRecordedEvent` | Published | Published after the author's last active timestamp was updated. Carries MessageId, UserId, LastActiveAt, ProcessedAt |

```
Messaging Service --MessagePostedEvent--> RabbitMQ --> User Service
User Service --UserActivityRecordedEvent--> RabbitMQ --> other services
```

When a `MessagePostedEvent` arrives, the handler looks up the author, updates `LastActiveAtUtc`
and publishes `UserActivityRecordedEvent` with the same MessageId so the message can be traced
through the system. If the author has no profile, the event is ignored.

## 6. Design decisions

- Message contracts live in a separate library so other services can reference the exact types
  without the web application.
- `IMessageClient` hides EasyNetQ, so the broker can be replaced without changing callers.
- Services communicate through events instead of a shared database, so each service owns its data.
- Eventual consistency between services is accepted.
