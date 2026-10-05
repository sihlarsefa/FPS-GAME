#!/usr/bin/env bash
# Dağıtık worker — COORDINATOR_URL ve WORKER_ID ortam değişkenleri
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
dotnet run --project Harekat.LoadTest -- \
  --scenario distributed-worker \
  --coordinator-url "${COORDINATOR_URL:-http://127.0.0.1:9090}" \
  --worker-id "${WORKER_ID:-$(hostname)-$$}" \
  --concurrency "${CONCURRENCY:-200}" \
  --base-url "${BASE_URL:-http://localhost:5080}" \
  --out reports \
  "$@"
