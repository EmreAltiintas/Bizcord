# Bizcord, User Service

The **User Service** owns the *user profiles* bounded context for **Bizcord**, a fictional
Discord/Slack-like backend built as a microservices system for a university project.

It talks to the rest of the system in two ways:

- a **REST API** (synchronous, request/response), and
- **asynchronous messaging** over **RabbitMQ** using **EasyNetQ** (pub/sub between services).

This repository currently contains only the scaffolding. No REST endpoints, message
contracts, or messaging/handler logic are implemented yet — those come in later tasks.

## Project layout

```
src/UserService.Api        ASP.NET Core Web API (.NET 8) — the HTTP entry point + (later) message handlers
src/UserService.Messages   Class library — the shared message contracts that travel over RabbitMQ
tests/UserService.Api.Tests xUnit test project for UserService.Api
docker-compose.yaml        RabbitMQ for local development
Dockerfile                 placeholder for containerizing UserService.Api (filled in later)
UserService.sln            solution referencing all three projects
```

**Why the Api / Messages split?** Message contracts are a *shared* concern: other services
need to reference the exact same class to publish or subscribe to an event. Keeping them in
a dependency-free class library (`UserService.Messages`) lets them be packaged and shared
without dragging in the whole web application. `UserService.Api` references
`UserService.Messages`, not the other way around.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://docs.docker.com/get-docker/) (for RabbitMQ)

## Run RabbitMQ locally

```bash
docker compose up -d
```

- AMQP: `localhost:5672`
- Management UI: <http://localhost:15672> (`guest` / `guest`)

Stop it with `docker compose down`.

## Run the API

```bash
dotnet run --project src/UserService.Api
```

The API listens on the URLs printed to the console (see
`src/UserService.Api/Properties/launchSettings.json`). In the `Development` environment a
Swagger UI is served at `/swagger`.

## Run the tests

```bash
dotnet test
```

## Next tasks

- Define message contracts in `UserService.Messages`.
- Register the EasyNetQ bus and add publishers / subscribers in `UserService.Api`.
- Implement the REST endpoints (thin controllers under `src/UserService.Api/Controllers`).
- Fill in the `Dockerfile` build/run stages.
