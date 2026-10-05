#!/usr/bin/env bash
# HAREKÂT blue-green geri alma (rollback)
# Kullanım: ./rollback.sh
set -euo pipefail

NAMESPACE="${NAMESPACE:-harekat}"
ACTIVE_SVC="harekat-backend-active"
PREVIEW_SVC="harekat-backend-preview"

active="$(kubectl get svc "${ACTIVE_SVC}" -n "${NAMESPACE}" -o jsonpath='{.spec.selector.version}')"
if [[ -z "${active}" ]]; then
  echo "[RB] Aktif slot okunamadı" >&2
  exit 1
fi

if [[ "${active}" == "blue" ]]; then
  previous=green
else
  previous=blue
fi

echo "[RB] Aktif=${active} → önceki slot=${previous}"

# Önceki slotta replica var mı?
ready="$(kubectl get deploy "harekat-backend-${previous}" -n "${NAMESPACE}" -o jsonpath='{.status.readyReplicas}' 2>/dev/null || echo 0)"
if [[ "${ready:-0}" -lt 1 ]]; then
  echo "[RB] Önceki slot ölçekleniyor..."
  kubectl scale "deployment/harekat-backend-${previous}" --replicas=3 -n "${NAMESPACE}"
  kubectl rollout status "deployment/harekat-backend-${previous}" -n "${NAMESPACE}" --timeout=180s
fi

kubectl patch svc "${ACTIVE_SVC}" -n "${NAMESPACE}" --type merge \
  -p "{\"spec\":{\"selector\":{\"app\":\"harekat-backend\",\"version\":\"${previous}\"}}}"
kubectl patch svc "${PREVIEW_SVC}" -n "${NAMESPACE}" --type merge \
  -p "{\"spec\":{\"selector\":{\"app\":\"harekat-backend\",\"version\":\"${active}\"}}}"

echo "[RB] Trafik ${previous} slotuna alındı. Başarısız slot (${active}) isteğe bağlı kapatılabilir:"
echo "     kubectl scale deployment/harekat-backend-${active} --replicas=0 -n ${NAMESPACE}"
