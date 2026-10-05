#!/usr/bin/env bash
# Yerel kind/minikube geliştirme kümesi kurulumu
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROVIDER="${CLUSTER_PROVIDER:-kind}"
CLUSTER_NAME="${CLUSTER_NAME:-harekat-dev}"

need() {
  command -v "$1" >/dev/null 2>&1 || { echo "Gerekli araç yok: $1" >&2; exit 1; }
}

create_kind() {
  need kind
  need kubectl
  if kind get clusters 2>/dev/null | grep -qx "${CLUSTER_NAME}"; then
    echo "[dev-up] kind kümesi zaten var: ${CLUSTER_NAME}"
  else
    cat <<EOF | kind create cluster --name "${CLUSTER_NAME}" --config=-
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
nodes:
  - role: control-plane
    kubeadmConfigPatches:
      - |
        kind: InitConfiguration
        nodeRegistration:
          kubeletExtraArgs:
            node-labels: "ingress-ready=true"
    extraPortMappings:
      - containerPort: 80
        hostPort: 8080
        protocol: TCP
      - containerPort: 443
        hostPort: 8443
        protocol: TCP
  - role: worker
  - role: worker
EOF
  fi
  kubectl cluster-info --context "kind-${CLUSTER_NAME}"
}

create_minikube() {
  need minikube
  need kubectl
  if minikube status -p "${CLUSTER_NAME}" >/dev/null 2>&1; then
    echo "[dev-up] minikube profili zaten var: ${CLUSTER_NAME}"
  else
    minikube start -p "${CLUSTER_NAME}" --cpus=4 --memory=8192 --driver=docker
  fi
  kubectl config use-context "${CLUSTER_NAME}"
}

install_ingress() {
  echo "[dev-up] ingress-nginx kuruluyor..."
  kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.11.1/deploy/static/provider/kind/deploy.yaml \
    || kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.11.1/deploy/static/provider/cloud/deploy.yaml
  kubectl wait --namespace ingress-nginx \
    --for=condition=ready pod \
    --selector=app.kubernetes.io/component=controller \
    --timeout=180s || true
}

apply_stack() {
  echo "[dev-up] HAREKÂT manifest'leri uygulanıyor..."
  kubectl apply -f "${ROOT}/k8s/namespaces/namespace.yaml"
  kubectl apply -f "${ROOT}/k8s/secrets/secrets.dev.yaml"
  kubectl apply -f "${ROOT}/k8s/postgres/statefulset.yaml"
  kubectl apply -f "${ROOT}/k8s/redis/deployment.yaml"
  kubectl apply -f "${ROOT}/k8s/backend/deployment.yaml"
  kubectl apply -f "${ROOT}/k8s/backend/service.yaml"
  kubectl apply -f "${ROOT}/k8s/backend/hpa.yaml"
  kubectl apply -f "${ROOT}/k8s/ingress/ingress.yaml"
  kubectl apply -f "${ROOT}/k8s/multi-region/regions.yaml"
  kubectl apply -f "${ROOT}/k8s/overlays/blue/deployment.yaml"
  kubectl apply -f "${ROOT}/k8s/overlays/green/deployment.yaml"
  kubectl apply -f "${ROOT}/monitoring/prometheus/alerts.yaml" || true
  kubectl apply -f "${ROOT}/monitoring/grafana/dashboards/" || true
  echo "[dev-up] Agones Fleet (CRD yoksa atlanır)..."
  kubectl apply -f "${ROOT}/agones/fleet.yaml" 2>/dev/null || echo "[dev-up] Agones CRD yok — atlandı"
  kubectl apply -f "${ROOT}/agones/fleet-autoscaler.yaml" 2>/dev/null || true
}

main() {
  echo "[dev-up] sağlayıcı=${PROVIDER} küme=${CLUSTER_NAME}"
  case "${PROVIDER}" in
    kind) create_kind ;;
    minikube) create_minikube ;;
    *) echo "CLUSTER_PROVIDER=kind|minikube olmalı" >&2; exit 1 ;;
  esac
  install_ingress
  apply_stack
  echo "[dev-up] Tamam. API: kubectl -n harekat port-forward svc/harekat-backend 8080:80"
}

main "$@"
