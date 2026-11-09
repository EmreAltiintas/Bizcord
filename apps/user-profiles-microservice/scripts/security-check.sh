#!/usr/bin/env bash
# Checks east/west security through the gateway (8080) and directly against the service (5084).
# Needs: gateway running (docker compose up --build in api-gateway), the service running with
# the same signing key as the gateway (read from Vault, see VAULT.md), and a clean in-memory store (restart the service).
set -u

GATEWAY=${GATEWAY:-http://localhost:8080}
SERVICE=${SERVICE:-http://localhost:5084}
USER_ID=$(cat /proc/sys/kernel/random/uuid 2>/dev/null || powershell.exe -NoProfile -Command "[guid]::NewGuid().ToString()" | tr -d '\r')
failures=0

token() {
  curl -sS -X POST "$GATEWAY/faucet" -H 'Content-Type: application/json' -d "$1" | sed -E 's/.*"value":"([^"]+)".*/\1/'
}

expect() {
  local name=$1 want=$2; shift 2
  local got
  got=$(curl -sS -o /dev/null -w '%{http_code}' "$@")
  if [ "$got" = "$want" ]; then echo "ok    $want  $name"; else echo "FAIL  want $want got $got  $name"; failures=$((failures + 1)); fi
}

INTERNAL=$(token '{"claims":{"sub":"11111111-1111-1111-1111-111111111111","role":"internal-service"}}')
MEMBER=$(token "{\"claims\":{\"sub\":\"$USER_ID\",\"role\":\"member\",\"scope\":\"user-profiles.read user-profiles.write\"}}")
READONLY=$(token "{\"claims\":{\"sub\":\"$USER_ID\",\"scope\":\"user-profiles.read\"}}")
BAD="${INTERNAL%?}x"

for base in "$GATEWAY" "$SERVICE"; do
  echo "== $base"
  if [ "$base" = "$GATEWAY" ]; then LIST=/internal/users; ME=/users/me; CREATE=/users/me; else LIST=/api/user-profiles; ME=/api/user-profiles/me; CREATE=/api/user-profiles; fi

  expect "internal list, no token" 401 "$base$LIST"
  expect "internal list, invalid token" 401 "$base$LIST" -H "Authorization: Bearer $BAD"
  expect "internal list, valid token without role" 403 "$base$LIST" -H "Authorization: Bearer $MEMBER"
  expect "internal list, role internal-service" 200 "$base$LIST" -H "Authorization: Bearer $INTERNAL"
  expect "me, no token" 401 "$base$ME" -H "X-UserId: $USER_ID"
  expect "me, token without write scope on POST" 403 -X POST "$base$CREATE" -H "Authorization: Bearer $READONLY" -H "X-UserId: $USER_ID" -H 'Content-Type: application/json' -d '{"displayName":"Emre"}'
done

echo "== create, duplicate and ownership through the gateway"
expect "POST /users/me creates profile" 201 -X POST "$GATEWAY/users/me" -H "Authorization: Bearer $MEMBER" -H 'Content-Type: application/json' -d '{"displayName":"Emre"}'
expect "POST /users/me again is a duplicate" 409 -X POST "$GATEWAY/users/me" -H "Authorization: Bearer $MEMBER" -H 'Content-Type: application/json' -d '{"displayName":"Emre"}'
expect "GET /users/me" 200 "$GATEWAY/users/me" -H "Authorization: Bearer $MEMBER"
expect "PUT /users/me" 200 -X PUT "$GATEWAY/users/me" -H "Authorization: Bearer $MEMBER" -H 'Content-Type: application/json' -d '{"displayName":"Emre A","presenceStatus":"Online"}'
expect "internal GET /internal/users/{id}" 200 "$GATEWAY/internal/users/$USER_ID" -H "Authorization: Bearer $INTERNAL"
expect "forged X-UserId from client is replaced" 200 "$GATEWAY/users/me" -H "Authorization: Bearer $MEMBER" -H "X-UserId: 22222222-2222-2222-2222-222222222222"
expect "direct call with mismatching X-UserId" 403 "$SERVICE/api/user-profiles/me" -H "Authorization: Bearer $MEMBER" -H "X-UserId: 22222222-2222-2222-2222-222222222222"
expect "direct PUT on another user's profile" 403 -X PUT "$SERVICE/api/user-profiles/11111111-1111-1111-1111-111111111111" -H "Authorization: Bearer $MEMBER" -H 'Content-Type: application/json' -d '{"displayName":"Hacked","presenceStatus":"Online"}'
expect "DELETE /users/me" 204 -X DELETE "$GATEWAY/users/me" -H "Authorization: Bearer $MEMBER"

echo "failures: $failures"
exit $failures
