#!/usr/bin/env bash
# Yerel hızlı koşu (dry-run varsayılan profil)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
dotnet run --project Harekat.LoadTest -- --profile default --dry-run --players "${1:-40}" --out reports "$@"
