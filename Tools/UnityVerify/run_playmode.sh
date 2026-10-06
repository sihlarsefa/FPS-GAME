#!/bin/zsh
# =====================================================================================
#  PlayMode duman testleri (gerçek Unity Editor gerekir; grafik açık batchmode).
#  F2-4 penceresinde SetupAll sonrası çalıştırın.
#
#  Kullanım:
#    zsh Tools/UnityVerify/run_playmode.sh
#    zsh Tools/UnityVerify/run_playmode.sh --filter KuzgunVadisi
#
#  Çıktılar:
#    Logs/playmode.xml   — NUnit sonuçları
#    Logs/playmode.log   — Unity log
#    Logs/screens/*.png  — test ekran görüntüleri
# =====================================================================================
set -euo pipefail

T=${0:A:h}
P=${T:h:h}
U="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity}"

FILTER=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --filter|-f)
      FILTER="${2:-}"
      shift 2
      ;;
    --unity)
      U="${2:-}"
      shift 2
      ;;
    -h|--help)
      echo "Kullanım: zsh Tools/UnityVerify/run_playmode.sh [--filter NAME] [--unity PATH]"
      exit 0
      ;;
    *)
      echo "Bilinmeyen argüman: $1" >&2
      exit 2
      ;;
  esac
done

if [[ ! -x "$U" ]]; then
  echo "Unity bulunamadı: $U" >&2
  echo "UNITY_EDITOR ortam değişkeni veya --unity ile yolu verin." >&2
  exit 1
fi

mkdir -p "$P/Logs/screens"

ARGS=(
  -batchmode
  -projectPath "$P"
  -runTests
  -testPlatform PlayMode
  -testCategory "!Soak"
  -testResults "$P/Logs/playmode.xml"
  -logFile "$P/Logs/playmode.log"
)

if [[ -n "$FILTER" ]]; then
  ARGS+=(-testFilter "$FILTER")
fi

echo "== PlayMode: $U"
echo "== Proje: $P"
echo "== Sonuç: Logs/playmode.xml | log: Logs/playmode.log | ekran: Logs/screens/"
[[ -n "$FILTER" ]] && echo "== Filtre: $FILTER"

set +e
"$U" "${ARGS[@]}"
CODE=$?
set -e

echo "== Unity çıkış kodu: $CODE"
if [[ -f "$P/Logs/playmode.xml" ]]; then
  TOTAL=$(grep -c 'result="Passed"' "$P/Logs/playmode.xml" 2>/dev/null || true)
  FAIL=$(grep -c 'result="Failed"' "$P/Logs/playmode.xml" 2>/dev/null || true)
  SKIP=$(grep -c 'result="Skipped"' "$P/Logs/playmode.xml" 2>/dev/null || true)
  echo "== Özet: passed=$TOTAL failed=$FAIL skipped=$SKIP"
fi

SCREENS=$(find "$P/Logs/screens" -name '*.png' 2>/dev/null | wc -l | tr -d ' ')
echo "== Ekran görüntüsü: $SCREENS dosya"

exit $CODE
