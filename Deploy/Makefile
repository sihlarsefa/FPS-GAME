.PHONY: help dev-up dev-down validate cost-sim blue-green rollback apply-prod test charts

ROOT := $(dir $(abspath $(lastword $(MAKEFILE_LIST))))
CLUSTER_PROVIDER ?= kind
CLUSTER_NAME ?= harekat-dev
export CLUSTER_PROVIDER CLUSTER_NAME

help:
	@echo "HAREKÂT Deploy hedefleri:"
	@echo "  make dev-up       — kind/minikube + stack (CLUSTER_PROVIDER=kind|minikube)"
	@echo "  make dev-down     — yerel küme sil"
	@echo "  make validate     — manifest / Dockerfile kontrolleri"
	@echo "  make cost-sim     — maliyet simülasyon CSV"
	@echo "  make test         — doğrulama + kapasite birim testleri"
	@echo "  make blue-green TAG=v1.2.3"
	@echo "  make rollback"

dev-up:
	@chmod +x $(ROOT)scripts/*.sh
	@$(ROOT)scripts/dev-up.sh

dev-down:
	@chmod +x $(ROOT)scripts/*.sh
	@$(ROOT)scripts/dev-down.sh

validate:
	@chmod +x $(ROOT)scripts/*.sh
	@$(ROOT)scripts/validate.sh

cost-sim:
	@chmod +x $(ROOT)scripts/*.sh
	@$(ROOT)scripts/cost-sim.sh

blue-green:
	@test -n "$(TAG)" || (echo "TAG=... gerekli" >&2; exit 1)
	@$(ROOT)scripts/blue-green-deploy.sh $(TAG)

rollback:
	@$(ROOT)scripts/rollback.sh

apply-prod:
	kubectl apply -k $(ROOT)k8s
	kubectl apply -f $(ROOT)agones/
	kubectl apply -f $(ROOT)backup/pitr-cronjobs.yaml
	kubectl apply -f $(ROOT)monitoring/prometheus/alerts.yaml

test: validate
	@python3 $(ROOT)scripts/test_capacity.py
	@$(ROOT)scripts/cost-sim.sh >/dev/null
	@test -s $(ROOT)docs/cost-simulation.csv
	@echo "[test] tüm kontroller geçti"
