#!/usr/bin/env bash
# Smoke-test the running docker-compose stack through nginx (the way a reviewer uses it).
# Usage: docker compose up --build -d   then   bash scripts/smoke-compose.sh [base-url]
set -euo pipefail

BASE="${1:-http://localhost:8080}"
fail() { echo "FAIL: $*" >&2; exit 1; }
pass() { echo "ok   $*"; }

[ "$(curl -fsS "$BASE/api/health")" = "Healthy" ] || fail "health"
pass "GET /api/health → Healthy"

curl -fsS "$BASE/api/menu" | grep -q '"code":"large"' || fail "menu"
pass "GET /api/menu"

DRAFT='{"lines":[{"sizeCode":"large","toppingCodes":["pepperoni","bacon"],"quantity":2}],"promoCode":"ILOVEPIZZA","tip":{"mode":"percent","value":"15"}'
curl -fsS -X POST "$BASE/api/orders/quote" -H 'Content-Type: application/json' -d "$DRAFT}" \
  | grep -q '"total":31.23' || fail "quote total is not 31.23"
pass "POST /api/orders/quote → 31.23 (docs/API.md worked example)"

KEY="smoke-$(date +%s)-$RANDOM"
HEADERS=$(mktemp)
curl -fsS -D "$HEADERS" -o /dev/null -X POST "$BASE/api/orders" -H 'Content-Type: application/json' \
  -H "Idempotency-Key: $KEY" -d "$DRAFT,\"expectedTotal\":31.23}" || fail "submit"
LOCATION=$(grep -i '^location:' "$HEADERS" | tr -d '\r' | cut -d' ' -f2)
rm -f "$HEADERS"
case "$LOCATION" in "$BASE"/api/orders/*) ;; *) fail "Location '$LOCATION' does not start with $BASE (#48)";; esac
pass "POST /api/orders → 201, Location $LOCATION"

curl -fsS "$LOCATION" | grep -q '"status":"NotStarted"' || fail "following Location"
pass "GET Location → NotStarted"

RETRY=$(curl -fsS -o /dev/null -w '%{http_code}' -X POST "$BASE/api/orders" -H 'Content-Type: application/json' \
  -H "Idempotency-Key: $KEY" -d "$DRAFT,\"expectedTotal\":31.23}")
[ "$RETRY" = "200" ] || fail "retry with the same key returned $RETRY, expected 200"
pass "retry with the same Idempotency-Key → 200 (same order)"

echo "Smoke test passed."
