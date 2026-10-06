#!/bin/zsh
# =====================================================================================
#  Soak (dayanıklılık) PlayMode testleri — kategori: Soak
#  Kullanım:
#    zsh Tools/UnityVerify/run_soak.sh
#    HAREKAT_SOAK_SECONDS=120 HAREKAT_SOAK_SHORT_SECONDS=60 zsh Tools/UnityVerify/run_soak.sh
#    zsh Tools/UnityVerify/run_soak.sh --filter KuzgunVadisi_BotMatch
#
#  Çıktılar:
#    Logs/soak/<tarih_tag>/RAPOR.md + ekran görüntüleri
#    Logs/soak/RAPOR.md (özet eklemeli)
#    Logs/soak.xml / Logs/soak.log
# =====================================================================================
set -euo pipefail

T=${0:A:h}
P=${T:h:h}
U="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity}"

FILTER=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --filter|-f) FILTER="${2:-}"; shift 2 ;;
    --unity) U="${2:-}"; shift 2 ;;
    -h|--help)
      echo "Kullanım: zsh Tools/UnityVerify/run_soak.sh [--filter NAME] [--unity PATH]"
      echo "Ortam: HAREKAT_SOAK_SECONDS (bot maç, varsayılan 1200), HAREKAT_SOAK_SHORT_SECONDS (diğer, 300)"
      exit 0
      ;;
    *) echo "Bilinmeyen argüman: $1" >&2; exit 2 ;;
  esac
done

if [[ ! -x "$U" ]]; then
  echo "Unity bulunamadı: $U" >&2
  exit 1
fi

mkdir -p "$P/Logs/soak" "$P/Logs/screens"

ARGS=(
  -batchmode
  -projectPath "$P"
  -runTests
  -testPlatform PlayMode
  -testCategory Soak
  -testResults "$P/Logs/soak.xml"
  -logFile "$P/Logs/soak.log"
)

if [[ -n "$FILTER" ]]; then
  ARGS+=(-testFilter "$FILTER")
fi

echo "== Soak PlayMode: $U"
echo "== HAREKAT_SOAK_SECONDS=${HAREKAT_SOAK_SECONDS:-1200} SHORT=${HAREKAT_SOAK_SHORT_SECONDS:-300}"
[[ -n "$FILTER" ]] && echo "== Filtre: $FILTER"

set +e
"$U" "${ARGS[@]}"
CODE=$?
set -e

echo "== Unity çıkış kodu: $CODE"
if [[ -f "$P/Logs/soak.xml" ]]; then
  TOTAL=$(grep -c 'result="Passed"' "$P/Logs/soak.xml" 2>/dev/null || true)
  FAIL=$(grep -c 'result="Failed"' "$P/Logs/soak.xml" 2>/dev/null || true)
  echo "== Özet: passed=$TOTAL failed=$FAIL"
fi
echo "== Rapor: Logs/soak/RAPOR.md"
exit $CODE
