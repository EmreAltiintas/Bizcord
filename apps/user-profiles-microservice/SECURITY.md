# East/West security: User Service

Task 01 (week 45), System Integration.

## Mechanism

| | Choice | Why |
|---|---|---|
| Authentication | JWT Bearer, HS256, validated in the gateway and again in the service | The gateway already validates JWTs. The service must also reject direct calls that bypass the gateway. |
| Authorization | Role claim `internal-service` for service-to-service calls, scope claims `user-profiles.read` and `user-profiles.write` for end users | Matches the split between internal and external endpoints. |
| Tokens | Issued by the gateway's `POST /faucet` | A teaching tool that signs any requested claims. There is no identity provider in the repo. |
| Claims | `MapInboundClaims = false` in gateway and service | Keeps `sub`, `role` and `scope` as named. `scope` is a space separated string. |

Validation covers signature and lifetime only. The faucet puts no `iss` or `aud` in its tokens and the gateway does not validate them, so the service does not either. The audience `internal-api` cannot be enforced until the token issuer sets one.

## Protected endpoints

Every route below is enforced twice: by the gateway route and by a policy in `UserProfilesController`. Policies are in `AuthorizationPolicies.cs`.

| Gateway route | Service endpoint | Requirement |
|---|---|---|
| `GET /internal/users` | `GET /api/user-profiles` | Role `internal-service` |
| `GET /internal/users/{id}` | `GET /api/user-profiles/{id}` | Role `internal-service` |
| `GET /users/me` | `GET /api/user-profiles/me` | Scope `user-profiles.read`, `X-UserId` equals `sub` |
| `POST /users/me` | `POST /api/user-profiles` | Scope `user-profiles.write`, `X-UserId` equals `sub` |
| `PUT /users/me` | `PUT /api/user-profiles/{sub}` | Scope `user-profiles.write`, `sub` equals `{id}` |
| `DELETE /users/me` | `DELETE /api/user-profiles/{sub}` | Scope `user-profiles.write`, `sub` equals `{id}` |
| not routed | `GET /health/live`, `/health/ready` | None |

Notes:

- The profile id is the token `sub`. It must be a Guid. `POST` takes the id from the token, never from the body, and returns 409 if the profile exists.
- For `PUT` and `DELETE` the gateway builds the downstream path from `Claims[sub]` (`ChangeDownstreamPathTemplate`). The service rejects any other id with 403, so a direct call cannot change another user's profile either.
- `X-UserId` is set by the gateway from `sub` and a client supplied value is removed there. The service still compares the header with `sub` and returns 403 on a mismatch, 400 if it is missing or not a Guid.
- Other users' data can only be read internally, through `/internal/users/{id}`.
- Status codes: no token or an invalid or expired token gives 401, a valid token without the role or scope gives 403.

Gateway routes live in the gateway clone, not in this repo: `api-gateway/src/Configuration/ocelot.user-service.json`. The table above lists them. The gateway reaches the service at `host.docker.internal:5084`, so run the service with `dotnet run` (port 5084) or `docker compose up` here (publishes 5084). On Linux, listen on `0.0.0.0` (`applicationUrl` `http://0.0.0.0:5084`).

## Configuration

The signing key (`Jwt:SigningKey`) is read from HashiCorp Vault when the service starts. The service refuses to start without it. The value stored in Vault equals `AuthenticationProviderKey` in the gateway's `appsettings.Development.json`. Setup and run instructions are in `VAULT.md`.

Tokens and keys are never logged.

## How services get and use a token

1. Request a token with the claims the caller needs:
   ```bash
   curl -sS -X POST http://localhost:8080/faucet -H 'Content-Type: application/json' \
     -d '{"claims":{"sub":"<service-guid>","role":"internal-service"}}'
   ```
   Use the `value` field.
2. Call the internal route with `Authorization: Bearer <value>`:
   ```bash
   curl http://localhost:8080/internal/users/<profile-guid> -H "Authorization: Bearer <value>"
   ```
A real deployment would replace the faucet with a client credentials flow from an identity provider. No other service exists in this repo yet, so there is no caller code (for example a `DelegatingHandler`) to change.

## Resetting data

Profiles are stored in memory and there is no seed data. Restart the service to clear everything. Profiles created before this change have random ids that do not match any `sub`, so restart the service once after upgrading.

## Tests

Unit and integration tests, including 401, 403 and 200 against the service directly (no gateway):

```bash
docker compose up -d rabbitmq
dotnet test UserService.sln
```

`MessagePostedIntegrationTests` needs RabbitMQ. The security tests are in `tests/UserService.Api.Tests/Security/EndpointSecurityTests.cs`.

End-to-end check through the gateway and directly against the service:

```bash
# terminal 1, in the api-gateway clone
docker compose up --build
# terminal 2, in this folder (key exported as above)
dotnet run --project src/UserService.Api --launch-profile http
# terminal 3
bash scripts/security-check.sh
```

The script requests tokens from the faucet and prints one line per case: no token (401), invalid token (401), no role (403), role (200), duplicate create (409), forged `X-UserId` (replaced by the gateway), mismatching `X-UserId` (403) and changing another user's profile (403). Restart the service first so the store is empty.

## Not covered

- The service still publishes port 5084 because the gateway reaches it through the host. Next step: a shared Docker network between the gateway and this service, the route pointing at the service name, and no published port. Until then the service defends itself with its own JWT validation.
- No issuer or audience validation (see Mechanism).
- The faucet signs any claims for anyone and the key is a shared demo key. Both are for local use only.
- Expired tokens are rejected, but there is no token revocation and no key rotation. Key storage is covered in `VAULT.md`.
