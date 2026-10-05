#!/usr/bin/env bash
# Dağıtık koordinatör
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
WORKERS="${EXPECTED_WORKERS:-4}"
PLAYERS="${PLAYERS:-10000}"
PORT="${COORDINATOR_PORT:-9090}"
dotnet run --project Harekat.LoadTest -- \
  --scenario distributed-coordinator \
  --expected-workers "$WORKERS" \
  --players "$PLAYERS" \
  --coordinator-port "$PORT" \
  --base-url "${BASE_URL:-http://localhost:5080}" \
  --out reports \
  "$@"
