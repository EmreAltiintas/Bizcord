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

## Project layout

```
src/UserService.Api          ASP.NET Core Web API: controllers and message subscription
src/UserService.Application  Use cases, repository abstraction, message handler
src/UserService.Domain       Entities, value objects, domain events
src/UserService.Messages     Shared message contracts
src/UserService.Messaging    IMessageClient and IMessageHandler (EasyNetQ implementation)
tests/                       xUnit tests for each project
docker-compose.yaml          RabbitMQ and the user service
Dockerfile                   Multi-stage build of UserService.Api
```

## Prerequisites

- .NET 8 SDK
- Docker

## Run with Docker

```bash
docker compose up -d
```

- API: http://localhost:8080
- RabbitMQ management UI: http://localhost:15672 (guest / guest)

Stop with `docker compose down`.

## Run the API locally

```bash
docker compose up -d rabbitmq
dotnet run --project src/UserService.Api
```

In the Development environment Swagger UI is served at `/swagger`. The RabbitMQ connection
string is read from `ConnectionStrings:RabbitMq` and defaults to `host=localhost`.

## Run the tests

```bash
docker compose up -d rabbitmq
dotnet test
```

The integration test needs a running RabbitMQ.
