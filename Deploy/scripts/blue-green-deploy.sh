#!/usr/bin/env bash
# HAREKÂT blue-green dağıtım
# Kullanım: ./blue-green-deploy.sh <image-tag> [blue|green]
set -euo pipefail

NAMESPACE="${NAMESPACE:-harekat}"
IMAGE_TAG="${1:?image tag gerekli (örn: v1.2.3)}"
TARGET_SLOT="${2:-}"
IMAGE_REPO="${IMAGE_REPO:-ghcr.io/harekat/backend}"

ACTIVE_SVC="harekat-backend-active"
PREVIEW_SVC="harekat-backend-preview"

get_active_slot() {
  kubectl get svc "${ACTIVE_SVC}" -n "${NAMESPACE}" \
    -o jsonpath='{.spec.selector.version}'
}

health_check() {
  local slot="$1"
  local deploy="harekat-backend-${slot}"
  echo "[BG] ${deploy} hazır olana kadar bekleniyor..."
  kubectl rollout status "deployment/${deploy}" -n "${NAMESPACE}" --timeout=300s
  local ready
  ready="$(kubectl get deploy "${deploy}" -n "${NAMESPACE}" -o jsonpath='{.status.readyReplicas}')"
  if [[ "${ready:-0}" -lt 1 ]]; then
    echo "[BG] HATA: ${deploy} ready replica yok" >&2
    return 1
  fi
  # Preview servisi üzerinden smoke
  kubectl run "harekat-smoke-${RANDOM}" -n "${NAMESPACE}" --rm -i --restart=Never \
    --image=curlimages/curl:8.5.0 -- \
    curl -sf "http://${PREVIEW_SVC}/health" >/dev/null \
    || curl -sf "http://harekat-backend-${slot}.${NAMESPACE}.svc.cluster.local/health" >/dev/null
}

switch_traffic() {
  local new_slot="$1"
  echo "[BG] Trafik ${new_slot} slotuna alınıyor..."
  kubectl patch svc "${ACTIVE_SVC}" -n "${NAMESPACE}" --type merge \
    -p "{\"spec\":{\"selector\":{\"app\":\"harekat-backend\",\"version\":\"${new_slot}\"}}}"
  local other
  if [[ "${new_slot}" == "blue" ]]; then other=green; else other=blue; fi
  kubectl patch svc "${PREVIEW_SVC}" -n "${NAMESPACE}" --type merge \
    -p "{\"spec\":{\"selector\":{\"app\":\"harekat-backend\",\"version\":\"${other}\"}}}"
}

main() {
  local active preview
  active="$(get_active_slot)"
  if [[ -z "${active}" ]]; then active=blue; fi
  if [[ "${active}" == "blue" ]]; then preview=green; else preview=blue; fi

  if [[ -n "${TARGET_SLOT}" ]]; then
    preview="${TARGET_SLOT}"
  fi

  echo "[BG] Aktif=${active} Hedef(preview)=${preview} Image=${IMAGE_REPO}:${IMAGE_TAG}"

  kubectl set image "deployment/harekat-backend-${preview}" \
    "api=${IMAGE_REPO}:${IMAGE_TAG}" -n "${NAMESPACE}"
  kubectl scale "deployment/harekat-backend-${preview}" --replicas=3 -n "${NAMESPACE}"

  if ! health_check "${preview}"; then
    echo "[BG] Sağlık kontrolü başarısız — trafik değiştirilmedi. Rollback gerekmez (aktif=${active})." >&2
    kubectl scale "deployment/harekat-backend-${preview}" --replicas=0 -n "${NAMESPACE}" || true
    exit 1
  fi

  switch_traffic "${preview}"
  echo "[BG] Başarılı. Yeni aktif=${preview}. Eski slot (${active}) 10 dk sonra ölçeklenebilir."
  echo "[BG] Geri alma: ./rollback.sh"
}

main "$@"
