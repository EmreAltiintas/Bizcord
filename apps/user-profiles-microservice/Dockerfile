# Containerizes UserService.Api (Bizcord User Service).
#
# Build context is the repository root, so all COPY paths below are relative
# to the monorepo root — this keeps the project reference graph intact
# (UserService.Api -> Application/Messages/Messaging, Application -> Domain/Messages).
# Build from the repo root with:
#   docker build -f apps/user-profiles-microservice/Dockerfile -t bizcord/user-service .

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apps/user-profiles-microservice/src/UserService.Api/UserService.Api.csproj", "apps/user-profiles-microservice/src/UserService.Api/"]
COPY ["apps/user-profiles-microservice/src/UserService.Application/UserService.Application.csproj", "apps/user-profiles-microservice/src/UserService.Application/"]
COPY ["apps/user-profiles-microservice/src/UserService.Domain/UserService.Domain.csproj", "apps/user-profiles-microservice/src/UserService.Domain/"]
COPY ["apps/user-profiles-microservice/src/UserService.Messages/UserService.Messages.csproj", "apps/user-profiles-microservice/src/UserService.Messages/"]
COPY ["apps/user-profiles-microservice/src/UserService.Messaging/UserService.Messaging.csproj", "apps/user-profiles-microservice/src/UserService.Messaging/"]
RUN dotnet restore "apps/user-profiles-microservice/src/UserService.Api/UserService.Api.csproj"
COPY apps/user-profiles-microservice/src/ apps/user-profiles-microservice/src/
WORKDIR "/src/apps/user-profiles-microservice/src/UserService.Api"
RUN dotnet build "UserService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "UserService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UserService.Api.dll"]
