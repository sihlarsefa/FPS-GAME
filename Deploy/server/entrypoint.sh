#!/usr/bin/env bash
# HAREKÂT dedicated server başlatıcı
set -euo pipefail

: "${GAME_PORT:=7777}"
: "${QUERY_PORT:=7778}"
: "${REGION:=eu-istanbul}"
: "${SERVER_KEY:=}"
: "${BACKEND_URL:=}"
: "${MAX_PLAYERS:=60}"
: "${MIN_PLAYERS:=40}"
: "${MATCH_DURATION_SEC:=1800}"
: "${UNITY_SERVER_EXECUTABLE:=Harekat.x86_64}"

EXECUTABLE="/opt/harekat/${UNITY_SERVER_EXECUTABLE}"

if [[ ! -x "${EXECUTABLE}" ]]; then
  echo "[HAREKÂT] HATA: Unity sunucu yürütülebilir dosyası bulunamadı: ${EXECUTABLE}" >&2
  echo "[HAREKÂT] UNITY_SERVER_BUILD_PATH ile build artefaktını imaja kopyalayın." >&2
  exit 1
fi

if [[ -z "${SERVER_KEY}" ]]; then
  echo "[HAREKÂT] UYARI: SERVER_KEY boş; backend heartbeat/result doğrulaması başarısız olabilir." >&2
fi

echo "[HAREKÂT] Dedicated server başlıyor"
echo "  REGION=${REGION} GAME_PORT=${GAME_PORT} QUERY_PORT=${QUERY_PORT}"
echo "  MAX_PLAYERS=${MAX_PLAYERS} MIN_PLAYERS=${MIN_PLAYERS}"
echo "  BACKEND_URL=${BACKEND_URL:-<unset>}"

exec "${EXECUTABLE}" \
  -batchmode \
  -nographics \
  -logFile /dev/stdout \
  -port "${GAME_PORT}" \
  -queryPort "${QUERY_PORT}" \
  -region "${REGION}" \
  -serverKey "${SERVER_KEY}" \
  -backendUrl "${BACKEND_URL}" \
  -maxPlayers "${MAX_PLAYERS}" \
  -minPlayers "${MIN_PLAYERS}" \
  -matchDuration "${MATCH_DURATION_SEC}" \
  "$@"
