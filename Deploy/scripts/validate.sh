#!/usr/bin/env bash
# Manifest doğrulama (kubectl dry-run / yamllint benzeri basit kontroller)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ERRORS=0

check_file() {
  local f="$1"
  if [[ ! -s "$f" ]]; then
    echo "BOŞ: $f" >&2
    ERRORS=$((ERRORS + 1))
    return
  fi
  # Temel YAML ayırıcı / apiVersion kontrolü
  if ! grep -qE '^(apiVersion:|kind:|#)' "$f"; then
    # Dockerfile / sh / tf / json / md atla
    case "$f" in
      *.yaml|*.yml)
        echo "UYARI: apiVersion/kind yok gibi: $f" >&2
        ERRORS=$((ERRORS + 1))
        ;;
    esac
  fi
}

echo "[validate] Deploy YAML taranıyor..."
while IFS= read -r -d '' f; do
  check_file "$f"
done < <(find "${ROOT}" -type f \( -name '*.yaml' -o -name '*.yml' \) -print0)

# Dockerfile ARG/ENV zorunlu alanlar
if ! grep -q 'UNITY_SERVER_BUILD_PATH' "${ROOT}/server/Dockerfile"; then
  echo "HATA: Dockerfile ARG UNITY_SERVER_BUILD_PATH eksik" >&2
  ERRORS=$((ERRORS + 1))
fi
for envn in GAME_PORT REGION SERVER_KEY; do
  if ! grep -q "${envn}" "${ROOT}/server/Dockerfile"; then
    echo "HATA: Dockerfile ENV ${envn} eksik" >&2
    ERRORS=$((ERRORS + 1))
  fi
done

# Kapasite hesabı tutarlılığı
if [[ ! -f "${ROOT}/docs/capacity.md" ]]; then
  echo "HATA: docs/capacity.md yok" >&2
  ERRORS=$((ERRORS + 1))
fi

if [[ "${ERRORS}" -gt 0 ]]; then
  echo "[validate] ${ERRORS} hata" >&2
  exit 1
fi
echo "[validate] OK"
