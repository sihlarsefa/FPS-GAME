#!/bin/zsh
# =====================================================================================
#  Otomatik ekran görüntüsü (görsel QA) — yerleşik macOS oyuncusunu bayraklarla çalıştırır.
#  Kullanım:
#    zsh Tools/UnityVerify/oto_ekran.sh [OUTDIR]                       # AAA_Benchmark Vitrin + kalite kademeleri + perf.csv
#    zsh Tools/UnityVerify/oto_ekran.sh --menu [OUTDIR]                # ana menü, menu_1.png + menu_2.png
#    zsh Tools/UnityVerify/oto_ekran.sh --harita <SahneAdı> [OUTDIR]   # harita, 5 bakış noktası
#  Ortam: OTO_SURE=<sn> sert zaman aşımı (varsayılan 120 oyun içi; betik +60 sn tolerans verir).
# =====================================================================================
set -uo pipefail
T=${0:A:h}
P=${T:h:h}
APP="$P/Builds/macOS/HAREKAT.app/Contents/MacOS"
BIN=$(ls -1 "$APP"/* 2>/dev/null | head -1)
[[ -z "$BIN" ]] && { echo "Oyuncu bulunamadı: $APP (önce macOS build alın)"; exit 2; }

SURE=${OTO_SURE:-120}
if [[ "${1:-}" == "--menu" ]]; then
  OUT=${2:-$P/Logs/otoekran/menu}
  FLAGS=(-otoekran-menu "$OUT")
  SURE=${OTO_SURE:-60}
elif [[ "${1:-}" == "--harita" ]]; then
  SCENE=${2:?sahne adı gerekli}
  OUT=${3:-$P/Logs/otoekran/$SCENE}
  FLAGS=(-otoekran-harita "$SCENE" "$OUT")
else
  OUT=${1:-$P/Logs/otoekran/benchmark}
  FLAGS=(-otoekran "$OUT")
fi
mkdir -p "$OUT"
LOG="$OUT/player.log"
echo "Başlatılıyor: $BIN ${FLAGS[*]}"
"$BIN" "${FLAGS[@]}" -otoekran-sure "$SURE" -screen-fullscreen 0 -logFile "$LOG" &
PID=$!
LIMIT=$((SURE + 60))
for ((i = 0; i < LIMIT; i++)); do
  kill -0 $PID 2>/dev/null || break
  sleep 1
done
if kill -0 $PID 2>/dev/null; then
  echo "Zaman aşımı: oyuncu sonlandırılıyor"
  kill $PID 2>/dev/null; sleep 3; kill -9 $PID 2>/dev/null
fi
wait $PID 2>/dev/null
grep -qE "\[OTOEKRAN(-MENU)?\] bitti" "$LOG" 2>/dev/null && echo "[OTOEKRAN] bitti (log doğrulandı)" || echo "UYARI: log'da 'bitti' yok: $LOG"
echo "--- PNG'ler ($OUT) ---"
ls -la "$OUT"/*.png 2>/dev/null || echo "(PNG yok)"
[[ -f "$OUT/perf.csv" ]] && { echo "--- perf.csv ---"; cat "$OUT/perf.csv"; }
