# Vault integration: User Service

Task 02 (week 45), System Integration.

## What moved to Vault

The JWT signing key (`Jwt:SigningKey`) that the service uses to validate tokens. It is the only real secret the service reads. There is no database, and the RabbitMQ login is the broker's default. The key was in `.env.example` and in the compose environment. It is now stored in Vault at `secret/user-profiles`, field `jwt-signing-key`, and removed from the repo files.

The value is the shared demo key from the gateway's `appsettings.Development.json`. The gateway is unchanged, so Vault holds the same value.

## Vault setup

Files are in `infra/vault`.

| Item | Value |
|---|---|
| Image | `hashicorp/vault:1.21.0` |
| Config | `config.hcl`: file storage on `/vault/file`, listener `0.0.0.0:8200`, `tls_disable = 1`, `disable_mlock = true`, `ui = true` |
| Port | Published on `127.0.0.1:8200` only |
| Data | Named volume `vault-data` |
| Network | `bizcord` (created by the Vault compose file, used by the service compose file as external) |
| Secret engine | KV v2 mounted at `secret/` |
| Path | `secret/user-profiles`, field `jwt-signing-key` |
| Policy | `user-profiles-read`: `read` on `secret/data/user-profiles` only |
| App token | `-policy=user-profiles-read -no-default-policy -ttl=1h` |

## How the service reads it

`AddVaultSecrets` runs in `Program.cs` before the JWT configuration is read. It calls `GET {VAULT_ADDR}/v1/secret/data/user-profiles` with the header `X-Vault-Token` (timeout 5 seconds, up to 3 attempts), takes `data.data.jwt-signing-key` and adds it as `Jwt:SigningKey`. The value is held in memory. It is not fetched per request.

| Situation | Behavior |
|---|---|
| 403 | Fails immediately: token invalid or expired, or the policy does not allow the path |
| 404, missing or empty value | Fails immediately with the path and field name |
| Sealed (503), unreachable, timeout | Retries 3 times, then fails |

The log contains only `Loaded Jwt:SigningKey from Vault path secret/data/user-profiles`. The token and the value are never logged. Path, field, timeout and attempts are in the `Vault` section of `appsettings.json`. The address and token come from the environment variables `VAULT_ADDR` and `VAULT_TOKEN`.

## Run it

Prerequisites: Docker, and the gateway clone at `../api-gateway` for the key value.

```bash
cd infra/vault
export JWT_SIGNING_KEY="<AuthenticationProviderKey from the gateway's appsettings.Development.json>"
bash setup.sh
```

`setup.sh` starts Vault, runs `operator init` (3 key shares, threshold 2), unseals with two keys, enables KV v2 at `secret/`, stores the secret, writes the policy and creates the app token. Unseal keys, the root token and the app token are written to `infra/vault/.local/`, which is git-ignored. Nothing is printed. Keep a copy of `init.json` outside the repo: without two of the keys the data cannot be unsealed.

Run the service with `dotnet run`:

```bash
set -a; . infra/vault/.local/app-token.env; set +a
dotnet run --project apps/user-profiles-microservice/src/UserService.Api --launch-profile http
```

Or with Docker (Vault must be running first, because it creates the `bizcord` network):

```bash
cd apps/user-profiles-microservice
cp ../../infra/vault/.local/app-token.env .env   # only VAULT_TOKEN is used, the container uses http://vault:8200
docker compose up -d
```

The token lives for 1 hour. After that a new service start fails with the 403 message. A running service is not affected, because it does not call Vault again. Create a new token with `bash infra/vault/setup.sh token`. After a Vault restart it is sealed: run `bash infra/vault/setup.sh unseal`.

## Tests

Unit tests of the client (200, 403, 404, missing or empty value, sealed, network error, timeout, token not in the error message) run with the normal suite:

```bash
dotnet test apps/user-profiles-microservice/UserService.sln
```

The web tests set `Vault:Enabled=false` and supply a test key, so they do not need Vault.

The four checks below run against a real Vault. Build the solution first, then:

```bash
bash infra/vault/verify.sh
```

| # | Check | Expected | Actual |
|---|---|---|---|
| 1 | Service starts with a valid app token, log says the secret was loaded, token not in the log | started, yes, yes | started, yes, yes |
| 1 | Task 01 security tests (401, 403, 200) | pass | pass |
| 2 | Vault with an invalid token | 403 | 403 |
| 2 | Service with an invalid token, log explains the denial | fails to start, yes | fails to start, yes |
| 3 | App token reads its own path | 200 | 200 |
| 3 | App token reads another path | 403 | 403 |
| 3 | App token overwrites the secret | 403 | 403 |
| 3 | Secret afterwards | unchanged, 200 | unchanged, 200 |
| 4 | `docker compose restart vault`, Vault health | sealed, 503 | 503 |
| 4 | Service started while Vault is sealed | fails to start, log says sealed or starting | fails to start, yes |
| 4 | Unseal with two of the original keys, health | 200 | 200 |
| 4 | Secret with the same app token, service start | 200, started | 200, started |

Check 4 differs from "requests fail while Vault is sealed": the service reads the key once at startup, so a running service keeps serving requests while Vault is sealed. What fails is starting it.

## Known limitations

- The app token expires after 1 hour. Nothing renews it, so a service that restarts later needs a new token.
- Vault runs without TLS locally (`tls_disable = 1`). The token and the secret travel in plain text on `localhost` and on the `bizcord` Docker network.
- The JWT key is still the demo key shared with the gateway, and it is in the git history (`.env.example` in an earlier commit, and the public gateway repo). It is not rotated because rotating it means changing the gateway.
- The key is read only at startup. Changing it in Vault needs a restart of the service.
- `setup.sh` prints no secrets, but it passes the root and app tokens to `docker exec` on the command line, which other local users could see in the process list.
- The RabbitMQ login (`guest`/`guest`) is still in `docker-compose.yaml`.
