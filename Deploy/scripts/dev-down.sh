#!/usr/bin/env bash
# Yerel küme yıkımı
set -euo pipefail
PROVIDER="${CLUSTER_PROVIDER:-kind}"
CLUSTER_NAME="${CLUSTER_NAME:-harekat-dev}"

case "${PROVIDER}" in
  kind)
    kind delete cluster --name "${CLUSTER_NAME}" || true
    ;;
  minikube)
    minikube delete -p "${CLUSTER_NAME}" || true
    ;;
esac
echo "[dev-down] ${PROVIDER}/${CLUSTER_NAME} silindi."
