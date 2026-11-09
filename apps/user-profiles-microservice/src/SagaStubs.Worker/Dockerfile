# Containerizes SagaStubs.Worker, which simulates the Channel and Streaming services.
# The role is chosen with the Stub__Role environment variable ("channel" or "streaming").
#
# Build context is the repository root, like the User Service Dockerfile. Build from the
# repo root with:
#   docker build -f apps/user-profiles-microservice/src/SagaStubs.Worker/Dockerfile -t bizcord/saga-stubs .

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apps/user-profiles-microservice/src/SagaStubs.Worker/SagaStubs.Worker.csproj", "apps/user-profiles-microservice/src/SagaStubs.Worker/"]
COPY ["apps/user-profiles-microservice/src/UserService.Messages/UserService.Messages.csproj", "apps/user-profiles-microservice/src/UserService.Messages/"]
COPY ["apps/user-profiles-microservice/src/UserService.Messaging/UserService.Messaging.csproj", "apps/user-profiles-microservice/src/UserService.Messaging/"]
RUN dotnet restore "apps/user-profiles-microservice/src/SagaStubs.Worker/SagaStubs.Worker.csproj"
COPY apps/user-profiles-microservice/src/ apps/user-profiles-microservice/src/
WORKDIR "/src/apps/user-profiles-microservice/src/SagaStubs.Worker"
RUN dotnet build "SagaStubs.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SagaStubs.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SagaStubs.Worker.dll"]
