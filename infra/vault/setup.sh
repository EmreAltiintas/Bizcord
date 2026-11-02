#!/usr/bin/env bash
# Usage:
#   JWT_SIGNING_KEY=<value> ./setup.sh        start, init, unseal, store the secret, create policy and app token
#   ./setup.sh unseal                         unseal with two of the saved keys
#   ./setup.sh token                          create a new app token (1h)
# Keys and tokens are written to .local/, which is git-ignored. Nothing is printed except status.
set -euo pipefail

cd "$(dirname "$0")"
LOCAL=.local
INIT=$LOCAL/init.json
APP_ENV=$LOCAL/app-token.env
CONTAINER=bizcord-vault
mkdir -p "$LOCAL"

vault_cli() {
  docker exec -i -e VAULT_ADDR=http://127.0.0.1:8200 ${VAULT_TOKEN:+-e VAULT_TOKEN="$VAULT_TOKEN"} "$CONTAINER" vault "$@"
}

vault_status() { vault_cli status 2>/dev/null || true; }

root_token() { sed -n 's/.*"root_token": *"\([^"]*\)".*/\1/p' "$INIT"; }
unseal_keys() { grep -o '"[A-Za-z0-9+/=]\{40,\}"' "$INIT" | tr -d '"' | head -2; }

wait_for_api() {
  for _ in $(seq 1 30); do
    code=0; vault_cli status >/dev/null 2>&1 || code=$?
    [ "$code" -ne 1 ] && return 0
    sleep 1
  done
  echo "Vault did not start" >&2; exit 1
}

unseal() {
  for key in $(unseal_keys); do vault_cli operator unseal "$key" >/dev/null; done
  echo "Vault unsealed"
}

make_token() {
  local out
  out=$(VAULT_TOKEN=$(root_token) vault_cli token create -policy=user-profiles-read -no-default-policy -ttl=1h -format=json)
  token=$(printf '%s' "$out" | sed -n 's/.*"client_token": *"\([^"]*\)".*/\1/p')
  printf 'VAULT_ADDR=http://localhost:8200\nVAULT_TOKEN=%s\n' "$token" > "$APP_ENV"
  chmod 600 "$APP_ENV"
  echo "App token (1h) written to infra/vault/$APP_ENV"
}

case "${1:-all}" in
  unseal) wait_for_api; unseal; exit 0 ;;
  token) make_token; exit 0 ;;
esac

: "${JWT_SIGNING_KEY:?Set JWT_SIGNING_KEY to the gateway AuthenticationProviderKey value}"

docker compose up -d
wait_for_api

if vault_status | grep -q 'Initialized *false'; then
  vault_cli operator init -key-shares=3 -key-threshold=2 -format=json > "$INIT"
  chmod 600 "$INIT"
  echo "Vault initialized, keys saved to infra/vault/$INIT"
fi

vault_status | grep -q 'Sealed *true' && unseal

export VAULT_TOKEN
VAULT_TOKEN=$(root_token)

(vault_cli secrets list || true) | grep -q '^secret/' || vault_cli secrets enable -path=secret kv-v2 >/dev/null
printf '%s' "$JWT_SIGNING_KEY" | vault_cli kv put secret/user-profiles jwt-signing-key=- >/dev/null
echo "Secret stored at secret/user-profiles"

vault_cli policy write user-profiles-read - >/dev/null <<'POLICY'
path "secret/data/user-profiles" {
  capabilities = ["read"]
}
POLICY
echo "Policy user-profiles-read written"

unset VAULT_TOKEN
make_token
