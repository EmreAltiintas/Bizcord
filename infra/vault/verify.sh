#!/usr/bin/env bash
# Runs the four Vault checks against a running, initialized Vault (see setup.sh).
# Starts and stops the built UserService.Api on port 5084, so stop any other instance first.
set -u

cd "$(dirname "$0")"
VAULT=http://localhost:8200
DLL=../../apps/user-profiles-microservice/src/UserService.Api/bin/Debug/net8.0/UserService.Api.dll
SLN=../../apps/user-profiles-microservice/UserService.sln
LOG=.local/service.log
failures=0

result() {
  if [ "$2" = "$3" ]; then echo "PASS  $1 (expected: $2, actual: $3)"; else echo "FAIL  $1 (expected: $2, actual: $3)"; failures=$((failures + 1)); fi
}

port_pid() { netstat -ano | grep -E ":5084 .*LISTENING" | awk '{print $NF}' | head -1; }

stop_service() {
  [ -n "${SVC_PID:-}" ] && kill "$SVC_PID" 2>/dev/null
  pid=$(port_pid); [ -n "$pid" ] && taskkill //PID "$pid" //F >/dev/null 2>&1
  SVC_PID=
}

# Prints "started" when /health/live answers 200, "failed" when the process exits first.
start_service() {
  ASPNETCORE_URLS=http://localhost:5084 dotnet "$DLL" > "$LOG" 2>&1 &
  SVC_PID=$!
  for _ in $(seq 1 45); do
    if ! kill -0 "$SVC_PID" 2>/dev/null; then echo failed; SVC_PID=; return; fi
    [ "$(curl -s -o /dev/null -w '%{http_code}' http://localhost:5084/health/live)" = 200 ] && { echo started; return; }
    sleep 1
  done
  echo timeout
}

http_code() { curl -s -o /dev/null -w '%{http_code}' "$@"; }

[ -f .local/app-token.env ] || { echo "Run setup.sh first" >&2; exit 1; }
bash setup.sh token >/dev/null
set -a; . .local/app-token.env; set +a
APP_TOKEN=$VAULT_TOKEN

echo "== 1. Service reads the secret at startup, Task 01 tests still pass"
result "service starts with a valid app token" started "$(start_service)"
result "startup log says the secret was loaded" yes "$(grep -q 'Loaded Jwt:SigningKey from Vault' "$LOG" && echo yes || echo no)"
result "token not written to the log" yes "$(grep -q "$APP_TOKEN" "$LOG" && echo no || echo yes)"
stop_service
tests=$(dotnet test "$SLN" --no-build --filter "FullyQualifiedName~EndpointSecurityTests" 2>&1 | grep -E "Passed!|Failed!" | head -1)
result "Task 01 security tests (401/403/200)" passed "$(echo "$tests" | grep -q 'Passed!' && echo passed || echo failed)"

echo "== 2. Invalid token"
result "Vault answers 403 for an invalid token" 403 "$(http_code -H 'X-Vault-Token: invalid' "$VAULT/v1/secret/data/user-profiles")"
result "service fails to start" failed "$(VAULT_TOKEN=invalid start_service)"
result "log explains the denial" yes "$(grep -q 'Vault denied read access' "$LOG" && echo yes || echo no)"
stop_service

echo "== 3. App token is limited to read on its own path"
result "read own path" 200 "$(http_code -H "X-Vault-Token: $APP_TOKEN" "$VAULT/v1/secret/data/user-profiles")"
result "read another path" 403 "$(http_code -H "X-Vault-Token: $APP_TOKEN" "$VAULT/v1/secret/data/other")"
result "overwrite the secret" 403 "$(http_code -X POST -H "X-Vault-Token: $APP_TOKEN" -d '{"data":{"jwt-signing-key":"changed"}}' "$VAULT/v1/secret/data/user-profiles")"
result "secret unchanged after the attempt" 200 "$(http_code -H "X-Vault-Token: $APP_TOKEN" "$VAULT/v1/secret/data/user-profiles")"

echo "== 4. Vault restart: sealed, then unsealed with two of the original keys"
docker compose restart vault >/dev/null 2>&1
for _ in $(seq 1 30); do [ "$(http_code "$VAULT/v1/sys/health")" != 000 ] && break; sleep 1; done
result "Vault health reports sealed" 503 "$(http_code "$VAULT/v1/sys/health")"
result "service refuses to start while sealed" failed "$(start_service)"
result "log reports Vault unavailable" yes "$(grep -q 'sealed or starting' "$LOG" && echo yes || echo no)"
stop_service
bash setup.sh unseal >/dev/null
result "Vault health after unseal" 200 "$(http_code "$VAULT/v1/sys/health")"
result "secret still readable with the same app token" 200 "$(http_code -H "X-Vault-Token: $APP_TOKEN" "$VAULT/v1/secret/data/user-profiles")"
result "service starts again" started "$(start_service)"
stop_service

echo "failures: $failures"
exit $failures
