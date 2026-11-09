# Bizcord, User Service

The User Service owns the user profiles bounded context of Bizcord, a Discord/Slack-like
backend built as microservices for a university project. See
[docs/analysis-and-design.md](../../docs/analysis-and-design.md) for the analysis and design.

It communicates in two ways:

- a REST API (synchronous), and
- asynchronous messaging over RabbitMQ using EasyNetQ (publish/subscribe).

## Features

- CRUD for user profiles under `/api/user-profiles`.
- Presence feature: consumes `MessagePostedEvent`, updates the author's last active timestamp
  and publishes `UserActivityRecordedEvent`.

- Ban saga: a moderator bans a user, and the User Service orchestrates removal from channels and ending of calls in other services. See [SAGA-DESIGN.md](SAGA-DESIGN.md).

## Project layout

```
src/UserService.Api          ASP.NET Core Web API: controllers and message subscription
src/UserService.Application  Use cases, repository abstraction, message handler
src/UserService.Domain       Entities, value objects, domain events
src/UserService.Messages     Shared message contracts
src/UserService.Messaging    IMessageClient and IMessageHandler (EasyNetQ implementation)
src/SagaStubs.Worker         Stand-ins for the Channel and Streaming services (ban saga)
tests/                       xUnit tests for each project
docker-compose.yaml          RabbitMQ, the user service and the two saga stubs
Dockerfile                   Multi-stage build of UserService.Api
```

## Prerequisites

- .NET 8 SDK
- Docker

## Run with Docker

The JWT signing key is read from Vault at startup, see `VAULT.md`. Start Vault first
(`infra/vault`), put the app token in `.env`, then:

```bash
cp .env.example .env
docker compose up -d
```

- API: http://localhost:5084 (all endpoints except `/health/*` need a JWT, see `SECURITY.md`)
- RabbitMQ management UI: http://localhost:15672 (guest / guest)

Stop with `docker compose down`.

## Run the API locally

```bash
docker compose up -d rabbitmq
export VAULT_ADDR=http://localhost:8200
export VAULT_TOKEN="<app token from infra/vault/.local/app-token.env>"
dotnet run --project src/UserService.Api --launch-profile http
```

The service refuses to start if it cannot read the signing key from Vault, see `VAULT.md`. In the Development environment Swagger UI is served at `/swagger`. The RabbitMQ connection
string is read from `ConnectionStrings:RabbitMq` and defaults to `host=localhost`.

## Run the tests

```bash
docker compose up -d rabbitmq
dotnet test
```

The integration test needs a running RabbitMQ.
